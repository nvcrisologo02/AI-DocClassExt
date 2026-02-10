using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using DocumentIA.Functions.Abstractions;

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
    public Dictionary<string, object> Run([ActivityTrigger] object input)
    {
        _logger.LogInformation("Extrayendo datos del documento");

        var inputJson = JsonSerializer.Serialize(input);
        var inputData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(inputJson);
        
        var tipologia = inputData?["Tipologia"].GetString() ?? "Desconocida";

        // Obtener datos a través del proveedor inyectado
        var datosExtraidos = _dataProvider.ObtenerDatos(tipologia);

        _logger.LogInformation($"Extracción completada para tipología: {tipologia}");
        return datosExtraidos;
    }
}
