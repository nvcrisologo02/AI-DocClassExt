#nullable enable
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Abstractions;
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

namespace DocumentIA.Tests.Unit.Services.Classification;

/// <summary>
/// Verifica que <see cref="GptClasificarDataProvider"/> delega las llamadas a Azure OpenAI en
/// <see cref="IAzureOpenAIResilienceExecutor"/> y que, cuando el executor agota los reintentos
/// ante 429/5xx sostenidos, <see cref="RateLimitExhaustedException"/> se propaga sin ser capturada
/// (la decisión de qué hacer con ella corresponde a una capa superior: activity/orquestador).
/// </summary>
public class GptClasificarDataProviderResilienceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _registryPath;

    public GptClasificarDataProviderResilienceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"GptClasificarDataProviderResilienceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _registryPath = Path.Combine(_tempDirectory, "models.json");

        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""classification.gpt4o-mini-fallback"",
                    ""provider"": ""azure-openai"",
                    ""useAsFallback"": true,
                    ""endpoint"": ""https://fake-openai.openai.azure.com/"",
                    ""apiKey"": ""fake-api-key"",
                    ""authMode"": ""ApiKey"",
                    ""deploymentName"": ""gpt-4o-mini-test"",
                    ""timeoutSeconds"": 30,
                    ""maxTokens"": 150
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
    public async Task ClasificarAsync_WhenExecutorThrowsRateLimitExhausted_Propagates()
    {
        var resilience = new Mock<IAzureOpenAIResilienceExecutor>();
        resilience
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RateLimitExhaustedException("cuota agotada"));

        var sut = CreateProviderWithResilience(resilience.Object);
        var input = BuildMinimalClasificacionInput();

        var act = async () => await sut.ClasificarAsync(input, CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExhaustedException>();
    }

    // ========== Helpers ==========

    private GptClasificarDataProvider CreateProviderWithResilience(IAzureOpenAIResilienceExecutor resilience)
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();

        // BuildTdn1Catalog() resuelve ICatalogoTdnRepository vía scope. No hay familias
        // configuradas: basta con un catálogo vacío para poder llegar a la llamada a
        // CompleteChatAsync (Phase 1), que es donde interviene el executor de resiliencia.
        var catalogoRepositoryMock = new Mock<ICatalogoTdnRepository>();
        catalogoRepositoryMock
            .Setup(r => r.GetFamiliasTdnActivasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TdnCatalogItem>());

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ICatalogoTdnRepository)))
            .Returns(catalogoRepositoryMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(scopeMock.Object);

        var modelRegistryLoader = new ClassificationModelRegistryLoader(_registryPath);

        var tipologiaPromptBuilder = new ClassificationTipologiaPromptBuilder(
            memoryCache,
            scopeFactoryMock.Object,
            new Mock<ILogger<ClassificationTipologiaPromptBuilder>>().Object);

        var tipologiaConfigLoader = new TipologiaConfigLoader(memoryCache, scopeFactoryMock.Object);

        var telemetryClient = new TelemetryClient(new TelemetryConfiguration { DisableTelemetry = true });
        var promptTraceTelemetry = new PromptTraceTelemetryService(
            telemetryClient,
            Options.Create(new PromptTracingSettings { Enabled = false }),
            new Mock<ILogger<PromptTraceTelemetryService>>().Object);

        var promptProviderMock = new Mock<IClassificationPromptProvider>();
        promptProviderMock
            .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClassificationPromptSet
            {
                Phase1SystemPrompt = "system phase1",
                Phase1UserPrompt = "user phase1 {CONTEXT_PROMPT} {TDN1_CATALOG} {DOCUMENT_TEXT}",
                Phase2SystemPrompt = "system phase2",
                Phase2UserPrompt = "user phase2 {TDN1_CODE} {TDN2_CATALOG} {DOCUMENT_TEXT}",
                RestrictedSystemPrompt = GptClasificarDataProvider.RestriccionFasePlanaSystemPrompt,
                RestrictedUserPrompt = GptClasificarDataProvider.RestriccionFasePlanaUserPromptTemplate,
                Version = 1,
                Source = "Fallback"
            });

        return new GptClasificarDataProvider(
            modelRegistryLoader,
            tipologiaPromptBuilder,
            tipologiaConfigLoader,
            scopeFactoryMock.Object,
            Options.Create(new ClassificationRoutingSettings()),
            Options.Create(new PromptDefaultsSettings()),
            Options.Create(new ClassificationPromptsSettings()),
            promptProviderMock.Object,
            promptTraceTelemetry,
            resilience,
            new Mock<ILogger<GptClasificarDataProvider>>().Object);
    }

    private static ClasificacionInput BuildMinimalClasificacionInput()
    {
        return new ClasificacionInput
        {
            GenerarResumenPorDefecto = false,
            Entrada = new ContratoEntrada
            {
                Documento = new Documento
                {
                    Name = "test-document.pdf",
                    Content = new ContenidoDocumento { Base64 = "dGVzdA==" }
                },
                Instrucciones = new Instrucciones
                {
                    Classification = new ConfiguracionIA()
                }
            },
            DatosNormalizados = new Dictionary<string, object>
            {
                { "Markdown", "Contenido de prueba para clasificación." }
            }
        };
    }
}
