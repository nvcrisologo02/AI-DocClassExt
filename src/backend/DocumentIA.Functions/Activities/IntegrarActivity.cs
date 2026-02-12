using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public async Task<ResultadoIntegracion> Run(
            [ActivityTrigger] IntegrarInput input)
        {
            logger.LogInformation("Iniciando integracion para tipologia: {Tipologia}", input.Tipologia);

            var resultado = new ResultadoIntegracion
            {
                Tipologia = input.Tipologia,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                // Cargar configuracion de plugins para esta tipologia
                var pluginConfig = await configLoader.LoadConfigAsync(input.Tipologia);

                if (pluginConfig.Plugins.Count == 0)
                {
                    logger.LogWarning("No hay plugins configurados para tipologia {Tipologia}", input.Tipologia);
                    resultado.Estado = "OK";
                    resultado.Mensaje = "No hay integraciones configuradas para esta tipologia";
                    return resultado;
                }

                // Ordenar plugins por prioridad
                var pluginsOrdenados = pluginConfig.Plugins
                    .Where(p => p.Enabled)
                    .OrderBy(p => p.Priority)
                    .ToList();

                logger.LogInformation("Ejecutando {Count} plugins en orden de prioridad", pluginsOrdenados.Count);

                // Ejecutar cada plugin
                foreach (var pluginCfg in pluginsOrdenados)
                {
                    var pluginResult = await ExecutePluginAsync(pluginCfg, input);
                    resultado.Plugins.Add(pluginResult);

                    // Si un plugin critico falla, detener la cadena
                    if (!pluginResult.Success && pluginCfg.Priority == 1)
                    {
                        logger.LogError("Plugin critico {PluginKey} fallo. Deteniendo cadena de integracion",
                            pluginCfg.PluginKey);
                        resultado.Estado = "ERROR";
                        resultado.Mensaje = $"Plugin critico {pluginCfg.PluginKey} fallo";
                        return resultado;
                    }
                }

                // Determinar estado final
                bool todosExitosos = resultado.Plugins.All(p => p.Success);
                bool algunoFallo = resultado.Plugins.Any(p => !p.Success);

                if (todosExitosos)
                {
                    resultado.Estado = "OK";
                    resultado.Mensaje = $"Todas las integraciones completadas exitosamente ({resultado.Plugins.Count})";
                }
                else if (algunoFallo)
                {
                    resultado.Estado = "REVISION";
                    resultado.Mensaje = $"Algunas integraciones fallaron. Revisar logs.";
                }

                logger.LogInformation("Integracion completada. Estado: {Estado}", resultado.Estado);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en proceso de integracion para tipologia {Tipologia}", input.Tipologia);
                resultado.Estado = "ERROR";
                resultado.Mensaje = $"Error en integracion: {ex.Message}";
                resultado.Plugins.Add(new PluginExecutionResult
                {
                    PluginKey = "sistema",
                    Success = false,
                    Mensaje = ex.Message,
                    Error = ex.ToString()
                });
            }

            return resultado;
        }

        private async Task<PluginExecutionResult> ExecutePluginAsync(PluginConfig pluginConfig, IntegrarInput input)
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
                    logger.LogInformation("Plugin {PluginKey} no encontrado en manager. Creando...", 
                        pluginConfig.PluginKey);
                    plugin = await pluginFactory.CreatePluginAsync(pluginConfig);
                    pluginManager.RegisterPlugin(pluginConfig.PluginKey, plugin);
                }

                // Preparar datos para el plugin
                var pluginData = new Dictionary<string, object>
                {
                    ["tipologia"] = input.Tipologia,
                    ["documentoId"] = input.DocumentoId,
                    ["datosExtraidos"] = input.DatosExtraidos,
                    ["metadata"] = input.Metadata
                };

                // Ejecutar plugin
                var integrationResult = await pluginManager.ExecutePluginAsync(pluginConfig.PluginKey, pluginData);

                result.Success = integrationResult.Success;
                result.Mensaje = integrationResult.Message;
                result.StatusCode = integrationResult.StatusCode;
                result.DurationMs = (int)integrationResult.Duration.TotalMilliseconds;
                result.ResponseData = integrationResult.ResponseData;

                if (!integrationResult.Success)
                {
                    result.Error = string.Join("; ", integrationResult.Errors);
                }

                logger.LogInformation("Plugin {PluginKey} ejecutado. Success: {Success}, Duration: {Duration}ms",
                    pluginConfig.PluginKey, result.Success, result.DurationMs);
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
    }

    // Modelos de entrada/salida
    public class IntegrarInput
    {
        public string Tipologia { get; set; } = string.Empty;
        public string DocumentoId { get; set; } = string.Empty;
        public Dictionary<string, object> DatosExtraidos { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ResultadoIntegracion
    {
        public string Tipologia { get; set; } = string.Empty;
        public string Estado { get; set; } = "OK"; // OK | ERROR | REVISION
        public string Mensaje { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public List<PluginExecutionResult> Plugins { get; set; } = new();
    }

    public class PluginExecutionResult
    {
        public string PluginKey { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool Success { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public int DurationMs { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object> ResponseData { get; set; } = new();
    }
}
