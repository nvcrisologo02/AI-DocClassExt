using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Data.Repositories;

public class PromptTemplateRepository : IPromptTemplateRepository
{
    private readonly DocumentIADbContext _context;

    public PromptTemplateRepository(DocumentIADbContext context)
    {
        _context = context;
    }

    public async Task<PromptTemplateEntity?> GetActivePromptAsync(string promptKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptKey))
        {
            throw new ArgumentException("La clave del prompt es obligatoria.", nameof(promptKey));
        }

        var normalized = promptKey.Trim().ToLowerInvariant();

        return await _context.PromptTemplates
            .AsNoTracking()
            .Where(p => p.PromptKey.ToLower() == normalized && p.IsActive)
            .OrderByDescending(p => p.Version) // Por si hay múltiples activos (no debería), tomar el más reciente
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PromptTemplateEntity>> GetActivePromptsByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyPrefix))
        {
            throw new ArgumentException("El prefijo de clave es obligatorio.", nameof(keyPrefix));
        }

        var normalized = keyPrefix.Trim().ToLowerInvariant();

        return await _context.PromptTemplates
            .AsNoTracking()
            .Where(p => p.PromptKey.ToLower().StartsWith(normalized) && p.IsActive)
            .OrderBy(p => p.PromptKey)
            .ThenByDescending(p => p.Version)
            .ToListAsync(cancellationToken);
    }
}
