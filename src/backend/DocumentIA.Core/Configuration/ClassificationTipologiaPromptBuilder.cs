using System.Text.Json;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Core.Configuration;

public class ClassificationTipologiaPromptBuilder
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public ClassificationTipologiaPromptBuilder(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public string Build()
    {
        return _cache.GetOrCreate("clasificacion:tipologias:prompt", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return BuildFromDatabase();
        }) ?? string.Empty;
    }

    private string BuildFromDatabase()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITipologiaRepository>();
        var tipologias = repository.GetAllPublishedAsync()
            .GetAwaiter()
            .GetResult();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();

        foreach (var tipologia in tipologias)
        {
            if (string.IsNullOrWhiteSpace(tipologia.ConfiguracionJson))
            {
                continue;
            }

            try
            {
                var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(tipologia.ConfiguracionJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config is null || !config.IsDefault || string.IsNullOrWhiteSpace(config.TipologiaId))
                {
                    continue;
                }

                if (!seen.Add(config.TipologiaId))
                {
                    continue;
                }

                var nombre = string.IsNullOrWhiteSpace(config.TipologiaNombre)
                    ? config.TipologiaId
                    : config.TipologiaNombre;
                var descripcion = string.IsNullOrWhiteSpace(config.GptDescripcion)
                    ? nombre
                    : config.GptDescripcion;

                lines.Add($"- {config.TipologiaId}: {descripcion}");
            }
            catch
            {
                // Ignorar tipologías malformadas para no bloquear la construcción del prompt.
            }
        }

        return string.Join("\n", lines);
    }
}