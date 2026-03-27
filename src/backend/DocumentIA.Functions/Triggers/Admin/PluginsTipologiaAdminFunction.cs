using System.Net;
using System.Text.Json;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Plugins.Integration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DocumentIA.Functions.Triggers.Admin;

public class PluginsTipologiaAdminFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPluginTipologiaConfigRepository _pluginRepository;

    public PluginsTipologiaAdminFunction(IPluginTipologiaConfigRepository pluginRepository)
    {
        _pluginRepository = pluginRepository;
    }

    [Function("Admin_GetPluginsTipologias")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/plugins-tipologias")] HttpRequestData req)
    {
        var rows = await _pluginRepository.GetAllAsync();
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(rows);
        return response;
    }

    [Function("Admin_GetPluginsTipologiaByCodigo")]
    public async Task<HttpResponseData> GetByCodigo(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/plugins-tipologias/{tipologiaCodigo}")] HttpRequestData req,
        string tipologiaCodigo)
    {
        var row = await _pluginRepository.GetByTipologiaCodigoAsync(tipologiaCodigo);
        if (row is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe configuracion de plugins para '{tipologiaCodigo}'.");
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(row);
        return response;
    }

    [Function("Admin_UpsertPluginsTipologiaDraft")]
    public async Task<HttpResponseData> UpsertDraft(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/plugins-tipologias/{tipologiaCodigo}")] HttpRequestData req,
        string tipologiaCodigo)
    {
        var payload = await ReadBody<PluginConfigUpsertRequest>(req);
        if (payload is null || string.IsNullOrWhiteSpace(payload.ConfiguracionJson))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido: ConfiguracionJson es obligatorio.");
        }

        if (!TryValidatePluginConfig(payload.ConfiguracionJson, tipologiaCodigo, out var error))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, error!);
        }

        var saved = await _pluginRepository.UpsertDraftAsync(tipologiaCodigo, payload.ConfiguracionJson, payload.Usuario);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(saved);
        return response;
    }

    [Function("Admin_PublicarPluginsTipologia")]
    public async Task<HttpResponseData> Publish(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/plugins-tipologias/{tipologiaCodigo}/publicar")] HttpRequestData req,
        string tipologiaCodigo)
    {
        var payload = await ReadBody<PluginPublishRequest>(req);
        var current = await _pluginRepository.GetByTipologiaCodigoAsync(tipologiaCodigo);
        if (current is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe configuracion de plugins para '{tipologiaCodigo}'.");
        }

        if (!TryValidatePluginConfig(current.ConfiguracionJson, tipologiaCodigo, out var error))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, error!);
        }

        await _pluginRepository.PublishAsync(tipologiaCodigo, payload?.Usuario);
        var updated = await _pluginRepository.GetByTipologiaCodigoAsync(tipologiaCodigo);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(updated);
        return response;
    }

    [Function("Admin_RetirarPluginsTipologia")]
    public async Task<HttpResponseData> Retire(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "COMPLETAR_GDC_HTTP_BASIC_USERNAME/plugins-tipologias/{tipologiaCodigo}/retirar")] HttpRequestData req,
        string tipologiaCodigo)
    {
        var current = await _pluginRepository.GetByTipologiaCodigoAsync(tipologiaCodigo);
        if (current is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe configuracion de plugins para '{tipologiaCodigo}'.");
        }

        await _pluginRepository.RetireAsync(tipologiaCodigo);
        var updated = await _pluginRepository.GetByTipologiaCodigoAsync(tipologiaCodigo);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(updated);
        return response;
    }

    private static bool TryValidatePluginConfig(string json, string tipologiaCodigo, out string? error)
    {
        try
        {
            var config = JsonSerializer.Deserialize<PluginConfiguration>(json, JsonOptions);
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

    private static async Task<T?> ReadBody<T>(HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private static async Task<HttpResponseData> CreateError(HttpRequestData req, HttpStatusCode code, string message)
    {
        var response = req.CreateResponse(code);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }

    private sealed class PluginConfigUpsertRequest
    {
        public string ConfiguracionJson { get; set; } = string.Empty;
        public string? Usuario { get; set; }
    }

    private sealed class PluginPublishRequest
    {
        public string? Usuario { get; set; }
    }
}
