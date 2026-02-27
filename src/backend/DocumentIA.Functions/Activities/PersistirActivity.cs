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

namespace DocumentIA.Functions.Activities
{
    public class PersistirActivity
    {
        private readonly ILogger<PersistirActivity> _logger;
        private readonly IDocumentoRepository _documentoRepo;
        private readonly IDocumentoEjecucionRepository _ejecucionRepo;
        private readonly IAuditoriaRepository _auditoriaRepo;
        private readonly DocumentIADbContext _context;

        public PersistirActivity(
            ILogger<PersistirActivity> logger,
            IDocumentoRepository documentoRepo,
            IDocumentoEjecucionRepository ejecucionRepo,
            IAuditoriaRepository auditoriaRepo,
            DocumentIADbContext context)
        {
            _logger = logger;
            _documentoRepo = documentoRepo;
            _ejecucionRepo = ejecucionRepo;
            _auditoriaRepo = auditoriaRepo;
            _context = context;
        }

        [Function(nameof(PersistirActivity))]
        public async Task Run([ActivityTrigger] ContratoSalida salida)
        {
            _logger.LogInformation("Persistiendo resultado para documento {Documento}", 
                salida.Identificacion.Documento);

            try
            {
                // 1. Obtener o crear documento base
                var documento = await _documentoRepo.GetBySHA256Async(salida.Integridad.SHA256);
                
                if (documento == null)
                {
                    documento = new DocumentoEntity
                    {
                        Guid = salida.Identificacion.Guid,
                        NombreArchivo = salida.Identificacion.Documento,
                        SHA256 = salida.Integridad.SHA256,
                        MD5 = salida.Integridad.MD5,
                        CRC32 = salida.Integridad.CRC32,
                        // Guardar la ruta del blob desde la primera persistencia para trazabilidad directa
                        RutaBlobStorage = salida.Integridad.RutaBlobStorage,
                        Tipologia = salida.Identificacion.Tipologia,
                        Estado = salida.Resultado.Estado,
                        ConfianzaGlobal = salida.Resultado.ConfianzaGlobal,
                        Paginas = salida.Identificacion.Paginas,
                        CorrelationId = salida.Identificacion.Guid,
                        FechaCreacion = DateTime.UtcNow,
                        FechaProceso = salida.Identificacion.FechaProceso
                    };
                    
                    documento = await _documentoRepo.AddAsync(documento);
                    _logger.LogInformation("Nuevo documento creado ID={Id}", documento.Id);
                }
                else
                {
                    documento.Estado = salida.Resultado.Estado;
                    documento.ConfianzaGlobal = salida.Resultado.ConfianzaGlobal;
                    documento.MD5 = salida.Integridad.MD5;
                    if (!string.IsNullOrWhiteSpace(salida.Integridad.RutaBlobStorage))
                    {
                        // Actualizar ruta solo cuando venga informada para no pisar con null/vacío
                        documento.RutaBlobStorage = salida.Integridad.RutaBlobStorage;
                    }
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
                var ejecucion = new DocumentoEjecucionEntity
                {
                    DocumentoId = documento.Id,
                    EjecucionGuid = Guid.NewGuid().ToString(),
                    FechaEjecucion = DateTime.UtcNow,
                    Tipologia = salida.Identificacion.Tipologia,
                    EstadoFinal = salida.Resultado.Estado,
                    ConfianzaGlobal = salida.Resultado.ConfianzaGlobal,
                    ModeloClasificacion = salida.DetalleEjecucion.Clasificacion.Modelo,
                    ConfianzaClasificacion = salida.DetalleEjecucion.Clasificacion.Confianza,
                    UseFallbackLLM = salida.DetalleEjecucion.Clasificacion.FallbackLLM,
                    
                    // NUEVO: Guardar respuesta completa para auditoria
                    ContratoSalidaCompletoJson = JsonSerializer.Serialize(salida, new JsonSerializerOptions 
                    { 
                        WriteIndented = false 
                    }),
                    
                    DatosOriginalesJson = salida.DetalleEjecucion.Integracion?.DatosOriginales != null 
                        ? JsonSerializer.Serialize(salida.DetalleEjecucion.Integracion.DatosOriginales) 
                        : null,
                    DatosFinalesJson = JsonSerializer.Serialize(salida.DatosExtraidos),
                    DuracionTotalMs = 0
                };

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
                        
                        ejecucion.DuracionTotalMs += plugin.DurationMs;
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
    }
}
