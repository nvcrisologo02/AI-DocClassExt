using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Core.Models;

namespace DocumentIA.Functions.Activities;

/// <summary>
/// Activity que extrae datos de documentos.
/// Utiliza una implementación de IExtraerDataProvider para obtener los datos.
/// Actualmente utiliza MockExtraerDataProvider para pruebas, 
/// pero puede ser reemplazada por una implementación real usando Azure AI Document Intelligence.
/// </summary>
public class ExtraerActivity
{
    private readonly ILogger<ExtraerActivity> _logger;
    private readonly IExtraerDataProvider _dataProvider;

    public ExtraerActivity(ILogger<ExtraerActivity> logger, IExtraerDataProvider dataProvider)
    {
        _logger = logger;
        _dataProvider = dataProvider;
    }

    [Function("ExtraerActivity")]
    public async Task<ExtraccionResultado> Run([ActivityTrigger] ExtraccionInput input)
    {
        _logger.LogInformation("Extrayendo datos del documento para tipología: {Tipologia}", input.Tipologia);

        var resultado = await _dataProvider.ObtenerDatosAsync(input);

        _logger.LogInformation(
            "Extracción completada para tipología: {Tipologia} con proveedor {Proveedor} y modelo {Modelo}",
            input.Tipologia,
            resultado.Proveedor,
            resultado.Modelo);

        return resultado;
    }
}
