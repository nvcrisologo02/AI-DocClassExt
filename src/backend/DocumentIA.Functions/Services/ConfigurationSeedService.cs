using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services;

public static class ConfigurationSeedService
{
    public static async Task SeedAsync(DocumentIADbContext dbContext, ILogger logger, string configRootPath)
    {
        await SeedTipologiasAsync(dbContext, logger, Path.Combine(configRootPath, "tipologias"));
        await SeedModelosAsync(dbContext, logger, configRootPath);
        await SeedPluginsAsync(dbContext, logger, Path.Combine(configRootPath, "tipologias"));
    }

    private static async Task SeedTipologiasAsync(DocumentIADbContext dbContext, ILogger logger, string tipologiasPath)
    {
        if (!Directory.Exists(tipologiasPath))
        {
            logger.LogWarning("No existe directorio de tipologias para seed: {Path}", tipologiasPath);
            return;
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var files = Directory.EnumerateFiles(tipologiasPath, "*.validation.json", SearchOption.TopDirectoryOnly);

        foreach (var filePath in files)
        {
            var technicalKey = Path.GetFileName(filePath).Replace(".validation.json", string.Empty, StringComparison.OrdinalIgnoreCase);
            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, options);
            if (config is null)
            {
                continue;
            }

            var entity = await dbContext.Tipologias.FirstOrDefaultAsync(t => t.Codigo == technicalKey);
            if (entity is null)
            {
                entity = new TipologiaEntity
                {
                    Codigo = technicalKey,
                    Nombre = string.IsNullOrWhiteSpace(config.TipologiaNombre) ? config.TipologiaId : config.TipologiaNombre,
                    Version = config.Version,
                    Activa = true,
                    Estado = EstadoTipologia.Published,
                    PublicadaEn = DateTime.UtcNow,
                    PublicadaPor = "seed",
                    VersionPublicada = config.Version,
                    ConfiguracionJson = json,
                    FechaCreacion = DateTime.UtcNow,
                    FechaActualizacion = DateTime.UtcNow
                };
                dbContext.Tipologias.Add(entity);
            }
            else if (string.IsNullOrWhiteSpace(entity.ConfiguracionJson))
            {
                entity.Nombre = string.IsNullOrWhiteSpace(config.TipologiaNombre) ? entity.Nombre : config.TipologiaNombre;
                entity.Version = string.IsNullOrWhiteSpace(config.Version) ? entity.Version : config.Version;
                entity.Activa = true;
                entity.Estado = EstadoTipologia.Published;
                entity.PublicadaEn ??= DateTime.UtcNow;
                entity.PublicadaPor ??= "seed";
                entity.VersionPublicada = config.Version;
                entity.ConfiguracionJson = json;
                entity.FechaActualizacion = DateTime.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedModelosAsync(DocumentIADbContext dbContext, ILogger logger, string configRootPath)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var classPath = Path.Combine(configRootPath, "classification", "models.json");
        if (File.Exists(classPath))
        {
            var json = await File.ReadAllTextAsync(classPath);
            var registry = JsonSerializer.Deserialize<ClassificationModelRegistry>(json, options);
            if (registry is not null)
            {
                foreach (var model in registry.Models)
                {
                    await UpsertModeloAsync(dbContext, TipoModelo.Clasificacion, model.Key, model.Provider, JsonSerializer.Serialize(model));
                }
            }
        }

        var extractionPath = Path.Combine(configRootPath, "extraction", "models.json");
        if (File.Exists(extractionPath))
        {
            var json = await File.ReadAllTextAsync(extractionPath);
            var registry = JsonSerializer.Deserialize<ExtractionModelRegistry>(json, options);
            if (registry is not null)
            {
                foreach (var model in registry.Models)
                {
                    await UpsertModeloAsync(dbContext, TipoModelo.Extraccion, model.Key, model.Provider, JsonSerializer.Serialize(model));
                }
            }
        }

        var promptPath = Path.Combine(configRootPath, "prompt", "models.json");
        if (File.Exists(promptPath))
        {
            var json = await File.ReadAllTextAsync(promptPath);
            var registry = JsonSerializer.Deserialize<PromptModelRegistry>(json, options);
            if (registry is not null)
            {
                foreach (var model in registry.Models)
                {
                    await UpsertModeloAsync(dbContext, TipoModelo.Prompt, model.Key, model.Provider, JsonSerializer.Serialize(model));
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task UpsertModeloAsync(DocumentIADbContext dbContext, TipoModelo tipo, string key, string provider, string configJson)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var existing = await dbContext.ModeloConfigs.FirstOrDefaultAsync(m => m.Key == key);
        if (existing is null)
        {
            dbContext.ModeloConfigs.Add(new ModeloConfigEntity
            {
                Tipo = tipo,
                Key = key,
                Provider = provider,
                Activo = true,
                ConfiguracionJson = configJson,
                CreadoPor = "seed",
                FechaCreacion = DateTime.UtcNow
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(existing.ConfiguracionJson))
        {
            existing.Tipo = tipo;
            existing.Provider = provider;
            existing.Activo = true;
            existing.ConfiguracionJson = configJson;
            existing.FechaActualizacion = DateTime.UtcNow;
        }
    }

    private static async Task SeedPluginsAsync(DocumentIADbContext dbContext, ILogger logger, string tipologiasPath)
    {
        if (!Directory.Exists(tipologiasPath))
        {
            logger.LogWarning("No existe directorio de tipologias para seed de plugins: {Path}", tipologiasPath);
            return;
        }

        var files = Directory.EnumerateFiles(tipologiasPath, "*.plugins.json", SearchOption.TopDirectoryOnly);
        foreach (var filePath in files)
        {
            var technicalKey = Path.GetFileName(filePath).Replace(".plugins.json", string.Empty, StringComparison.OrdinalIgnoreCase);
            var json = await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            var existing = await dbContext.PluginTipologiaConfigs
                .FirstOrDefaultAsync(p => p.TipologiaCodigo == technicalKey);

            if (existing is null)
            {
                dbContext.PluginTipologiaConfigs.Add(new PluginTipologiaConfigEntity
                {
                    TipologiaCodigo = technicalKey,
                    ConfiguracionJson = json,
                    Estado = EstadoPluginConfig.Published,
                    FechaCreacion = DateTime.UtcNow,
                    FechaActualizacion = DateTime.UtcNow,
                    PublicadaEn = DateTime.UtcNow,
                    PublicadaPor = "seed"
                });
            }
            else if (string.IsNullOrWhiteSpace(existing.ConfiguracionJson))
            {
                existing.ConfiguracionJson = json;
                existing.Estado = EstadoPluginConfig.Published;
                existing.FechaActualizacion = DateTime.UtcNow;
                existing.PublicadaEn ??= DateTime.UtcNow;
                existing.PublicadaPor ??= "seed";
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
