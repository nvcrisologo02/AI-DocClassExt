using DocumentIA.AssetResolver.Data;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DocumentIA.AssetResolver.Controllers;

/// <summary>
/// Endpoint principal del plugin AssetResolver.
/// Recibe campos de entrada y devuelve información enriquecida sobre activos.
/// </summary>
[ApiController]
[Route("api/assets")]
public class AssetResolverController : ControllerBase
{
    private readonly AssetResolverDbContext _db;
    private readonly ILogger<AssetResolverController> _logger;

    public AssetResolverController(AssetResolverDbContext db, ILogger<AssetResolverController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("GetAAIIInfo")]
    [ProducesResponseType(typeof(GetAAIIInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<GetAAIIInfoResponse> GetAAIIInfo([FromBody] GetAAIIInfoRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var extractedFieldsCount = request.ExtractedData?.Count ?? 0;

        _logger.LogInformation(
            "GetAAIIInfo recibido. CorrelationId={CorrelationId}, DocumentType={DocumentType}, Fields={Fields}",
            request.CorrelationId,
            request.DocumentType,
            extractedFieldsCount);

        // Placeholder: aquí irá la lógica de consulta a BD y mapeo de respuesta.
        var response = new GetAAIIInfoResponse
        {
            CorrelationId = request.CorrelationId,
            Found = false,
            Message = "Endpoint operativo. Logica pendiente de implementar.",
            Data = null
        };

        return Ok(response);
    }

    public class GetAAIIInfoRequest
    {
        [Required]
        public string CorrelationId { get; set; } = string.Empty;

        public string? DocumentType { get; set; }

        // Clave = nombre del campo extraido, valor = contenido extraido
        public Dictionary<string, string?> ExtractedData { get; set; } = new();
    }

    public class GetAAIIInfoResponse
    {
        public string CorrelationId { get; set; } = string.Empty;

        public bool Found { get; set; }

        public string Message { get; set; } = string.Empty;

        // Placeholder flexible para la salida final del enriquecimiento
        public object? Data { get; set; }
    }
}
