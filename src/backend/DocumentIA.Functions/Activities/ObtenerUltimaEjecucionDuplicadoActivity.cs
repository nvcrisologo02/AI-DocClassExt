using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Extensions;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Activities;

public class ObtenerUltimaEjecucionDuplicadoActivity
{
    private readonly ILogger<ObtenerUltimaEjecucionDuplicadoActivity> _logger;
    private readonly IDocumentoRepository _documentoRepository;
    private readonly IDocumentoEjecucionRepository _documentoEjecucionRepository;
    private readonly ITipologiaRepository _tipologiaRepository;
    private readonly DedupOptions _opciones;

    public ObtenerUltimaEjecucionDuplicadoActivity(
        ILogger<ObtenerUltimaEjecucionDuplicadoActivity> logger,
        IDocumentoRepository documentoRepository,
        IDocumentoEjecucionRepository documentoEjecucionRepository,
        ITipologiaRepository tipologiaRepository,
        IOptions<DedupOptions> opciones)
    {
        _logger = logger;
        _documentoRepository = documentoRepository;
        _documentoEjecucionRepository = documentoEjecucionRepository;
        _tipologiaRepository = tipologiaRepository;
        _opciones = opciones.Value;
    }

    [Function("ObtenerUltimaEjecucionDuplicadoActivity")]
    public async Task<ObtenerUltimaEjecucionDuplicadoResultado> Run([ActivityTrigger] object input)
    {
        var request = ParseInput(input);
        var estricto = _opciones.ModoEstricto;

        if (string.IsNullOrWhiteSpace(request.SHA256))
        {
            _logger.LogWarning("SHA256 vacio en recuperacion de duplicado");
            return ObtenerUltimaEjecucionDuplicadoResultado.NoReutilizada(MotivosReutilizacion.SinHistorica, estricto);
        }

        var documento = await _documentoRepository.GetBySHA256Async(request.SHA256);
        if (documento is null)
        {
            _logger.LogWarning("No se encontro documento para SHA256 {Sha256}", request.SHA256);
            return ObtenerUltimaEjecucionDuplicadoResultado.NoReutilizada(MotivosReutilizacion.SinHistorica, estricto);
        }

        if (!estricto)
        {
            var legada = await SeleccionarLegadoAsync(request, documento);
            return legada is null
                ? ObtenerUltimaEjecucionDuplicadoResultado.NoReutilizada(null, false)
                : ObtenerUltimaEjecucionDuplicadoResultado.Reutilizada(legada, false);
        }

        if (string.IsNullOrWhiteSpace(request.ClaveIntencion))
        {
            _logger.LogWarning("Peticion sin clave de intencion en modo estricto para documento {DocumentoId}: no se reutiliza", documento.Id);
            return ObtenerUltimaEjecucionDuplicadoResultado.NoReutilizada(MotivosReutilizacion.ClaveDistinta, true);
        }

        var motivo = await SeleccionarPorClaveAsync(request, documento);
        return motivo.Salida is null
            ? ObtenerUltimaEjecucionDuplicadoResultado.NoReutilizada(motivo.Motivo, true)
            : ObtenerUltimaEjecucionDuplicadoResultado.Reutilizada(motivo.Salida, true);
    }

    /// <summary>
    /// Seleccion por clave (spec, seccion 2): la mas reciente con la misma clave; si no supera la
    /// segunda fase, las siguientes hasta MaxCandidatas. Sin filas con clave se derivan las
    /// antiguas una vez y se graban.
    /// </summary>
    private async Task<(ContratoSalida? Salida, string Motivo)> SeleccionarPorClaveAsync(
        ObtenerUltimaEjecucionDuplicadoInput request, DocumentoEntity documento)
    {
        var clave = request.ClaveIntencion!;
        var necesidad = request.Necesidad();
        var motivo = MotivosReutilizacion.SinHistorica;

        var candidatas = await _documentoEjecucionRepository.GetCandidatasPorClaveAsync(documento.Id, clave, 0, 1);
        var derivadas = false;
        if (candidatas.Count == 0)
        {
            candidatas = await DerivarAntiguasAsync(documento.Id, clave);
            derivadas = true;
        }

        if (candidatas.Count == 0)
        {
            var hayOtras = await _documentoEjecucionRepository.TieneEjecucionesConContratoAsync(documento.Id);
            return (null, hayOtras ? MotivosReutilizacion.ClaveDistinta : MotivosReutilizacion.SinHistorica);
        }

        var examinadas = 0;
        var cola = new Queue<DocumentoEjecucionEntity>(candidatas);
        while (examinadas < _opciones.MaxCandidatas)
        {
            if (cola.Count == 0)
            {
                if (derivadas)
                {
                    break;
                }

                var siguientes = await _documentoEjecucionRepository.GetCandidatasPorClaveAsync(
                    documento.Id, clave, examinadas, _opciones.MaxCandidatas - examinadas);
                if (siguientes.Count == 0)
                {
                    break;
                }

                foreach (var s in siguientes)
                {
                    cola.Enqueue(s);
                }
            }

            var candidata = cola.Dequeue();
            examinadas++;

            var (salida, motivoCandidata) = await EvaluarCandidataAsync(candidata, request, necesidad, documento);
            if (salida is not null)
            {
                return (salida, MotivosReutilizacion.ClaveIgual);
            }

            motivo = motivoCandidata;
            _logger.LogInformation(
                "Candidata {EjecucionId} descartada para documento {DocumentoId}: {Motivo}",
                candidata.Id, documento.Id, motivo);
        }

        return (null, motivo);
    }

    /// <summary>Segunda fase sobre la candidata: contrato legible, modelo de extraccion (modo completo) y paginas.</summary>
    private async Task<(ContratoSalida? Salida, string Motivo)> EvaluarCandidataAsync(
        DocumentoEjecucionEntity candidata,
        ObtenerUltimaEjecucionDuplicadoInput request,
        NecesidadMarkdown necesidad,
        DocumentoEntity documento)
    {
        if (string.IsNullOrWhiteSpace(candidata.ContratoSalidaCompletoJson))
        {
            return (null, MotivosReutilizacion.ContratoInvalido);
        }

        ContratoSalida? salida;
        try
        {
            salida = JsonSerializer.Deserialize<ContratoSalida>(
                candidata.ContratoSalidaCompletoJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON invalido al recuperar salida de ejecucion {EjecucionId}", candidata.Id);
            return (null, MotivosReutilizacion.ContratoInvalido);
        }

        if (salida is null || string.Equals(salida.Resultado.Estado, "DUPLICADO", StringComparison.OrdinalIgnoreCase))
        {
            return (null, MotivosReutilizacion.ContratoInvalido);
        }

        if (!request.ClassificationOnly && !await ModeloExtraccionCoincideAsync(salida, request.ExtraccionModelKeyPedido))
        {
            return (null, MotivosReutilizacion.ModeloExtraccionDistinto);
        }

        if (!CoberturaCandidata.Cubre(salida.DetalleEjecucion, necesidad))
        {
            return (null, MotivosReutilizacion.PaginasInsuficientes);
        }

        ContratoTimelineRehidratador.Rehidratar(salida, candidata.ActivityTimelineJson);
        salida.DetalleEjecucion.EjecucionOriginalGuid = candidata.EjecucionGuid;
        salida.DetalleEjecucion.MotivoReutilizacion = MotivosReutilizacion.ClaveIgual;
        salida.DetalleEjecucion.MotivoNoReutilizacion = null;

        RehidratarResultadoSiIncompleto(salida, candidata);
        await RehidratarTdnSiIncompletoAsync(salida, documento);

        salida.Resultado.ReutilizadaPorDuplicado = true;
        salida.Resultado.MensajeReutilizacion = "Documento ya procesado previamente. Se reutiliza la ultima ejecucion con la misma intencion.";

        return (salida, MotivosReutilizacion.ClaveIgual);
    }

    /// <summary>
    /// El modelo de extraccion no esta en la clave porque depende de la tipologia resuelta. Se
    /// compara el ModelKeyEfectivo persistido con el que la tipologia publicada usaria hoy (o el
    /// que pide la peticion). La tipologia puede usar hoy el primario o el secundario (round robin
    /// del proveedor); el modelo pedido, si existe, manda. Tipologia no resuelta, no publicada o modelo distinto: no sirve.
    /// Ambos vacios (tipologia sin extraccion) coinciden.
    /// </summary>
    private async Task<bool> ModeloExtraccionCoincideAsync(ContratoSalida salida, string? modelKeyPedido)
    {
        var codigo = salida.Identificacion.Tipologia;
        if (string.IsNullOrWhiteSpace(codigo) || string.Equals(codigo, "Desconocido", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var tipologia = await _tipologiaRepository.GetByCodigoAsync(codigo);
        if (tipologia is null || tipologia.Estado != EstadoTipologia.Published || !tipologia.Activa)
        {
            return false;
        }

        var historico = (salida.DetalleEjecucion.Extraccion.ModelKeyEfectivo ?? string.Empty).Trim();

        bool Coincide(string? candidato) =>
            string.Equals((candidato ?? string.Empty).Trim(), historico, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(modelKeyPedido))
        {
            return Coincide(modelKeyPedido);
        }

        var extraccion = tipologia.GetExtractionConfig();
        return Coincide(extraccion.ModelKey)
            || (!string.IsNullOrWhiteSpace(extraccion.SecondaryModelKey) && Coincide(extraccion.SecondaryModelKey));
    }

    /// <summary>
    /// Filas anteriores al cambio: se deriva la clave de cada una desde el contrato (misma
    /// funcion que el backfill), se graba, y se devuelven las que coinciden con la pedida.
    /// </summary>
    private async Task<IReadOnlyList<DocumentoEjecucionEntity>> DerivarAntiguasAsync(int documentoId, string clave)
    {
        var sinClave = await _documentoEjecucionRepository.GetConContratoSinClaveAsync(documentoId, _opciones.MaxDerivacionAlVuelo);
        var coincidentes = new List<DocumentoEjecucionEntity>();

        foreach (var fila in sinClave)
        {
            var derivada = ClaveIntencionDerivador.DesdeContrato(fila.ContratoSalidaCompletoJson, ClassificationLevelResolver.DefaultLevel);
            if (derivada is null)
            {
                continue;
            }

            try
            {
                await _documentoEjecucionRepository.ActualizarClaveIntencionAsync(fila.Id, derivada.Clave, derivada.ComponentesJson);
            }
            catch (Exception ex)
            {
                // Grabar la clave es una optimizacion; si falla, la proxima peticion la deriva otra vez.
                _logger.LogWarning(ex, "No se pudo grabar la clave derivada en la ejecucion {EjecucionId}", fila.Id);
            }

            if (string.Equals(derivada.Clave, clave, StringComparison.Ordinal))
            {
                fila.ClaveIntencion = derivada.Clave;
                coincidentes.Add(fila);
            }
        }

        if (sinClave.Count > 0)
        {
            _logger.LogInformation(
                "Derivadas {Total} claves antiguas para documento {DocumentoId}; {Coincidentes} coinciden con la peticion",
                sinClave.Count, documentoId, coincidentes.Count);
        }

        return coincidentes;
    }

    /// <summary>
    /// Regla anterior (Dedup:ModoEstricto = false): ClassificationOnly y NivelClasificacion con
    /// fallback a la ultima con contrato (AB#100177). Se retira con el interruptor.
    /// </summary>
    private async Task<ContratoSalida?> SeleccionarLegadoAsync(ObtenerUltimaEjecucionDuplicadoInput request, DocumentoEntity documento)
    {
        var ejecuciones = await _documentoEjecucionRepository.GetByDocumentoIdAsync(documento.Id);
        var candidatas = ejecuciones
            .Where(e => !string.IsNullOrWhiteSpace(e.ContratoSalidaCompletoJson))
            .ToList();

        var ultimaConSalida = candidatas.FirstOrDefault(e =>
            e.ClassificationOnly == request.ClassificationOnly &&
            string.Equals(e.NivelClasificacion, request.NivelClasificacion, StringComparison.OrdinalIgnoreCase));

        if (ultimaConSalida is null && candidatas.Count > 0)
        {
            // AB#100177: sin coincidencia exacta se reutiliza la ultima con contrato (INC1338832).
            ultimaConSalida = candidatas[0];
            _logger.LogWarning(
                "Modo legado: sin ejecucion exacta para ClassificationOnly={SolicitadoCo}/Nivel={SolicitadoNivel} en documento ID={DocumentoId}; se reutiliza la ejecucion {EjecucionId}",
                request.ClassificationOnly, request.NivelClasificacion, documento.Id, ultimaConSalida.Id);
        }

        if (ultimaConSalida is null)
        {
            _logger.LogWarning("No hay ninguna ejecucion con contrato serializado para documento ID={DocumentoId}", documento.Id);
            return null;
        }

        try
        {
            var salida = JsonSerializer.Deserialize<ContratoSalida>(
                ultimaConSalida.ContratoSalidaCompletoJson!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (salida is null)
            {
                return null;
            }

            ContratoTimelineRehidratador.Rehidratar(salida, ultimaConSalida.ActivityTimelineJson);
            salida.DetalleEjecucion.EjecucionOriginalGuid = ultimaConSalida.EjecucionGuid;
            RehidratarResultadoSiIncompleto(salida, ultimaConSalida);
            await RehidratarTdnSiIncompletoAsync(salida, documento);
            salida.Resultado.ReutilizadaPorDuplicado = true;
            salida.Resultado.MensajeReutilizacion = "Documento ya procesado previamente. Se reutiliza la ultima ejecucion.";
            return salida;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON invalido al recuperar salida de ejecucion {EjecucionId}", ultimaConSalida.Id);
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
