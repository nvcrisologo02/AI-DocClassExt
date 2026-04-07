using System.Text.Json;
using DocumentIA.Data.Entities;

namespace DocumentIA.Admin.Services;

public class SystemConfigService
{
    private readonly IConfiguration _configuration;
    private readonly TipologiaAdminService _tipologiaService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SystemConfigService> _logger;

    public SystemConfigService(
        IConfiguration configuration, 
        TipologiaAdminService tipologiaService, 
        IHttpClientFactory httpClientFactory,
        ILogger<SystemConfigService> logger)
    {
        _configuration = configuration;
        _tipologiaService = tipologiaService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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

        // Obtener configuración de Functions
        var functionsConfig = await GetFunctionsConfigurationAsync();

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
            FunctionsConfiguration = functionsConfig,
            
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

    /// <summary>
    /// Obtiene la configuración del servicio de Functions
    /// </summary>
    private async Task<FunctionsConfiguration?> GetFunctionsConfigurationAsync()
    {
        try
        {
            // Obtener HttpClient de la factory - tiene BaseAddress configurado
            var httpClient = _httpClientFactory.CreateClient(nameof(SystemConfigService));
            
            // La URL relativa se resolverá contra BaseAddress del HttpClient
            // BaseAddress ya es http://localhost:7071/api/
            var configUrl = "management/configuration";

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await httpClient.GetAsync(configUrl, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var config = JsonSerializer.Deserialize<dynamic>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (config != null)
                {
                    return ParseFunctionsConfiguration(jsonContent);
                }
            }
            else
            {
                _logger.LogWarning($"No se pudo obtener configuración de Functions desde {configUrl}: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning($"Error conectando a Functions configuration: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timeout al obtener configuración de Functions");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener configuración de Functions: {ex.Message}");
        }

        return null;
    }

    private FunctionsConfiguration? ParseFunctionsConfiguration(string jsonContent)
    {
        try
        {
            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
            {
                var root = doc.RootElement;
                
                var config = new FunctionsConfiguration
                {
                    Environment = root.TryGetProperty("environment", out var env) ? env.GetString() : null,
                    TimestampUtc = root.TryGetProperty("timestampUtc", out var ts) ? ts.GetString() : null,
                    SqlConnectionStatus = "Configured",
                };

                // Extraer información de configuración
                if (root.TryGetProperty("settings", out var settings))
                {
                    var settingsList = new Dictionary<string, string>();
                    foreach (var setting in settings.EnumerateArray())
                    {
                        if (setting.TryGetProperty("key", out var key) && setting.TryGetProperty("value", out var value))
                        {
                            var keyStr = key.GetString() ?? "";
                            var valueStr = value.GetString() ?? "";
                            
                            // Filtrar solo configuración relevante (no secrets)
                            if (!keyStr.Contains("Password") && !keyStr.Contains("Key") && 
                                !valueStr.Contains("***") && !string.IsNullOrWhiteSpace(valueStr) && 
                                valueStr != "(empty)")
                            {
                                settingsList.TryAdd(keyStr, valueStr);
                            }
                        }
                    }
                    config.Settings = settingsList;
                }

                // Extraer información de SQL Connection
                if (root.TryGetProperty("effectiveSqlConnection", out var sqlConn))
                {
                    if (sqlConn.TryGetProperty("key", out var connKey))
                    {
                        config.SqlConnectionStatus = connKey.GetString();
                    }
                }

                return config;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error parsing Functions configuration: {ex.Message}");
            return null;
        }
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
    public FunctionsConfiguration? FunctionsConfiguration { get; set; }

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

public class FunctionsConfiguration
{
    public string? Environment { get; set; }
    public string? TimestampUtc { get; set; }
    public string? SqlConnectionStatus { get; set; }
    public Dictionary<string, string>? Settings { get; set; }
}
