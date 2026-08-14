namespace DocumentIA.Core.Configuration;

/// <summary>
/// Ajustes de transporte hacia Azure Document Intelligence.
/// Enlazada a la sección de configuración 'DocumentIntelligence'.
/// </summary>
public class DocumentIntelligenceSettings
{
    /// <summary>
    /// Cuando es true, el documento se envía inline (base64Source) en lugar de por referencia
    /// (urlSource con SAS). Necesario en entornos cuyo storage de documentos tiene el acceso
    /// público deshabilitado: Document Intelligence vive fuera de esa red y no puede descargar
    /// el blob, y una SAS no sirve contra un storage protegido por firewall.
    /// El default es false para no alterar el comportamiento de producción si el ajuste falta.
    /// </summary>
    public bool UseInlineContent { get; set; }
}
