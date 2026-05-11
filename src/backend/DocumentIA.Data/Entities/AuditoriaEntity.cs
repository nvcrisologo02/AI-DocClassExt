using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

[Table("Auditoria")]
public class AuditoriaEntity
{
    [Key]
    public int Id { get; set; }

    public int DocumentoId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Accion { get; set; } = string.Empty; // Ingesta, Clasificacion, Extraccion, etc.

    [MaxLength(50)]
    public string Nivel { get; set; } = "Info"; // Info, Warning, Error

    [Column(TypeName = "nvarchar(max)")]
    public string? Mensaje { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? DetallesJson { get; set; }

    [MaxLength(200)]
    public string? Usuario { get; set; }

    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    // Navegación
    [ForeignKey("DocumentoId")]
    public virtual DocumentoEntity Documento { get; set; } = null!;
}
