using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Classification;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

#nullable enable

namespace DocumentIA.Tests.Unit.Services.Classification
{

    public class DocumentWindowExtractorTests
    {
        private readonly Mock<ILogger<DocumentWindowExtractor>> _loggerMock;

        public DocumentWindowExtractorTests()
        {
            _loggerMock = new Mock<ILogger<DocumentWindowExtractor>>();
        }
        [Fact]
        public void ExtractWindow_WithMarkdownInDatos_ReturnsExtractedText()
        {
            // Arrange
            var extractor = new DocumentWindowExtractor(_loggerMock.Object);
            var input = CreateTestClassificacionInput("Test Markdown Content");

            // Act
            var window = extractor.ExtractWindow(input, maxCharacters: 100, pagesToInspect: 3);

            // Assert
            window.Should().NotBeNull();
            window.ExtractedText.Should().Contain("Test Markdown Content");
            window.PagesToInspect.Should().Be(3);
            window.DocumentName.Should().Be("test.pdf");
        }

        [Fact]
        public void ExtractWindow_WithEmptyDatos_ReturnsWindowWithoutText()
        {
            // Arrange
            var extractor = new DocumentWindowExtractor(_loggerMock.Object);
            var input = new ClasificacionInput
            {
                Entrada = new ContratoEntrada
                {
                    Documento = new Documento
                    {
                        Name = "empty.pdf",
                        Content = new ContenidoDocumento { Base64 = "dGVzdA==" }
                    },
                    Instrucciones = new Instrucciones()
                },
                DatosNormalizados = new Dictionary<string, object>()
            };

            // Act
            var window = extractor.ExtractWindow(input);

            // Assert
            window.Should().NotBeNull();
            window.ExtractedText.Should().BeNull();
        }

        [Fact]
        public void ExtractWindow_WithoutDatos_UsesNativePdfTextAsFallback()
        {
            var extractor = new DocumentWindowExtractor(_loggerMock.Object);
            var input = new ClasificacionInput
            {
                Entrada = new ContratoEntrada
                {
                    Documento = new Documento
                    {
                        Name = "native.pdf",
                        Content = new ContenidoDocumento { Base64 = BuildPdfBase64("ESCRITURA DE COMPRAVENTA") }
                    },
                    Instrucciones = new Instrucciones()
                },
                DatosNormalizados = new Dictionary<string, object>()
            };

            var window = extractor.ExtractWindow(input, maxCharacters: 200, pagesToInspect: 3);

            window.Should().NotBeNull();
            window.ExtractedText.Should().Contain("ESCRITURA DE COMPRAVENTA");
        }

        [Fact]
        public void ExtractWindow_RespectMaxCharacters()
        {
            // Arrange
            var extractor = new DocumentWindowExtractor(_loggerMock.Object);
            var longText = new string('A', 10000);
            var input = CreateTestClassificacionInput(longText);

            // Act
            var window = extractor.ExtractWindow(input, maxCharacters: 500);

            // Assert
            window.ExtractedText.Should().NotBeNull();
            window.ExtractedText!.Length.Should().Be(500);
        }

        private ClasificacionInput CreateTestClassificacionInput(string markdown)
        {
            return new ClasificacionInput
            {
                Entrada = new ContratoEntrada
                {
                    Documento = new Documento
                    {
                        Name = "test.pdf",
                        Content = new ContenidoDocumento { Base64 = "dGVzdA==" }
                    },
                    Instrucciones = new Instrucciones()
                },
                DatosNormalizados = new Dictionary<string, object>
                {
                    { "Markdown", markdown }
                }
            };
        }

        private static string BuildPdfBase64(string text)
        {
            var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(595, 842);
            page.AddText(text, 12, new PdfPoint(36, 806), font);
            return Convert.ToBase64String(builder.Build());
        }
    }

    public class RuleBasedTdnClassifierTests
    {
        private readonly Mock<ILogger<RuleBasedTdnClassifier>> _loggerMock;

        public RuleBasedTdnClassifierTests()
        {
            _loggerMock = new Mock<ILogger<RuleBasedTdnClassifier>>();
        }

        [Fact]
        public void Classify_WithCompraventa_DetectsCorrectTipologia()
        {
            // Arrange
            var classifier = new RuleBasedTdnClassifier(_loggerMock.Object);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "ESCRITURA DE COMPRAVENTA dacion en pago decreto de adjudicacion transmite dominio",
                DocumentName = "doc.pdf",
                TotalPaginas = 12,
                CharsTextoNativo = 900
            };

            // Act
            var result = classifier.Classify(window);

            // Assert
            result.Should().NotBeNull();
            result.TipologiaDetectada.Should().Be("escr.compraventa");
            result.Confianza.Should().BeGreaterThan(0.5);
        }

        [Fact]
        public void Classify_WithHipoteca_DetectsLoanTipologia()
        {
            // Arrange
            var classifier = new RuleBasedTdnClassifier(_loggerMock.Object);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "PRESTAMO HIPOTECARIO decreto de adjudicacion escritura dominio transmite hipoteca",
                DocumentName = "hipoteca.pdf",
                TotalPaginas = 9,
                CharsTextoNativo = 700
            };

            // Act
            var result = classifier.Classify(window);

            // Assert
            result.TipologiaDetectada.Should().Contain("prestamo");
            result.Confianza.Should().BeGreaterThan(0.5);
        }

        [Fact]
        public void Classify_WithNegativeSignals_ReducesConfidence()
        {
            // Arrange
            var classifier = new RuleBasedTdnClassifier(_loggerMock.Object);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "nota simple ibi desconocido",
                DocumentName = "negativo.pdf",
                TotalPaginas = 4,
                CharsTextoNativo = 120
            };

            // Act
            var result = classifier.Classify(window);

            // Assert
            result.Confianza.Should().BeLessThan(0.3);
        }

        [Fact]
        public void Classify_WithEmptyText_ReturnsDesconocido()
        {
            // Arrange
            var classifier = new RuleBasedTdnClassifier(_loggerMock.Object);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = null,
                DocumentName = "empty.pdf",
                TotalPaginas = 10,
                CharsTextoNativo = 0
            };

            // Act
            var result = classifier.Classify(window);

            // Assert
            result.TipologiaDetectada.Should().Be("Desconocido");
            result.Confianza.Should().Be(0.0);
        }

        [Fact]
        public void Classify_TracksSenalesEncontradas()
        {
            // Arrange
            var classifier = new RuleBasedTdnClassifier(_loggerMock.Object);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "escritura compraventa transmite",
                DocumentName = "señales.pdf",
                TotalPaginas = 7,
                CharsTextoNativo = 400
            };

            // Act
            var result = classifier.Classify(window);

            // Assert
            result.SeñalesEncontradas.Should().NotBeEmpty();
            result.SeñalesEncontradas.Should().ContainKey("escritura");
            result.SeñalesEncontradas.Should().ContainKey("compra");
        }

        [Fact]
        public void Classify_WithMetadataBonusForEscr29_PrioritizesTitularidadAnteriorSareb()
        {
            var config = new TipologiaClassificationDefinition
            {
                Codigo = "escr.titularidad-anterior.sareb",
                ClassificationConfig = new TipologiaClassificationConfig
                {
                    Priority = 45,
                    AllowAsFallback = false,
                    MinimumSignalScore = 0.15,
                    MinimumMarginOverSecond = 0.1,
                    StrongSignals = new List<string>(),
                    PositiveSignals = new List<string>(),
                    NegativeSignals = new List<string>(),
                    OutOfScopeSignals = new List<string>(),
                    MetadataBonus = new List<MetadataBonusDefinition>
                    {
                        new() { Condition = "TotalPaginas < 8 AND CharsTextoNativo < 300", Score = 0.25 }
                    }
                }
            };

            var provider = new InMemoryProfileProvider(new[] { TipologiaClassificationProfile.FromDefinition(config) });
            var classifier = new RuleBasedTdnClassifier(_loggerMock.Object, provider);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = string.Empty,
                DocumentName = "scan.pdf",
                TotalPaginas = 6,
                CharsTextoNativo = 120
            };

            var result = classifier.Classify(window);

            result.TipologiaDetectada.Should().Be("escr.titularidad-anterior.sareb");
            result.Confianza.Should().BeGreaterThan(0.15);
        }

        [Fact]
        public void Classify_WithImmediateTrigger_ReturnsDirectClassification()
        {
            var config = new TipologiaClassificationDefinition
            {
                Codigo = "escr.compraventa",
                ClassificationConfig = new TipologiaClassificationConfig
                {
                    Priority = 100,
                    AllowAsFallback = false,
                    MinimumSignalScore = 0.15,
                    MinimumMarginOverSecond = 0.1,
                    StrongSignals = new List<string> { "escritura de compraventa" },
                    PositiveSignals = new List<string>(),
                    NegativeSignals = new List<string>(),
                    OutOfScopeSignals = new List<string>(),
                    ImmediateClassificationTriggers = new ImmediateClassificationTriggersDefinition
                    {
                        Enabled = true,
                        Scope = "firstPage",
                        MaxCharsToScan = 600,
                        Triggers = new List<string> { "compraventa" },
                        ResultScore = 1.0,
                        VetoedByOutOfScope = true
                    }
                }
            };

            var provider = new InMemoryProfileProvider(new[] { TipologiaClassificationProfile.FromDefinition(config) });
            var classifier = new RuleBasedTdnClassifier(_loggerMock.Object, provider);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "COMPRAVENTA. escritura de compraventa otorgada por la parte vendedora",
                DocumentName = "immediate.pdf",
                TotalPaginas = 8,
                CharsTextoNativo = 500
            };

            var result = classifier.Classify(window);

            result.TipologiaDetectada.Should().Be("escr.compraventa");
            result.ClasificacionMetodo.Should().Be("immediateClassificationTrigger");
            result.Confianza.Should().Be(1.0);
            result.RouteToReview.Should().BeFalse();
            result.TriggerActivado.Should().Be("compraventa");
        }

        [Fact]
        public void Classify_WithOutOfScopeOnFirstPage_VetoesTipologia()
        {
            var config = new TipologiaClassificationDefinition
            {
                Codigo = "escr.compraventa",
                ClassificationConfig = new TipologiaClassificationConfig
                {
                    Priority = 100,
                    AllowAsFallback = false,
                    MinimumSignalScore = 0.15,
                    MinimumMarginOverSecond = 0.1,
                    StrongSignals = new List<string> { "escritura de compraventa" },
                    PositiveSignals = new List<string>(),
                    NegativeSignals = new List<string>(),
                    OutOfScopeSignals = new List<string> { "nota simple" },
                    ImmediateClassificationTriggers = new ImmediateClassificationTriggersDefinition
                    {
                        Enabled = true,
                        Scope = "firstPage",
                        MaxCharsToScan = 600,
                        Triggers = new List<string> { "compraventa" },
                        ResultScore = 1.0,
                        VetoedByOutOfScope = true
                    }
                }
            };

            var provider = new InMemoryProfileProvider(new[] { TipologiaClassificationProfile.FromDefinition(config) });
            var classifier = new RuleBasedTdnClassifier(_loggerMock.Object, provider);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "nota simple compraventa escritura de compraventa",
                DocumentName = "veto.pdf",
                TotalPaginas = 8,
                CharsTextoNativo = 500
            };

            var result = classifier.Classify(window);

            result.TipologiaDetectada.Should().Be("Desconocido");
            result.ClasificacionMetodo.Should().Be("indeterminate");
            result.RouteToReview.Should().BeTrue();
        }
    }

    public class FoundryTdnRescueClassifierTests
    {
        private readonly Mock<ILogger<FoundryTdnRescueClassifier>> _loggerMock;
        private readonly GptClasificarDataProvider _gptProvider;

        public FoundryTdnRescueClassifierTests()
        {
            _loggerMock = new Mock<ILogger<FoundryTdnRescueClassifier>>();
            _gptProvider = CreateGptProviderForTests();
        }

        [Fact]
        public async Task ClassifyAsync_WithValidText_ReturnsClassification()
        {
            // Arrange
            var classifier = new FoundryTdnRescueClassifier(_loggerMock.Object, _gptProvider);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "ESCRITURA DE COMPRAVENTA",
                DocumentName = "test.pdf"
            };

            // Act
            var result = await classifier.ClassifyAsync(window, timeoutMs: 100, maxRetries: 0);

            // Assert
            result.Should().NotBeNull();
            result.TipologiaDetectada.Should().NotBeEmpty();
            result.DocumentName.Should().Be("test.pdf");
        }

        [Fact]
        public async Task ClassifyAsync_RespectTimeout()
        {
            // Arrange
            var classifier = new FoundryTdnRescueClassifier(_loggerMock.Object, _gptProvider);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "test content",
                DocumentName = "test.pdf"
            };

            // Act
            var result = await classifier.ClassifyAsync(window, timeoutMs: 50, maxRetries: 0);

            // Assert - En caso de timeout muy corto, debería retornar sin error crítico
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ClassifyAsync_RecordsAttemptNumber()
        {
            // Arrange
            var classifier = new FoundryTdnRescueClassifier(_loggerMock.Object, _gptProvider);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "test",
                DocumentName = "test.pdf"
            };

            // Act
            var result = await classifier.ClassifyAsync(window, timeoutMs: 100, maxRetries: 0);

            // Assert
            result.ExitoDespuesIntento.Should().BeGreaterThanOrEqualTo(0);
        }

        private static GptClasificarDataProvider CreateGptProviderForTests()
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var modelRepoMock = new Mock<IModeloConfigRepository>();
            modelRepoMock
                .Setup(r => r.GetAllActivosByTipoAsync(TipoModelo.Clasificacion))
                .ReturnsAsync(new List<ModeloConfigEntity>
                {
                    new()
                    {
                        Key = "classification.gpt4o-mini-fallback",
                        Provider = "azure-openai",
                        Tipo = TipoModelo.Clasificacion,
                        Activo = true,
                        ConfiguracionJson = "{\"useAsFallback\":true,\"endpoint\":\"https://example.openai.azure.com/\",\"apiKey\":\"test\",\"authMode\":\"ApiKey\",\"deploymentName\":\"gpt-4o-mini\",\"timeoutSeconds\":1,\"maxTokens\":50}"
                    }
                });

            var tipologiaRepoMock = new Mock<ITipologiaRepository>();
            tipologiaRepoMock
                .Setup(r => r.GetAllPublishedAsync())
                .ReturnsAsync(new List<TipologiaEntity>
                {
                    new()
                    {
                        Nombre = "Compraventa",
                        Codigo = "escr.compraventa",
                        Activa = true,
                        Estado = EstadoTipologia.Published,
                        ConfiguracionJson = "{\"isDefault\":true,\"tipologiaId\":\"escr.compraventa\",\"tipologiaNombre\":\"Compraventa\",\"gptDescripcion\":\"Escritura de compraventa\"}"
                    }
                });

            var services = new ServiceCollection();
            services.AddSingleton(modelRepoMock.Object);
            services.AddSingleton(tipologiaRepoMock.Object);
            var serviceProvider = services.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var loader = new ClassificationModelRegistryLoader(memoryCache, scopeFactory);
            var promptBuilder = new ClassificationTipologiaPromptBuilder(
                memoryCache, 
                scopeFactory,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ClassificationTipologiaPromptBuilder>.Instance);
            var tipologiaConfigLoader = new TipologiaConfigLoader(memoryCache, scopeFactory);
            var promptTraceTelemetry = new PromptTraceTelemetryService(
                new TelemetryClient(new TelemetryConfiguration { DisableTelemetry = true }),
                Options.Create(new PromptTracingSettings { Enabled = false }),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PromptTraceTelemetryService>.Instance);

            return new GptClasificarDataProvider(
                loader,
                promptBuilder,
                tipologiaConfigLoader,
                scopeFactory,
                Options.Create(new ClassificationRoutingSettings()),
                Options.Create(new PromptDefaultsSettings()),
                promptTraceTelemetry,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GptClasificarDataProvider>.Instance);
        }
    }

    public class HybridTdnClasificarProviderTests
    {
        private readonly Mock<ILogger<HybridTdnClasificarProvider>> _loggerMock;
        private readonly Mock<IClasificarDataProvider> _diProviderMock;
        private readonly Mock<ILayoutMarkdownProvider> _layoutMarkdownProviderMock;
        private readonly Mock<ILogger<DocumentWindowExtractor>> _windowExtractorLoggerMock;
        private readonly Mock<ILogger<RuleBasedTdnClassifier>> _ruleClassifierLoggerMock;
        private readonly Mock<ILogger<FoundryTdnRescueClassifier>> _rescueClassifierLoggerMock;
        private readonly GptClasificarDataProvider _gptProvider;
        private readonly TelemetryClient _telemetryClient;

        public HybridTdnClasificarProviderTests()
        {
            _loggerMock = new Mock<ILogger<HybridTdnClasificarProvider>>();
            _diProviderMock = new Mock<IClasificarDataProvider>();
            _layoutMarkdownProviderMock = new Mock<ILayoutMarkdownProvider>();
            _windowExtractorLoggerMock = new Mock<ILogger<DocumentWindowExtractor>>();
            _ruleClassifierLoggerMock = new Mock<ILogger<RuleBasedTdnClassifier>>();
            _rescueClassifierLoggerMock = new Mock<ILogger<FoundryTdnRescueClassifier>>();
            _gptProvider = CreateGptProviderForTests();
            _telemetryClient = new TelemetryClient(new TelemetryConfiguration());
        }

        [Fact]
        public async Task ClasificarAsync_WithHighConfidenceRules_ReturnsRuleResult()
        {
            // Arrange
            var provider = CreateProviderWithHighRuleConfidence();
            var input = CreateTestClassificacionInput("ESCRITURA DE COMPRAVENTA dacion en pago decreto de adjudicacion transmite dominio");

            // Act
            var result = await provider.ClasificarAsync(input);

            // Assert
            result.Should().NotBeNull();
            result.Clasificador.Should().Be("RuleBasedTDN");
            result.Confianza.Should().BeGreaterThan(0.75);
        }

        [Fact]
        public async Task ClasificarAsync_WithLowRuleConfidence_FallbackToDI()
        {
            // Arrange
            var provider = CreateProviderWithLowRuleConfidence();
            var input = CreateTestClassificacionInput("vague text");

            _diProviderMock
                .Setup(d => d.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResultadoClasificacion
                {
                    TipologiaDetectada = "escr.titularidad.otro",
                    Confianza = 0.90,
                    ProveedorClasif = "DocumentIntelligence"
                });

            // Act
            var result = await provider.ClasificarAsync(input);

            // Assert
            result.Clasificador.Should().Be("DocumentIntelligence");
            _diProviderMock.Verify(d => d.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task ClasificarAsync_WithDIReturningRESTO_FallbackToRescue()
        {
            // Arrange
            var provider = CreateProviderWithLowRuleConfidence();
            var input = CreateTestClassificacionInput("unknown document");

            _diProviderMock
                .Setup(d => d.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResultadoClasificacion
                {
                    TipologiaDetectada = "RESTO",
                    Confianza = 0.45,
                    ProveedorClasif = "DocumentIntelligence"
                });

            // Act
            var result = await provider.ClasificarAsync(input);

            // Assert
            result.Clasificador.Should().Be("FoundryRescue");
        }

        [Fact]
        public async Task ClasificarAsync_WithDIBelowThreshold_FallbackToRescue()
        {
            // Arrange
            var provider = CreateProviderWithLowRuleConfidence();
            var input = CreateTestClassificacionInput("medium confidence");

            _diProviderMock
                .Setup(d => d.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResultadoClasificacion
                {
                    TipologiaDetectada = "escr.compraventa",
                    Confianza = 0.70, // Below 0.85 threshold
                    ProveedorClasif = "DocumentIntelligence"
                });

            // Act
            var result = await provider.ClasificarAsync(input);

            // Assert
            result.Clasificador.Should().Be("FoundryRescue");
        }

        [Fact]
        public async Task ClasificarAsync_MapsResultFieldsCorrectly()
        {
            // Arrange
            var provider = CreateProviderWithHighRuleConfidence();
            var input = CreateTestClassificacionInput("COMPRAVENTA test");

            // Act
            var result = await provider.ClasificarAsync(input);

            // Assert
            result.TipologiaDetectada.Should().NotBeNullOrEmpty();
            result.Confianza.Should().BeGreaterThanOrEqualTo(0);
            result.Confianza.Should().BeLessThanOrEqualTo(1.0);
            result.ProveedorClasif.Should().Be("HybridTDN");
        }

        [Fact]
        public async Task ClasificarAsync_PreservesContentExtraido()
        {
            // Arrange
            var provider = CreateProviderWithHighRuleConfidence();
            var input = CreateTestClassificacionInput("ESCRITURA DE COMPRAVENTA dacion en pago decreto de adjudicacion transmite dominio");

            // Act
            var result = await provider.ClasificarAsync(input);

            // Assert
            result.ContentExtraido.Should().NotBeNullOrEmpty();
            result.ContentExtraido.Should().Contain("COMPRAVENTA");
        }

        [Fact]
        public async Task ClasificarAsync_WithoutUsefulContext_InjectsMarkdownFromLayoutBeforeRules()
        {
            var provider = CreateProviderWithHighRuleConfidence();
            var input = new ClasificacionInput
            {
                Entrada = new ContratoEntrada
                {
                    Documento = new Documento
                    {
                        Name = "layout.pdf",
                        Content = new ContenidoDocumento { Base64 = "dGVzdA==" }
                    },
                    Instrucciones = new Instrucciones()
                },
                DatosNormalizados = new Dictionary<string, object>()
            };

            _layoutMarkdownProviderMock
                .Setup(p => p.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExtraerMarkdownLayoutResultado
                {
                    Markdown = "ESCRITURA DE COMPRAVENTA dacion en pago decreto de adjudicacion transmite dominio"
                });

            var result = await provider.ClasificarAsync(input);

            result.Clasificador.Should().Be("RuleBasedTDN");
            input.DatosNormalizados.Should().ContainKey("Markdown");
            _layoutMarkdownProviderMock.Verify(
                p => p.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _diProviderMock.Verify(d => d.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ClasificarAsync_WithExistingMarkdown_DoesNotCallLayoutProvider()
        {
            var provider = CreateProviderWithHighRuleConfidence();
            var input = CreateTestClassificacionInput("ESCRITURA DE COMPRAVENTA dacion en pago decreto de adjudicacion transmite dominio");

            var result = await provider.ClasificarAsync(input);

            result.Clasificador.Should().Be("RuleBasedTDN");
            _layoutMarkdownProviderMock.Verify(
                p => p.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ClasificarAsync_WithoutMarkdownButWithNativePdfText_UsesRuleBasedPath()
        {
            var provider = CreateProviderWithHighRuleConfidence();
            var input = new ClasificacionInput
            {
                Entrada = new ContratoEntrada
                {
                    Documento = new Documento
                    {
                        Name = "native.pdf",
                        Content = new ContenidoDocumento
                        {
                            Base64 = BuildPdfBase64("ESCRITURA DE COMPRAVENTA dacion en pago decreto de adjudicacion transmite dominio")
                        }
                    },
                    Instrucciones = new Instrucciones()
                },
                DatosNormalizados = new Dictionary<string, object>()
            };

            var result = await provider.ClasificarAsync(input);

            result.Clasificador.Should().Be("RuleBasedTDN");
            result.ContentExtraido.Should().Contain("COMPRAVENTA");
            _diProviderMock.Verify(d => d.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private HybridTdnClasificarProvider CreateProviderWithHighRuleConfidence()
        {
            var options = Microsoft.Extensions.Options.Options.Create(
                new HybridTdnOptions { RuleConfidenceThreshold = 0.60 });

            var windowExtractor = new DocumentWindowExtractor(_windowExtractorLoggerMock.Object);
            var ruleClassifier = new RuleBasedTdnClassifier(_ruleClassifierLoggerMock.Object);
            var rescueClassifier = new FoundryTdnRescueClassifier(_rescueClassifierLoggerMock.Object, _gptProvider);

            return new HybridTdnClasificarProvider(
                _loggerMock.Object,
                _diProviderMock.Object,
                _layoutMarkdownProviderMock.Object,
                windowExtractor,
                ruleClassifier,
                rescueClassifier,
                options,
                _telemetryClient);
        }

        private HybridTdnClasificarProvider CreateProviderWithLowRuleConfidence()
        {
            var options = Microsoft.Extensions.Options.Options.Create(
                new HybridTdnOptions { RuleConfidenceThreshold = 0.95 }); // Very high threshold

            var windowExtractor = new DocumentWindowExtractor(_windowExtractorLoggerMock.Object);
            var ruleClassifier = new RuleBasedTdnClassifier(_ruleClassifierLoggerMock.Object);
            var rescueClassifier = new FoundryTdnRescueClassifier(_rescueClassifierLoggerMock.Object, _gptProvider);

            return new HybridTdnClasificarProvider(
                _loggerMock.Object,
                _diProviderMock.Object,
                _layoutMarkdownProviderMock.Object,
                windowExtractor,
                ruleClassifier,
                rescueClassifier,
                options,
                _telemetryClient);
        }

        private ClasificacionInput CreateTestClassificacionInput(string markdown)
        {
            return new ClasificacionInput
            {
                Entrada = new ContratoEntrada
                {
                    Documento = new Documento
                    {
                        Name = "test.pdf",
                        Content = new ContenidoDocumento { Base64 = "dGVzdA==" }
                    },
                    Instrucciones = new Instrucciones()
                },
                DatosNormalizados = new Dictionary<string, object>
                {
                    { "Markdown", markdown }
                }
            };
        }

        private static GptClasificarDataProvider CreateGptProviderForTests()
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var modelRepoMock = new Mock<IModeloConfigRepository>();
            modelRepoMock
                .Setup(r => r.GetAllActivosByTipoAsync(TipoModelo.Clasificacion))
                .ReturnsAsync(new List<ModeloConfigEntity>
                {
                    new()
                    {
                        Key = "classification.gpt4o-mini-fallback",
                        Provider = "azure-openai",
                        Tipo = TipoModelo.Clasificacion,
                        Activo = true,
                        ConfiguracionJson = "{\"useAsFallback\":true,\"endpoint\":\"https://example.openai.azure.com/\",\"apiKey\":\"test\",\"authMode\":\"ApiKey\",\"deploymentName\":\"gpt-4o-mini\",\"timeoutSeconds\":1,\"maxTokens\":50}"
                    }
                });

            var tipologiaRepoMock = new Mock<ITipologiaRepository>();
            tipologiaRepoMock
                .Setup(r => r.GetAllPublishedAsync())
                .ReturnsAsync(new List<TipologiaEntity>
                {
                    new()
                    {
                        Nombre = "Compraventa",
                        Codigo = "escr.compraventa",
                        Activa = true,
                        Estado = EstadoTipologia.Published,
                        ConfiguracionJson = "{\"isDefault\":true,\"tipologiaId\":\"escr.compraventa\",\"tipologiaNombre\":\"Compraventa\",\"gptDescripcion\":\"Escritura de compraventa\"}"
                    }
                });

            var services = new ServiceCollection();
            services.AddSingleton(modelRepoMock.Object);
            services.AddSingleton(tipologiaRepoMock.Object);
            var serviceProvider = services.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            var loader = new ClassificationModelRegistryLoader(memoryCache, scopeFactory);
            var promptBuilder = new ClassificationTipologiaPromptBuilder(
                memoryCache, 
                scopeFactory,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ClassificationTipologiaPromptBuilder>.Instance);
            var tipologiaConfigLoader = new TipologiaConfigLoader(memoryCache, scopeFactory);
            var promptTraceTelemetry = new PromptTraceTelemetryService(
                new TelemetryClient(new TelemetryConfiguration { DisableTelemetry = true }),
                Options.Create(new PromptTracingSettings { Enabled = false }),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PromptTraceTelemetryService>.Instance);

            return new GptClasificarDataProvider(
                loader,
                promptBuilder,
                tipologiaConfigLoader,
                scopeFactory,
                Options.Create(new ClassificationRoutingSettings()),
                Options.Create(new PromptDefaultsSettings()),
                promptTraceTelemetry,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GptClasificarDataProvider>.Instance);
        }

        private static string BuildPdfBase64(string text)
        {
            var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(595, 842);
            page.AddText(text, 12, new PdfPoint(36, 806), font);
            return Convert.ToBase64String(builder.Build());
        }
    }

    internal sealed class InMemoryProfileProvider : ITipologiaClassificationProfileProvider
    {
        private readonly IReadOnlyList<TipologiaClassificationProfile> _profiles;

        public InMemoryProfileProvider(IEnumerable<TipologiaClassificationProfile> profiles)
        {
            _profiles = profiles.ToList();
        }

        public IReadOnlyList<TipologiaClassificationProfile> GetProfiles() => _profiles;
    }
}
