namespace DocumentIA.Core.Services;

using DocumentIA.Core.Models;

public static class ClassificationLevelResolver
{
    public const string LevelTdn1 = "TDN1";
    public const string LevelTdn1Tdn2 = "TDN1_TDN2";
    public const string DefaultLevel = LevelTdn1Tdn2;

    public static IReadOnlyCollection<string> AllowedLevels { get; } =
    [
        LevelTdn1,
        LevelTdn1Tdn2
    ];

    public static string Resolve(string? requestedLevel, string? defaultLevel)
    {
        var resolvedDefault = Normalize(defaultLevel, nameof(defaultLevel), "NivelClasificacionDefault");

        if (string.IsNullOrWhiteSpace(requestedLevel))
        {
            return resolvedDefault;
        }

        return Normalize(requestedLevel, nameof(requestedLevel), "NivelClasificacion");
    }

    public static string ApplyTo(ConfiguracionIA classificationConfig, string? defaultLevel)
    {
        ArgumentNullException.ThrowIfNull(classificationConfig);

        var resolvedLevel = Resolve(classificationConfig.NivelClasificacion, defaultLevel);
        classificationConfig.NivelClasificacion = resolvedLevel;
        return resolvedLevel;
    }

    public static bool TryResolve(string? requestedLevel, string? defaultLevel, out string resolvedLevel, out string? error)
    {
        try
        {
            resolvedLevel = Resolve(requestedLevel, defaultLevel);
            error = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            resolvedLevel = string.Empty;
            error = ex.Message;
            return false;
        }
    }

    private static string Normalize(string? value, string parameterName, string displayName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException(
                $"{displayName} es obligatorio y debe ser uno de: {string.Join(", ", AllowedLevels)}.",
                parameterName);
        }

        var match = AllowedLevels.FirstOrDefault(allowed =>
            string.Equals(allowed, trimmed, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new ArgumentException(
                $"{displayName} '{trimmed}' no es válido. Valores permitidos: {string.Join(", ", AllowedLevels)}.",
                parameterName);
        }

        return match;
    }
}