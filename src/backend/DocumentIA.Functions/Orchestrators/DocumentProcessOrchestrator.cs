using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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

        try
        {
            // 1. Normalizacion y calculo de hashes
            logger.LogInformation("Paso 1: Normalizando documento");
            var datosNormalizados = await context.CallActivityAsync<Dictionary<string, object>>(
                "NormalizarActivity",
                entrada);

            salida.Integridad.SHA256 = datosNormalizados["SHA256"].ToString() ?? "";
            salida.Integridad.CRC32 = datosNormalizados["CRC32"].ToString() ?? "";

            // 2. Verificar duplicados (si esta habilitado)
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

            // 3. Clasificacion
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
                logger.LogWarning($"Confianza de clasificacion baja: {resultadoClasificacion.Confianza}");
                return salida;
            }

            // 4. Extraccion
            logger.LogInformation("Paso 4: Extrayendo datos");
            var resultadoExtraccion = await context.CallActivityAsync<Dictionary<string, object>>(
                "ExtraerActivity",
                new { Entrada = entrada, Tipologia = salida.Identificacion.Tipologia, DatosNormalizados = datosNormalizados });

            salida.DatosExtraidos = resultadoExtraccion;

            // 5. Validacion - ACTUALIZADO PARA USAR MOTOR DE VALIDACION
            logger.LogInformation("Paso 5: Validando datos extraidos con motor de reglas");
            var validacionInput = new ValidacionInput
            {
                Tipologia = salida.Identificacion.Tipologia,
                DatosExtraidos = resultadoExtraccion.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object?)kvp.Value)
            };

            var resultadoValidacion = await context.CallActivityAsync<DetalleValidacion>(
                "ValidarActivity",
                validacionInput);

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
                salida.DetalleEjecucion.Postproceso.Inconsistencias.Add(
                    $"Total de errores de validacion: {resultadoValidacion.Errores}");
            }

            // Si hay errores criticos de validacion, marcar como ERROR
            if (resultadoValidacion.Errores > 0)
            {
                logger.LogWarning($"Documento tiene {resultadoValidacion.Errores} errores de validacion");
                salida.Resultado.Estado = "ERROR_VALIDACION";
                salida.Resultado.ConfianzaGlobal = resultadoValidacion.ConfianzaValidacion;
                return salida;
            }

            // 6. Integracion (opcional)
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
            salida.Resultado.ConfianzaGlobal = Math.Min(
                resultadoClasificacion.Confianza, 
                resultadoValidacion.ConfianzaValidacion);

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
