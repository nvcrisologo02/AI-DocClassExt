using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities;

public enum EstadoPluginConfig
{
    Draft = 0,
    Published = 1,
    Retired = 2
}

[Table("PluginTipologiaConfigs")]
public class PluginTipologiaConfigEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string TipologiaCodigo { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string ConfiguracionJson { get; set; } = "{}";

    public EstadoPluginConfig Estado { get; set; } = EstadoPluginConfig.Draft;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }
    public DateTime? PublicadaEn { get; set; }

    [MaxLength(200)]
    public string? PublicadaPor { get; set; }
}
