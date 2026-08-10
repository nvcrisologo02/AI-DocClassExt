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
    }
}
