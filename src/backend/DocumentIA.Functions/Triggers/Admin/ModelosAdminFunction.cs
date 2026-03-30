using System.Net;
using System.Text.Json;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Functions.Triggers.Admin;

public class ModelosAdminFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DocumentIADbContext _dbContext;

    public ModelosAdminFunction(DocumentIADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("Admin_GetModelosByTipo")]
    public async Task<HttpResponseData> GetModelosByTipo(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/modelos/{tipo}")] HttpRequestData req,
        string tipo)
    {
        if (!TryParseTipo(tipo, out var tipoModelo))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Tipo de modelo invalido. Valores: clasificacion, extraccion, prompt.");
        }

        var modelos = await _dbContext.ModeloConfigs
            .Where(m => m.Tipo == tipoModelo)
            .OrderBy(m => m.Key)
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(modelos);
        return response;
    }

    [Function("Admin_CreateModelo")]
    public async Task<HttpResponseData> CreateModelo(
           [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/modelos")] HttpRequestData req)
    {
        var payload = await ReadBody<ModeloUpsertRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido.");
        }

        if (!TryParseTipo(payload.Tipo, out var tipoModelo))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Tipo de modelo invalido. Valores: clasificacion, extraccion, prompt.");
        }

        if (string.IsNullOrWhiteSpace(payload.Key) || string.IsNullOrWhiteSpace(payload.Provider) || string.IsNullOrWhiteSpace(payload.ConfiguracionJson))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Key, Provider y ConfiguracionJson son obligatorios.");
        }

        if (!TryValidateJson(payload.ConfiguracionJson, out var jsonError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, jsonError!);
        }

        var exists = await _dbContext.ModeloConfigs.AnyAsync(m => m.Key == payload.Key);
        if (exists)
        {
            return await CreateError(req, HttpStatusCode.Conflict, $"Ya existe un modelo con key '{payload.Key}'.");
        }

        var entity = new ModeloConfigEntity
        {
            Tipo = tipoModelo,
            Key = payload.Key.Trim(),
            Provider = payload.Provider.Trim(),
            Activo = payload.Activo,
            ConfiguracionJson = payload.ConfiguracionJson,
            CreadoPor = payload.Usuario ?? "COMPLETAR_GDC_HTTP_BASIC_USERNAME",
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = DateTime.UtcNow
        };

        _dbContext.ModeloConfigs.Add(entity);
        await _dbContext.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(entity);
        return response;
    }

    [Function("Admin_UpdateModelo")]
    public async Task<HttpResponseData> UpdateModelo(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "management/modelos/{id:int}")] HttpRequestData req,
        int id)
    {
        var entity = await _dbContext.ModeloConfigs.FirstOrDefaultAsync(m => m.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe modelo con id {id}.");
        }

        var payload = await ReadBody<ModeloUpsertRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido.");
        }

        if (!TryParseTipo(payload.Tipo, out var tipoModelo))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Tipo de modelo invalido. Valores: clasificacion, extraccion, prompt.");
        }

        if (!TryValidateJson(payload.ConfiguracionJson, out var jsonError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, jsonError!);
        }

        entity.Tipo = tipoModelo;
        entity.Key = payload.Key?.Trim() ?? entity.Key;
        entity.Provider = payload.Provider?.Trim() ?? entity.Provider;
        entity.Activo = payload.Activo;
        entity.ConfiguracionJson = payload.ConfiguracionJson;
        entity.FechaActualizacion = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(entity);
        return response;
    }

    [Function("Admin_DeleteModelo")]
    public async Task<HttpResponseData> DeleteModelo(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "management/modelos/{id:int}")] HttpRequestData req,
        int id)
    {
        var entity = await _dbContext.ModeloConfigs.FirstOrDefaultAsync(m => m.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe modelo con id {id}.");
        }

        entity.Activo = false;
        entity.FechaActualizacion = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(entity);
        return response;
    }

    private static bool TryParseTipo(string? tipo, out TipoModelo tipoModelo)
    {
        tipoModelo = TipoModelo.Clasificacion;
        if (string.IsNullOrWhiteSpace(tipo))
        {
            return false;
        }

        return tipo.Trim().ToLowerInvariant() switch
        {
            "clasificacion" => SetTipo(TipoModelo.Clasificacion, out tipoModelo),
            "extraccion" => SetTipo(TipoModelo.Extraccion, out tipoModelo),
            "prompt" => SetTipo(TipoModelo.Prompt, out tipoModelo),
            _ => false
        };
    }

    private static bool SetTipo(TipoModelo value, out TipoModelo output)
    {
        output = value;
        return true;
    }

    private static bool TryValidateJson(string? json, out string? error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "ConfiguracionJson es obligatorio.";
            return false;
        }

        try
        {
            JsonDocument.Parse(json);
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

    private sealed class ModeloUpsertRequest
    {
        public string Tipo { get; set; } = string.Empty;
        public string? Key { get; set; }
        public string? Provider { get; set; }
        public string ConfiguracionJson { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        public string? Usuario { get; set; }
    }
}
