using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

[Table("ResultadosProcesamiento")]
public class ResultadoProcesamientoEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int DocumentoId { get; set; }

    // Clasificación
    [MaxLength(200)]
    public string? ModeloClasificacion { get; set; }
    public double? ConfianzaClasificacion { get; set; }
    public bool FallbackLLM { get; set; }

    // Extracción
    [MaxLength(200)]
    public string? ModeloExtraccion { get; set; }
    public bool LayoutEnabled { get; set; }

    // Datos extraídos (JSON)
    [Column(TypeName = "nvarchar(max)")]
    public string? DatosExtraidosJson { get; set; }

    // Postproceso
    [Column(TypeName = "nvarchar(max)")]
    public string? NormalizacionesJson { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ValidacionesJson { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? InconsistenciasJson { get; set; }

    // Integración
    [MaxLength(200)]
    public string? ModuloIntegracion { get; set; }

    [MaxLength(50)]
    public string? ResultadoIntegracion { get; set; } // OK, REVISION, ERROR

    // Tiempos
    public int? TiempoNormalizacionMs { get; set; }
    public int? TiempoClasificacionMs { get; set; }
    public int? TiempoExtraccionMs { get; set; }
    public int? TiempoValidacionMs { get; set; }
    public int? TiempoIntegracionMs { get; set; }
    public int? TiempoTotalMs { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    // Metricas agregadas
    public int NumeroEjecucion { get; set; }
    public int TotalPluginsEjecutados { get; set; }
    public int TotalValidacionesAplicadas { get; set; }
    public int TotalErroresValidacion { get; set; }
    public int TotalWarningsValidacion { get; set; }
    public double PorcentajeCompletitud { get; set; }

    // Navegación
    [ForeignKey("DocumentoId")]
    public virtual DocumentoEntity Documento { get; set; } = null!;
}
