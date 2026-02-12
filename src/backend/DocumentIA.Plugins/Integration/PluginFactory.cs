using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Factory para crear instancias de plugins basados en configuracion
    /// </summary>
    public class PluginFactory
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<PluginFactory> logger;

        public PluginFactory(IHttpClientFactory httpClientFactory, ILogger<PluginFactory> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
        }

        /// <summary>
        /// Crea y configura un plugin basado en la configuracion
        /// </summary>
        public async Task<IIntegrationPlugin> CreatePluginAsync(PluginConfig config)
        {
            IIntegrationPlugin plugin = config.PluginType.ToLower() switch
            {
                "rest" => CreateRestPlugin(config.PluginKey),
                "soap" => throw new NotImplementedException("Plugin SOAP pendiente de implementacion"),
                "custom" => throw new NotImplementedException("Plugin custom requiere implementacion especifica"),
                _ => throw new PluginException("PluginFactory", 
                    $"Tipo de plugin no soportado: {config.PluginType}")
            };

            // Inicializar con configuracion
            await plugin.InitializeAsync(config.Configuration);

            // Envolver con resiliencia si tiene retry policy
            if (config.RetryPolicy != null)
            {
                plugin = new ResilientPlugin(plugin, config.RetryPolicy, logger);
                logger.LogInformation("Plugin {PluginKey} envuelto con ResilientPlugin. Retries: {MaxRetries}",
                    config.PluginKey, config.RetryPolicy.MaxRetries);
            }

            return plugin;
        }

        /// <summary>
        /// Crea multiples plugins desde una configuracion de tipologia
        /// </summary>
        public async Task<Dictionary<string, IIntegrationPlugin>> CreatePluginsAsync(PluginConfiguration pluginConfig)
        {
            var plugins = new Dictionary<string, IIntegrationPlugin>();

            foreach (var config in pluginConfig.Plugins)
            {
                if (!config.Enabled)
                {
                    logger.LogInformation("Plugin {PluginKey} esta deshabilitado. Omitiendo...", config.PluginKey);
                    continue;
                }

                try
                {
                    var plugin = await CreatePluginAsync(config);
                    plugins[config.PluginKey] = plugin;
                    logger.LogInformation("Plugin creado: {PluginKey} ({PluginType})", 
                        config.PluginKey, config.PluginType);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error creando plugin {PluginKey}", config.PluginKey);
                    throw;
                }
            }

            return plugins;
        }

        private RestPlugin CreateRestPlugin(string pluginKey)
        {
            var httpClient = httpClientFactory.CreateClient(pluginKey);
            return new RestPlugin(httpClient);
        }
    }
}
