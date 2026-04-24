using Microsoft.EntityFrameworkCore;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories;

public class DocumentoRepository : IDocumentoRepository
{
    private readonly DocumentIADbContext _context;

    public DocumentoRepository(DocumentIADbContext context)
    {
        _context = context;
    }

    public async Task<DocumentoEntity?> GetByIdAsync(int id)
    {
        return await _context.Documentos
            .Include(d => d.Resultado)
            .Include(d => d.Auditorias)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<DocumentoEntity?> GetByGuidAsync(string guid)
    {
        return await _context.Documentos
            .Include(d => d.Resultado)
            .Include(d => d.Auditorias)
            .FirstOrDefaultAsync(d => d.Guid == guid);
    }

    public async Task<DocumentoEntity?> GetBySHA256Async(string sha256)
    {
        return await _context.Documentos
            .Include(d => d.Resultado)
            .FirstOrDefaultAsync(d => d.SHA256 == sha256);
    }

    public async Task<DocumentoEntity?> GetByMD5Async(string md5)
    {
        return await _context.Documentos
            .Include(d => d.Resultado)
            .FirstOrDefaultAsync(d => d.MD5 == md5);
    }

    public async Task<DocumentoEntity?> GetByCorrelationIdAsync(string correlationId)
    {
        return await _context.Documentos
            .Include(d => d.Resultado)
            .Include(d => d.Auditorias)
            .FirstOrDefaultAsync(d => d.CorrelationId == correlationId);
    }

    public async Task<IEnumerable<DocumentoEntity>> GetAllAsync()
    {
        return await _context.Documentos
            .Include(d => d.Resultado)
            .OrderByDescending(d => d.FechaCreacion)
            .ToListAsync();
    }

    public async Task<IEnumerable<DocumentoEntity>> GetByEstadoAsync(string estado)
    {
        return await _context.Documentos
            .Include(d => d.Resultado)
            .Where(d => d.Estado == estado)
            .OrderByDescending(d => d.FechaCreacion)
            .ToListAsync();
    }

    public async Task<DocumentoEntity> AddAsync(DocumentoEntity documento)
    {
        _context.Documentos.Add(documento);
        await _context.SaveChangesAsync();
        return documento;
    }

    public async Task UpdateAsync(DocumentoEntity documento)
    {
        documento.FechaActualizacion = DateTime.UtcNow;
        _context.Documentos.Update(documento);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var documento = await _context.Documentos.FindAsync(id);
        if (documento != null)
        {
            _context.Documentos.Remove(documento);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsBySHA256Async(string sha256)
    {
        return await _context.Documentos.AnyAsync(d => d.SHA256 == sha256);
    }
}
