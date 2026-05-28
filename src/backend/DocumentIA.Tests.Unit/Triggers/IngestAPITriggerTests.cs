using System.Net;
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Triggers;
using DocumentIA.Tests.Unit.Helpers;
using FluentAssertions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentIA.Tests.Unit.Triggers;

public class IngestAPITriggerTests
{
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

        var function = new IngestAPITrigger(logger.Object, promptValidator, blobStorage.Object, settings);

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
        var function = new IngestAPITrigger(logger.Object, promptValidator, blobStorage.Object, settings);

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
        var function = new IngestAPITrigger(logger.Object, promptValidator, blobStorage.Object, settings);

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

        var function = new IngestAPITrigger(logger.Object, promptValidator, blobStorage.Object, settings);

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
}
