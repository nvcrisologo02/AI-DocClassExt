using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Models;
using System.Net;
using System.Text.Json;

namespace DocumentIA.Functions.Triggers;

public class IngestAPITrigger
{
    private readonly ILogger<IngestAPITrigger> _logger;

    public IngestAPITrigger(ILogger<IngestAPITrigger> logger)
    {
        _logger = logger;
    }

    [Function("IngestDocument")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        _logger.LogInformation("Recibiendo documento para procesamiento");

        try
        {
            // Leer el body
            var requestBody = await req.ReadAsStringAsync();
            var contratoEntrada = JsonSerializer.Deserialize<ContratoEntrada>(requestBody!);

            if (contratoEntrada == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Contrato de entrada inválido");
                return badResponse;
            }

            // Iniciar orquestación
            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                "DocumentProcessOrchestrator",
                contratoEntrada);

            _logger.LogInformation($"Orquestación iniciada con ID: {instanceId}");

            // Responder con el ID de instancia
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                instanceId,
                statusQueryUri = $"{req.Url.Scheme}://{req.Url.Host}/runtime/webhooks/durabletask/instances/{instanceId}",
                correlationId = contratoEntrada.Trazabilidad.CorrelationId
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando solicitud");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}
