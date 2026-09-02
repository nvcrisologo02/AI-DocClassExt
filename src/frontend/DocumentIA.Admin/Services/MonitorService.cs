using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocumentIA.Admin.Services;

// ─── DTOs de resumen (lista) ─────────────────────────────────────────────────

public class EjecucionResumenDto
{
    public int Id { get; set; }
    public string EjecucionGuid { get; set; } = string.Empty;
    public DateTime FechaEjecucion { get; set; }
    public string? Tipologia { get; set; }
    public bool ClassificationOnly { get; set; }
    public string TipoFlujo { get; set; } = string.Empty;
    public string EstadoFinal { get; set; } = string.Empty;
    public double ConfianzaGlobal { get; set; }
    public double ConfianzaClasificacion { get; set; }
    public bool UseFallbackLLM { get; set; }
    public int DuracionTotalMs { get; set; }
    public int? DuracionClasificacionMs { get; set; }
    public int? DuracionExtraccionMs { get; set; }
    public int? DuracionGDCMs { get; set; }
    public int? DuracionValidacionMs { get; set; }
    public int? DuracionIntegracionMs { get; set; }
    public int? DuracionPersistenciaMs { get; set; }
    public string? NombreDocumento { get; set; }
    public string? SubmittedBy { get; set; }
    public string? SourceSystem { get; set; }
    public List<ActividadResumenDto> Actividades { get; set; } = [];
}

public class ActividadResumenDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

// ─── DTOs de detalle (expansión) ─────────────────────────────────────────────

public class ActividadDetalleDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public int DuracionMs { get; set; }
    public string? Mensaje { get; set; }
    public bool FallbackActivado { get; set; }
    public string? FallbackRazon { get; set; }
    public DateTime? InicioUtc { get; set; }
    public DateTime? FinUtc { get; set; }
}

public class CampoExtraidoDto
{
    public string Campo { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}

public class ValidacionItemDto
{
    public string Campo { get; set; } = string.Empty;
    public string Severidad { get; set; } = string.Empty;
    public string? Mensaje { get; set; }
    public string? ValorOriginal { get; set; }
    public string? ValorEsperado { get; set; }
    public bool Pasado { get; set; }
}

public class PluginItemDto
{
    public string PluginKey { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool Success { get; set; }
    public string? Mensaje { get; set; }
    public int StatusCode { get; set; }
    public int DurationMs { get; set; }
    public string? Error { get; set; }
}

public class ClasificacionDetalleDto
{
    public string Modelo { get; set; } = string.Empty;
    public string ProveedorClasif { get; set; } = string.Empty;
    public double Confianza { get; set; }
    public double ConfianzaDI { get; set; }
    public double ConfianzaGPT { get; set; }
    public bool FallbackLLM { get; set; }
    public string? FallbackRazon { get; set; }
    public string? TipologiaDetectada { get; set; }
    public double? UmbralFallbackAplicado { get; set; }
}

public class ExtraccionDetalleDto
{
    public string Modelo { get; set; } = string.Empty;
    public string ProveedorExtrac { get; set; } = string.Empty;
    public double ConfianzaExtraccion { get; set; }
    public bool FallbackUsado { get; set; }
    public string? FallbackRazon { get; set; }
    public List<string> CamposConDuda { get; set; } = [];
    public Dictionary<string, double> ConfianzaPorCampo { get; set; } = new();
}

public class GDCDetalleDto
{
    public bool Exitoso { get; set; }
    public string ObjectId { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string ErrorDetalle { get; set; } = string.Empty;
    public bool YaExistia { get; set; }
    public int Intentos { get; set; }
    public int DuracionMs { get; set; }
}

public class ResultadoDetalleDto
{
    public string Estado { get; set; } = string.Empty;
    public string EstadoCalidad { get; set; } = string.Empty;
    public double ConfianzaGlobal { get; set; }
    public double ConfianzaClasificacion { get; set; }
    public double ConfianzaExtraccion { get; set; }
    public double ConfianzaValidacion { get; set; }
    public string? MensajeError { get; set; }
    public bool ReutilizadaPorDuplicado { get; set; }
    public string? MensajeReutilizacion { get; set; }
}

public class IntegridadDetalleDto
{
    public string SHA256 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public string CRC32 { get; set; } = string.Empty;
    public string? RutaBlobStorage { get; set; }
    public string? IdActivo { get; set; }
    public string? IdActivoEntrada { get; set; }
    public bool IdActivoCambiado { get; set; }
    public string? GestorDocumental { get; set; }
}

public class IdentificacionDetalleDto
{
    public string Documento { get; set; } = string.Empty;
    public string Tipologia { get; set; } = string.Empty;
    public string TipologiaFamilia { get; set; } = string.Empty;
    public string TipologiaVersion { get; set; } = string.Empty;
    public int Paginas { get; set; }
    public DateTime FechaProceso { get; set; }
}

public class EjecucionDetalleDto
{
    public int Id { get; set; }
    public string EjecucionGuid { get; set; } = string.Empty;
    public string? SourceSystem { get; set; }
    public string? ModeloClasificacion { get; set; }
    public bool ClassificationOnly { get; set; }
    public string? TipologiaNombreCatalogo { get; set; }
    public string? TipologiaFamiliaNombreCatalogo { get; set; }
    // Contrato crudo tal cual almacenado; reservado para el visor de la siguiente entrega, no se usa aun.
    public string? ContratoSalidaCompletoJson { get; set; }
    public IdentificacionDetalleDto? Identificacion { get; set; }
    public IntegridadDetalleDto? Integridad { get; set; }
    public ResultadoDetalleDto? Resultado { get; set; }
    public ClasificacionDetalleDto? Clasificacion { get; set; }
    public ExtraccionDetalleDto? Extraccion { get; set; }
    public GDCDetalleDto? GDC { get; set; }
    public List<ActividadDetalleDto> Timeline { get; set; } = [];
    public List<CampoExtraidoDto> DatosExtraidos { get; set; } = [];
    public List<ValidacionItemDto> Validaciones { get; set; } = [];
    public List<PluginItemDto> Plugins { get; set; } = [];
}

// ─── DTOs de agregados (cuadro de mando) ─────────────────────────────────────

public class AgregadoGrupoDto
{
    public string Grupo { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Ok { get; set; }
    public int Revision { get; set; }
    public int Error { get; set; }
    public int Fallbacks { get; set; }
    public double ConfianzaMedia { get; set; }
    public double DuracionMediaMs { get; set; }
}

public class MatrizCeldaDto
{
    public string EstadoProceso { get; set; } = string.Empty;
    public string Calidad { get; set; } = string.Empty;
    public int Total { get; set; }
}

public class HistogramaBinDto
{
    public double Desde { get; set; }
    public double Hasta { get; set; }
    public int Total { get; set; }
}

public class DashboardAgregadosDto
{
    public int TotalEjecuciones { get; set; }
    public int PeriodoDias { get; set; }
    public int Ok { get; set; }
    public int Revision { get; set; }
    public int Error { get; set; }
    public int FallbacksTotal { get; set; }
    public double ConfianzaGlobalMedia { get; set; }
    public double DuracionMediaMs { get; set; }
    public List<AgregadoGrupoDto> PorTipologia { get; set; } = [];
    public List<AgregadoGrupoDto> PorModelo { get; set; } = [];
    public List<SeriePuntoDto> Serie { get; set; } = [];

    // Calidad por confianza (Monitor v2). Ok/Revision/Error de arriba cuentan por
    // estado de proceso; estos, por la confianza del resultado.
    public int CalidadOk { get; set; }
    public int CalidadRevision { get; set; }
    public int CalidadError { get; set; }
    public List<AgregadoGrupoDto> PorEstadoProceso { get; set; } = [];
    public List<MatrizCeldaDto> Matriz { get; set; } = [];
    public List<HistogramaBinDto> Histograma { get; set; } = [];
}

public class SeriePuntoDto
{
    public DateTime Fecha { get; set; }
    public int Total { get; set; }
    public int Ok { get; set; }
    public int Revision { get; set; }
    public int Error { get; set; }
    public int Fallbacks { get; set; }
}

public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class MonitorFiltroDto
{
    // Ventana temporal expresada en dias hacia atras, no como fechas absolutas: se
    // materializa contra el reloj en cada consulta (ver ToQueryString). Guardando
    // Desde/Hasta absolutos, calculados una sola vez al construir el filtro, el
    // auto-refresco de la pagina repetia siempre la misma consulta y ninguna
    // ejecucion posterior a la carga de la pagina entraba nunca en la ventana.
    public int RangoDias { get; set; } = 7;

    // Ventana lo bastante ancha para servir de catalogo historico. No es
    // "sin limite" porque el backend genera un punto de serie por dia del rango:
    // un extremo en el año 2000 producia miles de puntos inutiles por consulta.
    public const int RangoDiasHistorico = 3650;

    public string? Tipologia { get; set; }
    public string? Estado { get; set; }
    public string? Flujo { get; set; }
    public string? Busqueda { get; set; }
    public string? SubmittedBy { get; set; }
    public string? SourceSystem { get; set; }

    // Recortes de Monitor v2: estado de proceso exacto, calidad por confianza y
    // tramo del histograma. Van aparte de Estado (las tres categorias historicas)
    // porque aquel deja fuera estados como VALIDACION_CON_ERRORES.
    public string? EstadoProceso { get; set; }
    public string? Calidad { get; set; }
    public double? ConfianzaMin { get; set; }
    public double? ConfianzaMax { get; set; }

    public string ToQueryString()
    {
        var hasta = DateTime.UtcNow;
        var desde = hasta.AddDays(-RangoDias);
        var partes = new List<string>
        {
            $"desde={Uri.EscapeDataString(desde.ToString("o"))}",
            $"hasta={Uri.EscapeDataString(hasta.ToString("o"))}"
        };
        if (!string.IsNullOrWhiteSpace(Tipologia)) partes.Add($"tipologia={Uri.EscapeDataString(Tipologia)}");
        if (!string.IsNullOrWhiteSpace(Estado)) partes.Add($"estado={Uri.EscapeDataString(Estado)}");
        if (!string.IsNullOrWhiteSpace(Flujo)) partes.Add($"flujo={Uri.EscapeDataString(Flujo)}");
        if (!string.IsNullOrWhiteSpace(Busqueda)) partes.Add($"q={Uri.EscapeDataString(Busqueda)}");
        if (!string.IsNullOrWhiteSpace(SubmittedBy)) partes.Add($"submittedby={Uri.EscapeDataString(SubmittedBy)}");
        if (!string.IsNullOrWhiteSpace(SourceSystem)) partes.Add($"sourcesystem={Uri.EscapeDataString(SourceSystem)}");
        if (!string.IsNullOrWhiteSpace(EstadoProceso)) partes.Add($"estadoproceso={Uri.EscapeDataString(EstadoProceso)}");
        if (!string.IsNullOrWhiteSpace(Calidad)) partes.Add($"calidad={Uri.EscapeDataString(Calidad)}");
        // Punto decimal explicito: con la cultura espanola el separador seria una
        // coma y el backend leeria el numero mal.
        if (ConfianzaMin is { } min) partes.Add($"confmin={min.ToString(CultureInfo.InvariantCulture)}");
        if (ConfianzaMax is { } max) partes.Add($"confmax={max.ToString(CultureInfo.InvariantCulture)}");
        return string.Join("&", partes);
    }

    public MonitorFiltroDto Clonar() => (MonitorFiltroDto)MemberwiseClone();
}

public class HealthComponentDto
{
    public string Status { get; set; } = "unconfigured";
    public string? Message { get; set; }
}

public class ModelProvidersHealthDto
{
    public string Status { get; set; } = "unconfigured";
    public HealthComponentDto Classification { get; set; } = new();
    public HealthComponentDto Extraction { get; set; } = new();
    public HealthComponentDto Prompt { get; set; } = new();
}

public class HealthComponentsDto
{
    public HealthComponentDto Functions { get; set; } = new();
    public HealthComponentDto AssetResolver { get; set; } = new();
    public HealthComponentDto Gdc { get; set; } = new();
    public ModelProvidersHealthDto ModelProviders { get; set; } = new();
}

public class SystemHealthDto
{
    public bool Ok { get; set; }
    public string Status { get; set; } = "unconfigured";
    public DateTimeOffset Timestamp { get; set; }
    public HealthComponentsDto Components { get; set; } = new();
}

// ─── Utilidades de formato compartidas ───────────────────────────────────────

// Composicion "codigo — nombre" usada tanto por la fila desplegable del
// Monitor (EjecucionDetalle) como por el modal de JSON (EjecucionJsonModal):
// una sola implementacion para que las dos vistas no puedan divergir.
public static class MonitorFormato
{
    // Si el codigo esta retirado del catalogo el nombre llega nulo: se muestra
    // solo el codigo, sin inventar un nombre.
    public static string ConCodigoYNombre(string? codigo, string? nombre)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return "—";
        return string.IsNullOrWhiteSpace(nombre) ? codigo : $"{codigo} — {nombre}";
    }
}

// ─── Servicio ─────────────────────────────────────────────────────────────────

public class MonitorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public MonitorService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResultDto<EjecucionResumenDto>> GetEjecucionesAsync(
        MonitorFiltroDto filtro, int page = 1, int pageSize = 25)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<PagedResultDto<EjecucionResumenDto>>(
                $"management/ejecuciones?{filtro.ToQueryString()}&page={page}&pageSize={pageSize}", JsonOptions);
            return result ?? new PagedResultDto<EjecucionResumenDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Error al obtener ejecuciones del backend: {ex.Message}", ex);
        }
        // Una ventana de despliegue con el backend en una version distinta (Admin y
        // Functions se despliegan por separado) puede devolver una forma de JSON
        // que ya no coincide con el contrato esperado; sin este catch, JsonException
        // escapa de OnInitializedAsync y tumba el circuito de Blazor Server.
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Respuesta invalida al obtener ejecuciones del backend: {ex.Message}", ex);
        }
        // El timeout de HttpClient no llega como HttpRequestException sino como
        // TaskCanceledException (derivada de OperationCanceledException), igual que
        // la cancelacion por navegar fuera de la pagina. Sin traducirla escapa de
        // los catch de la pagina y termina el circuito de Blazor Server.
        catch (OperationCanceledException ex)
        {
            throw new InvalidOperationException($"El backend no respondio a tiempo al obtener ejecuciones: {ex.Message}", ex);
        }
    }

    public async Task<EjecucionDetalleDto?> GetEjecucionDetalleAsync(string guid)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<EjecucionDetalleDto>(
                $"management/ejecuciones/{Uri.EscapeDataString(guid)}/detalle", JsonOptions);
        }
        // 404 significa que la ejecucion no existe: no es un fallo de comunicacion, se distingue del resto.
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Error al obtener detalle de ejecución: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Respuesta invalida al obtener detalle de ejecución: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new InvalidOperationException($"El backend no respondio a tiempo al obtener el detalle: {ex.Message}", ex);
        }
    }

    // Los agregados alimentan el cuadro de mando, no la tabla principal: un fallo
    // aqui no debe impedir ver el listado, asi que se ignora en vez de propagarse
    // como InvalidOperationException. Se distinguen los dos tipos de fallo
    // esperados (comunicacion y deserializacion) en vez de un catch generico para
    // no enmascarar tambien errores de programacion.
    public async Task<DashboardAgregadosDto?> GetAgregadosAsync(MonitorFiltroDto filtro)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DashboardAgregadosDto>(
                $"management/ejecuciones/agregados?{filtro.ToQueryString()}", JsonOptions);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<SystemHealthDto?> GetSystemHealthAsync()
    {
        try
        {
            using var response = await _httpClient.PostAsync("healthcheck", content: null);
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 503)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<SystemHealthDto>(JsonOptions);
            return payload;
        }
        catch
        {
            return null;
        }
    }
}
