namespace DocumentIA.Core.Configuration;

/// <summary>
/// Configuración global del pipeline de procesamiento de documentos.
/// Se vincula a la sección "Pipeline" en appsettings.
/// </summary>
public class PipelineSettings
{
    /// <summary>
    /// Máximo de páginas de un documento para permitir la extracción completa.
    /// Si el documento supera este valor, el pipeline se detiene con estado PAGINAS_EXCEDIDAS.
    /// 0 o ausente = sin límite global. Una tipología puede sobreescribir con su propio MaxPaginasDocumento.
    /// </summary>
    public int MaxPaginasDocumento { get; set; } = 0;
}
