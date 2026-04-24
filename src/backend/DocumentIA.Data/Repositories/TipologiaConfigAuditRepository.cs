using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Data.Repositories;

public class TipologiaConfigAuditRepository : ITipologiaConfigAuditRepository
{
    private readonly DocumentIADbContext _context;

    public TipologiaConfigAuditRepository(DocumentIADbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TipologiaConfigAuditEntity audit)
    {
        _context.TipologiaConfigAudit.Add(audit);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyCollection<TipologiaConfigAuditEntity>> GetByTipologiaIdAsync(int tipologiaId, int take = 200)
    {
        return await _context.TipologiaConfigAudit
            .Where(x => x.TipologiaId == tipologiaId)
            .OrderByDescending(x => x.FechaHora)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync();
    }
}
