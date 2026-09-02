using System.Net;
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Triggers;
using DocumentIA.Tests.Unit.Helpers;
using FluentAssertions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentIA.Tests.Unit.Triggers;

public class IngestAPITriggerTests
{
    private static RestriccionTipologiasValidator CreateRestriccionValidatorSinCatalogo()
    {
        // Para peticiones sin restriccionTipologias el validador ni siquiera toca el repositorio,
        // por lo que basta un scope factory vacío.
        return new RestriccionTipologiasValidator(
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<ILogger<RestriccionTipologiasValidator>>());
    }

    private static RestriccionTipologiasValidator CreateRestriccionValidatorConCatalogo(params string[] codigosPublicados)
    {
        var repoMock = new Mock<ITipologiaRepository>();
        repoMock.Setup(r => r.GetAllPublishedAsync())
            .ReturnsAsync(codigosPublicados
                .Select(c => new TipologiaEntity { Codigo = c })
                .ToList());

        var services = new ServiceCollection();
        services.AddSingleton(repoMock.Object);
        var provider = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(() => provider.CreateScope());

        return new RestriccionTipologiasValidator(
            new MemoryCache(new MemoryCacheOptions()),
            scopeFactoryMock.Object,
            Mock.Of<ILogger<RestriccionTipologiasValidator>>());
    }

    [Fact]
    public async Task Run_WhenNivelClasificacionOmitted_AppliesDefaultAndSchedulesOrchestrationBeforeResponseSerialization()
    {
        var logger = new Mock<ILogger<IngestAPITrigger>>();
        var blobStorage = new Mock<IBlobStorageService>(MockBehavior.Strict);
        var promptValidator = new PromptInstruccionesValidator(new PromptModelRegistryLoader("dummy.json"));
        var settings = Options.Create(new ClassificationRoutingSettings
        {
            NivelClasificacionDefault = ClassificationLevelResolver.LevelTdn1Tdn2
        });

        ContratoEntrada? scheduledInput = null;
        TaskName? scheduledTaskName = null;
        var durableClient = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        durableClient
            .Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<object?>(),
                It.IsAny<StartOrchestrationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<TaskName, object?, StartOrchestrationOptions?, CancellationToken>((taskName, input, _, _) =>
            {
                scheduledTaskName = taskName;
                scheduledInput = input as ContratoEntrada;
            })
            .ReturnsAsync("instance-001");

        var function = new IngestAPITrigger(logger.Object, promptValidator, CreateRestriccionValidatorSinCatalogo(), blobStorage.Object, settings);

        var body = JsonSerializer.Serialize(new
        {
            documento = new
            {
                name = "test.pdf",
                blobPath = "documents/test.pdf"
            },
            instrucciones = new
            {
                classification = new { }
            },
            trazabilidad = new
            {
                correlationId = "corr-001",
                submittedBy = "tester"
            }
        });

        var request = HttpFunctionTestFactory.CreateRequest(
            method: "POST",
            url: "http://localhost/api/ingest",
            body: body,
            headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" });

        var response = await function.Run(request, durableClient.Object);
        var responseBody = await HttpFunctionTestFactory.ReadBodyAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        responseBody.Should().Contain("provider");
        scheduledTaskName.Should().NotBeNull();
        scheduledTaskName!.Value.Name.Should().Be("DocumentProcessOrchestrator");
        scheduledInput.Should().NotBeNull();
        scheduledInput!.Instrucciones.Classification.NivelClasificacion.Should().Be(ClassificationLevelResolver.LevelTdn1Tdn2);

        durableClient.VerifyAll();
        blobStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_WhenNivelClasificacionIsInvalid_ReturnsBadRequest()
    {
        var logger = new Mock<ILogger<IngestAPITrigger>>();
        var blobStorage = new Mock<IBlobStorageService>(MockBehavior.Strict);
        var promptValidator = new PromptInstruccionesValidator(new PromptModelRegistryLoader("dummy.json"));
        var settings = Options.Create(new ClassificationRoutingSettings
        {
            NivelClasificacionDefault = ClassificationLevelResolver.LevelTdn1Tdn2
        });

        var durableClient = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        var function = new IngestAPITrigger(logger.Object, promptValidator, CreateRestriccionValidatorSinCatalogo(), blobStorage.Object, settings);

        var body = JsonSerializer.Serialize(new
        {
            documento = new
            {
                name = "test.pdf",
                blobPath = "documents/test.pdf"
            },
            instrucciones = new
            {
                classification = new
                {
                    nivelClasificacion = "TDN2"
                }
            },
            trazabilidad = new
            {
                correlationId = "corr-001"
            }
        });

        var request = HttpFunctionTestFactory.CreateRequest(
            method: "POST",
            url: "http://localhost/api/ingest",
            body: body,
            headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" });

        var response = await function.Run(request, durableClient.Object);
        var responseBody = await HttpFunctionTestFactory.ReadBodyAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseBody.Should().Contain("NivelClasificacion");

        durableClient.VerifyNoOtherCalls();
        blobStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_WhenDefaultNivelClasificacionIsInvalid_ReturnsInternalServerError()
    {
        var logger = new Mock<ILogger<IngestAPITrigger>>();
        var blobStorage = new Mock<IBlobStorageService>(MockBehavior.Strict);
        var promptValidator = new PromptInstruccionesValidator(new PromptModelRegistryLoader("dummy.json"));
        var settings = Options.Create(new ClassificationRoutingSettings
        {
            NivelClasificacionDefault = "TDN3"
        });

        var durableClient = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        var function = new IngestAPITrigger(logger.Object, promptValidator, CreateRestriccionValidatorSinCatalogo(), blobStorage.Object, settings);

        var body = JsonSerializer.Serialize(new
        {
            documento = new
            {
                name = "test.pdf",
                blobPath = "documents/test.pdf"
            },
            instrucciones = new
            {
                classification = new { }
            },
            trazabilidad = new
            {
                correlationId = "corr-001"
            }
        });

        var request = HttpFunctionTestFactory.CreateRequest(
            method: "POST",
            url: "http://localhost/api/ingest",
            body: body,
            headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" });

        var response = await function.Run(request, durableClient.Object);
        var responseBody = await HttpFunctionTestFactory.ReadBodyAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        responseBody.Should().Contain("Configuración de clasificación inválida");

        durableClient.VerifyNoOtherCalls();
        blobStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_WhenNivelClasificacionIsTdn1_EnforcesClassificationOnlyInBackend()
    {
        var logger = new Mock<ILogger<IngestAPITrigger>>();
        var blobStorage = new Mock<IBlobStorageService>(MockBehavior.Strict);
        var promptValidator = new PromptInstruccionesValidator(new PromptModelRegistryLoader("dummy.json"));
        var settings = Options.Create(new ClassificationRoutingSettings
        {
            NivelClasificacionDefault = ClassificationLevelResolver.LevelTdn1Tdn2
        });

        ContratoEntrada? scheduledInput = null;
        var durableClient = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        durableClient
            .Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<object?>(),
                It.IsAny<StartOrchestrationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<TaskName, object?, StartOrchestrationOptions?, CancellationToken>((_, input, _, _) =>
            {
                scheduledInput = input as ContratoEntrada;
            })
            .ReturnsAsync("instance-tdn1");

        var function = new IngestAPITrigger(logger.Object, promptValidator, CreateRestriccionValidatorSinCatalogo(), blobStorage.Object, settings);

        var body = JsonSerializer.Serialize(new
        {
            documento = new
            {
                name = "test.pdf",
                blobPath = "documents/test.pdf"
            },
            instrucciones = new
            {
                classificationOnly = false,
                classification = new
                {
                    nivelClasificacion = "TDN1",
                    provider = "auto"
                }
            },
            trazabilidad = new
            {
                correlationId = "corr-tdn1",
                submittedBy = "tester"
            }
        });

        var request = HttpFunctionTestFactory.CreateRequest(
            method: "POST",
            url: "http://localhost/api/ingest",
            body: body,
            headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" });

        var response = await function.Run(request, durableClient.Object);
        var responseBody = await HttpFunctionTestFactory.ReadBodyAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        responseBody.Should().Contain("provider");

        scheduledInput.Should().NotBeNull();
        scheduledInput!.Instrucciones.Classification.NivelClasificacion.Should().Be(ClassificationLevelResolver.LevelTdn1);
        scheduledInput.Instrucciones.Classification.Provider.Should().Be("gpt");
        scheduledInput.Instrucciones.ClassificationOnly.Should().BeTrue();

        durableClient.VerifyAll();
        blobStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_WhenBase64IsInvalid_ReturnsBadRequest()
    {
        var logger = new Mock<ILogger<IngestAPITrigger>>();
        var blobStorage = new Mock<IBlobStorageService>(MockBehavior.Strict);
        var promptValidator = new PromptInstruccionesValidator(new PromptModelRegistryLoader("dummy.json"));
        var settings = Options.Create(new ClassificationRoutingSettings
        {
            NivelClasificacionDefault = ClassificationLevelResolver.LevelTdn1Tdn2
        });

        var durableClient = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        var function = new IngestAPITrigger(logger.Object, promptValidator, CreateRestriccionValidatorSinCatalogo(), blobStorage.Object, settings);

        var body = JsonSerializer.Serialize(new
        {
            documento = new
            {
                name = "test.pdf",
                content = new { base64 = "@@no-base64@@" }
            },
            instrucciones = new
            {
                classification = new { }
            },
            trazabilidad = new
            {
                correlationId = "corr-001"
            }
        });

        var request = HttpFunctionTestFactory.CreateRequest(
            method: "POST",
            url: "http://localhost/api/ingest",
            body: body,
            headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" });

        var response = await function.Run(request, durableClient.Object);
        var responseBody = await HttpFunctionTestFactory.ReadBodyAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseBody.Should().Contain("base64");

        durableClient.VerifyNoOtherCalls();
        blobStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_RestriccionTodosCodigosInvalidos_Devuelve400()
    {
        var logger = new Mock<ILogger<IngestAPITrigger>>();
        var blobStorage = new Mock<IBlobStorageService>(MockBehavior.Strict);
        var promptValidator = new PromptInstruccionesValidator(new PromptModelRegistryLoader("dummy.json"));
        var settings = Options.Create(new ClassificationRoutingSettings
        {
            NivelClasificacionDefault = ClassificationLevelResolver.LevelTdn1Tdn2
        });

        var function = new IngestAPITrigger(
            logger.Object,
            promptValidator,
            CreateRestriccionValidatorConCatalogo("SERE-25"),
            blobStorage.Object,
            settings);

        var body = JsonSerializer.Serialize(new
        {
            documento = new { name = "test.pdf", blobPath = "documents/test.pdf" },
            instrucciones = new
            {
                classification = new { },
                restriccionTipologias = new { codigos = new[] { "SERE-99", "XXXX-01" } }
            },
            trazabilidad = new { correlationId = "corr-001", submittedBy = "tester" }
        });

        var request = HttpFunctionTestFactory.CreateRequest(
            method: "POST",
            url: "http://localhost/api/ingest",
            body: body,
            headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        var durableClient = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");

        var response = await function.Run(request, durableClient.Object);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        durableClient.VerifyNoOtherCalls();
        blobStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_RestriccionConNivelTdn1_Devuelve400()
    {
        var logger = new Mock<ILogger<IngestAPITrigger>>();
        var blobStorage = new Mock<IBlobStorageService>(MockBehavior.Strict);
        var promptValidator = new PromptInstruccionesValidator(new PromptModelRegistryLoader("dummy.json"));
        var settings = Options.Create(new ClassificationRoutingSettings
        {
            NivelClasificacionDefault = ClassificationLevelResolver.LevelTdn1Tdn2
        });

        var function = new IngestAPITrigger(
            logger.Object,
            promptValidator,
            CreateRestriccionValidatorConCatalogo("SERE-25"),
            blobStorage.Object,
            settings);

        var body = JsonSerializer.Serialize(new
        {
            documento = new { name = "test.pdf", blobPath = "documents/test.pdf" },
            instrucciones = new
            {
                classification = new { nivelClasificacion = "TDN1" },
                restriccionTipologias = new { codigos = new[] { "SERE-25" } }
            },
            trazabilidad = new { correlationId = "corr-001", submittedBy = "tester" }
        });

        var request = HttpFunctionTestFactory.CreateRequest(
            method: "POST",
            url: "http://localhost/api/ingest",
            body: body,
            headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        var durableClient = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");

        var response = await function.Run(request, durableClient.Object);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        durableClient.VerifyNoOtherCalls();
        blobStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_ExtensionNoSoportada_Devuelve400()
    {
        // AB#100180: un .zip llegaba hasta DI/LLM y terminaba en resumen N/A con estado OK.
        var logger = new Mock<ILogger<IngestAPITrigger>>();
        var blobStorage = new Mock<IBlobStorageService>(MockBehavior.Strict);
        var promptValidator = new PromptInstruccionesValidator(new PromptModelRegistryLoader("dummy.json"));
        var settings = Options.Create(new ClassificationRoutingSettings
        {
            NivelClasificacionDefault = ClassificationLevelResolver.LevelTdn1Tdn2
        });

        var function = new IngestAPITrigger(logger.Object, promptValidator, CreateRestriccionValidatorSinCatalogo(), blobStorage.Object, settings);

        var body = JsonSerializer.Serialize(new
        {
            documento = new
            {
                name = "adjuntos.zip",
                content = new { base64 = "dGVzdA==" }
            },
            instrucciones = new { classification = new { } },
            trazabilidad = new { correlationId = "corr-001", submittedBy = "tester" }
        });

        var request = HttpFunctionTestFactory.CreateRequest(
            method: "POST",
            url: "http://localhost/api/ingest",
            body: body,
            headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        var durableClient = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");

        var response = await function.Run(request, durableClient.Object);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        durableClient.VerifyNoOtherCalls();
        blobStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_ExtensionSoportada_NoRechazaPorExtension()
    {
        var logger = new Mock<ILogger<IngestAPITrigger>>();
        var blobStorage = new Mock<IBlobStorageService>(MockBehavior.Strict);
        var promptValidator = new PromptInstruccionesValidator(new PromptModelRegistryLoader("dummy.json"));
        var settings = Options.Create(new ClassificationRoutingSettings
        {
            NivelClasificacionDefault = ClassificationLevelResolver.LevelTdn1Tdn2
        });

        var durableClient = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        durableClient
            .Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<object?>(),
                It.IsAny<StartOrchestrationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("instance-ext-ok");

        var function = new IngestAPITrigger(logger.Object, promptValidator, CreateRestriccionValidatorSinCatalogo(), blobStorage.Object, settings);

        var body = JsonSerializer.Serialize(new
        {
            documento = new
            {
                name = "documento.pdf",
                content = new { base64 = "dGVzdA==" }
            },
            instrucciones = new { classification = new { } },
            trazabilidad = new { correlationId = "corr-001", submittedBy = "tester" }
        });

        var request = HttpFunctionTestFactory.CreateRequest(
            method: "POST",
            url: "http://localhost/api/ingest",
            body: body,
            headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" });

        var response = await function.Run(request, durableClient.Object);

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }
}
