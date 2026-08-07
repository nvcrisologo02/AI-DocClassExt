namespace DocumentIA.Admin.Services;

/// <summary>
/// Texto y estilo del aviso de entorno del backend.
/// Un entorno sin identificar se muestra como advertencia, nunca como estado normal:
/// el aviso solo puede tranquilizar cuando sabe de verdad contra que entorno se trabaja.
/// </summary>
public sealed record EnvironmentBannerState(string Label, string CssClass)
{
    private const string UnknownFromBackend = "Unknown";

    public static EnvironmentBannerState Resolve(
        bool loaded,
        bool backendUnreachable,
        string? environment,
        bool readOnlyMode = false)
    {
        var state = ResolveEnvironment(loaded, backendUnreachable, environment);

        if (!loaded || !readOnlyMode)
        {
            return state;
        }

        return state with { Label = $"{state.Label} · solo lectura (sin usuario autenticado)" };
    }

    private static EnvironmentBannerState ResolveEnvironment(bool loaded, bool backendUnreachable, string? environment)
    {
        if (!loaded)
        {
            return new EnvironmentBannerState("Conectando con el backend…", "bg-light text-muted");
        }

        if (backendUnreachable)
        {
            return new EnvironmentBannerState("⚠ Backend no accesible", "bg-warning text-dark");
        }

        if (string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase))
        {
            return new EnvironmentBannerState("⛔ ENTORNO: PRODUCCIÓN", "bg-danger text-white");
        }

        if (string.IsNullOrWhiteSpace(environment)
            || string.Equals(environment, UnknownFromBackend, StringComparison.OrdinalIgnoreCase))
        {
            return new EnvironmentBannerState("⚠ Entorno del backend sin identificar", "bg-warning text-dark");
        }

        return new EnvironmentBannerState($"Entorno backend: {environment}", "bg-info text-dark");
    }
}
