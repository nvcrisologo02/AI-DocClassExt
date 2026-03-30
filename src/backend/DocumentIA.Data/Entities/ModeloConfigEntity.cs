using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

public enum TipoModelo
{
    Clasificacion = 0,
    Extraccion = 1,
    Prompt = 2
}

[Table("ModeloConfigs")]
public class ModeloConfigEntity
{
    [Key]
    public int Id { get; set; }

    public TipoModelo Tipo { get; set; }

    [Required]
    [MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Provider { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    [Column(TypeName = "nvarchar(max)")]
    public string ConfiguracionJson { get; set; } = "{}";

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    [MaxLength(200)]
    public string? CreadoPor { get; set; }
}