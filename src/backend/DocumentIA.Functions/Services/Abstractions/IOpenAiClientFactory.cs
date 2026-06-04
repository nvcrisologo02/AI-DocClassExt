using DocumentIA.Core.Configuration;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services.Abstractions;

public interface IOpenAiClientFactory
{
    /// <summary>
    /// Create and validate OpenAI ChatClient for extraction model.
    /// </summary>
    ChatClient CreateClient(ExtractionModelConfig model);
}
