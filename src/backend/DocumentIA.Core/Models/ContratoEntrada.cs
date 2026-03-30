namespace DocumentIA.Core.Models;

public class ContratoEntrada
{
    public Instrucciones Instrucciones { get; set; } = new();
    public Documento Documento { get; set; } = new();
    public Trazabilidad Trazabilidad { get; set; } = new();
}

public class Instrucciones
{
    public string ExpectedType { get; set; } = string.Empty;
    public bool SkipDuplicateCheck { get; set; }
    public bool ForceReprocess { get; set; }
    // Controla si se sube el documento al GDC. Si no se especifica (null), se usa el valor por defecto
    // configurado en la tipología detectada (tipologiaConfig.SkipGDCUpload).
    // true = omitir subida; false = forzar subida; null = respetar config de tipología.
    public bool? SkipGDCUpload { get; set; }
    public ConfiguracionIA Classification { get; set; } = new();
    public ConfiguracionIA Extraction { get; set; } = new();
}

public class ConfiguracionIA
{
    public string Provider { get; set; } = "auto"; // auto | azure-document-intelligence | mock
    public string Model { get; set; } = "auto"; // DI | GPT | auto
    /// <summary>
    /// Umbral de confianza para esta etapa (legado, aplica a completitud y confianza si no se informan los específicos).
    /// null = usar el valor configurado en la tipología o en el servidor.
    /// </summary>
    public double? Umbral { get; set; } = null;
    /// <summary>
    /// Umbral de completitud de extracción CU (ratio de campos esperados presentes) para esta petición.
    /// Tiene precedencia sobre tipología y sobre Umbral. null = usar tipología o global.
    /// Rango [0.0–1.0].
    /// </summary>
    public double? UmbralCompletitud { get; set; } = null;
    /// <summary>
    /// Umbral de confianza global de extracción CU para esta petición.
    /// Tiene precedencia sobre tipología y sobre Umbral. null = usar tipología o global.
    /// Rango [0.0–1.0].
    /// </summary>
    public double? UmbralConfianza { get; set; } = null;
}

public class Documento
{
    public string Name { get; set; } = string.Empty;
    public ContenidoDocumento Content { get; set; } = new();
}

public class ContenidoDocumento
{
    public string Base64 { get; set; } = string.Empty;
}

public class Trazabilidad
{
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public string SubmittedBy { get; set; } = string.Empty;
    public string? IdGDC { get; set; }
    public string? IdActivo { get; set; }
}
