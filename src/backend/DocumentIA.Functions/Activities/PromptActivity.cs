using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;

namespace DocumentIA.Functions.Activities;

public class PromptActivity
{
    private readonly ILogger<PromptActivity> _logger;
    private readonly IPromptDataProvider _promptProvider;

    public PromptActivity(ILogger<PromptActivity> logger, IPromptDataProvider promptProvider)
    {
        _logger = logger;
        _promptProvider = promptProvider;
    }

    [Function("PromptActivity")]
    public async Task<PromptResultado> Run([ActivityTrigger] PromptActivityInput input)
    {
        _logger.LogInformation(
            "Ejecutando PromptActivity para tipología {Tipologia}. CombinedWithFallback={Combined}",
            input.Tipologia,
            input.ResultadoPromptCombinado != null);

        var resultado = await _promptProvider.EjecutarPromptAsync(input);

        if (resultado.Error is not null)
        {
            _logger.LogWarning(
                "PromptActivity completado con error para tipología {Tipologia}: {Error}",
                input.Tipologia, resultado.Error);
        }
        else
        {
            _logger.LogInformation(
                "PromptActivity completado para tipología {Tipologia}. Modelo={Modelo}, TiempoMs={TiempoMs}, Combined={Combined}",
                input.Tipologia, resultado.Modelo, resultado.TiempoMs, resultado.CombinedWithFallback);
        }

        return resultado;
    }
}
