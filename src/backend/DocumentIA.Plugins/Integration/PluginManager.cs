using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Gestor de plugins - descubrimiento, registro y ejecucion
    /// </summary>
    public class PluginManager
    {
        private readonly Dictionary<string, IIntegrationPlugin> registeredPlugins = new();
        private readonly ILogger<PluginManager> logger;

        public PluginManager(ILogger<PluginManager> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Registra un plugin en el manager
        /// </summary>
        public void RegisterPlugin(string key, IIntegrationPlugin plugin)
        {
            if (registeredPlugins.ContainsKey(key))
            {
                logger.LogWarning("Plugin con key {Key} ya esta registrado. Se sobreescribira.", key);
            }

            registeredPlugins[key] = plugin;
            logger.LogInformation("Plugin registrado: {Key} - {PluginName} v{Version}",
                key, plugin.PluginName, plugin.Version);
        }

        /// <summary>
        /// Obtiene un plugin por su clave
        /// </summary>
        public IIntegrationPlugin? GetPlugin(string key)
        {
            return registeredPlugins.ContainsKey(key) ? registeredPlugins[key] : null;
        }

        /// <summary>
        /// Ejecuta un plugin especifico con los datos proporcionados
        /// </summary>
        public async Task<IntegrationResult> ExecutePluginAsync(string pluginKey, Dictionary<string, object> data)
        {
            var plugin = GetPlugin(pluginKey);

            if (plugin == null)
            {
                logger.LogError("Plugin no encontrado: {PluginKey}", pluginKey);
                return new IntegrationResult
                {
                    Success = false,
                    Status = "ERROR",
                    Message = $"Plugin {pluginKey} no encontrado",
                    Errors = new List<string> { $"No existe un plugin registrado con la clave: {pluginKey}" }
                };
            }

            logger.LogInformation("Ejecutando plugin: {PluginKey} - {PluginName}", pluginKey, plugin.PluginName);

            try
            {
                var result = await plugin.ExecuteAsync(data);
                logger.LogInformation("Plugin {PluginKey} ejecutado. Status: {Status}, Duration: {Duration}ms",
                    pluginKey, result.Status, result.Duration.TotalMilliseconds);
                return result;
            }
            catch (PluginException ex)
            {
                logger.LogError(ex, "Error en plugin {PluginKey}: {Message}", pluginKey, ex.Message);
                return new IntegrationResult
                {
                    Success = false,
                    Status = "ERROR",
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message },
                    Metadata = new Dictionary<string, object>
                    {
                        ["pluginName"] = ex.PluginName,
                        ["isTransient"] = ex.IsTransient
                    }
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error inesperado ejecutando plugin {PluginKey}", pluginKey);
                return new IntegrationResult
                {
                    Success = false,
                    Status = "ERROR",
                    Message = "Error inesperado en la ejecucion del plugin",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// Verifica el estado de todos los plugins registrados
        /// </summary>
        public async Task<Dictionary<string, bool>> HealthCheckAllAsync()
        {
            var results = new Dictionary<string, bool>();

            foreach (var kvp in registeredPlugins)
            {
                try
                {
                    results[kvp.Key] = await kvp.Value.HealthCheckAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Health check fallo para plugin {PluginKey}", kvp.Key);
                    results[kvp.Key] = false;
                }
            }

            return results;
        }

        /// <summary>
        /// Lista todos los plugins registrados
        /// </summary>
        public List<PluginInfo> ListPlugins()
        {
            return registeredPlugins.Select(kvp => new PluginInfo
            {
                Key = kvp.Key,
                Name = kvp.Value.PluginName,
                Version = kvp.Value.Version
            }).ToList();
        }
    }

    public class PluginInfo
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }
}
