using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DocumentIA.Tests.Unit.Services
{
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        // Last captured request — used by tests to assert on the outgoing SOAP XML
        public HttpRequestMessage? LastRequest { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content?.ReadAsStringAsync().Result ?? string.Empty;
            var content = LastRequestBody;

            if (content.Contains("searchEntities"))
            {
                if (content.Contains("md5-exists") || content.Contains("some-exists-id"))
                {
                    // SOAP 1.2 response — totalItemsResult + Entity/id (confirmed format from POC tests)
                    var xml = "<?xml version=\"1.0\"?>" +
                              "<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\"><soap:Body>" +
                              "<searchEntitiesResponse xmlns=\"http://services.api.sint.sareb.es/\"><return>" +
                              "<totalItemsResult>1</totalItemsResult>" +
                              "<data><Entity xmlns=\"http://data.model.api.sint.sareb.es\"><id>GDC-12345</id></Entity></data>" +
                              "</return></searchEntitiesResponse></soap:Body></soap:Envelope>";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) });
                }

                // Not found: totalItemsResult=0
                var xmlNo = "<?xml version=\"1.0\"?>" +
                            "<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\"><soap:Body>" +
                            "<searchEntitiesResponse xmlns=\"http://services.api.sint.sareb.es/\"><return>" +
                            "<totalItemsResult>0</totalItemsResult><data/>" +
                            "</return></searchEntitiesResponse></soap:Body></soap:Envelope>";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xmlNo) });
            }

            if (content.Contains(":create") || content.Contains("ns1:create"))
            {
                if (content.Contains("already") || content.Contains("Already"))
                {
                    // DOC_OBJECT_EXISTS as SOAP 1.1 unqualified fault (service may return either version)
                    var already = "<?xml version=\"1.0\"?>" +
                                  "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
                                  "<soap:Fault><faultcode>soap:Server</faultcode><faultstring>Error</faultstring>" +
                                  "<detail><ServiceException xmlns=\"http://exceptions.model.api.sint.sareb.es\">" +
                                  "<errorCode>DOC_OBJECT_EXISTS</errorCode></ServiceException></detail>" +
                                  "</soap:Fault></soap:Body></soap:Envelope>";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(already) });
                }

                if (content.Contains("soap12fault"))
                {
                    // SOAP 1.2 fault with Reason/Text (e.g. generic server error)
                    var fault12 = "<?xml version=\"1.0\"?>" +
                                  "<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\"><soap:Body>" +
                                  "<soap:Fault>" +
                                  "<soap:Code><soap:Value>soap:Receiver</soap:Value></soap:Code>" +
                                  "<soap:Reason><soap:Text xml:lang=\"en\">Generic server error</soap:Text></soap:Reason>" +
                                  "</soap:Fault></soap:Body></soap:Envelope>";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(fault12) });
                }

                var ok = "<?xml version=\"1.0\"?>" +
                         "<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\"><soap:Body>" +
                         "<ns1:createResponse xmlns:ns1=\"http://services.api.sint.sareb.es/\"><ns1:return>GDC-UPLOAD-1</ns1:return></ns1:createResponse>" +
                         "</soap:Body></soap:Envelope>";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ok) });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }
    }

    public class SimpleHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public SimpleHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    public class GdcServiceTests
    {
        private GdcService CreateService(HttpMessageHandler handler)
        {
            var client = new HttpClient(handler);
            var factory = new SimpleHttpClientFactory(client);
            var options = Options.Create(new GdcSettings { Endpoint = "http://example.local/", TimeoutSeconds = 10 });
            var logger = new NullLogger<GdcService>();
            return new GdcService(factory, options, logger);
        }

        [Fact]
        public async Task ConsultarDocumento_Returns_ObjectId_When_Exists()
        {
            var svc = CreateService(new FakeHttpMessageHandler());
            var (exists, objectId) = await svc.ConsultarDocumentoAsync("some-exists-id", "md5-exists", "MAT1");
            Assert.True(exists);
            Assert.Equal("GDC-12345", objectId);
        }

        [Fact]
        public async Task ConsultarDocumento_Returns_False_When_NotExists()
        {
            var svc = CreateService(new FakeHttpMessageHandler());
            var (exists, objectId) = await svc.ConsultarDocumentoAsync("noexist", "md5-noexists", "MAT1");
            Assert.False(exists);
            Assert.Null(objectId);
        }

        [Fact]
        public async Task SubirDocumento_Returns_ObjectId_On_Success()
        {
            var svc = CreateService(new FakeHttpMessageHandler());
            var input = new SubirGDCInput { IdActivo = "A1", NombreArchivo = "file.pdf", ContenidoBase64 = "dGVzdA==", Matricula = "M1", SHA256 = "abc" };
            var res = await svc.SubirDocumentoAsync(input);
            Assert.True(res.Exitoso);
            Assert.Equal("GDC-UPLOAD-1", res.ObjectId);
        }

        [Fact]
        public async Task SubirDocumento_AlreadyExists_Returns_YaExistia()
        {
            var svc = CreateService(new FakeHttpMessageHandler());
            var input = new SubirGDCInput { IdActivo = "A1", NombreArchivo = "already-file.pdf", ContenidoBase64 = "dGVzdA==", Matricula = "M1", SHA256 = "abc" };
            var res = await svc.SubirDocumentoAsync(input);
            Assert.True(res.Exitoso);
            Assert.True(res.YaExistia);
        }

        [Fact]
        public async Task ConsultarDocumento_Request_UsesSoap12EnvelopeAndContentType()
        {
            var handler = new FakeHttpMessageHandler();
            var svc = CreateService(handler);
            await svc.ConsultarDocumentoAsync("some-exists-id", "md5-exists", "MAT1");

            Assert.NotNull(handler.LastRequest);
            Assert.Equal("application/soap+xml", handler.LastRequest!.Content!.Headers.ContentType!.MediaType);
            Assert.Contains("http://www.w3.org/2003/05/soap-envelope", handler.LastRequestBody);
        }

        [Fact]
        public async Task ConsultarDocumento_Request_ContainsEntityExpressionIN_ForExpediente()
        {
            var handler = new FakeHttpMessageHandler();
            var svc = CreateService(handler);
            await svc.ConsultarDocumentoAsync("ACT-99", "anymd5", "MAT1");

            Assert.Contains("EntityExpression", handler.LastRequestBody);
            Assert.Contains("IN", handler.LastRequestBody);
            Assert.Contains("StringValueList", handler.LastRequestBody);
            Assert.Contains("id_expediente", handler.LastRequestBody);
            Assert.Contains("ACT-99", handler.LastRequestBody);
        }

        [Fact]
        public async Task ConsultarDocumento_Request_ContainsMd5FieldExpressionEQUALS()
        {
            var handler = new FakeHttpMessageHandler();
            var svc = CreateService(handler);
            await svc.ConsultarDocumentoAsync("ACT-99", "myhash123", "MAT1");

            Assert.Contains("FieldExpression", handler.LastRequestBody);
            Assert.Contains("EQUALS", handler.LastRequestBody);
            Assert.Contains("checksum", handler.LastRequestBody);
            Assert.Contains("myhash123", handler.LastRequestBody);
        }

        [Fact]
        public async Task SubirDocumento_Request_UsesSoap12EnvelopeAndContentType()
        {
            var handler = new FakeHttpMessageHandler();
            var svc = CreateService(handler);
            var input = new SubirGDCInput { IdActivo = "A1", NombreArchivo = "check-envelope.pdf", ContenidoBase64 = "dGVzdA==", Matricula = "M1", SHA256 = "abc" };
            await svc.SubirDocumentoAsync(input);

            Assert.NotNull(handler.LastRequest);
            Assert.Equal("application/soap+xml", handler.LastRequest!.Content!.Headers.ContentType!.MediaType);
            Assert.Contains("http://www.w3.org/2003/05/soap-envelope", handler.LastRequestBody);
        }

        [Fact]
        public async Task SubirDocumento_Soap12Fault_ParsesReasonText_As_Mensaje()
        {
            // Trigger the soap12fault branch in the handler via filename
            var svc = CreateService(new FakeHttpMessageHandler());
            var input = new SubirGDCInput { IdActivo = "A1", NombreArchivo = "soap12fault.pdf", ContenidoBase64 = "dGVzdA==", Matricula = "M1", SHA256 = "abc" };
            var res = await svc.SubirDocumentoAsync(input);

            Assert.False(res.Exitoso);
            Assert.Equal("Generic server error", res.Mensaje);
        }

        [Fact]
        public async Task SubirDocumento_Request_ContainsMandatoryGdcFields_WhenSet()
        {
            // All mandatory GDC fields (tipo_documento, serie, servicer, entidad_origen,
            // proceso_carga, publico, tipo_expediente, nombre_documento) must appear in the
            // SOAP body when configured.
            var handler = new FakeHttpMessageHandler();
            var client = new HttpClient(handler);
            var factory = new SimpleHttpClientFactory(client);
            var options = Options.Create(new GdcSettings
            {
                Endpoint = "http://example.local/",
                TimeoutSeconds = 10,
                OrigenDocumento = "CK01",
                ClaseExpediente = "AI04",
                ContentFieldName = "Content",
                Servicer = "9999",
                EntidadOrigen = "9999",
                ProcesoCarga = "PC01",
                TipoExpediente = "AA",
                Publico = "verdadero",
            });
            var svc = new GdcService(factory, options, new NullLogger<GdcService>());
            var input = new SubirGDCInput
            {
                IdActivo = "1058669",
                NombreArchivo = "nota_simple.pdf",
                ContenidoBase64 = "dGVzdA==",
                Matricula = "AI-01-NOTS-01",
                MD5 = "abc123",
                TipoDocumento = "NOTS",
                SubtipoDocumento = "NOTS01",
                Serie = "AI01",
            };
            await svc.SubirDocumentoAsync(input);

            var body = handler.LastRequestBody;
            // Mandatory catalog fields
            Assert.Contains("tipo_documento", body);
            Assert.Contains("NOTS", body);
            Assert.Contains("subtipo_documento", body);
            Assert.Contains("NOTS01", body);
            Assert.Contains("serie", body);
            Assert.Contains("AI01", body);
            // Mandatory global fields
            Assert.Contains("entidad_origen", body);
            Assert.Contains("9999", body);
            Assert.Contains("proceso_carga", body);
            Assert.Contains("PC01", body);
            Assert.Contains("publico", body);
            Assert.Contains("verdadero", body);
            Assert.Contains("servicer", body);
            Assert.Contains("tipo_expediente", body);
            Assert.Contains("AI", body);
            // nombre_documento should fall back to NombreArchivo
            Assert.Contains("nombre_documento", body);
            Assert.Contains("nota_simple.pdf", body);
        }
    }
}
