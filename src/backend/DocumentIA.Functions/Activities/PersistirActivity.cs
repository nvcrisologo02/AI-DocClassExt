using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using System.Text.Json;

namespace DocumentIA.Functions.Activities;

public class PersistirActivity
{
    private readonly ILogger<PersistirActivity> _logger;
    // TODO: Inyectar repositorio de datos

    public PersistirActivity(ILogger<PersistirActivity> logger)
    {
        _logger = logger;
    }

    [Function("PersistirActivity")]
    public void Run([ActivityTrigger] ContratoSalida salida)
    {
        _logger.LogInformation($"Persistiendo resultado para documento: {salida.Identificacion.Documento}");

        // TODO: Guardar en base de datos y blob storage
        var salidaJson = JsonSerializer.Serialize(salida, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation($"Resultado:\n{salidaJson}");

        _logger.LogInformation("Persistencia completada");
    }
}
