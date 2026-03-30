using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Data.Repositories;

public class ModeloConfigRepository : IModeloConfigRepository
{
    private readonly DocumentIADbContext _context;

    public ModeloConfigRepository(DocumentIADbContext context)
    {
        _context = context;
    }

    public async Task<ModeloConfigEntity?> GetByIdAsync(int id)
    {
        return await _context.ModeloConfigs.FindAsync(id);
    }

    public async Task<ModeloConfigEntity?> GetByKeyAsync(string key)
    {
        return await _context.ModeloConfigs
            .FirstOrDefaultAsync(m => m.Key == key);
    }

    public async Task<IReadOnlyCollection<ModeloConfigEntity>> GetAllActivosByTipoAsync(TipoModelo tipo)
    {
        return await _context.ModeloConfigs
            .Where(m => m.Tipo == tipo && m.Activo)
            .OrderBy(m => m.Key)
            .ToListAsync();
    }

    public async Task<ModeloConfigEntity> AddAsync(ModeloConfigEntity modelo)
    {
        _context.ModeloConfigs.Add(modelo);
        await _context.SaveChangesAsync();
        return modelo;
    }

    public async Task UpdateAsync(ModeloConfigEntity modelo)
    {
        modelo.FechaActualizacion = DateTime.UtcNow;
        _context.ModeloConfigs.Update(modelo);
        await _context.SaveChangesAsync();
    }
}