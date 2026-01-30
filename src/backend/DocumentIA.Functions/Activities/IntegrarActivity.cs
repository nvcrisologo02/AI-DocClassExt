using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;

namespace DocumentIA.Functions.Activities;

public class IntegrarActivity
{
    private readonly ILogger<IntegrarActivity> _logger;
    // TODO: Inyectar plugins de integración

    public IntegrarActivity(ILogger<IntegrarActivity> logger)
    {
        _logger = logger;
    }

    [Function("IntegrarActivity")]
    public ResultadoIntegracion Run([ActivityTrigger] object input)
    {
        _logger.LogInformation("Integrando con sistemas externos");

        // Mock de integración exitosa
        var resultado = new ResultadoIntegracion
        {
            Modulo = "MockIntegration",
            Result = "OK"
        };

        _logger.LogInformation("Integración completada");
        return resultado;
    }
}
