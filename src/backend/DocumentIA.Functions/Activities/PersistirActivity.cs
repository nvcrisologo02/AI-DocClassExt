using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using System.Text.Json;

namespace DocumentIA.Functions.Activities;

public class PersistirActivity
{
    private readonly ILogger<PersistirActivity> _logger;
    private readonly IDocumentoRepository _documentoRepository;
    private readonly IAuditoriaRepository _auditoriaRepository;

    public PersistirActivity(
        ILogger<PersistirActivity> logger,
        IDocumentoRepository documentoRepository,
        IAuditoriaRepository auditoriaRepository)
    {
        _logger = logger;
        _documentoRepository = documentoRepository;
        _auditoriaRepository = auditoriaRepository;
    }

    [Function("PersistirActivity")]
    public async Task Run([ActivityTrigger] ContratoSalida salida)
    {
        _logger.LogInformation($"Persistiendo resultado para documento: {salida.Identificacion.Documento}");

        try
        {
            // Buscar si ya existe el documento por SHA256
            var documentoExistente = await _documentoRepository.GetBySHA256Async(salida.Integridad.SHA256);

            if (documentoExistente != null)
            {
                // Actualizar documento existente
                documentoExistente.Estado = salida.Resultado.Estado;
                documentoExistente.ConfianzaGlobal = salida.Resultado.ConfianzaGlobal;
                documentoExistente.Tipologia = salida.Identificacion.Tipologia;
                documentoExistente.FechaProceso = salida.Identificacion.FechaProceso;
                documentoExistente.Paginas = salida.Identificacion.Paginas;

                // Actualizar o crear resultado
                if (documentoExistente.Resultado == null)
                {
                    documentoExistente.Resultado = new ResultadoProcesamientoEntity();
                }

                MapearResultado(documentoExistente.Resultado, salida, documentoExistente.Id);

                await _documentoRepository.UpdateAsync(documentoExistente);

                _logger.LogInformation($"Documento actualizado: ID {documentoExistente.Id}");

                // Registrar auditoría para actualización/reprocesamiento
                await _auditoriaRepository.AddAsync(new AuditoriaEntity
                {
                    DocumentoId = documentoExistente.Id,
                    Accion = "Reprocesamiento",
                    Nivel = salida.Resultado.Estado == "OK" ? "Info" : "Warning",
                    Mensaje = $"Documento reprocesado con estado: {salida.Resultado.Estado}",
                    DetallesJson = JsonSerializer.Serialize(salida.Resultado),
                    FechaHora = DateTime.UtcNow
                });
            }
            else
            {
                // Crear nuevo documento
                var nuevoDocumento = new DocumentoEntity
                {
                    Guid = salida.Identificacion.Guid,
                    NombreArchivo = salida.Identificacion.Documento,
                    SHA256 = salida.Integridad.SHA256,
                    CRC32 = salida.Integridad.CRC32,
                    Tipologia = salida.Identificacion.Tipologia,
                    Estado = salida.Resultado.Estado,
                    ConfianzaGlobal = salida.Resultado.ConfianzaGlobal,
                    Paginas = salida.Identificacion.Paginas,
                    CorrelationId = salida.Identificacion.Guid, // Temporal, actualizar cuando tengamos trazabilidad
                    FechaCreacion = DateTime.UtcNow,
                    FechaProceso = salida.Identificacion.FechaProceso
                };

                // Crear resultado
                nuevoDocumento.Resultado = new ResultadoProcesamientoEntity();
                MapearResultado(nuevoDocumento.Resultado, salida, 0);

                var documentoGuardado = await _documentoRepository.AddAsync(nuevoDocumento);

                _logger.LogInformation($"Nuevo documento guardado: ID {documentoGuardado.Id}");

                // Registrar auditoría
                await _auditoriaRepository.AddAsync(new AuditoriaEntity
                {
                    DocumentoId = documentoGuardado.Id,
                    Accion = "Procesamiento Completo",
                    Nivel = salida.Resultado.Estado == "OK" ? "Info" : "Warning",
                    Mensaje = $"Documento procesado con estado: {salida.Resultado.Estado}",
                    DetallesJson = JsonSerializer.Serialize(salida.Resultado),
                    FechaHora = DateTime.UtcNow
                });
            }

            _logger.LogInformation("Persistencia completada exitosamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la persistencia");
            throw;
        }
    }

    private void MapearResultado(ResultadoProcesamientoEntity resultado, ContratoSalida salida, int documentoId)
    {
        if (documentoId > 0)
            resultado.DocumentoId = documentoId;

        resultado.ModeloClasificacion = salida.DetalleEjecucion.Clasificacion.Modelo;
        resultado.ConfianzaClasificacion = salida.DetalleEjecucion.Clasificacion.Confianza;
        resultado.FallbackLLM = salida.DetalleEjecucion.Clasificacion.FallbackLLM;

        resultado.ModeloExtraccion = salida.DetalleEjecucion.Extraccion.Modelo;
        resultado.LayoutEnabled = salida.DetalleEjecucion.Extraccion.LayoutEnabled;

        resultado.DatosExtraidosJson = JsonSerializer.Serialize(salida.DatosExtraidos);
        resultado.NormalizacionesJson = JsonSerializer.Serialize(salida.DetalleEjecucion.Postproceso.Normalizaciones);
        resultado.ValidacionesJson = JsonSerializer.Serialize(salida.DetalleEjecucion.Postproceso.Validaciones);
        resultado.InconsistenciasJson = JsonSerializer.Serialize(salida.DetalleEjecucion.Postproceso.Inconsistencias);

        resultado.ModuloIntegracion = salida.DetalleEjecucion.Integracion.Modulo;
        resultado.ResultadoIntegracion = salida.DetalleEjecucion.Integracion.Result;

        // Tiempos (si estuvieran disponibles en el DetalleEjecucion)
        if (salida.DetalleEjecucion.Extraccion.TiemposMs != null && salida.DetalleEjecucion.Extraccion.TiemposMs.Count > 0)
        {
            resultado.TiempoClasificacionMs = salida.DetalleEjecucion.Extraccion.TiemposMs.ContainsKey("Classify") 
                ? salida.DetalleEjecucion.Extraccion.TiemposMs["Classify"] : null;
            resultado.TiempoExtraccionMs = salida.DetalleEjecucion.Extraccion.TiemposMs.ContainsKey("Extract") 
                ? salida.DetalleEjecucion.Extraccion.TiemposMs["Extract"] : null;
        }

        resultado.FechaCreacion = DateTime.UtcNow;
    }
}
