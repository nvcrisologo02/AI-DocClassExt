using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocumentIA.Functions.Activities;

public class ExtraerActivity
{
    private readonly ILogger<ExtraerActivity> _logger;
    // TODO: Inyectar servicio de Azure AI Document Intelligence

    public ExtraerActivity(ILogger<ExtraerActivity> logger)
    {
        _logger = logger;
    }

    [Function("ExtraerActivity")]
    public Dictionary<string, object> Run([ActivityTrigger] object input)
    {
        _logger.LogInformation("Extrayendo datos del documento");

        var inputJson = JsonSerializer.Serialize(input);
        var inputData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(inputJson);
        
        var tipologia = inputData?["Tipologia"].GetString() ?? "Desconocida";

        // Mock de datos extraídos según tipología
        var datosExtraidos = new Dictionary<string, object>
        {
            ["FechaDocumento"] = "24/10/2025",
            ["Emisor"] = "Tasadora Ejemplo S.L.",
            ["ValorTasado"] = 350000.00,
            ["Direccion"] = "Calle Alcalá 45, 28014 Madrid",
            ["ReferenciaCatastral"] = "1234567890ABCDEFGH"
        };

        _logger.LogInformation($"Extracción completada para tipología: {tipologia}");
        return datosExtraidos;
    }
}
