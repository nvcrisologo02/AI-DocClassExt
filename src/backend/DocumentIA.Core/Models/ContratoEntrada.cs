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
    public double Umbral { get; set; } = 0.85;
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
