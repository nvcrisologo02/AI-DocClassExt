namespace DocumentIA.Data.Repositories
{
    /// <summary>
    /// Calidad de una ejecucion segun su confianza global.
    /// </summary>
    /// <remarks>
    /// Es una dimension distinta de <see cref="EstadoEjecucion"/>: aquella dice si
    /// la tuberia llego al final, esta dice cuanta confianza merece el resultado.
    /// Una ejecucion puede completarse sin incidencias y aun asi necesitar
    /// revision, que es precisamente el caso que el recuento por EstadoFinal no
    /// permitia ver.
    ///
    /// Los umbrales viven aqui, y no en la configuracion de confianza de
    /// DocumentIA.Core, porque Core ya depende de este proyecto y la dependencia
    /// inversa cerraria un ciclo. ConfidenceConfig los toma de aqui para que haya
    /// una unica definicion.
    ///
    /// Limitacion conocida: una tipologia puede redefinir sus propios umbrales en
    /// ConfidenceConfig. Los recuentos agregados usan siempre los globales, porque
    /// la consulta agrupa sobre la columna ConfianzaGlobal sin conocer la
    /// configuracion de cada tipologia. Persistir el EstadoCalidad ya calculado
    /// resolveria esa discrepancia.
    /// </remarks>
    public static class CalidadEjecucion
    {
        public const string Ok = "OK";
        public const string Revision = "REVISION";
        public const string Error = "ERROR";

        /// <summary>Confianza global minima para considerar el resultado fiable.</summary>
        public const double UmbralOk = 0.85;

        /// <summary>Por debajo de este valor el resultado no se considera utilizable.</summary>
        public const double UmbralRevision = 0.70;

        public static string Clasificar(double confianzaGlobal) =>
            confianzaGlobal >= UmbralOk ? Ok
            : confianzaGlobal >= UmbralRevision ? Revision
            : Error;
    }
}
