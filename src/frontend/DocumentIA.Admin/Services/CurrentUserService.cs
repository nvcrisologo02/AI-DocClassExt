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
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    public const string PrincipalNameHeader = "X-MS-CLIENT-PRINCIPAL-NAME";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    public string UserName
    {
        get
        {
            string? headerValue = _httpContextAccessor.HttpContext?.Request.Headers[PrincipalNameHeader];
            return Resolve(headerValue, _environment.IsDevelopment());
        }
    }

    public static string Resolve(string? headerValue, bool isDevelopment)
    {
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.Trim();
        }

        return isDevelopment ? $"dev-{Environment.UserName}" : "no-autenticado";
    }
}
