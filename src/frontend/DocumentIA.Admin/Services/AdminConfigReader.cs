namespace DocumentIA.Admin.Services;

/// <summary>
/// Lee claves de configuración tolerando las dos formas que expone Azure App Service:
/// jerárquica ("Seccion:Clave") y plana con guion bajo simple ("Seccion_Clave").
/// </summary>
public static class AdminConfigReader
{
    public static string? Get(IConfiguration configuration, string section, string key)
        => configuration[$"{section}:{key}"] ?? configuration[$"{section}_{key}"];
}
