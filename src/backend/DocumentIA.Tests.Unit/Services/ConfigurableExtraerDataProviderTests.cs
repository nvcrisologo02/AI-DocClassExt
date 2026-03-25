#nullable enable
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Mocks;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentIA.Tests.Unit.Services;

public class ConfigurableExtraerDataProviderTests
{
    [Fact]
    public async Task ObtenerDatosAsync_ExtractionDisabled_ReturnsEmptyResultWithoutCallingProviders()
    {
        using var fixture = TestFixture.Create(minFieldsRatio: 0.5, fallbackEnabled: true, extractionEnabled: false);
        var sut = fixture.BuildSut();

        var result = await sut.ObtenerDatosAsync(fixture.CreateInput());

        result.Proveedor.Should().Be("none");
        result.Modelo.Should().Be("disabled");
        result.DatosExtraidos.Should().BeEmpty();

        fixture.AzureProvider.Verify(
            p => p.ObtenerDatosAsync(It.IsAny<ExtraccionInput>(), It.IsAny<CancellationToken>()),
            Times.Never);

        fixture.GptProvider.Verify(
            p => p.ObtenerDatosConFallbackAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ObtenerDatosAsync_CuException_ActivaFallbackGpt()
    {
        using var fixture = TestFixture.Create(minFieldsRatio: 0.5, fallbackEnabled: true);

        fixture.AzureProvider
            .Setup(p => p.ObtenerDatosAsync(It.IsAny<ExtraccionInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("CU down"));

        fixture.GptProvider
            .Setup(p => p.ObtenerDatosConFallbackAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtraccionResultado
            {
                Proveedor = "azure-openai",
                Modelo = "gpt-fallback",
                DatosExtraidos = new Dictionary<string, object>
                {
                    ["CampoA"] = "valor"
                }
            });

        var sut = fixture.BuildSut();

        var result = await sut.ObtenerDatosAsync(fixture.CreateInput());

        result.FallbackUsado.Should().BeTrue();
        result.FallbackRazon.Should().StartWith("exception:");
        fixture.GptProvider.Verify(
            p => p.ObtenerDatosConFallbackAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ObtenerDatosAsync_CuInsuficiente_ActivaFallbackGpt()
    {
        using var fixture = TestFixture.Create(minFieldsRatio: 0.90, fallbackEnabled: true);

        fixture.AzureProvider
            .Setup(p => p.ObtenerDatosAsync(It.IsAny<ExtraccionInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtraccionResultado
            {
                Proveedor = "azure-content-understanding",
                Modelo = "cu",
                DatosExtraidos = new Dictionary<string, object>
                {
                    ["CampoA"] = "v"
                },
                MarkdownExtraido = "contenido markdown"
            });

        fixture.GptProvider
            .Setup(p => p.ObtenerDatosConFallbackAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                "contenido markdown",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtraccionResultado
            {
                Proveedor = "azure-openai",
                Modelo = "gpt-fallback",
                DatosExtraidos = new Dictionary<string, object>
                {
                    ["CampoA"] = "v",
                    ["CampoB"] = "v2",
                    ["CampoC"] = "v3"
                }
            });

        var sut = fixture.BuildSut();

        var result = await sut.ObtenerDatosAsync(fixture.CreateInput());

        result.FallbackUsado.Should().BeTrue();
        result.FallbackRazon.Should().Contain("insufficient_fields");
        fixture.GptProvider.VerifyAll();
    }

    [Fact]
    public async Task ObtenerDatosAsync_CuSuficiente_NoActivaFallbackGpt()
    {
        using var fixture = TestFixture.Create(minFieldsRatio: 0.50, fallbackEnabled: true);

        var cuResult = new ExtraccionResultado
        {
            Proveedor = "azure-content-understanding",
            Modelo = "cu",
            DatosExtraidos = new Dictionary<string, object>
            {
                ["CampoA"] = "v",
                ["CampoB"] = "v2"
            }
        };

        fixture.AzureProvider
            .Setup(p => p.ObtenerDatosAsync(It.IsAny<ExtraccionInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cuResult);

        var sut = fixture.BuildSut();

        var result = await sut.ObtenerDatosAsync(fixture.CreateInput());

        result.Should().BeSameAs(cuResult);
        fixture.GptProvider.Verify(
            p => p.ObtenerDatosConFallbackAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class TestFixture : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _tipologiaId;

        public Mock<AzureContentUnderstandingProvider> AzureProvider { get; }
        public Mock<GptFallbackExtraerDataProvider> GptProvider { get; }
        public Mock<ILogger<ConfigurableExtraerDataProvider>> Logger { get; }

        private readonly TipologiaConfigLoader _tipologiaConfigLoader;
        private readonly PromptModelRegistryLoader _promptModelRegistryLoader;
        private readonly MockExtraerDataProvider _mockProvider;
        private readonly ExtractionRoutingSettings _routingSettings;
        private readonly GptFallbackExtraerSettings _fallbackSettings;

        private TestFixture(string tempDir, string tipologiaId, double minFieldsRatio, bool fallbackEnabled)
        {
            _tempDir = tempDir;
            _tipologiaId = tipologiaId;

            _tipologiaConfigLoader = new TipologiaConfigLoader(_tempDir);
            var promptRegistryPath = Path.Combine(_tempDir, "prompt.models.json");
            File.WriteAllText(promptRegistryPath, "{\"models\":[]}");
            _promptModelRegistryLoader = new PromptModelRegistryLoader(promptRegistryPath);
            _mockProvider = new MockExtraerDataProvider();

            var dummyRegistry = new ExtractionModelRegistryLoader(Path.Combine(_tempDir, "models.json"));
            var mapper = new ContentUnderstandingResultMapper();

            var azureSettings = Options.Create(new AzureContentUnderstandingSettings
            {
                Endpoint = "https://example.cognitiveservices.azure.com",
                ApiKey = "test",
                AuthMode = "ApiKey",
                DefaultProcessingLocation = "global"
            });

            AzureProvider = new Mock<AzureContentUnderstandingProvider>(
                MockBehavior.Strict,
                new Mock<ILogger<AzureContentUnderstandingProvider>>().Object,
                _tipologiaConfigLoader,
                dummyRegistry,
                mapper,
                azureSettings);

            var gptSettings = Options.Create(new GptFallbackExtraerSettings
            {
                Enabled = fallbackEnabled,
                Endpoint = "https://example.openai.azure.com",
                ApiKey = "test",
                AuthMode = "ApiKey",
                DeploymentName = "deployment-test",
                MinFieldsRatio = minFieldsRatio,
                Temperature = 0,
                MaxTokens = 256,
                TimeoutSeconds = 20
            });

            GptProvider = new Mock<GptFallbackExtraerDataProvider>(
                MockBehavior.Strict,
                gptSettings,
                new Mock<ILogger<GptFallbackExtraerDataProvider>>().Object);

            _routingSettings = new ExtractionRoutingSettings { DefaultProvider = "azure-content-understanding" };
            _fallbackSettings = gptSettings.Value;
            Logger = new Mock<ILogger<ConfigurableExtraerDataProvider>>();
        }

        public static TestFixture Create(double minFieldsRatio, bool fallbackEnabled, bool extractionEnabled = true)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "DocumentIA.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var tipologiaId = "tipologia.test";
            var tipologiaPath = Path.Combine(tempDir, $"{tipologiaId}.validation.json");

            var tipologiaConfig = new
            {
                tipologiaId,
                tipologiaNombre = "Tipologia de prueba",
                version = "1.0",
                isDefault = true,
                extraction = new
                {
                    enabled = extractionEnabled,
                    provider = "azure-content-understanding",
                    modelKey = "default",
                    autoMapUnmappedFields = true,
                    fieldMappings = Array.Empty<object>()
                },
                fields = new[]
                {
                    new { name = "CampoA", type = "string", required = true, rules = Array.Empty<object>() },
                    new { name = "CampoB", type = "string", required = false, rules = Array.Empty<object>() },
                    new { name = "CampoC", type = "string", required = false, rules = Array.Empty<object>() }
                }
            };

            File.WriteAllText(tipologiaPath, JsonSerializer.Serialize(tipologiaConfig));

            return new TestFixture(tempDir, tipologiaId, minFieldsRatio, fallbackEnabled);
        }

        public ConfigurableExtraerDataProvider BuildSut() =>
            new(
                _tipologiaConfigLoader,
                _mockProvider,
                AzureProvider.Object,
                GptProvider.Object,
                _promptModelRegistryLoader,
                Options.Create(_routingSettings),
                Options.Create(_fallbackSettings),
                Logger.Object);

        public ExtraccionInput CreateInput() => new()
        {
            Tipologia = _tipologiaId,
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
            }
        };

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
