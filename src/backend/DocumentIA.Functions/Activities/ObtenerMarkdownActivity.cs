using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

/// <summary>
/// Punto unico de entrada del orquestador al resolutor de markdown (AB#100251). La tarifa se
/// aplica aqui y no en el orquestador, que debe seguir siendo determinista (AB#100230).
/// </summary>
public class ObtenerMarkdownActivity
{
    private readonly ILogger<ObtenerMarkdownActivity> _logger;
    private readonly IMarkdownResolver _resolver;
    private readonly TarifaRegistryLoader _tarifas;

    public ObtenerMarkdownActivity(
        ILogger<ObtenerMarkdownActivity> logger,
        IMarkdownResolver resolver,
        TarifaRegistryLoader tarifas)
    {
        _logger = logger;
        _resolver = resolver;
        _tarifas = tarifas;
    }

    // El CancellationToken lo inyecta el host de Functions (modelo aislado) y se propaga al
    // resolutor: sin el llegaba default y los filtros de cancelacion del resolutor no se cumplian
    // nunca, asi que el reciclado del worker se registraba como "Layout fallo" y se enmascaraba
    // con el respaldo de base de datos (AB#100251).
    [Function("ObtenerMarkdownActivity")]
    public async Task<ResultadoMarkdown> Run(
        [ActivityTrigger] ObtenerMarkdownInput input,
        CancellationToken cancellationToken)
    {
        var resultado = await _resolver.ResolverAsync(input.Necesidad, input.Contexto, cancellationToken);

        TarificadorDeConsumos.Aplicar(resultado.Consumos, _tarifas, _logger);

        _logger.LogInformation(
            "Markdown resuelto para {Documento}: fuente={Fuente}, paginas={Paginas}, completo={Completo}, persistido={Persistido}, necesidad={Necesidad}",
            input.Contexto.NombreDocumento,
            resultado.Fuente,
            resultado.Paginas,
            resultado.Completo,
            resultado.Persistido,
            input.Necesidad.DocumentoCompleto ? "completo" : $"{input.Necesidad.PaginasMinimas} paginas");

        return resultado;
    }
}
