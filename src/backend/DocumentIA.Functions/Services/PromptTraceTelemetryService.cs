using System.Security.Cryptography;
using System.Text;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services;

public class PromptTraceTelemetryService
{
    private const string EventName = "Prompt.Trace";

    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<PromptTraceTelemetryService> _logger;
    private readonly PromptTracingSettings _settings;

    public PromptTraceTelemetryService(
        TelemetryClient telemetryClient,
        IOptions<PromptTracingSettings> settings,
        ILogger<PromptTraceTelemetryService> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
        _settings = settings.Value;
    }

    public void TrackPrompt(
        string provider,
        string operation,
        string tipologia,
        string modelKey,
        string deployment,
        string systemPrompt,
        string userPrompt)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        var safeTipologia = string.IsNullOrWhiteSpace(tipologia) ? "unknown" : tipologia;
        var safeModelKey = string.IsNullOrWhiteSpace(modelKey) ? "unknown" : modelKey;
        var safeDeployment = string.IsNullOrWhiteSpace(deployment) ? "unknown" : deployment;
        var safeSystemPrompt = systemPrompt ?? string.Empty;
        var safeUserPrompt = userPrompt ?? string.Empty;

        var properties = new Dictionary<string, string>
        {
            ["provider"] = provider,
            ["operation"] = operation,
            ["tipologia"] = safeTipologia,
            ["modelKey"] = safeModelKey,
            ["deployment"] = safeDeployment,
            ["systemPromptSha256"] = ComputeSha256(safeSystemPrompt),
            ["userPromptSha256"] = ComputeSha256(safeUserPrompt)
        };

        if (_settings.IncludePromptText)
        {
            properties["systemPromptSnippet"] = Truncate(safeSystemPrompt, _settings.MaxPromptTextChars);
            properties["userPromptSnippet"] = Truncate(safeUserPrompt, _settings.MaxPromptTextChars);
        }

        var metrics = new Dictionary<string, double>
        {
            ["systemPromptLength"] = safeSystemPrompt.Length,
            ["userPromptLength"] = safeUserPrompt.Length
        };

        _telemetryClient.TrackEvent(EventName, properties, metrics);

        _logger.LogInformation(
            "Prompt trace registrada. Provider={Provider}, Operation={Operation}, Tipologia={Tipologia}, ModelKey={ModelKey}, Deployment={Deployment}, SystemLen={SystemLen}, UserLen={UserLen}",
            provider,
            operation,
            safeTipologia,
            safeModelKey,
            safeDeployment,
            safeSystemPrompt.Length,
            safeUserPrompt.Length);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private static string Truncate(string value, int maxChars)
    {
        if (maxChars <= 0)
        {
            return string.Empty;
        }

        if (value.Length <= maxChars)
        {
            return value;
        }

        return value[..maxChars];
    }
}
