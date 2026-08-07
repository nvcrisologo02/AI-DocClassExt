#nullable enable
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Resilience;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenAI.Chat;
using Xunit;

namespace DocumentIA.Tests.Unit.Services.Prompt;

/// <summary>
/// Verifica que <see cref="OpenAIPromptDataProvider"/> delega la llamada a Azure OpenAI en
/// <see cref="IAzureOpenAIResilienceExecutor"/> y que, cuando el executor agota los reintentos
/// ante 429/5xx sostenidos, el prompt se degrada de forma graceful: no lanza la excepción,
/// sino que devuelve un <see cref="PromptResultado"/> con Error prefijado "rate_limit_exhausted:".
/// El prompt es enriquecimiento no bloqueante, por lo que nunca debe escalar el documento
/// a un estado de reintento.
/// </summary>
public class OpenAIPromptDataProviderResilienceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _registryPath;

    public OpenAIPromptDataProviderResilienceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"OpenAIPromptDataProviderResilienceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _registryPath = Path.Combine(_tempDirectory, "prompt-models.json");

        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""prompt.gpt4o-mini-test"",
                    ""provider"": ""azure-openai"",
                    ""endpoint"": ""https://fake-openai.openai.azure.com/"",
                    ""apiKey"": ""fake-api-key"",
                    ""authMode"": ""ApiKey"",
                    ""deploymentName"": ""gpt-4o-mini-test"",
                    ""timeoutSeconds"": 30
                }
            ]
        }");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public async Task EjecutarPromptAsync_WhenExecutorThrowsRateLimitExhausted_ReturnsGracefulErrorWithPrefix()
    {
        var resilience = new Mock<IAzureOpenAIResilienceExecutor>();
        resilience
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RateLimitExhaustedException("cuota agotada"));

        var sut = CreateProviderWithResilience(resilience.Object);
        var input = BuildMinimalPromptInput();

        var resultado = await sut.EjecutarPromptAsync(input, CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado.Error.Should().NotBeNullOrEmpty();
        resultado.Error!.Should().StartWith("rate_limit_exhausted:");
    }

    // ========== Helpers ==========

    private OpenAIPromptDataProvider CreateProviderWithResilience(IAzureOpenAIResilienceExecutor resilience)
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();

        // La tipología no está publicada en BD: LoadConfig lanza FileNotFoundException,
        // que el provider captura internamente y sustituye por un fallback mínimo.
        // El prompt se ejecuta igualmente porque el input trae un override completo (input.Prompt).
        var tipologiaRepositoryMock = new Mock<ITipologiaRepository>();
        tipologiaRepositoryMock
            .Setup(r => r.GetByCodigoAsync(It.IsAny<string>()))
            .ReturnsAsync((TipologiaEntity?)null);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ITipologiaRepository)))
            .Returns(tipologiaRepositoryMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(scopeMock.Object);

        var tipologiaConfigLoader = new TipologiaConfigLoader(memoryCache, scopeFactoryMock.Object);
        var promptModelRegistryLoader = new PromptModelRegistryLoader(_registryPath);

        var telemetryClient = new TelemetryClient(new TelemetryConfiguration { DisableTelemetry = true });
        var promptTraceTelemetry = new PromptTraceTelemetryService(
            telemetryClient,
            Options.Create(new PromptTracingSettings { Enabled = false }),
            new Mock<ILogger<PromptTraceTelemetryService>>().Object);

        return new OpenAIPromptDataProvider(
            tipologiaConfigLoader,
            promptModelRegistryLoader,
            Options.Create(new PromptDefaultsSettings()),
            promptTraceTelemetry,
            resilience,
            new Mock<ILogger<OpenAIPromptDataProvider>>().Object);
    }

    private static PromptActivityInput BuildMinimalPromptInput()
    {
        return new PromptActivityInput
        {
            Tipologia = "TIPOLOGIA-INEXISTENTE",
            MarkdownExtraido = "Contenido de prueba para el prompt.",
            DatosExtraidos = new Dictionary<string, object>(),
            Prompt = new PromptInstrucciones
            {
                ModelKey = "prompt.gpt4o-mini-test",
                SystemPrompt = "Eres un asistente de prueba.",
                UserPromptTemplate = "Resume: {contenido}",
                MaxTokens = 500,
                Temperature = 0.0,
                ContentMode = "markdown"
            }
        };
    }
}
