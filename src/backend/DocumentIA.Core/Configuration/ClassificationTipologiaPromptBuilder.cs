using System.Text.Json;
using DocumentIA.Data.Context;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIA.Core.Configuration;

public class ClassificationTipologiaPromptBuilder
{
    public const string Phase1ResponseFormatInstruction =
        "Responde exclusivamente en JSON válido con esta estructura: {\"tdn1\": \"CODIGO_TDN1\" | null, \"propuesta\": \"texto libre\"}. No incluyas texto fuera del JSON.";

    public const string Phase2ResponseFormatInstruction =
        "Responde exclusivamente en JSON válido con esta estructura: {\"tdn2\": \"CODIGO_TDN2\"}. No incluyas texto fuera del JSON.";

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

    public string BuildTdn1Catalog()
    {
        return _cache.GetOrCreate("clasificacion:catalogo:tdn1", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICatalogoTdnRepository>();
            var familias = repository
                .GetFamiliasTdnActivasAsync()
                .GetAwaiter()
                .GetResult();

            return string.Join("\n", familias.Select(f => $"- {f.Codigo}: {f.Descripcion}"));
        }) ?? string.Empty;
    }

    public string BuildTdn2CatalogByFamilia(string tdn1Codigo)
    {
        if (string.IsNullOrWhiteSpace(tdn1Codigo))
        {
            throw new ArgumentException("El código de familia TDN1 es obligatorio.", nameof(tdn1Codigo));
        }

        var normalizedFamily = tdn1Codigo.Trim().ToUpperInvariant();
        var cacheKey = $"clasificacion:catalogo:tdn2:{normalizedFamily}";

        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITipologiaRepository>();
            var db = scope.ServiceProvider.GetRequiredService<DocumentIADbContext>();
            var tipologias = repository.GetAllPublishedAsync()
                .GetAwaiter()
                .GetResult();

            var catalogoTdn2 = db.CatalogoTdn2
                .ToList()
                .GroupBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Nombre, StringComparer.OrdinalIgnoreCase);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = new List<string>();

            // Filtrar tipologías que pertenezcan a la familia TDN1 especificada
            var tipologiasEnFamilia = tipologias
                .Where(t => !string.IsNullOrWhiteSpace(t.ConfiguracionJson))
                .Select(t =>
                {
                    try
                    {
                        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(t.ConfiguracionJson!, 
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        return new { Tipologia = t, Config = config };
                    }
                    catch
                    {
                        return new { Tipologia = t, Config = (TipologiaValidationConfig?)null };
                    }
                })
                .Where(x => x.Config != null && string.Equals(x.Config.ResolvedTdn1?.Trim(), normalizedFamily, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Construir catálogo enriquecido con tipologiacodigo, tipologiaNombre, gptdescription, tdn2
            foreach (var item in tipologiasEnFamilia)
            {
                var tipologia = item.Tipologia;
                var config = item.Config!;

                if (string.IsNullOrWhiteSpace(config.TipologiaId))
                {
                    continue;
                }

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

                var descripcion = string.IsNullOrWhiteSpace(config.ResolvedGptDescripcion)
                    ? nombre
                    : config.ResolvedGptDescripcion;

                var tdn2Codigo = config.ResolvedTdn2?.Trim() ?? "N/A";
                var tdn2Nombre = string.IsNullOrWhiteSpace(tdn2Codigo) || tdn2Codigo == "N/A"
                    ? "N/A"
                    : (catalogoTdn2.TryGetValue(tdn2Codigo, out var n2) ? n2 : tdn2Codigo);

                // Formato: - tipologiacodigo [tdn2: tdn2nombre] descripcion
                lines.Add($"- {codigoCanónico} [{tdn2Codigo}: {tdn2Nombre}] {descripcion}");
            }

            return string.Join("\n", lines);
        }) ?? string.Empty;
    }

    private string BuildFromDatabase()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITipologiaRepository>();
        var db = scope.ServiceProvider.GetRequiredService<DocumentIADbContext>();
        var tipologias = repository.GetAllPublishedAsync()
            .GetAwaiter()
            .GetResult();

        var catalogoTdn1 = db.CatalogoTdn1
            .ToList()
            .GroupBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Nombre, StringComparer.OrdinalIgnoreCase);

        var catalogoTdn2 = db.CatalogoTdn2
            .ToList()
            .GroupBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Nombre, StringComparer.OrdinalIgnoreCase);

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

                var descripcion = string.IsNullOrWhiteSpace(config.ResolvedGptDescripcion)
                    ? nombre
                    : config.ResolvedGptDescripcion;

                var tdn1Codigo = config.ResolvedTdn1?.Trim() ?? string.Empty;
                var tdn2Codigo = config.ResolvedTdn2?.Trim() ?? string.Empty;

                var tdn1Nombre = string.IsNullOrWhiteSpace(tdn1Codigo)
                    ? "N/A"
                    : (catalogoTdn1.TryGetValue(tdn1Codigo, out var n1) ? n1 : tdn1Codigo);
                var tdn2Nombre = string.IsNullOrWhiteSpace(tdn2Codigo)
                    ? "N/A"
                    : (catalogoTdn2.TryGetValue(tdn2Codigo, out var n2) ? n2 : tdn2Codigo);

                lines.Add($"- {codigoCanónico}: [{tdn1Nombre} / {tdn2Nombre}] {descripcion}");
            }
            catch
            {
                // Ignorar tipologías malformadas para no bloquear la construcción del prompt.
            }
        }

        return string.Join("\n", lines);
    }
}