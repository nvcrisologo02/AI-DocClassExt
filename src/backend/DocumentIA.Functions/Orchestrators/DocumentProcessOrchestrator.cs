using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;

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

        try
        {
            // 1. Normalización y cálculo de hashes
            logger.LogInformation("Paso 1: Normalizando documento");
            var datosNormalizados = await context.CallActivityAsync<Dictionary<string, object>>(
                "NormalizarActivity",
                entrada);

            salida.Integridad.SHA256 = datosNormalizados["SHA256"].ToString() ?? "";
            salida.Integridad.CRC32 = datosNormalizados["CRC32"].ToString() ?? "";

            // 2. Verificar duplicados (si está habilitado)
            if (!entrada.Instrucciones.SkipDuplicateCheck)
            {
                logger.LogInformation("Paso 2: Verificando duplicados");
                var esDuplicado = await context.CallActivityAsync<bool>(
                    "VerificarDuplicadoActivity",
                    salida.Integridad.SHA256);

                if (esDuplicado && !entrada.Instrucciones.ForceReprocess)
                {
                    salida.Resultado.Estado = "DUPLICADO";
                    logger.LogWarning("Documento duplicado detectado");
                    return salida;
                }
            }

            // 3. Clasificación
            logger.LogInformation("Paso 3: Clasificando documento");
            var resultadoClasificacion = await context.CallActivityAsync<ResultadoClasificacion>(
                "ClasificarActivity",
                new { Entrada = entrada, DatosNormalizados = datosNormalizados });

            salida.DetalleEjecucion.Clasificacion = resultadoClasificacion;
            salida.Identificacion.Tipologia = resultadoClasificacion.TipologiaDetectada ?? "Desconocida";

            // Verificar umbral de confianza
            if (resultadoClasificacion.Confianza < entrada.Instrucciones.Classification.Umbral)
            {
                salida.Resultado.Estado = "BAJA_CONFIANZA_CLASIFICACION";
                logger.LogWarning($"Confianza de clasificación baja: {resultadoClasificacion.Confianza}");
                return salida;
            }

            // 4. Extracción
            logger.LogInformation("Paso 4: Extrayendo datos");
            var resultadoExtraccion = await context.CallActivityAsync<Dictionary<string, object>>(
                "ExtraerActivity",
                new { Entrada = entrada, Tipologia = salida.Identificacion.Tipologia, DatosNormalizados = datosNormalizados });

            salida.DatosExtraidos = resultadoExtraccion;

            // 5. Validación
            logger.LogInformation("Paso 5: Validando datos extraídos");
            var resultadoValidacion = await context.CallActivityAsync<InformacionPostproceso>(
                "ValidarActivity",
                new { Tipologia = salida.Identificacion.Tipologia, Datos = resultadoExtraccion });

            salida.DetalleEjecucion.Postproceso = resultadoValidacion;

            // 6. Integración (opcional)
            logger.LogInformation("Paso 6: Integrando con sistemas externos");
            var resultadoIntegracion = await context.CallActivityAsync<ResultadoIntegracion>(
                "IntegrarActivity",
                new { Tipologia = salida.Identificacion.Tipologia, Datos = resultadoExtraccion });

            salida.DetalleEjecucion.Integracion = resultadoIntegracion;

            // 7. Persistencia
            logger.LogInformation("Paso 7: Persistiendo resultados");
            await context.CallActivityAsync(
                "PersistirActivity",
                salida);

            // Resultado final
            salida.Resultado.Estado = "OK";
            salida.Resultado.ConfianzaGlobal = resultadoClasificacion.Confianza;

            logger.LogInformation($"Procesamiento completado exitosamente para {entrada.Documento.Name}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error durante el procesamiento");
            salida.Resultado.Estado = "ERROR";
            salida.DetalleEjecucion.Postproceso.Inconsistencias.Add($"Error: {ex.Message}");
        }

        return salida;
    }
}
