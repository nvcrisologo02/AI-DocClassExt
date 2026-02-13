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
        private readonly ILoggerFactory loggerFactory;

        public PluginFactory(IHttpClientFactory httpClientFactory, ILogger<PluginFactory> logger, ILoggerFactory loggerFactory)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
            this.loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Crea y configura un plugin basado en la configuracion
        /// </summary>
        public async Task<IIntegrationPlugin> CreatePluginAsync(PluginConfig config)
{
    logger.LogInformation("Creando plugin: {Key} - Tipo: {Type}", config.PluginKey, config.PluginType);

    IIntegrationPlugin plugin;

    switch (config.PluginType.ToLower())
    {
        case "rest":
            var httpClient = httpClientFactory.CreateClient();
            plugin = new RestPlugin(httpClient);
            logger.LogDebug("RestPlugin creado para {Key}", config.PluginKey);
            break;

        case "soap":
            var soapHttpClient = httpClientFactory.CreateClient();
            var soapLogger = loggerFactory.CreateLogger<SoapPlugin>();
            plugin = new SoapPlugin(soapHttpClient, soapLogger);
            logger.LogDebug("SoapPlugin creado para {Key}", config.PluginKey);
            break;

        case "custom":
            var customLogger = loggerFactory.CreateLogger<CustomPlugin>();
            plugin = new CustomPlugin(customLogger);
            logger.LogDebug("CustomPlugin creado para {Key}", config.PluginKey);
            break;

        default:
            throw new InvalidOperationException($"Tipo de plugin no soportado: {config.PluginType}");
    }

    // Inicializar plugin
    await plugin.InitializeAsync(config.Configuration);

    // Envolver con ResilientPlugin si hay retry policy
    if (config.RetryPolicy != null)
    {
        var resilientLogger = loggerFactory.CreateLogger<ResilientPlugin>();
        plugin = new ResilientPlugin(plugin, config.RetryPolicy, resilientLogger);
        logger.LogInformation(
            "Plugin {Key} envuelto con ResilientPlugin. Retries: {Retries}", 
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
