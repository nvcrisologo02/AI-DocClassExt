using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Activities;
using DocumentIA.Functions.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Orchestrators;

public class DocumentProcessOrchestrator
{
    private readonly ExtractionModelRegistryLoader _extractionModelRegistryLoader;
    private readonly ClassificationPreparationSettings _classificationPreparationSettings;

    public DocumentProcessOrchestrator(
        ExtractionModelRegistryLoader extractionModelRegistryLoader,
        IOptions<ClassificationPreparationSettings> classificationPreparationSettings)
    {
        _extractionModelRegistryLoader = extractionModelRegistryLoader;
        _classificationPreparationSettings = classificationPreparationSettings.Value;
    }

    internal static ObtenerActivoInput BuildObtenerActivoInput(
        ContratoEntrada entrada,
        ContratoSalida salida,
        ResolvedTipologia tipologiaResuelta)
    {
        // Resolver campos de búsqueda: Instrucciones > auto-detección
        var idufirOverride = entrada.Instrucciones.AssetResolver?.CamposBusqueda?.Idufir;
        var refCatastralOverride = entrada.Instrucciones.AssetResolver?.CamposBusqueda?.ReferenciaCatastral;

        // Resolver campos solicitados: Instrucciones > Tipología > solo obligatorios
        var camposSolicitados = entrada.Instrucciones.AssetResolver?.CamposSolicitados
            ?? tipologiaResuelta.AssetResolverCamposSolicitados;
        var modoCombinacionCriterios = tipologiaResuelta.AssetResolverModoCombinacionCriterios;

        // Aliases desde tipología para auto-detección en DatosExtraidos
        var mapeoIdufir = tipologiaResuelta.AssetResolverMapeoIdufir;
        var mapeoRefCatastral = tipologiaResuelta.AssetResolverMapeoReferenciaCatastral;

        // Flags de habilitación por criterio
        var busquedaIdufirHabilitada = tipologiaResuelta.AssetResolverBusquedaIdufirHabilitada;
        var busquedaReferenciaCatastralHabilitada = tipologiaResuelta.AssetResolverBusquedaReferenciaCatastralHabilitada;

        // Búsqueda por dirección como criterio adicional
        var busquedaDireccionHabilitada = tipologiaResuelta.AssetResolverBusquedaDireccionHabilitada;
        var mapeoDireccionCompleta = tipologiaResuelta.AssetResolverMapeoDireccionCompleta;
        var mapeoDireccionNombreVia = tipologiaResuelta.AssetResolverMapeoDireccionNombreVia;
        var mapeoDireccionNumero = tipologiaResuelta.AssetResolverMapeoDireccionNumero;
        var mapeoDireccionMunicipio = tipologiaResuelta.AssetResolverMapeoDireccionMunicipio;
        var mapeoDireccionCodigoPostal = tipologiaResuelta.AssetResolverMapeoDireccionCodigoPostal;
        var umbralScoreDireccion = tipologiaResuelta.AssetResolverUmbralScoreDireccion;

        return new ObtenerActivoInput
        {
            CorrelationId = entrada.Trazabilidad.CorrelationId,
            Tipologia = salida.Identificacion.Tipologia,
            DatosExtraidos = salida.DatosExtraidos,
            CamposSolicitados = camposSolicitados,
            IdufirOverride = idufirOverride,
            ReferenciaCatastralOverride = refCatastralOverride,
            ModoCombinacionCriterios = modoCombinacionCriterios,
            MapeoIdufir = mapeoIdufir?.ToList() ?? new List<string>(),
            MapeoReferenciaCatastral = mapeoRefCatastral?.ToList() ?? new List<string>(),
            BusquedaIdufirHabilitada = busquedaIdufirHabilitada,
            BusquedaReferenciaCatastralHabilitada = busquedaReferenciaCatastralHabilitada,
            BusquedaDireccionHabilitada = busquedaDireccionHabilitada,
            MapeoDireccionCompleta = mapeoDireccionCompleta ?? new(),
            MapeoDireccionNombreVia = mapeoDireccionNombreVia ?? new(),
            MapeoDireccionNumero = mapeoDireccionNumero ?? new(),
            MapeoDireccionMunicipio = mapeoDireccionMunicipio ?? new(),
            MapeoDireccionCodigoPostal = mapeoDireccionCodigoPostal ?? new(),
            UmbralScoreDireccion = umbralScoreDireccion
        };
    }
    [Function("DocumentProcessOrchestrator")]
    public async Task<ContratoSalida> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger<DocumentProcessOrchestrator>();

        var entrada = context.GetInput<ContratoEntrada>();
        if (entrada == null)
        {
            throw new ArgumentNullException(nameof(entrada), "Contrato de entrada no puede ser nulo");
        }

        logger.LogInformation($"Iniciando procesamiento para documento: {entrada.Documento.Name}");

        var salida = new ContratoSalida
        {
            Identificacion = new Identificacion
            {
                Documento = entrada.Documento.Name,
                FechaProceso = context.CurrentUtcDateTime
            },
            Integridad = new Integridad()
        };

        var inicioOrquestacion = context.CurrentUtcDateTime;
        // Poblar identificadores de correlación (instanceId es determinista y replay-safe)
        salida.DetalleEjecucion.InstanceId = context.InstanceId;
        salida.DetalleEjecucion.OperationId = entrada.Trazabilidad.OperationId;

        var entradaPorObjectIdGdc = !string.IsNullOrWhiteSpace(entrada.Documento.ObjectIdGDC);
        var actividadesNegocio = new List<string>();
        if (entradaPorObjectIdGdc)
        {
            actividadesNegocio.AddRange(new[] { "ObtenerMetadatosGDC", "VerificarDuplicadoPreGDC", "ObtenerDocumentoGDC" });
        }

        actividadesNegocio.AddRange(new[] { "Clasificar", "Extraer", "Validar", "ObtenerActivo", "Integrar", "SubirGDC", "Persistir" });

        var seguimiento = salida.DetalleEjecucion.Seguimiento;
        seguimiento.Estado = "Pending";
        seguimiento.ActividadActual = string.Empty;
        seguimiento.ActividadesTotales = actividadesNegocio.Count;
        seguimiento.Actividades.Clear();
        seguimiento.ActividadesCompletadas.Clear();

        foreach (var actividad in actividadesNegocio)
        {
            seguimiento.Actividades.Add(new TrazaActividad
            {
                Nombre = actividad,
                Estado = "Pending"
            });
        }

        TrazaActividad ObtenerTraza(string nombre) =>
            seguimiento.Actividades.First(a => string.Equals(a.Nombre, nombre, StringComparison.Ordinal));

        void PublicarEstado(string estado, string actividadActual, string? mensaje = null)
        {
            seguimiento.Estado = estado;
            seguimiento.ActividadActual = actividadActual;
            seguimiento.DuracionTotalMs = (int)Math.Max(0, (context.CurrentUtcDateTime - inicioOrquestacion).TotalMilliseconds);

            context.SetCustomStatus(new
            {
                version = seguimiento.Version,
                estado = seguimiento.Estado,
                actividadActual = seguimiento.ActividadActual,
                actividadesTotales = seguimiento.ActividadesTotales,
                actividadesCompletadas = seguimiento.ActividadesCompletadas,
                duracionTotalMs = seguimiento.DuracionTotalMs,
                actividades = seguimiento.Actividades.Select(a => new
                {
                    nombre = a.Nombre,
                    estado = a.Estado,
                    duracionMs = a.DuracionMs,
                    mensaje = a.Mensaje,
                    fallbackActivado = a.FallbackActivado,
                    fallbackRazon = a.FallbackRazon
                }).ToList(),
                mensaje
            });
        }

        void MarcarInicioActividad(string nombre)
        {
            var traza = ObtenerTraza(nombre);
            traza.Estado = "Running";
            traza.Mensaje = null;
            traza.FallbackActivado = false;
            traza.FallbackRazon = null;
            traza.InicioUtc = context.CurrentUtcDateTime;
            traza.FinUtc = null;
            traza.DuracionMs = 0;

            PublicarEstado("Running", nombre);
        }

        void MarcarFinActividad(
            string nombre,
            string estado,
            string? mensaje = null,
            bool fallbackActivado = false,
            string? fallbackRazon = null)
        {
            var traza = ObtenerTraza(nombre);
            traza.FinUtc = context.CurrentUtcDateTime;

            if (traza.InicioUtc == default)
            {
                traza.InicioUtc = traza.FinUtc.Value;
            }

            traza.DuracionMs = (int)Math.Max(0, (traza.FinUtc.Value - traza.InicioUtc).TotalMilliseconds);
            traza.Estado = estado;
            traza.Mensaje = mensaje;
            traza.FallbackActivado = fallbackActivado;
            traza.FallbackRazon = fallbackRazon;

            if ((estado == "Completed" || estado == "Skipped" || estado == "Timeout") &&
                !seguimiento.ActividadesCompletadas.Contains(nombre))
            {
                seguimiento.ActividadesCompletadas.Add(nombre);
            }

            PublicarEstado("Running", string.Empty, mensaje);
        }

        void MarcarActividadOmitida(string nombre, string mensaje)
        {
            if (!seguimiento.Actividades.Any(a => string.Equals(a.Nombre, nombre, StringComparison.Ordinal)))
            {
                return;
            }

            MarcarInicioActividad(nombre);
            MarcarFinActividad(nombre, "Skipped", mensaje);
        }

        void RegistrarMarkdown(string? markdown, string origen)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return;
            }

            salida.DetalleEjecucion.MarkdownGenerado = true;
            salida.DetalleEjecucion.OrigenMarkdown = origen;
        }

        void RegistrarModeloLlm(string? modelo)
        {
            if (!string.IsNullOrWhiteSpace(modelo))
            {
                salida.DetalleEjecucion.ModeloLLMUsado = modelo;
            }
        }

        async Task<T> EjecutarPasoNegocio<T>(string nombre, Func<Task<T>> accion)
        {
            MarcarInicioActividad(nombre);

            try
            {
                var resultado = await accion();
                MarcarFinActividad(nombre, "Completed");
                return resultado;
            }
            catch (Exception ex)
            {
                MarcarFinActividad(nombre, "Failed", ex.Message);
                throw;
            }
        }

        async Task EjecutarPasoNegocioSinResultado(string nombre, Func<Task> accion)
        {
            MarcarInicioActividad(nombre);

            try
            {
                await accion();
                MarcarFinActividad(nombre, "Completed");
            }
            catch (Exception ex)
            {
                MarcarFinActividad(nombre, "Failed", ex.Message);
                throw;
            }
        }

        void FinalizarSeguimiento(string estado, string? mensaje = null)
        {
            PublicarEstado(estado, string.Empty, mensaje);
        }

        async Task EjecutarSubidaGdcAsync(string? idActivo, bool skipGdc)
        {
            if (!skipGdc)
            {
                if (!string.IsNullOrWhiteSpace(idActivo))
                {
                    MarcarInicioActividad("SubirGDC");
                    logger.LogInformation("Paso 7: Intentando subir a GDC IdActivo={IdActivo}", idActivo);

                    var subirInput = new SubirGDCActivity.SubirGDCActivityInput
                    {
                        Tipologia = salida.Identificacion.Tipologia,
                        Input = new SubirGDCInput
                        {
                            IdActivo = idActivo,
                            ContenidoBase64 = entrada.Documento.Content.Base64,
                            NombreArchivo = entrada.Documento.Name,
                            SHA256 = salida.Integridad.SHA256,
                            MD5 = salida.Integridad.MD5,
                            CorrelationId = entrada.Trazabilidad.CorrelationId ?? string.Empty
                        }
                    };

                    const int GdcActivityTimeoutSeconds = 120;
                    using var cts = new System.Threading.CancellationTokenSource();
                    var activityTask = context.CallActivityAsync<ResultadoGDC>(
                        "SubirGDCActivity",
                        subirInput);
                    var timeoutTask = context.CreateTimer(
                        context.CurrentUtcDateTime.AddSeconds(GdcActivityTimeoutSeconds),
                        cts.Token);

                    var winner = await Task.WhenAny(activityTask, timeoutTask);
                    ResultadoGDC? resultadoGdc;
                    if (winner == timeoutTask)
                    {
                        logger.LogWarning(
                            "SubirGDCActivity timeout after {TimeoutSeconds}s for IdActivo={IdActivo}",
                            GdcActivityTimeoutSeconds,
                            idActivo);
                        resultadoGdc = new ResultadoGDC { Exitoso = false, Mensaje = "Timeout" };
                        MarcarFinActividad("SubirGDC", "Timeout", "Timeout en SubirGDCActivity");
                    }
                    else
                    {
                        cts.Cancel();
                        resultadoGdc = await activityTask;
                        MarcarFinActividad("SubirGDC", "Completed", resultadoGdc?.Mensaje);
                    }

                    salida.DetalleEjecucion.GDC = resultadoGdc ?? new ResultadoGDC();

                    if (resultadoGdc != null && resultadoGdc.Exitoso && !string.IsNullOrWhiteSpace(resultadoGdc.ObjectId))
                    {
                        salida.Integridad.GestorDocumental = resultadoGdc.ObjectId;
                    }
                }
                else
                {
                    logger.LogInformation("Omitiendo subida a GDC: IdActivo no disponible");
                    MarcarActividadOmitida("SubirGDC", "IdActivo no disponible");
                }
            }
            else
            {
                logger.LogInformation(
                    "SkipGDCUpload activo (fuente={Fuente}); se omite subida a GDC",
                    entrada.Instrucciones.SkipGDCUpload.HasValue ? "instrucciones" : "config-tipologia");
                MarcarActividadOmitida("SubirGDC", "SkipGDCUpload activo");
                salida.DetalleEjecucion.GDC = new ResultadoGDC { Exitoso = true, Mensaje = "Skipped" };
            }
        }

        PublicarEstado("Running", string.Empty);

        try
        {
            salida.DetalleEjecucion.ClassificationOnly = entrada.Instrucciones.ClassificationOnly;
            salida.DetalleEjecucion.NivelClasificacion = entrada.Instrucciones.Classification.NivelClasificacion;

            if (entradaPorObjectIdGdc)
            {
                entrada.Instrucciones.SkipGDCUpload = true;

                GdcDocumentoMetadatos? metadatosGdc = null;

                MarcarInicioActividad("ObtenerMetadatosGDC");
                try
                {
                    metadatosGdc = await context.CallActivityAsync<GdcDocumentoMetadatos>(
                        "ObtenerMetadatosDocumentoGDCActivity",
                        entrada.Documento.ObjectIdGDC);
                    MarcarFinActividad("ObtenerMetadatosGDC", "Completed", string.IsNullOrWhiteSpace(metadatosGdc?.MD5)
                        ? "Metadatos recuperados sin checksum"
                        : "Metadatos recuperados");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "No se pudieron recuperar metadatos desde GDC. Se continúa con descarga directa.");
                    MarcarFinActividad("ObtenerMetadatosGDC", "Skipped", "Metadatos no disponibles");
                }

                if (entrada.Instrucciones.SkipDuplicateCheck)
                {
                    MarcarInicioActividad("VerificarDuplicadoPreGDC");
                    MarcarFinActividad("VerificarDuplicadoPreGDC", "Skipped", "SkipDuplicateCheck activo");
                }
                else if (!string.IsNullOrWhiteSpace(metadatosGdc?.MD5))
                {
                    var duplicadoPorMd5 = await EjecutarPasoNegocio(
                        "VerificarDuplicadoPreGDC",
                        () => context.CallActivityAsync<VerificarDuplicadoMd5Result>(
                            "VerificarDuplicadoPorMD5Activity",
                            metadatosGdc.MD5));

                    if (duplicadoPorMd5.Existe && !entrada.Instrucciones.ForceReprocess && !string.IsNullOrWhiteSpace(duplicadoPorMd5.SHA256))
                    {
                        logger.LogWarning("Documento duplicado detectado por MD5 GDC. Recuperando última ejecución persistida");

                        var salidaDuplicado = await context.CallActivityAsync<ContratoSalida?>(
                            "ObtenerUltimaEjecucionDuplicadoActivity",
                            new ObtenerUltimaEjecucionDuplicadoInput
                            {
                                SHA256 = duplicadoPorMd5.SHA256,
                                ClassificationOnly = entrada.Instrucciones.ClassificationOnly,
                                NivelClasificacion = entrada.Instrucciones.Classification.NivelClasificacion
                            });

                        if (salidaDuplicado is not null)
                        {
                            salidaDuplicado.Resultado.ReutilizadaPorDuplicado = true;
                            salidaDuplicado.Resultado.MensajeReutilizacion = "Documento ya procesado previamente (checksum GDC). Se reutiliza la última ejecución.";

                            FinalizarSeguimiento("Completed", "Documento duplicado detectado por checksum GDC. Devolviendo última ejecución");
                            salidaDuplicado.DetalleEjecucion.Seguimiento = salida.DetalleEjecucion.Seguimiento;
                            return salidaDuplicado;
                        }
                    }
                }
                else
                {
                    MarcarInicioActividad("VerificarDuplicadoPreGDC");
                    MarcarFinActividad("VerificarDuplicadoPreGDC", "Skipped", "Checksum MD5 no disponible en metadatos GDC");
                }

                var documentoGdc = await EjecutarPasoNegocio(
                    "ObtenerDocumentoGDC",
                    () => context.CallActivityAsync<ObtenerDocumentoGDCResult>(
                        "ObtenerDocumentoGDCActivity",
                        entrada.Documento.ObjectIdGDC));

                entrada.Documento.Content.Base64 = documentoGdc.Base64;

                if (string.IsNullOrWhiteSpace(entrada.Documento.Name))
                {
                    entrada.Documento.Name = !string.IsNullOrWhiteSpace(documentoGdc.NombreArchivo)
                        ? documentoGdc.NombreArchivo
                        : metadatosGdc?.NombreArchivo ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(entrada.Documento.Name))
                {
                    salida.Identificacion.Documento = entrada.Documento.Name;
                }
            }

            // 1. Normalizacion y calculo de hashes
            logger.LogInformation("Paso 1: Normalizando documento");
            var datosNormalizados = await context.CallActivityAsync<Dictionary<string, object>>(
                "NormalizarActivity",
                entrada);

            salida.Integridad.SHA256 = datosNormalizados["SHA256"].ToString() ?? "";
            salida.Integridad.MD5 = datosNormalizados["MD5"].ToString() ?? "";
            salida.Integridad.CRC32 = datosNormalizados["CRC32"].ToString() ?? "";
            salida.Integridad.TamanoBytes = ObtenerEntero(datosNormalizados, "TamañoBytes");
            if (salida.Integridad.TamanoBytes == 0)
            {
                salida.Integridad.TamanoBytes = ObtenerEntero(datosNormalizados, "TamanoBytes");
            }
            salida.Integridad.RutaBlobStorage = null;
            salida.Identificacion.Paginas = ObtenerEntero(datosNormalizados, "Paginas");

            // 2. Verificar duplicados (si esta habilitado)
            if (!entrada.Instrucciones.SkipDuplicateCheck)
            {
                logger.LogInformation("Paso 2: Verificando duplicados");
                var esDuplicado = await context.CallActivityAsync<bool>(
                    "VerificarDuplicadoActivity",
                    salida.Integridad.SHA256);

                if (esDuplicado && !entrada.Instrucciones.ForceReprocess)
                {
                    logger.LogWarning("Documento duplicado detectado. Recuperando última ejecución persistida");

                    var salidaDuplicado = await context.CallActivityAsync<ContratoSalida?>(
                        "ObtenerUltimaEjecucionDuplicadoActivity",
                        new ObtenerUltimaEjecucionDuplicadoInput
                        {
                            SHA256 = salida.Integridad.SHA256,
                            ClassificationOnly = entrada.Instrucciones.ClassificationOnly,
                            NivelClasificacion = entrada.Instrucciones.Classification.NivelClasificacion
                        });

                    if (salidaDuplicado is not null)
                    {
                        if (salidaDuplicado.Integridad.TamanoBytes <= 0 && salida.Integridad.TamanoBytes > 0)
                        {
                            salidaDuplicado.Integridad.TamanoBytes = salida.Integridad.TamanoBytes;
                        }

                        salidaDuplicado.Resultado.ReutilizadaPorDuplicado = true;
                        salidaDuplicado.Resultado.MensajeReutilizacion = "Documento ya procesado previamente. Se reutiliza la última ejecución.";

                        FinalizarSeguimiento("Completed", "Documento duplicado detectado. Devolviendo última ejecución");
                        salidaDuplicado.DetalleEjecucion.Seguimiento = salida.DetalleEjecucion.Seguimiento;
                        return salidaDuplicado;
                    }

                    salida.Resultado.Estado = "DUPLICADO";
                    salida.Resultado.ReutilizadaPorDuplicado = true;
                    salida.Resultado.MensajeReutilizacion = "Documento duplicado detectado sin ejecución histórica reutilizable.";

                    FinalizarSeguimiento("Completed", "Documento detectado como duplicado");
                    return salida;
                }
            }

            logger.LogInformation("Paso 2.5: Subiendo documento a blob storage");
            var blobPath = await context.CallActivityAsync<string>(
                "SubirBlobActivity",
                new SubirBlobInput
                {
                    ContenidoBase64 = entrada.Documento.Content.Base64,
                    NombreArchivo = entrada.Documento.Name,
                    Contenedor = "documents",
                    BlobPath = entrada.Documento.BlobPath
                });

            if (!string.IsNullOrWhiteSpace(blobPath))
            {
                salida.Integridad.RutaBlobStorage = blobPath;
            }

            var docClasif = new PrepararDocumentoClasificacionResultado
            {
                DocumentoBase64Clasif = entrada.Documento.Content.Base64,
                TotalPaginas = salida.Identificacion.Paginas,
                CharsTextoNativo = 0,
                PaginasIncluidas = salida.Identificacion.Paginas,
                RecorteAplicado = false
            };

            salida.DetalleEjecucion.RecorteAplicado = docClasif.RecorteAplicado;
            salida.DetalleEjecucion.PaginasIncluidas = docClasif.PaginasIncluidas;

            if (_classificationPreparationSettings.Enabled)
            {
                try
                {
                    var maxPaginasClasificacion = entrada.Instrucciones.ClassificationOnly &&
                        entrada.Instrucciones.MaxPagesForClassificationOnly > 0
                        ? entrada.Instrucciones.MaxPagesForClassificationOnly
                        : ResolveMaxPaginasClasificacion(entrada.Instrucciones.ExpectedType);
                    logger.LogInformation(
                        "Paso 2.7: Preparando documento para clasificación. MaxPaginas={MaxPaginas}",
                        maxPaginasClasificacion);

                    var docClasifResult = await context.CallActivityAsync<PrepararDocumentoClasificacionResultado>(
                        "PrepararDocumentoClasificacionActivity",
                        new PrepararDocumentoClasificacionInput
                        {
                            DocumentoBase64 = entrada.Documento.Content.Base64,
                            NombreDocumento = entrada.Documento.Name,
                            MaxPaginasClasificacion = maxPaginasClasificacion,
                            BlobPath = !string.IsNullOrWhiteSpace(blobPath)
                                ? blobPath
                                : entrada.Documento.BlobPath
                        });

                    docClasif = docClasifResult ?? docClasif;

                    if (salida.Identificacion.Paginas <= 0 && docClasif.TotalPaginas > 0)
                    {
                        salida.Identificacion.Paginas = docClasif.TotalPaginas;
                    }

                    logger.LogInformation(
                        "Documento preparado para clasificación: totalPaginas={TotalPaginas}, paginasIncluidas={PaginasIncluidas}, charsTextoNativo={CharsTextoNativo}, recorteAplicado={RecorteAplicado}",
                        docClasif.TotalPaginas,
                        docClasif.PaginasIncluidas,
                        docClasif.CharsTextoNativo,
                        docClasif.RecorteAplicado);

                    salida.DetalleEjecucion.RecorteAplicado = docClasif.RecorteAplicado;
                    salida.DetalleEjecucion.PaginasIncluidas = docClasif.PaginasIncluidas > 0
                        ? docClasif.PaginasIncluidas
                        : (docClasif.TotalPaginas > 0 ? docClasif.TotalPaginas : salida.Identificacion.Paginas);
                }
                catch (Exception exPrep)
                {
                    logger.LogWarning(
                        exPrep,
                        "PrepararDocumentoClasificacionActivity falló. Se usará documento completo para clasificación.");
                }
            }

            // D4: si el caller aporta markdown pre-procesado en las instrucciones, inyectarlo directamente.
            // La condición del paso 2.8 comprueba !datosNormalizados.ContainsKey("Markdown"),
            // por lo que la inyección aquí evita la llamada innecesaria a ExtraerMarkdownLayoutActivity.
            if (!string.IsNullOrWhiteSpace(entrada.Instrucciones.Classification.Markdown))
            {
                datosNormalizados["Markdown"] = entrada.Instrucciones.Classification.Markdown;
                RegistrarMarkdown(entrada.Instrucciones.Classification.Markdown, "InstruccionesCallerPreClasificacion");
                logger.LogInformation(
                    "Paso 2.8 omitido: markdown inyectado desde instrucciones del caller ({Len} chars)",
                    entrada.Instrucciones.Classification.Markdown.Length);
            }

            // 2.8: Asegurar contexto textual para clasificación.
            // DI y CU generan su propio markdown durante la ejecución de clasificación y lo propagan
            // a datosNormalizados en el paso post-clasificación.
            // Todos los demás providers (gpt, hybrid, rules, auto) necesitan markdown previo;
            // si no está disponible, lo extraemos aquí vía DI Layout con el documento recortado.
            if (!datosNormalizados.ContainsKey("Markdown")
                && string.IsNullOrWhiteSpace(entrada.Instrucciones.ExpectedType)
                && !ClasificacionProviderGeneraMarkdownPropio(entrada.Instrucciones.Classification.Provider))
            {
                logger.LogInformation(
                    "Paso 2.8: Extrayendo markdown DI Layout previo a clasificación (provider={Provider}, doc={Doc})",
                    entrada.Instrucciones.Classification.Provider ?? "auto",
                    entrada.Documento.Name);
                try
                {
                    var markdownPreClasif = await context.CallActivityAsync<ExtraerMarkdownLayoutResultado>(
                        "ExtraerMarkdownLayoutActivity",
                        new ExtraerMarkdownLayoutInput
                        {
                            Tipologia = string.Empty,
                            DocumentoBase64 = docClasif.DocumentoBase64Clasif,
                            NombreDocumento = entrada.Documento.Name
                        });

                    if (!string.IsNullOrWhiteSpace(markdownPreClasif.Markdown))
                    {
                        datosNormalizados["Markdown"] = markdownPreClasif.Markdown;
                        RegistrarMarkdown(markdownPreClasif.Markdown, "LayoutPreClasificacion");
                        logger.LogInformation(
                            "Paso 2.8: Markdown DI Layout listo para clasificación ({Len} chars)",
                            markdownPreClasif.Markdown.Length);
                        if (salida.Identificacion.Paginas <= 0 && markdownPreClasif.Paginas > 0)
                            salida.Identificacion.Paginas = markdownPreClasif.Paginas;
                    }
                    else
                    {
                        logger.LogWarning(
                            "Paso 2.8: DI Layout no devolvió markdown útil para {Doc}. Se continúa sin markdown.",
                            entrada.Documento.Name);
                    }
                }
                catch (Exception exMd)
                {
                    logger.LogWarning(
                        exMd,
                        "Paso 2.8: No se pudo extraer markdown DI Layout previo. Se continúa sin markdown.");
                }
            }

            // 3. Clasificacion
            ResultadoClasificacion resultadoClasificacion;
            if (!string.IsNullOrWhiteSpace(entrada.Instrucciones.ExpectedType))
            {
                MarcarInicioActividad("Clasificar");
                logger.LogInformation("Paso 3: Clasificación omitida por ExpectedType={ExpectedType}", entrada.Instrucciones.ExpectedType);
                resultadoClasificacion = new ResultadoClasificacion
                {
                    Modelo = "expectedtype-input",
                    Confianza = 1.0,
                    FallbackLLM = false,
                    TipologiaDetectada = entrada.Instrucciones.ExpectedType
                };
                MarcarFinActividad("Clasificar", "Completed", "Clasificación por ExpectedType");
            }
            else
            {
                logger.LogInformation("Paso 3: Clasificando documento");
                MarcarInicioActividad("Clasificar");

                // Umbral de fallback DI→GPT: instrucciones ?? config servidor
                // (tipología no conocida todavía en este punto)
                var umbralClasifFallback = entrada.Instrucciones.Classification.Umbral;

                try
                {
                    resultadoClasificacion = await context.CallActivityAsync<ResultadoClasificacion>(
                        "ClasificarActivity",
                        new ClasificacionInput
                        {
                            Entrada = entrada,
                            DatosNormalizados = datosNormalizados,
                            UmbralFallbackEfectivo = umbralClasifFallback,
                            DocumentoBase64Override = docClasif.DocumentoBase64Clasif,
                            CharsTextoNativo = docClasif.CharsTextoNativo,
                            TotalPaginas = docClasif.TotalPaginas
                        });

                        var mensajeClasificacion = resultadoClasificacion.FallbackLLM
                            ? $"Fallback Azure OpenAI activado ({resultadoClasificacion.FallbackRazon ?? "sin razon informada"})"
                            : (!string.IsNullOrWhiteSpace(resultadoClasificacion.FallbackRazon)
                                && resultadoClasificacion.FallbackRazon.StartsWith("fallback_attempt_failed:", StringComparison.OrdinalIgnoreCase)
                                ? $"Fallback Azure OpenAI intentado pero no aplicado ({resultadoClasificacion.FallbackRazon})"
                                : null);

                    MarcarFinActividad(
                        "Clasificar",
                        "Completed",
                        mensajeClasificacion,
                        resultadoClasificacion.FallbackLLM,
                        resultadoClasificacion.FallbackRazon);
                }
                catch (Exception ex)
                {
                    MarcarFinActividad("Clasificar", "Failed", ex.Message);
                    
                    // Si la clasificación falló porque no se pudo identificar la tipología,
                    // terminar el proceso ahí sin intentar extraer ni validar
                    if (ex.Message.Contains("No se ha podido identificar la tipologia"))
                    {
                        const string mensajeTipologiaNoIdentificada = "no clasificable: no se ha podido identificar la tipologia del documento";

                        logger.LogWarning(
                            "Clasificación falló: no se pudo identificar la tipología. Terminando procesamiento.");

                        salida.DetalleEjecucion.Clasificacion = new ResultadoClasificacion
                        {
                            Modelo = "gpt-4o-mini",
                            Confianza = 0,
                            ConfianzaDI = 0,
                            ConfianzaGPT = 0,
                            ProveedorClasif = "GPT4oMini",
                            FallbackLLM = true,
                            FallbackRazon = "fallback_unclassified",
                            TipologiaDetectada = "Desconocido"
                        };
                        salida.DetalleEjecucion.MotivoErrorTipologia = mensajeTipologiaNoIdentificada;
                        RegistrarModeloLlm(salida.DetalleEjecucion.Clasificacion.Modelo);
                        
                        salida.Resultado.Estado = "NO_CLASIFICADO";
                        salida.Resultado.MensajeError = mensajeTipologiaNoIdentificada;
                        salida.Resultado.ConfianzaGlobal = 0;
                        salida.Resultado.EstadoCalidad = "ERROR";
                        salida.Resultado.ConfianzaClasificacion = 0;
                        salida.Resultado.ConfianzaExtraccion = 0;
                        salida.Resultado.ConfianzaValidacion = 0;
                        salida.DetalleEjecucion.Postproceso.Inconsistencias.Add(
                            $"Error: {mensajeTipologiaNoIdentificada}");
                        
                        FinalizarSeguimiento("Failed", mensajeTipologiaNoIdentificada);
                        return salida;
                    }
                    
                    throw;
                }
            }

            // Propagar markdown extraído por DI clasificador a DatosNormalizados
            if (!string.IsNullOrWhiteSpace(resultadoClasificacion.ContentExtraido)
                && !datosNormalizados.ContainsKey("Markdown"))
            {
                datosNormalizados["Markdown"] = resultadoClasificacion.ContentExtraido;
                RegistrarMarkdown(resultadoClasificacion.ContentExtraido, "Clasificacion");
                logger.LogInformation(
                    "Markdown de clasificación DI propagado a datosNormalizados ({Len} chars)",
                    resultadoClasificacion.ContentExtraido.Length);
            }
            if (resultadoClasificacion.FallbackLLM)
            {
                RegistrarModeloLlm(resultadoClasificacion.Modelo);
            }
            resultadoClasificacion.ContentExtraido = null; // limpiar: no exponer en respuesta
            salida.DetalleEjecucion.Clasificacion = resultadoClasificacion;

            if (resultadoClasificacion.ClasificacionParcial)
            {
                var tipologiaParcial = resultadoClasificacion.TipologiaDetectada ?? string.Empty;
                var esVirtual = string.IsNullOrWhiteSpace(tipologiaParcial)
                    || string.Equals(tipologiaParcial, "Desconocido", StringComparison.OrdinalIgnoreCase);

                if (esVirtual)
                {
                    // Tipología virtual: GPT identificó familia TDN1 pero no puede mapearla al catálogo.
                    // El pipeline se detiene aquí con Estado=OK y PropuestaTipologia accesible en la salida.
                    salida.Identificacion.Tipologia = tipologiaParcial;
                    salida.Identificacion.TipologiaFamilia = tipologiaParcial;
                    salida.Identificacion.TipologiaVersion = string.Empty;
                    salida.Identificacion.PropuestaTipologia = resultadoClasificacion.PropuestaTipologia;
                    salida.DetalleEjecucion.RunTipologia = tipologiaParcial;

                    salida.DetalleEjecucion.Extraccion = new ResultadoExtraccion
                    {
                        Modelo = "skipped",
                        LayoutEnabled = false,
                        FallbackUsado = false,
                        FallbackRazon = null,
                        ConfianzaExtraccion = 0,
                        ProveedorExtrac = "none",
                        ConfianzaPorCampo = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                        CamposConDuda = new List<string>(),
                        TiemposMs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    };

                    salida.DatosExtraidos = new Dictionary<string, object>();
                    salida.DetalleEjecucion.Postproceso = new InformacionPostproceso
                    {
                        Normalizaciones = new List<string>
                        {
                            "Tipología virtual TDN1: GPT no resolvió código de catálogo. Pipeline detenido con PropuestaTipologia."
                        },
                        Markdown = null,
                        Validaciones = new List<string>(),
                        Inconsistencias = new List<string>(),
                        ConfianzaValidacion = 1.0
                    };

                    MarcarActividadOmitida("ResolverTipologia", "Tipología virtual: sin código de catálogo");
                    MarcarActividadOmitida("Extraer", "Tipología virtual TDN1");
                    MarcarActividadOmitida("Prompt", "Tipología virtual TDN1");
                    MarcarActividadOmitida("Validar", "Tipología virtual TDN1");
                    MarcarActividadOmitida("ObtenerActivo", "Tipología virtual TDN1");
                    MarcarActividadOmitida("Integrar", "Tipología virtual TDN1");
                    MarcarActividadOmitida("SubirGDC", "Tipología virtual TDN1");

                    var confidenceCfgVirtual = new ConfidenceConfig();
                    salida.Resultado.Estado = "OK";
                    salida.Resultado.ConfianzaGlobal = RedondearSalida(
                        ConfidenceCalculator.Global(resultadoClasificacion.Confianza, null, 1.0));
                    salida.Resultado.EstadoCalidad = ConfidenceCalculator.EstadoCalidad(
                        salida.Resultado.ConfianzaGlobal,
                        confidenceCfgVirtual);
                    salida.Resultado.ConfianzaClasificacion = RedondearSalida(resultadoClasificacion.Confianza);
                    salida.Resultado.ConfianzaExtraccion = 0;
                    salida.Resultado.ConfianzaValidacion = 1.0;

                    await EjecutarPasoNegocioSinResultado(
                        "Persistir",
                        () => context.CallActivityAsync(
                            "PersistirActivity",
                            salida));

                    FinalizarSeguimiento("Completed", "Tipología virtual TDN1: propuesta sin código de catálogo");
                    return salida;
                }

                // Clasificación parcial con código TDN1 conocido: asignar Tdn1 y continuar pipeline.
                // nivelClasificacion=TDN1 no implica ClassificationOnly; el pipeline sigue normalmente.
                salida.Identificacion.Tdn1 = tipologiaParcial;
            }

            var tipologiaEntrada = resultadoClasificacion.TipologiaDetectada ?? "Desconocida";
            ResolvedTipologia tipologiaResuelta;

            try
            {
                tipologiaResuelta = await context.CallActivityAsync<ResolvedTipologia>(
                    "ResolverTipologiaActivity",
                    tipologiaEntrada);
            }
            catch (Exception ex) when (
                ex is KeyNotFoundException ||
                ex.InnerException is KeyNotFoundException ||
                ex is TaskFailedException && ex.Message.Contains("No existe la tipologia"))
            {
                const string mensajeTipologiaNoIdentificada = "no clasificable: no se ha podido identificar la tipologia del documento";

                logger.LogWarning(
                    ex,
                    "La clasificación devolvió una tipología no resoluble: {TipologiaDetectada}",
                    tipologiaEntrada);

                salida.DetalleEjecucion.RunTipologia = tipologiaEntrada;
                salida.DetalleEjecucion.MotivoErrorTipologia = ex.Message;
                salida.Resultado.Estado = "NO_CLASIFICADO";
                salida.Resultado.MensajeError = mensajeTipologiaNoIdentificada;
                salida.Resultado.ConfianzaGlobal = 0;
                salida.Resultado.EstadoCalidad = "ERROR";
                salida.Resultado.ConfianzaClasificacion = RedondearSalida(resultadoClasificacion.Confianza);
                salida.Resultado.ConfianzaExtraccion = 0;
                salida.Resultado.ConfianzaValidacion = 0;
                salida.DetalleEjecucion.Postproceso.Inconsistencias.Add($"Error: {mensajeTipologiaNoIdentificada}");

                FinalizarSeguimiento("Failed", mensajeTipologiaNoIdentificada);
                return salida;
            }

            var esTipologiaNoClasificable = string.Equals(tipologiaResuelta.TechnicalKey, "Desconocido", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipologiaResuelta.TipologiaId, "Desconocido", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipologiaResuelta.RequestedValue, "Desconocido", StringComparison.OrdinalIgnoreCase);

            if (esTipologiaNoClasificable)
            {
                const string mensajeTipologiaNoIdentificada = "no clasificable: tipologia desconocida";

                salida.DetalleEjecucion.RunTipologia = tipologiaResuelta.TechnicalKey;
                salida.DetalleEjecucion.MotivoErrorTipologia = mensajeTipologiaNoIdentificada;
                salida.DetalleEjecucion.Postproceso = new InformacionPostproceso
                {
                    Normalizaciones = new List<string>(),
                    Markdown = null,
                    Validaciones = new List<string>(),
                    Inconsistencias = new List<string> { $"Error: {mensajeTipologiaNoIdentificada}" }
                };

                salida.Identificacion.Tipologia = tipologiaResuelta.TechnicalKey;
                salida.Identificacion.TipologiaFamilia = tipologiaResuelta.TipologiaId;
                salida.Identificacion.TipologiaVersion = tipologiaResuelta.Version;
                salida.Resultado.Estado = "NO_CLASIFICADO";
                salida.Resultado.MensajeError = mensajeTipologiaNoIdentificada;
                salida.Resultado.ConfianzaGlobal = 0;
                salida.Resultado.EstadoCalidad = "ERROR";
                salida.Resultado.ConfianzaClasificacion = RedondearSalida(resultadoClasificacion.Confianza);
                salida.Resultado.ConfianzaExtraccion = 0;
                salida.Resultado.ConfianzaValidacion = 0;

                FinalizarSeguimiento("Completed", mensajeTipologiaNoIdentificada);
                return salida;
            }

            salida.Identificacion.Tipologia = tipologiaResuelta.TechnicalKey;
            salida.Identificacion.TipologiaFamilia = tipologiaResuelta.TipologiaId;
            salida.Identificacion.TipologiaVersion = tipologiaResuelta.Version;
            salida.DetalleEjecucion.RunTipologia = tipologiaResuelta.TechnicalKey;

            // Añadir actividad Prompt al seguimiento si la tipología la tiene habilitada o si hay override en la petición
            var promptActivoEnPeticion = tipologiaResuelta.PromptEnabled || entrada.Instrucciones.Prompt != null;
            if (promptActivoEnPeticion)
            {
                seguimiento.ActividadesTotales++;
                seguimiento.Actividades.Add(new TrazaActividad { Nombre = "Prompt", Estado = "Pending" });
            }

            // Verificar umbral de confianza: instrucciones ?? tipología ?? config servidor
            var umbralBajaConfianza = entrada.Instrucciones.Classification.Umbral
                ?? tipologiaResuelta.ConfidenceConfig?.ClasifUmbralFallback
                ?? resultadoClasificacion.UmbralFallbackAplicado
                ?? 0.6;

            if (resultadoClasificacion.Confianza < umbralBajaConfianza)
            {
                salida.Resultado.Estado = "BAJA_CONFIANZA_CLASIFICACION";
                logger.LogWarning($"Confianza de clasificacion baja: {resultadoClasificacion.Confianza} (umbral: {umbralBajaConfianza})");

                FinalizarSeguimiento("Completed", "Clasificación por debajo de umbral");
                return salida;
            }

            if (entrada.Instrucciones.ClassificationOnly)
            {
                logger.LogInformation("ClassificationOnly activo. Se omiten extracción, prompt, validación y asset resolver.");

                salida.DetalleEjecucion.Extraccion = new ResultadoExtraccion
                {
                    Modelo = "skipped",
                    LayoutEnabled = false,
                    FallbackUsado = false,
                    FallbackRazon = null,
                    ConfianzaExtraccion = 0,
                    ProveedorExtrac = "none",
                    ConfianzaPorCampo = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                    CamposConDuda = new List<string>(),
                    TiemposMs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                };

                var markdownClasificacion = datosNormalizados.TryGetValue("Markdown", out var markdownObj) &&
                    markdownObj is string markdownTexto &&
                    !string.IsNullOrWhiteSpace(markdownTexto)
                    ? markdownTexto
                    : null;

                salida.DatosExtraidos = new Dictionary<string, object>();
                salida.DetalleEjecucion.Postproceso = new InformacionPostproceso
                {
                    Normalizaciones = new List<string>
                    {
                        "ClassificationOnly activo: extracción, prompt, validación y asset resolver omitidos"
                    },
                    Markdown = markdownClasificacion,
                    Validaciones = new List<string>(),
                    Inconsistencias = new List<string>(),
                    ConfianzaValidacion = 1.0
                };

                if (!string.IsNullOrWhiteSpace(markdownClasificacion))
                {
                    salida.DetalleEjecucion.Postproceso.Normalizaciones.Add("Markdown");
                    RegistrarMarkdown(markdownClasificacion, salida.DetalleEjecucion.OrigenMarkdown ?? "Clasificacion");
                }

                MarcarActividadOmitida("Extraer", "ClassificationOnly activo");
                if (promptActivoEnPeticion)
                {
                    MarcarActividadOmitida("Prompt", "ClassificationOnly activo");
                }
                MarcarActividadOmitida("Validar", "ClassificationOnly activo");
                MarcarActividadOmitida("ObtenerActivo", "ClassificationOnly activo");

                var ejecutarIntegracionClasificacion =
                    entrada.Instrucciones.ExecuteIntegrarWhenClassificationOnly == true &&
                    !string.IsNullOrWhiteSpace(entrada.Trazabilidad.IdActivo);

                if (ejecutarIntegracionClasificacion)
                {
                    logger.LogInformation("ClassificationOnly activo con integración habilitada. Ejecutando Integrar.");

                    var resultadoIntegracionClasificacion = await EjecutarPasoNegocio(
                        "Integrar",
                        () => context.CallActivityAsync<DocumentIA.Core.Models.ResultadoIntegracion>(
                            "IntegrarActivity",
                            new DocumentIA.Core.Models.IntegrarInput
                            {
                                Tipologia = salida.Identificacion.Tipologia,
                                DocumentoId = salida.Identificacion.Guid,
                                DatosExtraidos = new Dictionary<string, object>(),
                                IdActivo = entrada.Trazabilidad.IdActivo,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["correlationId"] = entrada.Trazabilidad.CorrelationId,
                                    ["submittedBy"] = entrada.Trazabilidad.SubmittedBy
                                }
                            }));

                    salida.DetalleEjecucion.Integracion = resultadoIntegracionClasificacion;
                    salida.Integridad.IdActivoEntrada = resultadoIntegracionClasificacion.IdActivoEntrada;
                    salida.Integridad.IdActivo = resultadoIntegracionClasificacion.IdActivoResuelto ?? entrada.Trazabilidad.IdActivo;
                    salida.Integridad.IdActivoCambiado = resultadoIntegracionClasificacion.IdActivoCambiado;
                }
                else
                {
                    var mensajeIntegracion = entrada.Instrucciones.ExecuteIntegrarWhenClassificationOnly == true
                        ? "ClassificationOnly sin IdActivo"
                        : "ClassificationOnly activo";

                    MarcarActividadOmitida("Integrar", mensajeIntegracion);
                    salida.DetalleEjecucion.Integracion = new ResultadoIntegracion
                    {
                        Tipologia = salida.Identificacion.Tipologia,
                        Estado = "OK",
                        Mensaje = mensajeIntegracion,
                        Timestamp = context.CurrentUtcDateTime,
                        DatosOriginales = new Dictionary<string, object>(),
                        DatosFinales = new Dictionary<string, object>(),
                        IdActivoEntrada = string.IsNullOrWhiteSpace(entrada.Trazabilidad.IdActivo)
                            ? null
                            : entrada.Trazabilidad.IdActivo.Trim(),
                        IdActivoResuelto = string.IsNullOrWhiteSpace(entrada.Trazabilidad.IdActivo)
                            ? null
                            : entrada.Trazabilidad.IdActivo.Trim(),
                        IdActivoCambiado = false
                    };
                    salida.Integridad.IdActivoEntrada = salida.DetalleEjecucion.Integracion.IdActivoEntrada;
                    salida.Integridad.IdActivo = salida.DetalleEjecucion.Integracion.IdActivoResuelto;
                    salida.Integridad.IdActivoCambiado = false;
                }

                await EjecutarSubidaGdcAsync(
                    salida.Integridad.IdActivo,
                    entrada.Instrucciones.SkipGDCUpload ?? tipologiaResuelta.SkipGDCUpload);

                var tipologiaNoClasificable = string.Equals(entrada.Instrucciones.ExpectedType, "Desconocido", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tipologiaResuelta.TechnicalKey, "Desconocido", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tipologiaResuelta.TipologiaId, "Desconocido", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tipologiaResuelta.RequestedValue, "Desconocido", StringComparison.OrdinalIgnoreCase);

                if (tipologiaNoClasificable)
                {
                    const string mensajeTipologiaNoIdentificada = "no clasificable: tipologia desconocida";

                    salida.Resultado.Estado = "NO_CLASIFICADO";
                    salida.Resultado.MensajeError = mensajeTipologiaNoIdentificada;
                    salida.Resultado.ConfianzaGlobal = 0;
                    salida.Resultado.EstadoCalidad = "ERROR";
                    salida.Resultado.ConfianzaClasificacion = 0;
                    salida.Resultado.ConfianzaExtraccion = 0;
                    salida.Resultado.ConfianzaValidacion = 0;
                    salida.DetalleEjecucion.Postproceso.Inconsistencias.Add(mensajeTipologiaNoIdentificada);
                    
                    FinalizarSeguimiento("Completed", mensajeTipologiaNoIdentificada);
                    return salida;
                }

                var confidenceCfgClassificationOnly = tipologiaResuelta.ConfidenceConfig ?? new ConfidenceConfig();
                salida.Resultado.Estado = "OK";
                salida.Resultado.ConfianzaGlobal = RedondearSalida(
                    ConfidenceCalculator.Global(resultadoClasificacion.Confianza, null, 1.0));
                salida.Resultado.EstadoCalidad = ConfidenceCalculator.EstadoCalidad(
                    salida.Resultado.ConfianzaGlobal,
                    confidenceCfgClassificationOnly);
                salida.Resultado.ConfianzaClasificacion = RedondearSalida(resultadoClasificacion.Confianza);
                salida.Resultado.ConfianzaExtraccion = 0;
                salida.Resultado.ConfianzaValidacion = 1.0;

                logger.LogInformation("Paso 8: Persistiendo resultados (classificationOnly)");
                await EjecutarPasoNegocioSinResultado(
                    "Persistir",
                    () => context.CallActivityAsync(
                        "PersistirActivity",
                        salida));

                FinalizarSeguimiento("Completed", "ClassificationOnly completado");
                return salida;
            }

            resultadoClasificacion.Confianza = RedondearSalida(resultadoClasificacion.Confianza);
            resultadoClasificacion.ConfianzaDI = RedondearSalida(resultadoClasificacion.ConfianzaDI);
            resultadoClasificacion.ConfianzaGPT = RedondearSalida(resultadoClasificacion.ConfianzaGPT);

            ExtraccionResultado resultadoExtraccion;
            if (tipologiaResuelta.ExtractionEnabled)
            {
                // 4. Extraccion
                logger.LogInformation("Paso 4: Extrayendo datos");

                // Umbral de fallback CU→GPT: instrucciones ?? tipología ?? config servidor
                var minFieldsRatioFallback = ResolveExtractionFallbackMinFieldsRatio();
                var umbralExtracFallback = entrada.Instrucciones.Extraction.Umbral
                    ?? tipologiaResuelta.ConfidenceConfig?.ExtracUmbralFallback
                    ?? minFieldsRatioFallback;

                // Capa instrucciones para extracción:
                // - Si hay umbral específico, se usa ese valor.
                // - Si falta el específico, usa instrucciones.extraction.umbral (legado de la misma capa).
                var umbralExtracCompletitudRequest = entrada.Instrucciones.Extraction.UmbralCompletitud
                    ?? entrada.Instrucciones.Extraction.Umbral;
                var umbralExtracConfianzaRequest = entrada.Instrucciones.Extraction.UmbralConfianza
                    ?? entrada.Instrucciones.Extraction.Umbral;
                // Provider y model override: instrucciones ?? null (tipología se aplica en el proveedor)
                var providerEfectivo = string.IsNullOrWhiteSpace(entrada.Instrucciones.Extraction.Provider)
                    || entrada.Instrucciones.Extraction.Provider.Equals("auto", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : entrada.Instrucciones.Extraction.Provider;

                var modelKeyEfectivo = string.IsNullOrWhiteSpace(entrada.Instrucciones.Extraction.Model)
                    || entrada.Instrucciones.Extraction.Model.Equals("auto", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : entrada.Instrucciones.Extraction.Model;

                // Paso 3.5: si el proveedor es GPT-directo y no hay markdown previo de clasificación, extraerlo con DI Layout
                var providerParaMarkdown = providerEfectivo ?? tipologiaResuelta.ExtractionProvider;
                if (IsGptDirectProvider(providerParaMarkdown) && !datosNormalizados.ContainsKey("Markdown"))
                {
                    logger.LogInformation(
                        "Paso 3.5: Extrayendo markdown DI Layout previo para provider GPT-directo. Tipología={Tipologia}",
                        salida.Identificacion.Tipologia);

                    try
                    {
                        var markdownLayout = await context.CallActivityAsync<ExtraerMarkdownLayoutResultado>(
                            "ExtraerMarkdownLayoutActivity",
                            new ExtraerMarkdownLayoutInput
                            {
                                Tipologia = salida.Identificacion.Tipologia,
                                DocumentoBase64 = docClasif.DocumentoBase64Clasif,
                                NombreDocumento = entrada.Documento.Name
                            });

                        if (!string.IsNullOrWhiteSpace(markdownLayout.Markdown))
                        {
                            datosNormalizados["Markdown"] = markdownLayout.Markdown;
                            RegistrarMarkdown(markdownLayout.Markdown, "LayoutPrevioExtraccion");
                            logger.LogInformation(
                                "Markdown DI Layout listo para extraccion GPT-directo ({Len} chars)",
                                markdownLayout.Markdown.Length);
                        }

                        if (salida.Identificacion.Paginas <= 0 && markdownLayout.Paginas > 0)
                            salida.Identificacion.Paginas = markdownLayout.Paginas;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "No se pudo extraer markdown DI Layout previo. Tipología={Tipologia}. Se continua sin markdown.",
                            salida.Identificacion.Tipologia);
                    }
                }

                resultadoExtraccion = await EjecutarPasoNegocio(
                    "Extraer",
                    () => context.CallActivityAsync<ExtraccionResultado>(
                        "ExtraerActivity",
                        new ExtraccionInput
                        {
                            Entrada = entrada,
                            Tipologia = salida.Identificacion.Tipologia,
                            DatosNormalizados = datosNormalizados,
                            UmbralFallbackEfectivo = umbralExtracFallback,
                            UmbralFallbackEfectivoCompletitud = umbralExtracCompletitudRequest,
                            UmbralFallbackEfectivoConfianza = umbralExtracConfianzaRequest,
                            ProviderEfectivo = providerEfectivo,
                            ModelKeyEfectivo = modelKeyEfectivo
                        }));

                if (resultadoExtraccion.FallbackUsado)
                {
                    var trazaExtraccion = ObtenerTraza("Extraer");
                    trazaExtraccion.FallbackActivado = true;
                    trazaExtraccion.FallbackRazon = resultadoExtraccion.FallbackRazon;
                    trazaExtraccion.Mensaje = $"Fallback extracción activado ({resultadoExtraccion.FallbackRazon ?? "sin razon informada"})";
                    PublicarEstado("Running", string.Empty, trazaExtraccion.Mensaje);
                }
            }
            else
            {
                logger.LogInformation("Paso 4: Extracción omitida para tipología {Tipologia} (Extraction.Enabled=false)", salida.Identificacion.Tipologia);
                MarcarInicioActividad("Extraer");
                MarcarFinActividad("Extraer", "Skipped", "Extracción deshabilitada en configuración de tipología");

                resultadoExtraccion = new ExtraccionResultado
                {
                    Proveedor = "none",
                    Modelo = "disabled",
                    LayoutEnabled = false,
                    DatosExtraidos = new Dictionary<string, object>()
                };
            }

            var markdownNormalizacion = resultadoExtraccion.MarkdownExtraido;
            var origenMarkdownNormalizacion = "Extraccion";
            if (string.IsNullOrWhiteSpace(markdownNormalizacion)
                && datosNormalizados.TryGetValue("Markdown", out var markdownPreexistente)
                && markdownPreexistente is string markdownExistente
                && !string.IsNullOrWhiteSpace(markdownExistente))
            {
                markdownNormalizacion = markdownExistente;
                origenMarkdownNormalizacion = "MarkdownPrevio";
            }

            if (string.IsNullOrWhiteSpace(markdownNormalizacion)
                && resultadoExtraccion.DatosExtraidos.TryGetValue("Markdown", out var markdownExtraido)
                && markdownExtraido is string markdownDetectado
                && !string.IsNullOrWhiteSpace(markdownDetectado))
            {
                markdownNormalizacion = markdownDetectado;
                origenMarkdownNormalizacion = "Extraccion";
            }

            if (!string.IsNullOrWhiteSpace(markdownNormalizacion))
            {
                datosNormalizados["Markdown"] = markdownNormalizacion;
                RegistrarMarkdown(markdownNormalizacion, origenMarkdownNormalizacion);
                logger.LogInformation(
                    "Markdown de extracción preparado para normalización y fallbacks ({Length} caracteres)",
                    markdownNormalizacion.Length);
            }
            else
            {
                try
                {
                    logger.LogInformation(
                        "No se recibió markdown del provider de extracción. Intentando fallback DI layout para tipología {Tipologia}.",
                        salida.Identificacion.Tipologia);

                    var markdownLayout = await context.CallActivityAsync<ExtraerMarkdownLayoutResultado>(
                        "ExtraerMarkdownLayoutActivity",
                        new ExtraerMarkdownLayoutInput
                        {
                            Tipologia = salida.Identificacion.Tipologia,
                            DocumentoBase64 = entrada.Documento.Content.Base64,
                            NombreDocumento = entrada.Documento.Name
                        });

                    if (!string.IsNullOrWhiteSpace(markdownLayout.Markdown))
                    {
                        markdownNormalizacion = markdownLayout.Markdown;
                        resultadoExtraccion.MarkdownExtraido = markdownLayout.Markdown;
                        datosNormalizados["Markdown"] = markdownLayout.Markdown;
                        RegistrarMarkdown(markdownLayout.Markdown, "FallbackLayout");

                        logger.LogInformation(
                            "Markdown obtenido vía fallback DI layout ({Length} caracteres)",
                            markdownLayout.Markdown.Length);
                    }

                    if (salida.Identificacion.Paginas <= 0 && markdownLayout.Paginas > 0)
                    {
                        salida.Identificacion.Paginas = markdownLayout.Paginas;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "No se pudo obtener markdown vía DI layout para tipología {Tipologia}. Se continúa sin markdown.",
                        salida.Identificacion.Tipologia);
                }
            }

            // El markdown no debe formar parte de DatosExtraidos de salida.
            resultadoExtraccion.DatosExtraidos.Remove("Markdown");
            salida.DatosExtraidos = resultadoExtraccion.DatosExtraidos;

            // Prioridad para páginas:
            // 1) valor explícito devuelto por el proveedor de extracción
            // 2) metadato presente en DatosExtraidos
            // 3) heurística previa de normalización
            if (resultadoExtraccion.Paginas > 0)
            {
                salida.Identificacion.Paginas = resultadoExtraccion.Paginas;
            }
            else if (salida.Identificacion.Paginas <= 0)
            {
                var paginasDesdeExtraccion = ObtenerEntero(salida.DatosExtraidos, "Paginas");
                if (paginasDesdeExtraccion <= 0)
                {
                    paginasDesdeExtraccion = ObtenerEntero(salida.DatosExtraidos, "NumeroPaginas");
                }
                if (paginasDesdeExtraccion <= 0)
                {
                    paginasDesdeExtraccion = ObtenerEntero(salida.DatosExtraidos, "NumPaginas");
                }
                if (paginasDesdeExtraccion <= 0)
                {
                    paginasDesdeExtraccion = ObtenerEntero(salida.DatosExtraidos, "pageCount");
                }
                if (paginasDesdeExtraccion <= 0)
                {
                    paginasDesdeExtraccion = ObtenerEntero(salida.DatosExtraidos, "pages");
                }

                salida.Identificacion.Paginas = paginasDesdeExtraccion;
            }

            salida.DetalleEjecucion.Extraccion = new ResultadoExtraccion
            {
                Modelo = resultadoExtraccion.Modelo,
                LayoutEnabled = resultadoExtraccion.LayoutEnabled,
                FallbackUsado = resultadoExtraccion.FallbackUsado,
                FallbackRazon = resultadoExtraccion.FallbackRazon,
                ConfianzaExtraccion = RedondearSalida(resultadoExtraccion.ConfianzaExtraccion),
                ProveedorExtrac = resultadoExtraccion.ProveedorExtrac,
                ConfianzaPorCampo = resultadoExtraccion.MetricasDebug?.ConfianzaPorCampo
                    .ToDictionary(kvp => kvp.Key, kvp => RedondearSalida(kvp.Value), StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                CamposConDuda = resultadoExtraccion.MetricasDebug?.CamposBajaConfianza
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    ?? new List<string>(),
                TiemposMs = resultadoExtraccion.TiemposMs
            };

            // 4.5 Prompt libre (si la tipología lo tiene habilitado, o si la petición trae instrucciones de prompt ad-hoc)
            if (tipologiaResuelta.PromptEnabled || entrada.Instrucciones.Prompt != null)
            {
                var markdownParaPrompt = resultadoExtraccion.MarkdownExtraido;
                if (string.IsNullOrWhiteSpace(markdownParaPrompt) && !string.IsNullOrWhiteSpace(markdownNormalizacion))
                {
                    markdownParaPrompt = markdownNormalizacion;
                }

                logger.LogInformation("Paso 4.5: Ejecutando prompt libre de tipología");
                var promptInput = new PromptActivityInput
                {
                    Tipologia = salida.Identificacion.Tipologia,
                    MarkdownExtraido = markdownParaPrompt,
                    DocumentoBase64 = entrada.Documento.Content.Base64,
                    DatosExtraidos = resultadoExtraccion.DatosExtraidos,
                    // Optimización: si el fallback ya ejecutó el prompt en modo combinado, no hacer otra llamada
                    ResultadoPromptCombinado = resultadoExtraccion.ResultadoPromptCombinado,
                    Prompt = entrada.Instrucciones.Prompt
                };

                var resultadoPrompt = await EjecutarPasoNegocio(
                    "Prompt",
                    () => context.CallActivityAsync<PromptResultado>("PromptActivity", promptInput));

                if (!string.IsNullOrWhiteSpace(resultadoPrompt.Resultado))
                {
                    // Exponer resultado de prompt en DatosExtraidos para validación/integración/persistencia
                    salida.DatosExtraidos["ResultadoPrompt"] = resultadoPrompt.Resultado;
                }

                salida.DetalleEjecucion.Prompt = new ResultadoPromptEjecucion
                {
                    Modelo = resultadoPrompt.Modelo,
                    TiempoMs = resultadoPrompt.TiempoMs,
                    CombinedWithFallback = resultadoPrompt.CombinedWithFallback,
                    Error = resultadoPrompt.Error
                };
                RegistrarModeloLlm(resultadoPrompt.Modelo);

                if (resultadoPrompt.CombinedWithFallback)
                {
                    var trazaPrompt = ObtenerTraza("Prompt");
                    trazaPrompt.Mensaje = "Resultado reutilizado de llamada combinada con fallback de extracción";
                    PublicarEstado("Running", string.Empty, trazaPrompt.Mensaje);
                }
            }

            // 5. Validacion - ACTUALIZADO PARA USAR MOTOR DE VALIDACION
            logger.LogInformation("Paso 5: Validando datos extraidos con motor de reglas");
            var validacionInput = new ValidacionInput
            {
                Tipologia = salida.Identificacion.Tipologia,
                DatosExtraidos = salida.DatosExtraidos.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object?)kvp.Value)
            };

            var resultadoValidacion = await EjecutarPasoNegocio(
                "Validar",
                () => context.CallActivityAsync<DetalleValidacion>(
                    "ValidarActivity",
                    validacionInput));

            // Convertir DetalleValidacion a InformacionPostproceso
            salida.DetalleEjecucion.Postproceso = new InformacionPostproceso
            {
                Normalizaciones = new List<string> 
                { 
                    $"Aplicadas {resultadoValidacion.ReglasAplicadas} reglas de validacion"
                },
                Markdown = string.IsNullOrWhiteSpace(markdownNormalizacion) ? null : markdownNormalizacion,
                Validaciones = resultadoValidacion.Validaciones
                    .Where(v => v.Severidad == "Warning" || v.Severidad == "Info")
                    .Select(v => $"[{v.Severidad}] {v.Campo}: {v.Mensaje}")
                    .ToList(),
                Inconsistencias = resultadoValidacion.Validaciones
                    .Where(v => v.Severidad == "Error")
                    .Select(v => $"[ERROR] {v.Campo}: {v.Mensaje}")
                    .ToList()
            };

            var confianzaValidacionRedondeada = RedondearSalida(resultadoValidacion.ConfianzaValidacion);
            salida.DetalleEjecucion.Postproceso.ConfianzaValidacion = confianzaValidacionRedondeada;

            // Agregar metadata de validacion
            salida.DetalleEjecucion.Postproceso.Normalizaciones.Add(
                $"Confianza de validacion: {confianzaValidacionRedondeada:P0}");

            if (!string.IsNullOrWhiteSpace(salida.DetalleEjecucion.Postproceso.Markdown))
            {
                salida.DetalleEjecucion.Postproceso.Normalizaciones.Add("Markdown");
            }
            
            if (resultadoValidacion.Errores > 0)
            {
                // Resumen agregado para facilitar consulta rápida en auditoría/persistencia
                salida.DetalleEjecucion.Postproceso.Inconsistencias.Add(
                    $"Total de errores de validacion: {resultadoValidacion.Errores}");
            }

            // Si hay errores de validacion, registrar pero continuar procesando
            bool conErroresValidacion = false;
            if (resultadoValidacion.Errores > 0)
            {
                conErroresValidacion = true;
                logger.LogWarning($"Documento tiene {resultadoValidacion.Errores} errores de validacion, continuando con procesamiento");
            }

            // Paso 5.5: ObtenerActivo (opcional — controlado por Instrucciones/Tipología)
            var assetResolverEnabled = entrada.Instrucciones.AssetResolver?.Enabled
                ?? tipologiaResuelta.AssetResolverEnabled;

            string? idActivoDesdeAssetResolver = null;

            if (assetResolverEnabled)
            {
                logger.LogInformation("Paso 5.5: Obteniendo activo desde AssetResolver");

                var obtenerActivoInput = BuildObtenerActivoInput(entrada, salida, tipologiaResuelta);

                var resultadoAssetResolver = await EjecutarPasoNegocio(
                    "ObtenerActivo",
                    () => context.CallActivityAsync<ResultadoAssetResolver>(
                        nameof(ObtenerActivoActivity),
                        obtenerActivoInput));

                salida.DetalleEjecucion.AssetResolver = resultadoAssetResolver;

                // Resolución de IdActivo: solo si un único activo encontrado
                if (resultadoAssetResolver.Exitoso && resultadoAssetResolver.Count == 1)
                {
                    idActivoDesdeAssetResolver = resultadoAssetResolver.Activos[0].IdActivo;
                    logger.LogInformation(
                        "AssetResolver resolvió un único activo: IdActivo={IdActivo}",
                        idActivoDesdeAssetResolver);
                }
                else if (resultadoAssetResolver.Count > 1)
                {
                    logger.LogWarning(
                        "AssetResolver encontró {Count} activos. No se sobreescribe IdActivo.",
                        resultadoAssetResolver.Count);
                }
            }
            else
            {
                MarcarInicioActividad("ObtenerActivo");
                MarcarFinActividad("ObtenerActivo", "Skipped", "AssetResolver no habilitado");
            }

            // Paso 6: Integrar con sistemas externos (ENRIQUECIMIENTO)
            logger.LogInformation("Paso 6: Integrando con sistemas externos");
            var integrarInput = new DocumentIA.Core.Models.IntegrarInput
            {
                Tipologia = salida.Identificacion.Tipologia,
                DocumentoId = salida.Identificacion.Guid,
                DatosExtraidos = salida.DatosExtraidos, // Pasar datos extraidos
                IdActivo = idActivoDesdeAssetResolver ?? entrada.Trazabilidad.IdActivo, // AssetResolver > Entrada
                Metadata = new Dictionary<string, object>
                {
                    ["correlationId"] = entrada.Trazabilidad.CorrelationId,
                    ["submittedBy"] = entrada.Trazabilidad.SubmittedBy
                }
            };

            var resultadoIntegracion = await EjecutarPasoNegocio(
                "Integrar",
                () => context.CallActivityAsync<DocumentIA.Core.Models.ResultadoIntegracion>(
                    "IntegrarActivity",
                    integrarInput));

            salida.DetalleEjecucion.Integracion = resultadoIntegracion;

            // IMPORTANTE: Reemplazar datos extraidos con datos enriquecidos
            if (resultadoIntegracion.Estado == "OK" && resultadoIntegracion.DatosFinales.Count > 0)
            {
                salida.DatosExtraidos = resultadoIntegracion.DatosFinales; // Usar datos enriquecidos
                logger.LogInformation("Datos enriquecidos: {Count} campos totales", resultadoIntegracion.DatosFinales.Count);
            }

            // Resolver IdActivo: primero lo devuelto por plugins, luego el original de entrada
            salida.Integridad.IdActivoEntrada = resultadoIntegracion.IdActivoEntrada;
            salida.Integridad.IdActivo = resultadoIntegracion.IdActivoResuelto
                ?? entrada.Trazabilidad.IdActivo;
            salida.Integridad.IdActivoCambiado = resultadoIntegracion.IdActivoCambiado;

            if (salida.Integridad.IdActivoCambiado)
            {
                logger.LogWarning(
                    "Se detectó cambio de IdActivo entre entrada y salida. Entrada={IdActivoEntrada}, Salida={IdActivo}",
                    salida.Integridad.IdActivoEntrada,
                    salida.Integridad.IdActivo);
            }

            if (!string.IsNullOrWhiteSpace(salida.Integridad.IdActivo))
                logger.LogInformation("IdActivo resuelto para GDC: {IdActivo}", salida.Integridad.IdActivo);
            else
                logger.LogWarning("IdActivo no disponible tras integración. La subida a GDC será omitida si aplica.");

            // 7. Subida a GDC (opcional)
            // Prioridad: Instrucciones.SkipGDCUpload (si viene informado) > tipologiaResuelta.SkipGDCUpload (config de tipología)
            await EjecutarSubidaGdcAsync(
                salida.Integridad.IdActivo,
                entrada.Instrucciones.SkipGDCUpload ?? tipologiaResuelta.SkipGDCUpload);

            // Resultado final
            if (conErroresValidacion)
            {
                salida.Resultado.Estado = "VALIDACION_CON_ERRORES";
                logger.LogWarning("Procesamiento completado con errores de validacion");
            }
            else
            {
                salida.Resultado.Estado = "OK";
                logger.LogInformation($"Procesamiento completado exitosamente para {entrada.Documento.Name}");
            }

            // Confianza global = MIN(Clasif, Extrac, Valid)
            var confClasif = resultadoClasificacion.Confianza;
            var confExtrac = tipologiaResuelta.ExtractionEnabled
                ? resultadoExtraccion.ConfianzaExtraccion
                : (double?)null;
            var confValid = resultadoValidacion.ConfianzaValidacion;
            var confidenceCfg = tipologiaResuelta.ConfidenceConfig ?? new ConfidenceConfig();

            salida.Resultado.ConfianzaGlobal = RedondearSalida(ConfidenceCalculator.Global(confClasif, confExtrac, confValid));
            salida.Resultado.EstadoCalidad = ConfidenceCalculator.EstadoCalidad(salida.Resultado.ConfianzaGlobal, confidenceCfg);
            salida.Resultado.ConfianzaClasificacion = RedondearSalida(confClasif);
            salida.Resultado.ConfianzaExtraccion = RedondearSalida(confExtrac ?? 0.0);
            salida.Resultado.ConfianzaValidacion = confianzaValidacionRedondeada;
            salida.DetalleEjecucion.Postproceso.ConfianzaValidacion = confianzaValidacionRedondeada;

            logger.LogInformation(
                "ConfGlobal={Global:F3} \u2192 {EstadoCalidad} (Clasif:{Clasif:F3}, Extrac:{Extrac:F3}, Valid:{Valid:F3})",
                salida.Resultado.ConfianzaGlobal,
                salida.Resultado.EstadoCalidad,
                confClasif,
                confExtrac ?? 0.0,
                confValid);

            // 8. Persistencia
            logger.LogInformation("Paso 8: Persistiendo resultados");
            await EjecutarPasoNegocioSinResultado(
                "Persistir",
                () => context.CallActivityAsync(
                    "PersistirActivity",
                    salida));

            FinalizarSeguimiento("Completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error durante el procesamiento");
            salida.Resultado.Estado = "ERROR";
            salida.Resultado.MensajeError = ex.Message;
            salida.DetalleEjecucion.Postproceso.Inconsistencias.Add($"Error: {ex.Message}");
            FinalizarSeguimiento("Failed", ex.Message);
        }

        return salida;
    }

    private static int ObtenerEntero(IDictionary<string, object> values, string key)
    {
        if (!values.TryGetValue(key, out var raw) || raw == null)
        {
            return 0;
        }

        return raw switch
        {
            int i => i,
            long l when l <= int.MaxValue && l >= int.MinValue => (int)l,
            decimal d => (int)d,
            double db => (int)db,
            float f => (int)f,
            string s when int.TryParse(s, out var parsed) => parsed,
            JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var parsedInt) => parsedInt,
            JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetInt64(out var parsedLong) && parsedLong <= int.MaxValue && parsedLong >= int.MinValue => (int)parsedLong,
            JsonElement json when json.ValueKind == JsonValueKind.String && int.TryParse(json.GetString(), out var parsedString) => parsedString,
            _ => 0
        };
    }

    private static double RedondearSalida(double value)
    {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private double ResolveExtractionFallbackMinFieldsRatio()
    {
        try
        {
            return _extractionModelRegistryLoader.GetFallbackModel().MinFieldsRatio;
        }
        catch (KeyNotFoundException)
        {
            return 0.5;
        }
    }

    private int ResolveMaxPaginasClasificacion(string? tipologia)
    {
        var defaultPages = Math.Max(1, _classificationPreparationSettings.MaxPaginasClasificacionDefault);

        if (!string.IsNullOrWhiteSpace(tipologia) &&
            _classificationPreparationSettings.OverridesPorTipologia.TryGetValue(tipologia, out var perTipologia) &&
            perTipologia > 0)
        {
            return perTipologia;
        }

        var familia = !string.IsNullOrWhiteSpace(tipologia)
            ? tipologia.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : null;

        if (!string.IsNullOrWhiteSpace(familia) &&
            _classificationPreparationSettings.OverridesPorFamilia.TryGetValue(familia, out var perFamilia) &&
            perFamilia > 0)
        {
            return perFamilia;
        }

        return defaultPages;
    }

    private static bool IsGptDirectProvider(string? provider) =>
        !string.IsNullOrWhiteSpace(provider) &&
        (provider.Equals("azure-openai", StringComparison.OrdinalIgnoreCase) ||
         provider.Equals("openai", StringComparison.OrdinalIgnoreCase) ||
         provider.Equals("gpt", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Devuelve true para los providers de clasificación que generan su propio markdown
    /// durante la ejecución (DI y CU). Estos no necesitan que el orquestador pre-extraiga
    /// markdown vía DI Layout antes del paso de clasificación.
    /// </summary>
    private static bool ClasificacionProviderGeneraMarkdownPropio(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return false;
        return provider.ToLowerInvariant() switch
        {
            "di" or "azure-di" or "azure-document-intelligence" => true,
            "cu" or "azure-content-understanding" or "content-understanding" => true,
            _ => false
        };
    }
}
