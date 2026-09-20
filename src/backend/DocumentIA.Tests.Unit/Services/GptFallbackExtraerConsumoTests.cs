#nullable enable
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
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
/// AB#100227: el proveedor de extraccion generativa debe trasladar el consumo de
/// tokens de la respuesta al contrato. Sin esto el mayor coste del pipeline queda
/// sin instrumentar.
/// </summary>
public class GptFallbackExtraerConsumoTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _registryPath;

    public GptFallbackExtraerConsumoTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"GptFallbackExtraerConsumoTests_{Guid.NewGuid():N}");
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
                    ""deploymentName"": ""gpt-4o-mini"",
                    ""timeoutSeconds"": 30,
                    ""maxTokens"": 2000
                }
            ]
        }");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Limpieza best-effort del directorio temporal.
        }
    }

    [Fact]
    public async Task ObtenerDatosConFallback_RegistraElConsumoDeLaRespuesta()
    {
        var sut = CreateSut(inputTokens: 1200, outputTokens: 300, cachedTokens: 720);

        var resultado = await sut.ObtenerDatosConFallbackAsync(
            BuildInput(),
            BuildTipologiaConfig(),
            markdownContexto: "contenido del documento");

        resultado.Consumos.Should().ContainSingle();

        var consumo = resultado.Consumos[0];
        consumo.Proveedor.Should().Be(ProveedoresIA.AzureOpenAI);
        consumo.Actividad.Should().Be(ActividadesIA.Extraer);
        consumo.Operacion.Should().Be("extraction.gpt.fallback");
        consumo.Modelo.Should().Be("gpt-4o-mini");
        consumo.TokensEntrada.Should().Be(1200);
        consumo.TokensSalida.Should().Be(300);
        // Los cacheados se guardan sin restar de la entrada: eso lo hace la calculadora.
        consumo.TokensEntradaCache.Should().Be(720);
    }

    [Fact]
    public async Task ObtenerDatosConFallbackYPrompt_RegistraUnUnicoConsumo()
    {
        // Una sola llamada resuelve extraccion y prompt a la vez: dos consumos
        // contarian el mismo gasto dos veces.
        var sut = CreateSut(inputTokens: 5000, outputTokens: 900);

        var resultado = await sut.ObtenerDatosConFallbackYPromptAsync(
            BuildInput(),
            BuildTipologiaConfig(),
            new PromptConfig { Enabled = true, UserPromptTemplate = "resume esto" },
            markdownContexto: "contenido del documento");

        resultado.Consumos.Should().ContainSingle();
        resultado.Consumos[0].Operacion.Should().Be("extraction.gpt.combined");
        resultado.Consumos[0].TokensEntrada.Should().Be(5000);
    }

    private RespondingGptFallbackExtraerDataProvider CreateSut(
        int inputTokens,
        int outputTokens,
        int cachedTokens = 0)
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
        responseParserMock
            .Setup(p => p.Parse(It.IsAny<string>(), It.IsAny<TipologiaValidationConfig>()))
            .Returns(new GptExtractionResponse
            {
                CamposExtraidos = new Dictionary<string, object> { ["CampoA"] = "valor" },
                ConfianzaExtraccionGpt = 0.9
            });

        var clientFactoryMock = new Mock<IOpenAiClientFactory>();
        clientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<ExtractionModelConfig>()))
            .Returns(CreateDummyChatClient());

        return new RespondingGptFallbackExtraerDataProvider(
            modelRegistryLoader,
            Options.Create(new PromptDefaultsSettings()),
            new Mock<ILogger<GptFallbackExtraerDataProvider>>().Object,
            promptBuilderMock.Object,
            responseParserMock.Object,
            clientFactoryMock.Object,
            inputTokens,
            outputTokens,
            cachedTokens);
    }

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
    /// Sustituye la invocacion real al modelo por una respuesta con bloque de uso
    /// construido con las factorias de prueba del propio SDK.
    /// </summary>
    private sealed class RespondingGptFallbackExtraerDataProvider : GptFallbackExtraerDataProvider
    {
        private readonly int _inputTokens;
        private readonly int _outputTokens;
        private readonly int _cachedTokens;

        public RespondingGptFallbackExtraerDataProvider(
            ExtractionModelRegistryLoader modelRegistryLoader,
            IOptions<PromptDefaultsSettings> promptDefaults,
            ILogger<GptFallbackExtraerDataProvider> logger,
            IGptPromptBuilder promptBuilder,
            IGptJsonResponseParser responseParser,
            IOpenAiClientFactory clientFactory,
            int inputTokens,
            int outputTokens,
            int cachedTokens)
            : base(modelRegistryLoader, promptDefaults, logger, promptBuilder, responseParser, clientFactory)
        {
            _inputTokens = inputTokens;
            _outputTokens = outputTokens;
            _cachedTokens = cachedTokens;
        }

        protected override Task<ClientResult<ChatCompletion>> InvokeChatCompletionAsync(
            ChatClient chatClient,
            List<ChatMessage> messages,
            ChatCompletionOptions options,
            CancellationToken cancellationToken)
        {
            var usage = OpenAIChatModelFactory.ChatTokenUsage(
                outputTokenCount: _outputTokens,
                inputTokenCount: _inputTokens,
                totalTokenCount: _inputTokens + _outputTokens,
                inputTokenDetails: OpenAIChatModelFactory.ChatInputTokenUsageDetails(
                    cachedTokenCount: _cachedTokens));

            var completion = OpenAIChatModelFactory.ChatCompletion(
                role: ChatMessageRole.Assistant,
                content: new ChatMessageContent("{\"CampoA\":\"valor\"}"),
                usage: usage);

            return Task.FromResult(ClientResult.FromValue(completion, new Mock<PipelineResponse>().Object));
        }
    }
}
