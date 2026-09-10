using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

/// <summary>
/// Persiste el markdown que aporto otra actividad (ContentExtraido del clasificador DI/CU,
/// MarkdownExtraido de la extraccion CU) con la misma regla de cobertura (AB#100251).
/// </summary>
public class PersistirMarkdownActivity
{
    private readonly ILogger<PersistirMarkdownActivity> _logger;
    private readonly IMarkdownResolver _resolver;

    public PersistirMarkdownActivity(ILogger<PersistirMarkdownActivity> logger, IMarkdownResolver resolver)
    {
        _logger = logger;
        _resolver = resolver;
    }

    // Igual que en ObtenerMarkdownActivity: el token del host se propaga al resolutor para que
    // una cancelacion real no acabe registrada como "no se pudo persistir" (AB#100251).
    [Function("PersistirMarkdownActivity")]
    public async Task<bool> Run(
        [ActivityTrigger] PersistirMarkdownInput input,
        CancellationToken cancellationToken)
    {
        var persistido = await _resolver.PersistirAportadoAsync(input, cancellationToken);
        _logger.LogInformation(
            "Markdown aportado para {Sha256}: paginas={Paginas}, completo={Completo}, persistido={Persistido}",
            input.Sha256, input.Paginas, input.Completo, persistido);
        return persistido;
    }
}
