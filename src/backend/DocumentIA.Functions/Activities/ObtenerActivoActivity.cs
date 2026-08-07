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

            // Expansión multi-activo: si la tipología declara un campo colección y éste
            // es un array de objetos, cada elemento se convierte en un grupo de criterios.
            var grupos = ExpandirColeccionActivos(input.DatosExtraidos, input.MapeoColeccionActivos, out var campoColeccion);

            // Construir payload para el plugin (el campo colección no viaja aplanado)
            var payload = new
            {
                CorrelationId = input.CorrelationId,
                DocumentType = input.Tipologia,
                ExtractedData = input.DatosExtraidos?
                    .Where(kv => campoColeccion is null || !string.Equals(kv.Key, campoColeccion, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(kv => kv.Key, kv => FlattenToString(kv.Value))
                    ?? new Dictionary<string, string?>(),
                Grupos = grupos,
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
            resultado.CriteriosUsados = MapCriteriosUsados(pluginResp.CriteriosUsados) ?? new CriteriosBusquedaActivo();
            resultado.Activos = pluginResp.Activos?.Select(a => new ActivoEncontrado
            {
                IdActivo = a.IdActivo,
                FchCierre = a.FchCierre,
                CamposSolicitados = a.CamposSolicitados ?? new Dictionary<string, object?>()
            }).ToList() ?? [];
            resultado.ActivosPorGrupo = pluginResp.ActivosPorGrupo?.Select(g => new GrupoActivosEncontrados
            {
                Indice = g.Indice,
                CriteriosEntrada = g.CriteriosEntrada ?? new Dictionary<string, string?>(),
                CriteriosUsados = MapCriteriosUsados(g.CriteriosUsados),
                Activos = g.Activos?.Select(a => new ActivoEncontrado
                {
                    IdActivo = a.IdActivo,
                    FchCierre = a.FchCierre,
                    CamposSolicitados = a.CamposSolicitados ?? new Dictionary<string, object?>()
                }).ToList() ?? [],
                Count = g.Count,
                CriterioUtilizado = g.CriterioUtilizado,
                Mensaje = g.Mensaje
            }).ToList();
            resultado.CamposConError = pluginResp.CamposConError ?? [];
            resultado.Mensaje = pluginResp.Message ?? string.Empty;
            resultado.Error = pluginResp.Error ?? resultado.Error;
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

    /// <summary>
    /// Busca el primer campo de MapeoColeccionActivos presente en DatosExtraidos cuyo
    /// valor sea un array JSON de objetos no vacío y lo expande a grupos de criterios.
    /// Devuelve null (sin grupos) si no hay colección aplicable.
    /// </summary>
    private List<Dictionary<string, string?>>? ExpandirColeccionActivos(
        Dictionary<string, object>? datosExtraidos,
        List<string> mapeoColeccionActivos,
        out string? campoColeccion)
    {
        campoColeccion = null;
        if (datosExtraidos is null || mapeoColeccionActivos is not { Count: > 0 })
            return null;

        foreach (var nombre in mapeoColeccionActivos)
        {
            var match = datosExtraidos.FirstOrDefault(
                kv => string.Equals(kv.Key, nombre, StringComparison.OrdinalIgnoreCase));
            if (match.Key is null)
                continue;

            var grupos = ExtraerGrupos(match.Value);
            if (grupos is { Count: > 0 })
            {
                campoColeccion = match.Key;
                _logger.LogInformation(
                    "Colección de activos '{Campo}' expandida a {Grupos} grupos de criterios.",
                    match.Key, grupos.Count);
                return grupos;
            }
        }

        return null;
    }

    private List<Dictionary<string, string?>>? ExtraerGrupos(object? valor)
    {
        if (valor is not System.Text.Json.JsonElement je || je.ValueKind != System.Text.Json.JsonValueKind.Array)
            return null;

        var grupos = new List<Dictionary<string, string?>>();
        foreach (var elemento in je.EnumerateArray())
        {
            if (elemento.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                _logger.LogWarning(
                    "Elemento no-objeto ({Kind}) ignorado en colección de activos.",
                    elemento.ValueKind);
                continue;
            }

            var grupo = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in elemento.EnumerateObject())
            {
                grupo[prop.Name] = FlattenToString(prop.Value);
            }

            grupos.Add(grupo);
        }

        return grupos.Count > 0 ? grupos : null;
    }

    private static CriteriosBusquedaActivo? MapCriteriosUsados(PluginCriteriosUsados? criterios)
    {
        if (criterios is null) return null;

        return new CriteriosBusquedaActivo
        {
            Idufir = criterios.Idufir,
            ReferenciaCatastral = criterios.ReferenciaCatastral,
            ModoCombinacionCriterios = criterios.ModoCombinacionCriterios ?? "OR",
            Direccion = criterios.Direccion != null
                ? new DireccionCriterioActivo
                {
                    DireccionCompleta = criterios.Direccion.DireccionCompleta,
                    NombreVia = criterios.Direccion.NombreVia,
                    Numero = criterios.Direccion.Numero,
                    Municipio = criterios.Direccion.Municipio,
                    CodigoPostal = criterios.Direccion.CodigoPostal,
                    DireccionNormalizada = criterios.Direccion.DireccionNormalizada,
                    Score = criterios.Direccion.Score,
                    CandidatosEvaluados = criterios.Direccion.CandidatosEvaluados,
                    Razon = criterios.Direccion.Razon
                }
                : null,
            DireccionTipificada = criterios.DireccionTipificada != null
                ? new DireccionTipificadaCriterioActivo
                {
                    Pais = criterios.DireccionTipificada.Pais,
                    Provincia = criterios.DireccionTipificada.Provincia,
                    ComunidadAutonoma = criterios.DireccionTipificada.ComunidadAutonoma,
                    Municipio = criterios.DireccionTipificada.Municipio,
                    Poblacion = criterios.DireccionTipificada.Poblacion,
                    TipoVia = criterios.DireccionTipificada.TipoVia,
                    Calle = criterios.DireccionTipificada.Calle,
                    Numero = criterios.DireccionTipificada.Numero,
                    Bloque = criterios.DireccionTipificada.Bloque,
                    Puerta = criterios.DireccionTipificada.Puerta,
                    CodigoPostal = criterios.DireccionTipificada.CodigoPostal,
                    Planta = criterios.DireccionTipificada.Planta,
                    CandidatosEvaluados = criterios.DireccionTipificada.CandidatosEvaluados,
                    Razon = criterios.DireccionTipificada.Razon
                }
                : null
        };
    }

    // ── DTOs internos para deserializar la respuesta del plugin ──

    /// <summary>
    /// Convierte un valor de DatosExtraidos a string plano para el payload del AssetResolver.
    /// Los arrays JSON (JsonElement) se aplanan al primer elemento, evitando ruido de serialización.
    /// </summary>
    private static string? FlattenToString(object? value)
    {
        if (value is null) return null;

        if (value is System.Text.Json.JsonElement je)
        {
            return je.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Array =>
                    je.GetArrayLength() > 0
                        ? (je[0].ValueKind == System.Text.Json.JsonValueKind.String
                            ? je[0].GetString()
                            : je[0].ToString())
                        : null,
                System.Text.Json.JsonValueKind.String =>
                    je.GetString(),
                System.Text.Json.JsonValueKind.Null =>
                    null,
                _ => je.ToString()
            };
        }

        // Si ya es lista en memoria (tras deserialización no-JsonElement)
        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
                return item?.ToString();
            return null;
        }

        return value.ToString();
    }

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
        public List<PluginGrupoResultado>? ActivosPorGrupo { get; set; }
    }

    private class PluginGrupoResultado
    {
        public int Indice { get; set; }
        public Dictionary<string, string?>? CriteriosEntrada { get; set; }
        public PluginCriteriosUsados? CriteriosUsados { get; set; }
        public List<PluginActivoEncontrado>? Activos { get; set; }
        public int Count { get; set; }
        public string? CriterioUtilizado { get; set; }
        public string? Mensaje { get; set; }
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
