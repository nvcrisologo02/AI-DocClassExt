using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public interface ITipologiaRepository
{
    Task<TipologiaEntity?> GetByIdAsync(int id);
    Task<TipologiaEntity?> GetByCodigoAsync(string codigo);
    Task<IReadOnlyCollection<TipologiaEntity>> GetAllPublishedAsync();
    Task<IEnumerable<TipologiaEntity>> GetAllActivasAsync();
    Task<TipologiaEntity> AddAsync(TipologiaEntity tipologia);
    Task UpdateAsync(TipologiaEntity tipologia);
    Task PublicarAsync(int id, string publicadaPor);
    Task RetirarAsync(int id);
    Task DeleteAsync(int id);
}
