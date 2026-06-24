using System.Collections.Generic;

namespace DocumentIA.Core.Models
{
    // Tipos de compatibilidad para pruebas y migraciones de nombres.
    // Añadidos temporalmente para que tests que esperan estos tipos compilen.
    public class ResultadoValidacion
    {
        public bool EsValido { get; set; }
        public double Score { get; set; }
        public List<ResultadoReglaValidacion> Reglas { get; set; } = new List<ResultadoReglaValidacion>();
    }

    public class ResultadoReglaValidacion
    {
        public string Campo { get; set; } = string.Empty;
        public bool Pasado { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }

    public class ObtenerActivoOutput
    {
        public bool ActivoEncontrado { get; set; }
        public List<Dictionary<string, object>> Coincidencias { get; set; } = new List<Dictionary<string, object>>();
    }
}
