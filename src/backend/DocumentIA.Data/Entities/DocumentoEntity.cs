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
    [MaxLength(32)]
    public string MD5 { get; set; } = string.Empty;

    [Required]
    [MaxLength(8)]
    public string CRC32 { get; set; } = string.Empty;

    public long TamanoBytes { get; set; }

    [MaxLength(100)]
    public string? Tipologia { get; set; }

    // === Clasificación jerárquica TDN ===
    [MaxLength(50)]
    public string? Tdn1 { get; set; }

    [MaxLength(100)]
    public string? Tdn2 { get; set; }

    [MaxLength(50)]
    public string? Matricula { get; set; }

    [MaxLength(500)]
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Procesando, OK, Error, BajaConfianza

    public double? ConfianzaGlobal { get; set; }

    public int Paginas { get; set; }

    [MaxLength(500)]
    public string? RutaBlobStorage { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? NormalizacionMarkdownCompressed { get; set; }

    // === Auditoría y trazabilidad de clasificación ===
    [MaxLength(500)]
    public string? EvidenceUri { get; set; }

    [MaxLength(50)]
    public string? ClassifierVersion { get; set; }

    public int PagesProcessed { get; set; }

    [MaxLength(64)]
    public string? DedupSha256 { get; set; }

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
    public virtual ICollection<DocumentoEjecucionEntity> Ejecuciones { get; set; }
        = new List<DocumentoEjecucionEntity>();
    // Navegación
    public virtual ResultadoProcesamientoEntity? Resultado { get; set; }
    public virtual ICollection<AuditoriaEntity> Auditorias { get; set; } = new List<AuditoriaEntity>();
}
