using DocumentIA.Core.Models;
using DocumentIA.Data.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class VerificarDuplicadoPorMD5Activity
{
    private readonly ILogger<VerificarDuplicadoPorMD5Activity> _logger;
    private readonly IDocumentoRepository _documentoRepository;

    public VerificarDuplicadoPorMD5Activity(
        ILogger<VerificarDuplicadoPorMD5Activity> logger,
        IDocumentoRepository documentoRepository)
    {
        _logger = logger;
        _documentoRepository = documentoRepository;
    }

    [Function("VerificarDuplicadoPorMD5Activity")]
    public async Task<VerificarDuplicadoMd5Result> Run([ActivityTrigger] string md5)
    {
        if (string.IsNullOrWhiteSpace(md5))
        {
            return new VerificarDuplicadoMd5Result { Existe = false };
        }

        var documento = await _documentoRepository.GetByMD5Async(md5);
        if (documento == null)
        {
            return new VerificarDuplicadoMd5Result { Existe = false };
        }

        _logger.LogInformation("Documento duplicado por MD5 encontrado. DocumentoId={DocumentoId}", documento.Id);
        return new VerificarDuplicadoMd5Result
        {
            Existe = true,
            SHA256 = documento.SHA256 ?? string.Empty
        };
    }
}
