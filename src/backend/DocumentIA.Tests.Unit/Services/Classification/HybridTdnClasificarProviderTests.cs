using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Services.Classification;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Moq;
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
                Entrada = new EntradaInput
                {
                    Documento = new System.IO.FileInfo("empty.pdf"),
                    Instrucciones = new InstruccionesInput()
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
                Entrada = new EntradaInput
                {
                    Documento = new System.IO.FileInfo("test.pdf"),
                    Instrucciones = new InstruccionesInput()
                },
                DatosNormalizados = new Dictionary<string, object>
                {
                    { "Markdown", markdown }
                }
            };
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
                ExtractedText = "ESCRITURA DE COMPRAVENTA Vendedor compra inmueble",
                DocumentName = "doc.pdf"
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
                ExtractedText = "PRESTAMO HIPOTECARIO Banco otorga hipoteca",
                DocumentName = "hipoteca.pdf"
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
                DocumentName = "negativo.pdf"
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
                DocumentName = "empty.pdf"
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
                DocumentName = "señales.pdf"
            };

            // Act
            var result = classifier.Classify(window);

            // Assert
            result.SeñalesEncontradas.Should().NotBeEmpty();
            result.SeñalesEncontradas.Should().ContainKey("escritura");
            result.SeñalesEncontradas.Should().ContainKey("compraventa");
        }
    }

    public class FoundryTdnRescueClassifierTests
    {
        private readonly Mock<ILogger<FoundryTdnRescueClassifier>> _loggerMock;

        public FoundryTdnRescueClassifierTests()
        {
            _loggerMock = new Mock<ILogger<FoundryTdnRescueClassifier>>();
        }

        [Fact]
        public async Task ClassifyAsync_WithValidText_ReturnsClassification()
        {
            // Arrange
            var classifier = new FoundryTdnRescueClassifier(_loggerMock.Object);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "ESCRITURA DE COMPRAVENTA",
                DocumentName = "test.pdf"
            };

            // Act
            var result = await classifier.ClassifyAsync(window, timeoutMs: 5000, maxRetries: 1);

            // Assert
            result.Should().NotBeNull();
            result.TipologiaDetectada.Should().NotBeEmpty();
            result.TipologiaDetectada.Should().NotBe("Desconocido");
        }

        [Fact]
        public async Task ClassifyAsync_RespectTimeout()
        {
            // Arrange
            var classifier = new FoundryTdnRescueClassifier(_loggerMock.Object);
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
            var classifier = new FoundryTdnRescueClassifier(_loggerMock.Object);
            var window = new DocumentClassificationWindow
            {
                ExtractedText = "test",
                DocumentName = "test.pdf"
            };

            // Act
            var result = await classifier.ClassifyAsync(window, timeoutMs: 5000, maxRetries: 1);

            // Assert
            result.ExitoDespuesIntento.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    public class HybridTdnClasificarProviderTests
    {
        private readonly Mock<ILogger<HybridTdnClasificarProvider>> _loggerMock;
        private readonly Mock<IClasificarDataProvider> _diProviderMock;
        private readonly Mock<ILogger<DocumentWindowExtractor>> _windowExtractorLoggerMock;
        private readonly Mock<ILogger<RuleBasedTdnClassifier>> _ruleClassifierLoggerMock;
        private readonly Mock<ILogger<FoundryTdnRescueClassifier>> _rescueClassifierLoggerMock;
        private readonly TelemetryClient _telemetryClient;

        public HybridTdnClasificarProviderTests()
        {
            _loggerMock = new Mock<ILogger<HybridTdnClasificarProvider>>();
            _diProviderMock = new Mock<IClasificarDataProvider>();
            _windowExtractorLoggerMock = new Mock<ILogger<DocumentWindowExtractor>>();
            _ruleClassifierLoggerMock = new Mock<ILogger<RuleBasedTdnClassifier>>();
            _rescueClassifierLoggerMock = new Mock<ILogger<FoundryTdnRescueClassifier>>();
            _telemetryClient = new TelemetryClient(new TelemetryConfiguration());
        }

        [Fact]
        public async Task ClasificarAsync_WithHighConfidenceRules_ReturnsRuleResult()
        {
            // Arrange
            var provider = CreateProviderWithHighRuleConfidence();
            var input = CreateTestClassificacionInput("ESCRITURA DE COMPRAVENTA");

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
            var input = CreateTestClassificacionInput("ESCRITURA DE COMPRAVENTA");

            // Act
            var result = await provider.ClasificarAsync(input);

            // Assert
            result.ContentExtraido.Should().NotBeNullOrEmpty();
            result.ContentExtraido.Should().Contain("COMPRAVENTA");
        }

        private HybridTdnClasificarProvider CreateProviderWithHighRuleConfidence()
        {
            var options = Microsoft.Extensions.Options.Options.Create(
                new HybridTdnOptions { RuleConfidenceThreshold = 0.60 });

            var windowExtractor = new DocumentWindowExtractor(_windowExtractorLoggerMock.Object);
            var ruleClassifier = new RuleBasedTdnClassifier(_ruleClassifierLoggerMock.Object);
            var rescueClassifier = new FoundryTdnRescueClassifier(_rescueClassifierLoggerMock.Object);

            return new HybridTdnClasificarProvider(
                _loggerMock.Object,
                _diProviderMock.Object,
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
            var rescueClassifier = new FoundryTdnRescueClassifier(_rescueClassifierLoggerMock.Object);

            return new HybridTdnClasificarProvider(
                _loggerMock.Object,
                _diProviderMock.Object,
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
                Entrada = new EntradaInput
                {
                    Documento = new System.IO.FileInfo("test.pdf"),
                    Instrucciones = new InstruccionesInput()
                },
                DatosNormalizados = new Dictionary<string, object>
                {
                    { "Markdown", markdown }
                }
            };
        }
    }
}
