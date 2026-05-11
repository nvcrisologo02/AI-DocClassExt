using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Data.Repositories;

public class PluginTipologiaConfigRepository : IPluginTipologiaConfigRepository
{
    private readonly DocumentIADbContext _context;

    public PluginTipologiaConfigRepository(DocumentIADbContext context)
    {
        _context = context;
    }

    public async Task<PluginTipologiaConfigEntity?> GetByTipologiaCodigoAsync(string tipologiaCodigo)
    {
        return await _context.PluginTipologiaConfigs
            .FirstOrDefaultAsync(x => x.TipologiaCodigo == tipologiaCodigo);
    }

    public async Task<PluginTipologiaConfigEntity?> GetPublishedByTipologiaCodigoAsync(string tipologiaCodigo)
    {
        return await _context.PluginTipologiaConfigs
            .FirstOrDefaultAsync(x => x.TipologiaCodigo == tipologiaCodigo && x.Estado == EstadoPluginConfig.Published);
    }

    public async Task<IReadOnlyCollection<PluginTipologiaConfigEntity>> GetAllAsync()
    {
        return await _context.PluginTipologiaConfigs
            .OrderBy(x => x.TipologiaCodigo)
            .ToListAsync();
    }

    public async Task<PluginTipologiaConfigEntity> UpsertDraftAsync(string tipologiaCodigo, string configuracionJson, string? usuario)
    {
        var current = await GetByTipologiaCodigoAsync(tipologiaCodigo);
        if (current is null)
        {
            current = new PluginTipologiaConfigEntity
            {
                TipologiaCodigo = tipologiaCodigo,
                ConfiguracionJson = configuracionJson,
                Estado = EstadoPluginConfig.Draft,
                FechaCreacion = DateTime.UtcNow,
                FechaActualizacion = DateTime.UtcNow
            };
            _context.PluginTipologiaConfigs.Add(current);
        }
        else
        {
            current.ConfiguracionJson = configuracionJson;
            current.Estado = EstadoPluginConfig.Draft;
            current.FechaActualizacion = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return current;
    }

    public async Task PublishAsync(string tipologiaCodigo, string? usuario)
    {
        var current = await GetByTipologiaCodigoAsync(tipologiaCodigo)
            ?? throw new KeyNotFoundException($"No existe configuracion de plugins para tipologia '{tipologiaCodigo}'.");

        current.Estado = EstadoPluginConfig.Published;
        current.PublicadaEn = DateTime.UtcNow;
        current.PublicadaPor = usuario;
        current.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task RetireAsync(string tipologiaCodigo)
    {
        var current = await GetByTipologiaCodigoAsync(tipologiaCodigo)
            ?? throw new KeyNotFoundException($"No existe configuracion de plugins para tipologia '{tipologiaCodigo}'.");

        current.Estado = EstadoPluginConfig.Retired;
        current.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
