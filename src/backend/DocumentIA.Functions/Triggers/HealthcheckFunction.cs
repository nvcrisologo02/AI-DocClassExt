using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DocumentIA.Functions.Triggers;

public class HealthcheckFunction
{
    [Function("PostHealthcheck")]
    public async Task<HttpResponseData> PostHealthcheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "healthcheck")] HttpRequestData req)
    {
        throw new NotImplementedException();
    }
}
