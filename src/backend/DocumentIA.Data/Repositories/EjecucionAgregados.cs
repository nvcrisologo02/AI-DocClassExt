using System;
using System.Collections.Generic;

namespace DocumentIA.Data.Repositories
{
    public class AgregadoGrupo
    {
        public string Grupo { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Ok { get; set; }
        public int Revision { get; set; }
        public int Error { get; set; }
        public int Fallbacks { get; set; }
        public double ConfianzaMedia { get; set; }
        public double DuracionMediaMs { get; set; }
    }

    public class SeriePunto
    {
        public DateTime Fecha { get; set; }
        public int Total { get; set; }
        public int Ok { get; set; }
        public int Revision { get; set; }
        public int Error { get; set; }
        public int Fallbacks { get; set; }
    }

    /// <summary>Una celda del cruce entre estado de proceso y calidad.</summary>
    public class MatrizCelda
    {
        public string EstadoProceso { get; set; } = string.Empty;
        public string Calidad { get; set; } = string.Empty;
        public int Total { get; set; }
    }

    /// <summary>Tramo del histograma de confianza global. Hasta es exclusivo.</summary>
    public class HistogramaBin
    {
        public double Desde { get; set; }
        public double Hasta { get; set; }
        public int Total { get; set; }
    }

    public class EjecucionAgregadosResult
    {
        public int TotalEjecuciones { get; set; }
        public int PeriodoDias { get; set; }
        public int Ok { get; set; }
        public int Revision { get; set; }
        public int Error { get; set; }
        public int FallbacksTotal { get; set; }
        public double ConfianzaGlobalMedia { get; set; }
        public double DuracionMediaMs { get; set; }
        public List<AgregadoGrupo> PorTipologia { get; set; } = new List<AgregadoGrupo>();
        public List<AgregadoGrupo> PorModelo { get; set; } = new List<AgregadoGrupo>();
        public List<SeriePunto> Serie { get; set; } = new List<SeriePunto>();

        // ── Calidad por confianza (Monitor v2) ────────────────────────────────
        // Ok/Revision/Error de mas arriba cuentan por EstadoFinal, es decir por lo
        // que hizo el proceso. Estos cuentan por la confianza del resultado, que es
        // una dimension independiente: una ejecucion puede completarse sin
        // incidencias y necesitar revision igualmente.

        public int CalidadOk { get; set; }
        public int CalidadRevision { get; set; }
        public int CalidadError { get; set; }

        /// <summary>
        /// Reparto por EstadoFinal sin agrupar en categorias fijas, de modo que
        /// ningun estado quede sin contar (el recuento por las tres listas de
        /// EstadoEjecucion dejaba fuera estados como VALIDACION_CON_ERRORES).
        /// </summary>
        public List<AgregadoGrupo> PorEstadoProceso { get; set; } = new List<AgregadoGrupo>();

        public List<MatrizCelda> Matriz { get; set; } = new List<MatrizCelda>();
        public List<HistogramaBin> Histograma { get; set; } = new List<HistogramaBin>();
    }
}
