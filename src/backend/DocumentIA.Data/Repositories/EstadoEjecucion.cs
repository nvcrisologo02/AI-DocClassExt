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
        public static readonly string[] Error = { "Error", "ERROR", "Fallido" };
    }
}
