using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class ExtraerMarkdownLayoutActivity
{
    private readonly ILogger<ExtraerMarkdownLayoutActivity> _logger;
    private readonly ILayoutMarkdownProvider _provider;
    private readonly TarifaRegistryLoader _tarifas;

    public ExtraerMarkdownLayoutActivity(
        ILogger<ExtraerMarkdownLayoutActivity> logger,
        ILayoutMarkdownProvider provider,
        TarifaRegistryLoader tarifas)
    {
        _logger = logger;
        _provider = provider;
        _tarifas = tarifas;
    }

    [Function("ExtraerMarkdownLayoutActivity")]
    public async Task<ExtraerMarkdownLayoutResultado> Run([ActivityTrigger] ExtraerMarkdownLayoutInput input)
    {
        _logger.LogInformation(
            "Extrayendo markdown DI layout para tipología {Tipologia} y documento {NombreDocumento}",
            input.Tipologia,
            input.NombreDocumento);

        var resultado = await _provider.ExtraerMarkdownAsync(input);

        TarificadorDeConsumos.Aplicar(resultado.Consumos, _tarifas, _logger);

        return resultado;
    }
}
