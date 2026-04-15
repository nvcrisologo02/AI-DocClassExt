using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocumentIA.Admin.Services;

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
}
