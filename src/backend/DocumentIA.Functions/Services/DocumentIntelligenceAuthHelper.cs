using Azure.Core;
using Azure.Identity;
using System.Net.Http.Headers;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Adds the correct auth header to an HttpRequestMessage for Azure Document Intelligence.
/// - AuthMode "ApiKey": adds Ocp-Apim-Subscription-Key header.
/// - AuthMode "DefaultAzureCredential": acquires a bearer token via Managed Identity.
/// </summary>
internal static class DocumentIntelligenceAuthHelper
{
    private static readonly string[] Scopes = ["https://cognitiveservices.azure.com/.default"];
    private static DefaultAzureCredential? _credential;
    private static readonly object _lock = new();

    public static async Task ApplyAuthAsync(
        HttpRequestMessage request,
        string authMode,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(authMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            var credential = GetCredential();
            var tokenContext = new TokenRequestContext(Scopes);
            var token = await credential.GetTokenAsync(tokenContext, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        else
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
        }
    }

    private static DefaultAzureCredential GetCredential()
    {
        if (_credential is null)
        {
            lock (_lock)
            {
                _credential ??= new DefaultAzureCredential();
            }
        }
        return _credential;
    }
}
