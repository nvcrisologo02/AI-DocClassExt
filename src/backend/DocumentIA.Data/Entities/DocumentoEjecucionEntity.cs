using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.Data.Entities
{
    [Table("DocumentoEjecuciones")]
    public class DocumentoEjecucionEntity
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int DocumentoId { get; set; }
        
        [Required]
        [MaxLength(36)]
        public string EjecucionGuid { get; set; } = Guid.NewGuid().ToString();
        
        public DateTime FechaEjecucion { get; set; } = DateTime.UtcNow;

        /// <summary>ID de instancia Durable Functions. Correlaciona con el estado en el portal de Azure.</summary>
        [MaxLength(200)]
        public string? InstanceId { get; set; }

        /// <summary>W3C TraceId de App Insights (operation_Id). Usar en KQL: union traces,requests | where operation_Id == OperationId.</summary>
        [MaxLength(100)]
        public string? OperationId { get; set; }
        
        [MaxLength(100)]
        public string? Tipologia { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string EstadoFinal { get; set; } = string.Empty;
        
        public double ConfianzaGlobal { get; set; }
        
        [MaxLength(200)]
        public string? ModeloClasificacion { get; set; }
        
        public double ConfianzaClasificacion { get; set; }
        
        public bool UseFallbackLLM { get; set; }

        /// <summary>
        /// Indica si esta ejecución se realizó en modo solo clasificación.
        /// </summary>
        public bool ClassificationOnly { get; set; }
        
        [Column(TypeName = "nvarchar(max)")]
        public string? DatosOriginalesJson { get; set; }
        
        [Column(TypeName = "nvarchar(max)")]
        public string? DatosFinalesJson { get; set; }
        
         [Column(TypeName = "nvarchar(max)")]
        public string? ContratoSalidaCompletoJson { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? ActivityTimelineJson { get; set; }

        public int DuracionTotalMs { get; set; }

        public int? DuracionClasificacionMs { get; set; }

        public int? DuracionExtraccionMs { get; set; }

        public int? DuracionValidacionMs { get; set; }

        public int? DuracionIntegracionMs { get; set; }

        public int? DuracionGDCMs { get; set; }

        public int? DuracionPersistenciaMs { get; set; }

        public int? DuracionAssetResolverMs { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? AssetResolverResultJson { get; set; }
        
        [ForeignKey(nameof(DocumentoId))]
        public virtual DocumentoEntity Documento { get; set; } = null!;
        
        public virtual ICollection<PluginEjecucionEntity> PluginsEjecutados { get; set; } 
            = new List<PluginEjecucionEntity>();
        
        public virtual ICollection<ValidacionResultadoEntity> Validaciones { get; set; } 
            = new List<ValidacionResultadoEntity>();
    }
}
