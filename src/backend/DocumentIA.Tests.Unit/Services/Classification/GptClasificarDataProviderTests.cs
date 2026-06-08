using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Data.Entities;
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
using Xunit;

#nullable enable

namespace DocumentIA.Tests.Unit.Services.Classification
{
    /// <summary>
    /// Test suite para ResolveResumenPrompt() en GptClasificarDataProvider.
    /// Verifica que el sistema resuelve correctamente el resumen garantizado
    /// cuando GenerarResumenPorDefecto=true.
    /// </summary>
    public class GptClasificarDataProviderTests
    {
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
        private readonly Mock<ILogger<GptClasificarDataProvider>> _loggerMock;
        private readonly PromptTraceTelemetryService _promptTraceTelemetryMock;
        private readonly IOptions<ClassificationRoutingSettings> _routingSettings;
        private readonly IOptions<PromptDefaultsSettings> _promptDefaults;
        private readonly GptClasificarDataProvider _provider;

        public GptClasificarDataProviderTests()
        {
            // Use real ClassificationModelRegistryLoader with memory cache
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _loggerMock = new Mock<ILogger<GptClasificarDataProvider>>();
            
            // Create real PromptTraceTelemetryService instead of mocking
            var telemetryClient = new TelemetryClient();
            var promptTracingSettings = Options.Create(new PromptTracingSettings { Enabled = false });
            var promptTraceTelemetryLogger = new Mock<ILogger<PromptTraceTelemetryService>>();
            _promptTraceTelemetryMock = new PromptTraceTelemetryService(
                telemetryClient,
                promptTracingSettings,
                promptTraceTelemetryLogger.Object);

            _routingSettings = Options.Create(new ClassificationRoutingSettings());
            
            _promptDefaults = Options.Create(new PromptDefaultsSettings
            {
                ModelKey = "default.gpt4o-mini",
                     SystemPrompt = "Eres un analista documental experto. Responde en espanol de Espana, sin inventar informacion y siguiendo estrictamente el formato solicitado.",
                     UserPromptTemplate = @"Genera un resumen ejecutivo del documento procesado siguiendo estrictamente estas instrucciones:

- Idioma: Espanol (Espana)
- Longitud maxima: 500 caracteres
- No inventar informacion ni inferir datos no presentes en el documento
- Ser claro, conciso y preciso
- No utilizar frases genericas ni vagas
- Evitar redundancias
- Priorizar informacion relevante para la toma de decisiones

Formato obligatorio (mantener este orden y estructura):

1. Objetivo del documento:
    Describir brevemente la finalidad del documento

2. Datos clave:
    Enumerar los puntos mas relevantes o informacion esencial

3. Alertas:
    Identificar riesgos, inconsistencias o aspectos criticos

4. Acciones recomendadas:
    Proponer actuaciones basadas unicamente en el contenido del documento

5. Contenido:
    Resumen general del contenido principal

Contenido del documento:
{contenido}",
                MaxTokens = 1600,
                Temperature = 0.0,
                ContentMode = "markdown"
            });

            // Create real instances of dependencies
            var modelRegistryLoader = new ClassificationModelRegistryLoader(_memoryCache, _scopeFactoryMock.Object);
            var tipologiaPromptBuilderLogger = new Mock<ILogger<ClassificationTipologiaPromptBuilder>>();
            var tipologiaPromptBuilder = new ClassificationTipologiaPromptBuilder(
                _memoryCache,
                _scopeFactoryMock.Object,
                tipologiaPromptBuilderLogger.Object);

            _provider = new GptClasificarDataProvider(
                modelRegistryLoader,
                tipologiaPromptBuilder,
                _scopeFactoryMock.Object,
                _routingSettings,
                _promptDefaults,
                _promptTraceTelemetryMock,
                _loggerMock.Object);
        }

        [Fact]
        public void ResolveResumenPrompt_WhenGenerarResumenPorDefectoIsFalse_ReturnsNull()
        {
            // Given: A ClasificacionInput with GenerarResumenPorDefecto=false
            var input = CreateClasificacionInput(generarResumenPorDefecto: false);
            var contextoTexto = "# Título\n\nEste es el contenido del documento.";

            // When: ResolveResumenPrompt is called
            var result = InvokeResolveResumenPrompt(input, contextoTexto);

            // Then: Should return null since resumen generation is disabled
            result.Should().BeNull();
        }

        [Fact]
        public void ResolveResumenPrompt_WhenUserPromptTemplateIsEmpty_ReturnsNull()
        {
            // Given: A ClasificacionInput with GenerarResumenPorDefecto=true
            //        and PromptDefaults with empty UserPromptTemplate
            var promptDefaultsEmpty = Options.Create(new PromptDefaultsSettings
            {
                ModelKey = "default.gpt4o-mini",
                SystemPrompt = "Sistema...",
                UserPromptTemplate = "", // Empty template
                MaxTokens = 1600,
                Temperature = 0.0,
                ContentMode = "markdown"
            });

            var providerWithEmptyTemplate = new GptClasificarDataProvider(
                new ClassificationModelRegistryLoader(_memoryCache, _scopeFactoryMock.Object),
                new ClassificationTipologiaPromptBuilder(
                    _memoryCache,
                    _scopeFactoryMock.Object,
                    new Mock<ILogger<ClassificationTipologiaPromptBuilder>>().Object),
                _scopeFactoryMock.Object,
                _routingSettings,
                promptDefaultsEmpty,
                _promptTraceTelemetryMock,
                _loggerMock.Object);

            var input = CreateClasificacionInput(generarResumenPorDefecto: true);
            var contextoTexto = "# Contenido";

            // When: ResolveResumenPrompt is called with empty template
            var result = InvokeResolveResumenPromptOnProvider(providerWithEmptyTemplate, input, contextoTexto);

            // Then: Should return null since template is empty
            result.Should().BeNull();
        }

        [Fact]
        public void ResolveResumenPrompt_WhenGenerarResumenPorDefectoIsTrue_ReturnsPromptConfigWithInterpolatedTemplate()
        {
            // Given: A ClasificacionInput with GenerarResumenPorDefecto=true
            //        and valid PromptDefaults with template containing {contenido}
            var input = CreateClasificacionInput(generarResumenPorDefecto: true);
            var contextoTexto = "## Documento Importante\n\nEste documento contiene información crítica.";

            // When: ResolveResumenPrompt is called
            var result = InvokeResolveResumenPrompt(input, contextoTexto);

            // Then: Should return a PromptConfig with interpolated template
            result.Should().NotBeNull();
            result!.Enabled.Should().BeTrue();
            result.ModelKey.Should().Be("default.gpt4o-mini");
            result.SystemPrompt.Should().Contain("Eres un analista documental experto");
            result.MaxTokens.Should().Be(1600);
            result.Temperature.Should().Be(0.0);
            result.ContentMode.Should().Be("markdown");

            // Contracto funcional: el resumen por defecto debe mantener la estructura de 5 apartados.
            result.UserPromptTemplate.Should().Contain("1. Objetivo del documento");
            result.UserPromptTemplate.Should().Contain("2. Datos clave");
            result.UserPromptTemplate.Should().Contain("3. Alertas");
            result.UserPromptTemplate.Should().Contain("4. Acciones recomendadas");
            result.UserPromptTemplate.Should().Contain("5. Contenido");
        }

        [Fact]
        public void ResolveResumenPrompt_TemplateInterpolation_ReplacesContenidoPlaceholder()
        {
            // Given: A template with {contenido} placeholder
            //        and context text to interpolate
            var input = CreateClasificacionInput(generarResumenPorDefecto: true);
            var contextoTexto = "CONTENIDO_ACTUAL_DEL_DOCUMENTO";

            // When: ResolveResumenPrompt interpolates the template
            var result = InvokeResolveResumenPrompt(input, contextoTexto);

            // Then: The {contenido} placeholder should be replaced with actual content
            result.Should().NotBeNull();
            result!.UserPromptTemplate.Should().Contain(contextoTexto);
            result.UserPromptTemplate.Should().NotContain("{contenido}");
        }

        [Fact]
        public void ResolveResumenPrompt_LoadsPromptDefaultsFromIOptions()
        {
            // Given: PromptDefaults configured via IOptions<PromptDefaultsSettings>
            var customDefaults = Options.Create(new PromptDefaultsSettings
            {
                ModelKey = "custom.gpt4o",
                SystemPrompt = "Custom system prompt...",
                UserPromptTemplate = "Custom template: {contenido}",
                MaxTokens = 2000,
                Temperature = 0.5,
                ContentMode = "vision"
            });

            var providerWithCustomDefaults = new GptClasificarDataProvider(
                new ClassificationModelRegistryLoader(_memoryCache, _scopeFactoryMock.Object),
                new ClassificationTipologiaPromptBuilder(
                    _memoryCache,
                    _scopeFactoryMock.Object,
                    new Mock<ILogger<ClassificationTipologiaPromptBuilder>>().Object),
                _scopeFactoryMock.Object,
                _routingSettings,
                customDefaults,
                _promptTraceTelemetryMock,
                _loggerMock.Object);

            var input = CreateClasificacionInput(generarResumenPorDefecto: true);
            var contextoTexto = "Test content";

            // When: ResolveResumenPrompt is called
            var result = InvokeResolveResumenPromptOnProvider(providerWithCustomDefaults, input, contextoTexto);

            // Then: Should use values from custom PromptDefaults
            result.Should().NotBeNull();
            result!.ModelKey.Should().Be("custom.gpt4o");
            result.SystemPrompt.Should().Be("Custom system prompt...");
            result.MaxTokens.Should().Be(2000);
            result.Temperature.Should().Be(0.5);
            result.ContentMode.Should().Be("vision");
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
                    { "Markdown", "Sample markdown content" }
                }
            };
        }

        private PromptConfig? InvokeResolveResumenPrompt(ClasificacionInput input, string contextoTexto)
        {
            // Reflection-based invocation of private ResolveResumenPrompt method
            var method = typeof(GptClasificarDataProvider)
                .GetMethod("ResolveResumenPrompt", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            return (PromptConfig?)method?.Invoke(_provider, new object[] { input, contextoTexto });
        }

        private PromptConfig? InvokeResolveResumenPromptOnProvider(
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
