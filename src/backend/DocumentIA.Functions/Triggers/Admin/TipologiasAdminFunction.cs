using System.Net;
using System.Text.Json;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Extensions;
using DocumentIA.Core.Mappers;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Triggers.Admin;

public class TipologiasAdminFunction
{
    private static readonly Regex CodigoRegex = new("^[a-z0-9][a-z0-9_.-]{2,99}$", RegexOptions.Compiled);
    private static readonly Regex VersionRegex = new("^\\d+\\.\\d+(\\.\\d+)?(-[0-9A-Za-z-.]+)?(\\+[0-9A-Za-z-.]+)?$", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedFieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "decimal", "number", "integer", "int", "date", "datetime", "boolean", "bool", "array", "object"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions JsonIndentedOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private const int MaxImportZipBytes = 20 * 1024 * 1024;
    private const int MaxImportZipEntries = 20;
    private const int MaxImportEntryChars = 2 * 1024 * 1024;

    private readonly DocumentIADbContext _dbContext;
    private readonly ITipologiaRepository _tipologiaRepository;
    private readonly ITipologiaConfigAuditRepository _tipologiaAuditRepository;
    private readonly ILogger<TipologiasAdminFunction> _logger;
    private readonly IMemoryCache _cache;
    private readonly TipologiaMapper _mapper;

    public TipologiasAdminFunction(
        DocumentIADbContext dbContext,
        ITipologiaRepository tipologiaRepository,
        ITipologiaConfigAuditRepository tipologiaAuditRepository,
        ILogger<TipologiasAdminFunction> logger,
        IMemoryCache cache,
        TipologiaMapper mapper)
    {
        _dbContext = dbContext;
        _tipologiaRepository = tipologiaRepository;
        _tipologiaAuditRepository = tipologiaAuditRepository;
        _logger = logger;
        _cache = cache;
        _mapper = mapper;
    }

    [Function("Admin_GetTipologias")]
    public async Task<HttpResponseData> GetTipologias(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/tipologias")] HttpRequestData req)
    {
        var tipologias = await _dbContext.Tipologias
            .OrderBy(t => t.Nombre)
            .ToListAsync();

        // Convert to clean DTOs (AB#99735: omit deprecated fields)
        var dtos = _mapper.ToResponseDtos(tipologias);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(dtos);
        return response;
    }

    [Function("Admin_GetTipologiaById")]
    public async Task<HttpResponseData> GetTipologiaById(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/tipologias/{id:int}")] HttpRequestData req,
        int id)
    {
        var tipologia = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);
        if (tipologia is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        // Convert to clean DTO (AB#99735: omit deprecated fields)
        var dto = _mapper.ToResponseDto(tipologia);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(dto);
        return response;
    }

    [Function("Admin_CreateTipologia")]
    public async Task<HttpResponseData> CreateTipologia(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/tipologias")] HttpRequestData req)
    {
        var payload = await ReadBody<TipologiaUpsertRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido.");
        }

        if (string.IsNullOrWhiteSpace(payload.Codigo) || string.IsNullOrWhiteSpace(payload.Nombre) || string.IsNullOrWhiteSpace(payload.Version))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Codigo, Nombre y Version son obligatorios.");
        }

        if (!TryValidateConfig(payload.ConfiguracionJson, out var configError, out var config))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, configError!);
        }

        var validationError = await ValidateTipologiaRequestAsync(payload, config!, null);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, validationError);
        }

        var exists = await _dbContext.Tipologias.AnyAsync(t => t.Codigo == payload.Codigo);
        if (exists)
        {
            return await CreateError(req, HttpStatusCode.Conflict, $"Ya existe una tipologia con codigo '{payload.Codigo}'.");
        }

        var entity = new TipologiaEntity
        {
            Codigo = payload.Codigo.Trim(),
            Nombre = payload.Nombre.Trim(),
            Version = payload.Version.Trim(),
            Activa = true,
            Estado = EstadoTipologia.Draft,
            ConfiguracionJson = payload.ConfiguracionJson,
            CreadoPor = payload.Usuario ?? "SYSTEM",
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = DateTime.UtcNow
        };

        await _tipologiaRepository.AddAsync(entity, payload.Usuario ?? "SYSTEM");

        // Convert to clean DTO (AB#99735: omit deprecated fields)
        var dto = _mapper.ToResponseDto(entity);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(dto);
        return response;
    }

    [Function("Admin_UpdateTipologia")]
    public async Task<HttpResponseData> UpdateTipologia(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "management/tipologias/{id:int}")] HttpRequestData req,
        int id)
    {
        var entity = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        if (entity.Estado == EstadoTipologia.Published)
        {
              return await CreateError(req, HttpStatusCode.Conflict, "Solo se pueden editar tipologias en estado Draft o Retired.");
        }

        var payload = await ReadBody<TipologiaUpsertRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido.");
        }

        if (string.IsNullOrWhiteSpace(payload.Codigo) || string.IsNullOrWhiteSpace(payload.Nombre) || string.IsNullOrWhiteSpace(payload.Version))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Codigo, Nombre y Version son obligatorios.");
        }

        if (!TryValidateConfig(payload.ConfiguracionJson, out var configError, out var config))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, configError!);
        }

        var validationError = await ValidateTipologiaRequestAsync(payload, config!, entity);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, validationError);
        }

        entity.Nombre = payload.Nombre?.Trim() ?? entity.Nombre;
        entity.Version = payload.Version?.Trim() ?? entity.Version;
        entity.ConfiguracionJson = payload.ConfiguracionJson;
        entity.FechaActualizacion = DateTime.UtcNow;

        await _tipologiaRepository.UpdateAsync(entity, payload.Usuario ?? "SYSTEM", "Updated");

        // Convert to clean DTO (AB#99735: omit deprecated fields)
        var dto = _mapper.ToResponseDto(entity);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(dto);
        return response;
    }

    [Function("Admin_PublicarTipologia")]
    public async Task<HttpResponseData> PublicarTipologia(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/tipologias/{id:int}/publicar")] HttpRequestData req,
        int id)
    {
        var entity = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        if (!TryValidateConfig(entity.ConfiguracionJson, out var configError, out var config))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, configError!);
        }

        var modelValidation = await ValidateReferencedModels(config!);
        if (!string.IsNullOrEmpty(modelValidation))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, modelValidation);
        }

        var payload = await ReadBody<PublicarTipologiaRequest>(req);
        await _tipologiaRepository.PublicarAsync(id, payload?.Usuario ?? "SYSTEM");

        // Invalidar caché del resolver para que la nueva tipología publicada esté disponible de inmediato
        _cache.Remove("tipologias:snapshot");

        var updated = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);
        
        // Convert to clean DTO (AB#99735: omit deprecated fields)
        var dto = _mapper.ToResponseDto(updated!);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(dto);
        return response;
    }

    [Function("Admin_RetirarTipologia")]
    public async Task<HttpResponseData> RetirarTipologia(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/tipologias/{id:int}/retirar")] HttpRequestData req,
        int id)
    {
        var exists = await _dbContext.Tipologias.AnyAsync(t => t.Id == id);
        if (!exists)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        var payload = await ReadBody<RetirarTipologiaRequest>(req);
        await _tipologiaRepository.RetirarAsync(id, payload?.Usuario ?? "SYSTEM");
        var updated = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);

        // Convert to clean DTO (AB#99735: omit deprecated fields)
        var dto = _mapper.ToResponseDto(updated!);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(dto);
        return response;
    }

    [Function("Admin_PasarTipologiaADraft")]
    public async Task<HttpResponseData> PasarTipologiaADraft(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/tipologias/{id:int}/draft")] HttpRequestData req,
        int id)
    {
        var entity = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        var payload = await ReadBody<PasarTipologiaADraftRequest>(req);
        await _tipologiaRepository.PasarADraftAsync(id, payload?.Usuario ?? "SYSTEM");

        entity = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);

        // Convert to clean DTO (AB#99735: omit deprecated fields)
        var dto = _mapper.ToResponseDto(entity!);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(dto);
        return response;
    }

    [Function("Admin_GetTipologiaAudit")]
    public async Task<HttpResponseData> GetTipologiaAudit(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/tipologias/{id:int}/audit")] HttpRequestData req,
        int id)
    {
        var exists = await _dbContext.Tipologias.AnyAsync(t => t.Id == id);
        if (!exists)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        var take = 200;
        var query = req.Url.Query;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var parts = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0].Equals("take", StringComparison.OrdinalIgnoreCase) && int.TryParse(Uri.UnescapeDataString(kv[1]), out var parsedTake))
                {
                    take = parsedTake;
                    break;
                }
            }
        }

        var rows = await _tipologiaAuditRepository.GetByTipologiaIdAsync(id, take);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(rows.Select(x => new
        {
            x.Id,
            x.TipologiaId,
            x.Accion,
            x.Usuario,
            x.FechaHora,
            x.DetallesJson
        }));
        return response;
    }

    [Function("Admin_GetTipologiaVersions")]
    public async Task<HttpResponseData> GetTipologiaVersions(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/tipologias/{id:int}/versions")] HttpRequestData req,
        int id)
    {
        var current = await _dbContext.Tipologias.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (current is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        var currentFamily = GetTipologiaFamily(current);
        if (string.IsNullOrWhiteSpace(currentFamily))
        {
            var fallbackResponse = req.CreateResponse(HttpStatusCode.OK);
            await fallbackResponse.WriteAsJsonAsync(new[]
            {
                new
                {
                    current.Id,
                    current.Codigo,
                    current.Nombre,
                    current.Version,
                    current.Estado,
                    Family = current.Codigo,
                    IsCurrent = true
                }
            });
            return fallbackResponse;
        }

        var all = await _dbContext.Tipologias
            .AsNoTracking()
            .OrderBy(t => t.Nombre)
            .ThenBy(t => t.Version)
            .ToListAsync();

        var versions = all
            .Select(t => new
            {
                Entity = t,
                Family = GetTipologiaFamily(t)
            })
            .Where(x => string.Equals(x.Family, currentFamily, StringComparison.OrdinalIgnoreCase))
            .Select(x => new
            {
                x.Entity.Id,
                x.Entity.Codigo,
                x.Entity.Nombre,
                x.Entity.Version,
                x.Entity.Estado,
                Family = x.Family,
                IsCurrent = x.Entity.Id == current.Id
            })
            .ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(versions);
        return response;
    }

    [Function("Admin_GetTipologiaDiff")]
    public async Task<HttpResponseData> GetTipologiaDiff(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/tipologias/{id:int}/diff/{otherId:int}")] HttpRequestData req,
        int id,
        int otherId)
    {
        var left = await _dbContext.Tipologias.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        var right = await _dbContext.Tipologias.AsNoTracking().FirstOrDefaultAsync(t => t.Id == otherId);

        if (left is null || right is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, "No existe una de las tipologias a comparar.");
        }

        var leftFamily = GetTipologiaFamily(left);
        var rightFamily = GetTipologiaFamily(right);
        if (!string.IsNullOrWhiteSpace(leftFamily) && !string.IsNullOrWhiteSpace(rightFamily)
            && !string.Equals(leftFamily, rightFamily, StringComparison.OrdinalIgnoreCase))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Las tipologias no pertenecen a la misma familia.");
        }

        var leftPlugins = await _dbContext.PluginTipologiaConfigs.AsNoTracking().FirstOrDefaultAsync(p => p.TipologiaCodigo == left.Codigo);
        var rightPlugins = await _dbContext.PluginTipologiaConfigs.AsNoTracking().FirstOrDefaultAsync(p => p.TipologiaCodigo == right.Codigo);

        var changes = new List<DiffChange>();
        AddJsonSectionDiff(changes, "validation", left.ConfiguracionJson, right.ConfiguracionJson);
        AddJsonSectionDiff(changes, "plugins", leftPlugins?.ConfiguracionJson, rightPlugins?.ConfiguracionJson);
        
        var leftPrompt = left.GetSystemPrompt();
        var rightPrompt = right.GetSystemPrompt();
        AddJsonSectionDiff(changes, "prompt", ToPromptJson(leftPrompt), ToPromptJson(rightPrompt));

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            Left = new { left.Id, left.Codigo, left.Nombre, left.Version, Family = leftFamily },
            Right = new { right.Id, right.Codigo, right.Nombre, right.Version, Family = rightFamily },
            Changes = changes,
            TotalChanges = changes.Count,
            Added = changes.Count(c => c.ChangeType == "added"),
            Removed = changes.Count(c => c.ChangeType == "removed"),
            Modified = changes.Count(c => c.ChangeType == "modified")
        });
        return response;
    }

    [Function("Admin_ExportTipologia")]
    [Obsolete("Use direct GET /management/tipologias/{id} + ConfiguracionJson instead. Deprecated 2026-06-04.", false)]
    public async Task<HttpResponseData> ExportTipologia(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/tipologias/{id:int}/export")] HttpRequestData req,
        int id)
    {
        _logger.LogWarning("Deprecated endpoint: Admin_ExportTipologia called. Use GET /management/tipologias/{id} instead.");
        
        var tipologia = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);
        if (tipologia is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        var pluginConfig = await _dbContext.PluginTipologiaConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TipologiaCodigo == tipologia.Codigo);

        var manifest = new TipologiaExportManifest
        {
            Codigo = tipologia.Codigo,
            Nombre = tipologia.Nombre,
            Version = tipologia.Version,
            ExportedAtUtc = DateTime.UtcNow,
            IncludesPlugins = pluginConfig is not null,
            IncludesPrompt = !string.IsNullOrWhiteSpace(tipologia.GetSystemPrompt()),
            Source = "DocumentIA.AdminAPI"
        };

        var zipBytes = BuildTipologiaExportZip(tipologia, pluginConfig, manifest);

        await _tipologiaAuditRepository.AddAsync(new TipologiaConfigAuditEntity
        {
            TipologiaId = tipologia.Id,
            Accion = "Exported",
            Usuario = "SYSTEM",
            FechaHora = DateTime.UtcNow,
            DetallesJson = JsonSerializer.Serialize(new
            {
                manifest.Codigo,
                manifest.Version,
                manifest.ExportedAtUtc,
                manifest.IncludesPlugins,
                manifest.IncludesPrompt
            })
        });

        var fileName = $"tipologia-{tipologia.Codigo}-v{tipologia.Version}.zip";
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/zip");
        response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
        await response.Body.WriteAsync(zipBytes, 0, zipBytes.Length);
        return response;
    }

    [Function("Admin_ImportTipologia")]
    [Obsolete("Import/Export deprecated. Use POST /management/tipologias with ConfiguracionJson. Deprecated 2026-06-04.", false)]
    public async Task<HttpResponseData> ImportTipologia(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/tipologias/import")] HttpRequestData req)
    {
        _logger.LogWarning("Deprecated endpoint: Admin_ImportTipologia called. Use POST /management/tipologias instead.");
        
        var payload = await ReadBody<TipologiaImportRequest>(req);
        if (payload is null || string.IsNullOrWhiteSpace(payload.ZipBase64))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido: ZipBase64 es obligatorio.");
        }

        byte[] zipBytes;
        try
        {
            zipBytes = Convert.FromBase64String(payload.ZipBase64);
        }
        catch (Exception)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "ZipBase64 invalido.");
        }

        if (zipBytes.Length == 0)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "ZIP vacio.");
        }

        if (zipBytes.Length > MaxImportZipBytes)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, $"ZIP excede el maximo permitido ({MaxImportZipBytes / (1024 * 1024)} MB).");
        }

        if (!TryReadTipologiaImportZip(zipBytes, out var manifest, out var validationJson, out var pluginsJson, out var promptGpt, out var zipError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, zipError!);
        }

        if (!TryValidateConfig(validationJson, out var configError, out var config))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, configError!);
        }

        var codigo = manifest?.Codigo?.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            codigo = config!.TipologiaId?.Trim();
        }

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "El paquete no contiene Codigo de tipologia.");
        }

        var nombre = manifest?.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = codigo;
        }

        var version = manifest?.Version?.Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            version = config!.Version?.Trim();
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "El paquete no contiene Version de tipologia.");
        }

        if (!string.IsNullOrWhiteSpace(config?.TipologiaId)
            && !string.Equals(config.TipologiaId.Trim(), codigo, StringComparison.OrdinalIgnoreCase))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Inconsistencia en paquete: manifest.Codigo y configuracion.tipologiaId no coinciden.");
        }

        if (!string.IsNullOrWhiteSpace(config?.Version)
            && !string.Equals(config.Version.Trim(), version, StringComparison.OrdinalIgnoreCase))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Inconsistencia en paquete: manifest.Version y configuracion.version no coinciden.");
        }

        var exists = await _dbContext.Tipologias.AnyAsync(t => t.Codigo == codigo);
        if (exists)
        {
            return await CreateError(req, HttpStatusCode.Conflict, $"Ya existe una tipologia con codigo '{codigo}'.");
        }

        if (!string.IsNullOrWhiteSpace(pluginsJson))
        {
            if (!TryValidatePluginsConfig(pluginsJson, codigo, out var pluginsError))
            {
                return await CreateError(req, HttpStatusCode.BadRequest, pluginsError!);
            }
        }

        var usuario = payload.Usuario ?? "SYSTEM";
        var entity = new TipologiaEntity
        {
            Codigo = codigo,
            Nombre = nombre,
            Version = version,
            Activa = true,
            Estado = EstadoTipologia.Draft,
            ConfiguracionJson = validationJson,
            PromptGPT = promptGpt,
            CreadoPor = usuario,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = DateTime.UtcNow
        };

        await _tipologiaRepository.AddAsync(entity, usuario);

        if (!string.IsNullOrWhiteSpace(pluginsJson))
        {
            _dbContext.PluginTipologiaConfigs.Add(new PluginTipologiaConfigEntity
            {
                TipologiaCodigo = codigo,
                ConfiguracionJson = pluginsJson,
                Estado = EstadoPluginConfig.Draft,
                FechaCreacion = DateTime.UtcNow,
                FechaActualizacion = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }

        await _tipologiaAuditRepository.AddAsync(new TipologiaConfigAuditEntity
        {
            TipologiaId = entity.Id,
            Accion = "Imported",
            Usuario = usuario,
            FechaHora = DateTime.UtcNow,
            DetallesJson = JsonSerializer.Serialize(new
            {
                entity.Codigo,
                entity.Version,
                Source = manifest?.Source,
                ImportedAtUtc = DateTime.UtcNow,
                HasPlugins = !string.IsNullOrWhiteSpace(pluginsJson),
                HasPrompt = !string.IsNullOrWhiteSpace(promptGpt)
            })
        });

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(entity);
        return response;
    }

    private async Task<string?> ValidateReferencedModels(TipologiaValidationConfig config)
    {
        if (config.Extraction.Enabled)
        {
            if (string.IsNullOrWhiteSpace(config.Extraction.ModelKey))
            {
                return "Extraction habilitado pero modelKey vacio.";
            }

            var extractionExists = await _dbContext.ModeloConfigs.AnyAsync(m =>
                m.Tipo == TipoModelo.Extraccion &&
                m.Key == config.Extraction.ModelKey &&
                m.Activo);

            if (!extractionExists)
            {
                return $"No existe modelo activo de extraccion con key '{config.Extraction.ModelKey}'.";
            }
        }

        if (config.PromptConfig?.Enabled == true)
        {
            if (string.IsNullOrWhiteSpace(config.PromptConfig.ModelKey))
            {
                return "Prompt habilitado pero modelKey vacio.";
            }

            var promptExists = await _dbContext.ModeloConfigs.AnyAsync(m =>
                m.Tipo == TipoModelo.Prompt &&
                m.Key == config.PromptConfig.ModelKey &&
                m.Activo);

            if (!promptExists)
            {
                return $"No existe modelo activo de prompt con key '{config.PromptConfig.ModelKey}'.";
            }
        }

        return null;
    }

    private async Task<string?> ValidateTipologiaRequestAsync(
        TipologiaUpsertRequest payload,
        TipologiaValidationConfig config,
        TipologiaEntity? existingEntity)
    {
        var codigo = payload.Codigo?.Trim() ?? string.Empty;
        var nombre = payload.Nombre?.Trim() ?? string.Empty;
        var version = payload.Version?.Trim() ?? string.Empty;

        if (!CodigoRegex.IsMatch(codigo))
        {
            return "Codigo debe tener entre 3 y 100 caracteres y usar solo minúsculas, números, punto, guion o guion bajo.";
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "Nombre es obligatorio.";
        }

        if (!VersionRegex.IsMatch(version))
        {
            return "Version debe usar formato tipo 1.0 o 1.0.0.";
        }

        if (existingEntity is not null
            && !string.Equals(existingEntity.Codigo, codigo, StringComparison.OrdinalIgnoreCase))
        {
            return "Codigo no puede modificarse tras el primer guardado.";
        }

        if (!string.Equals(config.TipologiaId?.Trim(), codigo, StringComparison.OrdinalIgnoreCase))
        {
            return "La configuracion debe incluir un tipologiaId que coincida con Codigo.";
        }

        if (!string.Equals(config.Version?.Trim(), version, StringComparison.OrdinalIgnoreCase))
        {
            return "La configuracion debe incluir una version que coincida con Version.";
        }

        var configError = ValidateBusinessRules(config);
        if (!string.IsNullOrWhiteSpace(configError))
        {
            return configError;
        }

        return await ValidateReferencedModels(config);
    }

    private static string? ValidateBusinessRules(TipologiaValidationConfig config)
    {
        if (config.ConfidenceConfig is not null)
        {
            if (!IsProbability(config.ConfidenceConfig.ClasifUmbralFallback)
                || !IsProbability(config.ConfidenceConfig.ExtracWeightCampos)
                || !IsProbability(config.ConfidenceConfig.ExtracWeightRequeridos)
                || !IsProbability(config.ConfidenceConfig.ExtracWeightWarnings)
                || !IsProbability(config.ConfidenceConfig.UmbralOK)
                || !IsProbability(config.ConfidenceConfig.UmbralRevision)
                || !IsNullableProbability(config.ConfidenceConfig.ExtracUmbralFallback)
                || !IsNullableProbability(config.ConfidenceConfig.ExtracUmbralFallbackCompletitud)
                || !IsNullableProbability(config.ConfidenceConfig.ExtracUmbralFallbackConfianza))
            {
                return "Los umbrales y pesos de confidenceConfig deben estar entre 0 y 1.";
            }

            if (config.ConfidenceConfig.UmbralRevision > config.ConfidenceConfig.UmbralOK)
            {
                return "confidenceConfig.umbralRevision no puede ser mayor que confidenceConfig.umbralOK.";
            }
        }

        if (config.Extraction.Enabled)
        {
            if (string.IsNullOrWhiteSpace(config.Extraction.Provider))
            {
                return "extraction.provider es obligatorio cuando extraction.enabled=true.";
            }

            if (string.IsNullOrWhiteSpace(config.Extraction.ModelKey))
            {
                return "extraction.modelKey es obligatorio cuando extraction.enabled=true.";
            }
        }

        if (!config.ResolvedSkipGDCUpload)
        {
            if (string.IsNullOrWhiteSpace(config.ResolvedGdcTipo))
            {
                return "gdcTipoDocumento es obligatorio cuando skipGDCUpload=false.";
            }

            if (string.IsNullOrWhiteSpace(config.ResolvedGdcSerie))
            {
                return "gdcSerie es obligatorio cuando skipGDCUpload=false.";
            }
        }

        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in config.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                return "fields.name es obligatorio.";
            }

            if (!fieldNames.Add(field.Name.Trim()))
            {
                return $"fields contiene nombres duplicados: {field.Name}.";
            }

            if (string.IsNullOrWhiteSpace(field.Type) || !SupportedFieldTypes.Contains(field.Type.Trim()))
            {
                return $"fields[{field.Name}].type no soportado: {field.Type}.";
            }

            if (string.Equals(field.Type.Trim(), "array", StringComparison.OrdinalIgnoreCase))
            {
                if (field.Items is null || string.IsNullOrWhiteSpace(field.Items.Type))
                {
                    return $"fields[{field.Name}] de tipo array requiere items.type.";
                }
            }
        }

        var mappedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in config.Extraction.FieldMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.TargetField))
            {
                return "extraction.fieldMappings.targetField es obligatorio.";
            }

            if (string.IsNullOrWhiteSpace(mapping.SourcePath))
            {
                return $"extraction.fieldMappings[{mapping.TargetField}].sourcePath es obligatorio.";
            }

            if (!mappedTargets.Add(mapping.TargetField.Trim()))
            {
                return $"extraction.fieldMappings contiene targetField duplicado: {mapping.TargetField}.";
            }

            if (fieldNames.Count > 0 && !fieldNames.Contains(mapping.TargetField.Trim()))
            {
                return $"extraction.fieldMappings referencia un field inexistente: {mapping.TargetField}.";
            }
        }

        return null;
    }

    private static bool IsProbability(double value) => value is >= 0 and <= 1;

    private static bool IsNullableProbability(double? value) => !value.HasValue || IsProbability(value.Value);

    private static bool TryValidateConfig(string? configJson, out string? error)
    {
        var ok = TryValidateConfig(configJson, out error, out _);
        return ok;
    }

    private static string GetTipologiaFamily(TipologiaEntity tipologia)
    {
        if (string.IsNullOrWhiteSpace(tipologia.ConfiguracionJson))
        {
            return tipologia.Codigo;
        }

        try
        {
            var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(tipologia.ConfiguracionJson, JsonOptions);
            if (!string.IsNullOrWhiteSpace(config?.TipologiaId))
            {
                return config.TipologiaId.Trim();
            }
        }
        catch
        {
            // ignore and fallback to codigo
        }

        return tipologia.Codigo;
    }

    private static string ToPromptJson(string? prompt)
    {
        return JsonSerializer.Serialize(new { promptGPT = prompt ?? string.Empty }, JsonIndentedOptions);
    }

    private static void AddJsonSectionDiff(List<DiffChange> changes, string section, string? leftJson, string? rightJson)
    {
        JsonNode? left;
        JsonNode? right;

        try
        {
            left = string.IsNullOrWhiteSpace(leftJson) ? null : JsonNode.Parse(leftJson);
        }
        catch
        {
            left = JsonValue.Create(leftJson ?? string.Empty);
        }

        try
        {
            right = string.IsNullOrWhiteSpace(rightJson) ? null : JsonNode.Parse(rightJson);
        }
        catch
        {
            right = JsonValue.Create(rightJson ?? string.Empty);
        }

        CompareNodes(changes, section, "$", left, right);
    }

    private static void CompareNodes(List<DiffChange> changes, string section, string path, JsonNode? left, JsonNode? right)
    {
        if (left is null && right is null)
        {
            return;
        }

        if (left is null)
        {
            changes.Add(new DiffChange(section, path, "added", null, NodeToString(right)));
            return;
        }

        if (right is null)
        {
            changes.Add(new DiffChange(section, path, "removed", NodeToString(left), null));
            return;
        }

        if (left is JsonObject leftObj && right is JsonObject rightObj)
        {
            var keys = leftObj.Select(k => k.Key)
                .Union(rightObj.Select(k => k.Key), StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal);

            foreach (var key in keys)
            {
                var childPath = $"{path}.{key}";
                CompareNodes(changes, section, childPath, leftObj[key], rightObj[key]);
            }
            return;
        }

        if (left is JsonArray leftArr && right is JsonArray rightArr)
        {
            var max = Math.Max(leftArr.Count, rightArr.Count);
            for (var i = 0; i < max; i++)
            {
                var childPath = $"{path}[{i}]";
                var leftNode = i < leftArr.Count ? leftArr[i] : null;
                var rightNode = i < rightArr.Count ? rightArr[i] : null;
                CompareNodes(changes, section, childPath, leftNode, rightNode);
            }
            return;
        }

        var leftString = NodeToString(left);
        var rightString = NodeToString(right);
        if (!string.Equals(leftString, rightString, StringComparison.Ordinal))
        {
            changes.Add(new DiffChange(section, path, "modified", leftString, rightString));
        }
    }

    private static string? NodeToString(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        return node.ToJsonString(JsonIndentedOptions);
    }

    private static bool TryValidatePluginsConfig(string pluginsJson, string tipologiaCodigo, out string? error)
    {
        try
        {
            var config = JsonSerializer.Deserialize<DocumentIA.Plugins.Integration.PluginConfiguration>(pluginsJson, JsonOptions);
            if (config is null)
            {
                error = "No se pudo deserializar la configuracion de plugins.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.TipologiaId))
            {
                config.TipologiaId = tipologiaCodigo;
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Configuracion de plugins invalida: {ex.Message}";
            return false;
        }
    }

    private static byte[] BuildTipologiaExportZip(
        TipologiaEntity tipologia,
        PluginTipologiaConfigEntity? pluginConfig,
        TipologiaExportManifest manifest)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(archive, "manifest.json", JsonSerializer.Serialize(manifest, JsonIndentedOptions));
            AddZipEntry(archive, "tipologia.validation.json", PrettyJson(tipologia.ConfiguracionJson));

            if (pluginConfig is not null)
            {
                AddZipEntry(archive, "tipologia.plugins.json", PrettyJson(pluginConfig.ConfiguracionJson));
            }

            var systemPrompt = tipologia.GetSystemPrompt();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                AddZipEntry(archive, "tipologia.prompt.json", JsonSerializer.Serialize(new
                {
                    promptGPT = systemPrompt
                }, JsonIndentedOptions));
            }
        }

        return ms.ToArray();
    }

    private static bool TryReadTipologiaImportZip(
        byte[] zipBytes,
        out TipologiaExportManifest? manifest,
        out string validationJson,
        out string? pluginsJson,
        out string? promptGpt,
        out string? error)
    {
        manifest = null;
        validationJson = string.Empty;
        pluginsJson = null;
        promptGpt = null;

        try
        {
            using var ms = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);

            if (archive.Entries.Count > MaxImportZipEntries)
            {
                error = $"ZIP invalido: contiene demasiadas entradas (max {MaxImportZipEntries}).";
                return false;
            }

            var manifestEntry = archive.GetEntry("manifest.json");
            var validationEntry = archive.GetEntry("tipologia.validation.json");
            var pluginsEntry = archive.GetEntry("tipologia.plugins.json");
            var promptEntry = archive.GetEntry("tipologia.prompt.json");

            if (manifestEntry is null)
            {
                error = "ZIP invalido: falta manifest.json.";
                return false;
            }

            if (validationEntry is null)
            {
                error = "ZIP invalido: falta tipologia.validation.json.";
                return false;
            }

            var manifestText = ReadZipEntryAsString(manifestEntry);
            if (manifestText.Length > MaxImportEntryChars)
            {
                error = "ZIP invalido: manifest.json excede el tamano permitido.";
                return false;
            }
            manifest = JsonSerializer.Deserialize<TipologiaExportManifest>(manifestText, JsonOptions);

            validationJson = ReadZipEntryAsString(validationEntry);
            if (validationJson.Length > MaxImportEntryChars)
            {
                error = "ZIP invalido: tipologia.validation.json excede el tamano permitido.";
                return false;
            }

            if (pluginsEntry is not null)
            {
                pluginsJson = ReadZipEntryAsString(pluginsEntry);
                if (pluginsJson.Length > MaxImportEntryChars)
                {
                    error = "ZIP invalido: tipologia.plugins.json excede el tamano permitido.";
                    return false;
                }
            }

            if (promptEntry is not null)
            {
                var promptText = ReadZipEntryAsString(promptEntry);
                promptGpt = TryExtractPrompt(promptText);
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"ZIP invalido: {ex.Message}";
            return false;
        }
    }

    private static string TryExtractPrompt(string promptJson)
    {
        if (string.IsNullOrWhiteSpace(promptJson))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(promptJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("promptGPT", out var promptElement)
                && promptElement.ValueKind == JsonValueKind.String)
            {
                return promptElement.GetString() ?? string.Empty;
            }

            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                return doc.RootElement.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string PrettyJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, JsonIndentedOptions);
        }
        catch
        {
            return json;
        }
    }

    private static void AddZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }

    private static string ReadZipEntryAsString(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static bool TryValidateConfig(string? configJson, out string? error, out TipologiaValidationConfig? config)
    {
        config = null;

        if (string.IsNullOrWhiteSpace(configJson))
        {
            error = "ConfiguracionJson es obligatorio.";
            return false;
        }

        try
        {
            config = JsonSerializer.Deserialize<TipologiaValidationConfig>(configJson, JsonOptions);
            if (config is null)
            {
                error = "No se pudo deserializar ConfiguracionJson.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.TipologiaId) || string.IsNullOrWhiteSpace(config.Version))
            {
                error = "La configuracion debe incluir tipologiaId y version.";
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"ConfiguracionJson invalido: {ex.Message}";
            return false;
        }
    }

    private static async Task<T?> ReadBody<T>(HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private static async Task<HttpResponseData> CreateError(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }

    private sealed class TipologiaUpsertRequest
    {
        public string? Codigo { get; set; }
        public string? Nombre { get; set; }
        public string? Version { get; set; }
        public string ConfiguracionJson { get; set; } = string.Empty;
        public string? Usuario { get; set; }
    }

    private sealed class PublicarTipologiaRequest
    {
        public string? Usuario { get; set; }
    }

    private sealed class RetirarTipologiaRequest
    {
        public string? Usuario { get; set; }
    }

    private sealed class PasarTipologiaADraftRequest
    {
        public string? Usuario { get; set; }
    }

    private sealed class TipologiaImportRequest
    {
        public string ZipBase64 { get; set; } = string.Empty;
        public string? Usuario { get; set; }
    }

    private sealed class TipologiaExportManifest
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime ExportedAtUtc { get; set; }
        public bool IncludesPlugins { get; set; }
        public bool IncludesPrompt { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    private sealed record DiffChange(string Section, string Path, string ChangeType, string? LeftValue, string? RightValue);
}
