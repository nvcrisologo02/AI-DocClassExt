using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocumentIA.Core.Extensions;
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
    private readonly ITipologiaRepository _tipologiaRepository;

    public ObtenerUltimaEjecucionDuplicadoActivity(
        ILogger<ObtenerUltimaEjecucionDuplicadoActivity> logger,
        IDocumentoRepository documentoRepository,
        IDocumentoEjecucionRepository documentoEjecucionRepository,
        ITipologiaRepository tipologiaRepository)
    {
        _logger = logger;
        _documentoRepository = documentoRepository;
        _documentoEjecucionRepository = documentoEjecucionRepository;
        _tipologiaRepository = tipologiaRepository;
    }

    [Function("ObtenerUltimaEjecucionDuplicadoActivity")]
    public async Task<ContratoSalida?> Run([ActivityTrigger] object input)
    {
        var request = ParseInput(input);
        var sha256 = request.SHA256;

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
        var candidatas = ejecuciones
            .Where(e => !string.IsNullOrWhiteSpace(e.ContratoSalidaCompletoJson))
            .ToList();

        var ultimaConSalida = candidatas.FirstOrDefault(e =>
            e.ClassificationOnly == request.ClassificationOnly &&
            string.Equals(e.NivelClasificacion, request.NivelClasificacion, StringComparison.OrdinalIgnoreCase));

        if (ultimaConSalida is null && candidatas.Count > 0)
        {
            // AB#100177: sin coincidencia exacta de ClassificationOnly/NivelClasificacion se
            // reutiliza la última ejecución con contrato. El filtro estricto dejaba peticiones
            // GDC sin resumen cuando la histórica había entrado por portal (INC1338832).
            ultimaConSalida = candidatas[0];
            _logger.LogWarning(
                "Sin ejecución exacta para ClassificationOnly={SolicitadoCo}/Nivel={SolicitadoNivel} en documento ID={DocumentoId}; se reutiliza la ejecución {EjecucionId} (ClassificationOnly={HistoricoCo}/Nivel={HistoricoNivel})",
                request.ClassificationOnly,
                request.NivelClasificacion,
                documento.Id,
                ultimaConSalida.Id,
                ultimaConSalida.ClassificationOnly,
                ultimaConSalida.NivelClasificacion);
        }

        if (ultimaConSalida is null)
        {
            _logger.LogWarning("No hay ninguna ejecución con contrato serializado para documento ID={DocumentoId}", documento.Id);
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
            await RehidratarTdnSiIncompletoAsync(salida, documento);

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

    private static ObtenerUltimaEjecucionDuplicadoInput ParseInput(object input)
    {
        if (input is ObtenerUltimaEjecucionDuplicadoInput typed)
        {
            return typed;
        }

        if (input is string sha)
        {
            return new ObtenerUltimaEjecucionDuplicadoInput
            {
                SHA256 = sha,
                ClassificationOnly = false,
                NivelClasificacion = null
            };
        }

        var json = JsonSerializer.Serialize(input);
        var parsed = JsonSerializer.Deserialize<ObtenerUltimaEjecucionDuplicadoInput>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return parsed ?? new ObtenerUltimaEjecucionDuplicadoInput();
    }

    /// <summary>
    /// Completa Identificacion.Tdn1/Tdn2 cuando la salida histórica no los trae, en cascada:
    /// JSON guardado → Documentos.Tdn1/Tdn2 → configuración de la tipología publicada.
    /// Las ejecuciones anteriores al poblado de TDN en el camino feliz no los serializaron.
    /// </summary>
    private async Task RehidratarTdnSiIncompletoAsync(ContratoSalida salida, DocumentoEntity documento)
    {
        if (string.IsNullOrWhiteSpace(salida.Identificacion.Tdn1)
            && !string.IsNullOrWhiteSpace(documento.Tdn1))
        {
            salida.Identificacion.Tdn1 = documento.Tdn1;
        }

        if (string.IsNullOrWhiteSpace(salida.Identificacion.Tdn2)
            && !string.IsNullOrWhiteSpace(documento.Tdn2))
        {
            salida.Identificacion.Tdn2 = documento.Tdn2;
        }

        if (!string.IsNullOrWhiteSpace(salida.Identificacion.Tdn1)
            && !string.IsNullOrWhiteSpace(salida.Identificacion.Tdn2))
        {
            return;
        }

        var codigoTipologia = salida.Identificacion.Tipologia;
        if (string.IsNullOrWhiteSpace(codigoTipologia)
            || string.Equals(codigoTipologia, "Desconocido", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var tipologia = await _tipologiaRepository.GetByCodigoAsync(codigoTipologia);
            if (tipologia is null)
            {
                _logger.LogWarning(
                    "No se encontró tipología {Codigo} para rehidratar TDN en reutilización por duplicado",
                    codigoTipologia);
                return;
            }

            if (string.IsNullOrWhiteSpace(salida.Identificacion.Tdn1))
            {
                var tdn1 = tipologia.GetTdn1();
                if (!string.IsNullOrWhiteSpace(tdn1))
                {
                    salida.Identificacion.Tdn1 = tdn1;
                }
            }

            if (string.IsNullOrWhiteSpace(salida.Identificacion.Tdn2))
            {
                var tdn2 = tipologia.GetTdn2();
                if (!string.IsNullOrWhiteSpace(tdn2))
                {
                    salida.Identificacion.Tdn2 = tdn2;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error rehidratando TDN desde tipología {Codigo} en reutilización por duplicado",
                codigoTipologia);
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
