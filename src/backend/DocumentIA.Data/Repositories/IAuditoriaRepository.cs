using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public interface IAuditoriaRepository
{
    Task AddAsync(AuditoriaEntity auditoria);
    Task<IEnumerable<AuditoriaEntity>> GetByDocumentoIdAsync(int documentoId);
}
