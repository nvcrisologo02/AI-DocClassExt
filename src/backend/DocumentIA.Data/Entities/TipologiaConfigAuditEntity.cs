using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

[Table("TipologiaConfigAudit")]
public class TipologiaConfigAuditEntity
{
    [Key]
    public int Id { get; set; }

    public int TipologiaId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Accion { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Usuario { get; set; }

    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "nvarchar(max)")]
    public string? DetallesJson { get; set; }
}
