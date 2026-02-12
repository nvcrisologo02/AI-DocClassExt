using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Cargador de configuraciones de plugins desde archivos JSON
    /// </summary>
    public class PluginConfigLoader
    {
        private readonly string configBasePath;
        private readonly ILogger<PluginConfigLoader> logger;
        private readonly Dictionary<string, PluginConfiguration> cachedConfigs = new();

        public PluginConfigLoader(string configBasePath, ILogger<PluginConfigLoader> logger)
        {
            this.configBasePath = configBasePath;
            this.logger = logger;
        }

        /// <summary>
        /// Carga la configuracion de plugins para una tipologia especifica
        /// </summary>
        public async Task<PluginConfiguration> LoadConfigAsync(string tipologiaId)
        {
            // Verificar cache
            if (cachedConfigs.ContainsKey(tipologiaId))
            {
                logger.LogDebug("Configuracion de plugins para {TipologiaId} encontrada en cache", tipologiaId);
                return cachedConfigs[tipologiaId];
            }

            string configPath = Path.Combine(configBasePath, $"{tipologiaId}.plugins.json");

            if (!File.Exists(configPath))
            {
                logger.LogWarning("No se encontro configuracion de plugins para tipologia {TipologiaId} en {Path}",
                    tipologiaId, configPath);

                // Retornar configuracion vacia
                return new PluginConfiguration
                {
                    TipologiaId = tipologiaId,
                    Plugins = new List<PluginConfig>()
                };
            }

            try
            {
                string jsonContent = await File.ReadAllTextAsync(configPath);
                var config = JsonSerializer.Deserialize<PluginConfiguration>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config == null)
                {
                    throw new InvalidOperationException($"La configuracion de {tipologiaId} es nula");
                }

                // Cachear configuracion
                cachedConfigs[tipologiaId] = config;

                logger.LogInformation("Configuracion de plugins cargada para {TipologiaId}: {PluginCount} plugins",
                    tipologiaId, config.Plugins.Count);

                return config;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cargando configuracion de plugins para {TipologiaId}", tipologiaId);
                throw new PluginException("ConfigLoader",
                    $"Error cargando configuracion de plugins para {tipologiaId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Invalida la cache de configuraciones
        /// </summary>
        public void ClearCache()
        {
            cachedConfigs.Clear();
            logger.LogInformation("Cache de configuraciones de plugins limpiada");
        }

        /// <summary>
        /// Recarga una configuracion especifica
        /// </summary>
        public async Task<PluginConfiguration> ReloadConfigAsync(string tipologiaId)
        {
            if (cachedConfigs.ContainsKey(tipologiaId))
                cachedConfigs.Remove(tipologiaId);

            return await LoadConfigAsync(tipologiaId);
        }
    }
}
