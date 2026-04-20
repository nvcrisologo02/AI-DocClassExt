using DocumentIA.AssetResolver.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DocumentIA.AssetResolver.Controllers;

[ApiController]
[Route("api/assets")]
public class AssetResolverController : ControllerBase
{
    private readonly AssetResolverService _service;
    private readonly ILogger<AssetResolverController> _logger;

    public AssetResolverController(AssetResolverService service, ILogger<AssetResolverController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("GetAAIIInfo")]
    [ProducesResponseType(typeof(GetAAIIInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GetAAIIInfoResponse>> GetAAIIInfo(
        [FromBody] GetAAIIInfoRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        _logger.LogInformation(
            "GetAAIIInfo recibido. CorrelationId={CorrelationId}, DocumentType={DocumentType}, Fields={Fields}, RequestedFields={RequestedFields}",
            request.CorrelationId,
            request.DocumentType,
            request.ExtractedData?.Count ?? 0,
            request.RequestedFields?.Count ?? 0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await _service.BuscarActivosAsync(request, ct);
            sw.Stop();
            response.DuracionMs = (int)sw.ElapsedMilliseconds;
            return Ok(response);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error en GetAAIIInfo. CorrelationId={CorrelationId}", request.CorrelationId);
            return Ok(new GetAAIIInfoResponse
            {
                CorrelationId = request.CorrelationId,
                Found = false,
                Message = "Error interno al consultar activos.",
                Error = ex.Message,
                DuracionMs = (int)sw.ElapsedMilliseconds
            });
        }
    }

    public class GetAAIIInfoRequest
    {
        [Required]
        public string CorrelationId { get; set; } = string.Empty;

        public string? DocumentType { get; set; }

        public Dictionary<string, string?> ExtractedData { get; set; } = new();

        /// <summary>
        /// Columnas de DM_POSICION_AAII_TB a devolver por nombre de columna real.
        /// Soporta la constante #ALL# para expandir a todas las columnas.
        /// Si se informa una lista explícita, ID_ACTIVO_SAREB y FCH_CIERRE se incluyen siempre.
        /// </summary>
        public List<string>? RequestedFields { get; set; }

        /// <summary>Override de IDUFIR (desde Instrucciones).</summary>
        public string? IdufirOverride { get; set; }

        /// <summary>Override de Referencia Catastral (desde Instrucciones).</summary>
        public string? ReferenciaCatastralOverride { get; set; }

        /// <summary>Aliases adicionales para IDUFIR (desde tipología).</summary>
        public List<string>? MapeoIdufir { get; set; }

        /// <summary>Aliases adicionales para ReferenciaCatastral (desde tipología).</summary>
        public List<string>? MapeoReferenciaCatastral { get; set; }

        /// <summary>
        /// Modo de combinación entre criterios resueltos de búsqueda.
        /// Valores admitidos: AND, OR. Default: OR.
        /// </summary>
        public string ModoCombinacionCriterios { get; set; } = "OR";

        // ── Búsqueda por dirección como criterio adicional ──

        /// <summary>Si true, se incluye IDUFIR como criterio de búsqueda. Default: true.</summary>
        public bool BusquedaIdufirHabilitada { get; set; } = true;
        /// <summary>Si true, se incluye ReferenciaCatastral como criterio de búsqueda. Default: true.</summary>
        public bool BusquedaReferenciaCatastralHabilitada { get; set; } = true;
        /// <summary>Si true, habilita la búsqueda por dirección como un criterio adicional.</summary>
        public bool BusquedaDireccionHabilitada { get; set; } = false;
        /// <summary>
        /// Si true, habilita la búsqueda por dirección tipificada (campos DES_*/NUM_*),
        /// aplicando filtros AND solo sobre los campos presentes.
        /// </summary>
        public bool BusquedaDireccionTipificadaHabilitada { get; set; } = false;
        /// <summary>
        /// Campos de búsqueda de dirección tipificada. No se resuelven desde ExtractedData;
        /// se usan directamente como filtros de búsqueda.
        /// </summary>
        public DireccionTipificadaInput? DireccionTipificada { get; set; }

        /// <summary>Aliases para detectar una dirección libre/completa en ExtractedData.</summary>
        public List<string>? MapeoDireccionCompleta { get; set; }

        /// <summary>Aliases para detectar nombre de vía en ExtractedData.</summary>
        public List<string>? MapeoDireccionNombreVia { get; set; }

        /// <summary>Aliases para detectar número de vía en ExtractedData.</summary>
        public List<string>? MapeoDireccionNumero { get; set; }

        /// <summary>Aliases para detectar municipio en ExtractedData.</summary>
        public List<string>? MapeoDireccionMunicipio { get; set; }

        /// <summary>Aliases para detectar código postal en ExtractedData.</summary>
        public List<string>? MapeoDireccionCodigoPostal { get; set; }

        /// <summary>Umbral mínimo de score para aceptar un match por dirección (0.0–1.0, default 0.75).</summary>
        public double UmbralScoreDireccion { get; set; } = 0.75;
    }

    public class DireccionTipificadaInput
    {
        public string? Pais { get; set; }
        public string? Provincia { get; set; }
        public string? ComunidadAutonoma { get; set; }
        public string? Municipio { get; set; }
        public string? Poblacion { get; set; }
        public string? TipoVia { get; set; }
        public string? Calle { get; set; }
        public string? Numero { get; set; }
        public string? Bloque { get; set; }
        public string? Puerta { get; set; }
        public string? CodigoPostal { get; set; }
        public string? Planta { get; set; }
    }

    public class GetAAIIInfoResponse
    {
        public string CorrelationId { get; set; } = string.Empty;
        public bool Found { get; set; }
        public int Count { get; set; }
        public AssetResolverService.CriteriosUsados? CriteriosUsados { get; set; }
        public List<AssetResolverService.ActivoEncontrado> Activos { get; set; } = [];
        public List<string> CamposConError { get; set; } = [];
        public string Message { get; set; } = string.Empty;
        public int DuracionMs { get; set; }
        public string? Error { get; set; }
        /// <summary>Criterio de búsqueda utilizado, incluyendo combinaciones AND/OR si aplica.</summary>
        public string? CriterioUtilizado { get; set; }
    }
}
