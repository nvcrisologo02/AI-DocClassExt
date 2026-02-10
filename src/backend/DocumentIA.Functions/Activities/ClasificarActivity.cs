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
        // Deserializar input y comprobar si las instrucciones incluyen un ExpectedType
        var inputJson = JsonSerializer.Serialize(input);
        var inputData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(inputJson);

        try
        {
            if (inputData != null && inputData.TryGetValue("Entrada", out var entradaEl))
            {
                if (entradaEl.ValueKind == JsonValueKind.Object && entradaEl.TryGetProperty("Instrucciones", out var instrEl))
                {
                    if (instrEl.ValueKind == JsonValueKind.Object && instrEl.TryGetProperty("ExpectedType", out var expectedEl))
                    {
                        var expected = expectedEl.GetString();
                        if (!string.IsNullOrWhiteSpace(expected))
                        {
                            var forced = new ResultadoClasificacion
                            {
                                Modelo = "expectedtype-input",
                                Confianza = 1.0,
                                FallbackLLM = false,
                                TipologiaDetectada = expected
                            };

                            _logger.LogInformation($"Clasificación forzada por ExpectedType: {expected}");
                            return forced;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error leyendo ExpectedType del input de clasificación; continuando con clasificador mock.");
        }

        // Por ahora retornamos un resultado mock de clasificación
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
