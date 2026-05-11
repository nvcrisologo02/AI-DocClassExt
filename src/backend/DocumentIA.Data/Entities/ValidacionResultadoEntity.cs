using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities
{
    [Table("ValidacionResultados")]
    public class ValidacionResultadoEntity
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int EjecucionId { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Campo { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string Severidad { get; set; } = string.Empty;
        
        [Column(TypeName = "nvarchar(max)")]
        public string? Mensaje { get; set; }
        
        [MaxLength(500)]
        public string? ValorOriginal { get; set; }
        
        [MaxLength(500)]
        public string? ValorEsperado { get; set; }
        
        public bool Pasado { get; set; }
        
        public DateTime FechaValidacion { get; set; } = DateTime.UtcNow;
        
        [ForeignKey(nameof(EjecucionId))]
        public virtual DocumentoEjecucionEntity Ejecucion { get; set; } = null!;
    }
}
