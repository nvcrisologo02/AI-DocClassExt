using System.Text.Json;
using System.Text;
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Entities;
using DocumentIA.Plugins.Integration;
using System.Net;
using System.Net.Http.Json;

namespace DocumentIA.Admin.Services;

public class TipologiaAdminService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly HttpClient _httpClient;

    public TipologiaAdminService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyCollection<TipologiaEntity>> GetTipologiasAsync()
    {
        return await GetRequiredAsync<List<TipologiaEntity>>("management/tipologias") ?? [];
    }

    public async Task<TipologiaEntity?> GetTipologiaAsync(int id)
    {
        return await GetOptionalAsync<TipologiaEntity>($"management/tipologias/{id}");
    }

    public async Task<TipologiaEntity> SaveTipologiaAsync(TipologiaEntity tipologia)
    {
        if (tipologia.Id == 0)
        {
            return await SendRequiredAsync<TipologiaEntity>(
                HttpMethod.Post,
                "management/tipologias",
                new TipologiaUpsertRequest
                {
                    Codigo = tipologia.Codigo,
                    Nombre = tipologia.Nombre,
                    Version = tipologia.Version,
                    ConfiguracionJson = tipologia.ConfiguracionJson ?? string.Empty,
                    Usuario = "COMPLETAR_GDC_HTTP_BASIC_USERNAME-ui"
                });
        }

        return await SendRequiredAsync<TipologiaEntity>(
            HttpMethod.Put,
            $"management/tipologias/{tipologia.Id}",
            new TipologiaUpsertRequest
            {
                Codigo = tipologia.Codigo,
                Nombre = tipologia.Nombre,
                Version = tipologia.Version,
                ConfiguracionJson = tipologia.ConfiguracionJson ?? string.Empty,
                Usuario = "COMPLETAR_GDC_HTTP_BASIC_USERNAME-ui"
            });
    }

    public async Task PublishTipologiaAsync(int id, string usuario)
    {
        await SendRequiredAsync<TipologiaEntity>(
            HttpMethod.Post,
            $"management/tipologias/{id}/publicar",
            new UsuarioRequest { Usuario = usuario });
    }

    public async Task RetireTipologiaAsync(int id)
    {
        await SendRequiredAsync<TipologiaEntity>(HttpMethod.Post, $"management/tipologias/{id}/retirar", new { });
    }

    public async Task PasarTipologiaADraftAsync(int id)
    {
        await SendRequiredAsync<TipologiaEntity>(HttpMethod.Post, $"management/tipologias/{id}/draft", new { });
    }

    public async Task<IReadOnlyCollection<ModeloConfigEntity>> GetModelosByTipoAsync(TipoModelo tipo)
    {
        try
        {
            var list = await GetRequiredAsync<List<ModeloConfigEntity>>($"management/modelos/{ToTipoSegment(tipo)}");
            return (IReadOnlyCollection<ModeloConfigEntity>)(list ?? new List<ModeloConfigEntity>());
        }
        catch (InvalidOperationException ex)
        {
            // Backend may reject unknown tipo values (e.g. layout) with a clear message.
            // In that case, return an empty collection so the UI remains functional.
            if (ex.Message?.Contains("Tipo de modelo invalido") == true || ex.Message?.Contains("Valores:") == true)
            {
                return Array.Empty<ModeloConfigEntity>();
            }

            throw;
        }
    }

    public async Task<ModeloConfigEntity?> GetModeloByIdAsync(int id)
    {
        var modelos = await GetRequiredAsync<List<ModeloConfigEntity>>($"management/modelos/clasificacion") ?? [];
        modelos.AddRange(await GetRequiredAsync<List<ModeloConfigEntity>>($"management/modelos/extraccion") ?? []);
        modelos.AddRange(await GetRequiredAsync<List<ModeloConfigEntity>>($"management/modelos/prompt") ?? []);
        return modelos.FirstOrDefault(m => m.Id == id);
    }

    public async Task<ModeloConfigEntity> SaveModeloAsync(ModeloConfigEntity modelo)
    {
        if (modelo.Id == 0)
        {
            return await SendRequiredAsync<ModeloConfigEntity>(
                HttpMethod.Post,
                "management/modelos",
                new ModeloUpsertRequest
                {
                    Tipo = ToTipoSegment(modelo.Tipo),
                    Key = modelo.Key,
                    Provider = modelo.Provider,
                    ConfiguracionJson = modelo.ConfiguracionJson,
                    Activo = modelo.Activo,
                    Usuario = "COMPLETAR_GDC_HTTP_BASIC_USERNAME-ui"
                });
        }

        return await SendRequiredAsync<ModeloConfigEntity>(
            HttpMethod.Put,
            $"management/modelos/{modelo.Id}",
            new ModeloUpsertRequest
            {
                Tipo = ToTipoSegment(modelo.Tipo),
                Key = modelo.Key,
                Provider = modelo.Provider,
                ConfiguracionJson = modelo.ConfiguracionJson,
                Activo = modelo.Activo,
                Usuario = "COMPLETAR_GDC_HTTP_BASIC_USERNAME-ui"
            });
    }

    public async Task DeleteModeloAsync(int id)
    {
        await SendRequiredAsync<ModeloConfigEntity>(HttpMethod.Delete, $"management/modelos/{id}", null);
    }

    public async Task<IReadOnlyCollection<PluginTipologiaConfigEntity>> GetPluginConfigsAsync()
    {
        return await GetRequiredAsync<List<PluginTipologiaConfigEntity>>("management/plugins-tipologias") ?? [];
    }

    public async Task<PluginTipologiaConfigEntity?> GetPluginConfigAsync(string tipologiaCodigo)
    {
        return await GetOptionalAsync<PluginTipologiaConfigEntity>($"management/plugins-tipologias/{Uri.EscapeDataString(tipologiaCodigo)}");
    }

    public async Task<PluginTipologiaConfigEntity> SavePluginDraftAsync(string tipologiaCodigo, string configuracionJson)
    {
        return await SendRequiredAsync<PluginTipologiaConfigEntity>(
            HttpMethod.Put,
            $"management/plugins-tipologias/{Uri.EscapeDataString(tipologiaCodigo)}",
            new PluginConfigUpsertRequest
            {
                ConfiguracionJson = configuracionJson,
                Usuario = "COMPLETAR_GDC_HTTP_BASIC_USERNAME-ui"
            });
    }

    public async Task PublishPluginConfigAsync(string tipologiaCodigo, string usuario)
    {
        await SendRequiredAsync<PluginTipologiaConfigEntity>(
            HttpMethod.Post,
            $"management/plugins-tipologias/{Uri.EscapeDataString(tipologiaCodigo)}/publicar",
            new UsuarioRequest { Usuario = usuario });
    }

    public async Task RetirePluginConfigAsync(string tipologiaCodigo)
    {
        await SendRequiredAsync<PluginTipologiaConfigEntity>(
            HttpMethod.Post,
            $"management/plugins-tipologias/{Uri.EscapeDataString(tipologiaCodigo)}/retirar",
            new { });
    }

    private async Task<T?> GetOptionalAsync<T>(string relativeUrl)
    {
        using var response = await _httpClient.GetAsync(relativeUrl);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private async Task<T?> GetRequiredAsync<T>(string relativeUrl)
    {
        using var response = await _httpClient.GetAsync(relativeUrl);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private async Task<T> SendRequiredAsync<T>(HttpMethod method, string relativeUrl, object? body)
    {
        using var request = new HttpRequestMessage(method, relativeUrl);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return result ?? throw new InvalidOperationException("La API devolvió una respuesta vacía.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var message = TryReadError(body) ?? $"La API devolvió {(int)response.StatusCode} {response.ReasonPhrase}.";
        throw new InvalidOperationException(message);
    }

    private static string? TryReadError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var errorElement)
                && errorElement.ValueKind == JsonValueKind.String)
            {
                return errorElement.GetString();
            }
        }
        catch
        {
            // ignore parse errors and return raw body below
        }

        return body;
    }

    private static string ToTipoSegment(TipoModelo tipo) => tipo switch
    {
        TipoModelo.Clasificacion => "clasificacion",
        TipoModelo.Extraccion => "extraccion",
        TipoModelo.Prompt => "prompt",
        TipoModelo.Layout => "layout",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null)
    };

    public static IReadOnlyCollection<string> ValidarConfiguracionJson(string json)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("ConfiguracionJson no puede estar vacío.");
            return errors;
        }

        try
        {
            var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config is null)
            {
                errors.Add("No se pudo deserializar la configuración.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(config.TipologiaId))
            {
                errors.Add("tipologiaId es obligatorio.");
            }

            if (string.IsNullOrWhiteSpace(config.Version))
            {
                errors.Add("version es obligatorio.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"JSON inválido: {ex.Message}");
        }

        return errors;
    }

    public static IReadOnlyCollection<string> ValidarPluginConfigJson(string json)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("ConfiguracionJson no puede estar vacío.");
            return errors;
        }

        try
        {
            var config = JsonSerializer.Deserialize<PluginConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config is null)
            {
                errors.Add("No se pudo deserializar la configuración de plugins.");
                return errors;
            }

            if (config.Plugins is null)
            {
                errors.Add("plugins es obligatorio.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"JSON inválido: {ex.Message}");
        }

        return errors;
    }

    public static IReadOnlyCollection<string> ValidarModeloConfigJson(string json)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("ConfiguracionJson no puede estar vacío.");
            return errors;
        }

        try
        {
            JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            errors.Add($"JSON inválido: {ex.Message}");
        }

        return errors;
    }

    private sealed class TipologiaUpsertRequest
    {
        public string? Codigo { get; set; }
        public string? Nombre { get; set; }
        public string? Version { get; set; }
        public string ConfiguracionJson { get; set; } = string.Empty;
        public string? Usuario { get; set; }
    }

    private sealed class ModeloUpsertRequest
    {
        public string Tipo { get; set; } = string.Empty;
        public string? Key { get; set; }
        public string? Provider { get; set; }
        public string ConfiguracionJson { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        public string? Usuario { get; set; }
    }

    private sealed class PluginConfigUpsertRequest
    {
        public string ConfiguracionJson { get; set; } = string.Empty;
        public string? Usuario { get; set; }
    }

    private sealed class UsuarioRequest
    {
        public string? Usuario { get; set; }
    }
}
