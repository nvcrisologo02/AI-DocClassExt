using System.Net;
using System.Text.Json;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Functions.Triggers.Admin;

public class CatalogosTdnAdminFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DocumentIADbContext _dbContext;

    public CatalogosTdnAdminFunction(DocumentIADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("Admin_GetCatalogoTdn1")]
    public async Task<HttpResponseData> GetCatalogoTdn1(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/catalogotdn1")] HttpRequestData req)
    {
        var rows = await _dbContext.CatalogoTdn1
            .AsNoTracking()
            .OrderBy(x => x.Codigo)
            .Select(x => new CatalogoTdn1Dto
            {
                Id = x.Id,
                Codigo = x.Codigo,
                Nombre = x.Nombre,
                Descripcion = x.Descripcion,
                Tdn2Prompt = x.TDN2_Prompt,
                Subtipos = x.SubTipos.Count
            })
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(rows);
        return response;
    }

    [Function("Admin_GetCatalogoTdn1ById")]
    public async Task<HttpResponseData> GetCatalogoTdn1ById(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/catalogotdn1/{id:int}")] HttpRequestData req,
        int id)
    {
        var row = await _dbContext.CatalogoTdn1
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CatalogoTdn1Dto
            {
                Id = x.Id,
                Codigo = x.Codigo,
                Nombre = x.Nombre,
                Descripcion = x.Descripcion,
                Tdn2Prompt = x.TDN2_Prompt,
                Subtipos = x.SubTipos.Count
            })
            .FirstOrDefaultAsync();

        if (row is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe registro CatalogoTdn1 con id {id}.");
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(row);
        return response;
    }

    [Function("Admin_CreateCatalogoTdn1")]
    public async Task<HttpResponseData> CreateCatalogoTdn1(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/catalogotdn1")] HttpRequestData req)
    {
        var payload = await ReadBody<CatalogoTdn1UpsertRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido.");
        }

        var validationError = ValidateTdn1(payload);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, validationError);
        }

        var codigo = payload.Codigo.Trim().ToUpperInvariant();
        var exists = await _dbContext.CatalogoTdn1.AnyAsync(x => x.Codigo == codigo);
        if (exists)
        {
            return await CreateError(req, HttpStatusCode.Conflict, $"Ya existe un TDN1 con codigo '{codigo}'.");
        }

        var entity = new CatalogoTdn1Entity
        {
            Codigo = codigo,
            Nombre = payload.Nombre.Trim(),
            Descripcion = payload.Descripcion?.Trim(),
            TDN2_Prompt = payload.Tdn2Prompt
        };

        _dbContext.CatalogoTdn1.Add(entity);
        await _dbContext.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new CatalogoTdn1Dto
        {
            Id = entity.Id,
            Codigo = entity.Codigo,
            Nombre = entity.Nombre,
            Descripcion = entity.Descripcion,
            Tdn2Prompt = entity.TDN2_Prompt,
            Subtipos = 0
        });
        return response;
    }

    [Function("Admin_UpdateCatalogoTdn1")]
    public async Task<HttpResponseData> UpdateCatalogoTdn1(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "management/catalogotdn1/{id:int}")] HttpRequestData req,
        int id)
    {
        var entity = await _dbContext.CatalogoTdn1.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe registro CatalogoTdn1 con id {id}.");
        }

        var payload = await ReadBody<CatalogoTdn1UpsertRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido.");
        }

        var validationError = ValidateTdn1(payload);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, validationError);
        }

        var codigo = payload.Codigo.Trim().ToUpperInvariant();
        var duplicated = await _dbContext.CatalogoTdn1.AnyAsync(x => x.Id != id && x.Codigo == codigo);
        if (duplicated)
        {
            return await CreateError(req, HttpStatusCode.Conflict, $"Ya existe un TDN1 con codigo '{codigo}'.");
        }

        entity.Codigo = codigo;
        entity.Nombre = payload.Nombre.Trim();
        entity.Descripcion = payload.Descripcion?.Trim();
        entity.TDN2_Prompt = payload.Tdn2Prompt;

        await _dbContext.SaveChangesAsync();

        var subtipos = await _dbContext.CatalogoTdn2.CountAsync(x => x.Tdn1Id == id);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new CatalogoTdn1Dto
        {
            Id = entity.Id,
            Codigo = entity.Codigo,
            Nombre = entity.Nombre,
            Descripcion = entity.Descripcion,
            Tdn2Prompt = entity.TDN2_Prompt,
            Subtipos = subtipos
        });
        return response;
    }

    [Function("Admin_DeleteCatalogoTdn1")]
    public async Task<HttpResponseData> DeleteCatalogoTdn1(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "management/catalogotdn1/{id:int}")] HttpRequestData req,
        int id)
    {
        var entity = await _dbContext.CatalogoTdn1.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe registro CatalogoTdn1 con id {id}.");
        }

        var hasChildren = await _dbContext.CatalogoTdn2.AnyAsync(x => x.Tdn1Id == id);
        if (hasChildren)
        {
            return await CreateError(req, HttpStatusCode.Conflict, "No se puede borrar el TDN1 porque tiene registros TDN2 asociados.");
        }

        _dbContext.CatalogoTdn1.Remove(entity);
        await _dbContext.SaveChangesAsync();

        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    [Function("Admin_GetCatalogoTdn2")]
    public async Task<HttpResponseData> GetCatalogoTdn2(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/catalogotdn2")] HttpRequestData req)
    {
        var rows = await _dbContext.CatalogoTdn2
            .AsNoTracking()
            .OrderBy(x => x.CodigoTdn1)
            .ThenBy(x => x.Codigo)
            .Select(x => new CatalogoTdn2Dto
            {
                Id = x.Id,
                Codigo = x.Codigo,
                Nombre = x.Nombre,
                Descripcion = x.Descripcion,
                CodigoTdn1 = x.CodigoTdn1,
                Tdn1Id = x.Tdn1Id
            })
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(rows);
        return response;
    }

    [Function("Admin_GetCatalogoTdn2ById")]
    public async Task<HttpResponseData> GetCatalogoTdn2ById(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/catalogotdn2/{id:int}")] HttpRequestData req,
        int id)
    {
        var row = await _dbContext.CatalogoTdn2
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CatalogoTdn2Dto
            {
                Id = x.Id,
                Codigo = x.Codigo,
                Nombre = x.Nombre,
                Descripcion = x.Descripcion,
                CodigoTdn1 = x.CodigoTdn1,
                Tdn1Id = x.Tdn1Id
            })
            .FirstOrDefaultAsync();

        if (row is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe registro CatalogoTdn2 con id {id}.");
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(row);
        return response;
    }

    [Function("Admin_GetCatalogoTdn2ByTdn1")]
    public async Task<HttpResponseData> GetCatalogoTdn2ByTdn1(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "management/catalogotdn2/by-tdn1/{codigoTdn1}")] HttpRequestData req,
        string codigoTdn1)
    {
        if (string.IsNullOrWhiteSpace(codigoTdn1))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "codigoTdn1 es obligatorio.");
        }

        var codigo = codigoTdn1.Trim().ToUpperInvariant();
        var rows = await _dbContext.CatalogoTdn2
            .AsNoTracking()
            .Where(x => x.CodigoTdn1 == codigo)
            .OrderBy(x => x.Codigo)
            .Select(x => new CatalogoTdn2Dto
            {
                Id = x.Id,
                Codigo = x.Codigo,
                Nombre = x.Nombre,
                Descripcion = x.Descripcion,
                CodigoTdn1 = x.CodigoTdn1,
                Tdn1Id = x.Tdn1Id
            })
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(rows);
        return response;
    }

    [Function("Admin_CreateCatalogoTdn2")]
    public async Task<HttpResponseData> CreateCatalogoTdn2(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "management/catalogotdn2")] HttpRequestData req)
    {
        var payload = await ReadBody<CatalogoTdn2UpsertRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido.");
        }

        var validationError = ValidateTdn2(payload);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, validationError);
        }

        var codigo = payload.Codigo.Trim().ToUpperInvariant();
        var codigoTdn1 = payload.CodigoTdn1.Trim().ToUpperInvariant();

        var duplicated = await _dbContext.CatalogoTdn2.AnyAsync(x => x.Codigo == codigo);
        if (duplicated)
        {
            return await CreateError(req, HttpStatusCode.Conflict, $"Ya existe un TDN2 con codigo '{codigo}'.");
        }

        var parent = await _dbContext.CatalogoTdn1.FirstOrDefaultAsync(x => x.Codigo == codigoTdn1);
        if (parent is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, $"No existe TDN1 con codigo '{codigoTdn1}'.");
        }

        var entity = new CatalogoTdn2Entity
        {
            Codigo = codigo,
            Nombre = payload.Nombre.Trim(),
            Descripcion = payload.Descripcion?.Trim(),
            CodigoTdn1 = parent.Codigo,
            Tdn1Id = parent.Id
        };

        _dbContext.CatalogoTdn2.Add(entity);
        await _dbContext.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new CatalogoTdn2Dto
        {
            Id = entity.Id,
            Codigo = entity.Codigo,
            Nombre = entity.Nombre,
            Descripcion = entity.Descripcion,
            CodigoTdn1 = entity.CodigoTdn1,
            Tdn1Id = entity.Tdn1Id
        });
        return response;
    }

    [Function("Admin_UpdateCatalogoTdn2")]
    public async Task<HttpResponseData> UpdateCatalogoTdn2(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "management/catalogotdn2/{id:int}")] HttpRequestData req,
        int id)
    {
        var entity = await _dbContext.CatalogoTdn2.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe registro CatalogoTdn2 con id {id}.");
        }

        var payload = await ReadBody<CatalogoTdn2UpsertRequest>(req);
        if (payload is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, "Body invalido.");
        }

        var validationError = ValidateTdn2(payload);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return await CreateError(req, HttpStatusCode.BadRequest, validationError);
        }

        var codigo = payload.Codigo.Trim().ToUpperInvariant();
        var codigoTdn1 = payload.CodigoTdn1.Trim().ToUpperInvariant();

        var duplicated = await _dbContext.CatalogoTdn2.AnyAsync(x => x.Id != id && x.Codigo == codigo);
        if (duplicated)
        {
            return await CreateError(req, HttpStatusCode.Conflict, $"Ya existe un TDN2 con codigo '{codigo}'.");
        }

        var parent = await _dbContext.CatalogoTdn1.FirstOrDefaultAsync(x => x.Codigo == codigoTdn1);
        if (parent is null)
        {
            return await CreateError(req, HttpStatusCode.BadRequest, $"No existe TDN1 con codigo '{codigoTdn1}'.");
        }

        entity.Codigo = codigo;
        entity.Nombre = payload.Nombre.Trim();
        entity.Descripcion = payload.Descripcion?.Trim();
        entity.CodigoTdn1 = parent.Codigo;
        entity.Tdn1Id = parent.Id;

        await _dbContext.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new CatalogoTdn2Dto
        {
            Id = entity.Id,
            Codigo = entity.Codigo,
            Nombre = entity.Nombre,
            Descripcion = entity.Descripcion,
            CodigoTdn1 = entity.CodigoTdn1,
            Tdn1Id = entity.Tdn1Id
        });
        return response;
    }

    [Function("Admin_DeleteCatalogoTdn2")]
    public async Task<HttpResponseData> DeleteCatalogoTdn2(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "management/catalogotdn2/{id:int}")] HttpRequestData req,
        int id)
    {
        var entity = await _dbContext.CatalogoTdn2.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return await CreateError(req, HttpStatusCode.NotFound, $"No existe registro CatalogoTdn2 con id {id}.");
        }

        _dbContext.CatalogoTdn2.Remove(entity);
        await _dbContext.SaveChangesAsync();

        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    private static string? ValidateTdn1(CatalogoTdn1UpsertRequest payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Codigo))
        {
            return "Codigo es obligatorio.";
        }

        if (payload.Codigo.Trim().Length > 10)
        {
            return "Codigo no puede superar 10 caracteres.";
        }

        if (string.IsNullOrWhiteSpace(payload.Nombre))
        {
            return "Nombre es obligatorio.";
        }

        if (payload.Nombre.Trim().Length > 200)
        {
            return "Nombre no puede superar 200 caracteres.";
        }

        if (!string.IsNullOrWhiteSpace(payload.Descripcion) && payload.Descripcion.Trim().Length > 2000)
        {
            return "Descripcion no puede superar 2000 caracteres.";
        }

        return null;
    }

    private static string? ValidateTdn2(CatalogoTdn2UpsertRequest payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Codigo))
        {
            return "Codigo es obligatorio.";
        }

        if (payload.Codigo.Trim().Length > 15)
        {
            return "Codigo no puede superar 15 caracteres.";
        }

        if (string.IsNullOrWhiteSpace(payload.CodigoTdn1))
        {
            return "CodigoTdn1 es obligatorio.";
        }

        if (payload.CodigoTdn1.Trim().Length > 10)
        {
            return "CodigoTdn1 no puede superar 10 caracteres.";
        }

        if (string.IsNullOrWhiteSpace(payload.Nombre))
        {
            return "Nombre es obligatorio.";
        }

        if (payload.Nombre.Trim().Length > 200)
        {
            return "Nombre no puede superar 200 caracteres.";
        }

        if (!string.IsNullOrWhiteSpace(payload.Descripcion) && payload.Descripcion.Trim().Length > 2000)
        {
            return "Descripcion no puede superar 2000 caracteres.";
        }

        return null;
    }

    private static async Task<T?> ReadBody<T>(HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private static async Task<HttpResponseData> CreateError(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }

    private sealed class CatalogoTdn1UpsertRequest
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? Tdn2Prompt { get; set; }
    }

    private sealed class CatalogoTdn2UpsertRequest
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string CodigoTdn1 { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }

    private sealed class CatalogoTdn1Dto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? Tdn2Prompt { get; set; }
        public int Subtipos { get; set; }
    }

    private sealed class CatalogoTdn2Dto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string CodigoTdn1 { get; set; } = string.Empty;
        public int Tdn1Id { get; set; }
    }
}
