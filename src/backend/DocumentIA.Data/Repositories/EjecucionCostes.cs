using System;
using System.Collections.Generic;

namespace DocumentIA.Data.Repositories
{
    /// <summary>
    /// Agregados de coste de IA de un periodo (AB#100237). Se calculan sobre las
    /// columnas escalares de DocumentoEjecuciones, nunca sobre el contrato JSON.
    /// </summary>
    public class EjecucionCostesResult
    {
        public int TotalEjecuciones { get; set; }
        public int PeriodoDias { get; set; }

        /// <summary>Ejecuciones con coste medido por la propia ejecucion.</summary>
        public int ConCosteReal { get; set; }

        /// <summary>Ejecuciones cuyo coste procede del relleno retroactivo.</summary>
        public int ConCosteEstimado { get; set; }

        /// <summary>Ejecuciones sin ningun coste, anteriores a la funcionalidad y sin rellenar.</summary>
        public int SinCoste { get; set; }

        /// <summary>
        /// True si los importes incluyen las ejecuciones estimadas. Por defecto no:
        /// lo estimado se presenta aparte y solo entra si se pide.
        /// </summary>
        public bool IncluyeEstimados { get; set; }

        /// <summary>Ejecuciones que han entrado en los importes.</summary>
        public int EjecucionesConImporte { get; set; }

        public decimal CosteTotalEur { get; set; }

        /// <summary>Coste total entre las ejecuciones con importe.</summary>
        public decimal CosteMedioEur { get; set; }

        public long TokensTotales { get; set; }

        // Desglose por actividad: las cuatro suman CosteTotalEur.
        public decimal LayoutEur { get; set; }
        public decimal ClasificacionEur { get; set; }
        public decimal ExtraccionEur { get; set; }
        public decimal PromptEur { get; set; }

        public List<CosteGrupo> PorTipologia { get; set; } = new List<CosteGrupo>();
        public List<CosteGrupo> PorModelo { get; set; } = new List<CosteGrupo>();
        public List<CosteSeriePunto> Serie { get; set; } = new List<CosteSeriePunto>();
    }

    public class CosteGrupo
    {
        public string Grupo { get; set; } = string.Empty;
        public int Total { get; set; }
        public int ConImporte { get; set; }
        public decimal CosteEur { get; set; }
        public decimal CosteMedioEur { get; set; }
        public decimal LayoutEur { get; set; }
        public decimal ClasificacionEur { get; set; }
        public decimal ExtraccionEur { get; set; }
        public decimal PromptEur { get; set; }
    }

    public class CosteSeriePunto
    {
        public DateTime Fecha { get; set; }
        public int Total { get; set; }
        public decimal CosteEur { get; set; }
    }
}
