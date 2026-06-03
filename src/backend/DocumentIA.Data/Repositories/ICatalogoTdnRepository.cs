namespace DocumentIA.Data.Repositories;

public interface ICatalogoTdnRepository
{
    Task<IReadOnlyCollection<TdnCatalogItem>> GetFamiliasTdnActivasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TdnCatalogItem>> GetSubtiposByFamiliaAsync(string tdn1Codigo, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Obtiene el prompt personalizado para clasificación TDN2 de una familia específica.
    /// </summary>
    /// <param name="tdn1Codigo">Código de la familia TDN1 (ej: "ESCR", "CANC")</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Prompt personalizado si está configurado, null si debe usarse generación dinámica</returns>
    Task<string?> GetTdn2PromptByFamiliaAsync(string tdn1Codigo, CancellationToken cancellationToken = default);
}

public sealed record TdnCatalogItem(string Codigo, string Nombre, string Descripcion);