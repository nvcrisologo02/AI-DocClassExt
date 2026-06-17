using DocumentIA.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Data.Repositories;

public class CatalogoTdnRepository : ICatalogoTdnRepository
{
    private readonly DocumentIADbContext _context;
    private readonly ILogger<CatalogoTdnRepository> _logger;

    public CatalogoTdnRepository(DocumentIADbContext context, ILogger<CatalogoTdnRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TdnCatalogItem>> GetFamiliasTdnActivasAsync(CancellationToken cancellationToken = default)
    {
#nullable disable warnings
        // El catálogo actual no tiene flag Activo; para esta fase se consideran activas
        // todas las familias disponibles en tabla con código informado.
        return await _context.CatalogoTdn1
            .AsNoTracking()
            .Where(t => !string.IsNullOrWhiteSpace(t.Codigo))
            .OrderBy(t => t.Codigo)
            .Select(t => new TdnCatalogItem(
                t.Codigo,
                t.Nombre ?? string.Empty,
                (t.Descripcion ?? t.Nombre ?? string.Empty)!))
            .ToListAsync(cancellationToken);
#nullable restore warnings
    }

    public async Task<IReadOnlyCollection<TdnCatalogItem>> GetSubtiposByFamiliaAsync(string tdn1Codigo, CancellationToken cancellationToken = default)
    {
#nullable disable warnings
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
                t.Nombre ?? string.Empty,
                (t.Descripcion ?? t.Nombre ?? string.Empty)!))
            .ToListAsync(cancellationToken);
#nullable restore warnings
    }

    public async Task<string?> GetTdn2PromptByFamiliaAsync(string tdn1Codigo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tdn1Codigo))
        {
            throw new ArgumentException("El código de familia TDN1 es obligatorio.", nameof(tdn1Codigo));
        }

        var normalizedCodigo = tdn1Codigo.Trim().ToUpperInvariant();
        _logger.LogInformation("GetTdn2PromptByFamiliaAsync: Buscando TDN2_Prompt en CatalogoTdn1 donde Codigo.ToUpper() = '{NormalizedCodigo}'", normalizedCodigo);

        var familia = await _context.CatalogoTdn1
            .AsNoTracking()
            .Where(t => t.Codigo.ToUpper() == normalizedCodigo)
            .Select(t => new { t.Codigo, t.TDN2_Prompt })
            .FirstOrDefaultAsync(cancellationToken);

        if (familia == null)
        {
            _logger.LogWarning("GetTdn2PromptByFamiliaAsync: NO encontrado registro en CatalogoTdn1 para familia '{NormalizedCodigo}'", normalizedCodigo);
            return null;
        }

        if (string.IsNullOrWhiteSpace(familia.TDN2_Prompt))
        {
            _logger.LogWarning("GetTdn2PromptByFamiliaAsync: Registro encontrado para '{NormalizedCodigo}' pero TDN2_Prompt está NULL o vacío", normalizedCodigo);
            return null;
        }

        _logger.LogInformation("GetTdn2PromptByFamiliaAsync: ✓ TDN2_Prompt encontrado para '{Codigo}'. Longitud: {Length} chars", familia.Codigo, familia.TDN2_Prompt.Length);
        return familia.TDN2_Prompt;
    }
}