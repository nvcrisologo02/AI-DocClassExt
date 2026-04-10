using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Triggers.Admin;

public class ConfigurationAdminFunction
{
    private static readonly string[] DiagnosticKeys =
    {
        "SecretsSource",
        "KeyVaultName",
        "RunDatabaseMigrationsOnStartup",
        "ConnectionStrings:DocumentIA",
        "SqlConnectionString",
        "AzureWebJobsStorage",
        "AzureStorageConnectionString",
        "GDC:Endpoint"
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationAdminFunction> _logger;

    public ConfigurationAdminFunction(IConfiguration configuration, ILogger<ConfigurationAdminFunction> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [Function("Admin_GetConfiguration")]
    public async Task<HttpResponseData> GetConfiguration(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/configuration")] HttpRequestData req)
    {
        var root = _configuration as IConfigurationRoot;
        var providers = root?.Providers.ToList() ?? new List<IConfigurationProvider>();

        var diagnostics = DiagnosticKeys.Select(key =>
        {
            var rawValue = _configuration[key];
            var source = ResolveSource(key, providers);

            return new
            {
                key,
                value = MaskValue(key, rawValue),
                source = source.Source,
                provider = source.Provider
            };
        }).ToList();

        var connectionFromConnectionStrings = _configuration["ConnectionStrings:DocumentIA"];
        var connectionFromSqlConnectionString = _configuration["SqlConnectionString"];
        var effectiveConnectionKey = !string.IsNullOrWhiteSpace(connectionFromConnectionStrings)
            ? "ConnectionStrings:DocumentIA"
            : "SqlConnectionString";
        var effectiveConnectionValue = !string.IsNullOrWhiteSpace(connectionFromConnectionStrings)
            ? connectionFromConnectionStrings
            : connectionFromSqlConnectionString;
        var effectiveConnectionSource = ResolveSource(effectiveConnectionKey, providers);

        var responsePayload = new
        {
            timestampUtc = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? "Unknown",
            effectiveSqlConnection = new
            {
                key = effectiveConnectionKey,
                value = MaskConnectionString(effectiveConnectionValue),
                source = effectiveConnectionSource.Source,
                provider = effectiveConnectionSource.Provider
            },
            settings = diagnostics
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(responsePayload);

        _logger.LogInformation("Configuration diagnostics endpoint called.");
        return response;
    }

    private static (string Source, string Provider) ResolveSource(string key, IReadOnlyList<IConfigurationProvider> providers)
    {
        for (var i = providers.Count - 1; i >= 0; i--)
        {
            if (providers[i].TryGet(key, out _))
            {
                var providerName = providers[i].GetType().Name;
                return (ClassifyProvider(providerName), providerName);
            }
        }

        return ("NotFound", "None");
    }

    private static string ClassifyProvider(string providerName)
    {
        if (providerName.Contains("AzureKeyVault", StringComparison.OrdinalIgnoreCase))
        {
            return "AzureKeyVault";
        }

        if (providerName.Contains("EnvironmentVariables", StringComparison.OrdinalIgnoreCase))
        {
            return "EnvironmentOrAppSettings";
        }

        if (providerName.Contains("Json", StringComparison.OrdinalIgnoreCase))
        {
            return "JsonFile";
        }

        return providerName;
    }

    private static string MaskValue(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty)";
        }

        if (key.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase))
        {
            return MaskConnectionString(value);
        }

        if (key.Contains("AzureWebJobsStorage", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Password", StringComparison.OrdinalIgnoreCase))
        {
            return "***";
        }

        return value;
    }

    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "(empty)";
        }

        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1], StringComparer.OrdinalIgnoreCase);

        parts.TryGetValue("Server", out var server);
        if (string.IsNullOrWhiteSpace(server))
        {
            parts.TryGetValue("Data Source", out server);
        }

        parts.TryGetValue("Database", out var database);
        if (string.IsNullOrWhiteSpace(database))
        {
            parts.TryGetValue("Initial Catalog", out database);
        }

        return $"Server={server ?? "?"};Database={database ?? "?"};Password=***;";
    }
}
