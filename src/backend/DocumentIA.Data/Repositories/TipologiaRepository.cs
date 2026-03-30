using Microsoft.EntityFrameworkCore;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public class TipologiaRepository : ITipologiaRepository
{
    private readonly DocumentIADbContext _context;

    public TipologiaRepository(DocumentIADbContext context)
    {
        _context = context;
    }

    public async Task<TipologiaEntity?> GetByIdAsync(int id)
    {
        return await _context.Tipologias.FindAsync(id);
    }

    public async Task<TipologiaEntity?> GetByCodigoAsync(string codigo)
    {
        return await _context.Tipologias
            .FirstOrDefaultAsync(t => t.Codigo == codigo);
    }

    public async Task<IReadOnlyCollection<TipologiaEntity>> GetAllPublishedAsync()
    {
        return await _context.Tipologias
            .Where(t => t.Estado == EstadoTipologia.Published && t.Activa)
            .OrderBy(t => t.Nombre)
            .ToListAsync();
    }

    public async Task<IEnumerable<TipologiaEntity>> GetAllActivasAsync()
    {
        return await _context.Tipologias
            .Where(t => t.Activa)
            .OrderBy(t => t.Nombre)
            .ToListAsync();
    }

    public async Task<TipologiaEntity> AddAsync(TipologiaEntity tipologia)
    {
        _context.Tipologias.Add(tipologia);
        await _context.SaveChangesAsync();
        return tipologia;
    }

    public async Task UpdateAsync(TipologiaEntity tipologia)
    {
        tipologia.FechaActualizacion = DateTime.UtcNow;
        _context.Tipologias.Update(tipologia);
        await _context.SaveChangesAsync();
    }

    public async Task PublicarAsync(int id, string publicadaPor)
    {
        var tipologia = await _context.Tipologias.FindAsync(id)
            ?? throw new KeyNotFoundException($"No se encontro la tipologia con id '{id}'.");

        tipologia.Estado = EstadoTipologia.Published;
        tipologia.PublicadaEn = DateTime.UtcNow;
        tipologia.PublicadaPor = publicadaPor;
        tipologia.VersionPublicada = tipologia.Version;
        tipologia.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task RetirarAsync(int id)
    {
        var tipologia = await _context.Tipologias.FindAsync(id)
            ?? throw new KeyNotFoundException($"No se encontro la tipologia con id '{id}'.");

        tipologia.Estado = EstadoTipologia.Retired;
        tipologia.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var tipologia = await _context.Tipologias.FindAsync(id);
        if (tipologia != null)
        {
            _context.Tipologias.Remove(tipologia);
            await _context.SaveChangesAsync();
        }
    }
}
