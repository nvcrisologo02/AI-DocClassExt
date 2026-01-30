using Microsoft.EntityFrameworkCore;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public class AuditoriaRepository : IAuditoriaRepository
{
    private readonly DocumentIADbContext _context;

    public AuditoriaRepository(DocumentIADbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditoriaEntity auditoria)
    {
        _context.Auditoria.Add(auditoria);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AuditoriaEntity>> GetByDocumentoIdAsync(int documentoId)
    {
        return await _context.Auditoria
            .Where(a => a.DocumentoId == documentoId)
            .OrderByDescending(a => a.FechaHora)
            .ToListAsync();
    }
}
