using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services;

/// <summary>
/// Calcula la clave de intencion (AB#100930). Puro y sin dependencias de Functions para que
/// la actividad, la derivacion de filas antiguas y los tests usen la misma funcion. El
/// backfill en PowerShell replica FormaCanonica y Calcular; los vectores de
/// ClaveIntencionBuilderTests fijan la paridad.
/// </summary>
public static class ClaveIntencionBuilder
{
    public const string Version = "v1";
    public const int LongitudClave = 32;
    public const string UniversoCompleto = ClaveIntencionBuilderConstantes.UniversoCompleto;
    public const string ModeloExpectedType = "expectedtype-input";
    private const string ExpectedTypeDesconocido = "DESCONOCIDO";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static ClaveIntencionComponentes Normalizar(
        bool classificationOnly,
        string? nivelClasificacion,
        string? nivelDefault,
        IEnumerable<string>? codigosRestriccion,
        string? expectedType,
        string? modeloClasificacion)
    {
        var expected = NormalizarExpectedType(expectedType);

        return new ClaveIntencionComponentes
        {
            Modo = classificationOnly ? ModosIntencion.SoloClasificacion : ModosIntencion.Completo,
            Nivel = NormalizarNivel(nivelClasificacion, nivelDefault),
            Universo = NormalizarUniverso(codigosRestriccion),
            ExpectedType = expected,
            ModeloClasificacion = expected.Length > 0
                ? ModeloExpectedType
                : (modeloClasificacion ?? string.Empty).Trim().ToLowerInvariant()
        };
    }

    public static ClaveIntencion Construir(ClaveIntencionComponentes componentes)
        => new(Calcular(componentes), Serializar(componentes), componentes);

    public static string FormaCanonica(ClaveIntencionComponentes c)
        => string.Join("|", Version, c.Modo, c.Nivel, c.Universo, c.ExpectedType, c.ModeloClasificacion);

    public static string Calcular(ClaveIntencionComponentes componentes)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(FormaCanonica(componentes)));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..LongitudClave];
    }

    public static string Serializar(ClaveIntencionComponentes componentes)
        => JsonSerializer.Serialize(componentes, JsonOptions);

    public static ClaveIntencionComponentes? Deserializar(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ClaveIntencionComponentes>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string NormalizarUniverso(IEnumerable<string>? codigos)
    {
        var lista = (codigos ?? Array.Empty<string>())
            .Select(c => (c ?? string.Empty).Trim().ToUpperInvariant())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        return lista.Count == 0 ? UniversoCompleto : string.Join(",", lista);
    }

    public static string NormalizarExpectedType(string? expectedType)
    {
        var valor = (expectedType ?? string.Empty).Trim().ToUpperInvariant();
        return valor == ExpectedTypeDesconocido ? string.Empty : valor;
    }

    private static string NormalizarNivel(string? nivel, string? nivelDefault)
    {
        var defecto = string.IsNullOrWhiteSpace(nivelDefault) ? ClassificationLevelResolver.DefaultLevel : nivelDefault;
        if (ClassificationLevelResolver.TryResolve(nivel, defecto, out var resuelto, out _))
        {
            return resuelto;
        }

        // Un nivel invalido no llega al orquestador (el trigger lo rechaza). Si llegase por otra
        // via, la clave sigue siendo estable y distinta de las validas.
        return (nivel ?? string.Empty).Trim().ToUpperInvariant();
    }
}
