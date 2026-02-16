using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Repositories
{
    public class DocumentoEjecucionRepository : IDocumentoEjecucionRepository
    {
        private readonly DocumentIADbContext _context;

        public DocumentoEjecucionRepository(DocumentIADbContext context)
        {
            _context = context;
        }

        public async Task<DocumentoEjecucionEntity?> GetByIdAsync(int id)
        {
            return await _context.DocumentoEjecuciones
                .Include(e => e.PluginsEjecutados)
                .Include(e => e.Validaciones)
                .Include(e => e.Documento)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<DocumentoEjecucionEntity?> GetByGuidAsync(string guid)
        {
            return await _context.DocumentoEjecuciones
                .Include(e => e.PluginsEjecutados)
                .Include(e => e.Validaciones)
                .Include(e => e.Documento)
                .FirstOrDefaultAsync(e => e.EjecucionGuid == guid);
        }

        public async Task<IEnumerable<DocumentoEjecucionEntity>> GetByDocumentoIdAsync(int documentoId)
        {
            return await _context.DocumentoEjecuciones
                .Include(e => e.PluginsEjecutados)
                .Include(e => e.Validaciones)
                .Where(e => e.DocumentoId == documentoId)
                .OrderByDescending(e => e.FechaEjecucion)
                .ToListAsync();
        }

        public async Task<DocumentoEjecucionEntity> AddAsync(DocumentoEjecucionEntity ejecucion)
        {
            _context.DocumentoEjecuciones.Add(ejecucion);
            await _context.SaveChangesAsync();
            return ejecucion;
        }

        public async Task<IEnumerable<DocumentoEjecucionEntity>> GetUltimasEjecucionesAsync(int top = 10)
        {
            return await _context.DocumentoEjecuciones
                .Include(e => e.Documento)
                .Include(e => e.PluginsEjecutados)
                .Include(e => e.Validaciones)
                .OrderByDescending(e => e.FechaEjecucion)
                .Take(top)
                .ToListAsync();
        }
    }
}
