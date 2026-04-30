using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class ObtenerUltimaEjecucionDuplicadoActivity
{
    private readonly ILogger<ObtenerUltimaEjecucionDuplicadoActivity> _logger;
    private readonly IDocumentoRepository _documentoRepository;
    private readonly IDocumentoEjecucionRepository _documentoEjecucionRepository;

    public ObtenerUltimaEjecucionDuplicadoActivity(
        ILogger<ObtenerUltimaEjecucionDuplicadoActivity> logger,
        IDocumentoRepository documentoRepository,
        IDocumentoEjecucionRepository documentoEjecucionRepository)
    {
        _logger = logger;
        _documentoRepository = documentoRepository;
        _documentoEjecucionRepository = documentoEjecucionRepository;
    }

    [Function("ObtenerUltimaEjecucionDuplicadoActivity")]
    public async Task<ContratoSalida?> Run([ActivityTrigger] string sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256))
        {
            _logger.LogWarning("SHA256 vacío en recuperación de duplicado");
            return null;
        }

        var documento = await _documentoRepository.GetBySHA256Async(sha256);
        if (documento is null)
        {
            _logger.LogWarning("No se encontró documento para SHA256 {Sha256}", sha256);
            return null;
        }

        var ejecuciones = await _documentoEjecucionRepository.GetByDocumentoIdAsync(documento.Id);
        var ultimaConSalida = ejecuciones.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.ContratoSalidaCompletoJson));

        if (ultimaConSalida is null)
        {
            _logger.LogWarning("No hay ejecuciones con salida serializada para documento ID={DocumentoId}", documento.Id);
            return null;
        }

        try
        {
            var salida = JsonSerializer.Deserialize<ContratoSalida>(
                ultimaConSalida.ContratoSalidaCompletoJson!,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (salida is null)
            {
                _logger.LogWarning("No se pudo deserializar la salida de la ejecución {EjecucionId}", ultimaConSalida.Id);
                return null;
            }

            RehidratarResultadoSiIncompleto(salida, ultimaConSalida);

            salida.Resultado.ReutilizadaPorDuplicado = true;
            salida.Resultado.MensajeReutilizacion = "Documento ya procesado previamente. Se reutiliza la última ejecución.";

            return salida;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON inválido al recuperar salida de ejecución {EjecucionId}", ultimaConSalida.Id);
            return null;
        }
    }

    private static void RehidratarResultadoSiIncompleto(ContratoSalida salida, DocumentoEjecucionEntity ejecucion)
    {
        if (!string.IsNullOrWhiteSpace(ejecucion.EstadoFinal))
        {
            salida.Resultado.Estado = ejecucion.EstadoFinal;
        }

        if (salida.Resultado.ConfianzaClasificacion <= 0 && ejecucion.ConfianzaClasificacion > 0)
        {
            salida.Resultado.ConfianzaClasificacion = ejecucion.ConfianzaClasificacion;
        }

        if (salida.Resultado.ConfianzaExtraccion <= 0 && salida.DetalleEjecucion.Extraccion.ConfianzaExtraccion > 0)
        {
            salida.Resultado.ConfianzaExtraccion = salida.DetalleEjecucion.Extraccion.ConfianzaExtraccion;
        }

        if (salida.Resultado.ConfianzaValidacion <= 0 && salida.DetalleEjecucion.Postproceso.ConfianzaValidacion > 0)
        {
            salida.Resultado.ConfianzaValidacion = salida.DetalleEjecucion.Postproceso.ConfianzaValidacion;
        }

        if (salida.Resultado.ConfianzaGlobal <= 0 && ejecucion.ConfianzaGlobal > 0)
        {
            salida.Resultado.ConfianzaGlobal = ejecucion.ConfianzaGlobal;
        }

        if (salida.Resultado.ConfianzaGlobal <= 0 &&
            salida.Resultado.ConfianzaClasificacion > 0 &&
            salida.Resultado.ConfianzaValidacion > 0)
        {
            var confianzaExtraccion = salida.Resultado.ConfianzaExtraccion > 0
                ? salida.Resultado.ConfianzaExtraccion
                : (double?)null;

            salida.Resultado.ConfianzaGlobal = Math.Round(
                ConfidenceCalculator.Global(
                    salida.Resultado.ConfianzaClasificacion,
                    confianzaExtraccion,
                    salida.Resultado.ConfianzaValidacion),
                3,
                MidpointRounding.AwayFromZero);
        }

        if (string.IsNullOrWhiteSpace(salida.Resultado.EstadoCalidad) && salida.Resultado.ConfianzaGlobal > 0)
        {
            salida.Resultado.EstadoCalidad = ConfidenceCalculator.EstadoCalidad(salida.Resultado.ConfianzaGlobal);
        }
    }
}
