using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Activities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentIA.Functions.Orchestrators;

public class DocumentProcessOrchestrator
{
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
        var actividadesNegocio = new[] { "Clasificar", "Extraer", "Validar", "Integrar", "SubirGDC", "Persistir" };

        var seguimiento = salida.DetalleEjecucion.Seguimiento;
        seguimiento.Estado = "Pending";
        seguimiento.ActividadActual = string.Empty;
        seguimiento.ActividadesTotales = actividadesNegocio.Length;
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

        PublicarEstado("Running", string.Empty);

        try
        {
            // 1. Normalizacion y calculo de hashes
            logger.LogInformation("Paso 1: Normalizando documento");
            var datosNormalizados = await context.CallActivityAsync<Dictionary<string, object>>(
                "NormalizarActivity",
                entrada);

            salida.Integridad.SHA256 = datosNormalizados["SHA256"].ToString() ?? "";
            salida.Integridad.MD5 = datosNormalizados["MD5"].ToString() ?? "";
            salida.Integridad.CRC32 = datosNormalizados["CRC32"].ToString() ?? "";
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
                        salida.Integridad.SHA256);

                    if (salidaDuplicado is not null)
                    {
                        salidaDuplicado.Resultado.ReutilizadaPorDuplicado = true;
                        salidaDuplicado.Resultado.MensajeReutilizacion = "Documento ya procesado previamente. Se reutiliza la última ejecución.";

                        FinalizarSeguimiento("Completed", "Documento duplicado detectado. Devolviendo última ejecución");
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
                    Contenedor = "documents"
                });

            if (!string.IsNullOrWhiteSpace(blobPath))
            {
                salida.Integridad.RutaBlobStorage = blobPath;
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

                try
                {
                    resultadoClasificacion = await context.CallActivityAsync<ResultadoClasificacion>(
                        "ClasificarActivity",
                        new ClasificacionInput
                        {
                            Entrada = entrada,
                            DatosNormalizados = datosNormalizados
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
                    throw;
                }
            }

            salida.DetalleEjecucion.Clasificacion = resultadoClasificacion;
            var tipologiaEntrada = resultadoClasificacion.TipologiaDetectada ?? "Desconocida";
            ResolvedTipologia tipologiaResuelta;

            try
            {
                tipologiaResuelta = await context.CallActivityAsync<ResolvedTipologia>(
                    "ResolverTipologiaActivity",
                    tipologiaEntrada);
            }
            catch (Exception ex) when (ex is KeyNotFoundException || ex.InnerException is KeyNotFoundException)
            {
                const string mensajeTipologiaNoIdentificada = "No se ha podido identificar la tipologia del documento";

                logger.LogWarning(
                    ex,
                    "La clasificación devolvió una tipología no resoluble: {TipologiaDetectada}",
                    tipologiaEntrada);

                salida.DetalleEjecucion.RunTipologia = tipologiaEntrada;
                salida.Resultado.Estado = "ERROR";
                salida.DetalleEjecucion.Postproceso.Inconsistencias.Add($"Error: {mensajeTipologiaNoIdentificada}");

                FinalizarSeguimiento("Failed", mensajeTipologiaNoIdentificada);
                return salida;
            }

            salida.Identificacion.Tipologia = tipologiaResuelta.TechnicalKey;
            salida.Identificacion.TipologiaFamilia = tipologiaResuelta.TipologiaId;
            salida.Identificacion.TipologiaVersion = tipologiaResuelta.Version;
            salida.DetalleEjecucion.RunTipologia = tipologiaResuelta.TechnicalKey;

            // Añadir actividad Prompt al seguimiento si la tipología la tiene habilitada
            if (tipologiaResuelta.PromptEnabled)
            {
                seguimiento.ActividadesTotales++;
                seguimiento.Actividades.Add(new TrazaActividad { Nombre = "Prompt", Estado = "Pending" });
            }

            // Verificar umbral de confianza
            if (resultadoClasificacion.Confianza < entrada.Instrucciones.Classification.Umbral)
            {
                salida.Resultado.Estado = "BAJA_CONFIANZA_CLASIFICACION";
                logger.LogWarning($"Confianza de clasificacion baja: {resultadoClasificacion.Confianza}");

                FinalizarSeguimiento("Completed", "Clasificación por debajo de umbral");
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
                resultadoExtraccion = await EjecutarPasoNegocio(
                    "Extraer",
                    () => context.CallActivityAsync<ExtraccionResultado>(
                        "ExtraerActivity",
                        new ExtraccionInput
                        {
                            Entrada = entrada,
                            Tipologia = salida.Identificacion.Tipologia,
                            DatosNormalizados = datosNormalizados
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

            salida.DatosExtraidos = resultadoExtraccion.DatosExtraidos;

            // Propagar markdown extraído a datosNormalizados para fallbacks
            if (resultadoExtraccion.DatosExtraidos.TryGetValue("Markdown", out var markdownExtraido) 
                && markdownExtraido is string markdown 
                && !string.IsNullOrWhiteSpace(markdown))
            {
                datosNormalizados["Markdown"] = markdown;
                logger.LogInformation("Markdown de extracción propagado a datosNormalizados ({Length} caracteres)", markdown.Length);
            }

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
                TiemposMs = resultadoExtraccion.TiemposMs
            };

            // 4.5 Prompt libre (si la tipología lo tiene habilitado)
            if (tipologiaResuelta.PromptEnabled)
            {
                var markdownParaPrompt = resultadoExtraccion.MarkdownExtraido;
                if (string.IsNullOrWhiteSpace(markdownParaPrompt)
                    && resultadoExtraccion.DatosExtraidos.TryGetValue("Markdown", out var markdownDesdeDatos)
                    && markdownDesdeDatos is string markdownDetectado
                    && !string.IsNullOrWhiteSpace(markdownDetectado))
                {
                    markdownParaPrompt = markdownDetectado;
                }

                if (!tipologiaResuelta.ExtractionEnabled && string.IsNullOrWhiteSpace(markdownParaPrompt))
                {
                    logger.LogInformation(
                        "Paso 4.4: Tipología {Tipologia} sin extracción. Obteniendo markdown con DI layout antes del prompt.",
                        salida.Identificacion.Tipologia);

                    try
                    {
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
                            markdownParaPrompt = markdownLayout.Markdown;
                            resultadoExtraccion.MarkdownExtraido = markdownLayout.Markdown;
                            resultadoExtraccion.DatosExtraidos["Markdown"] = markdownLayout.Markdown;
                            datosNormalizados["Markdown"] = markdownLayout.Markdown;

                            logger.LogInformation(
                                "Markdown DI layout preparado para prompt ({Length} caracteres)",
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
                            "No se pudo extraer markdown DI layout para tipología {Tipologia}. Se continúa con prompt sin markdown.",
                            salida.Identificacion.Tipologia);
                    }
                }

                logger.LogInformation("Paso 4.5: Ejecutando prompt libre de tipología");
                var promptInput = new PromptActivityInput
                {
                    Tipologia = salida.Identificacion.Tipologia,
                    MarkdownExtraido = markdownParaPrompt,
                    DocumentoBase64 = entrada.Documento.Content.Base64,
                    DatosExtraidos = resultadoExtraccion.DatosExtraidos,
                    // Optimización: si el fallback ya ejecutó el prompt en modo combinado, no hacer otra llamada
                    ResultadoPromptCombinado = resultadoExtraccion.ResultadoPromptCombinado
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
                Validaciones = resultadoValidacion.Validaciones
                    .Where(v => v.Severidad == "Warning" || v.Severidad == "Info")
                    .Select(v => $"[{v.Severidad}] {v.Campo}: {v.Mensaje}")
                    .ToList(),
                Inconsistencias = resultadoValidacion.Validaciones
                    .Where(v => v.Severidad == "Error")
                    .Select(v => $"[ERROR] {v.Campo}: {v.Mensaje}")
                    .ToList()
            };

            // Agregar metadata de validacion
            salida.DetalleEjecucion.Postproceso.Normalizaciones.Add(
                $"Confianza de validacion: {resultadoValidacion.ConfianzaValidacion:P0}");
            
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

            // Paso 6: Integrar con sistemas externos (ENRIQUECIMIENTO)
            logger.LogInformation("Paso 6: Integrando con sistemas externos");
            var integrarInput = new DocumentIA.Core.Models.IntegrarInput
            {
                Tipologia = salida.Identificacion.Tipologia,
                DocumentoId = salida.Identificacion.Guid,
                DatosExtraidos = salida.DatosExtraidos, // Pasar datos extraidos
                IdActivo = entrada.Trazabilidad.IdActivo, // Puede venir vacío; un plugin puede resolverlo
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
            var skipGDC = entrada.Instrucciones.SkipGDCUpload ?? tipologiaResuelta.SkipGDCUpload;
            if (!skipGDC)
            {
                if (!string.IsNullOrWhiteSpace(salida.Integridad.IdActivo))
                {
                    MarcarInicioActividad("SubirGDC");
                    logger.LogInformation("Paso 7: Intentando subir a GDC IdActivo={IdActivo}", salida.Integridad.IdActivo);

                    var subirInput = new SubirGDCActivity.SubirGDCActivityInput
                    {
                        Tipologia = salida.Identificacion.Tipologia,
                        Input = new SubirGDCInput
                        {
                            IdActivo = salida.Integridad.IdActivo ?? string.Empty,
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
                            GdcActivityTimeoutSeconds, salida.Integridad.IdActivo);
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
                    MarcarInicioActividad("SubirGDC");
                    MarcarFinActividad("SubirGDC", "Skipped", "IdActivo no disponible");
                }
            }
            else
            {
                logger.LogInformation(
                    "SkipGDCUpload activo (fuente={Fuente}); se omite subida a GDC",
                    entrada.Instrucciones.SkipGDCUpload.HasValue ? "instrucciones" : "config-tipologia");
                MarcarInicioActividad("SubirGDC");
                MarcarFinActividad("SubirGDC", "Skipped", "SkipGDCUpload activo");
            }

            // 8. Persistencia
            logger.LogInformation("Paso 8: Persistiendo resultados");
            await EjecutarPasoNegocioSinResultado(
                "Persistir",
                () => context.CallActivityAsync(
                    "PersistirActivity",
                    salida));

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
            salida.Resultado.ConfianzaValidacion = RedondearSalida(confValid);
            salida.DetalleEjecucion.Postproceso.ConfianzaValidacion = RedondearSalida(confValid);

            logger.LogInformation(
                "ConfGlobal={Global:F3} \u2192 {EstadoCalidad} (Clasif:{Clasif:F3}, Extrac:{Extrac:F3}, Valid:{Valid:F3})",
                salida.Resultado.ConfianzaGlobal,
                salida.Resultado.EstadoCalidad,
                confClasif,
                confExtrac ?? 0.0,
                confValid);

            FinalizarSeguimiento("Completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error durante el procesamiento");
            salida.Resultado.Estado = "ERROR";
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
}
