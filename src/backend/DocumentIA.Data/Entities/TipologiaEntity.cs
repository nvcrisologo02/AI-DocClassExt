using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

public enum EstadoTipologia
{
    Draft = 0,
    Published = 1,
    Retired = 2
}

[Table("Tipologias")]
public class TipologiaEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Codigo { get; set; } = string.Empty; // ej: tasacion

    [Required]
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty; // ej: Tasación

    [MaxLength(50)]
    public string Version { get; set; } = "1.0";

    public bool Activa { get; set; } = true;

    // Configuración de modelos
    [MaxLength(200)]
    public string? ModeloClasificacionDI { get; set; }
    public double UmbralClasificacion { get; set; } = 0.85;

    [MaxLength(200)]
    public string? ModeloExtraccionDI { get; set; }
    public double UmbralExtraccion { get; set; } = 0.80;

    [Column(TypeName = "nvarchar(max)")]
    public string? PromptGPT { get; set; }

    // Configuración JSON (campos esperados, reglas de validación, etc.)
    [Column(TypeName = "nvarchar(max)")]
    public string? ConfiguracionJson { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    [MaxLength(200)]
    public string? CreadoPor { get; set; }

    public EstadoTipologia Estado { get; set; } = EstadoTipologia.Draft;

    public DateTime? PublicadaEn { get; set; }

    [MaxLength(200)]
    public string? PublicadaPor { get; set; }

    [MaxLength(50)]
    public string? VersionPublicada { get; set; }
}
