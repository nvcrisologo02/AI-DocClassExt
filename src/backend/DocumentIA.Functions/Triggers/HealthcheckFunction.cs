using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentIA.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DocumentIA.Functions.Triggers;

public class HealthcheckFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ISystemHealthService? _healthService;

    /// <summary>Constructor con servicio de salud (producción / tests integrados).</summary>
    public HealthcheckFunction(ISystemHealthService healthService)
    {
        _healthService = healthService;
    }

    /// <summary>Constructor sin dependencias para mantener compatibilidad con tests existentes.</summary>
    public HealthcheckFunction() { }

    [Function("PostHealthcheck")]
    public async Task<HttpResponseData> PostHealthcheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "healthcheck")] HttpRequestData req)
    {
        if (_healthService is null)
        {
            var fallbackResponse = req.CreateResponse(HttpStatusCode.OK);
            // Fallback: respuesta mínima compatible con el contrato anterior
            await WriteJsonAsync(fallbackResponse, new
            {
                ok = true,
                timestamp = DateTimeOffset.UtcNow
            });
            return fallbackResponse;
        }

        var snapshot = await _healthService.GetHealthAsync(req.FunctionContext.CancellationToken);
        var aggregate = snapshot.AggregateStatus;

        var payload = new
        {
            ok = aggregate != ComponentHealth.StatusUnhealthy,
            status = aggregate,
            timestamp = DateTimeOffset.UtcNow,
            components = new
            {
                functions = snapshot.Functions,
                assetResolver = snapshot.AssetResolver,
                gdc = snapshot.Gdc,
                modelProviders = new
                {
                    status = snapshot.ModelProviders.Status,
                    classification = snapshot.ModelProviders.Classification,
                    extraction = snapshot.ModelProviders.Extraction,
                    prompt = snapshot.ModelProviders.Prompt
                }
            }
        };

        var httpStatus = aggregate == ComponentHealth.StatusUnhealthy
            ? HttpStatusCode.ServiceUnavailable
            : HttpStatusCode.OK;

        var response = req.CreateResponse(httpStatus);
        await WriteJsonAsync(response, payload);
        return response;
    }

    private static async Task WriteJsonAsync(HttpResponseData response, object payload)
    {
        response.Headers.Remove("Content-Type");
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
