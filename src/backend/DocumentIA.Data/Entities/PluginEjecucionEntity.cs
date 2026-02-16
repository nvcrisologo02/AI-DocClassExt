using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities
{
    [Table("PluginEjecuciones")]
    public class PluginEjecucionEntity
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int EjecucionId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string PluginKey { get; set; } = string.Empty;
        
        public int Priority { get; set; }
        
        public bool Success { get; set; }
        
        [MaxLength(500)]
        public string? Mensaje { get; set; }
        
        public int StatusCode { get; set; }
        
        public int DurationMs { get; set; }
        
        [Column(TypeName = "nvarchar(max)")]
        public string? Error { get; set; }
        
        [Column(TypeName = "nvarchar(max)")]
        public string? DatosEnriquecidosJson { get; set; }
        
        public DateTime FechaEjecucion { get; set; } = DateTime.UtcNow;
        
        [ForeignKey(nameof(EjecucionId))]
        public virtual DocumentoEjecucionEntity Ejecucion { get; set; } = null!;
    }
}
