using System.Net.Http.Json;
using System.Text.Json;
using DocumentIA.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

public class ObtenerActivoActivity
{
    private readonly ILogger<ObtenerActivoActivity> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ObtenerActivoActivity(
        ILogger<ObtenerActivoActivity> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [Function(nameof(ObtenerActivoActivity))]
    public async Task<ResultadoAssetResolver> Run([ActivityTrigger] ObtenerActivoInput input)
    {
        _logger.LogInformation(
            "ObtenerActivoActivity iniciada. CorrelationId={CorrelationId}, Tipologia={Tipologia}",
            input.CorrelationId, input.Tipologia);

        var resultado = new ResultadoAssetResolver { Ejecutado = true };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var client = _httpClientFactory.CreateClient("AssetResolver");

            // Construir payload para el plugin
            var payload = new
            {
                CorrelationId = input.CorrelationId,
                DocumentType = input.Tipologia,
                ExtractedData = input.DatosExtraidos?.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value?.ToString()) ?? new Dictionary<string, string?>(),
                RequestedFields = input.CamposSolicitados,
                IdufirOverride = input.IdufirOverride,
                ReferenciaCatastralOverride = input.ReferenciaCatastralOverride,
                ModoCombinacionCriterios = input.ModoCombinacionCriterios,
                MapeoIdufir = input.MapeoIdufir,
                MapeoReferenciaCatastral = input.MapeoReferenciaCatastral,
                BusquedaIdufirHabilitada = input.BusquedaIdufirHabilitada,
                BusquedaReferenciaCatastralHabilitada = input.BusquedaReferenciaCatastralHabilitada,
                BusquedaDireccionHabilitada = input.BusquedaDireccionHabilitada,
                BusquedaDireccionTipificadaHabilitada = input.BusquedaDireccionTipificadaHabilitada,
                DireccionTipificada = input.DireccionTipificada,
                MapeoDireccionCompleta = input.MapeoDireccionCompleta,
                MapeoDireccionNombreVia = input.MapeoDireccionNombreVia,
                MapeoDireccionNumero = input.MapeoDireccionNumero,
                MapeoDireccionMunicipio = input.MapeoDireccionMunicipio,
                MapeoDireccionCodigoPostal = input.MapeoDireccionCodigoPostal,
                UmbralScoreDireccion = input.UmbralScoreDireccion
            };

            var response = await client.PostAsJsonAsync("api/assets/GetAAIIInfo", payload);

            if (!response.IsSuccessStatusCode)
            {
                sw.Stop();
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "AssetResolver respondió {StatusCode}. Body={Body}",
                    response.StatusCode, body);

                resultado.Exitoso = false;
                resultado.Mensaje = $"HTTP {(int)response.StatusCode}: {body}";
                resultado.DuracionMs = (int)sw.ElapsedMilliseconds;
                return resultado;
            }

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var pluginResp = await response.Content.ReadFromJsonAsync<AssetResolverPluginResponse>(jsonOptions);

            sw.Stop();

            if (pluginResp == null)
            {
                resultado.Exitoso = false;
                resultado.Mensaje = "Respuesta vacía del plugin AssetResolver.";
                resultado.DuracionMs = (int)sw.ElapsedMilliseconds;
                return resultado;
            }

            resultado.Exitoso = pluginResp.Found;
            resultado.Count = pluginResp.Count;
            resultado.CriteriosUsados = pluginResp.CriteriosUsados != null
                ? new CriteriosBusquedaActivo
                {
                    Idufir = pluginResp.CriteriosUsados.Idufir,
                    ReferenciaCatastral = pluginResp.CriteriosUsados.ReferenciaCatastral,
                    ModoCombinacionCriterios = pluginResp.CriteriosUsados.ModoCombinacionCriterios ?? "OR",
                    Direccion = pluginResp.CriteriosUsados.Direccion != null
                        ? new DireccionCriterioActivo
                        {
                            DireccionCompleta    = pluginResp.CriteriosUsados.Direccion.DireccionCompleta,
                            NombreVia           = pluginResp.CriteriosUsados.Direccion.NombreVia,
                            Numero              = pluginResp.CriteriosUsados.Direccion.Numero,
                            Municipio           = pluginResp.CriteriosUsados.Direccion.Municipio,
                            CodigoPostal        = pluginResp.CriteriosUsados.Direccion.CodigoPostal,
                            DireccionNormalizada = pluginResp.CriteriosUsados.Direccion.DireccionNormalizada,
                            Score               = pluginResp.CriteriosUsados.Direccion.Score,
                            CandidatosEvaluados = pluginResp.CriteriosUsados.Direccion.CandidatosEvaluados,
                            Razon               = pluginResp.CriteriosUsados.Direccion.Razon
                        }
                        : null,
                    DireccionTipificada = pluginResp.CriteriosUsados.DireccionTipificada != null
                        ? new DireccionTipificadaCriterioActivo
                        {
                            Pais = pluginResp.CriteriosUsados.DireccionTipificada.Pais,
                            Provincia = pluginResp.CriteriosUsados.DireccionTipificada.Provincia,
                            ComunidadAutonoma = pluginResp.CriteriosUsados.DireccionTipificada.ComunidadAutonoma,
                            Municipio = pluginResp.CriteriosUsados.DireccionTipificada.Municipio,
                            Poblacion = pluginResp.CriteriosUsados.DireccionTipificada.Poblacion,
                            TipoVia = pluginResp.CriteriosUsados.DireccionTipificada.TipoVia,
                            Calle = pluginResp.CriteriosUsados.DireccionTipificada.Calle,
                            Numero = pluginResp.CriteriosUsados.DireccionTipificada.Numero,
                            Bloque = pluginResp.CriteriosUsados.DireccionTipificada.Bloque,
                            Puerta = pluginResp.CriteriosUsados.DireccionTipificada.Puerta,
                            CodigoPostal = pluginResp.CriteriosUsados.DireccionTipificada.CodigoPostal,
                            Planta = pluginResp.CriteriosUsados.DireccionTipificada.Planta,
                            CandidatosEvaluados = pluginResp.CriteriosUsados.DireccionTipificada.CandidatosEvaluados,
                            Razon = pluginResp.CriteriosUsados.DireccionTipificada.Razon
                        }
                        : null
                }
                : new CriteriosBusquedaActivo();
            resultado.Activos = pluginResp.Activos?.Select(a => new ActivoEncontrado
            {
                IdActivo = a.IdActivo,
                FchCierre = a.FchCierre,
                CamposSolicitados = a.CamposSolicitados ?? new Dictionary<string, object?>()
            }).ToList() ?? [];
            resultado.CamposConError = pluginResp.CamposConError ?? [];
            resultado.Mensaje = pluginResp.Message ?? string.Empty;
            resultado.DuracionMs = pluginResp.DuracionMs > 0 ? pluginResp.DuracionMs : (int)sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "ObtenerActivoActivity completada. Found={Found}, Count={Count}, DuracionMs={DuracionMs}",
                resultado.Exitoso, resultado.Count, resultado.DuracionMs);

            return resultado;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error en ObtenerActivoActivity. CorrelationId={CorrelationId}", input.CorrelationId);
            resultado.Exitoso = false;
            resultado.Error = ex.Message;
            resultado.Mensaje = "Error al comunicarse con el plugin AssetResolver.";
            resultado.DuracionMs = (int)sw.ElapsedMilliseconds;
            return resultado;
        }
    }

    // ── DTOs internos para deserializar la respuesta del plugin ──

    private class AssetResolverPluginResponse
    {
        public string CorrelationId { get; set; } = string.Empty;
        public bool Found { get; set; }
        public int Count { get; set; }
        public PluginCriteriosUsados? CriteriosUsados { get; set; }
        public List<PluginActivoEncontrado>? Activos { get; set; }
        public List<string>? CamposConError { get; set; }
        public string? Message { get; set; }
        public int DuracionMs { get; set; }
        public string? Error { get; set; }
    }

    private class PluginCriteriosUsados
    {
        public string? Idufir { get; set; }
        public string? ReferenciaCatastral { get; set; }
        public string? ModoCombinacionCriterios { get; set; }
        public PluginDireccionCriterio? Direccion { get; set; }
        public PluginDireccionTipificadaCriterio? DireccionTipificada { get; set; }
    }

    private class PluginDireccionCriterio
    {
        public string? DireccionCompleta { get; set; }
        public string? NombreVia { get; set; }
        public string? Numero { get; set; }
        public string? Municipio { get; set; }
        public string? CodigoPostal { get; set; }
        public string? DireccionNormalizada { get; set; }
        public double Score { get; set; }
        public int CandidatosEvaluados { get; set; }
        public string? Razon { get; set; }
    }

    private class PluginDireccionTipificadaCriterio
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
        public int CandidatosEvaluados { get; set; }
        public string? Razon { get; set; }
    }

    private class PluginActivoEncontrado
    {
        public string IdActivo { get; set; } = string.Empty;
        public DateTime? FchCierre { get; set; }
        public Dictionary<string, object?>? CamposSolicitados { get; set; }
    }
}
