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

        public async Task<EjecucionAgregadosResult> GetAgregadosAsync(EjecucionFiltro filtro)
        {
            var q = AplicarFiltro(_context.DocumentoEjecuciones.Include(e => e.Documento), filtro);

            var total = await q.CountAsync();

            int ok = 0, revision = 0, error = 0, fallbacks = 0;
            double confianzaMedia = 0, duracionMedia = 0;

            if (total > 0)
            {
                ok        = await q.CountAsync(e => EstadoEjecucion.Ok.Contains(e.EstadoFinal));
                revision  = await q.CountAsync(e => EstadoEjecucion.Revision.Contains(e.EstadoFinal));
                error     = await q.CountAsync(e => EstadoEjecucion.Error.Contains(e.EstadoFinal));
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
                    Ok             = g.Count(e => EstadoEjecucion.Ok.Contains(e.EstadoFinal)),
                    Revision       = g.Count(e => EstadoEjecucion.Revision.Contains(e.EstadoFinal)),
                    Error          = g.Count(e => EstadoEjecucion.Error.Contains(e.EstadoFinal)),
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
                    Ok             = g.Count(e => EstadoEjecucion.Ok.Contains(e.EstadoFinal)),
                    Revision       = g.Count(e => EstadoEjecucion.Revision.Contains(e.EstadoFinal)),
                    Error          = g.Count(e => EstadoEjecucion.Error.Contains(e.EstadoFinal)),
                    Fallbacks      = g.Count(e => e.UseFallbackLLM),
                    ConfianzaMedia = g.Average(e => e.ConfianzaGlobal),
                    DuracionMediaMs = g.Average(e => (double)e.DuracionTotalMs)
                })
                .OrderByDescending(g => g.Total)
                .ToListAsync();

            var porDia = await q
                .GroupBy(e => e.FechaEjecucion.Date)
                .Select(g => new SeriePunto
                {
                    Fecha     = g.Key,
                    Total     = g.Count(),
                    Ok        = g.Count(e => EstadoEjecucion.Ok.Contains(e.EstadoFinal)),
                    Revision  = g.Count(e => EstadoEjecucion.Revision.Contains(e.EstadoFinal)),
                    Error     = g.Count(e => EstadoEjecucion.Error.Contains(e.EstadoFinal)),
                    Fallbacks = g.Count(e => e.UseFallbackLLM)
                })
                .ToListAsync();

            // Los dias sin ejecuciones deben aparecer a cero: si se omitieran, el
            // grafico uniria dos dias no consecutivos con una linea recta y daria a
            // entender que hubo actividad intermedia.
            var porDiaIndexado = porDia.ToDictionary(p => p.Fecha.Date);
            var serie = new List<SeriePunto>();
            var ultimoDia = filtro.Hasta.AddTicks(-1).Date;
            for (var dia = filtro.Desde.Date; dia <= ultimoDia; dia = dia.AddDays(1))
            {
                serie.Add(porDiaIndexado.TryGetValue(dia, out var punto)
                    ? punto
                    : new SeriePunto { Fecha = dia });
            }

            // ── Calidad por confianza ─────────────────────────────────────────
            // Se calcula sobre ConfianzaGlobal en vez de leer un EstadoCalidad
            // almacenado porque ese valor solo existe dentro del contrato JSON de
            // cada ejecucion, no en columna, y desde ahi no se puede agrupar.
            // Consecuencia asumida: una tipologia con umbrales propios se cuenta
            // aqui con los globales. Ver CalidadEjecucion.
            var porCalidad = await q
                .GroupBy(e => e.ConfianzaGlobal >= CalidadEjecucion.UmbralOk
                    ? CalidadEjecucion.Ok
                    : e.ConfianzaGlobal >= CalidadEjecucion.UmbralRevision
                        ? CalidadEjecucion.Revision
                        : CalidadEjecucion.Error)
                .Select(g => new { Calidad = g.Key, Total = g.Count() })
                .ToListAsync();

            var porEstadoProceso = await q
                .GroupBy(e => e.EstadoFinal)
                .Select(g => new AgregadoGrupo
                {
                    Grupo           = g.Key,
                    Total           = g.Count(),
                    Ok              = g.Count(e => e.ConfianzaGlobal >= CalidadEjecucion.UmbralOk),
                    Revision        = g.Count(e => e.ConfianzaGlobal < CalidadEjecucion.UmbralOk
                                                && e.ConfianzaGlobal >= CalidadEjecucion.UmbralRevision),
                    Error           = g.Count(e => e.ConfianzaGlobal < CalidadEjecucion.UmbralRevision),
                    Fallbacks       = g.Count(e => e.UseFallbackLLM),
                    ConfianzaMedia  = g.Average(e => e.ConfianzaGlobal),
                    DuracionMediaMs = g.Average(e => (double)e.DuracionTotalMs)
                })
                .OrderByDescending(g => g.Total)
                .ToListAsync();

            var matriz = await q
                .GroupBy(e => new
                {
                    e.EstadoFinal,
                    Calidad = e.ConfianzaGlobal >= CalidadEjecucion.UmbralOk
                        ? CalidadEjecucion.Ok
                        : e.ConfianzaGlobal >= CalidadEjecucion.UmbralRevision
                            ? CalidadEjecucion.Revision
                            : CalidadEjecucion.Error
                })
                .Select(g => new MatrizCelda
                {
                    EstadoProceso = g.Key.EstadoFinal,
                    Calidad       = g.Key.Calidad,
                    Total         = g.Count()
                })
                .ToListAsync();

            var histograma = await ConstruirHistogramaAsync(q, total);

            return new EjecucionAgregadosResult
            {
                CalidadOk        = porCalidad.FirstOrDefault(c => c.Calidad == CalidadEjecucion.Ok)?.Total ?? 0,
                CalidadRevision  = porCalidad.FirstOrDefault(c => c.Calidad == CalidadEjecucion.Revision)?.Total ?? 0,
                CalidadError     = porCalidad.FirstOrDefault(c => c.Calidad == CalidadEjecucion.Error)?.Total ?? 0,
                PorEstadoProceso = porEstadoProceso,
                Matriz           = matriz,
                Histograma       = histograma,

                TotalEjecuciones  = total,
                PeriodoDias       = (int)Math.Ceiling((filtro.Hasta - filtro.Desde).TotalDays),
                Ok                = ok,
                Revision          = revision,
                Error             = error,
                FallbacksTotal    = fallbacks,
                ConfianzaGlobalMedia = confianzaMedia,
                DuracionMediaMs   = duracionMedia,
                PorTipologia      = byTipologia,
                PorModelo         = byModelo,
                Serie             = serie
            };
        }

        // Tramos del histograma de confianza. Mas finos cerca de los umbrales,
        // que es donde la decision de revisar o no se juega: agrupar todo en
        // decimos escondería si la masa se acumula justo por encima de 0,85.
        private static readonly double[] LimitesHistograma =
            { 0.0, 0.40, 0.50, 0.60, 0.65, 0.70, 0.75, 0.80, 0.85, 0.90, 0.95, 1.01 };

        private static async Task<List<HistogramaBin>> ConstruirHistogramaAsync(
            IQueryable<DocumentoEjecucionEntity> q, int total)
        {
            var bins = new List<HistogramaBin>();
            if (total == 0)
            {
                return bins;
            }

            // Una consulta por tramo en vez de un GROUP BY con un CASE de doce
            // ramas: el SQL resultante seria ilegible y EF no siempre lo traduce.
            for (var i = 0; i < LimitesHistograma.Length - 1; i++)
            {
                var desde = LimitesHistograma[i];
                var hasta = LimitesHistograma[i + 1];
                var n = await q.CountAsync(e => e.ConfianzaGlobal >= desde && e.ConfianzaGlobal < hasta);
                bins.Add(new HistogramaBin { Desde = desde, Hasta = hasta, Total = n });
            }

            return bins;
        }

        private static IQueryable<DocumentoEjecucionEntity> AplicarFiltro(
            IQueryable<DocumentoEjecucionEntity> q, EjecucionFiltro filtro)
        {
            q = q.Where(e => e.FechaEjecucion >= filtro.Desde && e.FechaEjecucion < filtro.Hasta);

            if (!string.IsNullOrWhiteSpace(filtro.Tipologia))
            {
                q = q.Where(e => e.Tipologia == filtro.Tipologia);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Estado))
            {
                var estado = filtro.Estado.Trim().ToUpperInvariant();
                if (estado == "OK")
                {
                    q = q.Where(e => EstadoEjecucion.Ok.Contains(e.EstadoFinal));
                }
                else if (estado == "REVISION")
                {
                    q = q.Where(e => EstadoEjecucion.Revision.Contains(e.EstadoFinal));
                }
                else if (estado == "ERROR")
                {
                    q = q.Where(e => EstadoEjecucion.Error.Contains(e.EstadoFinal));
                }
            }

            // Estado de proceso exacto: es lo que permite aislar VALIDACION_CON_ERRORES
            // y demas estados que las tres categorias historicas no contemplan.
            if (!string.IsNullOrWhiteSpace(filtro.EstadoProceso))
            {
                var estadoProceso = filtro.EstadoProceso.Trim();
                q = q.Where(e => e.EstadoFinal == estadoProceso);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Calidad))
            {
                switch (filtro.Calidad.Trim().ToUpperInvariant())
                {
                    case CalidadEjecucion.Ok:
                        q = q.Where(e => e.ConfianzaGlobal >= CalidadEjecucion.UmbralOk);
                        break;
                    case CalidadEjecucion.Revision:
                        q = q.Where(e => e.ConfianzaGlobal < CalidadEjecucion.UmbralOk
                                      && e.ConfianzaGlobal >= CalidadEjecucion.UmbralRevision);
                        break;
                    case CalidadEjecucion.Error:
                        q = q.Where(e => e.ConfianzaGlobal < CalidadEjecucion.UmbralRevision);
                        break;
                }
            }

            if (filtro.ConfianzaMin is { } min)
            {
                q = q.Where(e => e.ConfianzaGlobal >= min);
            }

            if (filtro.ConfianzaMax is { } max)
            {
                q = q.Where(e => e.ConfianzaGlobal < max);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Flujo))
            {
                var soloClasificacion = filtro.Flujo.Equals("Clasificacion", StringComparison.OrdinalIgnoreCase);
                q = q.Where(e => e.ClassificationOnly == soloClasificacion);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            {
                var busqueda = filtro.Busqueda.Trim();
                // Guid.TryParse acepta formatos sin guiones o con llaves; comparar la
                // forma normalizada evita que un GUID valido pero no canonico caiga
                // en cero resultados en vez de en la busqueda por nombre.
                if (Guid.TryParse(busqueda, out var guid))
                {
                    var guidNormalizado = guid.ToString();
                    q = q.Where(e => e.EjecucionGuid == guidNormalizado);
                }
                else
                {
                    q = q.Where(e => e.Documento != null && e.Documento.NombreArchivo.Contains(busqueda));
                }
            }

            if (!string.IsNullOrWhiteSpace(filtro.SubmittedBy))
            {
                var submittedBy = filtro.SubmittedBy.Trim();
                // SubmittedBy propio de la ejecucion tiene prioridad (el mismo documento
                // deduplicado puede reprocesarse desde otro origen); si la ejecucion no lo
                // informa, cae al SubmittedBy original del documento. El "??" sobre la
                // navegacion anulable se traduce a COALESCE por EF Core (LEFT JOIN con
                // columna a NULL cuando la ejecucion es huerfana).
                q = q.Where(e => (e.SubmittedBy ?? e.Documento.SubmittedBy) != null
                    && (e.SubmittedBy ?? e.Documento.SubmittedBy)!.Contains(submittedBy));
            }

            return q;
        }

        public async Task<(IReadOnlyList<EjecucionListadoItem> Items, int Total)> GetPagedAsync(
            EjecucionFiltro filtro, int page, int pageSize)
        {
            // Sin Include: la proyeccion referencia e.Documento y EF genera el join
            // solo con las columnas seleccionadas, en vez de arrastrar los LOB de
            // la ejecucion y el markdown comprimido del documento.
            var q = AplicarFiltro(_context.DocumentoEjecuciones.AsNoTracking(), filtro);

            var total = await q.CountAsync();

            var items = await q
                .OrderByDescending(e => e.FechaEjecucion)
                .ThenByDescending(e => e.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new EjecucionListadoItem
                {
                    Id = e.Id,
                    EjecucionGuid = e.EjecucionGuid,
                    FechaEjecucion = e.FechaEjecucion,
                    Tipologia = e.Tipologia,
                    ClassificationOnly = e.ClassificationOnly,
                    EstadoFinal = e.EstadoFinal,
                    ConfianzaGlobal = e.ConfianzaGlobal,
                    ConfianzaClasificacion = e.ConfianzaClasificacion,
                    UseFallbackLLM = e.UseFallbackLLM,
                    DuracionTotalMs = e.DuracionTotalMs,
                    DuracionClasificacionMs = e.DuracionClasificacionMs,
                    DuracionExtraccionMs = e.DuracionExtraccionMs,
                    DuracionGDCMs = e.DuracionGDCMs,
                    DuracionValidacionMs = e.DuracionValidacionMs,
                    DuracionIntegracionMs = e.DuracionIntegracionMs,
                    DuracionPersistenciaMs = e.DuracionPersistenciaMs,
                    NombreDocumento = e.Documento != null ? e.Documento.NombreArchivo : null,
                    SubmittedBy = e.SubmittedBy
                        ?? (e.Documento != null ? e.Documento.SubmittedBy : null),
                    ActivityTimelineJson = e.ActivityTimelineJson
                })
                .ToListAsync();

            return (items, total);
        }
    }
}
