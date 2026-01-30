using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

[Table("Documentos")]
public class DocumentoEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Guid { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string NombreArchivo { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string SHA256 { get; set; } = string.Empty;

    [Required]
    [MaxLength(8)]
    public string CRC32 { get; set; } = string.Empty;

    public long TamanoBytes { get; set; }

    [MaxLength(100)]
    public string? Tipologia { get; set; }

    [MaxLength(100)]
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Procesando, OK, Error, BajaConfianza

    public double? ConfianzaGlobal { get; set; }

    public int Paginas { get; set; }

    [MaxLength(500)]
    public string? RutaBlobStorage { get; set; }

    // Trazabilidad
    [Required]
    [MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? SubmittedBy { get; set; }

    [MaxLength(100)]
    public string? IdGDC { get; set; }

    [MaxLength(100)]
    public string? IdActivo { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaProceso { get; set; }
    public DateTime? FechaActualizacion { get; set; }

    // Navegación
    public virtual ResultadoProcesamientoEntity? Resultado { get; set; }
    public virtual ICollection<AuditoriaEntity> Auditorias { get; set; } = new List<AuditoriaEntity>();
}
