using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Resilience;
using System.Text.Json;

namespace DocumentIA.Functions.Activities;

public class ClasificarActivity
{
    private readonly ILogger<ClasificarActivity> _logger;
    private readonly IClasificarDataProvider _clasificadorProvider;
    private readonly TarifaRegistryLoader _tarifas;

    public ClasificarActivity(
        ILogger<ClasificarActivity> logger,
        IClasificarDataProvider clasificadorProvider,
        TarifaRegistryLoader tarifas)
    {
        _logger = logger;
        _clasificadorProvider = clasificadorProvider;
        _tarifas = tarifas;
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

        try
        {
            var resultado = await _clasificadorProvider.ClasificarAsync(clasificacionInput);

            TarificadorDeConsumos.Aplicar(resultado.Consumos, _tarifas, _logger);

            _logger.LogInformation("Clasificación completada: {Tipologia} (confianza: {Confianza})", resultado.TipologiaDetectada, resultado.Confianza);
            return resultado;
        }
        catch (RateLimitExhaustedException ex)
        {
            _logger.LogWarning(ex,
                "Clasificación pospuesta por rate limit (429) en documento {Documento}.",
                clasificacionInput.Entrada.Documento.Name);

            var resultadoRateLimit = new ResultadoClasificacion
            {
                RateLimitExcedido = true,
                FallbackRazon = "rate_limit_exhausted",
                TipologiaDetectada = "Desconocido",
                Confianza = 0
            };

            // El gasto anterior al 429 se conserva y se tarifica: la ejecucion queda
            // PENDIENTE_REINTENTO, pero esas llamadas ya se facturaron.
            resultadoRateLimit.Consumos.AddRange(ex.ConsumosParciales);
            TarificadorDeConsumos.Aplicar(resultadoRateLimit.Consumos, _tarifas, _logger);

            return resultadoRateLimit;
        }
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
