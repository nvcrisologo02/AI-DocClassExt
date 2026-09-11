using System;

namespace DocumentIA.Data.Repositories
{
    /// <summary>
    /// Tratamiento de las filas de reutilizacion por duplicado en una consulta.
    /// El valor por defecto (0) las excluye: una reutilizacion no es una ejecucion de IA
    /// y contarla falsearia calidad, volumen y coste (AB#100258).
    /// </summary>
    public enum FiltroReutilizadas
    {
        Excluir = 0,
        Incluir = 1,
        Solo = 2
    }

    /// <summary>
    /// Recorte compartido por el listado y por los agregados. Que ambos usen el
    /// mismo tipo es lo que impide que la cabecera de KPIs y la tabla describan
    /// conjuntos distintos.
    /// </summary>
    public class EjecucionFiltro
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public string? Tipologia { get; set; }
        /// <summary>Categoria normalizada: OK, REVISION o ERROR.</summary>
        public string? Estado { get; set; }
        /// <summary>"Clasificacion" o "Completo".</summary>
        public string? Flujo { get; set; }
        /// <summary>GUID exacto o fragmento del nombre de documento.</summary>
        public string? Busqueda { get; set; }
        /// <summary>Fragmento de quien solicito la ejecucion (Documentos.SubmittedBy).</summary>
        public string? SubmittedBy { get; set; }

        /// <summary>
        /// Valor exacto de EstadoFinal. A diferencia de <see cref="Estado"/>, que
        /// agrupa en las tres categorias historicas y deja fuera estados como
        /// VALIDACION_CON_ERRORES, aqui se filtra por el estado tal cual.
        /// </summary>
        public string? EstadoProceso { get; set; }

        /// <summary>Calidad por confianza: OK, REVISION o ERROR. Ver CalidadEjecucion.</summary>
        public string? Calidad { get; set; }

        /// <summary>Tramo de confianza global; Max es exclusivo. Alimenta el histograma.</summary>
        public double? ConfianzaMin { get; set; }
        public double? ConfianzaMax { get; set; }

        /// <summary>
        /// Solo lo usan los agregados de coste (AB#100237). Por defecto los importes
        /// excluyen las ejecuciones cuyo coste procede del relleno retroactivo; con
        /// true entran tambien. Los recuentos por origen no dependen de este flag.
        /// </summary>
        public bool IncluirEstimados { get; set; }

        /// <summary>
        /// Que hacer con las peticiones servidas por reutilizacion de duplicado. Por
        /// defecto se excluyen, de modo que toda consulta existente conserva sus numeros
        /// sin tener que tocarla (AB#100258).
        /// </summary>
        public FiltroReutilizadas Reutilizadas { get; set; } = FiltroReutilizadas.Excluir;
    }
}
