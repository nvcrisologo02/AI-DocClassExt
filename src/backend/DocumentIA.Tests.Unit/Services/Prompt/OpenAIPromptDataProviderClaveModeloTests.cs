#nullable enable
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
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
/// Clave de modelo en <see cref="OpenAIPromptDataProvider"/> (AB#101438): la clave de la petición
/// aplica también al resumen por defecto sin prompt, y la rama de reutilización etiqueta el modelo
/// con el mismo DeploymentName que la rama que ejecuta. Mismo patrón de construcción que
/// <c>OpenAIPromptDataProviderGuardaContenidoTests</c>: loaders reales y resiliencia simulada.
/// </summary>
public class OpenAIPromptDataProviderClaveModeloTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _registryPath;

    public OpenAIPromptDataProviderClaveModeloTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"OpenAIPromptDataProviderClaveModeloTests_{Guid.NewGuid()}");
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
                },
                {
                    ""key"": ""prompt.peticion"",
                    ""provider"": ""azure-openai"",
                    ""endpoint"": ""https://fake-openai.openai.azure.com/"",
                    ""apiKey"": ""fake-api-key"",
                    ""authMode"": ""ApiKey"",
                    ""deploymentName"": ""deployment-peticion"",
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
    public async Task EjecutarPromptAsync_SoloResumenConClaveEnLaPeticion_UsaElModeloDeLaPeticion()
    {
        // Sin prompt de tipología ni instrucciones en la petición: solo resumen por defecto. La clave
        // de la petición debe decidir el modelo en vez de caer a PromptDefaults:ModelKey.
        var resiliencia = new Mock<IAzureOpenAIResilienceExecutor>(MockBehavior.Strict);
        resiliencia
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ClientResult<ChatCompletion>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateChatResult("{\"resumen\": \"Resumen con el modelo de la petición.\"}"));
        var sut = CrearSut(resiliencia.Object);

        var input = new PromptActivityInput
        {
            Tipologia = "prpe.09",
            MarkdownExtraido = "# Contenido real del documento",
            ForzarResumenPorDefecto = true,
            Prompt = new PromptInstrucciones { ModelKey = "prompt.peticion" }
        };

        var resultado = await sut.EjecutarPromptAsync(input);

        resultado.Error.Should().BeNullOrEmpty();
        resultado.Modelo.Should().Be("deployment-peticion");
        resultado.Resumen.Should().Be("Resumen con el modelo de la petición.");
    }

    [Fact]
    public async Task EjecutarPromptAsync_Reutilizacion_EtiquetaElDeploymentComoLaRamaQueEjecuta()
    {
        // Resultados combinados previos y resumen ya presente: no se llama al modelo y la etiqueta
        // es el DeploymentName de la clave efectiva, no la clave.
        var resiliencia = new Mock<IAzureOpenAIResilienceExecutor>(MockBehavior.Strict);
        var sut = CrearSut(resiliencia.Object);

        var input = new PromptActivityInput
        {
            Tipologia = "prpe.09",
            MarkdownExtraido = "# Contenido real del documento",
            ForzarResumenPorDefecto = true,
            ResultadoPromptCombinado = "resultado previo",
            ResumenCombinado = "resumen previo"
        };

        var resultado = await sut.EjecutarPromptAsync(input);

        resultado.Modelo.Should().Be("gpt-4o-mini-test");
        resultado.Resultado.Should().Be("resultado previo");
        resultado.Resumen.Should().Be("resumen previo");
        resultado.TiempoMs.Should().Be(0);
        resiliencia.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EjecutarPromptAsync_ReutilizacionConClaveSinRegistro_ConservaLaClaveSinFallar()
    {
        var resiliencia = new Mock<IAzureOpenAIResilienceExecutor>(MockBehavior.Strict);
        var sut = CrearSut(resiliencia.Object, defaultModelKey: "prompt.inexistente");

        var input = new PromptActivityInput
        {
            Tipologia = "prpe.09",
            MarkdownExtraido = "# Contenido real del documento",
            ForzarResumenPorDefecto = true,
            ResultadoPromptCombinado = "resultado previo",
            ResumenCombinado = "resumen previo"
        };

        var resultado = await sut.EjecutarPromptAsync(input);

        resultado.Modelo.Should().Be("prompt.inexistente");
        resultado.Error.Should().BeNullOrEmpty();
        resiliencia.VerifyNoOtherCalls();
    }

    // ========== Helpers ==========

    private static ClientResult<ChatCompletion> CreateChatResult(string responseText)
    {
        var completion = OpenAIChatModelFactory.ChatCompletion(
            role: ChatMessageRole.Assistant,
            content: new ChatMessageContent(responseText));
        return ClientResult.FromValue(completion, new Mock<PipelineResponse>().Object);
    }

    private OpenAIPromptDataProvider CrearSut(IAzureOpenAIResilienceExecutor resilience, string defaultModelKey = "default.gpt4o-mini")
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();

        // La tipología no está publicada en BD: el provider usa su fallback mínimo sin PromptConfig.
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
            Options.Create(new PromptDefaultsSettings { ModelKey = defaultModelKey }),
            promptTraceTelemetry,
            resilience,
            new Mock<ILogger<OpenAIPromptDataProvider>>().Object);
    }
}
