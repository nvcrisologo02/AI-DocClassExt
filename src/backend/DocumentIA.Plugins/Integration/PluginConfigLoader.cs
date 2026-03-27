using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Cargador de configuraciones de plugins desde archivos JSON
    /// </summary>
    public class PluginConfigLoader
    {
        private readonly string? configBasePath;
        private readonly IMemoryCache? cache;
        private readonly IServiceScopeFactory? scopeFactory;
        private readonly ILogger<PluginConfigLoader> logger;
        private readonly Dictionary<string, PluginConfiguration> cachedConfigs = new();

        public PluginConfigLoader(string configBasePath, ILogger<PluginConfigLoader> logger)
        {
            this.configBasePath = configBasePath;
            this.logger = logger;
        }

        public PluginConfigLoader(IMemoryCache cache, IServiceScopeFactory scopeFactory, ILogger<PluginConfigLoader> logger)
        {
            this.cache = cache;
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        /// <summary>
        /// Carga la configuracion de plugins para una tipologia especifica
        /// </summary>
        public async Task<PluginConfiguration> LoadConfigAsync(string tipologiaId)
        {
            if (cache is not null && scopeFactory is not null)
            {
                return await cache.GetOrCreateAsync($"plugins:{tipologiaId}", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    return await LoadConfigFromDatabaseAsync(tipologiaId);
                }) ?? new PluginConfiguration { TipologiaId = tipologiaId };
            }

            // Verificar cache
            if (cachedConfigs.ContainsKey(tipologiaId))
            {
                logger.LogDebug("Configuracion de plugins para {TipologiaId} encontrada en cache", tipologiaId);
                return cachedConfigs[tipologiaId];
            }

            if (string.IsNullOrWhiteSpace(configBasePath))
            {
                throw new InvalidOperationException("PluginConfigLoader no esta correctamente configurado.");
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
            if (cache is MemoryCache memoryCache)
            {
                memoryCache.Clear();
            }
            logger.LogInformation("Cache de configuraciones de plugins limpiada");
        }

        /// <summary>
        /// Recarga una configuracion especifica
        /// </summary>
        public async Task<PluginConfiguration> ReloadConfigAsync(string tipologiaId)
        {
            if (cachedConfigs.ContainsKey(tipologiaId))
                cachedConfigs.Remove(tipologiaId);

            cache?.Remove($"plugins:{tipologiaId}");

            return await LoadConfigAsync(tipologiaId);
        }

        private async Task<PluginConfiguration> LoadConfigFromDatabaseAsync(string tipologiaId)
        {
            using var scope = scopeFactory!.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPluginTipologiaConfigRepository>();
            var dbConfig = await repository.GetPublishedByTipologiaCodigoAsync(tipologiaId);

            if (dbConfig is null || dbConfig.Estado != EstadoPluginConfig.Published)
            {
                logger.LogWarning("No se encontro configuracion publicada de plugins para tipologia {TipologiaId} en BD", tipologiaId);
                return new PluginConfiguration
                {
                    TipologiaId = tipologiaId,
                    Plugins = new List<PluginConfig>()
                };
            }

            try
            {
                var config = JsonSerializer.Deserialize<PluginConfiguration>(dbConfig.ConfiguracionJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config is null)
                {
                    throw new InvalidOperationException($"La configuracion de plugins para {tipologiaId} en BD es nula");
                }

                if (string.IsNullOrWhiteSpace(config.TipologiaId))
                {
                    config.TipologiaId = tipologiaId;
                }

                return config;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deserializando configuracion de plugins para {TipologiaId} desde BD", tipologiaId);
                throw new PluginException("ConfigLoader", $"Error cargando configuracion de plugins para {tipologiaId}: {ex.Message}", ex);
            }
        }
    }
}
