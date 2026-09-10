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

    public async Task<IEnumerable<DocumentoEntity>> GetDocumentosConBlobExpiradosAsync(int top)
    {
        var batchSize = top <= 0 ? 200 : top;
        var nowUtc = DateTime.UtcNow;

        return await _context.Documentos
            .Where(d => d.RutaBlobStorage != null && d.RutaBlobStorage != string.Empty)
            .Where(d => d.FechaExpiracionBlob.HasValue && d.FechaExpiracionBlob.Value <= nowUtc)
            .OrderBy(d => d.FechaExpiracionBlob)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<DocumentoEntity> AddAsync(DocumentoEntity documento)
    {
        _context.Documentos.Add(documento);
        await _context.SaveChangesAsync();
        return documento;
    }

    // Las cuatro columnas de markdown no se escriben nunca desde aqui (AB#100254): sus unicos
    // escritores son el alta del documento y ActualizarMarkdownSiMejoraAsync, que compara cobertura
    // en el propio UPDATE y por tanto es atomico. Update() marca TODA la entidad como modificada,
    // asi que el UPDATE generado incluia esas columnas con los valores que se leyeron al abrir la
    // actividad; si entretanto otra ejecucion del mismo SHA256 mejoraba la cobertura, este
    // SaveChanges la revertia a la anterior.
    public async Task UpdateAsync(DocumentoEntity documento)
    {
        documento.FechaActualizacion = DateTime.UtcNow;
        var entrada = _context.Documentos.Update(documento);
        entrada.Property(d => d.NormalizacionMarkdownGzip).IsModified = false;
        entrada.Property(d => d.NormalizacionMarkdownCompressed).IsModified = false;
        entrada.Property(d => d.MarkdownPaginas).IsModified = false;
        entrada.Property(d => d.MarkdownCompleto).IsModified = false;
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

    public async Task<int> ActualizarMarkdownSiMejoraAsync(
        string sha256, byte[] gzip, string base64, int paginas, bool completo, bool forzar)
    {
        var ahora = DateTime.UtcNow;

        return await _context.Documentos
            .Where(d => d.SHA256 == sha256)
            .Where(d =>
                forzar
                // no habia nada: ni binario ni el Base64 historico anterior a AB#100169
                || (d.NormalizacionMarkdownGzip == null && d.NormalizacionMarkdownCompressed == null)
                // pasamos a completo
                || (completo && !d.MarkdownCompleto)
                // mas paginas que un parcial de cobertura conocida
                || (!completo && !d.MarkdownCompleto && d.MarkdownPaginas != null && d.MarkdownPaginas < paginas))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.NormalizacionMarkdownGzip, gzip)
                .SetProperty(d => d.NormalizacionMarkdownCompressed, base64)
                .SetProperty(d => d.MarkdownPaginas, paginas)
                .SetProperty(d => d.MarkdownCompleto, completo)
                .SetProperty(d => d.FechaActualizacion, ahora));
    }
}
