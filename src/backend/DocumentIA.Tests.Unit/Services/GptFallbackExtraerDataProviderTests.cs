#nullable enable
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenAI.Chat;
using Xunit;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// AB#100130: cuando la llamada al modelo GPT (fallback o directa) agota su propio
/// <c>TimeoutSeconds</c>, la cancelación NO debe propagarse como error técnico hacia
/// ExtraerActivity/el orquestador. Debe convertirse en un resultado de extracción controlado
/// (sin datos, ConfianzaExtraccion=0, FallbackRazon=RazonExtraccionTimeout), de forma análoga a
/// cómo AzureContentUnderstandingProvider convierte su hard timeout en CuExtraccionException para
/// activar el fallback en lugar de reventar la activity.
///
/// Distingue explícitamente ese timeout propio (cts.CancelAfter interno) de una cancelación
/// solicitada por el caller (token externo cancelado), que sí debe propagarse sin enmascarar.
/// </summary>
public class GptFallbackExtraerDataProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _registryPath;

    public GptFallbackExtraerDataProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"GptFallbackExtraerDataProviderTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _registryPath = Path.Combine(_tempDir, "extraction.models.json");
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""fallback.gpt"",
                    ""provider"": ""azure-openai"",
                    ""useAsFallback"": true,
                    ""endpoint"": ""https://fake-openai.openai.azure.com/"",
                    ""apiKey"": ""fake-api-key"",
                    ""authMode"": ""ApiKey"",
                    ""deploymentName"": ""gpt-4o-mini-test"",
                    ""timeoutSeconds"": 1,
                    ""maxTokens"": 150
                },
                {
                    ""key"": ""direct.gpt"",
                    ""provider"": ""azure-openai"",
                    ""endpoint"": ""https://fake-openai.openai.azure.com/"",
                    ""apiKey"": ""fake-api-key"",
                    ""authMode"": ""ApiKey"",
                    ""deploymentName"": ""gpt-4o-mini-direct-test"",
                    ""timeoutSeconds"": 1,
                    ""maxTokens"": 150
                }
            ]
        }");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task ObtenerDatosConFallbackAsync_CuandoLaLlamadaAgotaSuPropioTimeout_DevuelveResultadoControladoSinLanzar()
    {
        var sut = CreateSut();
        var input = BuildInput();
        var tipologiaConfig = BuildTipologiaConfig();

        var resultado = await sut.ObtenerDatosConFallbackAsync(
            input,
            tipologiaConfig,
            markdownContexto: "contenido de prueba",
            cancellationToken: CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado.FallbackRazon.Should().Be(GptFallbackExtraerDataProvider.RazonExtraccionTimeout);
        resultado.DatosExtraidos.Should().BeEmpty();
        resultado.ConfianzaExtraccion.Should().Be(0);
        resultado.FallbackUsado.Should().BeTrue();
    }

    [Fact]
    public async Task ObtenerDatosConModeloAsync_CuandoLaLlamadaDirectaAgotaSuPropioTimeout_DevuelveResultadoControladoSinLanzar()
    {
        var sut = CreateSut();
        var input = BuildInput();
        var tipologiaConfig = BuildTipologiaConfig();

        var resultado = await sut.ObtenerDatosConModeloAsync(
            input,
            tipologiaConfig,
            modelKey: "direct.gpt",
            markdownContexto: "contenido de prueba",
            cancellationToken: CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado.FallbackRazon.Should().Be(GptFallbackExtraerDataProvider.RazonExtraccionTimeout);
        resultado.DatosExtraidos.Should().BeEmpty();
        resultado.ConfianzaExtraccion.Should().Be(0);
        // Camino directo: FallbackUsado se corresponde con isFallback=false pasado internamente.
        resultado.FallbackUsado.Should().BeFalse();
    }

    [Fact]
    public async Task ObtenerDatosConFallbackAsync_CuandoElCallerCancelaExplicitamente_PropagaLaCancelacionSinEnmascarar()
    {
        var sut = CreateSut();
        var input = BuildInput();
        var tipologiaConfig = BuildTipologiaConfig();

        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel();

        var act = async () => await sut.ObtenerDatosConFallbackAsync(
            input,
            tipologiaConfig,
            markdownContexto: "contenido de prueba",
            cancellationToken: callerCts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ========== Helpers ==========

    private TimeoutSimulatingGptFallbackExtraerDataProvider CreateSut()
    {
        var modelRegistryLoader = new ExtractionModelRegistryLoader(_registryPath);

        var promptBuilderMock = new Mock<IGptPromptBuilder>();
        promptBuilderMock
            .Setup(p => p.BuildSystemPrompt(It.IsAny<PromptMode>(), It.IsAny<PromptConfig?>(), It.IsAny<PromptConfig?>()))
            .Returns("system prompt de prueba");
        promptBuilderMock
            .Setup(p => p.BuildFieldCatalog(It.IsAny<TipologiaValidationConfig>()))
            .Returns("catalogo de campos de prueba");

        var responseParserMock = new Mock<IGptJsonResponseParser>();

        var clientFactoryMock = new Mock<IOpenAiClientFactory>();
        clientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<ExtractionModelConfig>()))
            .Returns(CreateDummyChatClient());

        return new TimeoutSimulatingGptFallbackExtraerDataProvider(
            modelRegistryLoader,
            Options.Create(new PromptDefaultsSettings()),
            new Mock<ILogger<GptFallbackExtraerDataProvider>>().Object,
            promptBuilderMock.Object,
            responseParserMock.Object,
            clientFactoryMock.Object);
    }

    /// <summary>
    /// ChatClient real (sin llamadas de red hasta que se invoque CompleteChatAsync), construido solo
    /// para satisfacer el tipo de retorno de IOpenAiClientFactory.CreateClient. La llamada real queda
    /// interceptada por <see cref="TimeoutSimulatingGptFallbackExtraerDataProvider.InvokeChatCompletionAsync"/>.
    /// </summary>
    private static ChatClient CreateDummyChatClient()
    {
        var azureClient = new Azure.AI.OpenAI.AzureOpenAIClient(
            new Uri("https://fake-openai.openai.azure.com/"),
            new Azure.AzureKeyCredential("fake-api-key"));
        return azureClient.GetChatClient("gpt-4o-mini-test");
    }

    private static ExtraccionInput BuildInput() => new()
    {
        Tipologia = "tipologia.test",
        Entrada = new ContratoEntrada
        {
            Documento = new Documento
            {
                Name = "doc.pdf",
                Content = new ContenidoDocumento
                {
                    Base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("dummy-pdf"))
                }
            }
        },
        DatosNormalizados = new Dictionary<string, object>(),
        GenerarResumenPorDefecto = false
    };

    private static TipologiaValidationConfig BuildTipologiaConfig() => new()
    {
        TipologiaId = "tipologia.test",
        TipologiaNombre = "Tipologia de prueba",
        Fields = new List<FieldValidationConfig>
        {
            new() { Name = "CampoA", Type = "string", Required = true }
        }
    };

    /// <summary>
    /// Subclase que sustituye la invocación real al modelo por una cancelación determinista, para
    /// poder probar el manejo de timeout/cancelación sin depender de red real ni de temporizadores.
    /// </summary>
    private sealed class TimeoutSimulatingGptFallbackExtraerDataProvider : GptFallbackExtraerDataProvider
    {
        public TimeoutSimulatingGptFallbackExtraerDataProvider(
            ExtractionModelRegistryLoader modelRegistryLoader,
            IOptions<PromptDefaultsSettings> promptDefaults,
            ILogger<GptFallbackExtraerDataProvider> logger,
            IGptPromptBuilder promptBuilder,
            IGptJsonResponseParser responseParser,
            IOpenAiClientFactory clientFactory)
            : base(modelRegistryLoader, promptDefaults, logger, promptBuilder, responseParser, clientFactory)
        {
        }

        protected override Task<ClientResult<ChatCompletion>> InvokeChatCompletionAsync(
            ChatClient chatClient,
            List<ChatMessage> messages,
            ChatCompletionOptions options,
            CancellationToken cancellationToken)
        {
            // Simula lo que ocurre en producción cuando el cts ligado a model.TimeoutSeconds dispara:
            // el SDK de OpenAI/Azure lanza una OperationCanceledException con el token cancelado
            // (aquí, cts.Token, que es un token distinto del cancellationToken original del caller).
            throw new OperationCanceledException("Timeout simulado para pruebas.", cancellationToken);
        }
    }
}
