using System;

namespace DocumentIA.Data.Repositories
{
    /// <summary>
    /// Proyeccion del listado del Monitor. Existe para que la pagina no arrastre
    /// los LOB de la entidad (contrato de salida, datos originales/finales,
    /// markdown del documento): con la entidad completa cada pagina de 25 filas
    /// movia ~1 MB para pintar una tabla de escalares.
    /// </summary>
    public class EjecucionListadoItem
    {
        public int Id { get; set; }
        public string EjecucionGuid { get; set; } = string.Empty;
        public DateTime FechaEjecucion { get; set; }
        public string? Tipologia { get; set; }
        public bool ClassificationOnly { get; set; }
        public string EstadoFinal { get; set; } = string.Empty;
        public double ConfianzaGlobal { get; set; }
        public double ConfianzaClasificacion { get; set; }
        public bool UseFallbackLLM { get; set; }
        public int DuracionTotalMs { get; set; }
        public int? DuracionClasificacionMs { get; set; }
        public int? DuracionExtraccionMs { get; set; }
        public int? DuracionGDCMs { get; set; }
        public int? DuracionValidacionMs { get; set; }
        public int? DuracionIntegracionMs { get; set; }
        public int? DuracionPersistenciaMs { get; set; }
        public string? NombreDocumento { get; set; }
        public string? SubmittedBy { get; set; }
        public string? ActivityTimelineJson { get; set; }

        /// <summary>Coste de IA de la ejecucion; nulo si no se registro (AB#100238).</summary>
        public decimal? CosteIAEur { get; set; }

        /// <summary>True si el coste procede del relleno retroactivo.</summary>
        public bool CosteEstimado { get; set; }

        /// <summary>
        /// True si la fila registra una peticion servida reutilizando otra ejecucion:
        /// no se reproceso el documento y no tiene contrato ni coste propios (AB#100258).
        /// </summary>
        public bool ReutilizadaPorDuplicado { get; set; }

        /// <summary>Id de la ejecucion cuyo contrato se devolvio; null si no es una reutilizacion.</summary>
        public int? EjecucionOriginalId { get; set; }
    }
}
