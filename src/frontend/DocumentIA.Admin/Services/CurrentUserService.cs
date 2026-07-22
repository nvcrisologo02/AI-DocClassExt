namespace DocumentIA.Admin.Services;

/// <summary>Identidad del usuario actual para trazabilidad de auditoría.</summary>
public interface ICurrentUserService
{
    string UserName { get; }
}

/// <summary>
/// Resuelve la identidad desde App Service Authentication (EasyAuth), que inyecta
/// la cabecera X-MS-CLIENT-PRINCIPAL-NAME en cada petición autenticada.
/// En desarrollo local (sin EasyAuth) usa el usuario del sistema con prefijo "dev-".
/// En Blazor Server (InteractiveServer) el servicio es scoped: una instancia por
/// circuito SignalR. Los handlers de eventos del circuito pueden ejecutarse sin
/// HttpContext (p. ej. tras el render inicial), por lo que el valor de la cabecera
/// se captura una vez en el constructor -momento en que la conexión HTTP del
/// circuito sí está disponible- y se usa como fallback si en un acceso posterior
/// el HttpContext ya no existe. Así se evita que una interacción legítima post-render
/// (publicar, guardar, activar) quede registrada como "no-autenticado".
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    public const string PrincipalNameHeader = "X-MS-CLIENT-PRINCIPAL-NAME";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;
    private readonly string? _capturedHeaderValue;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
        // Captura al crear el scope (circuito Blazor): los event handlers del circuito
        // pueden ejecutarse sin HttpContext, pero la construcción ocurre con la conexión activa.
        _capturedHeaderValue = ReadHeader();
    }

    public string UserName
    {
        get
        {
            var current = ReadHeader();
            return Resolve(!string.IsNullOrWhiteSpace(current) ? current : _capturedHeaderValue, _environment.IsDevelopment());
        }
    }

    private string? ReadHeader()
        => _httpContextAccessor.HttpContext?.Request.Headers[PrincipalNameHeader];

    public static string Resolve(string? headerValue, bool isDevelopment)
    {
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.Trim();
        }

        return isDevelopment ? $"dev-{Environment.UserName}" : "no-autenticado";
    }
}
