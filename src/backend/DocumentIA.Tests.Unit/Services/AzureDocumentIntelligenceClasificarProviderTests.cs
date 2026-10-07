#nullable enable
using System.Net;
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
/// AB#100814: el proveedor de clasificacion DI resuelve el documento por ruta (recorte u original);
/// el base64 solo entra por el campo legado.
/// </summary>
public sealed class AzureDocumentIntelligenceClasificarProviderTests : IDisposable
{
    private const string RutaOriginal = "documents/2026/10/original.pdf";
    private const string RutaRecorte = "documents-clasif/2026/10/recorte.pdf";
    private const string UrlSas = "https://srbstgdevdocai.blob.core.windows.net/x?sig=y";
    private static readonly byte[] ContenidoBlob = [1, 2, 3, 4];

    private readonly string _registryPath = Path.Combine(Path.GetTempPath(), $"di-registry-{Guid.NewGuid():N}.json");
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly FakeHandler _handler = new();

    public AzureDocumentIntelligenceClasificarProviderTests()
    {
        File.WriteAllText(_registryPath, """
            {
              "models": [
                {
                  "key": "di-test",
                  "provider": "azure-document-intelligence",
                  "isDefault": true,
                  "endpoint": "https://di.test.local",
                  "apiKey": "clave",
                  "authMode": "ApiKey",
                  "classifierId": "clasificador-test",
                  "pollIntervalMs": 250,
                  "timeoutSeconds": 30
                }
              ]
            }
            """);
        _blob.Setup(b => b.GenerateSasUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(UrlSas);
        _blob.Setup(b => b.DownloadDocumentAsync(It.IsAny<string>())).ReturnsAsync(ContenidoBlob);
    }

    public void Dispose()
    {
        if (File.Exists(_registryPath))
        {
            File.Delete(_registryPath);
        }
    }

    [Fact]
    public async Task ClasificarAsync_ConRutaDeClasificacion_ResuelveElRecorteYNoElOriginal()
    {
        _handler.Respuestas.Enqueue(Iniciada());
        _handler.Respuestas.Enqueue(Exito());

        await CrearSut().ClasificarAsync(CrearInput(rutaClasificacion: RutaRecorte));

        _blob.Verify(b => b.GenerateSasUrlAsync(RutaRecorte, It.IsAny<TimeSpan?>()), Times.Once);
        _blob.Verify(b => b.GenerateSasUrlAsync(RutaOriginal, It.IsAny<TimeSpan?>()), Times.Never);
        _handler.CuerpoPrimeraPeticion.Should().Contain("urlSource");
    }

    [Fact]
    public async Task ClasificarAsync_SinRutaDeClasificacion_ResuelveElDocumentoOriginal()
    {
        _handler.Respuestas.Enqueue(Iniciada());
        _handler.Respuestas.Enqueue(Exito());

        await CrearSut().ClasificarAsync(CrearInput(rutaClasificacion: null));

        _blob.Verify(b => b.GenerateSasUrlAsync(RutaOriginal, It.IsAny<TimeSpan?>()), Times.Once);
        _blob.Verify(b => b.GenerateSasUrlAsync(RutaRecorte, It.IsAny<TimeSpan?>()), Times.Never);
    }

    [Fact]
    public async Task ClasificarAsync_ConBase64LegadoEnElOverride_LoEnviaTalCualSinTocarElBlob()
    {
        _handler.Respuestas.Enqueue(Iniciada());
        _handler.Respuestas.Enqueue(Exito());

        await CrearSut().ClasificarAsync(CrearInput(rutaClasificacion: RutaRecorte, base64Legado: "cmVjb3J0YWRv"));

        _handler.CuerpoPrimeraPeticion.Should().Contain("base64Source").And.Contain("cmVjb3J0YWRv");
        _blob.Verify(b => b.GenerateSasUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
        _blob.Verify(b => b.DownloadDocumentAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ClasificarAsync_ConInvalidContentEnUrlSource_ReintentaInlineConLaRutaDeClasificacion()
    {
        _handler.Respuestas.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":{\"code\":\"InvalidContent\"}}")
        });
        _handler.Respuestas.Enqueue(Iniciada());
        _handler.Respuestas.Enqueue(Exito());

        await CrearSut().ClasificarAsync(CrearInput(rutaClasificacion: RutaRecorte));

        _blob.Verify(b => b.DownloadDocumentAsync(RutaRecorte), Times.Once);
        _blob.Verify(b => b.DownloadDocumentAsync(RutaOriginal), Times.Never);
        _handler.Cuerpos[0].Should().Contain("urlSource");
        _handler.Cuerpos[1].Should().Contain("base64Source").And.Contain(Convert.ToBase64String(ContenidoBlob));
    }

    private AzureDocumentIntelligenceClasificarProvider CrearSut()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(_handler, disposeHandler: false));

        var resolver = new DocumentIntelligenceSourceResolver(
            _blob.Object,
            Options.Create(new DocumentIntelligenceSettings { UseInlineContent = false }),
            NullLogger<DocumentIntelligenceSourceResolver>.Instance);

        return new AzureDocumentIntelligenceClasificarProvider(
            factory.Object,
            new ClassificationModelRegistryLoader(_registryPath),
            resolver,
            NullLogger<AzureDocumentIntelligenceClasificarProvider>.Instance);
    }

    private static ClasificacionInput CrearInput(string? rutaClasificacion, string? base64Legado = null)
    {
        var input = new ClasificacionInput
        {
            Entrada = new ContratoEntrada
            {
                Documento = new Documento
                {
                    Name = "original.pdf",
                    BlobPath = RutaOriginal,
                    Content = new ContenidoDocumento()
                },
                Instrucciones = new Instrucciones()
            },
            DatosNormalizados = new Dictionary<string, object>(),
            BlobPathClasificacion = rutaClasificacion
        };
#pragma warning disable CS0618
        input.DocumentoBase64Override = base64Legado;
#pragma warning restore CS0618
        return input;
    }

    private static HttpResponseMessage Iniciada()
    {
        var respuesta = new HttpResponseMessage(HttpStatusCode.Accepted);
        respuesta.Headers.Add("operation-location", "https://di.test.local/operations/1");
        return respuesta;
    }

    private static HttpResponseMessage Exito() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            "{\"status\":\"succeeded\",\"analyzeResult\":{\"documents\":[{\"docType\":\"escr.compraventa\",\"confidence\":0.9}],\"pages\":[{}]}}")
    };

    private sealed class FakeHandler : HttpMessageHandler
    {
        public Queue<HttpResponseMessage> Respuestas { get; } = new();
        public List<string> Cuerpos { get; } = [];
        public string CuerpoPrimeraPeticion => Cuerpos.Count > 0 ? Cuerpos[0] : string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                Cuerpos.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return Respuestas.Dequeue();
        }
    }
}
