namespace DocumentIA.Functions.Abstractions;

/// <summary>
/// Interfaz para proporcionar datos extraídos de documentos.
/// Permite implementar diferentes estrategias de extracción (mocks, servicios reales, etc.)
/// </summary>
public interface IExtraerDataProvider
{
    /// <summary>
    /// Extrae datos del documento según su tipología.
    /// </summary>
    /// <param name="tipologia">Identificador de la tipología del documento</param>
    /// <returns>Diccionario con los datos extraídos</returns>
    Dictionary<string, object> ObtenerDatos(string tipologia);
}
