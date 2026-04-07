using DocumentIA.Data.Entities;

namespace DocumentIA.Admin.Services;

public class SystemConfigService
{
    private readonly IConfiguration _configuration;
    private readonly TipologiaAdminService _tipologiaService;

    public SystemConfigService(IConfiguration configuration, TipologiaAdminService tipologiaService)
    {
        _configuration = configuration;
        _tipologiaService = tipologiaService;
    }

    /// <summary>
    /// Obtiene un resumen de la configuración del sistema
    /// </summary>
    public async Task<SystemConfiguration> GetConfigurationResumenAsync()
    {
        var tipologias = await _tipologiaService.GetTipologiasAsync();
        var plugins = await _tipologiaService.GetPluginConfigsAsync();
        
        var modelos = new List<ModeloConfigEntity>();
        modelos.AddRange(await _tipologiaService.GetModelosByTipoAsync(TipoModelo.Clasificacion));
        modelos.AddRange(await _tipologiaService.GetModelosByTipoAsync(TipoModelo.Extraccion));
        modelos.AddRange(await _tipologiaService.GetModelosByTipoAsync(TipoModelo.Prompt));

        return new SystemConfiguration
        {
            // Información general
            Environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            DotNetVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            TimeZone = TimeZoneInfo.Local.DisplayName,
            Timestamp = DateTime.UtcNow,

            // Configuración de APIs
            FunctionsBaseUrl = _configuration["FunctionsAdminApi:BaseUrl"] ?? string.Empty,
            
            // Resumen de datos
            TipologiasTotal = tipologias.Count,
            TipologiasActivas = tipologias.Count(t => t.Activa),
            TipologiasDraft = tipologias.Count(t => t.Estado == EstadoTipologia.Draft),
            TipologiasPublished = tipologias.Count(t => t.Estado == EstadoTipologia.Published),
            TipologiasRetired = tipologias.Count(t => t.Estado == EstadoTipologia.Retired),

            ModelosTotal = modelos.Count,
            ModelosActivos = modelos.Count(m => m.Activo),
            ModelosClasificacion = modelos.Count(m => m.Tipo == TipoModelo.Clasificacion),
            ModelosExtraccion = modelos.Count(m => m.Tipo == TipoModelo.Extraccion),
            ModelosPrompt = modelos.Count(m => m.Tipo == TipoModelo.Prompt),

            PluginsTotal = plugins.Count,
            PluginsDraft = plugins.Count(p => p.Estado == EstadoPluginConfig.Draft),
            PluginsPublished = plugins.Count(p => p.Estado == EstadoPluginConfig.Published),
            PluginsRetired = plugins.Count(p => p.Estado == EstadoPluginConfig.Retired),

            // Providers únicos
            ProvidersUsados = modelos.Select(m => m.Provider).Distinct().OrderBy(p => p).ToList(),
            
            // Tipologías con más plugins
            TipologiasConPlugins = plugins.Select(p => p.TipologiaCodigo).Distinct().Count()
        };
    }
}

public class SystemConfiguration
{
    // Sistema
    public string Environment { get; set; } = string.Empty;
    public string DotNetVersion { get; set; } = string.Empty;
    public string OS { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    // APIs
    public string FunctionsBaseUrl { get; set; } = string.Empty;

    // Tipologías
    public int TipologiasTotal { get; set; }
    public int TipologiasActivas { get; set; }
    public int TipologiasDraft { get; set; }
    public int TipologiasPublished { get; set; }
    public int TipologiasRetired { get; set; }

    // Modelos
    public int ModelosTotal { get; set; }
    public int ModelosActivos { get; set; }
    public int ModelosClasificacion { get; set; }
    public int ModelosExtraccion { get; set; }
    public int ModelosPrompt { get; set; }

    // Plugins
    public int PluginsTotal { get; set; }
    public int PluginsDraft { get; set; }
    public int PluginsPublished { get; set; }
    public int PluginsRetired { get; set; }
    public int TipologiasConPlugins { get; set; }

    // Providers
    public List<string> ProvidersUsados { get; set; } = new();
}
