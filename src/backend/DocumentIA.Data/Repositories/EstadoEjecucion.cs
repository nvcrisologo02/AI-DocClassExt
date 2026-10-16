namespace DocumentIA.Data.Repositories
{
    /// <summary>
    /// Valores que puede tomar EstadoFinal para cada categoria. Existen variantes
    /// historicas de mayusculas y de idioma, asi que la clasificacion se define aqui
    /// una sola vez. Se usan con Contains para que EF Core lo traduzca a IN (...).
    /// </summary>
    public static class EstadoEjecucion
    {
        public static readonly string[] Ok = { "OK", "Completado", "Completed" };
        public static readonly string[] Revision = { "REVISION", "Revision" };
        public static readonly string[] Error = { "Error", "ERROR", "Fallido", "SIN_CONTENIDO_DOCUMENTO" }; // sin contenido del documento es un fallo de proceso, cuenta y filtra como error

        /// <summary>
        /// Estados de fallo de proceso (AB#100956): la ejecucion no produjo un resultado util y
        /// un reintento debe reprocesar. Es Error mas el estado retriable.
        /// </summary>
        public static readonly string[] Fallidos = Error.Concat(new[] { "PENDIENTE_REINTENTO" }).ToArray();

        /// <summary>
        /// Lo que nunca es candidata a reutilizacion por duplicado: los fallidos y el
        /// DUPLICADO vacio de la regla anterior (AB#100178). Lista de exclusion a proposito:
        /// el historico tiene variantes de exito y revision que una lista blanca dejaria fuera.
        /// </summary>
        public static readonly string[] NoReutilizables = Fallidos.Concat(new[] { "DUPLICADO" }).ToArray();
    }
}
