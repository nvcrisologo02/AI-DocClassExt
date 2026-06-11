using System.Net;
using System.Text.Json;
using DocumentIA.Core.Models;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Triggers.Admin;

public class PromptsAdminFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Placeholders permitidos según especificación
    private static readonly string[] AllowedPlaceholders =
    {
        "{CONTEXT_PROMPT}",
        "{TDN1_CATALOG}",
        "{TDN2_CATALOG}",
        "{TDN1_CODE}",
        "{DOCUMENT_TEXT}",
        "{campo}",
        "{contenido}"
    };

    private readonly DocumentIADbContext _dbContext;
    private readonly ILogger<PromptsAdminFunction> _logger;

    public PromptsAdminFunction(DocumentIADbContext dbContext, ILogger<PromptsAdminFunction> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #region GET Endpoints

    [Function("Admin_GetPromptTemplates")]
    public async Task<HttpResponseData> GetPromptTemplates(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/prompts")] HttpRequestData req)
    {
        var prompts = await _dbContext.PromptTemplates
            .AsNoTracking()
            .OrderBy(p => p.PromptKey)
            .ThenByDescending(p => p.Version)
            .Select(p => new PromptTemplateListItemDto
            {
                Id = p.Id,
                PromptKey = p.PromptKey,
                Version = p.Version,
                IsActive = p.IsActive,
                Description = p.Description,
                CreatedAtUtc = p.CreatedAtUtc,
                PublishedAtUtc = p.PublishedAtUtc,
                ContentLength = p.Content.Length
            })
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(prompts);
        _logger.LogInformation("Listado de {Count} prompts obtenido.", prompts.Count);
        return response;
    }

    [Function("Admin_GetPromptTemplateById")]
    public async Task<HttpResponseData> GetPromptTemplateById(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/prompts/{id:long}")] HttpRequestData req,
        long id)
    {
        var prompt = await _dbContext.PromptTemplates
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PromptTemplateDto
            {
                Id = p.Id,
                PromptKey = p.PromptKey,
                Version = p.Version,
                Content = p.Content,
                IsActive = p.IsActive,
                Description = p.Description,
                CreatedAtUtc = p.CreatedAtUtc,
                CreatedBy = p.CreatedBy,
                UpdatedAtUtc = p.UpdatedAtUtc,
                UpdatedBy = p.UpdatedBy,
                PublishedAtUtc = p.PublishedAtUtc,
                PublishedBy = p.PublishedBy
            })
            .FirstOrDefaultAsync();

        if (prompt is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe PromptTemplate con Id {id}.");
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(prompt);
        _logger.LogInformation("PromptTemplate {Id} obtenido.", id);
        return response;
    }

    [Function("Admin_GetPromptTemplatesByKey")]
    public async Task<HttpResponseData> GetPromptTemplatesByKey(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/prompts/by-key/{promptKey}")] HttpRequestData req,
        string promptKey)
    {
        var normalizedKey = promptKey.Trim().ToLowerInvariant();

        var prompts = await _dbContext.PromptTemplates
            .AsNoTracking()
            .Where(p => p.PromptKey.ToLower() == normalizedKey)
            .OrderByDescending(p => p.Version)
            .Select(p => new PromptTemplateDto
            {
                Id = p.Id,
                PromptKey = p.PromptKey,
                Version = p.Version,
                Content = p.Content,
                IsActive = p.IsActive,
                Description = p.Description,
                CreatedAtUtc = p.CreatedAtUtc,
                CreatedBy = p.CreatedBy,
                UpdatedAtUtc = p.UpdatedAtUtc,
                UpdatedBy = p.UpdatedBy,
                PublishedAtUtc = p.PublishedAtUtc,
                PublishedBy = p.PublishedBy
            })
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(prompts);
        _logger.LogInformation("Obtenidas {Count} versiones para PromptKey '{PromptKey}'.", prompts.Count, promptKey);
        return response;
    }

    #endregion

    #region POST/PUT Endpoints

    [Function("Admin_CreatePromptTemplate")]
    public async Task<HttpResponseData> CreatePromptTemplate(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/prompts")] HttpRequestData req)
    {
        var payload = await ReadBody<CreatePromptTemplateRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body inválido.");
        }

        // Validar contenido
        var validationError = ValidatePromptContent(payload.Content, payload.PromptKey);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, validationError);
        }

        var normalizedKey = payload.PromptKey.Trim().ToLowerInvariant();

        // Calcular siguiente versión
        var maxVersion = await _dbContext.PromptTemplates
            .Where(p => p.PromptKey.ToLower() == normalizedKey)
            .MaxAsync(p => (int?)p.Version) ?? 0;

        var newVersion = maxVersion + 1;

        var entity = new PromptTemplateEntity
        {
            PromptKey = payload.PromptKey.Trim(),
            Version = newVersion,
            Content = payload.Content.Trim(),
            IsActive = false, // Siempre se crea como borrador
            Description = string.IsNullOrWhiteSpace(payload.Description) ? null : payload.Description!.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = string.IsNullOrWhiteSpace(payload.CreatedBy) ? null : payload.CreatedBy!.Trim()
        };

        _dbContext.PromptTemplates.Add(entity);
        await _dbContext.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new PromptTemplateDto
        {
            Id = entity.Id,
            PromptKey = entity.PromptKey,
            Version = entity.Version,
            Content = entity.Content,
            IsActive = entity.IsActive,
            Description = entity.Description,
            CreatedAtUtc = entity.CreatedAtUtc,
            CreatedBy = entity.CreatedBy,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            UpdatedBy = entity.UpdatedBy,
            PublishedAtUtc = entity.PublishedAtUtc,
            PublishedBy = entity.PublishedBy
        });

        _logger.LogInformation("PromptTemplate borrador creado: {PromptKey} v{Version} (Id: {Id}).",
            entity.PromptKey, entity.Version, entity.Id);

        return response;
    }

    [Function("Admin_UpdatePromptTemplate")]
    public async Task<HttpResponseData> UpdatePromptTemplate(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "management/prompts/{id:long}")] HttpRequestData req,
        long id)
    {
        var entity = await _dbContext.PromptTemplates.FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe PromptTemplate con Id {id}.");
        }

        if (entity.IsActive)
        {
            return await CreateError(req, HttpStatusCode.Conflict,
                "No se puede modificar un PromptTemplate activo. Desactívelo primero o cree una nueva versión.");
        }

        var payload = await ReadBody<UpdatePromptTemplateRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body inválido.");
        }

        // Validar contenido
        var validationError = ValidatePromptContent(payload.Content, entity.PromptKey);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, validationError);
        }

        entity.Content = payload.Content.Trim();
        entity.Description = string.IsNullOrWhiteSpace(payload.Description) ? null : payload.Description!.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = string.IsNullOrWhiteSpace(payload.UpdatedBy) ? null : payload.UpdatedBy!.Trim();

        await _dbContext.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new PromptTemplateDto
        {
            Id = entity.Id,
            PromptKey = entity.PromptKey,
            Version = entity.Version,
            Content = entity.Content,
            IsActive = entity.IsActive,
            Description = entity.Description,
            CreatedAtUtc = entity.CreatedAtUtc,
            CreatedBy = entity.CreatedBy,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            UpdatedBy = entity.UpdatedBy,
            PublishedAtUtc = entity.PublishedAtUtc,
            PublishedBy = entity.PublishedBy
        });

        _logger.LogInformation("PromptTemplate actualizado: {PromptKey} v{Version} (Id: {Id}).",
            entity.PromptKey, entity.Version, entity.Id);

        return response;
    }

    [Function("Admin_ActivatePromptVersion")]
    public async Task<HttpResponseData> ActivatePromptVersion(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "management/prompts/{id:long}/activate")] HttpRequestData req,
        long id)
    {
        var entity = await _dbContext.PromptTemplates.FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe PromptTemplate con Id {id}.");
        }

        if (entity.IsActive)
        {
            return await CreateError(req, HttpStatusCode.Conflict,
                $"El PromptTemplate {entity.PromptKey} v{entity.Version} ya está activo.");
        }

        var payload = await ReadBody<ActivatePromptVersionRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body inválido.");
        }

        // Desactivar versión activa anterior del mismo PromptKey (si existe)
        var normalizedKey = entity.PromptKey.ToLowerInvariant();
        var currentActive = await _dbContext.PromptTemplates
            .Where(p => p.PromptKey.ToLower() == normalizedKey && p.IsActive)
            .FirstOrDefaultAsync();

        if (currentActive is not null)
        {
            currentActive.IsActive = false;
            _logger.LogInformation("Desactivando versión anterior: {PromptKey} v{Version} (Id: {Id}).",
                currentActive.PromptKey, currentActive.Version, currentActive.Id);
        }

        // Activar nueva versión
        entity.IsActive = true;
        entity.PublishedAtUtc = DateTime.UtcNow;
        entity.PublishedBy = string.IsNullOrWhiteSpace(payload.PublishedBy) ? null : payload.PublishedBy!.Trim();

        await _dbContext.SaveChangesAsync();

        // Invalidar cache de ClassificationPromptProvider
        _logger.LogWarning("⚠️ CACHE INVALIDATION REQUIRED: PromptTemplate activado. Reiniciar aplicación o esperar expiración de cache (120s).");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new PromptTemplateDto
        {
            Id = entity.Id,
            PromptKey = entity.PromptKey,
            Version = entity.Version,
            Content = entity.Content,
            IsActive = entity.IsActive,
            Description = entity.Description,
            CreatedAtUtc = entity.CreatedAtUtc,
            CreatedBy = entity.CreatedBy,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            UpdatedBy = entity.UpdatedBy,
            PublishedAtUtc = entity.PublishedAtUtc,
            PublishedBy = entity.PublishedBy
        });

        _logger.LogInformation("PromptTemplate activado: {PromptKey} v{Version} (Id: {Id}).",
            entity.PromptKey, entity.Version, entity.Id);

        return response;
    }

    [Function("Admin_RollbackPromptVersion")]
    public async Task<HttpResponseData> RollbackPromptVersion(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/prompts/rollback")] HttpRequestData req)
    {
        var payload = await ReadBody<RollbackPromptVersionRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body inválido.");
        }

        var normalizedKey = payload.PromptKey.Trim().ToLowerInvariant();

        // Buscar versión target
        var targetVersion = await _dbContext.PromptTemplates
            .Where(p => p.PromptKey.ToLower() == normalizedKey && p.Version == payload.TargetVersion)
            .FirstOrDefaultAsync();

        if (targetVersion is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound,
                $"No existe versión {payload.TargetVersion} para PromptKey '{payload.PromptKey}'.");
        }

        if (targetVersion.IsActive)
        {
            return await CreateError(req, HttpStatusCode.Conflict,
                $"La versión {payload.TargetVersion} ya está activa.");
        }

        // Desactivar versión activa actual
        var currentActive = await _dbContext.PromptTemplates
            .Where(p => p.PromptKey.ToLower() == normalizedKey && p.IsActive)
            .FirstOrDefaultAsync();

        if (currentActive is not null)
        {
            currentActive.IsActive = false;
            _logger.LogInformation("Desactivando versión actual: {PromptKey} v{Version} (Id: {Id}).",
                currentActive.PromptKey, currentActive.Version, currentActive.Id);
        }

        // Activar versión target
        targetVersion.IsActive = true;
        targetVersion.PublishedAtUtc = DateTime.UtcNow;
        targetVersion.PublishedBy = string.IsNullOrWhiteSpace(payload.PublishedBy) ? null : payload.PublishedBy!.Trim();

        await _dbContext.SaveChangesAsync();

        // Invalidar cache
        _logger.LogWarning("⚠️ CACHE INVALIDATION REQUIRED: Rollback ejecutado. Reiniciar aplicación o esperar expiración de cache (120s).");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new PromptTemplateDto
        {
            Id = targetVersion.Id,
            PromptKey = targetVersion.PromptKey,
            Version = targetVersion.Version,
            Content = targetVersion.Content,
            IsActive = targetVersion.IsActive,
            Description = targetVersion.Description,
            CreatedAtUtc = targetVersion.CreatedAtUtc,
            CreatedBy = targetVersion.CreatedBy,
            UpdatedAtUtc = targetVersion.UpdatedAtUtc,
            UpdatedBy = targetVersion.UpdatedBy,
            PublishedAtUtc = targetVersion.PublishedAtUtc,
            PublishedBy = targetVersion.PublishedBy
        });

        _logger.LogInformation("Rollback ejecutado: {PromptKey} v{OldVersion} → v{NewVersion}.",
            payload.PromptKey, currentActive?.Version ?? 0, targetVersion.Version);

        return response;
    }

    [Function("Admin_DeletePromptTemplate")]
    public async Task<HttpResponseData> DeletePromptTemplate(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "management/prompts/{id:long}")] HttpRequestData req,
        long id)
    {
        var entity = await _dbContext.PromptTemplates.FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe PromptTemplate con Id {id}.");
        }

        if (entity.IsActive)
        {
            return await CreateError(req, HttpStatusCode.Conflict,
                "No se puede eliminar un PromptTemplate activo. Desactívelo primero.");
        }

        _dbContext.PromptTemplates.Remove(entity);
        await _dbContext.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.NoContent);
        _logger.LogInformation("PromptTemplate eliminado: {PromptKey} v{Version} (Id: {Id}).",
            entity.PromptKey, entity.Version, entity.Id);

        return response;
    }

    #endregion

    #region Validation

    /// <summary>
    /// Valida el contenido de un prompt según las reglas de negocio.
    /// </summary>
    private string? ValidatePromptContent(string content, string promptKey)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "El contenido del prompt no puede estar vacío.";
        }

        if (content.Length > 16000)
        {
            return $"El contenido del prompt excede el límite de 16000 caracteres (actual: {content.Length}).";
        }

        if (content.Length < 10)
        {
            return "El contenido del prompt debe tener al menos 10 caracteres.";
        }

        // Validar presencia de placeholders según el tipo de prompt
        var normalizedKey = promptKey.ToLowerInvariant();

        if (normalizedKey.Contains("phase1"))
        {
            // Phase1 debe tener {CONTEXT_PROMPT}, {TDN1_CATALOG}, {DOCUMENT_TEXT}
            if (!content.Contains("{CONTEXT_PROMPT}"))
            {
                return "Phase1 prompts deben contener el placeholder {CONTEXT_PROMPT}.";
            }

            if (normalizedKey.Contains("user") && !content.Contains("{DOCUMENT_TEXT}"))
            {
                return "Phase1 user prompts deben contener el placeholder {DOCUMENT_TEXT}.";
            }
        }
        else if (normalizedKey.Contains("phase2"))
        {
            // Phase2 debe tener {TDN1_CODE}, {TDN2_CATALOG}, {DOCUMENT_TEXT}
            if (normalizedKey.Contains("user"))
            {
                if (!content.Contains("{TDN1_CODE}"))
                {
                    return "Phase2 user prompts deben contener el placeholder {TDN1_CODE}.";
                }

                if (!content.Contains("{DOCUMENT_TEXT}"))
                {
                    return "Phase2 user prompts deben contener el placeholder {DOCUMENT_TEXT}.";
                }
            }
        }

        // Validar que no haya placeholders no permitidos
        var invalidPlaceholders = new System.Text.RegularExpressions.Regex(@"\{[A-Z_]+\}")
            .Matches(content)
            .Select(m => m.Value)
            .Where(p => !AllowedPlaceholders.Contains(p))
            .Distinct()
            .ToList();

        if (invalidPlaceholders.Any())
        {
            return $"Placeholders no permitidos detectados: {string.Join(", ", invalidPlaceholders)}. " +
                   $"Placeholders válidos: {string.Join(", ", AllowedPlaceholders)}.";
        }

        return null; // Validación exitosa
    }

    #endregion

    #region Helper Methods

    private static async Task<T?> ReadBody<T>(HttpRequestData req) where T : class
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<HttpResponseData> CreateError(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new ValidationErrorResponse
        {
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
        return response;
    }

    #endregion
}
