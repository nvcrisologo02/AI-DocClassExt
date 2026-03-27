using System.Net;
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Triggers.Admin;

public class TipologiasAdminFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DocumentIADbContext _dbContext;
    private readonly ITipologiaRepository _tipologiaRepository;
    private readonly ILogger<TipologiasAdminFunction> _logger;

    public TipologiasAdminFunction(
        DocumentIADbContext dbContext,
        ITipologiaRepository tipologiaRepository,
        ILogger<TipologiasAdminFunction> logger)
    {
        _dbContext = dbContext;
        _tipologiaRepository = tipologiaRepository;
        _logger = logger;
    }

    [Function("Admin_GetTipologias")]
    public async Task<HttpResponseData> GetTipologias(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/tipologias")] HttpRequestData req)
    {
        var rows = await _dbContext.Tipologias
            .OrderBy(t => t.Nombre)
            .Select(t => new
            {
                t.Id,
                t.Codigo,
                t.Nombre,
                t.Version,
                t.Estado,
                t.Activa,
                t.FechaCreacion,
                t.FechaActualizacion,
                t.PublicadaEn,
                t.PublicadaPor,
                t.VersionPublicada
            })
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(rows);
        return response;
    }

    [Function("Admin_GetTipologiaById")]
    public async Task<HttpResponseData> GetTipologiaById(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/tipologias/{id:int}")] HttpRequestData req,
        int id)
    {
        var tipologia = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);
        if (tipologia is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(tipologia);
        return response;
    }

    [Function("Admin_CreateTipologia")]
    public async Task<HttpResponseData> CreateTipologia(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/tipologias")] HttpRequestData req)
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

        if (!TryValidateConfig(payload.ConfiguracionJson, out var configError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, configError!);
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
            CreadoPor = payload.Usuario ?? "COMPLETAR_GDC_HTTP_BASIC_USERNAME",
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = DateTime.UtcNow
        };

        await _tipologiaRepository.AddAsync(entity);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(entity);
        return response;
    }

    [Function("Admin_UpdateTipologia")]
    public async Task<HttpResponseData> UpdateTipologia(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/tipologias/{id:int}")] HttpRequestData req,
        int id)
    {
        var entity = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        if (entity.Estado != EstadoTipologia.Draft)
        {
            return await CreateError(req, HttpStatusCode.Conflict, "Solo se pueden editar tipologias en estado Draft.");
        }

        var payload = await ReadBody<TipologiaUpsertRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido.");
        }

        if (!TryValidateConfig(payload.ConfiguracionJson, out var configError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, configError!);
        }

        entity.Codigo = payload.Codigo?.Trim() ?? entity.Codigo;
        entity.Nombre = payload.Nombre?.Trim() ?? entity.Nombre;
        entity.Version = payload.Version?.Trim() ?? entity.Version;
        entity.ConfiguracionJson = payload.ConfiguracionJson;
        entity.FechaActualizacion = DateTime.UtcNow;

        await _tipologiaRepository.UpdateAsync(entity);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(entity);
        return response;
    }

    [Function("Admin_PublicarTipologia")]
    public async Task<HttpResponseData> PublicarTipologia(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/tipologias/{id:int}/publicar")] HttpRequestData req,
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
        await _tipologiaRepository.PublicarAsync(id, payload?.Usuario ?? "COMPLETAR_GDC_HTTP_BASIC_USERNAME");

        var updated = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(updated);
        return response;
    }

    [Function("Admin_RetirarTipologia")]
    public async Task<HttpResponseData> RetirarTipologia(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/tipologias/{id:int}/retirar")] HttpRequestData req,
        int id)
    {
        var exists = await _dbContext.Tipologias.AnyAsync(t => t.Id == id);
        if (!exists)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe tipologia con id {id}.");
        }

        await _tipologiaRepository.RetirarAsync(id);
        var updated = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(updated);
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

    private static bool TryValidateConfig(string? configJson, out string? error)
    {
        var ok = TryValidateConfig(configJson, out error, out _);
        return ok;
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
}
