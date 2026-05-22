#nullable enable
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Mocks;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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

        fixture.DirectGptProvider.Verify(
            p => p.ObtenerDatosAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ObtenerDatosAsync_GptDirecto_UsaProveedorDirectoSinFallback()
    {
        using var fixture = TestFixture.Create(minFieldsRatio: 0.5, fallbackEnabled: true, extractionProvider: "azure-openai");

        fixture.DirectGptProvider
            .Setup(p => p.ObtenerDatosAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtraccionResultado
            {
                Proveedor = "azure-openai",
                Modelo = "gpt-direct",
                DatosExtraidos = new Dictionary<string, object>
                {
                    ["CampoA"] = "valor"
                }
            });

        var sut = fixture.BuildSut();

        var result = await sut.ObtenerDatosAsync(fixture.CreateInput());

        result.Proveedor.Should().Be("azure-openai");

        fixture.DirectGptProvider.Verify(
            p => p.ObtenerDatosAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        fixture.GptProvider.Verify(
            p => p.ObtenerDatosConFallbackAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        fixture.AzureProvider.Verify(
            p => p.ObtenerDatosAsync(It.IsAny<ExtraccionInput>(), It.IsAny<CancellationToken>()),
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
        result.FallbackRazon.Should().Contain("insufficient_extraction");
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
            ConfianzaExtraccion = 0.95,
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

    [Fact]
    public async Task ObtenerDatosAsync_InstruccionesLegacyRellenaEspecificosYPrimaSobreTipologia()
    {
        using var fixture = TestFixture.Create(
            minFieldsRatio: 0.5,
            fallbackEnabled: true,
            tipExtracUmbralFallback: 0.95,
            tipExtracUmbralFallbackCompletitud: 0.90,
            tipExtracUmbralFallbackConfianza: 0.90);

        var input = fixture.CreateInput();
        input.UmbralFallbackEfectivo = 0.80;
        input.UmbralFallbackEfectivoCompletitud = 0.80;
        input.UmbralFallbackEfectivoConfianza = 0.80;

        var cuResult = new ExtraccionResultado
        {
            Proveedor = "azure-content-understanding",
            Modelo = "cu",
            ConfianzaExtraccion = 0.85,
            DatosExtraidos = new Dictionary<string, object>
            {
                ["CampoA"] = "v",
                ["CampoB"] = "v2",
                ["CampoC"] = "v3"
            }
        };

        fixture.AzureProvider
            .Setup(p => p.ObtenerDatosAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cuResult);

        var sut = fixture.BuildSut();

        var result = await sut.ObtenerDatosAsync(input);

        result.Should().BeSameAs(cuResult);
        fixture.GptProvider.Verify(
            p => p.ObtenerDatosConFallbackAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ObtenerDatosAsync_InstruccionesEspecificoPrimaSobreLegacyEnSuCriterio()
    {
        using var fixture = TestFixture.Create(
            minFieldsRatio: 0.5,
            fallbackEnabled: true,
            tipExtracUmbralFallback: 0.70,
            tipExtracUmbralFallbackCompletitud: 0.70,
            tipExtracUmbralFallbackConfianza: 0.70);

        var input = fixture.CreateInput();
        input.UmbralFallbackEfectivo = 0.80;
        input.UmbralFallbackEfectivoCompletitud = 0.80;
        input.UmbralFallbackEfectivoConfianza = 0.92;

        fixture.AzureProvider
            .Setup(p => p.ObtenerDatosAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtraccionResultado
            {
                Proveedor = "azure-content-understanding",
                Modelo = "cu",
                ConfianzaExtraccion = 0.85,
                DatosExtraidos = new Dictionary<string, object>
                {
                    ["CampoA"] = "v",
                    ["CampoB"] = "v2",
                    ["CampoC"] = "v3"
                }
            });

        fixture.GptProvider
            .Setup(p => p.ObtenerDatosConFallbackAsync(
                input,
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtraccionResultado
            {
                Proveedor = "azure-openai",
                Modelo = "gpt-fallback",
                DatosExtraidos = new Dictionary<string, object>
                {
                    ["CampoA"] = "v"
                }
            });

        var sut = fixture.BuildSut();

        var result = await sut.ObtenerDatosAsync(input);

        result.FallbackUsado.Should().BeTrue();
        result.FallbackRazon.Should().Contain("conf=0.850<0.920");
        fixture.GptProvider.VerifyAll();
    }

    [Fact]
    public async Task ObtenerDatosAsync_SinInstruccionesUsaTipologiaEspecificoAntesQueLegacy()
    {
        using var fixture = TestFixture.Create(
            minFieldsRatio: 0.5,
            fallbackEnabled: true,
            tipExtracUmbralFallback: 0.80,
            tipExtracUmbralFallbackCompletitud: 0.80,
            tipExtracUmbralFallbackConfianza: 0.90);

        var input = fixture.CreateInput();
        input.UmbralFallbackEfectivo = 0.80;
        input.UmbralFallbackEfectivoCompletitud = null;
        input.UmbralFallbackEfectivoConfianza = null;

        fixture.AzureProvider
            .Setup(p => p.ObtenerDatosAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtraccionResultado
            {
                Proveedor = "azure-content-understanding",
                Modelo = "cu",
                ConfianzaExtraccion = 0.85,
                DatosExtraidos = new Dictionary<string, object>
                {
                    ["CampoA"] = "v",
                    ["CampoB"] = "v2",
                    ["CampoC"] = "v3"
                }
            });

        fixture.GptProvider
            .Setup(p => p.ObtenerDatosConFallbackAsync(
                input,
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtraccionResultado
            {
                Proveedor = "azure-openai",
                Modelo = "gpt-fallback",
                DatosExtraidos = new Dictionary<string, object>
                {
                    ["CampoA"] = "v"
                }
            });

        var sut = fixture.BuildSut();

        var result = await sut.ObtenerDatosAsync(input);

        result.FallbackUsado.Should().BeTrue();
        result.FallbackRazon.Should().Contain("conf=0.850<0.900");
        fixture.GptProvider.VerifyAll();
    }

    private sealed class TestFixture : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _tipologiaId;

        public Mock<AzureContentUnderstandingProvider> AzureProvider { get; }
        public Mock<GptDirectExtraerDataProvider> DirectGptProvider { get; }
        public Mock<GptFallbackExtraerDataProvider> GptProvider { get; }
        public Mock<ILogger<ConfigurableExtraerDataProvider>> Logger { get; }

        private readonly TipologiaConfigLoader _tipologiaConfigLoader;
        private readonly ExtractionModelRegistryLoader _extractionModelRegistryLoader;
        private readonly PromptModelRegistryLoader _promptModelRegistryLoader;
        private readonly MockExtraerDataProvider _mockProvider;
        private readonly ExtractionRoutingSettings _routingSettings;

        private TestFixture(string tempDir, string tipologiaId, double minFieldsRatio, bool fallbackEnabled)
        {
            _tempDir = tempDir;
            _tipologiaId = tipologiaId;

            _tipologiaConfigLoader = CreateLoaderFromTempDirectory(_tempDir);
            var extractionRegistryPath = Path.Combine(_tempDir, "extraction.models.json");
            var extractionRegistry = fallbackEnabled
                ? new
                {
                    models = new object[]
                    {
                        new
                        {
                            key = "default.cu",
                            provider = "azure-content-understanding",
                            isDefault = true,
                            endpoint = "https://example.cu.azure.com",
                            apiKey = "test",
                            authMode = "ApiKey",
                            analyzerId = "analyzer-default",
                            processingLocation = "global",
                            minFieldsRatio = minFieldsRatio
                        },
                        new
                        {
                            key = "direct.gpt",
                            provider = "azure-openai",
                            endpoint = "https://example.openai.azure.com",
                            apiKey = "test",
                            authMode = "ApiKey",
                            deploymentName = "deployment-direct",
                            minFieldsRatio = minFieldsRatio,
                            temperature = 0.0,
                            maxTokens = 256,
                            timeoutSeconds = 20
                        },
                        new
                        {
                            key = "fallback.gpt",
                            provider = "azure-openai",
                            useAsFallback = true,
                            endpoint = "https://example.openai.azure.com",
                            apiKey = "test",
                            authMode = "ApiKey",
                            deploymentName = "deployment-test",
                            minFieldsRatio = minFieldsRatio,
                            temperature = 0.0,
                            maxTokens = 256,
                            timeoutSeconds = 20
                        }
                    }
                }
                : new
                {
                    models = new object[]
                    {
                        new
                        {
                            key = "default.cu",
                            provider = "azure-content-understanding",
                            isDefault = true,
                            endpoint = "https://example.cu.azure.com",
                            apiKey = "test",
                            authMode = "ApiKey",
                            analyzerId = "analyzer-default",
                            processingLocation = "global",
                            minFieldsRatio = minFieldsRatio
                        },
                        new
                        {
                            key = "direct.gpt",
                            provider = "azure-openai",
                            endpoint = "https://example.openai.azure.com",
                            apiKey = "test",
                            authMode = "ApiKey",
                            deploymentName = "deployment-direct",
                            minFieldsRatio = minFieldsRatio,
                            temperature = 0.0,
                            maxTokens = 256,
                            timeoutSeconds = 20
                        }
                    }
                };
            File.WriteAllText(extractionRegistryPath, JsonSerializer.Serialize(extractionRegistry));
            _extractionModelRegistryLoader = new ExtractionModelRegistryLoader(extractionRegistryPath);

            var promptRegistryPath = Path.Combine(_tempDir, "prompt.models.json");
            File.WriteAllText(promptRegistryPath, "{\"models\":[]}");
            _promptModelRegistryLoader = new PromptModelRegistryLoader(promptRegistryPath);
            _mockProvider = new MockExtraerDataProvider();

            var mapper = new ContentUnderstandingResultMapper();

            AzureProvider = new Mock<AzureContentUnderstandingProvider>(
                MockBehavior.Strict,
                new Mock<ILogger<AzureContentUnderstandingProvider>>().Object,
                _tipologiaConfigLoader,
                _extractionModelRegistryLoader,
                mapper,
                new Mock<IBlobStorageService>().Object);

            GptProvider = new Mock<GptFallbackExtraerDataProvider>(
                MockBehavior.Strict,
                _extractionModelRegistryLoader,
                new Mock<ILogger<GptFallbackExtraerDataProvider>>().Object);

            DirectGptProvider = new Mock<GptDirectExtraerDataProvider>(
                MockBehavior.Strict,
                GptProvider.Object,
                _extractionModelRegistryLoader,
                new Mock<ILogger<GptDirectExtraerDataProvider>>().Object);

            _routingSettings = new ExtractionRoutingSettings { DefaultProvider = "azure-content-understanding" };
            Logger = new Mock<ILogger<ConfigurableExtraerDataProvider>>();
        }

        private static TipologiaConfigLoader CreateLoaderFromTempDirectory(string tempDir)
        {
            var repository = new Mock<ITipologiaRepository>();
            repository
                .Setup(x => x.GetByCodigoAsync(It.IsAny<string>()))
                .ReturnsAsync((string codigo) =>
                {
                    var configPath = Path.Combine(tempDir, $"{codigo}.validation.json");
                    if (!File.Exists(configPath))
                    {
                        return null;
                    }

                    return new TipologiaEntity
                    {
                        Codigo = codigo,
                        Activa = true,
                        Estado = EstadoTipologia.Published,
                        ConfiguracionJson = File.ReadAllText(configPath)
                    };
                });

            var provider = new Mock<IServiceProvider>();
            provider
                .Setup(x => x.GetService(typeof(ITipologiaRepository)))
                .Returns(repository.Object);

            var scope = new Mock<IServiceScope>();
            scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

            var cache = new MemoryCache(new MemoryCacheOptions());
            return new TipologiaConfigLoader(cache, scopeFactory.Object);
        }

        public static TestFixture Create(
            double minFieldsRatio,
            bool fallbackEnabled,
            bool extractionEnabled = true,
            string extractionProvider = "azure-content-understanding",
            double? tipExtracUmbralFallback = null,
            double? tipExtracUmbralFallbackCompletitud = null,
            double? tipExtracUmbralFallbackConfianza = null)
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
                    provider = extractionProvider,
                    modelKey = extractionProvider == "azure-openai" ? "direct.gpt" : "default.cu",
                    autoMapUnmappedFields = true,
                    fieldMappings = Array.Empty<object>()
                },
                confidenceConfig = new
                {
                    extracUmbralFallback = tipExtracUmbralFallback,
                    extracUmbralFallbackCompletitud = tipExtracUmbralFallbackCompletitud,
                    extracUmbralFallbackConfianza = tipExtracUmbralFallbackConfianza
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
                null!,  // diExtraerProvider — no usado en estos tests (ruta CU/GPT)
                DirectGptProvider.Object,
                GptProvider.Object,
                _extractionModelRegistryLoader,
                _promptModelRegistryLoader,
                Options.Create(_routingSettings),
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
