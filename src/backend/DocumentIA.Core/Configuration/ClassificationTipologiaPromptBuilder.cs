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

                if (config is null 
                // || !config.IsDefault Comento el isdefault para que gestione en gpt todas las tipologias publicadas.
                || string.IsNullOrWhiteSpace(config.TipologiaId))
                {
                    continue;
                }

                // Usar Codigo (columna DB) como identificador canónico del prompt.
                // Es el mismo valor que devuelven DI y las reglas (ej: SERE-25, ESCR-01, nota-simple).
                // Fallback a tipologiaId del JSON si el Codigo está vacío.
                var codigoCanónico = !string.IsNullOrWhiteSpace(tipologia.Codigo)
                    ? tipologia.Codigo
                    : config.TipologiaId;

                if (!seen.Add(codigoCanónico))
                {
                    continue;
                }

                var nombre = string.IsNullOrWhiteSpace(config.TipologiaNombre)
                    ? codigoCanónico
                    : config.TipologiaNombre;
                var descripcion = string.IsNullOrWhiteSpace(config.GptDescripcion)
                    ? nombre
                    : config.GptDescripcion;

                lines.Add($"- {codigoCanónico}: {descripcion}");
            }
            catch
            {
                // Ignorar tipologías malformadas para no bloquear la construcción del prompt.
            }
        }

        return string.Join("\n", lines);
    }
}