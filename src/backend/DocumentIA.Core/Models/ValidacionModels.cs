using System.Collections.Generic;

namespace DocumentIA.Core.Models
{
    public class ValidacionInput
    {
        public string Tipologia { get; set; } = string.Empty;
        public Dictionary<string, object?> DatosExtraidos { get; set; } = new Dictionary<string, object?>();
    }

    public class DetalleValidacion
    {
        public int TotalReglas { get; set; }
        public int ReglasAplicadas { get; set; }
        public int Errores { get; set; }
        public int Warnings { get; set; }
        public List<ItemValidacion> Validaciones { get; set; }
        public double ConfianzaValidacion { get; set; }

        public DetalleValidacion()
        {
            Validaciones = new List<ItemValidacion>();
        }
    }

    public class ItemValidacion
    {
        public string Campo { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Severidad { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string Sugerencia { get; set; } = string.Empty;
    }
}
