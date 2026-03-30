using System.Net;
using System.Text.Json;
using DocumentIA.Data.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Triggers;

public class TipologiasPublicasFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ITipologiaRepository _tipologiaRepository;
    private readonly ILogger<TipologiasPublicasFunction> _logger;

    public TipologiasPublicasFunction(
        ITipologiaRepository tipologiaRepository,
        ILogger<TipologiasPublicasFunction> logger)
    {
        _tipologiaRepository = tipologiaRepository;
        _logger = logger;
    }

    [Function("GetTipologiasPublicadas")]
    public async Task<HttpResponseData> GetTipologiasPublicadas(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tipologias")] HttpRequestData req)
    {
        _logger.LogInformation("GetTipologiasPublicadas requested.");

        var tipologias = await _tipologiaRepository.GetAllPublishedAsync();

        var dtos = tipologias
            .Select(t => new
            {
                identificador = $"{t.Codigo}@{t.Version}",
                nombre = t.Nombre
            })
            .ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(dtos, JsonOptions));
        return response;
    }
}
