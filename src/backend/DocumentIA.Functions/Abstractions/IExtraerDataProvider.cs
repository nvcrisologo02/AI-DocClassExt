namespace DocumentIA.Functions.Abstractions;

using DocumentIA.Core.Models;

/// <summary>
/// Interfaz para proporcionar datos extraídos de documentos.
/// Permite implementar diferentes estrategias de extracción (mocks, servicios reales, etc.)
/// </summary>
public interface IExtraerDataProvider
{
    /// <summary>
    /// Extrae datos del documento según su tipología.
    /// </summary>
    /// <param name="input">Datos de entrada necesarios para la extracción</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Resultado de extracción con datos y metadatos</returns>
    Task<ExtraccionResultado> ObtenerDatosAsync(ExtraccionInput input, CancellationToken cancellationToken = default);
}
