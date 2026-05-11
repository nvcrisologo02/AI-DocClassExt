using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public interface ITipologiaConfigAuditRepository
{
    Task AddAsync(TipologiaConfigAuditEntity audit);
    Task<IReadOnlyCollection<TipologiaConfigAuditEntity>> GetByTipologiaIdAsync(int tipologiaId, int take = 200);
}
