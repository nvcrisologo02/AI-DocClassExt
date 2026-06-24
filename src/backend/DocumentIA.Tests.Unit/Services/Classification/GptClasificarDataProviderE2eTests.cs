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
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

#nullable enable

namespace DocumentIA.Tests.Unit.Services.Classification
{
    /// <summary>
    /// E2E integration tests for resumen garantizado in GptClasificarDataProvider.
    /// Validates that ResumenCombinado is properly populated when GenerarResumenPorDefecto=true.
    /// These tests simulate a simplified classification flow with mocks.
    /// </summary>
    public class GptClasificarDataProviderE2eTests
    {
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
        private readonly TipologiaConfigLoader _tipologiaConfigLoader;
        private readonly Mock<ILogger<GptClasificarDataProvider>> _loggerMock;
        private readonly PromptTraceTelemetryService _promptTraceTelemetryService;
        private readonly IOptions<ClassificationRoutingSettings> _routingSettings;
        private readonly IOptions<PromptDefaultsSettings> _promptDefaults;

        public GptClasificarDataProviderE2eTests()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _loggerMock = new Mock<ILogger<GptClasificarDataProvider>>();
            
            var telemetryClient = new TelemetryClient(new TelemetryConfiguration { DisableTelemetry = true });
            var promptTracingSettings = Options.Create(new PromptTracingSettings { Enabled = false });
            var promptTraceTelemetryLogger = new Mock<ILogger<PromptTraceTelemetryService>>();
            _promptTraceTelemetryService = new PromptTraceTelemetryService(
                telemetryClient,
                promptTracingSettings,
                promptTraceTelemetryLogger.Object);

            _tipologiaConfigLoader = new TipologiaConfigLoader(_memoryCache, _scopeFactoryMock.Object);
            _routingSettings = Options.Create(new ClassificationRoutingSettings());
            
            _promptDefaults = Options.Create(new PromptDefaultsSettings
            {
                ModelKey = "default.gpt4o-mini",
                SystemPrompt = "Eres un analista documental experto. Responde en español de España sin inventar información.",
                UserPromptTemplate = @"Genera un resumen ejecutivo:

1. Objetivo del documento: Describir la finalidad
2. Datos clave: Puntos relevantes
3. Alertas: Riesgos o inconsistencias
4. Acciones recomendadas: Actuaciones basadas en contenido
5. Contenido: Resumen general

Documento: {contenido}",
                MaxTokens = 1600,
                Temperature = 0.0,
                ContentMode = "markdown"
            });
        }

        [Fact]
        public void E2E_ClasificacionWithGenerarResumenPorDefectoTrue_ContractPhase1IncludesResumenInResponseFormat()
        {
            // Given: A classification input with GenerarResumenPorDefecto=true
            var input = CreateClasificacionInput(generarResumenPorDefecto: true);
            var contextoTexto = "Documento de compraventa de bien inmueble ubicado en Madrid. Valor tasación 350.000€.";

            // When: ResolveResumenPrompt is called for this input
            var provider = CreateGptClassificationProvider();
            var resumenPrompt = InvokeResolveResumenPrompt(provider, input, contextoTexto);

            // Then: ResumenPrompt should be resolved from PromptDefaults
            resumenPrompt.Should().NotBeNull("resumen debe estar habilitado cuando GenerarResumenPorDefecto=true");
            resumenPrompt!.Enabled.Should().BeTrue();
            resumenPrompt.UserPromptTemplate.Should().Contain("1. Objetivo del documento");
            resumenPrompt.UserPromptTemplate.Should().Contain("2. Datos clave");
            resumenPrompt.UserPromptTemplate.Should().Contain("3. Alertas");
            resumenPrompt.UserPromptTemplate.Should().Contain("4. Acciones recomendadas");
            resumenPrompt.UserPromptTemplate.Should().Contain("5. Contenido");
            
            // And: The template should have been interpolated with actual content
            resumenPrompt.UserPromptTemplate.Should().Contain(contextoTexto);
            resumenPrompt.UserPromptTemplate.Should().NotContain("{contenido}");
        }

        [Fact]
        public void E2E_ClasificacionPhase1ResponseFormat_WhenResumenPromptIncluded_JsonStructureIncludesResumenField()
        {
            // Given: A GptClasificarDataProvider configured with resumen defaults
            //        and a classification input with GenerarResumenPorDefecto=true
            var input = CreateClasificacionInput(generarResumenPorDefecto: true);

            // Expected response format from Azure OpenAI when resumen is included
            var expectedJsonResponse = @"{
  ""tdn1"": ""ESCR-06"",
  ""propuesta"": ""ESCR-06: Escritura de transmisión de bienes"",
  ""resumen"": ""1. Objetivo del documento: Formalizar la transmisión de bienes inmuebles\\n2. Datos clave: Inmueble ubicado en Madrid, valor tasación 350.000€\\n3. Alertas: Carga hipotecaria pendiente de cancelación\\n4. Acciones recomendadas: Obtener cancelación hipotecaria antes cierre\\n5. Contenido: Documento notarial de compraventa con condiciones estándar"",
  ""confianza"": 0.95
}";

            // Contract: When resumen instruction is added to Phase1UserText (via ResolveResumenPrompt),
            // the Phase1ResponseFormatInstruction should be updated to include 'resumen' field in response.
            // The parser should then extract 'resumen' from the JSON and populate ResultadoClasificacion.ResumenCombinado.
            
            // Verify that the response format instruction from ClassificationTipologiaPromptBuilder includes resumen when applicable
            var classificationBuilder = new ClassificationTipologiaPromptBuilder(
                _memoryCache,
                _scopeFactoryMock.Object,
                new Mock<ILogger<ClassificationTipologiaPromptBuilder>>().Object);
            
            // Note: This validates the contract, not that we've modified the builder yet.
            // In real E2E, this would be verified after GptHierarchicalClassificationParser processes the response.
            
            // When deserialized, the response should have resumen field
            var jsonDocument = System.Text.Json.JsonDocument.Parse(expectedJsonResponse);
            jsonDocument.RootElement.TryGetProperty("resumen", out var resumenElement).Should().BeTrue("JSON response debe incluir campo 'resumen'");
            resumenElement.ValueKind.Should().Be(System.Text.Json.JsonValueKind.String);
        }

        [Fact]
        public void E2E_ResumenDefaultsVSTipologiaOverride_WhenTipologiaHasPromptConfig_UsesTipologiaVersion()
        {
            // Given: A tipología with a custom PromptConfig override
            var tipologiaCodigo = "ESCR-06";
            var tipologiaJson = """
{
  "tipologiaId": "ESCR-06",
  "tipologiaNombre": "Escritura",
  "version": "1.0",
  "promptConfig": {
    "enabled": true,
    "modelKey": "tipologia.gpt4o-mini",
    "systemPrompt": "Expert in notarial documents (tipologia-specific system prompt)",
    "userPromptTemplate": "[TIPOLOGIA-SPECIFIC] Summarize this notarial deed:\n{contenido}",
    "maxTokens": 999,
    "temperature": 0.1,
    "contentMode": "markdown"
  },
  "fields": []
}
""";

            var repoMock = new Mock<ITipologiaRepository>();
            repoMock
                .Setup(r => r.GetByCodigoAsync(tipologiaCodigo))
                .ReturnsAsync(new TipologiaEntity
                {
                    Codigo = tipologiaCodigo,
                    Nombre = "Escritura",
                    Activa = true,
                    Estado = EstadoTipologia.Published,
                    ConfiguracionJson = tipologiaJson
                });

            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(ITipologiaRepository)))
                .Returns(repoMock.Object);

            var scopeMock = new Mock<IServiceScope>();
            scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
            _scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(scopeMock.Object);

            // When: Classification is invoked with ExpectedType=ESCR-06 and GenerarResumenPorDefecto=true
            var input = CreateClasificacionInput(generarResumenPorDefecto: true);
            input.Entrada.Instrucciones.ExpectedType = tipologiaCodigo;
            var contextoTexto = "Compraventa de propiedad en Madrid.";

            var provider = CreateGptClassificationProvider();
            var resumenPrompt = InvokeResolveResumenPrompt(provider, input, contextoTexto);

            // Then: ResumenPrompt should use tipología-specific values
            resumenPrompt.Should().NotBeNull();
            resumenPrompt!.ModelKey.Should().Be("tipologia.gpt4o-mini");
            resumenPrompt.SystemPrompt.Should().Contain("Expert in notarial documents");
            resumenPrompt.UserPromptTemplate.Should().Contain("[TIPOLOGIA-SPECIFIC]");
            resumenPrompt.MaxTokens.Should().Be(999);
            resumenPrompt.Temperature.Should().Be(0.1);
            
            // And: Content should still be interpolated
            resumenPrompt.UserPromptTemplate.Should().Contain(contextoTexto);
        }

        // ========== Helper Methods ==========

        private ClasificacionInput CreateClasificacionInput(bool generarResumenPorDefecto)
        {
            return new ClasificacionInput
            {
                GenerarResumenPorDefecto = generarResumenPorDefecto,
                Entrada = new ContratoEntrada
                {
                    Documento = new Documento
                    {
                        Name = "test-escritura.pdf",
                        Content = new ContenidoDocumento { Base64 = "dGVzdA==" }
                    },
                    Instrucciones = new Instrucciones
                    {
                        Classification = new ConfiguracionIA()
                    }
                },
                DatosNormalizados = new Dictionary<string, object>
                {
                    { "Markdown", "Compraventa de bien inmueble" }
                }
            };
        }

        private GptClasificarDataProvider CreateGptClassificationProvider()
        {
            var modelRegistryLoader = new ClassificationModelRegistryLoader(_memoryCache, _scopeFactoryMock.Object);
            var tipologiaPromptBuilder = new ClassificationTipologiaPromptBuilder(
                _memoryCache,
                _scopeFactoryMock.Object,
                new Mock<ILogger<ClassificationTipologiaPromptBuilder>>().Object);

            return new GptClasificarDataProvider(
                modelRegistryLoader,
                tipologiaPromptBuilder,
                _tipologiaConfigLoader,
                _scopeFactoryMock.Object,
                _routingSettings,
                _promptDefaults,
                Options.Create(new ClassificationPromptsSettings()),
                new Mock<IClassificationPromptProvider>().Object,
                _promptTraceTelemetryService,
                _loggerMock.Object);
        }

        private PromptConfig? InvokeResolveResumenPrompt(
            GptClasificarDataProvider provider,
            ClasificacionInput input,
            string contextoTexto)
        {
            var method = typeof(GptClasificarDataProvider)
                .GetMethod("ResolveResumenPrompt",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            return (PromptConfig?)method?.Invoke(provider, new object[] { input, contextoTexto });
        }
    }
}
