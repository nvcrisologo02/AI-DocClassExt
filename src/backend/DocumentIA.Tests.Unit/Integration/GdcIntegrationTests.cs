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

namespace DocumentIA.Tests.Unit.Integration
{
    // Integration-like tests that run against a deterministic fake HTTP handler
    public class GdcIntegrationTests
    {
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var content = request.Content?.ReadAsStringAsync().Result ?? string.Empty;

                if (content.Contains("searchEntities") || content.Contains("entityTypeId") )
                {
                    if (content.Contains("md5-exists") || content.Contains("this-exists") || content.Contains("some-exists-id"))
                    {
                        var xml = "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><searchEntitiesResponse xmlns=\"http://services.api.sint.sareb.es/\"><return><entities><entityId>GDC-12345</entityId></entities></return></searchEntitiesResponse></soap:Body></soap:Envelope>";
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) });
                    }

                    var xmlNo = "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><searchEntitiesResponse xmlns=\"http://services.api.sint.sareb.es/\"><return></return></searchEntitiesResponse></soap:Body></soap:Envelope>";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xmlNo) });
                }

                if (content.Contains(":create") || content.Contains("ns1:create"))
                {
                    if (content.Contains("already") || content.Contains("Already") || content.Contains("already-file"))
                    {
                        var already = "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Error</faultstring><detail><ServiceException xmlns=\"http://exceptions.model.api.sint.sareb.es\"><errorCode>DOC_OBJECT_EXISTS</errorCode></ServiceException></detail></soap:Fault></soap:Body></soap:Envelope>";
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(already) });
                    }

                    var ok = "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><ns1:createResponse xmlns:ns1=\"http://services.api.sint.sareb.es/\"><ns1:return>GDC-UPLOAD-1</ns1:return></ns1:createResponse></soap:Body></soap:Envelope>";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ok) });
                }

                if (content.Contains("SubirDocumentoRequest") || content.Contains("SubirDocumentoResponse"))
                {
                    if (content.Contains("already") || content.Contains("Already") || content.Contains("already-file"))
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

        private class SimpleHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;
            public SimpleHttpClientFactory(HttpClient client) => _client = client;
            public HttpClient CreateClient(string name) => _client;
        }

        private GdcService CreateService(HttpMessageHandler handler)
        {
            var client = new HttpClient(handler);
            var factory = new SimpleHttpClientFactory(client);
            var options = Options.Create(new GdcSettings { Endpoint = "http://example.local/", TimeoutSeconds = 10 });
            var logger = new NullLogger<GdcService>();
            return new GdcService(factory, options, logger);
        }

        [Fact]
        public async Task SubirDocumento_EndToEnd_With_FakeHttpHandler_ReturnsObjectId()
        {
            var svc = CreateService(new FakeHttpMessageHandler());

            var input = new SubirGDCInput
            {
                IdActivo = "activo-123",
                NombreArchivo = "test-file.pdf",
                ContenidoBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test")),
                Matricula = "MATR",
                SHA256 = "abc"
            };

            var res = await svc.SubirDocumentoAsync(input);
            Assert.True(res.Exitoso);
            Assert.False(string.IsNullOrWhiteSpace(res.ObjectId));
        }

        [Fact]
        public async Task ConsultarDocumento_EndToEnd_With_FakeHttpHandler_ReturnsExists()
        {
            var svc = CreateService(new FakeHttpMessageHandler());

            var (exists, objectId) = await svc.ConsultarDocumentoAsync("this-exists-999", "md5-exists", "MATR");
            Assert.True(exists);
            Assert.Equal("GDC-12345", objectId);
        }
    }
}
