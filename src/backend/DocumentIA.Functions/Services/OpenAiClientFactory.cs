using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using DocumentIA.Functions.Services.Abstractions;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

public class OpenAiClientFactory : IOpenAiClientFactory
{
    private readonly ILogger<OpenAiClientFactory> _logger;

    public OpenAiClientFactory(ILogger<OpenAiClientFactory> logger)
    {
        _logger = logger;
    }

    public ChatClient CreateClient(ExtractionModelConfig model)
    {
        ValidateModelConfiguration(model);

        AzureOpenAIClient azureClient;

        if (string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            azureClient = new AzureOpenAIClient(new Uri(model.Endpoint), new DefaultAzureCredential());
        }
        else
        {
            azureClient = new AzureOpenAIClient(
                new Uri(model.Endpoint),
                new AzureKeyCredential(model.ApiKey));
        }

        return azureClient.GetChatClient(model.DeploymentName);
    }

    private void ValidateModelConfiguration(ExtractionModelConfig model)
    {
        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            throw new InvalidOperationException(
                $"Extracción GPT: modelo '{model.Key}' requiere Endpoint configurado. Verifica la configuración de '{model.Key}' en appsettings/KeyVault.");
        }

        if (string.IsNullOrWhiteSpace(model.DeploymentName))
        {
            throw new InvalidOperationException(
                $"Extracción GPT: modelo '{model.Key}' requiere DeploymentName configurado. Verifica la configuración de '{model.Key}' en appsettings/KeyVault.");
        }

        if (!string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(model.ApiKey))
        {
            throw new InvalidOperationException(
                $"Extracción GPT: modelo '{model.Key}' requiere ApiKey configurada cuando AuthMode={model.AuthMode}. Verifica la configuración en appsettings/KeyVault (ej: Extraction:GptFallback:ApiKey).");
        }
    }
}
