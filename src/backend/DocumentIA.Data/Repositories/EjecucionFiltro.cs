using System;

namespace DocumentIA.Data.Repositories
{
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
        /// <summary>Origen exacto declarado por el consumidor, por ejemplo Colabora.</summary>
        public string? SourceSystem { get; set; }

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
    }
}
