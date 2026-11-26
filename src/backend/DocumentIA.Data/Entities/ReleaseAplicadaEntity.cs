using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

/// <summary>
/// Registro de los pasos de release ya aplicados en un entorno (AB#101570). Lo escribe y lo lee
/// por SQL, con token de Entra, la pipeline Release; la aplicacion no usa esta entidad en
/// ejecucion. Existe para que la migracion EF cree la tabla en los tres entornos con el 807.
/// </summary>
[Table("ReleaseAplicada")]
public class ReleaseAplicadaEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>Version de la release (por ejemplo v1.2.0).</summary>
    [Required]
    [MaxLength(32)]
    public string Version { get; set; } = string.Empty;

    /// <summary>Ruta del paso, o <c>__cierre__</c> para la fila de cierre de la release.</summary>
    [Required]
    [MaxLength(400)]
    public string Paso { get; set; } = string.Empty;

    /// <summary>SHA-256 hexadecimal del fichero del paso; vacio en la fila de cierre.</summary>
    [Required]
    [MaxLength(64)]
    public string HashContenido { get; set; } = string.Empty;

    /// <summary>Resultado del paso: <c>Aplicado</c> o <c>Revertido</c>.</summary>
    [Required]
    [MaxLength(16)]
    public string Resultado { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string BuildId { get; set; } = string.Empty;

    public DateTime FechaUtc { get; set; }
}
