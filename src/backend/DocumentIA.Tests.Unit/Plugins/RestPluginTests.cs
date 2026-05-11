using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Plugins.Integration;
using Xunit;
using Moq;
using Moq.Protected;

namespace DocumentIA.Tests.Plugins
{
    public class RestPluginTests
    {
        [Fact]
        public async Task ExecuteAsync_SuccessfulRequest_ReturnsOkResult()
        {
            // Arrange
            var mockResponse = new { message = "Success", id = 123 };
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var plugin = new RestPlugin(httpClient);

            var config = new Dictionary<string, object>
            {
                ["baseUrl"] = "https://api.example.com",
                ["authToken"] = "test-token",
                ["timeoutSeconds"] = 30
            };

            await plugin.InitializeAsync(config);

            var data = new Dictionary<string, object>
            {
                ["endpoint"] = "/api/process",
                ["method"] = "POST",
                ["payload"] = new { test = "data" }
            };

            // Act
            var result = await plugin.ExecuteAsync(data);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("OK", result.Status);
            Assert.Equal(200, result.StatusCode);
            Assert.NotEmpty(result.ResponseData);
        }

        [Fact]
        public async Task ExecuteAsync_HttpError_ReturnsErrorResult()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    ReasonPhrase = "Internal Server Error"
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var plugin = new RestPlugin(httpClient);

            var config = new Dictionary<string, object>
            {
                ["baseUrl"] = "https://api.example.com"
            };

            await plugin.InitializeAsync(config);

            var data = new Dictionary<string, object>
            {
                ["endpoint"] = "/api/fail"
            };

            // Act
            var result = await plugin.ExecuteAsync(data);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("ERROR", result.Status);
            Assert.Equal(500, result.StatusCode);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public async Task ExecuteAsync_Timeout_ReturnsErrorWithTransientFlag()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timeout"));

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var plugin = new RestPlugin(httpClient);

            var config = new Dictionary<string, object>
            {
                ["baseUrl"] = "https://api.example.com",
                ["timeoutSeconds"] = 1
            };

            await plugin.InitializeAsync(config);

            var data = new Dictionary<string, object>
            {
                ["endpoint"] = "/api/slow"
            };

            // Act
            var result = await plugin.ExecuteAsync(data);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("ERROR", result.Status);
            Assert.Contains("Timeout", result.Message);
            Assert.True((bool)result.Metadata["isTransient"]);
        }

        [Fact]
        public async Task InitializeAsync_WithContentTypeHeader_DoesNotThrow()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var plugin = new RestPlugin(httpClient);

            var config = new Dictionary<string, object>
            {
                ["baseUrl"] = "https://api.example.com",
                ["headers"] = new Dictionary<string, object>
                {
                    ["Content-Type"] = "application/json",
                    ["X-Client-Id"] = "DocumentIA"
                }
            };

            // Act
            var exception = await Record.ExceptionAsync(() => plugin.InitializeAsync(config));

            // Assert
            Assert.Null(exception);
            Assert.True(httpClient.DefaultRequestHeaders.Contains("X-Client-Id"));
        }
    }
}
