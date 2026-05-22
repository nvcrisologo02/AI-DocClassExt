using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using DocumentIA.Data.Repositories;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Context;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace DocumentIA.Functions.Activities
{
    public class PersistirActivity
    {
        private readonly ILogger<PersistirActivity> _logger;
        private readonly IDocumentoRepository _documentoRepo;
        private readonly IDocumentoEjecucionRepository _ejecucionRepo;
        private readonly IAuditoriaRepository _auditoriaRepo;
        private readonly DocumentIADbContext _context;
        private readonly TelemetryClient _telemetryClient;

        public PersistirActivity(
            ILogger<PersistirActivity> logger,
            IDocumentoRepository documentoRepo,
            IDocumentoEjecucionRepository ejecucionRepo,
            IAuditoriaRepository auditoriaRepo,
            DocumentIADbContext context,
            TelemetryClient telemetryClient)
        {
            _logger = logger;
            _documentoRepo = documentoRepo;
            _ejecucionRepo = ejecucionRepo;
            _auditoriaRepo = auditoriaRepo;
            _context = context;
            _telemetryClient = telemetryClient;
        }

        [Function(nameof(PersistirActivity))]
        public async Task Run([ActivityTrigger] ContratoSalida salida)
        {
            var nombreArchivoPersistible = ResolveNombreArchivoPersistible(salida.Identificacion.Documento, salida.Identificacion.Guid);

            _logger.LogInformation("Persistiendo resultado para documento {Documento}", 
                nombreArchivoPersistible);

            try
            {
                // 1. Obtener o crear documento base
                var documento = await _documentoRepo.GetBySHA256Async(salida.Integridad.SHA256);
                
                if (documento == null)
                {
                    documento = new DocumentoEntity
                    {
                        Guid = salida.Identificacion.Guid,
                        NombreArchivo = nombreArchivoPersistible,
                        SHA256 = salida.Integridad.SHA256,
                        MD5 = salida.Integridad.MD5,
                        CRC32 = salida.Integridad.CRC32,
                        TamanoBytes = salida.Integridad.TamanoBytes,
                        // Guardar la ruta del blob desde la primera persistencia para trazabilidad directa
                        RutaBlobStorage = salida.Integridad.RutaBlobStorage,
                        Tipologia = salida.Identificacion.Tipologia,
                        Estado = salida.Resultado.Estado,
                        ConfianzaGlobal = salida.Resultado.ConfianzaGlobal,
                        Paginas = salida.Identificacion.Paginas,
                        CorrelationId = salida.Identificacion.Guid,
                        NormalizacionMarkdownCompressed = CompressToBase64(salida.DetalleEjecucion.Postproceso?.Markdown),
                        // Registrar IdGDC e IdActivo si están disponibles
                        IdGDC = salida.Integridad.GestorDocumental,
                        IdActivo = salida.Integridad.IdActivo,
                        // === Campos TDN jerárquicos ===
                        Tdn1 = salida.Identificacion.Tdn1,
                        Tdn2 = salida.Identificacion.Tdn2,
                        Matricula = salida.Identificacion.Matricula,
                        EvidenceUri = salida.DetalleEjecucion.Clasificacion.EvidenceUri,
                        ClassifierVersion = salida.DetalleEjecucion.Clasificacion.ClassifierVersion,
                        PagesProcessed = salida.DetalleEjecucion.Clasificacion.PagesProcessed,
                        FechaCreacion = DateTime.UtcNow,
                        FechaProceso = salida.Identificacion.FechaProceso
                    };
                    
                    documento = await _documentoRepo.AddAsync(documento);
                    _logger.LogInformation("Nuevo documento creado ID={Id}", documento.Id);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(documento.NombreArchivo))
                    {
                        documento.NombreArchivo = nombreArchivoPersistible;
                    }

                    documento.Estado = salida.Resultado.Estado;
                    documento.ConfianzaGlobal = salida.Resultado.ConfianzaGlobal;
                    documento.MD5 = salida.Integridad.MD5;
                    documento.TamanoBytes = salida.Integridad.TamanoBytes;
                    if (!string.IsNullOrWhiteSpace(salida.Integridad.RutaBlobStorage))
                    {
                        // Actualizar ruta solo cuando venga informada para no pisar con null/vacío
                        documento.RutaBlobStorage = salida.Integridad.RutaBlobStorage;
                    }
                    // Actualizar IdGDC/IdActivo si vienen informados
                    if (!string.IsNullOrWhiteSpace(salida.Integridad.GestorDocumental))
                    {
                        documento.IdGDC = salida.Integridad.GestorDocumental;
                    }

                    if (!string.IsNullOrWhiteSpace(salida.Integridad.IdActivo))
                    {
                        documento.IdActivo = salida.Integridad.IdActivo;
                    }
                    
                    // === Actualizar campos TDN ===
                    if (!string.IsNullOrWhiteSpace(salida.Identificacion.Tdn1))
                        documento.Tdn1 = salida.Identificacion.Tdn1;
                    if (!string.IsNullOrWhiteSpace(salida.Identificacion.Tdn2))
                        documento.Tdn2 = salida.Identificacion.Tdn2;
                    if (!string.IsNullOrWhiteSpace(salida.Identificacion.Matricula))
                        documento.Matricula = salida.Identificacion.Matricula;
                    if (!string.IsNullOrWhiteSpace(salida.DetalleEjecucion.Clasificacion.EvidenceUri))
                        documento.EvidenceUri = salida.DetalleEjecucion.Clasificacion.EvidenceUri;
                    if (!string.IsNullOrWhiteSpace(salida.DetalleEjecucion.Clasificacion.ClassifierVersion))
                        documento.ClassifierVersion = salida.DetalleEjecucion.Clasificacion.ClassifierVersion;
                    if (salida.DetalleEjecucion.Clasificacion.PagesProcessed > 0)
                        documento.PagesProcessed = salida.DetalleEjecucion.Clasificacion.PagesProcessed;
                    
                    documento.NormalizacionMarkdownCompressed = CompressToBase64(salida.DetalleEjecucion.Postproceso?.Markdown);
                    documento.FechaActualizacion = DateTime.UtcNow;
                    await _documentoRepo.UpdateAsync(documento);
                    _logger.LogInformation("Documento actualizado ID={Id}", documento.Id);
                }

                // 2. Crear registro en ResultadosProcesamiento (tabla principal de resultados)
                var resultado = new ResultadoProcesamientoEntity
                {
                    DocumentoId = documento.Id,
                    
                    // Clasificación
                    ModeloClasificacion = salida.DetalleEjecucion.Clasificacion.Modelo,
                    ConfianzaClasificacion = salida.DetalleEjecucion.Clasificacion.Confianza,
                    FallbackLLM = salida.DetalleEjecucion.Clasificacion.FallbackLLM,
                    
                    // Extracción
                    ModeloExtraccion = salida.DetalleEjecucion.Extraccion.Modelo,
                    LayoutEnabled = salida.DetalleEjecucion.Extraccion.LayoutEnabled,
                    DatosExtraidosJson = JsonSerializer.Serialize(salida.DatosExtraidos),
                    
                    // Postproceso
                    NormalizacionesJson = salida.DetalleEjecucion.Postproceso?.Normalizaciones != null 
                        ? JsonSerializer.Serialize(salida.DetalleEjecucion.Postproceso.Normalizaciones) 
                        : null,
                    ValidacionesJson = salida.DetalleEjecucion.Postproceso?.Validaciones != null 
                        ? JsonSerializer.Serialize(salida.DetalleEjecucion.Postproceso.Validaciones) 
                        : null,
                    InconsistenciasJson = salida.DetalleEjecucion.Postproceso?.Inconsistencias != null 
                        ? JsonSerializer.Serialize(salida.DetalleEjecucion.Postproceso.Inconsistencias) 
                        : null,
                    
                    // Integración
                    ModuloIntegracion = string.Join(",", 
                        salida.DetalleEjecucion.Integracion?.Plugins?.Select(p => p.PluginKey) ?? Enumerable.Empty<string>()),
                    ResultadoIntegracion = salida.DetalleEjecucion.Integracion?.Estado ?? "DESCONOCIDO",
                    
                    // Tiempos
                    TiempoNormalizacionMs = GetTiempoMs(salida.DetalleEjecucion.Extraccion.TiemposMs, "Normalize"),
                    TiempoClasificacionMs = GetTiempoMs(salida.DetalleEjecucion.Extraccion.TiemposMs, "Classify"),
                    TiempoExtraccionMs = GetTiempoMs(salida.DetalleEjecucion.Extraccion.TiemposMs, "Extract"),
                    
                    FechaCreacion = DateTime.UtcNow
                };
                
                // Guardar ResultadosProcesamiento
                _context.ResultadosProcesamiento.Add(resultado);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Resultado procesamiento guardado ID={Id}", resultado.Id);

                // 3. Crear registro de ejecucion con historico completo
                var markdownPostproceso = salida.DetalleEjecucion.Postproceso?.Markdown;
                if (salida.DetalleEjecucion.Postproceso != null)
                {
                    salida.DetalleEjecucion.Postproceso.Markdown = null;
                }

                var ejecucion = new DocumentoEjecucionEntity
                {
                    DocumentoId = documento.Id,
                    EjecucionGuid = Guid.NewGuid().ToString(),
                    FechaEjecucion = DateTime.UtcNow,
                    InstanceId = salida.DetalleEjecucion.InstanceId,
                    OperationId = salida.DetalleEjecucion.OperationId,
                    Tipologia = salida.Identificacion.Tipologia,
                    EstadoFinal = salida.Resultado.Estado,
                    ConfianzaGlobal = salida.Resultado.ConfianzaGlobal,
                    ModeloClasificacion = salida.DetalleEjecucion.Clasificacion.Modelo,
                    ConfianzaClasificacion = salida.DetalleEjecucion.Clasificacion.Confianza,
                    UseFallbackLLM = salida.DetalleEjecucion.Clasificacion.FallbackLLM,
                    ClassificationOnly = salida.DetalleEjecucion.ClassificationOnly,
                    
                    // NUEVO: Guardar respuesta completa para auditoria
                    ContratoSalidaCompletoJson = JsonSerializer.Serialize(salida, new JsonSerializerOptions 
                    { 
                        WriteIndented = false 
                    }),

                    ActivityTimelineJson = salida.DetalleEjecucion.Seguimiento?.Actividades != null
                        ? JsonSerializer.Serialize(salida.DetalleEjecucion.Seguimiento.Actividades)
                        : null,
                    
                    DatosOriginalesJson = salida.DetalleEjecucion.Integracion?.DatosOriginales != null 
                        ? JsonSerializer.Serialize(salida.DetalleEjecucion.Integracion.DatosOriginales) 
                        : null,
                    DatosFinalesJson = JsonSerializer.Serialize(salida.DatosExtraidos),
                    DuracionTotalMs = salida.DetalleEjecucion.Seguimiento?.DuracionTotalMs ?? 0,
                    DuracionClasificacionMs = GetDuracionActividad(salida, "Clasificar"),
                    DuracionExtraccionMs = GetDuracionActividad(salida, "Extraer"),
                    DuracionValidacionMs = GetDuracionActividad(salida, "Validar"),
                    DuracionIntegracionMs = GetDuracionActividad(salida, "Integrar"),
                    DuracionGDCMs = GetDuracionActividad(salida, "SubirGDC"),
                    DuracionPersistenciaMs = GetDuracionActividad(salida, "Persistir"),
                    DuracionAssetResolverMs = GetDuracionActividad(salida, "ObtenerActivo"),
                    AssetResolverResultJson = salida.DetalleEjecucion.AssetResolver is { Ejecutado: true }
                        ? JsonSerializer.Serialize(
                            salida.DetalleEjecucion.AssetResolver.Activos?.Select(a => new { a.IdActivo, a.FchCierre }),
                            new JsonSerializerOptions { WriteIndented = false })
                        : null
                };

                if (salida.DetalleEjecucion.Postproceso != null)
                {
                    salida.DetalleEjecucion.Postproceso.Markdown = markdownPostproceso;
                }

                // 3. Guardar detalle de cada plugin ejecutado
                if (salida.DetalleEjecucion.Integracion?.Plugins != null)
                {
                    foreach (var plugin in salida.DetalleEjecucion.Integracion.Plugins)
                    {
                        ejecucion.PluginsEjecutados.Add(new PluginEjecucionEntity
                        {
                            PluginKey = plugin.PluginKey,
                            Priority = plugin.Priority,
                            Success = plugin.Success,
                            Mensaje = plugin.Mensaje,
                            StatusCode = plugin.StatusCode,
                            DurationMs = plugin.DurationMs,
                            Error = plugin.Error,
                            DatosEnriquecidosJson = plugin.DatosEnriquecidos != null 
                                ? JsonSerializer.Serialize(plugin.DatosEnriquecidos) 
                                : null,
                            FechaEjecucion = DateTime.UtcNow
                        });
                        
                        if (ejecucion.DuracionTotalMs <= 0)
                        {
                            ejecucion.DuracionTotalMs += plugin.DurationMs;
                        }
                    }
                }

                // 4. Guardar validaciones estructuradas
                if (salida.DetalleEjecucion.Postproceso?.Validaciones != null)
                {
                    foreach (var validacion in salida.DetalleEjecucion.Postproceso.Validaciones)
                    {
                        // Parsear formato "[Warning] Campo: Mensaje"
                        var match = Regex.Match(validacion, @"\[(\w+)\]\s+(\w+):\s+(.+)");
                        if (match.Success)
                        {
                            ejecucion.Validaciones.Add(new ValidacionResultadoEntity
                            {
                                Severidad = match.Groups[1].Value,
                                Campo = match.Groups[2].Value,
                                Mensaje = match.Groups[3].Value,
                                Pasado = false,
                                FechaValidacion = DateTime.UtcNow
                            });
                        }
                    }
                }

                // Guardar ejecucion con todas sus relaciones
                await _ejecucionRepo.AddAsync(ejecucion);

                // EP6-Fase C: emitir evento y métricas a Application Insights
                EmitirTelemetria(ejecucion, salida);

                _logger.LogInformation(
                    "Ejecucion guardada ID={Id}, Plugins={Plugins}, Validaciones={Validaciones}",
                    ejecucion.Id,
                    ejecucion.PluginsEjecutados.Count,
                    ejecucion.Validaciones.Count);

                // 5. Registrar auditoria
                await _auditoriaRepo.AddAsync(new AuditoriaEntity
                {
                    DocumentoId = documento.Id,
                    Accion = "Procesamiento Completo",
                    Nivel = salida.Resultado.Estado == "OK" ? "Info" : "Warning",
                    Mensaje = $"Documento procesado con estado {salida.Resultado.Estado}",
                    DetallesJson = JsonSerializer.Serialize(new 
                    { 
                        EjecucionGuid = ejecucion.EjecucionGuid,
                        Confianza = salida.Resultado.ConfianzaGlobal 
                    }),
                    FechaHora = DateTime.UtcNow
                });

                _logger.LogInformation("Persistencia completada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la persistencia");
                throw;
            }
        }

        private int? GetTiempoMs(Dictionary<string, int> tiempos, string clave)
        {
            if (tiempos == null || !tiempos.ContainsKey(clave))
                return null;
            
            return tiempos[clave];
        }

        private static string ResolveNombreArchivoPersistible(string? nombreArchivo, string? guid)
        {
            if (!string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return nombreArchivo;
            }

            var suffix = !string.IsNullOrWhiteSpace(guid)
                ? guid
                : Guid.NewGuid().ToString();

            return $"documento-{suffix}.pdf";
        }

        private int? GetDuracionActividad(ContratoSalida salida, string nombreActividad)
        {
            var actividad = salida.DetalleEjecucion.Seguimiento?.Actividades?
                .FirstOrDefault(a => string.Equals(a.Nombre, nombreActividad, StringComparison.Ordinal));

            if (actividad == null)
            {
                return null;
            }

            if (!actividad.FinUtc.HasValue || string.Equals(actividad.Estado, "Running", StringComparison.Ordinal))
            {
                return null;
            }

            return actividad.DuracionMs;
        }

        private void EmitirTelemetria(DocumentoEjecucionEntity ejecucion, ContratoSalida salida)
        {
            try
            {
                // T1 – TrackEvent "DocumentProcessed" con dimensiones clave
                var properties = new Dictionary<string, string>
                {
                    ["Tipologia"]      = ejecucion.Tipologia ?? string.Empty,
                    ["EstadoFinal"]    = ejecucion.EstadoFinal ?? string.Empty,
                    ["UseFallbackLLM"] = ejecucion.UseFallbackLLM.ToString(),
                    ["NombreDocumento"] = salida.Identificacion.Documento ?? string.Empty,
                    ["EjecucionGuid"]  = ejecucion.EjecucionGuid
                };
                _telemetryClient.TrackEvent("DocumentProcessed", properties);

                // T2 – TrackMetric duraciones por actividad
                TrackDuracion("Total",        ejecucion.DuracionTotalMs,        ejecucion.Tipologia);
                TrackDuracion("Clasificacion", ejecucion.DuracionClasificacionMs, ejecucion.Tipologia);
                TrackDuracion("Extraccion",   ejecucion.DuracionExtraccionMs,   ejecucion.Tipologia);
                TrackDuracion("Validacion",   ejecucion.DuracionValidacionMs,   ejecucion.Tipologia);
                TrackDuracion("GDC",          ejecucion.DuracionGDCMs,          ejecucion.Tipologia);
                TrackDuracion("Integracion",  ejecucion.DuracionIntegracionMs,  ejecucion.Tipologia);
                TrackDuracion("Persistencia", ejecucion.DuracionPersistenciaMs, ejecucion.Tipologia);
            }
            catch (Exception ex)
            {
                // La telemetría nunca debe bloquear el flujo principal
                _logger.LogWarning(ex, "Error al emitir telemetría a Application Insights");
            }
        }

        private void TrackDuracion(string actividad, int? duracionMs, string? tipologia)
        {
            if (!duracionMs.HasValue) return;
            var metric = new MetricTelemetry($"DocumentIA.Duracion.{actividad}", duracionMs.Value);
            metric.Properties["Tipologia"] = tipologia ?? string.Empty;
            _telemetryClient.TrackMetric(metric);
        }

        private static string? CompressToBase64(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var rawBytes = Encoding.UTF8.GetBytes(value);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(rawBytes, 0, rawBytes.Length);
            }

            output.Position = 0;
            return Convert.ToBase64String(output.ToArray());
        }
    }
}
