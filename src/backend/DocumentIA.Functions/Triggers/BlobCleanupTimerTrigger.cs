using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Triggers;

public class BlobCleanupTimerTrigger
{
    private readonly ILogger<BlobCleanupTimerTrigger> _logger;
    private readonly IDocumentoRepository _documentoRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IAuditoriaRepository _auditoriaRepository;
    private readonly TelemetryClient _telemetryClient;
    private readonly IConfiguration _configuration;

    public BlobCleanupTimerTrigger(
        ILogger<BlobCleanupTimerTrigger> logger,
        IDocumentoRepository documentoRepository,
        IBlobStorageService blobStorageService,
        IAuditoriaRepository auditoriaRepository,
        TelemetryClient telemetryClient,
        IConfiguration configuration)
    {
        _logger = logger;
        _documentoRepository = documentoRepository;
        _blobStorageService = blobStorageService;
        _auditoriaRepository = auditoriaRepository;
        _telemetryClient = telemetryClient;
        _configuration = configuration;
    }

    [Function(nameof(BlobCleanupTimerTrigger))]
    public async Task Run([TimerTrigger("%BlobRetentionCleanupCron%") ] TimerInfo? timerInfo)
    {
        var batchSize = _configuration.GetValue<int?>("BlobRetention:BatchSize") ?? 200;
        if (batchSize <= 0)
        {
            batchSize = 200;
        }

        var candidatos = (await _documentoRepository.GetDocumentosConBlobExpiradosAsync(batchSize)).ToList();

        var procesados = 0;
        var eliminados = 0;
        var noEncontrados = 0;
        var errores = 0;
        long bytesLiberados = 0;

        foreach (var documento in candidatos)
        {
            procesados++;

            if (string.IsNullOrWhiteSpace(documento.RutaBlobStorage))
            {
                continue;
            }

            try
            {
                var ruta = documento.RutaBlobStorage;
                var existe = await _blobStorageService.ExistsAsync(ruta);

                if (!existe)
                {
                    documento.RutaBlobStorage = null;
                    await _documentoRepository.UpdateAsync(documento);

                    await AddAuditoriaAsync(
                        documento.Id,
                        "BlobNoEncontrado",
                        "Warning",
                        $"Blob no encontrado en almacenamiento. Ruta original: {ruta}");

                    noEncontrados++;
                    continue;
                }

                var eliminado = await _blobStorageService.DeleteDocumentAsync(ruta);
                if (eliminado)
                {
                    documento.RutaBlobStorage = null;
                    await _documentoRepository.UpdateAsync(documento);

                    await AddAuditoriaAsync(
                        documento.Id,
                        "BlobEliminado",
                        "Info",
                        $"Blob eliminado correctamente: {ruta}");

                    eliminados++;
                    bytesLiberados += Math.Max(documento.TamanoBytes, 0);
                    continue;
                }

                await AddAuditoriaAsync(
                    documento.Id,
                    "BlobEliminadoError",
                    "Error",
                    $"No se pudo eliminar el blob: {ruta}");
                errores++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error limpiando blob para documento {DocumentoId}", documento.Id);

                await AddAuditoriaAsync(
                    documento.Id,
                    "BlobEliminadoError",
                    "Error",
                    $"Excepción durante limpieza de blob: {ex.Message}");

                errores++;
            }
        }

        _logger.LogInformation(
            "Ciclo BlobCleanup completado. procesados={Procesados} eliminados={Eliminados} noEncontrados={NoEncontrados} errores={Errores} bytesLiberados={BytesLiberados}",
            procesados,
            eliminados,
            noEncontrados,
            errores,
            bytesLiberados);

        _telemetryClient.TrackEvent(
            "BlobCleanupCycle",
            new Dictionary<string, string>
            {
                ["source"] = "BlobCleanupTimerTrigger"
            },
            new Dictionary<string, double>
            {
                ["blobs_procesados"] = procesados,
                ["blobs_eliminados"] = eliminados,
                ["blobs_no_encontrados"] = noEncontrados,
                ["blobs_error"] = errores,
                ["bytes_liberados"] = bytesLiberados
            });
    }

    private Task AddAuditoriaAsync(int documentoId, string accion, string nivel, string mensaje)
    {
        return _auditoriaRepository.AddAsync(new AuditoriaEntity
        {
            DocumentoId = documentoId,
            Accion = accion,
            Nivel = nivel,
            Mensaje = mensaje,
            FechaHora = DateTime.UtcNow
        });
    }
}
