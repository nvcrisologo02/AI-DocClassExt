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
    public string? ModeloClasificacion { get; set; }
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

    public async Task<List<EjecucionResumenDto>> GetUltimasEjecucionesAsync(int top = 50)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<EjecucionResumenDto>>(
                $"management/ejecuciones?top={top}", JsonOptions);
            return result ?? [];
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Error al obtener ejecuciones del backend: {ex.Message}", ex);
        }
    }

    public async Task<EjecucionDetalleDto?> GetEjecucionDetalleAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<EjecucionDetalleDto>(
                $"management/ejecuciones/{id}/detalle", JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Error al obtener detalle de ejecución: {ex.Message}", ex);
        }
    }

    public async Task<DashboardAgregadosDto?> GetAgregadosAsync(int dias = 30)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DashboardAgregadosDto>(
                $"management/ejecuciones/agregados?dias={dias}", JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
