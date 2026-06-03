using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

[Table("CatalogoTdn1")]
public class CatalogoTdn1Entity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }

    /// <summary>
    /// Prompt personalizado para clasificación Phase 2 (TDN2) de esta familia.
    /// Si tiene valor, se usa este prompt en lugar de generar uno dinámicamente desde las tipologías.
    /// El sistema inyecta automáticamente: tdn1Code, contextoTexto del documento, y phase2ResponseInstruction.
    /// </summary>
    public string? TDN2_Prompt { get; set; }

    public ICollection<CatalogoTdn2Entity> SubTipos { get; set; } = new List<CatalogoTdn2Entity>();
}
