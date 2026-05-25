namespace DocumentIA.Data.Repositories;

public interface ICatalogoTdnRepository
{
    Task<IReadOnlyCollection<TdnCatalogItem>> GetFamiliasTdnActivasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TdnCatalogItem>> GetSubtiposByFamiliaAsync(string tdn1Codigo, CancellationToken cancellationToken = default);
}

public sealed record TdnCatalogItem(string Codigo, string Descripcion);