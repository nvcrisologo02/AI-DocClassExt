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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.Content?.ReadAsStringAsync().Result ?? string.Empty;

            if (content.Contains("searchEntities"))
            {
                if (content.Contains("md5-exists") || content.Contains("some-exists-id"))
                {
                    var xml = "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><searchEntitiesResponse xmlns=\"http://services.api.sint.sareb.es/\"><return><entities><entityId>GDC-12345</entityId></entities></return></searchEntitiesResponse></soap:Body></soap:Envelope>";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) });
                }

                var xmlNo = "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><searchEntitiesResponse xmlns=\"http://services.api.sint.sareb.es/\"><return></return></searchEntitiesResponse></soap:Body></soap:Envelope>";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xmlNo) });
            }

            if (content.Contains("createRequest") || content.Contains(":create") || content.Contains("ns1:create"))
            {
                if (content.Contains("already") || content.Contains("Already"))
                {
                    var already = "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Error</faultstring><detail><ServiceException xmlns=\"http://exceptions.model.api.sint.sareb.es\"><errorCode>DOC_OBJECT_EXISTS</errorCode></ServiceException></detail></soap:Fault></soap:Body></soap:Envelope>";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(already) });
                }

                var ok = "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><ns1:createResponse xmlns:ns1=\"http://services.api.sint.sareb.es/\"><ns1:return>GDC-UPLOAD-1</ns1:return></ns1:createResponse></soap:Body></soap:Envelope>";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ok) });
            }

            if (content.Contains("SubirDocumentoRequest"))
            {
                if (content.Contains("already") || content.Contains("Already"))
                {
                    var already = "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><SubirDocumentoResponse xmlns=\"http://sintws.example.org/\"><AlreadyExists>1</AlreadyExists></SubirDocumentoResponse></soap:Body></soap:Envelope>";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(already) });
                }

                var ok = "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><SubirDocumentoResponse xmlns=\"http://sintws.example.org/\"><ObjectId>GDC-UPLOAD-1</ObjectId></SubirDocumentoResponse></soap:Body></soap:Envelope>";
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
    }
}
