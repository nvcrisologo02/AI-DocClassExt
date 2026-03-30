using Newtonsoft.Json;

namespace DocumentIA.Desktop.Models
{
    public class TipologiaPublicadaDto
    {
        [JsonProperty("identificador")]
        public string Identificador { get; set; } = string.Empty;

        [JsonProperty("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Nombre + versión extraída del identificador, ej. "Nota Simple 1.4"</summary>
        public string Display
        {
            get
            {
                var atIdx = Identificador.IndexOf('@');
                if (atIdx >= 0 && atIdx < Identificador.Length - 1)
                    return $"{Nombre}  {Identificador[(atIdx + 1)..]}";
                return Nombre;
            }
        }

        public override string ToString() => Display;
    }
}
