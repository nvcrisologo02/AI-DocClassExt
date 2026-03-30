using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public interface IPluginTipologiaConfigRepository
{
    Task<PluginTipologiaConfigEntity?> GetByTipologiaCodigoAsync(string tipologiaCodigo);
    Task<PluginTipologiaConfigEntity?> GetPublishedByTipologiaCodigoAsync(string tipologiaCodigo);
    Task<IReadOnlyCollection<PluginTipologiaConfigEntity>> GetAllAsync();
    Task<PluginTipologiaConfigEntity> UpsertDraftAsync(string tipologiaCodigo, string configuracionJson, string? usuario);
    Task PublishAsync(string tipologiaCodigo, string? usuario);
    Task RetireAsync(string tipologiaCodigo);
}
