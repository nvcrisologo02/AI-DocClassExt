using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public interface IModeloConfigRepository
{
    Task<ModeloConfigEntity?> GetByIdAsync(int id);
    Task<ModeloConfigEntity?> GetByKeyAsync(string key);
    Task<IReadOnlyCollection<ModeloConfigEntity>> GetAllActivosByTipoAsync(TipoModelo tipo);
    Task<ModeloConfigEntity> AddAsync(ModeloConfigEntity modelo);
    Task UpdateAsync(ModeloConfigEntity modelo);
}