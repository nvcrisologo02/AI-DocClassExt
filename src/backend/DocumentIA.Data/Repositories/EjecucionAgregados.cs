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
    }
}
