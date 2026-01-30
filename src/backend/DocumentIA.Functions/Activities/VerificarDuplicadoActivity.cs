using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Data.Repositories;

namespace DocumentIA.Functions.Activities;

public class VerificarDuplicadoActivity
{
    private readonly ILogger<VerificarDuplicadoActivity> _logger;
    private readonly IDocumentoRepository _documentoRepository;

    public VerificarDuplicadoActivity(
        ILogger<VerificarDuplicadoActivity> logger,
        IDocumentoRepository documentoRepository)
    {
        _logger = logger;
        _documentoRepository = documentoRepository;
    }

    [Function("VerificarDuplicadoActivity")]
    public async Task<bool> Run([ActivityTrigger] string sha256)
    {
        _logger.LogInformation($"Verificando si existe documento con SHA256: {sha256}");

        var existe = await _documentoRepository.ExistsBySHA256Async(sha256);

        if (existe)
        {
            _logger.LogWarning($"Documento duplicado encontrado: {sha256}");
        }

        return existe;
    }
}
