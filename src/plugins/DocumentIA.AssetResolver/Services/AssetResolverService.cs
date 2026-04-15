using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;
using DocumentIA.AssetResolver.Data;
using DocumentIA.AssetResolver.Data.Entities;
using DocumentIA.AssetResolver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static DocumentIA.AssetResolver.Controllers.AssetResolverController;

namespace DocumentIA.AssetResolver.Services;

public class AssetResolverService
{
    private const string AllFieldsToken = "#ALL#";

    private readonly AssetResolverDbContext _db;
    private readonly FieldAliasesConfig _aliases;
    private readonly ILogger<AssetResolverService> _logger;

    // Columnas obligatorias que siempre se devuelven
    private static readonly HashSet<string> ObligatoryFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ID_ACTIVO_SAREB",
        "FCH_CIERRE",
        "FCH_ALTA",
        "FCH_BAJA",
        "DES_SERVICER"
    };

    private sealed record FieldProjection(PropertyInfo Property, string ColumnName);

    private static readonly Dictionary<string, FieldProjection> ValidFieldsByRequestName = BuildValidFieldsByRequestName();

    private static readonly Dictionary<string, FieldProjection> ValidFieldsByColumnName =
        ValidFieldsByRequestName.Values
            .GroupBy(v => v.ColumnName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private static readonly List<string> AllColumnNames =
        ValidFieldsByColumnName.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public AssetResolverService(
        AssetResolverDbContext db,
        IOptions<FieldAliasesConfig> aliases,
        ILogger<AssetResolverService> logger)
    {
        _db = db;
        _aliases = aliases.Value;
        _logger = logger;
    }

    public async Task<GetAAIIInfoResponse> BuscarActivosAsync(GetAAIIInfoRequest request, CancellationToken ct = default)
    {
        var response = new GetAAIIInfoResponse { CorrelationId = request.CorrelationId };

        // 1. Detectar valores de búsqueda (aliases del request tienen prioridad sobre config global)
        var aliasesIdufir = request.MapeoIdufir is { Count: > 0 }
            ? request.MapeoIdufir
            : _aliases.Idufir;
        var aliasesRefCat = request.MapeoReferenciaCatastral is { Count: > 0 }
            ? request.MapeoReferenciaCatastral
            : _aliases.ReferenciaCatastral;

        // Regla de negocio: la resolución por aliases solo debe ejecutarse si ambos campos
        // vienen vacíos. Si uno viene indicado (override o mapeado en la tipología),
        // la búsqueda se realiza exclusivamente por el/los campos indicados.
        // Nota: no consideramos presencia directa en `ExtractedData` como indicador;
        // en su lugar, el hecho de que exista un mapeo en la tipología indica que
        // la intención es buscar por ese campo.
        bool indicatedIdufir = !string.IsNullOrWhiteSpace(request.IdufirOverride)
            || (request.MapeoIdufir is { Count: > 0 });
        bool indicatedRefCat = !string.IsNullOrWhiteSpace(request.ReferenciaCatastralOverride)
            || (request.MapeoReferenciaCatastral is { Count: > 0 });

        string? idufir = null;
        string? refCatastral = null;

        if (!indicatedIdufir && !indicatedRefCat)
        {
            // Ninguno indicado: intentar resolver ambos mediante aliases
            idufir = DetectarValor(request.ExtractedData, request.IdufirOverride, aliasesIdufir);
            refCatastral = DetectarValor(request.ExtractedData, request.ReferenciaCatastralOverride, aliasesRefCat);
        }
        else if (indicatedIdufir && !indicatedRefCat)
        {
            // Solo IDUFIR indicado: resolver IDUFIR y no intentar resolver referencia catastral
            idufir = DetectarValor(request.ExtractedData, request.IdufirOverride, aliasesIdufir);
            refCatastral = null;
        }
        else if (!indicatedIdufir && indicatedRefCat)
        {
            // Solo ReferenciaCatastral indicada: resolver referencia y no intentar IDUFIR
            idufir = null;
            refCatastral = DetectarValor(request.ExtractedData, request.ReferenciaCatastralOverride, aliasesRefCat);
        }
        else
        {
            // Ambos indicados: resolver ambos
            idufir = DetectarValor(request.ExtractedData, request.IdufirOverride, aliasesIdufir);
            refCatastral = DetectarValor(request.ExtractedData, request.ReferenciaCatastralOverride, aliasesRefCat);
        }

        if (string.IsNullOrWhiteSpace(idufir) && string.IsNullOrWhiteSpace(refCatastral))
        {
            response.Message = "No se encontraron criterios de búsqueda (IDUFIR ni ReferenciaCatastral) en los datos extraídos.";
            return response;
        }

        response.CriteriosUsados = new CriteriosUsados
        {
            Idufir = idufir,
            ReferenciaCatastral = refCatastral
        };

        // 2. Resolver campos solicitados por nombre de columna; #ALL# expande a todas las columnas.
        var camposValidos = ResolveRequestedFields(request.RequestedFields, out var camposConError);
        response.CamposConError = camposConError;

        // 3. Consultar BD
        var resultados = await ConsultarAsync(idufir, refCatastral, ct);

        if (resultados.Count == 0)
        {
            response.Message = "No se encontraron activos con los criterios proporcionados.";
            return response;
        }

        // 4. Construir respuesta con campos solicitados
        response.Found = true;
        response.Count = resultados.Count;
        response.Activos = resultados.Select(r => BuildActivoEncontrado(r, camposValidos)).ToList();
        response.Message = resultados.Count == 1
            ? "Se encontró 1 activo."
            : $"Se encontraron {resultados.Count} activos.";

        return response;
    }

    private string? DetectarValor(
        Dictionary<string, string?> extractedData,
        string? overrideValue,
        List<string> aliases)
    {
        // Override del llamante tiene prioridad
        if (!string.IsNullOrWhiteSpace(overrideValue))
            return overrideValue.Trim();

        // Buscar en datos extraídos por aliases (case-insensitive)
        foreach (var alias in aliases)
        {
            var match = extractedData
                .FirstOrDefault(kv => string.Equals(kv.Key, alias, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
                return match.Value.Trim();
        }

        return null;
    }

    private async Task<List<DmPosicionAAII>> ConsultarAsync(
        string? idufir, string? refCatastral, CancellationToken ct)
    {
        // Traer resultados por cada criterio, deduplicar por PK, quedarnos con el más reciente por activo
        var resultadosPorIdufir = new List<DmPosicionAAII>();
        var resultadosPorRefCat = new List<DmPosicionAAII>();

        if (!string.IsNullOrWhiteSpace(idufir))
        {
            resultadosPorIdufir = await _db.DmPosicionAAII
                .AsNoTracking()
                .Where(x => x.IdIdufir == idufir)
                .ToListAsync(ct);

            _logger.LogInformation("Búsqueda por IDUFIR={Idufir}: {Count} registros", idufir, resultadosPorIdufir.Count);
        }

        if (!string.IsNullOrWhiteSpace(refCatastral))
        {
            resultadosPorRefCat = await _db.DmPosicionAAII
                .AsNoTracking()
                .Where(x => x.IdRefCatast == refCatastral)
                .ToListAsync(ct);

            _logger.LogInformation("Búsqueda por RefCatastral={RefCat}: {Count} registros", refCatastral, resultadosPorRefCat.Count);
        }

        // Unir y deduplicar por PK (IdActivoSareb + FchCierreDt)
        var todos = resultadosPorIdufir
            .Concat(resultadosPorRefCat)
            .GroupBy(x => new { x.IdActivoSareb, x.FchCierreDt })
            .Select(g => g.First())
            .ToList();

        // Quedarnos con el registro más reciente por IdActivoSareb
        var masRecientes = todos
            .GroupBy(x => x.IdActivoSareb)
            .Select(g => g.OrderByDescending(x => x.FchCierreDt).First())
            .ToList();

        return masRecientes;
    }

    private static Dictionary<string, FieldProjection> BuildValidFieldsByRequestName()
    {
        var fields = new Dictionary<string, FieldProjection>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in typeof(DmPosicionAAII).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var columnName = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
            var projection = new FieldProjection(property, columnName);

            fields[columnName] = projection;

            if (!fields.ContainsKey(property.Name))
            {
                fields[property.Name] = projection;
            }
        }

        return fields;
    }

    private static List<string> ResolveRequestedFields(List<string>? requestedFields, out List<string> camposConError)
    {
        camposConError = new List<string>();

        if (requestedFields == null || requestedFields.Count == 0)
        {
            return [];
        }

        if (requestedFields.Any(field => string.Equals(field?.Trim(), AllFieldsToken, StringComparison.OrdinalIgnoreCase)))
        {
            return [.. AllColumnNames];
        }

        var camposValidos = new List<string>();

        foreach (var requestedField in requestedFields)
        {
            var normalized = requestedField?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (ValidFieldsByRequestName.TryGetValue(normalized, out var fieldProjection))
            {
                if (!camposValidos.Contains(fieldProjection.ColumnName, StringComparer.OrdinalIgnoreCase))
                {
                    camposValidos.Add(fieldProjection.ColumnName);
                }
            }
            else
            {
                camposConError.Add(normalized);
            }
        }

        foreach (var obligatoryField in ObligatoryFields)
        {
            if (!camposValidos.Contains(obligatoryField, StringComparer.OrdinalIgnoreCase))
            {
                camposValidos.Add(obligatoryField);
            }
        }

        return camposValidos;
    }

    private static ActivoEncontrado BuildActivoEncontrado(DmPosicionAAII entity, List<string> camposValidos)
    {
        var activo = new ActivoEncontrado
        {
            IdActivo = entity.IdActivoSareb.ToString("F0"),
            FchCierre = entity.FchCierre
        };

        var campos = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var campo in camposValidos)
        {
            if (ValidFieldsByColumnName.TryGetValue(campo, out var fieldProjection))
            {
                campos[fieldProjection.ColumnName] = fieldProjection.Property.GetValue(entity);
            }
        }
        activo.CamposSolicitados = campos;

        return activo;
    }

    // ── DTOs de respuesta internos (también usados por el controller) ──

    public class CriteriosUsados
    {
        public string? Idufir { get; set; }
        public string? ReferenciaCatastral { get; set; }
    }

    public class ActivoEncontrado
    {
        public string IdActivo { get; set; } = string.Empty;
        public DateTime? FchCierre { get; set; }
        public Dictionary<string, object?> CamposSolicitados { get; set; } = new();
    }
}
