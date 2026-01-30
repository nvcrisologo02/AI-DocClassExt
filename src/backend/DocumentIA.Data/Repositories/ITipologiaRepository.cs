using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public interface ITipologiaRepository
{
    Task<TipologiaEntity?> GetByIdAsync(int id);
    Task<TipologiaEntity?> GetByCodigoAsync(string codigo);
    Task<IEnumerable<TipologiaEntity>> GetAllActivasAsync();
    Task<TipologiaEntity> AddAsync(TipologiaEntity tipologia);
    Task UpdateAsync(TipologiaEntity tipologia);
    Task DeleteAsync(int id);
}
