using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using System.Text.Json;

namespace DocumentIA.Functions.Activities;

public class ClasificarActivity
{
    private readonly ILogger<ClasificarActivity> _logger;
    private readonly IClasificarDataProvider _clasificadorProvider;

    public ClasificarActivity(ILogger<ClasificarActivity> logger, IClasificarDataProvider clasificadorProvider)
    {
        _logger = logger;
        _clasificadorProvider = clasificadorProvider;
    }

    [Function("ClasificarActivity")]
    public async Task<ResultadoClasificacion> Run([ActivityTrigger] object input)
    {
        _logger.LogInformation("Clasificando documento");

        var clasificacionInput = ParseInput(input);
        var expectedType = clasificacionInput.Entrada.Instrucciones.ExpectedType;

        if (!string.IsNullOrWhiteSpace(expectedType))
        {
            var forced = new ResultadoClasificacion
            {
                Modelo = "expectedtype-input",
                Confianza = 1.0,
                FallbackLLM = false,
                TipologiaDetectada = expectedType
            };

            _logger.LogInformation("Clasificación forzada por ExpectedType: {ExpectedType}", expectedType);
            return forced;
        }

        var resultado = await _clasificadorProvider.ClasificarAsync(clasificacionInput);
        _logger.LogInformation("Clasificación completada: {Tipologia} (confianza: {Confianza})", resultado.TipologiaDetectada, resultado.Confianza);
        return resultado;
    }

    private static ClasificacionInput ParseInput(object input)
    {
        if (input is ClasificacionInput typedInput)
        {
            return typedInput;
        }

        var json = JsonSerializer.Serialize(input);
        var parsed = JsonSerializer.Deserialize<ClasificacionInput>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (parsed is null)
        {
            throw new InvalidOperationException("Input de clasificación inválido");
        }

        return parsed;
    }
}
