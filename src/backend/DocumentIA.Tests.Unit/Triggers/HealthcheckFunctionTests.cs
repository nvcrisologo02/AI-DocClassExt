using System.Net;
using System.Text.Json;
using DocumentIA.Functions.Triggers;
using DocumentIA.Tests.Unit.Helpers;
using FluentAssertions;

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
}
