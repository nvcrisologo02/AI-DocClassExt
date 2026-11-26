using System.ClientModel;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services.Classification;

/// <summary>
/// Decorador de <see cref="IEmbeddingsCliente"/> que, cuando el servicio rechaza la entrada por superar su tope de
/// tokens (HTTP 400 con mensaje de longitud maxima), reintenta con la primera mitad del texto hasta un tope de
/// recortes. Es el mismo tratamiento que aplica embeddings.py del repo DocumentIA.Batch, con el que se entrenaron
/// los modelos v1 y v2, asi que el texto que llega al modelo coincide con el del entrenamiento. Cualquier otro
/// error se relanza sin reintentar para que lo cuente el ejecutor de resiliencia. AB#101245.
/// </summary>
public sealed class EmbeddingsClienteConRecorte : IEmbeddingsCliente
{
    public const int MaxRecortesPorDefecto = 3;

    private static readonly string[] MensajesLongitud = { "maximum input length", "maximum context length" };

    private readonly IEmbeddingsCliente _interno;
    private readonly ILogger<EmbeddingsClienteConRecorte> _logger;
    private readonly int _maxRecortes;

    public EmbeddingsClienteConRecorte(
        IEmbeddingsCliente interno,
        ILogger<EmbeddingsClienteConRecorte> logger,
        int maxRecortes = MaxRecortesPorDefecto)
    {
        _interno = interno;
        _logger = logger;
        _maxRecortes = maxRecortes;
    }

    public async Task<RespuestaEmbedding> GenerarAsync(string texto, CancellationToken cancellationToken)
    {
        var recortes = 0;
        while (true)
        {
            try
            {
                return await _interno.GenerarAsync(texto, cancellationToken);
            }
            catch (ClientResultException ex) when (ex.Status == 400 && EsLongitudExcedida(ex.Message) && recortes < _maxRecortes && texto.Length > 1)
            {
                var longitudAnterior = texto.Length;
                texto = texto[..(texto.Length / 2)];
                recortes++;
                _logger.LogWarning(
                    "Clasificador por embeddings: el servicio rechazo la entrada por longitud; recorte {Recorte}/{MaxRecortes} de {LongitudAnterior} a {LongitudNueva} caracteres.",
                    recortes, _maxRecortes, longitudAnterior, texto.Length);
            }
        }
    }

    /// <summary>Mensaje del servicio que indica que la entrada supera el tope de tokens del modelo.</summary>
    internal static bool EsLongitudExcedida(string? mensaje) =>
        !string.IsNullOrEmpty(mensaje)
        && MensajesLongitud.Any(m => mensaje.Contains(m, StringComparison.OrdinalIgnoreCase));
}
