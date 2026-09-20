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

    /// <summary>
    /// Markdown normalizado en Base64 de GZip. Forma historica (AB#100169): se sigue escribiendo
    /// en paralelo a <see cref="NormalizacionMarkdownGzip"/> para que revertir el codigo o la
    /// migracion no pierda datos. Su retirada es una fase posterior.
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? NormalizacionMarkdownCompressed { get; set; }

    /// <summary>
    /// Markdown normalizado en GZip binario (AB#100169). Ocupa ~2,7 veces menos que la variante
    /// Base64 en nvarchar(max), que anade un 33% por el Base64 y otro x2 por UTF-16.
    /// </summary>
    [Column(TypeName = "varbinary(max)")]
    public byte[]? NormalizacionMarkdownGzip { get; set; }

    /// <summary>
    /// Paginas que cubre el markdown persistido. NULL = cobertura desconocida (historico
    /// anterior a AB#100245): solo vale como fallback, nunca satisface la regla de cobertura.
    /// </summary>
    public int? MarkdownPaginas { get; set; }

    /// <summary>El markdown persistido cubre el documento entero. Necesario porque para
    /// Office no se conoce el total de paginas y "completo" no siempre es un numero.</summary>
    public bool MarkdownCompleto { get; set; }

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
    public DateTime? FechaExpiracionBlob { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public virtual ICollection<DocumentoEjecucionEntity> Ejecuciones { get; set; }
        = new List<DocumentoEjecucionEntity>();
    // Navegación
    public virtual ResultadoProcesamientoEntity? Resultado { get; set; }
    public virtual ICollection<AuditoriaEntity> Auditorias { get; set; } = new List<AuditoriaEntity>();
}
