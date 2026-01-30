using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class VerificarDuplicadoActivity
{
    private readonly ILogger<VerificarDuplicadoActivity> _logger;
    // TODO: Inyectar repositorio cuando se implemente persistencia

    public VerificarDuplicadoActivity(ILogger<VerificarDuplicadoActivity> logger)
    {
        _logger = logger;
    }

    [Function("VerificarDuplicadoActivity")]
    public bool Run([ActivityTrigger] string sha256)
    {
        _logger.LogInformation($"Verificando si existe documento con SHA256: {sha256}");

        // TODO: Consultar base de datos o storage
        // Por ahora retornamos false (no duplicado)
        return false;
    }
}
