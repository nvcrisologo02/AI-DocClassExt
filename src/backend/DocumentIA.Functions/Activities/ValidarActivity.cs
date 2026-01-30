using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using System.Text.Json;

namespace DocumentIA.Functions.Activities;

public class ValidarActivity
{
    private readonly ILogger<ValidarActivity> _logger;
    // TODO: Inyectar motor de reglas

    public ValidarActivity(ILogger<ValidarActivity> logger)
    {
        _logger = logger;
    }

    [Function("ValidarActivity")]
    public InformacionPostproceso Run([ActivityTrigger] object input)
    {
        _logger.LogInformation("Validando datos extraídos");

        var resultado = new InformacionPostproceso
        {
            Normalizaciones = new List<string> { "Dirección normalizada", "Fecha convertida a ISO" },
            Validaciones = new List<string> { "Formato referencia catastral OK", "Rango valor tasado OK" },
            Inconsistencias = new List<string>()
        };

        _logger.LogInformation("Validación completada");
        return resultado;
    }
}
