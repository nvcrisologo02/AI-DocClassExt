using DocumentIA.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Data.Repositories;

public class CatalogoTdnRepository : ICatalogoTdnRepository
{
    private readonly DocumentIADbContext _context;

    public CatalogoTdnRepository(DocumentIADbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<TdnCatalogItem>> GetFamiliasTdnActivasAsync(CancellationToken cancellationToken = default)
    {
        // El catálogo actual no tiene flag Activo; para esta fase se consideran activas
        // todas las familias disponibles en tabla con código informado.
        return await _context.CatalogoTdn1
            .AsNoTracking()
            .Where(t => !string.IsNullOrWhiteSpace(t.Codigo))
            .OrderBy(t => t.Codigo)
            .Select(t => new TdnCatalogItem(
                t.Codigo,
                string.IsNullOrWhiteSpace(t.Descripcion) ? t.Nombre : t.Descripcion!))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TdnCatalogItem>> GetSubtiposByFamiliaAsync(string tdn1Codigo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tdn1Codigo))
        {
            throw new ArgumentException("El código de familia TDN1 es obligatorio.", nameof(tdn1Codigo));
        }

        var normalizedFamily = tdn1Codigo.Trim().ToUpperInvariant();
        var familyPrefix = $"{normalizedFamily}-";

        return await _context.CatalogoTdn2
            .AsNoTracking()
            .Where(t => !string.IsNullOrWhiteSpace(t.Codigo) && t.Codigo.ToUpper().StartsWith(familyPrefix))
            .OrderBy(t => t.Codigo)
            .Select(t => new TdnCatalogItem(
                t.Codigo,
                string.IsNullOrWhiteSpace(t.Descripcion) ? t.Nombre : t.Descripcion!))
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetTdn2PromptByFamiliaAsync(string tdn1Codigo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tdn1Codigo))
        {
            throw new ArgumentException("El código de familia TDN1 es obligatorio.", nameof(tdn1Codigo));
        }

        var normalizedCodigo = tdn1Codigo.Trim().ToUpperInvariant();

        var familia = await _context.CatalogoTdn1
            .AsNoTracking()
            .Where(t => t.Codigo.ToUpper() == normalizedCodigo)
            .Select(t => t.TDN2_Prompt)
            .FirstOrDefaultAsync(cancellationToken);

        return familia;
    }
}