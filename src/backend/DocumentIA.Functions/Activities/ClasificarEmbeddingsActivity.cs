using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Classification;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

/// <summary>
/// Activity del clasificador por embeddings (AB#100779). Determinista hacia el
/// orquestador: nunca propaga excepciones, cualquier fallo vuelve como DerivarGpt con
/// Error, y tarifica el consumo igual que ClasificarActivity.
/// </summary>
public class ClasificarEmbeddingsActivity
{
    private readonly ILogger<ClasificarEmbeddingsActivity> _logger;
    private readonly IEmbeddingsClasificarProvider _provider;
    private readonly TarifaRegistryLoader _tarifas;

    public ClasificarEmbeddingsActivity(
        ILogger<ClasificarEmbeddingsActivity> logger,
        IEmbeddingsClasificarProvider provider,
        TarifaRegistryLoader tarifas)
    {
        _logger = logger;
        _provider = provider;
        _tarifas = tarifas;
    }

    // El CancellationToken lo inyecta el host de Functions (modelo aislado), igual que en
    // ObtenerMarkdownActivity (AB#100251): permite cancelar el proveedor al reciclar el worker.
    [Function("ClasificarEmbeddingsActivity")]
    public Task<ResultadoEmbeddings> Run(
        [ActivityTrigger] ClasificarEmbeddingsInput input,
        CancellationToken cancellationToken)
        => EjecutarAsync(input, cancellationToken);

    public async Task<ResultadoEmbeddings> EjecutarAsync(ClasificarEmbeddingsInput? input, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return new ResultadoEmbeddings { Decision = DecisionesEmbeddings.Omitido, Motivo = MotivosEmbeddings.SinTexto };
        }

        try
        {
            var resultado = await _provider.ClasificarAsync(input, cancellationToken);
            TarificadorDeConsumos.Aplicar(resultado.Consumos, _tarifas, _logger);

            _logger.LogInformation(
                "Clasificador por embeddings: {Decision} ({Motivo}), TDN1={Tdn1}, confianza={Confianza:F3}, {LatenciaMs} ms",
                resultado.Decision, resultado.Motivo, resultado.Tdn1, resultado.Confianza, resultado.LatenciaMs);

            return resultado;
        }
        catch (Exception ex)
        {
            // Cubre cualquier excepcion que escape del proveedor o del tarificador, de modo que el
            // orquestador nunca vea la activity fallida. Los fallos de DI ocurren al activar la
            // clase, fuera de Run, y no pasan por aqui.
            _logger.LogWarning(ex, "ClasificarEmbeddingsActivity: fallo no controlado; se deriva al GPT.");
            return new ResultadoEmbeddings
            {
                Decision = DecisionesEmbeddings.DerivarGpt,
                Motivo = MotivosEmbeddings.Error,
                Error = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }
}
