using System;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Activities
{
    public class SubirGDCActivity
    {
        private readonly ILogger<SubirGDCActivity> logger;
        private readonly IGdcService gdcService;
        private readonly TipologiaConfigLoader tipologiaLoader;
        private readonly GdcSettings gdcSettings;
        private readonly IBlobStorageService blobStorageService;

        public SubirGDCActivity(
            ILogger<SubirGDCActivity> logger,
            IGdcService gdcService,
            TipologiaConfigLoader tipologiaLoader,
            IOptions<GdcSettings> gdcOptions,
            IBlobStorageService blobStorageService)
        {
            this.logger = logger;
            this.gdcService = gdcService;
            this.tipologiaLoader = tipologiaLoader;
            this.gdcSettings = gdcOptions.Value;
            this.blobStorageService = blobStorageService;
        }

        public class SubirGDCActivityInput
        {
            public string Tipologia { get; set; } = string.Empty;
            public SubirGDCInput Input { get; set; } = new SubirGDCInput();
        }

        [Function("SubirGDCActivity")]
        public async Task<ResultadoGDC> Run([ActivityTrigger] SubirGDCActivityInput wrapper)
        {
            var input = wrapper?.Input ?? new SubirGDCInput();
            var tipologia = wrapper?.Tipologia ?? string.Empty;

            var resultado = new ResultadoGDC();

            try
            {
                // Determine matricula: prefer explicit, else tipologia config, else default
                var matricula = input.Matricula;
                if (string.IsNullOrWhiteSpace(matricula) && !string.IsNullOrWhiteSpace(tipologia))
                {
                    try
                    {
                        var cfg = tipologiaLoader.LoadConfig(tipologia);
                        if (!string.IsNullOrWhiteSpace(cfg.TipologiaMGDCMatricula))
                        {
                            matricula = cfg.TipologiaMGDCMatricula;
                        }

                        // Resolve GDC taxonomy fields from tipología config when not already set
                        if (string.IsNullOrWhiteSpace(input.TipoDocumento) && !string.IsNullOrWhiteSpace(cfg.GdcTipoDocumento))
                        {
                            input.TipoDocumento = cfg.GdcTipoDocumento;
                        }

                        if (string.IsNullOrWhiteSpace(input.SubtipoDocumento) && !string.IsNullOrWhiteSpace(cfg.GdcSubtipoDocumento))
                        {
                            input.SubtipoDocumento = cfg.GdcSubtipoDocumento;
                        }

                        if (string.IsNullOrWhiteSpace(input.Serie) && !string.IsNullOrWhiteSpace(cfg.GdcSerie))
                        {
                            input.Serie = cfg.GdcSerie;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "No se pudo cargar config de tipologia {Tipologia}, usando default matricula", tipologia);
                    }
                }

                if (string.IsNullOrWhiteSpace(matricula))
                {
                    matricula = gdcSettings.DefaultMatricula;
                }

                // Blob-first: si hay BlobPath y no hay base64 → descargar del blob
                if (!string.IsNullOrWhiteSpace(input.BlobPath) && string.IsNullOrWhiteSpace(input.ContenidoBase64))
                {
                    logger.LogInformation("Descargando documento desde blob para subida a GDC. BlobPath={BlobPath}", input.BlobPath);
                    var bytes = await blobStorageService.DownloadDocumentAsync(input.BlobPath);
                    input.ContenidoBase64 = Convert.ToBase64String(bytes);
                }

                // Validate IdActivo
                if (string.IsNullOrWhiteSpace(input.IdActivo))
                {
                    resultado.Exitoso = false;
                    resultado.Mensaje = "IdActivo ausente, no se puede subir a GDC";
                    logger.LogWarning("SubirGDCActivity: IdActivo ausente para archivo {Nombre}", input.NombreArchivo);
                    return resultado;
                }

                // Check existence
                logger.LogInformation("Consultar documento en GDC para IdActivo={IdActivo} Matricula={Matricula} MD5={MD5}", input.IdActivo, matricula, input.MD5);
                var (exists, objectId) = await gdcService.ConsultarDocumentoAsync(input.IdActivo, input.MD5, matricula);

                if (exists)
                {
                    resultado.Exitoso = true;
                    resultado.YaExistia = true;
                    resultado.ObjectId = objectId ?? string.Empty;
                    resultado.Mensaje = "Documento ya existente";
                    logger.LogInformation("Documento ya existe en GDC: {ObjectId}", resultado.ObjectId);
                    return resultado;
                }

                // Ensure matricula in input
                input.Matricula = matricula;

                // Upload
                logger.LogInformation("Subiendo documento a GDC IdActivo={IdActivo} Nombre={Nombre}", input.IdActivo, input.NombreArchivo);
                var uploadResult = await gdcService.SubirDocumentoAsync(input);
                return uploadResult;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en SubirGDCActivity");
                resultado.Exitoso = false;
                resultado.Mensaje = "Exception";
                resultado.ErrorDetalle = ex.ToString();
                return resultado;
            }
        }
    }
}
