using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

[Table("CatalogoTdn2")]
public class CatalogoTdn2Entity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(15)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }

    [Required]
    [MaxLength(10)]
    public string CodigoTdn1 { get; set; } = string.Empty;

    public int Tdn1Id { get; set; }

    [ForeignKey(nameof(Tdn1Id))]
    public CatalogoTdn1Entity? Tdn1 { get; set; }
}
