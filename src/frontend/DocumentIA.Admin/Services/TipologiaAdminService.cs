using System.Text.Json;
using System.Text;
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Entities;
using DocumentIA.Plugins.Integration;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace DocumentIA.Admin.Services;

public class TipologiaAdminService
{
    private static readonly Regex CodigoRegex = new("^[a-z0-9][a-z0-9_.-]{2,99}$", RegexOptions.Compiled);
    private static readonly Regex VersionRegex = new("^\\d+\\.\\d+(\\.\\d+)?(-[0-9A-Za-z-.]+)?(\\+[0-9A-Za-z-.]+)?$", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedFieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "decimal", "number", "integer", "int", "date", "datetime", "boolean", "bool", "array", "object"
    };

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

    public async Task<IReadOnlyCollection<string>> ValidateTipologiaAsync(TipologiaEntity tipologia, int? currentId = null)
    {
        var tipologias = await GetTipologiasAsync();
        return ValidarTipologia(tipologia, tipologias, currentId);
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
        await SendRequiredAsync<TipologiaEntity>(
            HttpMethod.Post,
            $"management/tipologias/{id}/retirar",
            new UsuarioRequest { Usuario = "COMPLETAR_GDC_HTTP_BASIC_USERNAME-ui" });
    }

    public async Task PasarTipologiaADraftAsync(int id)
    {
        await SendRequiredAsync<TipologiaEntity>(
            HttpMethod.Post,
            $"management/tipologias/{id}/draft",
            new UsuarioRequest { Usuario = "COMPLETAR_GDC_HTTP_BASIC_USERNAME-ui" });
    }

    public async Task<IReadOnlyCollection<TipologiaAuditEntry>> GetTipologiaAuditAsync(int id, int take = 200)
    {
        var safeTake = Math.Clamp(take, 1, 1000);
        return await GetRequiredAsync<List<TipologiaAuditEntry>>($"management/tipologias/{id}/audit?take={safeTake}") ?? [];
    }

    public async Task<IReadOnlyCollection<TipologiaVersionItem>> GetTipologiaVersionsAsync(int id)
    {
        return await GetRequiredAsync<List<TipologiaVersionItem>>($"management/tipologias/{id}/versions") ?? [];
    }

    public async Task<TipologiaDiffResult> GetTipologiaDiffAsync(int leftId, int rightId)
    {
        return await GetRequiredAsync<TipologiaDiffResult>($"management/tipologias/{leftId}/diff/{rightId}")
            ?? new TipologiaDiffResult();
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

    public static IReadOnlyCollection<string> ValidarTipologia(
        TipologiaEntity tipologia,
        IEnumerable<TipologiaEntity>? existingTipologias = null,
        int? currentId = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(tipologia.Codigo))
        {
            errors.Add("Codigo es obligatorio.");
        }
        else if (!CodigoRegex.IsMatch(tipologia.Codigo.Trim()))
        {
            errors.Add("Codigo debe tener entre 3 y 100 caracteres y usar solo minúsculas, números, punto, guion o guion bajo.");
        }

        if (string.IsNullOrWhiteSpace(tipologia.Nombre))
        {
            errors.Add("Nombre es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(tipologia.Version))
        {
            errors.Add("Version es obligatoria.");
        }
        else if (!VersionRegex.IsMatch(tipologia.Version.Trim()))
        {
            errors.Add("Version debe usar formato tipo 1.0 o 1.0.0.");
        }

        if (existingTipologias is not null && !string.IsNullOrWhiteSpace(tipologia.Codigo))
        {
            var duplicated = existingTipologias.Any(t =>
                t.Id != currentId
                && string.Equals(t.Codigo, tipologia.Codigo.Trim(), StringComparison.OrdinalIgnoreCase));

            if (duplicated)
            {
                errors.Add($"Ya existe una tipología con codigo '{tipologia.Codigo.Trim()}'.");
            }
        }

        errors.AddRange(ValidarConfiguracionJson(tipologia.ConfiguracionJson ?? string.Empty, tipologia.Codigo, tipologia.Version));
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyCollection<string> ValidarConfiguracionJson(string json, string? expectedCodigo = null, string? expectedVersion = null)
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

            if (!string.IsNullOrWhiteSpace(config.Version) && !VersionRegex.IsMatch(config.Version.Trim()))
            {
                errors.Add("version debe usar formato tipo 1.0 o 1.0.0.");
            }

            if (!string.IsNullOrWhiteSpace(expectedCodigo)
                && !string.Equals(config.TipologiaId?.Trim(), expectedCodigo.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("tipologiaId debe coincidir con Codigo.");
            }

            if (!string.IsNullOrWhiteSpace(expectedVersion)
                && !string.Equals(config.Version?.Trim(), expectedVersion.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("version del JSON debe coincidir con Version.");
            }

            if (config.ConfidenceConfig is not null)
            {
                ValidateProbability(errors, "confidenceConfig.clasifUmbralFallback", config.ConfidenceConfig.ClasifUmbralFallback);
                ValidateNullableProbability(errors, "confidenceConfig.extracUmbralFallback", config.ConfidenceConfig.ExtracUmbralFallback);
                ValidateNullableProbability(errors, "confidenceConfig.extracUmbralFallbackCompletitud", config.ConfidenceConfig.ExtracUmbralFallbackCompletitud);
                ValidateNullableProbability(errors, "confidenceConfig.extracUmbralFallbackConfianza", config.ConfidenceConfig.ExtracUmbralFallbackConfianza);
                ValidateProbability(errors, "confidenceConfig.extracWeightCampos", config.ConfidenceConfig.ExtracWeightCampos);
                ValidateProbability(errors, "confidenceConfig.extracWeightRequeridos", config.ConfidenceConfig.ExtracWeightRequeridos);
                ValidateProbability(errors, "confidenceConfig.extracWeightWarnings", config.ConfidenceConfig.ExtracWeightWarnings);
                ValidateProbability(errors, "confidenceConfig.umbralOK", config.ConfidenceConfig.UmbralOK);
                ValidateProbability(errors, "confidenceConfig.umbralRevision", config.ConfidenceConfig.UmbralRevision);

                if (config.ConfidenceConfig.UmbralRevision > config.ConfidenceConfig.UmbralOK)
                {
                    errors.Add("confidenceConfig.umbralRevision no puede ser mayor que confidenceConfig.umbralOK.");
                }
            }

            if (config.Extraction is not null)
            {
                if (config.Extraction.Enabled)
                {
                    if (string.IsNullOrWhiteSpace(config.Extraction.Provider))
                    {
                        errors.Add("extraction.provider es obligatorio cuando extraction.enabled=true.");
                    }

                    if (string.IsNullOrWhiteSpace(config.Extraction.ModelKey))
                    {
                        errors.Add("extraction.modelKey es obligatorio cuando extraction.enabled=true.");
                    }
                }

                var fieldNames = config.Fields
                    .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                    .Select(f => f.Name.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var duplicatedMappings = config.Extraction.FieldMappings
                    .Where(m => !string.IsNullOrWhiteSpace(m.TargetField))
                    .GroupBy(m => m.TargetField.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToArray();

                foreach (var targetField in duplicatedMappings)
                {
                    errors.Add($"extraction.fieldMappings contiene targetField duplicado: {targetField}.");
                }

                foreach (var mapping in config.Extraction.FieldMappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.TargetField))
                    {
                        errors.Add("extraction.fieldMappings.targetField es obligatorio.");
                    }

                    if (string.IsNullOrWhiteSpace(mapping.SourcePath))
                    {
                        errors.Add($"extraction.fieldMappings[{mapping.TargetField}].sourcePath es obligatorio.");
                    }

                    if (!string.IsNullOrWhiteSpace(mapping.TargetField) && fieldNames.Count > 0 && !fieldNames.Contains(mapping.TargetField.Trim()))
                    {
                        errors.Add($"extraction.fieldMappings referencia un field inexistente: {mapping.TargetField}.");
                    }
                }
            }

            var duplicatedFields = config.Fields
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .GroupBy(f => f.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            foreach (var duplicatedField in duplicatedFields)
            {
                errors.Add($"fields contiene nombres duplicados: {duplicatedField}.");
            }

            foreach (var field in config.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.Name))
                {
                    errors.Add("fields.name es obligatorio.");
                }

                if (string.IsNullOrWhiteSpace(field.Type))
                {
                    errors.Add($"fields[{field.Name}].type es obligatorio.");
                }
                else if (!SupportedFieldTypes.Contains(field.Type.Trim()))
                {
                    errors.Add($"fields[{field.Name}].type no soportado: {field.Type}.");
                }

                if (string.Equals(field.Type?.Trim(), "array", StringComparison.OrdinalIgnoreCase))
                {
                    if (field.Items is null)
                    {
                        errors.Add($"fields[{field.Name}] de tipo array requiere items.");
                    }
                    else if (string.IsNullOrWhiteSpace(field.Items.Type))
                    {
                        errors.Add($"fields[{field.Name}].items.type es obligatorio.");
                    }
                }
            }

            if (!config.SkipGDCUpload)
            {
                if (string.IsNullOrWhiteSpace(config.GdcTipoDocumento))
                {
                    errors.Add("gdcTipoDocumento es obligatorio cuando skipGDCUpload=false.");
                }

                if (string.IsNullOrWhiteSpace(config.GdcSerie))
                {
                    errors.Add("gdcSerie es obligatorio cuando skipGDCUpload=false.");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"JSON inválido: {ex.Message}");
        }

        return errors;
    }

    private static void ValidateProbability(List<string> errors, string name, double value)
    {
        if (value is < 0 or > 1)
        {
            errors.Add($"{name} debe estar entre 0 y 1.");
        }
    }

    private static void ValidateNullableProbability(List<string> errors, string name, double? value)
    {
        if (value.HasValue)
        {
            ValidateProbability(errors, name, value.Value);
        }
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

    public sealed class TipologiaAuditEntry
    {
        public int Id { get; set; }
        public int TipologiaId { get; set; }
        public string Accion { get; set; } = string.Empty;
        public string? Usuario { get; set; }
        public DateTime FechaHora { get; set; }
        public string? DetallesJson { get; set; }
    }

    public sealed class TipologiaVersionItem
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public EstadoTipologia Estado { get; set; }
        public string Family { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
    }

    public sealed class TipologiaDiffResult
    {
        public TipologiaDiffEndpoint Left { get; set; } = new();
        public TipologiaDiffEndpoint Right { get; set; } = new();
        public IReadOnlyCollection<TipologiaDiffChange> Changes { get; set; } = Array.Empty<TipologiaDiffChange>();
        public int TotalChanges { get; set; }
        public int Added { get; set; }
        public int Removed { get; set; }
        public int Modified { get; set; }
    }

    public sealed class TipologiaDiffEndpoint
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
    }

    public sealed class TipologiaDiffChange
    {
        public string Section { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty;
        public string? LeftValue { get; set; }
        public string? RightValue { get; set; }
    }
}
