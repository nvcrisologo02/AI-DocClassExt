using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public interface ITipologiaRepository
{
    Task<TipologiaEntity?> GetByIdAsync(int id);
    Task<TipologiaEntity?> GetByCodigoAsync(string codigo);
    Task<IReadOnlyCollection<TipologiaEntity>> GetAllPublishedAsync();
    Task<IEnumerable<TipologiaEntity>> GetAllActivasAsync();
    Task<TipologiaEntity> AddAsync(TipologiaEntity tipologia, string? usuario = null);
    Task UpdateAsync(TipologiaEntity tipologia, string? usuario = null, string accion = "Updated");
    Task PublicarAsync(int id, string publicadaPor);
    Task RetirarAsync(int id, string? retiradaPor = null);
    Task PasarADraftAsync(int id, string? usuario = null);
    Task DeleteAsync(int id);
}
