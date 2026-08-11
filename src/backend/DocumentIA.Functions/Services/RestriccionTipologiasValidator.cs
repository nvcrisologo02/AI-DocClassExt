using DocumentIA.Core.Models;
using DocumentIA.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Valida y normaliza <see cref="RestriccionTipologias"/> contra el catálogo de tipologías
/// publicadas: canonicaliza códigos a su forma de BD, descarta los no publicados dejando
/// aviso en CodigosIgnorados y rechaza la petición si no queda ningún código válido.
/// </summary>
public sealed class RestriccionTipologiasValidator
{
    private const string CacheKey = "clasificacion:catalogo:codigos-publicados";

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RestriccionTipologiasValidator> _logger;

    public RestriccionTipologiasValidator(
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        ILogger<RestriccionTipologiasValidator> logger)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<RestriccionTipologiasValidationResult> ValidateAndNormalizeAsync(
        RestriccionTipologias? restriccion)
    {
        if (restriccion is null)
        {
            return new RestriccionTipologiasValidationResult(true, null);
        }

        var solicitados = (restriccion.Codigos ?? new List<string>())
            .Select(c => c?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (solicitados.Count == 0)
        {
            return new RestriccionTipologiasValidationResult(
                false,
                "instrucciones.restriccionTipologias.codigos debe contener al menos un código de tipología.");
        }

        var publicados = await GetCodigosPublicadosAsync();

        var validos = new List<string>();
        var ignorados = new List<string>();
        foreach (var codigo in solicitados)
        {
            if (publicados.TryGetValue(codigo, out var canonico))
            {
                validos.Add(canonico);
            }
            else
            {
                ignorados.Add(codigo);
            }
        }

        if (validos.Count == 0)
        {
            return new RestriccionTipologiasValidationResult(
                false,
                "instrucciones.restriccionTipologias.codigos no contiene ningún código de tipología publicada. " +
                $"Códigos rechazados: {string.Join(", ", ignorados)}.");
        }

        if (ignorados.Count > 0)
        {
            _logger.LogWarning(
                "RestriccionTipologias: {IgnoradosCount} códigos no publicados ignorados: {Ignorados}. Válidos: {Validos}",
                ignorados.Count,
                string.Join(", ", ignorados),
                string.Join(", ", validos));
        }

        restriccion.Codigos = validos;
        restriccion.CodigosIgnorados = ignorados.Count > 0 ? ignorados : null;

        return new RestriccionTipologiasValidationResult(true, null);
    }

    /// <summary>Mapa código (case-insensitive) -> forma canónica de BD, cacheado 5 min.</summary>
    private async Task<Dictionary<string, string>> GetCodigosPublicadosAsync()
    {
        var cached = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITipologiaRepository>();
            var tipologias = await repository.GetAllPublishedAsync();

            var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tipologia in tipologias)
            {
                if (!string.IsNullOrWhiteSpace(tipologia.Codigo))
                {
                    mapa.TryAdd(tipologia.Codigo.Trim(), tipologia.Codigo.Trim());
                }
            }

            return mapa;
        });

        return cached ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record RestriccionTipologiasValidationResult(bool IsValid, string? Error);
