using System.Net;
using System.Text.Json;
using DocumentIA.Functions.Triggers;
using DocumentIA.Tests.Unit.Helpers;
using FluentAssertions;
using Moq;
using DocumentIA.Functions.Services;

namespace DocumentIA.Tests.Unit.Triggers;

public class HealthcheckFunctionTests
{
    [Fact]
    public async Task PostHealthcheck_ReturnsOkAndTimestamp()
    {
        var function = new HealthcheckFunction();
        var request = HttpFunctionTestFactory.CreateRequest(method: "POST", url: "http://localhost/api/healthcheck");

        var response = await function.PostHealthcheck(request);
        var body = await HttpFunctionTestFactory.ReadBodyAsync(response);
        var json = JsonDocument.Parse(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        DateTimeOffset.Parse(json.RootElement.GetProperty("timestamp").GetString()!).UtcDateTime.Should().NotBe(default);
    }
    
    // ------------------------------------------------------------------
    // Con SystemHealthService mockeado
    // ------------------------------------------------------------------

    [Fact]
    public async Task PostHealthcheck_WhenAllHealthy_ReturnsOkStatusHealthy()
    {
        var snapshot = AllHealthySnapshot();
        var function = BuildFunctionWithSnapshot(snapshot);
        var request = HttpFunctionTestFactory.CreateRequest(method: "POST", url: "http://localhost/api/healthcheck");

        var response = await function.PostHealthcheck(request);
        var json = JsonDocument.Parse(await HttpFunctionTestFactory.ReadBodyAsync(response));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("status").GetString().Should().Be("healthy");
        json.RootElement.GetProperty("components").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task PostHealthcheck_WhenComponentUnconfigured_ReturnsOkStatusUnconfigured()
    {
        var snapshot = new ComponentsHealthSnapshot
        {
            Functions = ComponentHealth.Healthy(),
            AssetResolver = ComponentHealth.Unconfigured("AssetResolver:BaseUrl not configured"),
            Gdc = ComponentHealth.Healthy(),
            ModelProviders = AllHealthyModelProviders()
        };
        var function = BuildFunctionWithSnapshot(snapshot);
        var request = HttpFunctionTestFactory.CreateRequest(method: "POST", url: "http://localhost/api/healthcheck");

        var response = await function.PostHealthcheck(request);
        var json = JsonDocument.Parse(await HttpFunctionTestFactory.ReadBodyAsync(response));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("status").GetString().Should().Be("unconfigured");
    }

    [Fact]
    public async Task PostHealthcheck_WhenAssetResolverFails_ReturnsDegraded()
    {
        var snapshot = new ComponentsHealthSnapshot
        {
            Functions = ComponentHealth.Healthy(),
            AssetResolver = ComponentHealth.Degraded("HTTP 503"),
            Gdc = ComponentHealth.Healthy(),
            ModelProviders = AllHealthyModelProviders()
        };
        var function = BuildFunctionWithSnapshot(snapshot);
        var request = HttpFunctionTestFactory.CreateRequest(method: "POST", url: "http://localhost/api/healthcheck");

        var response = await function.PostHealthcheck(request);
        var json = JsonDocument.Parse(await HttpFunctionTestFactory.ReadBodyAsync(response));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("status").GetString().Should().Be("degraded");
    }

    [Fact]
    public async Task PostHealthcheck_WhenGdcFails_ReturnsDegraded()
    {
        var snapshot = new ComponentsHealthSnapshot
        {
            Functions = ComponentHealth.Healthy(),
            AssetResolver = ComponentHealth.Healthy(),
            Gdc = ComponentHealth.Degraded("Timeout"),
            ModelProviders = AllHealthyModelProviders()
        };
        var function = BuildFunctionWithSnapshot(snapshot);
        var request = HttpFunctionTestFactory.CreateRequest(method: "POST", url: "http://localhost/api/healthcheck");

        var response = await function.PostHealthcheck(request);
        var json = JsonDocument.Parse(await HttpFunctionTestFactory.ReadBodyAsync(response));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("status").GetString().Should().Be("degraded");
    }

    [Fact]
    public async Task PostHealthcheck_WhenUnhealthy_Returns503AndOkFalse()
    {
        var snapshot = new ComponentsHealthSnapshot
        {
            Functions = ComponentHealth.Healthy(),
            AssetResolver = ComponentHealth.Unhealthy("Connection refused"),
            Gdc = ComponentHealth.Unhealthy("Connection refused"),
            ModelProviders = AllHealthyModelProviders()
        };
        var function = BuildFunctionWithSnapshot(snapshot);
        var request = HttpFunctionTestFactory.CreateRequest(method: "POST", url: "http://localhost/api/healthcheck");

        var response = await function.PostHealthcheck(request);
        var json = JsonDocument.Parse(await HttpFunctionTestFactory.ReadBodyAsync(response));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        json.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("status").GetString().Should().Be("unhealthy");
    }

    // ------------------------------------------------------------------
    // SystemHealthService.AggregateStatus unit tests
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(new[] { "healthy", "healthy", "healthy" }, "healthy")]
    [InlineData(new[] { "healthy", "unconfigured", "healthy" }, "unconfigured")]
    [InlineData(new[] { "healthy", "degraded", "healthy" }, "degraded")]
    [InlineData(new[] { "degraded", "unconfigured", "healthy" }, "degraded")]
    [InlineData(new[] { "unhealthy", "healthy", "healthy" }, "unhealthy")]
    [InlineData(new[] { "unhealthy", "degraded", "healthy" }, "unhealthy")]
    public void AggregateStatus_ReturnsExpected(string[] statuses, string expected)
    {
        SystemHealthService.AggregateStatus(statuses).Should().Be(expected);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static HealthcheckFunction BuildFunctionWithSnapshot(ComponentsHealthSnapshot snapshot)
    {
        var mockService = new Mock<ISystemHealthService>();
        mockService
            .Setup(s => s.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        return new HealthcheckFunction(mockService.Object);
    }

    private static ComponentsHealthSnapshot AllHealthySnapshot() => new()
    {
        Functions = ComponentHealth.Healthy("Running"),
        AssetResolver = ComponentHealth.Healthy("HTTP 200"),
        Gdc = ComponentHealth.Healthy("Reachable"),
        ModelProviders = AllHealthyModelProviders()
    };

    private static ModelProvidersHealth AllHealthyModelProviders() => new()
    {
        Status = ComponentHealth.StatusHealthy,
        Classification = ComponentHealth.Healthy("Loader registered"),
        Extraction = ComponentHealth.Healthy("Loader registered"),
        Prompt = ComponentHealth.Healthy("Loader registered")
    };
}
