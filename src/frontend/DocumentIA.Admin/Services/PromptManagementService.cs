using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentIA.Admin.Models;

namespace DocumentIA.Admin.Services;

/// <summary>Servicio para gestionar plantillas de prompts configurables via API admin.</summary>
public class PromptManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _httpClient;

    public PromptManagementService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>Obtiene lista resumida de todos los templates de prompts.</summary>
    public async Task<List<PromptTemplateListItemDto>> GetPromptTemplatesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("management/prompts");
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<List<PromptTemplateListItemDto>>(JsonOptions) ?? []
                : [];
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error al obtener lista de prompts", ex);
        }
    }

    /// <summary>Obtiene detalle completo de un template de prompt por ID.</summary>
    public async Task<PromptTemplateDto?> GetPromptTemplateAsync(long id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"management/prompts/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Error al obtener prompt: {response.StatusCode}");

            return await response.Content.ReadFromJsonAsync<PromptTemplateDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al obtener prompt ID {id}", ex);
        }
    }

    /// <summary>Obtiene todas las versiones de un template por su clave (key).</summary>
    public async Task<List<PromptTemplateDto>> GetPromptTemplatesByKeyAsync(string promptKey)
    {
        try
        {
            var response = await _httpClient.GetAsync($"management/prompts/by-key/{Uri.EscapeDataString(promptKey)}");
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<List<PromptTemplateDto>>(JsonOptions) ?? []
                : [];
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al obtener versiones de {promptKey}", ex);
        }
    }

    /// <summary>Crea un nuevo template de prompt en estado draft (IsActive=false).</summary>
    public async Task<PromptTemplateDto> CreatePromptTemplateAsync(CreatePromptTemplateRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("management/prompts", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var error = JsonSerializer.Deserialize<ValidationErrorResponse>(errorContent, JsonOptions);
                    throw new InvalidOperationException($"Validación fallida: {string.Join("; ", error?.Errors.Values.SelectMany(v => v) ?? [])}");
                }
                throw new InvalidOperationException($"Error al crear prompt: {response.StatusCode} - {errorContent}");
            }

            return await response.Content.ReadFromJsonAsync<PromptTemplateDto>(JsonOptions)
                   ?? throw new InvalidOperationException("Respuesta vacía del servidor");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error al crear prompt template", ex);
        }
    }

    /// <summary>Actualiza contenido de un template en estado draft.</summary>
    /// <remarks>Solo es posible si IsActive=false. Si IsActive=true devuelve 403 Forbidden.</remarks>
    public async Task<PromptTemplateDto> UpdatePromptTemplateAsync(long id, UpdatePromptTemplateRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"management/prompts/{id}", request);

            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new InvalidOperationException("No se puede actualizar un prompt activo. Debe estar en estado draft.");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Error al actualizar prompt: {response.StatusCode} - {errorContent}");
            }

            return await response.Content.ReadFromJsonAsync<PromptTemplateDto>(JsonOptions)
                   ?? throw new InvalidOperationException("Respuesta vacía del servidor");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al actualizar prompt ID {id}", ex);
        }
    }

    /// <summary>Activa una versión de un template y desactiva la anterior.</summary>
    /// <remarks>Operación atómica: desactiva todas las versiones IsActive=true para la misma key, luego activa la especificada.</remarks>
    public async Task<PromptTemplateDto> ActivatePromptVersionAsync(long id, string publishedBy)
    {
        try
        {
            var request = new ActivatePromptVersionRequest(id, publishedBy);
            var response = await _httpClient.PutAsJsonAsync($"management/prompts/{id}/activate", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Error al activar prompt: {response.StatusCode} - {errorContent}");
            }

            return await response.Content.ReadFromJsonAsync<PromptTemplateDto>(JsonOptions)
                   ?? throw new InvalidOperationException("Respuesta vacía del servidor");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al activar prompt ID {id}", ex);
        }
    }

    /// <summary>Hace rollback a una versión anterior de un template.</summary>
    /// <remarks>Desactiva la versión activa actual y activa la versión target especificada.</remarks>
    public async Task<PromptTemplateDto> RollbackPromptVersionAsync(string promptKey, int targetVersion, string publishedBy)
    {
        try
        {
            var request = new RollbackPromptVersionRequest(promptKey, targetVersion, publishedBy);
            var response = await _httpClient.PostAsJsonAsync("management/prompts/rollback", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Error en rollback: {response.StatusCode} - {errorContent}");
            }

            return await response.Content.ReadFromJsonAsync<PromptTemplateDto>(JsonOptions)
                   ?? throw new InvalidOperationException("Respuesta vacía del servidor");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error en rollback de {promptKey} a versión {targetVersion}", ex);
        }
    }

    /// <summary>Elimina un template de prompt si está en estado draft (IsActive=false).</summary>
    /// <remarks>No es posible eliminar templates activos. Devuelve 403 Forbidden si IsActive=true.</remarks>
    public async Task DeletePromptTemplateAsync(long id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"management/prompts/{id}");

            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new InvalidOperationException("No se puede eliminar un prompt activo. Debe estar en estado draft.");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Error al eliminar prompt: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al eliminar prompt ID {id}", ex);
        }
    }
}
