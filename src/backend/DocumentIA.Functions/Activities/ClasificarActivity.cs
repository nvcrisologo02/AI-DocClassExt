using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using System.Text.Json;

namespace DocumentIA.Functions.Activities;

public class ClasificarActivity
{
    private readonly ILogger<ClasificarActivity> _logger;
    // TODO: Inyectar servicio de Azure AI Document Intelligence

    public ClasificarActivity(ILogger<ClasificarActivity> logger)
    {
        _logger = logger;
    }

    [Function("ClasificarActivity")]
    public ResultadoClasificacion Run([ActivityTrigger] object input)
    {
        _logger.LogInformation("Clasificando documento");

        // Deserializar input
        var inputJson = JsonSerializer.Serialize(input);
        var inputData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(inputJson);

        // Por ahora retornamos un resultado mock
        // TODO: Integrar con Azure AI Document Intelligence
        var resultado = new ResultadoClasificacion
        {
            Modelo = "mock-classifier-v1",
            Confianza = 0.95,
            FallbackLLM = false,
            TipologiaDetectada = "Tasacion"
        };

        _logger.LogInformation($"Clasificación completada: {resultado.TipologiaDetectada} (confianza: {resultado.Confianza})");
        return resultado;
    }
}
