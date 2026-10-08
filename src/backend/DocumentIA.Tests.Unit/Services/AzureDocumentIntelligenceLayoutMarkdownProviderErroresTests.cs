using System.Net;
using System.Text;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// AB#100880: cuando Document Intelligence responde con error, el proveedor de layout lanza una
/// excepcion que conserva el codigo HTTP, para que el resolutor pueda distinguir un fallo
/// transitorio (429, 5xx) de uno definitivo (400, 401) sin parsear el mensaje.
/// </summary>
public sealed class AzureDocumentIntelligenceLayoutMarkdownProviderErroresTests : IDisposable
{
    private readonly string _registryPath;

    public AzureDocumentIntelligenceLayoutMarkdownProviderErroresTests()
    {
        _registryPath = Path.Combine(Path.GetTempPath(), $"layout-models-{Guid.NewGuid():N}.json");
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""layout.prebuilt-layout"",
                    ""provider"": ""azure-document-intelligence"",
                    ""isDefault"": true,
                    ""endpoint"": ""https://fake-di.cognitiveservices.azure.com/"",
                    ""apiKey"": ""fake-key"",
                    ""authMode"": ""ApiKey"",
                    ""apiVersion"": ""2024-11-30"",
                    ""timeoutSeconds"": 10,
                    ""pollIntervalMs"": 250
                }
            ]
        }");
    }

    public void Dispose()
    {
        if (File.Exists(_registryPath))
        {
            File.Delete(_registryPath);
        }
    }

    private sealed class RespuestasEnCola : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respuestas;

        public RespuestasEnCola(params HttpResponseMessage[] respuestas) => _respuestas = new Queue<HttpResponseMessage>(respuestas);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respuestas.Dequeue());
    }

    private AzureDocumentIntelligenceLayoutMarkdownProvider CrearSut(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));

        var sourceResolver = new DocumentIntelligenceSourceResolver(
            new Mock<IBlobStorageService>().Object,
            Options.Create(new DocumentIntelligenceSettings()),
            NullLogger<DocumentIntelligenceSourceResolver>.Instance);

        return new AzureDocumentIntelligenceLayoutMarkdownProvider(
            factory.Object,
            new LayoutModelRegistryLoader(_registryPath),
            sourceResolver,
            NullLogger<AzureDocumentIntelligenceLayoutMarkdownProvider>.Instance);
    }

    private static ExtraerMarkdownLayoutInput Input() => new()
    {
        Tipologia = "nota.simple",
        NombreDocumento = "doc.pdf",
        DocumentoBase64 = "dGVzdA=="
    };

    private static HttpResponseMessage Respuesta(HttpStatusCode codigo, string cuerpo)
        => new(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task DiRechazaElInicio_LanzaLayoutRequestExceptionConElCodigoHttp()
    {
        var sut = CrearSut(new RespuestasEnCola(
            Respuesta(HttpStatusCode.Unauthorized, "{\"error\":{\"code\":\"PermissionDenied\",\"message\":\"Principal does not have access.\"}}")));

        var accion = () => sut.ExtraerMarkdownAsync(Input());

        var ex = await accion.Should().ThrowAsync<LayoutRequestException>();
        ex.Which.CodigoHttp.Should().Be(401);
        ex.Which.Message.Should().Contain("PermissionDenied");
    }

    [Fact]
    public async Task DiFallaAlConsultarElResultado_LanzaLayoutRequestExceptionConElCodigoHttp()
    {
        var inicio = Respuesta(HttpStatusCode.Accepted, string.Empty);
        inicio.Headers.Add("operation-location", "https://fake-di.cognitiveservices.azure.com/analyzeResults/op-1");

        var sut = CrearSut(new RespuestasEnCola(
            inicio,
            Respuesta(HttpStatusCode.ServiceUnavailable, "{\"error\":{\"code\":\"ServiceUnavailable\"}}")));

        var accion = () => sut.ExtraerMarkdownAsync(Input());

        var ex = await accion.Should().ThrowAsync<LayoutRequestException>();
        ex.Which.CodigoHttp.Should().Be(503);
    }

    [Fact]
    public async Task LayoutRequestException_SigueSiendoInvalidOperationException()
    {
        // Quien ya capturaba InvalidOperationException no debe verse afectado.
        var sut = CrearSut(new RespuestasEnCola(Respuesta(HttpStatusCode.BadRequest, "{\"error\":{\"code\":\"InvalidContent\"}}")));

        var accion = () => sut.ExtraerMarkdownAsync(Input());

        await accion.Should().ThrowAsync<InvalidOperationException>();
    }
}
