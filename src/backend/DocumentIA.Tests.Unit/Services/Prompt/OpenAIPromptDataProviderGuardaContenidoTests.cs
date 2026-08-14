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
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
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

namespace DocumentIA.Tests.Unit.Services.Prompt;

/// <summary>
/// Guarda de contenido vacío en <see cref="OpenAIPromptDataProvider"/>: sin markdown ni base64
/// utilizable, el prompt no debe invocar al modelo. La aserción central es que
/// <see cref="IAzureOpenAIResilienceExecutor.ExecuteAsync{T}"/> nunca se llega a ejecutar
/// (ni por el camino de prompt libre ni por el de resumen dedicado, <c>EjecutarPromptJsonAsync</c>);
/// comprobar solo el valor devuelto dejaría pasar una regresión que llama al LLM y descarta la
/// respuesta.
///
/// Ni <see cref="TipologiaConfigLoader.LoadConfig"/> ni <see cref="PromptModelRegistryLoader.GetModel"/>
/// son <c>virtual</c>, así que no se pueden simular con Moq. En su lugar, el proveedor se construye
/// con instancias reales de esos loaders (repositorio de tipologías simulado vía
/// <see cref="IServiceScopeFactory"/> y un registro de modelos respaldado por un fichero temporal),
/// siguiendo el mismo patrón que <c>OpenAIPromptDataProviderResilienceTests</c>. Solo se simula
/// <see cref="IAzureOpenAIResilienceExecutor"/>, que sí es una interfaz, en <see cref="MockBehavior.Strict"/>.
/// </summary>
public class OpenAIPromptDataProviderGuardaContenidoTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _registryPath;

    public OpenAIPromptDataProviderGuardaContenidoTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"OpenAIPromptDataProviderGuardaContenidoTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _registryPath = Path.Combine(_tempDirectory, "prompt-models.json");

        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""default.gpt4o-mini"",
                    ""provider"": ""azure-openai"",
                    ""endpoint"": ""https://fake-openai.openai.azure.com/"",
                    ""apiKey"": ""fake-api-key"",
                    ""authMode"": ""ApiKey"",
                    ""deploymentName"": ""gpt-4o-mini-test"",
                    ""timeoutSeconds"": 30
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
    public async Task EjecutarPromptAsync_SinMarkdownNiBase64_NoLlamaAlModeloYMarcaSinContenido()
    {
        var resiliencia = new Mock<IAzureOpenAIResilienceExecutor>(MockBehavior.Strict);
        var sut = CrearSut(resiliencia.Object);

        var input = new PromptActivityInput
        {
            Tipologia = "prpe.09",
            MarkdownExtraido = null,
            DocumentoBase64 = null,
            ForzarResumenPorDefecto = true
        };

        var resultado = await sut.EjecutarPromptAsync(input);

        resultado.SinContenido.Should().BeTrue();
        resultado.Error.Should().NotBeNullOrWhiteSpace();
        resultado.Resumen.Should().BeEmpty();
        resultado.Resultado.Should().BeEmpty();

        // La aserción central: el modelo no se llegó a invocar (ni en el camino de resumen
        // dedicado -EjecutarPromptJsonAsync- ni en el de prompt libre).
        resiliencia.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EjecutarPromptAsync_ForzarResumenConMarkdown_PasaPorResilienceYDevuelveResumen()
    {
        // Camino positivo de la guarda (contrapartida del test anterior): con markdown disponible
        // la guarda NO debe bloquear nada y el proveedor sí debe invocar al modelo a través de
        // IAzureOpenAIResilienceExecutor.ExecuteAsync (mock Strict: solo responde a lo configurado).
        var resiliencia = new Mock<IAzureOpenAIResilienceExecutor>(MockBehavior.Strict);
        resiliencia
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateChatResult("{\"resumen\": \"Resumen de prueba generado por el modelo.\"}"));

        var sut = CrearSut(resiliencia.Object);

        var input = new PromptActivityInput
        {
            Tipologia = "prpe.09",
            MarkdownExtraido = "# Contenido real del documento",
            DocumentoBase64 = null,
            ForzarResumenPorDefecto = true
        };

        var resultado = await sut.EjecutarPromptAsync(input);

        resultado.SinContenido.Should().BeFalse();
        resultado.Resumen.Should().Be("Resumen de prueba generado por el modelo.");
        resultado.Error.Should().BeNullOrEmpty();

        resiliencia.Verify(
            x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void TieneContenidoUtilizable_ConMarkdown_EsTrue()
    {
        var input = new PromptActivityInput { MarkdownExtraido = "# Documento" };

        OpenAIPromptDataProvider.TieneContenidoUtilizable(input).Should().BeTrue();
    }

    [Fact]
    public void TieneContenidoUtilizable_ConDocumentoBase64_EsTrue()
    {
        var input = new PromptActivityInput { DocumentoBase64 = "QUJD" };

        OpenAIPromptDataProvider.TieneContenidoUtilizable(input).Should().BeTrue();
    }

    [Fact]
    public void TieneContenidoUtilizable_SinMarkdownNiBase64_EsFalse()
    {
        var input = new PromptActivityInput();

        OpenAIPromptDataProvider.TieneContenidoUtilizable(input).Should().BeFalse();
    }

    [Fact]
    public void TieneContenidoUtilizable_MarkdownEnBlanco_EsFalse()
    {
        var input = new PromptActivityInput { MarkdownExtraido = "   " };

        OpenAIPromptDataProvider.TieneContenidoUtilizable(input).Should().BeFalse();
    }

    // ========== Helpers ==========

    private static ClientResult<ChatCompletion> CreateChatResult(string responseText)
    {
        var completion = OpenAIChatModelFactory.ChatCompletion(
            role: ChatMessageRole.Assistant,
            content: new ChatMessageContent(responseText));
        return ClientResult.FromValue(completion, new Mock<PipelineResponse>().Object);
    }

    private OpenAIPromptDataProvider CrearSut(IAzureOpenAIResilienceExecutor resilience)
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();

        // La tipología no está publicada en BD: LoadConfig lanza FileNotFoundException,
        // que el provider captura internamente y sustituye por un fallback mínimo.
        var tipologiaRepositoryMock = new Mock<ITipologiaRepository>();
        tipologiaRepositoryMock
            .Setup(r => r.GetByCodigoAsync(It.IsAny<string>()))
            .ReturnsAsync((TipologiaEntity?)null);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ITipologiaRepository)))
            .Returns(tipologiaRepositoryMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(scopeMock.Object);

        var tipologiaConfigLoader = new TipologiaConfigLoader(memoryCache, scopeFactoryMock.Object);
        var promptModelRegistryLoader = new PromptModelRegistryLoader(_registryPath);

        var telemetryClient = new TelemetryClient(new TelemetryConfiguration { DisableTelemetry = true });
        var promptTraceTelemetry = new PromptTraceTelemetryService(
            telemetryClient,
            Options.Create(new PromptTracingSettings { Enabled = false }),
            new Mock<ILogger<PromptTraceTelemetryService>>().Object);

        return new OpenAIPromptDataProvider(
            tipologiaConfigLoader,
            promptModelRegistryLoader,
            Options.Create(new PromptDefaultsSettings()),
            promptTraceTelemetry,
            resilience,
            new Mock<ILogger<OpenAIPromptDataProvider>>().Object);
    }
}
