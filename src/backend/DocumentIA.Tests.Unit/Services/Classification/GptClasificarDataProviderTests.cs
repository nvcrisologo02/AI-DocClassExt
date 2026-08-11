using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Classification;
using DocumentIA.Functions.Services.Resilience;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenAI.Chat;
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
        private readonly TipologiaConfigLoader _tipologiaConfigLoader;
        private readonly Mock<ILogger<GptClasificarDataProvider>> _loggerMock;
        private readonly PromptTraceTelemetryService _promptTraceTelemetryMock;
        private readonly IOptions<ClassificationRoutingSettings> _routingSettings;
        private readonly IOptions<PromptDefaultsSettings> _promptDefaults;
        private readonly Mock<IAzureOpenAIResilienceExecutor> _resilienceMock;
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
            _tipologiaConfigLoader = new TipologiaConfigLoader(_memoryCache, _scopeFactoryMock.Object);
            _resilienceMock = new Mock<IAzureOpenAIResilienceExecutor>();

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
                _tipologiaConfigLoader,
                _scopeFactoryMock.Object,
                _routingSettings,
                _promptDefaults,
                Options.Create(new ClassificationPromptsSettings()),
                new Mock<IClassificationPromptProvider>().Object,
                _promptTraceTelemetryMock,
                _resilienceMock.Object,
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
                _tipologiaConfigLoader,
                _scopeFactoryMock.Object,
                _routingSettings,
                promptDefaultsEmpty,
                Options.Create(new ClassificationPromptsSettings()),
                new Mock<IClassificationPromptProvider>().Object,
                _promptTraceTelemetryMock,
                _resilienceMock.Object,
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

            // Then: {contenido} se resuelve, pero con la referencia al bloque CONTENIDO DEL
            // DOCUMENTO del prompt de Fase 1, no con el documento otra vez (AB#100006)
            result.Should().NotBeNull();
            result!.UserPromptTemplate.Should().Contain(GptClasificarDataProvider.ResumenContenidoReferencia);
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
                _tipologiaConfigLoader,
                _scopeFactoryMock.Object,
                _routingSettings,
                customDefaults,
                Options.Create(new ClassificationPromptsSettings()),
                new Mock<IClassificationPromptProvider>().Object,
                _promptTraceTelemetryMock,
                _resilienceMock.Object,
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

        [Fact]
        public void ResolveResumenPrompt_WhenTipologiaPromptExists_UsesTipologiaOverrideOverDefaults()
        {
            var tipologiaCodigo = "ESCR-06";
            var tipologiaJson = """
{
  "tipologiaId": "ESCR-06",
  "tipologiaNombre": "Escritura",
  "version": "1.0",
  "promptConfig": {
    "enabled": true,
    "modelKey": "tipologia.gpt4o-mini",
    "systemPrompt": "System tipologia",
    "userPromptTemplate": "Plantilla tipologia:\n{contenido}",
    "maxTokens": 999,
    "temperature": 0.25,
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
            scopeMock
                .SetupGet(s => s.ServiceProvider)
                .Returns(serviceProviderMock.Object);

            _scopeFactoryMock
                .Setup(sf => sf.CreateScope())
                .Returns(scopeMock.Object);

            var input = CreateClasificacionInput(generarResumenPorDefecto: true);
            input.Entrada.Instrucciones.ExpectedType = tipologiaCodigo;

            var result = InvokeResolveResumenPrompt(input, "CONTENIDO_DOC");

            result.Should().NotBeNull();
            result!.ModelKey.Should().Be("tipologia.gpt4o-mini");
            result.SystemPrompt.Should().Be("System tipologia");
            result.MaxTokens.Should().Be(999);
            result.Temperature.Should().Be(0.25);
            result.UserPromptTemplate.Should().Contain("Plantilla tipologia");
            // AB#100006: también en el override por tipología, {contenido} se interpola con la
            // referencia al bloque ya enviado, no con el documento duplicado.
            result.UserPromptTemplate.Should().NotContain("CONTENIDO_DOC");
            result.UserPromptTemplate.Should().Contain(GptClasificarDataProvider.ResumenContenidoReferencia);
        }

        // ========== Documento duplicado en el prompt de Fase 1 (AB#100006) ==========

        [Fact]
        public void ResolveResumenPrompt_NoDuplicaElDocumento_InterpolaContenidoConReferenciaAlBloquePrincipal()
        {
            // Given: GenerarResumenPorDefecto=true y un template de resumen con {contenido}.
            // El documento ya viaja en el bloque CONTENIDO DEL DOCUMENTO del user prompt de
            // Fase 1 (placeholder {DOCUMENT_TEXT}), así que la interpolación del prompt de
            // resumen no debe volver a incluirlo: duplicarlo dobla los tokens de entrada.
            var input = CreateClasificacionInput(generarResumenPorDefecto: true);
            var contextoTexto = "CONTENIDO_ACTUAL_DEL_DOCUMENTO";

            // When
            var result = InvokeResolveResumenPrompt(input, contextoTexto);

            // Then: {contenido} se sustituye por una referencia al bloque ya enviado
            result.Should().NotBeNull();
            result!.UserPromptTemplate.Should().NotContain(contextoTexto);
            result.UserPromptTemplate.Should().NotContain("{contenido}");
            result.UserPromptTemplate.Should().Contain("ya está incluido");
        }

        [Fact]
        public void ResolveResumenPrompt_CuandoTemplateNoTienePlaceholderContenido_NoAnadeReferencia()
        {
            // Given: un template de resumen sin {contenido}; no hay nada que interpolar
            var defaultsSinPlaceholder = Options.Create(new PromptDefaultsSettings
            {
                ModelKey = "default.gpt4o-mini",
                SystemPrompt = "Sistema...",
                UserPromptTemplate = "Genera un resumen ejecutivo del documento en 500 caracteres.",
                MaxTokens = 1600,
                Temperature = 0.0,
                ContentMode = "markdown"
            });

            var provider = new GptClasificarDataProvider(
                new ClassificationModelRegistryLoader(_memoryCache, _scopeFactoryMock.Object),
                new ClassificationTipologiaPromptBuilder(
                    _memoryCache,
                    _scopeFactoryMock.Object,
                    new Mock<ILogger<ClassificationTipologiaPromptBuilder>>().Object),
                _tipologiaConfigLoader,
                _scopeFactoryMock.Object,
                _routingSettings,
                defaultsSinPlaceholder,
                Options.Create(new ClassificationPromptsSettings()),
                new Mock<IClassificationPromptProvider>().Object,
                _promptTraceTelemetryMock,
                _resilienceMock.Object,
                _loggerMock.Object);

            var input = CreateClasificacionInput(generarResumenPorDefecto: true);

            // When
            var result = InvokeResolveResumenPromptOnProvider(provider, input, "CONTENIDO_DOC");

            // Then: el template queda tal cual, sin documento ni referencia inyectados
            result.Should().NotBeNull();
            result!.UserPromptTemplate.Should().Be("Genera un resumen ejecutivo del documento en 500 caracteres.");
        }

        // ========== Phase 2 sin TDN2 parseable → virtual TDN1 (AB#99891) ==========

        [Theory]
        [InlineData("respuesta truncada que no es json", "fase2_parsing_error")]
        [InlineData("{\"tdn2\": null, \"confianza\": 0.4}", "fase2_parsing_error")]
        [InlineData("{\"tdn2\": \"\", \"confianza\": 0.4}", "fase2_parsing_error")]
        public async Task ClasificarAsync_CuandoPhase2NoDevuelveTdn2Parseable_DegradaAVirtualTdn1(string phase2Response, string fallbackRazonEsperada)
        {
            // Given: Phase 1 resuelve TDN1=TASA con confianza 0.72 y Phase 2 no devuelve un tdn2 parseable
            var promptProviderMock = new Mock<IClassificationPromptProvider>();
            promptProviderMock
                .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePromptSet());

            SeedClassificationCaches();

            var resilienceMock = new Mock<IAzureOpenAIResilienceExecutor>();
            resilienceMock
                .SetupSequence(r => r.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateChatResult(
                    "{\"tdn1\": \"TASA\", \"propuesta\": \"TASA: informe de tasacion de activo\", \"resumen\": \"Resumen Phase 1\", \"confianza\": 0.72}"))
                .ReturnsAsync(CreateChatResult(phase2Response));

            var provider = CreateProvider(promptProviderMock.Object, resilienceMock.Object);
            var input = CreateClasificacionInput(generarResumenPorDefecto: false);
            input.Entrada.Instrucciones.Classification.NivelClasificacion = "TDN1_TDN2";

            // When
            var result = await provider.ClasificarAsync(input);

            // Then: conserva el TDN1 de Phase 1 como tipología virtual en vez de descartar todo con "Desconocido"
            result.TipologiaDetectada.Should().Be("TASA");
            result.ClasificacionParcial.Should().BeTrue();
            result.Confianza.Should().Be(0.72);
            result.ConfianzaGPT.Should().Be(0.72);
            result.FallbackRazon.Should().Be(fallbackRazonEsperada);
            result.ResumenCombinado.Should().Be("Resumen Phase 1");
            result.PropuestaTipologia.Should().Be("TASA: informe de tasacion de activo");
        }

        [Fact]
        public async Task ClasificarAsync_CuandoPhase2DevuelveTdn2NullExplicitoSinModoRestringido_PreservaFallbackRazonLegacy()
        {
            // Given: Phase 1 resuelve TDN1=TASA y Phase 2 responde "tdn2": null explícito (ninguna
            // tipología del catálogo encaja). Sin modo restringido, el provider debe seguir emitiendo
            // el motivo histórico Phase2ParsingErrorReason para no romper el contrato que consume
            // DocumentProcessOrchestrator al detectar el corte "virtual TDN1" (AB#100050).
            var promptProviderMock = new Mock<IClassificationPromptProvider>();
            promptProviderMock
                .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePromptSet());

            SeedClassificationCaches();

            var resilienceMock = new Mock<IAzureOpenAIResilienceExecutor>();
            resilienceMock
                .SetupSequence(r => r.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateChatResult(
                    "{\"tdn1\": \"TASA\", \"propuesta\": \"TASA: informe de tasacion de activo\", \"resumen\": \"Resumen Phase 1\", \"confianza\": 0.72}"))
                .ReturnsAsync(CreateChatResult("{\"tdn2\": null, \"confianza\": 0.4}"));

            var provider = CreateProvider(promptProviderMock.Object, resilienceMock.Object);
            var input = CreateClasificacionInput(generarResumenPorDefecto: false);
            input.Entrada.Instrucciones.Classification.NivelClasificacion = "TDN1_TDN2";

            // When
            var result = await provider.ClasificarAsync(input);

            // Then: FallbackRazon es el motivo legacy, no el nuevo Fase2NingunaTipologiaReason
            result.TipologiaDetectada.Should().Be("TASA");
            result.ClasificacionParcial.Should().BeTrue();
            result.FallbackRazon.Should().Be(GptHierarchicalClassificationParser.Phase2ParsingErrorReason);
        }

        // ========== Robustez resolución tipología: mapeo propuesta -> catálogo (AB#99984) ==========

        [Fact]
        public async Task ClasificarAsync_CuandoPropuestaNombraFamiliaSinPrefijoDeCodigo_ResuelveTdn1PorCatalogoEnLugarDeDesconocido()
        {
            // Given: Phase 1 no devuelve 'tdn1' y la propuesta describe la familia en prosa libre,
            // sin anteponer el código de catálogo (formato que ExtraerTdn1DePropuesta no soporta,
            // pero que el prompt de Phase 1 tampoco exige: 'propuesta' es "texto libre").
            // Caso reproducido a partir del baseline de evaluación (AB#99948): 18 documentos
            // clasificables cayeron a Tdn1 vacío por este motivo (familia TASA entre ellos).
            var promptProviderMock = new Mock<IClassificationPromptProvider>();
            promptProviderMock
                .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePromptSet());

            SeedClassificationCaches();
            SetupTipologiaRepository(new TipologiaEntity
            {
                Codigo = "tasa.09",
                Nombre = "Tasación: Informe activo",
                Activa = true,
                Estado = EstadoTipologia.Published,
                ConfiguracionJson = "{\"tipologiaId\":\"tasa.09\",\"classification\":{\"tdn1\":\"TASA\",\"tdn2\":\"TASA-09\"}}"
            });

            const string propuestaSinPrefijo =
                "Informe de tasación completo de Sociedad de Tasación con metodología (comparación, coste), " +
                "comparables de mercado y valor de tasación-hipotecario del inmueble, propio de Tasaciones y Valoraciones.";

            var resilienceMock = new Mock<IAzureOpenAIResilienceExecutor>();
            resilienceMock
                .SetupSequence(r => r.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateChatResult(
                    $"{{\"tdn1\": null, \"propuesta\": \"{propuestaSinPrefijo}\", \"resumen\": \"Resumen Phase 1\", \"confianza\": 0.97}}"))
                .ReturnsAsync(CreateChatResult("{\"tdn2\": \"TASA-09\", \"confianza\": 0.9}"));

            var provider = CreateProvider(promptProviderMock.Object, resilienceMock.Object);
            var input = CreateClasificacionInput(generarResumenPorDefecto: false);
            input.Entrada.Instrucciones.Classification.NivelClasificacion = "TDN1_TDN2";

            // When
            var result = await provider.ClasificarAsync(input);

            // Then: NO debe degradar a Desconocido/virtual; debe resolver TASA por mapeo
            // tolerante propuesta->catálogo, completar Phase 2 con normalidad y dejar traza
            // distintiva en FallbackRazon que lo diferencie de un Desconocido legítimo.
            result.TipologiaDetectada.Should().Be("tasa.09");
            result.Tdn2Detectado.Should().Be("TASA-09");
            result.ClasificacionParcial.Should().BeFalse();
            result.FallbackRazon.Should().Be("tdn1_resuelto_por_mapeo_propuesta");
            result.PropuestaTipologia.Should().Be(propuestaSinPrefijo);
        }

        [Fact]
        public async Task ClasificarAsync_CuandoPropuestaNoMencionaNingunaFamiliaDelCatalogo_SigueDevolviendoDesconocido()
        {
            // Given: Phase 1 no devuelve 'tdn1' y la propuesta es un genuino "no clasificable"
            // (documento ilegible/sin contenido). El mapeo tolerante NO debe inventar una familia.
            var promptProviderMock = new Mock<IClassificationPromptProvider>();
            promptProviderMock
                .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePromptSet());

            SeedClassificationCaches();

            var resilienceMock = new Mock<IAzureOpenAIResilienceExecutor>();
            resilienceMock
                .Setup(r => r.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateChatResult(
                    "{\"tdn1\": null, \"propuesta\": \"documento ilegible o sin contenido identificable para clasificar\", \"confianza\": 0.1}"));

            var provider = CreateProvider(promptProviderMock.Object, resilienceMock.Object);
            var input = CreateClasificacionInput(generarResumenPorDefecto: false);
            input.Entrada.Instrucciones.Classification.NivelClasificacion = "TDN1_TDN2";

            // When
            var result = await provider.ClasificarAsync(input);

            // Then: sigue siendo Desconocido legítimo (no hay familia real mencionada en el texto)
            result.TipologiaDetectada.Should().Be("Desconocido");
            result.ClasificacionParcial.Should().BeTrue();
            result.FallbackRazon.Should().Be("tdn1_virtual_propuesta");
        }

        [Fact]
        public async Task ClasificarAsync_CuandoPhase1NoResuelveTdn1_SigueDevolviendoDesconocido()
        {
            // Given: Phase 1 no devuelve tdn1 ni propuesta con código extraíble
            var promptProviderMock = new Mock<IClassificationPromptProvider>();
            promptProviderMock
                .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePromptSet());

            SeedClassificationCaches();

            var resilienceMock = new Mock<IAzureOpenAIResilienceExecutor>();
            resilienceMock
                .Setup(r => r.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateChatResult("respuesta que no es json"));

            var provider = CreateProvider(promptProviderMock.Object, resilienceMock.Object);
            var input = CreateClasificacionInput(generarResumenPorDefecto: false);
            input.Entrada.Instrucciones.Classification.NivelClasificacion = "TDN1_TDN2";

            // When
            var result = await provider.ClasificarAsync(input);

            // Then: sin TDN1 de Phase 1 no hay nada que conservar → Desconocido
            result.TipologiaDetectada.Should().Be("Desconocido");
            result.Confianza.Should().Be(0.0);
            result.FallbackRazon.Should().Be("fase1_parsing_error");
        }

        [Fact]
        public async Task Router_ResultadoParcialSatisfactorio_ConservaFallbackRazon()
        {
            // Given: el provider GPT devuelve un virtual TDN1 (fase2_parsing_error, motivo legacy que
            // el provider preserva incluso ante un "tdn2": null explícito) con confianza 0.72, que
            // supera el umbral 0.6 y por tanto el router lo considera satisfactorio
            var promptProviderMock = new Mock<IClassificationPromptProvider>();
            promptProviderMock
                .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePromptSet());

            SeedClassificationCaches();

            var resilienceMock = new Mock<IAzureOpenAIResilienceExecutor>();
            resilienceMock
                .SetupSequence(r => r.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateChatResult(
                    "{\"tdn1\": \"TASA\", \"propuesta\": \"TASA: informe de tasacion de activo\", \"resumen\": \"Resumen Phase 1\", \"confianza\": 0.72}"))
                .ReturnsAsync(CreateChatResult("{\"tdn2\": null}"));

            var gptProvider = CreateProvider(promptProviderMock.Object, resilienceMock.Object);
            var router = CreateRouter(gptProvider);

            var input = CreateClasificacionInput(generarResumenPorDefecto: false);
            input.Entrada.Instrucciones.Classification.Provider = "gpt";
            input.Entrada.Instrucciones.Classification.NivelClasificacion = "TDN1_TDN2";

            // When
            var result = await router.ClasificarAsync(input);

            // Then: el router NO debe borrar FallbackRazon de un resultado parcial —
            // el orquestador la necesita para tratar el resultado como tipología virtual
            result.ClasificacionParcial.Should().BeTrue();
            result.TipologiaDetectada.Should().Be("TASA");
            result.FallbackRazon.Should().Be("fase2_parsing_error");
            result.FallbackLLM.Should().BeFalse();
        }

        private ConfigurableClasificarDataProvider CreateRouter(GptClasificarDataProvider gptProvider)
        {
            var windowExtractor = new DocumentWindowExtractor(new Mock<ILogger<DocumentWindowExtractor>>().Object);
            var ruleClassifier = new RuleBasedTdnClassifier(new Mock<ILogger<RuleBasedTdnClassifier>>().Object);
            var hybridOptions = Options.Create(new HybridTdnOptions());
            var modelRegistryLoader = new ClassificationModelRegistryLoader(_memoryCache, _scopeFactoryMock.Object);

            var hybridProvider = new HybridTdnClasificarProvider(
                new Mock<ILogger<HybridTdnClasificarProvider>>().Object,
                new Mock<IClasificarDataProvider>().Object,
                new Mock<ILayoutMarkdownProvider>().Object,
                windowExtractor,
                ruleClassifier,
                new FoundryTdnRescueClassifier(
                    new Mock<ILogger<FoundryTdnRescueClassifier>>().Object,
                    gptProvider),
                hybridOptions,
                new TelemetryClient());

            var sourceResolver = new DocumentIntelligenceSourceResolver(
                new Mock<DocumentIA.Core.Services.IBlobStorageService>().Object,
                Options.Create(new DocumentIntelligenceSettings()),
                new Mock<ILogger<DocumentIntelligenceSourceResolver>>().Object);

            var azureProvider = new AzureDocumentIntelligenceClasificarProvider(
                new Mock<System.Net.Http.IHttpClientFactory>().Object,
                modelRegistryLoader,
                sourceResolver,
                new Mock<ILogger<AzureDocumentIntelligenceClasificarProvider>>().Object);

            return new ConfigurableClasificarDataProvider(
                new MockClasificarDataProvider(),
                azureProvider,
                gptProvider,
                hybridProvider,
                ruleClassifier,
                windowExtractor,
                hybridOptions,
                modelRegistryLoader,
                _routingSettings,
                new Mock<ILogger<ConfigurableClasificarDataProvider>>().Object);
        }

        [Fact]
        public async Task ClasificarAsync_CuandoTdn2NoTieneTipologiaPublicada_DevuelveVirtualConTdn2Detectado()
        {
            // Given: Phase 2 devuelve un TDN2 válido del catálogo pero sin tipología publicada que lo mapee
            var promptProviderMock = new Mock<IClassificationPromptProvider>();
            promptProviderMock
                .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePromptSet());

            SeedClassificationCaches();
            SetupTipologiaRepository();

            var resilienceMock = new Mock<IAzureOpenAIResilienceExecutor>();
            resilienceMock
                .SetupSequence(r => r.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateChatResult(
                    "{\"tdn1\": \"TASA\", \"propuesta\": \"TASA: informe de tasacion de activo\", \"resumen\": \"Resumen Phase 1\", \"confianza\": 0.72}"))
                .ReturnsAsync(CreateChatResult("{\"tdn2\": \"TASA-10\", \"confianza\": 0.8}"));

            var provider = CreateProvider(promptProviderMock.Object, resilienceMock.Object);
            var input = CreateClasificacionInput(generarResumenPorDefecto: false);
            input.Entrada.Instrucciones.Classification.NivelClasificacion = "TDN1_TDN2";

            // When
            var result = await provider.ClasificarAsync(input);

            // Then: virtual TDN1, pero el TDN2 elegido por Phase 2 se conserva para persistencia/evaluación
            result.ClasificacionParcial.Should().BeTrue();
            result.TipologiaDetectada.Should().Be("TASA");
            result.Tdn2Detectado.Should().Be("TASA-10");
        }

        [Fact]
        public async Task ClasificarAsync_CuandoTdn2ResuelveATipologiaPublicada_TambienInformaTdn2Detectado()
        {
            // Given: Phase 2 devuelve un TDN2 con tipología publicada (TASA-09 → tasa.09)
            var promptProviderMock = new Mock<IClassificationPromptProvider>();
            promptProviderMock
                .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePromptSet());

            SeedClassificationCaches();
            SetupTipologiaRepository(new TipologiaEntity
            {
                Codigo = "tasa.09",
                Nombre = "Tasación: Informe activo",
                Activa = true,
                Estado = EstadoTipologia.Published,
                ConfiguracionJson = "{\"tipologiaId\":\"tasa.09\",\"classification\":{\"tdn1\":\"TASA\",\"tdn2\":\"TASA-09\"}}"
            });

            var resilienceMock = new Mock<IAzureOpenAIResilienceExecutor>();
            resilienceMock
                .SetupSequence(r => r.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateChatResult(
                    "{\"tdn1\": \"TASA\", \"propuesta\": \"TASA: informe de tasacion de activo\", \"resumen\": \"Resumen Phase 1\", \"confianza\": 0.72}"))
                .ReturnsAsync(CreateChatResult("{\"tdn2\": \"TASA-09\", \"confianza\": 0.9}"));

            var provider = CreateProvider(promptProviderMock.Object, resilienceMock.Object);
            var input = CreateClasificacionInput(generarResumenPorDefecto: false);
            input.Entrada.Instrucciones.Classification.NivelClasificacion = "TDN1_TDN2";

            // When
            var result = await provider.ClasificarAsync(input);

            // Then
            result.ClasificacionParcial.Should().BeFalse();
            result.TipologiaDetectada.Should().Be("tasa.09");
            result.Tdn2Detectado.Should().Be("TASA-09");
        }

        // ========== Modo restringido: TDN2 sin mapeo publicado (AB#100046) ==========

        [Fact]
        public async Task ClasificarAsync_ModoRestringido_CuandoTdn2NoTieneTipologiaPublicada_DevuelveDesconocido()
        {
            // Given: mismo escenario que ClasificarAsync_CuandoTdn2NoTieneTipologiaPublicada_
            // DevuelveVirtualConTdn2Detectado (Phase 2 devuelve un TDN2 sin tipología publicada
            // que lo mapee), pero con restricción de tipologías activa. Antes del fix (AB#100046)
            // esta rama no interceptaba el modo restringido y devolvía un resultado virtual con
            // un TipologiaDetectada sintético, que el router podía reutilizar como
            // PropuestaTipologia pese a estar fuera del catálogo permitido.
            var promptProviderMock = new Mock<IClassificationPromptProvider>();
            promptProviderMock
                .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePromptSet());

            var codigosPermitidos = new List<string> { "TASA-10" };
            SeedClassificationCaches();
            SeedClassificationCachesRestringido(codigosPermitidos);
            SetupTipologiaRepository(); // sin tipologías publicadas: TASA-10 no mapea a ninguna

            var resilienceMock = new Mock<IAzureOpenAIResilienceExecutor>();
            resilienceMock
                .SetupSequence(r => r.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateChatResult(
                    "{\"tdn1\": \"TASA\", \"propuesta\": \"TASA: informe de tasacion de activo\", \"resumen\": \"Resumen Phase 1\", \"confianza\": 0.72}"))
                .ReturnsAsync(CreateChatResult("{\"tdn2\": \"TASA-10\", \"confianza\": 0.8}"));

            var provider = CreateProvider(promptProviderMock.Object, resilienceMock.Object);
            var input = CreateClasificacionInput(generarResumenPorDefecto: false);
            input.Entrada.Instrucciones.Classification.NivelClasificacion = "TDN1_TDN2";
            input.Entrada.Instrucciones.RestriccionTipologias = new RestriccionTipologias
            {
                Codigos = codigosPermitidos
            };

            // When
            var result = await provider.ClasificarAsync(input);

            // Then: modo restringido intercepta y devuelve Desconocido, no un virtual sintético
            result.TipologiaDetectada.Should().Be("Desconocido");
            result.Confianza.Should().Be(0.0);
            result.ClasificacionParcial.Should().BeFalse();
            result.FallbackRazon.Should().Be(RestriccionTipologiasMotivos.FueraDeConjunto);
            result.PropuestaTipologia.Should().Be("TASA: informe de tasacion de activo");
        }

        /// <summary>
        /// Siembra las claves de caché de catálogo TDN1/TDN2 usadas en modo restringido. La clave
        /// incluye un hash del conjunto permitido (ver ClassificationTipologiaPromptBuilder.
        /// ComputeSetHash); se replica aquí el mismo algoritmo para no depender de
        /// ICatalogoTdnRepository/DocumentIADbContext, que no están mockeados en este harness.
        /// </summary>
        private void SeedClassificationCachesRestringido(List<string> codigosPermitidos)
        {
            var hash = ComputeRestrictedCacheHash(codigosPermitidos);
            _memoryCache.Set(
                $"clasificacion:catalogo:tdn1:r:{hash}",
                "- TASA: Tasaciones y Valoraciones, informes de tasacion");
            _memoryCache.Set(
                $"clasificacion:catalogo:tdn2:TASA:r:{hash}",
                "TASA-10 | Tasacion restringida");
        }

        private static string ComputeRestrictedCacheHash(IEnumerable<string> codigos)
        {
            var canonical = string.Join(
                "|",
                codigos.Select(c => c.Trim().ToUpperInvariant()).OrderBy(c => c, StringComparer.Ordinal));
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(bytes)[..16];
        }

        private void SetupTipologiaRepository(params TipologiaEntity[] tipologias)
        {
            var repoMock = new Mock<ITipologiaRepository>();
            repoMock
                .Setup(r => r.GetAllPublishedAsync())
                .ReturnsAsync(tipologias.ToList());

            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(ITipologiaRepository)))
                .Returns(repoMock.Object);

            var scopeMock = new Mock<IServiceScope>();
            scopeMock
                .SetupGet(s => s.ServiceProvider)
                .Returns(serviceProviderMock.Object);

            _scopeFactoryMock
                .Setup(sf => sf.CreateScope())
                .Returns(scopeMock.Object);
        }

        private void SeedClassificationCaches()
        {
            _memoryCache.Set("modelos:clasificacion", new ClassificationModelRegistry
            {
                Models =
                {
                    new ClassificationModelConfig
                    {
                        Key = "classification.gpt4o-mini-fallback",
                        Provider = "azure-openai",
                        UseAsFallback = true,
                        Endpoint = "https://unit-test.openai.azure.com",
                        DeploymentName = "gpt-4o-mini",
                        AuthMode = "DefaultAzureCredential",
                        TimeoutSeconds = 5,
                        MaxTokens = 500
                    }
                }
            });
            _memoryCache.Set("clasificacion:catalogo:tdn1", "- TASA: Tasaciones y Valoraciones, informes de tasacion");
            _memoryCache.Set("clasificacion:catalogo:tdn2:TASA", "TASA-09 | Tasacion: informe activo");
        }

        private GptClasificarDataProvider CreateProvider(
            IClassificationPromptProvider promptProvider,
            IAzureOpenAIResilienceExecutor resilience)
        {
            return new GptClasificarDataProvider(
                new ClassificationModelRegistryLoader(_memoryCache, _scopeFactoryMock.Object),
                new ClassificationTipologiaPromptBuilder(
                    _memoryCache,
                    _scopeFactoryMock.Object,
                    new Mock<ILogger<ClassificationTipologiaPromptBuilder>>().Object),
                _tipologiaConfigLoader,
                _scopeFactoryMock.Object,
                _routingSettings,
                _promptDefaults,
                Options.Create(new ClassificationPromptsSettings()),
                promptProvider,
                _promptTraceTelemetryMock,
                resilience,
                _loggerMock.Object);
        }

        private static ClassificationPromptSet CreatePromptSet()
        {
            return new ClassificationPromptSet
            {
                Phase1SystemPrompt = "sys fase 1",
                Phase1UserPrompt = "{CONTEXT_PROMPT}\n{TDN1_CATALOG}\n{DOCUMENT_TEXT}",
                Phase2SystemPrompt = "sys fase 2",
                Phase2UserPrompt = "{TDN1_CODE}\n{TDN2_CATALOG}\n{DOCUMENT_TEXT}",
                Version = 1,
                Source = "UnitTest"
            };
        }

        private static ClientResult<ChatCompletion> CreateChatResult(string responseText)
        {
            var completion = OpenAIChatModelFactory.ChatCompletion(
                role: ChatMessageRole.Assistant,
                content: new ChatMessageContent(responseText));
            return ClientResult.FromValue(completion, new Mock<PipelineResponse>().Object);
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
            // Reflection-based invocation of private ResolveResumenPrompt method.
            // contextoTexto se conserva en la firma del helper para documentar en cada test
            // qué documento estaría en juego, aunque desde AB#100006 el método ya no lo recibe.
            return InvokeResolveResumenPromptOnProvider(_provider, input, contextoTexto);
        }

        private PromptConfig? InvokeResolveResumenPromptOnProvider(
            GptClasificarDataProvider provider,
            ClasificacionInput input,
            string contextoTexto)
        {
            var method = typeof(GptClasificarDataProvider)
                .GetMethod("ResolveResumenPrompt",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return (PromptConfig?)method?.Invoke(provider, new object[] { input });
        }
    }
}
