using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentIA.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Plugins.Integration;

namespace DocumentIA.Functions.Activities
{
    public class IntegrarActivity
    {
        private readonly ILogger<IntegrarActivity> logger;
        private readonly PluginManager pluginManager;
        private readonly PluginConfigLoader configLoader;
        private readonly PluginFactory pluginFactory;

        public IntegrarActivity(
            ILogger<IntegrarActivity> logger,
            PluginManager pluginManager,
            PluginConfigLoader configLoader,
            PluginFactory pluginFactory)
        {
            this.logger = logger;
            this.pluginManager = pluginManager;
            this.configLoader = configLoader;
            this.pluginFactory = pluginFactory;
        }

        [Function(nameof(IntegrarActivity))]
        public async Task<ResultadoIntegracion> Run([ActivityTrigger] IntegrarInput input)
        {
            logger.LogInformation("Iniciando integracion para tipologia: {Tipologia}", input.Tipologia);

            var resultado = new ResultadoIntegracion
            {
                Tipologia = input.Tipologia,
                Timestamp = DateTime.UtcNow,
                DatosOriginales = new Dictionary<string, object>(input.DatosExtraidos),
                DatosFinales = new Dictionary<string, object>(input.DatosExtraidos), // Copia inicial
                IdActivoEntrada = string.IsNullOrWhiteSpace(input.IdActivo) ? null : input.IdActivo.Trim()
            };

            try
            {
                var pluginConfig = await configLoader.LoadConfigAsync(input.Tipologia);

                if (pluginConfig.Plugins.Count == 0)
                {
                    logger.LogWarning("No hay plugins configurados para tipologia {Tipologia}", input.Tipologia);
                    resultado.Estado = "OK";
                    resultado.Mensaje = "No hay integraciones configuradas";
                    return resultado;
                }

                var pluginsOrdenados = pluginConfig.Plugins
                    .Where(p => p.Enabled)
                    .OrderBy(p => p.Priority)
                    .ToList();

                logger.LogInformation("Ejecutando {Count} plugins en orden de prioridad", pluginsOrdenados.Count);

                // Ejecutar cada plugin secuencialmente
                foreach (var pluginConf in pluginsOrdenados)
                {
                    var pluginResult = await ExecutePluginWithEnrichmentAsync(
                        pluginConf, 
                        input, 
                        resultado.DatosFinales); // Pasar datos acumulados

                    resultado.Plugins.Add(pluginResult);

                    // Si el plugin devolvió datos enriquecidos, hacer MERGE
                    if (pluginResult.Success && pluginResult.DatosEnriquecidos != null)
                    {
                        MergeDatos(resultado.DatosFinales, pluginResult.DatosEnriquecidos);
                        
                        logger.LogInformation(
                            "Plugin {PluginKey} enriqueció datos. Total campos ahora: {Count}", 
                            pluginConf.PluginKey, 
                            resultado.DatosFinales.Count);
                    }

                    // Detener si plugin critico falla
                    if (!pluginResult.Success && pluginConf.Priority == 1)
                    {
                        logger.LogError("Plugin critico {PluginKey} fallo. Deteniendo cadena", pluginConf.PluginKey);
                        resultado.Estado = "ERROR";
                        resultado.Mensaje = $"Plugin critico {pluginConf.PluginKey} fallo";
                        return resultado;
                    }
                }

                // Determinar estado final
                bool todosExitosos = resultado.Plugins.All(p => p.Success);
                bool algunoFallo = resultado.Plugins.Any(p => !p.Success);

                if (todosExitosos)
                {
                    resultado.Estado = "OK";
                    resultado.Mensaje = $"Integraciones completadas. Datos finales: {resultado.DatosFinales.Count} campos";
                }
                else if (algunoFallo)
                {
                    resultado.Estado = "REVISION";
                    resultado.Mensaje = "Algunas integraciones fallaron pero el proceso continuo";
                }

                logger.LogInformation("Integracion completada. Estado: {Estado}", resultado.Estado);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en proceso de integracion");
                resultado.Estado = "ERROR";
                resultado.Mensaje = $"Error: {ex.Message}";
            }

            // Resolver IdActivo: extraer de DatosFinales (case-insensitive) si algún plugin lo devolvió,
            // o mantener el que vino en la entrada si ya estaba informado
            if (TryObtenerIdActivo(resultado.DatosFinales, out var idActivoEnriquecido))
            {
                resultado.IdActivoResuelto = idActivoEnriquecido;
                logger.LogInformation("IdActivo resuelto por plugin: {IdActivo}", idActivoEnriquecido);
            }

            if (string.IsNullOrWhiteSpace(resultado.IdActivoResuelto) && !string.IsNullOrWhiteSpace(input.IdActivo))
            {
                resultado.IdActivoResuelto = input.IdActivo.Trim();
                logger.LogInformation("IdActivo mantenido de entrada: {IdActivo}", resultado.IdActivoResuelto);
            }

            resultado.IdActivoCambiado =
                !string.IsNullOrWhiteSpace(resultado.IdActivoEntrada) &&
                !string.IsNullOrWhiteSpace(resultado.IdActivoResuelto) &&
                !string.Equals(resultado.IdActivoEntrada, resultado.IdActivoResuelto, StringComparison.OrdinalIgnoreCase);

            if (resultado.IdActivoCambiado)
            {
                logger.LogWarning(
                    "IdActivo cambiado durante la integración. Entrada={IdActivoEntrada}, Resuelto={IdActivoResuelto}",
                    resultado.IdActivoEntrada,
                    resultado.IdActivoResuelto);
            }

            if (string.IsNullOrWhiteSpace(resultado.IdActivoResuelto))
                logger.LogWarning("IdActivo no disponible tras integración para tipologia {Tipologia}", input.Tipologia);

            return resultado;
        }

        private async Task<PluginExecutionResult> ExecutePluginWithEnrichmentAsync(
            PluginConfig pluginConfig,
            IntegrarInput input,
            Dictionary<string, object> datosActuales)
        {
            var result = new PluginExecutionResult
            {
                PluginKey = pluginConfig.PluginKey,
                Priority = pluginConfig.Priority
            };

            try
            {
                // Obtener o crear plugin
                var plugin = pluginManager.GetPlugin(pluginConfig.PluginKey);
                if (plugin == null)
                {
                    logger.LogInformation("Creando plugin {PluginKey}...", pluginConfig.PluginKey);
                    plugin = await pluginFactory.CreatePluginAsync(pluginConfig);
                    pluginManager.RegisterPlugin(pluginConfig.PluginKey, plugin);
                }

                // Preparar payload: enviar TODOS los datos actuales
                // idActivo se incluye siempre (aunque sea vacío) para que el plugin pueda recibirlo,
                // enriquecerlo o retornarlo cumplimentado en la respuesta
                var idActivoActual = TryObtenerIdActivo(datosActuales, out var idActivoEnriquecido)
                    ? idActivoEnriquecido
                    : (input.IdActivo ?? string.Empty);

                var payload = new Dictionary<string, object>
                {
                    ["tipologia"] = input.Tipologia,
                    ["documentoId"] = input.DocumentoId,
                    ["datosExtraidos"] = datosActuales, // Datos acumulados hasta ahora
                    ["idActivo"] = idActivoActual,
                    ["metadata"] = input.Metadata
                };

                // Ejecutar plugin
                var integrationResult = await pluginManager.ExecutePluginAsync(pluginConfig.PluginKey, payload);

                result.Success = integrationResult.Success;
                result.Mensaje = integrationResult.Message;
                result.StatusCode = integrationResult.StatusCode;
                result.DurationMs = (int)integrationResult.Duration.TotalMilliseconds;

                // Si el endpoint devolvió datos en ResponseData, usarlos como enriquecidos
                if (integrationResult.Success && integrationResult.ResponseData != null && integrationResult.ResponseData.Count > 0)
                {
                    result.DatosEnriquecidos = integrationResult.ResponseData;
                    logger.LogInformation(
                        "Plugin {PluginKey} devolvió {Count} campos enriquecidos", 
                        pluginConfig.PluginKey, 
                        integrationResult.ResponseData.Count);
                }

                if (!integrationResult.Success)
                {
                    result.Error = string.Join("; ", integrationResult.Errors);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error ejecutando plugin {PluginKey}", pluginConfig.PluginKey);
                result.Success = false;
                result.Mensaje = "Error ejecutando plugin";
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Merge simple: los datos nuevos sobrescriben los existentes
        /// </summary>
        private void MergeDatos(Dictionary<string, object> destino, Dictionary<string, object> nuevos)
        {
            foreach (var kvp in nuevos)
            {
                if (destino.ContainsKey(kvp.Key))
                {
                    logger.LogDebug("Actualizando campo existente: {Key}", kvp.Key);
                }
                else
                {
                    logger.LogDebug("Agregando nuevo campo: {Key}", kvp.Key);
                }
                
                destino[kvp.Key] = kvp.Value; // Sobrescribir o agregar
            }
        }

        private static bool TryObtenerIdActivo(IReadOnlyDictionary<string, object> values, out string idActivo)
        {
            foreach (var kvp in values)
            {
                if (!string.Equals(kvp.Key, "idActivo", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = kvp.Value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    idActivo = value;
                    return true;
                }
            }

            idActivo = string.Empty;
            return false;
        }
    }

}
