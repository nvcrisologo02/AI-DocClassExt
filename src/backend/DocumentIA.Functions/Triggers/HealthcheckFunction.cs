using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DocumentIA.Functions.Triggers;

public class HealthcheckFunction
{
    [Function("PostHealthcheck")]
    public async Task<HttpResponseData> PostHealthcheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "healthcheck")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(new
        {
            ok = true,
            timestamp = DateTimeOffset.UtcNow
        }));
        return response;
    }
}
