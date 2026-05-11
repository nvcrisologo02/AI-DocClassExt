using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Plugins.Integration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Plugins
{
    public class IdActivoPropagationTests
    {
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public FakeHttpMessageHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }

        [Fact]
        public async Task RestPlugin_PreservesIdActivoWhenResponseLacksIt()
        {
            // Arrange: fake HTTP returns JSON without idActivo
            var json = "{ \"some\": \"value\" }";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };

            var handler = new FakeHttpMessageHandler(response);
            var client = new HttpClient(handler);

            var plugin = new RestPlugin(client);
            var config = new Dictionary<string, object>
            {
                ["baseUrl"] = "http://localhost:8080",
                ["endpoint"] = "/",
                ["timeoutSeconds"] = 5
            };
            await plugin.InitializeAsync(config);

            var payload = new Dictionary<string, object>
            {
                ["datosExtraidos"] = new Dictionary<string, object> { ["campo"] = "x" },
                ["idActivo"] = "ACT-123"
            };

            // Act
            var result = await plugin.ExecuteAsync(payload);

            // Assert
            result.Success.Should().BeTrue();
            result.ResponseData.Should().ContainKey("idActivo");
            result.ResponseData["idActivo"].ToString().Should().Be("ACT-123");
        }

        [Fact]
        public async Task SoapPlugin_PreservesIdActivoWhenResponseLacksIt()
        {
            // Arrange: SOAP response without idActivo
            var soap =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "<soap:Body><Response><Field>A</Field></Response></soap:Body></soap:Envelope>";

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(soap)
            };

            var handler = new FakeHttpMessageHandler(response);
            var client = new HttpClient(handler);

            var loggerMock = new Mock<ILogger<SoapPlugin>>();
            var plugin = new SoapPlugin(client, loggerMock.Object);
            var config = new Dictionary<string, object>
            {
                ["endpoint"] = "http://localhost:8081",
                ["soapVersion"] = "1.1",
                ["timeoutSeconds"] = 5
            };
            await plugin.InitializeAsync(config);

            var payload = new Dictionary<string, object>
            {
                ["datosExtraidos"] = new Dictionary<string, object> { ["campo"] = "x" },
                ["idActivo"] = "ACT-456"
            };

            // Act
            var result = await plugin.ExecuteAsync(payload);

            // Assert
            result.Success.Should().BeTrue();
            result.ResponseData.Should().ContainKey("Field");
            result.ResponseData.Should().ContainKey("idActivo");
            result.ResponseData["idActivo"].ToString().Should().Be("ACT-456");
        }
    }
}
