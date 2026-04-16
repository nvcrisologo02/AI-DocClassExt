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

        public async Task<EjecucionAgregadosResult> GetAgregadosAsync(int dias = 30)
        {
            var desde = DateTime.UtcNow.AddDays(-dias);
            var q = _context.DocumentoEjecuciones.Where(e => e.FechaEjecucion >= desde);

            var total = await q.CountAsync();

            int ok = 0, revision = 0, error = 0, fallbacks = 0;
            double confianzaMedia = 0, duracionMedia = 0;

            if (total > 0)
            {
                ok        = await q.CountAsync(e => e.EstadoFinal == "OK" || e.EstadoFinal == "Completado" || e.EstadoFinal == "Completed");
                revision  = await q.CountAsync(e => e.EstadoFinal == "REVISION" || e.EstadoFinal == "Revision");
                error     = await q.CountAsync(e => e.EstadoFinal == "Error" || e.EstadoFinal == "Fallido" || e.EstadoFinal == "ERROR");
                fallbacks = await q.CountAsync(e => e.UseFallbackLLM);
                confianzaMedia = await q.AverageAsync(e => e.ConfianzaGlobal);
                duracionMedia  = await q.AverageAsync(e => (double)e.DuracionTotalMs);
            }

            var byTipologia = await q
                .GroupBy(e => e.Tipologia == null ? "(sin tipología)" : e.Tipologia)
                .Select(g => new AgregadoGrupo
                {
                    Grupo          = g.Key,
                    Total          = g.Count(),
                    Ok             = g.Count(e => e.EstadoFinal == "OK" || e.EstadoFinal == "Completado" || e.EstadoFinal == "Completed"),
                    Revision       = g.Count(e => e.EstadoFinal == "REVISION" || e.EstadoFinal == "Revision"),
                    Error          = g.Count(e => e.EstadoFinal == "Error" || e.EstadoFinal == "Fallido" || e.EstadoFinal == "ERROR"),
                    Fallbacks      = g.Count(e => e.UseFallbackLLM),
                    ConfianzaMedia = g.Average(e => e.ConfianzaGlobal),
                    DuracionMediaMs = g.Average(e => (double)e.DuracionTotalMs)
                })
                .OrderByDescending(g => g.Total)
                .ToListAsync();

            var byModelo = await q
                .GroupBy(e => e.ModeloClasificacion == null ? "(sin modelo)" : e.ModeloClasificacion)
                .Select(g => new AgregadoGrupo
                {
                    Grupo          = g.Key,
                    Total          = g.Count(),
                    Ok             = g.Count(e => e.EstadoFinal == "OK" || e.EstadoFinal == "Completado" || e.EstadoFinal == "Completed"),
                    Revision       = g.Count(e => e.EstadoFinal == "REVISION" || e.EstadoFinal == "Revision"),
                    Error          = g.Count(e => e.EstadoFinal == "Error" || e.EstadoFinal == "Fallido" || e.EstadoFinal == "ERROR"),
                    Fallbacks      = g.Count(e => e.UseFallbackLLM),
                    ConfianzaMedia = g.Average(e => e.ConfianzaGlobal),
                    DuracionMediaMs = g.Average(e => (double)e.DuracionTotalMs)
                })
                .OrderByDescending(g => g.Total)
                .ToListAsync();

            return new EjecucionAgregadosResult
            {
                TotalEjecuciones  = total,
                PeriodoDias       = dias,
                Ok                = ok,
                Revision          = revision,
                Error             = error,
                FallbacksTotal    = fallbacks,
                ConfianzaGlobalMedia = confianzaMedia,
                DuracionMediaMs   = duracionMedia,
                PorTipologia      = byTipologia,
                PorModelo         = byModelo
            };
        }
    }
}
