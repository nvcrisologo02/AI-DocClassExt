using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;
using DocumentIA.AssetResolver.Data;
using DocumentIA.AssetResolver.Data.Entities;
using DocumentIA.AssetResolver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using static DocumentIA.AssetResolver.Controllers.AssetResolverController;

namespace DocumentIA.AssetResolver.Services;

public class AssetResolverService
{
    private const string AllFieldsToken = "#ALL#";

    private readonly AssetResolverDbContext _db;
    private readonly FieldAliasesConfig _aliases;
    private readonly AssetResolverPerformanceOptions _performance;
    private readonly ILogger<AssetResolverService> _logger;

    // Columnas obligatorias que siempre se devuelven
    private static readonly HashSet<string> ObligatoryFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ID_ACTIVO_SAREB",
        "FCH_CIERRE",
        "FCH_ALTA",
        "FCH_BAJA",
        "DES_SERVICER",
        "IND_STATUS"
    };

    private sealed record FieldProjection(PropertyInfo Property, string ColumnName);
    private sealed record DireccionResolvedValues(
        string? DireccionCompleta,
        string? NombreVia,
        string? Numero,
        string? Municipio,
        string? CodigoPostal,
        DireccionQuery Query,
        string DireccionNormalizada);

    private sealed record DireccionSearchResult(
        List<DmPosicionAAII> Resultados,
        double MejorScore,
        int CandidatosEvaluados,
        string Razon);

    private sealed record DireccionSearchResultAacc(
        List<DmPosicionAACC> Resultados,
        double MejorScore,
        int CandidatosEvaluados,
        string Razon);

    private sealed record DireccionParseResult(
        string? NombreVia,
        string? Numero,
        string? Municipio,
        string? CodigoPostal);

    private sealed record DireccionTipificadaResolvedValues(
        string? Pais,
        string? Provincia,
        string? ComunidadAutonoma,
        string? Municipio,
        string? Poblacion,
        string? TipoVia,
        string? Calle,
        string? Numero,
        string? Bloque,
        string? Puerta,
        string? CodigoPostal,
        string? Planta);

    private sealed record DireccionTipificadaSearchResult(
        List<DmPosicionAAII> Resultados,
        int CandidatosEvaluados,
        string Razon);

    private sealed record DireccionTipificadaSearchResultAacc(
        List<DmPosicionAACC> Resultados,
        int CandidatosEvaluados,
        string Razon);

    private static readonly Dictionary<string, FieldProjection> ValidFieldsByRequestName = BuildValidFieldsByRequestName();

    private static readonly Dictionary<string, FieldProjection> ValidFieldsByColumnName =
        ValidFieldsByRequestName.Values
            .GroupBy(v => v.ColumnName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private static readonly List<string> AllColumnNames =
        ValidFieldsByColumnName.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static readonly Dictionary<string, FieldProjection> ValidFieldsByRequestNameAacc = BuildValidFieldsByRequestName(typeof(DmPosicionAACC));

    private static readonly Dictionary<string, FieldProjection> ValidFieldsByColumnNameAacc =
        ValidFieldsByRequestNameAacc.Values
            .GroupBy(v => v.ColumnName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private static readonly List<string> AllColumnNamesAacc =
        ValidFieldsByColumnNameAacc.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static readonly Regex CodigoPostalRegex = new(@"\b\d{5}\b", RegexOptions.Compiled);
    private static readonly Regex NumeroDireccionRegex = new(@"\b\d+[A-Z]?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PisoPuertaTailRegex = new(@"\s+\d+\s*[ºª]?\s*[A-Z]{0,2}\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // Prefijo de piso/puerta al inicio de un segmento, ej. "4C ", "2º ", "1B "
    private static readonly Regex FloorDoorPrefixRegex = new(@"^\d+\s*[ºª]?\s*[A-Z]{0,2}\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AssetResolverService(
        AssetResolverDbContext db,
        IOptions<FieldAliasesConfig> aliases,
        ILogger<AssetResolverService> logger,
        IOptions<AssetResolverPerformanceOptions>? performanceOptions = null)
    {
        _db = db;
        _aliases = aliases.Value;
        _performance = performanceOptions?.Value ?? new AssetResolverPerformanceOptions();
        _logger = logger;
    }

    public async Task<GetAAIIInfoResponse> BuscarActivosAsync(GetAAIIInfoRequest request, CancellationToken ct = default)
    {
        var response = new GetAAIIInfoResponse { CorrelationId = request.CorrelationId };
        var modoCombinacion = NormalizeModoCombinacion(request.ModoCombinacionCriterios);

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

        // Respetar flags de habilitación: si un criterio está deshabilitado, vaciar aliases
        // y limpiar "indicated" para que la lógica posterior lo ignore completamente.
        if (!request.BusquedaIdufirHabilitada) { aliasesIdufir = []; indicatedIdufir = false; }
        if (!request.BusquedaReferenciaCatastralHabilitada) { aliasesRefCat = []; indicatedRefCat = false; }

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

        var direccionResuelta = request.BusquedaDireccionHabilitada
            ? ResolverDireccion(request)
            : null;
        var direccionTipificadaResuelta = request.BusquedaDireccionTipificadaHabilitada
            ? ResolverDireccionTipificada(request)
            : null;

        if (string.IsNullOrWhiteSpace(idufir)
            && string.IsNullOrWhiteSpace(refCatastral)
            && direccionResuelta is null
            && direccionTipificadaResuelta is null)
        {
            response.Message = "No se encontraron criterios de búsqueda (IDUFIR, ReferenciaCatastral, Dirección o DirecciónTipificada) en los datos extraídos.";
            return response;
        }

        if (!request.AAII_Search && !request.AACC_Search)
        {
            response.Message = "No hay ningún origen habilitado para búsqueda. Activa AAII_Search y/o AACC_Search.";
            return response;
        }

        response.CriteriosUsados = new CriteriosUsados
        {
            Idufir = idufir,
            ReferenciaCatastral = refCatastral,
            ModoCombinacionCriterios = modoCombinacion,
            Direccion = direccionResuelta is null
                ? null
                : new DireccionCriterio
                {
                    DireccionCompleta = direccionResuelta.DireccionCompleta,
                    NombreVia = direccionResuelta.NombreVia,
                    Numero = direccionResuelta.Numero,
                    Municipio = direccionResuelta.Municipio,
                    CodigoPostal = direccionResuelta.CodigoPostal,
                    DireccionNormalizada = direccionResuelta.DireccionNormalizada
                },
            DireccionTipificada = direccionTipificadaResuelta is null
                ? null
                : new DireccionTipificadaCriterio
                {
                    Pais = direccionTipificadaResuelta.Pais,
                    Provincia = direccionTipificadaResuelta.Provincia,
                    ComunidadAutonoma = direccionTipificadaResuelta.ComunidadAutonoma,
                    Municipio = direccionTipificadaResuelta.Municipio,
                    Poblacion = direccionTipificadaResuelta.Poblacion,
                    TipoVia = direccionTipificadaResuelta.TipoVia,
                    Calle = direccionTipificadaResuelta.Calle,
                    Numero = direccionTipificadaResuelta.Numero,
                    Bloque = direccionTipificadaResuelta.Bloque,
                    Puerta = direccionTipificadaResuelta.Puerta,
                    CodigoPostal = direccionTipificadaResuelta.CodigoPostal,
                    Planta = direccionTipificadaResuelta.Planta
                }
        };

        // 2. Resolver campos solicitados por origen; #ALL# expande a todas las columnas de ese origen.
        List<string> erroresAaii = [];
        List<string> erroresAacc = [];

        var camposValidosAaii = request.AAII_Search
            ? ResolveRequestedFields(request.RequestedFields, ValidFieldsByRequestName, AllColumnNames, out erroresAaii)
            : [];

        var camposValidosAacc = request.AACC_Search
            ? ResolveRequestedFields(request.RequestedFields, ValidFieldsByRequestNameAacc, AllColumnNamesAacc, out erroresAacc)
            : [];

        response.CamposConErrorAAII = erroresAaii;
        response.CamposConErrorAACC = erroresAacc;
        response.CamposConError = [.. erroresAaii.Concat(erroresAacc).Distinct(StringComparer.OrdinalIgnoreCase)];

        // 3. Consultar BD por origen habilitado y combinar resultados por criterio en cada origen.
        List<(string Nombre, List<DmPosicionAAII> Resultados)> resultadosAaiiPorCriterio = [];
        List<(string Nombre, List<DmPosicionAACC> Resultados)> resultadosAaccPorCriterio = [];

        if (request.AAII_Search)
        {
            if (!string.IsNullOrWhiteSpace(idufir))
            {
                var aaiiIdufirResults = await ConsultarPorIdufirAsync(idufir, ct);
                resultadosAaiiPorCriterio.Add(("Idufir", aaiiIdufirResults));
            }

            if (!string.IsNullOrWhiteSpace(refCatastral))
            {
                var aaiiRefResults = await ConsultarPorRefCatastralAsync(refCatastral, ct);
                resultadosAaiiPorCriterio.Add(("ReferenciaCatastral", aaiiRefResults));
            }

            if (direccionResuelta is not null)
            {
                var direccionResultado = await BuscarPorDireccionAsync(
                    direccionResuelta.Query,
                    request.UmbralScoreDireccion > 0.0 ? request.UmbralScoreDireccion : 0.75,
                    ct);

                response.CriteriosUsados!.Direccion!.Score = direccionResultado.MejorScore;
                response.CriteriosUsados.Direccion.CandidatosEvaluados = direccionResultado.CandidatosEvaluados;
                response.CriteriosUsados.Direccion.Razon = direccionResultado.Razon;
                resultadosAaiiPorCriterio.Add(("Direccion", direccionResultado.Resultados));
            }

            if (direccionTipificadaResuelta is not null)
            {
                var direccionTipificadaResultado = await BuscarPorDireccionTipificadaAsync(direccionTipificadaResuelta, ct);
                response.CriteriosUsados!.DireccionTipificada!.CandidatosEvaluados = direccionTipificadaResultado.CandidatosEvaluados;
                response.CriteriosUsados.DireccionTipificada.Razon = direccionTipificadaResultado.Razon;
                resultadosAaiiPorCriterio.Add(("DireccionTipificada", direccionTipificadaResultado.Resultados));
            }
        }

        if (request.AACC_Search)
        {
            if (!string.IsNullOrWhiteSpace(idufir))
            {
                var aaccIdufirResults = await ConsultarPorIdufirAaccAsync(idufir, ct);
                resultadosAaccPorCriterio.Add(("Idufir", aaccIdufirResults));
            }

            if (!string.IsNullOrWhiteSpace(refCatastral))
            {
                var aaccRefResults = await ConsultarPorRefCatastralAaccAsync(refCatastral, ct);
                resultadosAaccPorCriterio.Add(("ReferenciaCatastral", aaccRefResults));
            }

            if (direccionResuelta is not null)
            {
                var direccionResultado = await BuscarPorDireccionAaccAsync(
                    direccionResuelta.Query,
                    request.UmbralScoreDireccion > 0.0 ? request.UmbralScoreDireccion : 0.75,
                    ct);

                if (response.CriteriosUsados?.Direccion is not null
                    && direccionResultado.MejorScore > response.CriteriosUsados.Direccion.Score)
                {
                    response.CriteriosUsados.Direccion.Score = direccionResultado.MejorScore;
                    response.CriteriosUsados.Direccion.CandidatosEvaluados = direccionResultado.CandidatosEvaluados;
                    response.CriteriosUsados.Direccion.Razon = direccionResultado.Razon;
                }

                resultadosAaccPorCriterio.Add(("Direccion", direccionResultado.Resultados));
            }

            if (direccionTipificadaResuelta is not null)
            {
                var direccionTipificadaResultado = await BuscarPorDireccionTipificadaAaccAsync(direccionTipificadaResuelta, ct);

                if (response.CriteriosUsados?.DireccionTipificada is not null
                    && direccionTipificadaResultado.CandidatosEvaluados > response.CriteriosUsados.DireccionTipificada.CandidatosEvaluados)
                {
                    response.CriteriosUsados.DireccionTipificada.CandidatosEvaluados = direccionTipificadaResultado.CandidatosEvaluados;
                    response.CriteriosUsados.DireccionTipificada.Razon = direccionTipificadaResultado.Razon;
                }

                resultadosAaccPorCriterio.Add(("DireccionTipificada", direccionTipificadaResultado.Resultados));
            }
        }

        var resultadosAaii = CombinarResultados(resultadosAaiiPorCriterio, modoCombinacion);
        var resultadosAacc = CombinarResultadosAacc(resultadosAaccPorCriterio, modoCombinacion);

        response.ActivosAAII = resultadosAaii.Select(r => BuildActivoEncontrado(r, camposValidosAaii)).ToList();
        response.ActivosAACC = resultadosAacc.Select(r => BuildActivoEncontradoAacc(r, camposValidosAacc)).ToList();
        response.CountAAII = response.ActivosAAII.Count;
        response.CountAACC = response.ActivosAACC.Count;

        response.Activos =
        [
            .. response.ActivosAAII,
            .. response.ActivosAACC
        ];

        response.Count = response.Activos.Count;
        response.Found = response.Count > 0;

        var criterioAaii = BuildCriterioUtilizado(resultadosAaiiPorCriterio, modoCombinacion);
        var criterioAacc = BuildCriterioUtilizadoAacc(resultadosAaccPorCriterio, modoCombinacion);
        response.CriterioUtilizado = string.Join(" | ", new[]
        {
            request.AAII_Search ? $"AAII:{(string.IsNullOrWhiteSpace(criterioAaii) ? "-" : criterioAaii)}" : null,
            request.AACC_Search ? $"AACC:{(string.IsNullOrWhiteSpace(criterioAacc) ? "-" : criterioAacc)}" : null
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        response.Message = response.Count == 0
            ? $"No se encontraron activos con los criterios proporcionados ({response.CriterioUtilizado})."
            : $"Se encontraron {response.Count} activos (AAII={response.CountAAII}, AACC={response.CountAACC}).";

        return response;
    }

    // ── Búsqueda fuzzy por dirección ──────────────────────────────────────────────────────────────

    private DireccionResolvedValues? ResolverDireccion(GetAAIIInfoRequest request)
    {
        var aliasesCompleta = request.MapeoDireccionCompleta is { Count: > 0 }
            ? request.MapeoDireccionCompleta
            : _aliases.DireccionCompleta;
        var aliasesVia = request.MapeoDireccionNombreVia is { Count: > 0 }
            ? request.MapeoDireccionNombreVia
            : _aliases.DireccionNombreVia;
        var aliasesNum = request.MapeoDireccionNumero is { Count: > 0 }
            ? request.MapeoDireccionNumero
            : _aliases.DireccionNumero;
        var aliasesMun = request.MapeoDireccionMunicipio is { Count: > 0 }
            ? request.MapeoDireccionMunicipio
            : _aliases.DireccionMunicipio;
        var aliasesCp = request.MapeoDireccionCodigoPostal is { Count: > 0 }
            ? request.MapeoDireccionCodigoPostal
            : _aliases.DireccionCodigoPostal;

        var rawCompleta = DetectarValor(request.ExtractedData, null, aliasesCompleta);
        var rawVia = DetectarValor(request.ExtractedData, null, aliasesVia);
        var rawNum = DetectarValor(request.ExtractedData, null, aliasesNum);
        var rawMun = DetectarValor(request.ExtractedData, null, aliasesMun);
        var rawCp = DetectarValor(request.ExtractedData, null, aliasesCp);

        if (!string.IsNullOrWhiteSpace(rawCompleta))
        {
            var parsed = ParseDireccionCompleta(rawCompleta);
            rawVia ??= parsed.NombreVia;
            rawNum ??= parsed.Numero;
            rawMun ??= parsed.Municipio;
            rawCp ??= parsed.CodigoPostal;
        }

        var query = new DireccionQuery(
            NombreVia: DireccionNormalizer.NormalizeNombreVia(rawVia),
            Numero: DireccionNormalizer.NormalizeNumero(rawNum),
            Municipio: DireccionNormalizer.NormalizeMunicipio(rawMun),
            CodigoPostal: DireccionNormalizer.NormalizeCodigoPostal(rawCp));

        if (string.IsNullOrEmpty(query.NombreVia)
            && string.IsNullOrEmpty(query.Numero)
            && string.IsNullOrEmpty(query.Municipio)
            && string.IsNullOrEmpty(query.CodigoPostal))
        {
            return null;
        }

        var direccionNormalizada = string.Join(" ", new[] { query.NombreVia, query.Numero, query.Municipio, query.CodigoPostal }
            .Where(s => !string.IsNullOrEmpty(s)));

        return new DireccionResolvedValues(
            rawCompleta,
            rawVia,
            rawNum,
            rawMun,
            rawCp,
            query,
            direccionNormalizada);
    }

    private async Task<DireccionSearchResult> BuscarPorDireccionAsync(
        DireccionQuery query,
        double umbral,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(query.NombreVia)
            && string.IsNullOrEmpty(query.Numero)
            && string.IsNullOrEmpty(query.Municipio)
            && string.IsNullOrEmpty(query.CodigoPostal))
        {
            return new DireccionSearchResult([], 0.0, 0, "Sin datos de dirección resolubles");
        }

        _logger.LogInformation(
            "Búsqueda por dirección — Via='{Via}', Num='{Num}', Municipio='{Municipio}', CP='{CP}'",
            query.NombreVia, query.Numero, query.Municipio, query.CodigoPostal);

        // Pre-filtro eficiente: si hay CP o Municipio normalizado, limitar candidatos en BD
        IQueryable<DmPosicionAAII> baseQuery = _db.DmPosicionAAII.AsNoTracking();

        if (!string.IsNullOrEmpty(query.CodigoPostal))
        {
            baseQuery = baseQuery.Where(x => x.NumCodPostal != null && x.NumCodPostal == query.CodigoPostal);
        }
        else if (!string.IsNullOrEmpty(query.Municipio))
        {
            var municipioPrefix = query.Municipio[..Math.Min(query.Municipio.Length, 6)];
            var municipioLikePattern = $"%{municipioPrefix}%";
            baseQuery = baseQuery.Where(x => x.DesMunicp != null &&
                EF.Functions.Like(x.DesMunicp, municipioLikePattern));
        }

        var candidatos = await LimitQuery(baseQuery, _performance.MaxCandidatesDireccion).ToListAsync(ct);
        _logger.LogInformation("Candidatos pre-filtrados para scoring: {Count}", candidatos.Count);

        var conScore = candidatos
            .Select(c =>
            {
                var cand = new DireccionCandidate(
                    NombreVia:    DireccionNormalizer.NormalizeNombreVia(c.DesNombreVia),
                    Numero:       DireccionNormalizer.NormalizeNumero(c.NumVia),
                    Municipio:    DireccionNormalizer.NormalizeMunicipio(c.DesMunicp),
                    CodigoPostal: DireccionNormalizer.NormalizeCodigoPostal(c.NumCodPostal));
                var score = DireccionNormalizer.ScoreDireccion(query, cand);
                return (Entidad: c, Score: score);
            })
            .Where(x => x.Score >= umbral)
            .OrderByDescending(x => x.Score)
            .ToList();

        var resultados = DeduplicarPorActivo(conScore.Select(x => x.Entidad));
        var mejorScore = conScore.Count == 0 ? 0.0 : conScore[0].Score;
        var razon = conScore.Count == 0
            ? (candidatos.Count == 0
                ? "Sin candidatos tras pre-filtro"
                : $"Ningún candidato superó el umbral de score {umbral:F2}")
            : (resultados.Count == 1
                ? $"1 activo superó el umbral {umbral:F2}"
                : $"{resultados.Count} activos superaron el umbral {umbral:F2}");

        return new DireccionSearchResult(resultados, mejorScore, candidatos.Count, razon);
    }

    private async Task<DireccionSearchResultAacc> BuscarPorDireccionAaccAsync(
        DireccionQuery query,
        double umbral,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(query.NombreVia)
            && string.IsNullOrEmpty(query.Numero)
            && string.IsNullOrEmpty(query.Municipio)
            && string.IsNullOrEmpty(query.CodigoPostal))
        {
            return new DireccionSearchResultAacc([], 0.0, 0, "Sin datos de dirección resolubles");
        }

        IQueryable<DmPosicionAACC> baseQuery = _db.DmPosicionAACC.AsNoTracking();

        if (!string.IsNullOrEmpty(query.CodigoPostal))
        {
            baseQuery = baseQuery.Where(x => x.NumCodPostal != null && x.NumCodPostal == query.CodigoPostal);
        }
        else if (!string.IsNullOrEmpty(query.Municipio))
        {
            var municipioPrefix = query.Municipio[..Math.Min(query.Municipio.Length, 6)];
            var municipioLikePattern = $"%{municipioPrefix}%";
            baseQuery = baseQuery.Where(x => x.DesMunicp != null &&
                EF.Functions.Like(x.DesMunicp, municipioLikePattern));
        }

        var candidatos = await LimitQuery(baseQuery, _performance.MaxCandidatesDireccion).ToListAsync(ct);

        var conScore = candidatos
            .Select(c =>
            {
                var cand = new DireccionCandidate(
                    NombreVia: DireccionNormalizer.NormalizeNombreVia(c.DesNombreVia),
                    Numero: DireccionNormalizer.NormalizeNumero(c.NumVia),
                    Municipio: DireccionNormalizer.NormalizeMunicipio(c.DesMunicp),
                    CodigoPostal: DireccionNormalizer.NormalizeCodigoPostal(c.NumCodPostal));
                var score = DireccionNormalizer.ScoreDireccion(query, cand);
                return (Entidad: c, Score: score);
            })
            .Where(x => x.Score >= umbral)
            .OrderByDescending(x => x.Score)
            .ToList();

        var resultados = DeduplicarPorActivoAacc(conScore.Select(x => x.Entidad));
        var mejorScore = conScore.Count == 0 ? 0.0 : conScore[0].Score;
        var razon = conScore.Count == 0
            ? (candidatos.Count == 0
                ? "Sin candidatos tras pre-filtro"
                : $"Ningún candidato superó el umbral de score {umbral:F2}")
            : (resultados.Count == 1
                ? $"1 activo superó el umbral {umbral:F2}"
                : $"{resultados.Count} activos superaron el umbral {umbral:F2}");

        return new DireccionSearchResultAacc(resultados, mejorScore, candidatos.Count, razon);
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

    private async Task<List<DmPosicionAAII>> ConsultarPorIdufirAsync(string idufir, CancellationToken ct)
    {
        var resultados = await _db.DmPosicionAAII
            .AsNoTracking()
            .Where(x => x.IdIdufir == idufir)
            .OrderByDescending(x => x.FchCierreDt)
            .Take(NormalizeMax(_performance.MaxRowsPerExactSearch, 500))
            .ToListAsync(ct);

        _logger.LogInformation("Búsqueda por IDUFIR={Idufir}: {Count} registros", idufir, resultados.Count);
        return DeduplicarPorActivo(resultados);
    }

    private async Task<List<DmPosicionAAII>> ConsultarPorRefCatastralAsync(string refCatastral, CancellationToken ct)
    {
        var resultados = await _db.DmPosicionAAII
            .AsNoTracking()
            .Where(x => x.IdRefCatast == refCatastral)
            .OrderByDescending(x => x.FchCierreDt)
            .Take(NormalizeMax(_performance.MaxRowsPerExactSearch, 500))
            .ToListAsync(ct);

        _logger.LogInformation("Búsqueda por RefCatastral={RefCat}: {Count} registros", refCatastral, resultados.Count);
        return DeduplicarPorActivo(resultados);
    }

    private async Task<List<DmPosicionAACC>> ConsultarPorIdufirAaccAsync(string idufir, CancellationToken ct)
    {
        var resultados = await _db.DmPosicionAACC
            .AsNoTracking()
            .Where(x => x.IdIdufir == idufir)
            .OrderByDescending(x => x.FchCierreDt)
            .Take(NormalizeMax(_performance.MaxRowsPerExactSearch, 500))
            .ToListAsync(ct);

        _logger.LogInformation("Búsqueda AACC por IDUFIR={Idufir}: {Count} registros", idufir, resultados.Count);
        return DeduplicarPorActivoAacc(resultados);
    }

    private async Task<List<DmPosicionAACC>> ConsultarPorRefCatastralAaccAsync(string refCatastral, CancellationToken ct)
    {
        var resultados = await _db.DmPosicionAACC
            .AsNoTracking()
            .Where(x => x.IdRefCatast == refCatastral)
            .OrderByDescending(x => x.FchCierreDt)
            .Take(NormalizeMax(_performance.MaxRowsPerExactSearch, 500))
            .ToListAsync(ct);

        _logger.LogInformation("Búsqueda AACC por RefCatastral={RefCat}: {Count} registros", refCatastral, resultados.Count);
        return DeduplicarPorActivoAacc(resultados);
    }

    private static List<DmPosicionAAII> CombinarResultados(
        List<(string Nombre, List<DmPosicionAAII> Resultados)> resultadosPorCriterio,
        string modoCombinacion)
    {
        var criteriosNormalizados = resultadosPorCriterio
            .Select(x => (x.Nombre, Resultados: DeduplicarPorActivo(x.Resultados)))
            .ToList();

        if (criteriosNormalizados.Count == 0)
        {
            return [];
        }

        if (string.Equals(modoCombinacion, "AND", StringComparison.OrdinalIgnoreCase)
            && criteriosNormalizados.Any(x => x.Resultados.Count == 0))
        {
            return [];
        }

        var criteriosConResultados = string.Equals(modoCombinacion, "AND", StringComparison.OrdinalIgnoreCase)
            ? criteriosNormalizados
            : criteriosNormalizados.Where(x => x.Resultados.Count > 0).ToList();

        if (criteriosConResultados.Count == 0)
        {
            return [];
        }

        var conjuntosPorCriterio = criteriosConResultados
            .Select(x => x.Resultados.Select(GetAssetKey).ToHashSet(StringComparer.OrdinalIgnoreCase))
            .ToList();

        HashSet<string> idsFinales = new(conjuntosPorCriterio[0], StringComparer.OrdinalIgnoreCase);
        foreach (var conjunto in conjuntosPorCriterio.Skip(1))
        {
            if (string.Equals(modoCombinacion, "AND", StringComparison.OrdinalIgnoreCase))
            {
                idsFinales.IntersectWith(conjunto);
            }
            else
            {
                idsFinales.UnionWith(conjunto);
            }
        }

        return criteriosConResultados
            .SelectMany(x => x.Resultados)
            .Where(x => idsFinales.Contains(GetAssetKey(x)))
            .GroupBy(GetAssetKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.FchCierreDt).First())
            .ToList();
    }

    private static List<DmPosicionAAII> DeduplicarPorActivo(IEnumerable<DmPosicionAAII> resultados)
    {
        return resultados
            .GroupBy(GetAssetKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.FchCierreDt).First())
            .ToList();
    }

    private static List<DmPosicionAACC> CombinarResultadosAacc(
        List<(string Nombre, List<DmPosicionAACC> Resultados)> resultadosPorCriterio,
        string modoCombinacion)
    {
        var criteriosNormalizados = resultadosPorCriterio
            .Select(x => (x.Nombre, Resultados: DeduplicarPorActivoAacc(x.Resultados)))
            .ToList();

        if (criteriosNormalizados.Count == 0)
        {
            return [];
        }

        if (string.Equals(modoCombinacion, "AND", StringComparison.OrdinalIgnoreCase)
            && criteriosNormalizados.Any(x => x.Resultados.Count == 0))
        {
            return [];
        }

        var criteriosConResultados = string.Equals(modoCombinacion, "AND", StringComparison.OrdinalIgnoreCase)
            ? criteriosNormalizados
            : criteriosNormalizados.Where(x => x.Resultados.Count > 0).ToList();

        if (criteriosConResultados.Count == 0)
        {
            return [];
        }

        var conjuntosPorCriterio = criteriosConResultados
            .Select(x => x.Resultados.Select(GetAssetKeyAacc).ToHashSet(StringComparer.OrdinalIgnoreCase))
            .ToList();

        HashSet<string> idsFinales = new(conjuntosPorCriterio[0], StringComparer.OrdinalIgnoreCase);
        foreach (var conjunto in conjuntosPorCriterio.Skip(1))
        {
            if (string.Equals(modoCombinacion, "AND", StringComparison.OrdinalIgnoreCase))
            {
                idsFinales.IntersectWith(conjunto);
            }
            else
            {
                idsFinales.UnionWith(conjunto);
            }
        }

        return criteriosConResultados
            .SelectMany(x => x.Resultados)
            .Where(x => idsFinales.Contains(GetAssetKeyAacc(x)))
            .GroupBy(GetAssetKeyAacc, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.FchCierreDt).First())
            .ToList();
    }

    private static List<DmPosicionAACC> DeduplicarPorActivoAacc(IEnumerable<DmPosicionAACC> resultados)
    {
        return resultados
            .GroupBy(GetAssetKeyAacc, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.FchCierreDt).First())
            .ToList();
    }

    private static string BuildCriterioUtilizado(
        List<(string Nombre, List<DmPosicionAAII> Resultados)> resultadosPorCriterio,
        string modoCombinacion)
    {
        var nombres = resultadosPorCriterio
            .Select(x => x.Nombre)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return nombres.Count switch
        {
            0 => string.Empty,
            1 => nombres[0],
            _ => string.Join($" {modoCombinacion} ", nombres)
        };
    }

    private static string BuildCriterioUtilizadoAacc(
        List<(string Nombre, List<DmPosicionAACC> Resultados)> resultadosPorCriterio,
        string modoCombinacion)
    {
        var nombres = resultadosPorCriterio
            .Select(x => x.Nombre)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return nombres.Count switch
        {
            0 => string.Empty,
            1 => nombres[0],
            _ => string.Join($" {modoCombinacion} ", nombres)
        };
    }

    private static string NormalizeModoCombinacion(string? modo)
        => string.Equals(modo, "AND", StringComparison.OrdinalIgnoreCase) ? "AND" : "OR";

    private static string GetAssetKey(DmPosicionAAII entity)
        => entity.IdActivoSareb.ToString("F0");

    private static string GetAssetKeyAacc(DmPosicionAACC entity)
        => entity.IdActivoSareb.ToString("F0");

    private static DireccionParseResult ParseDireccionCompleta(string? direccionCompleta)
    {
        if (string.IsNullOrWhiteSpace(direccionCompleta))
        {
            return new DireccionParseResult(null, null, null, null);
        }

        var raw = direccionCompleta.Trim();
        string? codigoPostal = null;
        var cpMatch = CodigoPostalRegex.Match(raw);
        if (cpMatch.Success)
        {
            codigoPostal = cpMatch.Value;
            raw = CodigoPostalRegex.Replace(raw, string.Empty).Trim(' ', ',');
        }

        var segmentos = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tramoVia = segmentos.Length > 0 ? segmentos[0] : raw;

        // Con 3+ segmentos el último suele ser la provincia, no el municipio.
        // El penúltimo es el municipio (puede llevar prefijo de piso/puerta como "4C ").
        string? municipio = null;
        if (segmentos.Length >= 3)
        {
            var candidatoMunicipio = FloorDoorPrefixRegex.Replace(segmentos[^2], string.Empty).Trim();
            municipio = string.IsNullOrWhiteSpace(candidatoMunicipio) ? segmentos[^1] : candidatoMunicipio;
        }
        else if (segmentos.Length == 2)
        {
            municipio = segmentos[^1];
        }

        string? numero = null;
        string? nombreVia = tramoVia;
        var numeroMatch = NumeroDireccionRegex.Match(tramoVia);
        if (numeroMatch.Success)
        {
            numero = numeroMatch.Value;
            nombreVia = tramoVia[..numeroMatch.Index].Trim(' ', ',');
        }

        return new DireccionParseResult(nombreVia, numero, municipio, codigoPostal);
    }

    private DireccionTipificadaResolvedValues? ResolverDireccionTipificada(GetAAIIInfoRequest request)
    {
        var input = request.DireccionTipificada;
        if (input is null)
        {
            return null;
        }

        var pais = input.Pais;
        var provincia = input.Provincia;
        var comunidadAutonoma = input.ComunidadAutonoma;
        var municipio = input.Municipio;
        var poblacion = input.Poblacion;
        var tipoVia = input.TipoVia;
        var calle = input.Calle;
        var numero = input.Numero;
        var bloque = input.Bloque;
        var puerta = input.Puerta;
        var codigoPostal = input.CodigoPostal;
        var planta = input.Planta;

        if (string.IsNullOrWhiteSpace(pais)
            && string.IsNullOrWhiteSpace(provincia)
            && string.IsNullOrWhiteSpace(comunidadAutonoma)
            && string.IsNullOrWhiteSpace(municipio)
            && string.IsNullOrWhiteSpace(poblacion)
            && string.IsNullOrWhiteSpace(tipoVia)
            && string.IsNullOrWhiteSpace(calle)
            && string.IsNullOrWhiteSpace(numero)
            && string.IsNullOrWhiteSpace(bloque)
            && string.IsNullOrWhiteSpace(puerta)
            && string.IsNullOrWhiteSpace(codigoPostal)
            && string.IsNullOrWhiteSpace(planta))
        {
            return null;
        }

        return new DireccionTipificadaResolvedValues(
            Pais: NormalizeTextForFilter(pais),
            Provincia: NormalizeTextForFilter(provincia),
            ComunidadAutonoma: NormalizeTextForFilter(comunidadAutonoma),
            Municipio: NormalizeTextForFilter(municipio),
            Poblacion: NormalizeTextForFilter(poblacion),
            TipoVia: NormalizeTextForFilter(tipoVia),
            Calle: NormalizeTextForFilter(calle),
            Numero: DireccionNormalizer.NormalizeNumero(numero),
            Bloque: NormalizeTextForFilter(bloque),
            Puerta: NormalizeTextForFilter(puerta),
            CodigoPostal: DireccionNormalizer.NormalizeCodigoPostal(codigoPostal),
            Planta: NormalizeTextForFilter(planta));
    }

    private async Task<DireccionTipificadaSearchResult> BuscarPorDireccionTipificadaAsync(
        DireccionTipificadaResolvedValues direccion,
        CancellationToken ct)
    {
        IQueryable<DmPosicionAAII> query = _db.DmPosicionAAII.AsNoTracking();

        // Los campos de texto usan LIKE con wildcards (%valor%) para búsqueda por subcadena,
        // lo que permite insensibilidad a mayúsculas/tildes y variaciones de formato en la BD.
        if (!string.IsNullOrEmpty(direccion.Pais))
        { var p = $"%{direccion.Pais}%"; query = query.Where(x => x.DesPais != null && EF.Functions.Like(x.DesPais, p)); }
        if (!string.IsNullOrEmpty(direccion.Provincia))
        { var p = $"%{direccion.Provincia}%"; query = query.Where(x => x.DesProvnc != null && EF.Functions.Like(x.DesProvnc, p)); }
        if (!string.IsNullOrEmpty(direccion.ComunidadAutonoma))
        { var p = $"%{direccion.ComunidadAutonoma}%"; query = query.Where(x => x.DesComuniAuto != null && EF.Functions.Like(x.DesComuniAuto, p)); }
        if (!string.IsNullOrEmpty(direccion.Municipio))
        { var p = $"%{direccion.Municipio}%"; query = query.Where(x => x.DesMunicp != null && EF.Functions.Like(x.DesMunicp, p)); }
        if (!string.IsNullOrEmpty(direccion.Poblacion))
        { var p = $"%{direccion.Poblacion}%"; query = query.Where(x => x.DesPoblcn != null && EF.Functions.Like(x.DesPoblcn, p)); }
        if (!string.IsNullOrEmpty(direccion.TipoVia))
        { var p = $"%{direccion.TipoVia}%"; query = query.Where(x => x.DesTipoVia != null && EF.Functions.Like(x.DesTipoVia, p)); }
        if (!string.IsNullOrEmpty(direccion.Calle))
        { var p = $"%{direccion.Calle}%"; query = query.Where(x => x.DesNombreVia != null && EF.Functions.Like(x.DesNombreVia, p)); }
        if (!string.IsNullOrEmpty(direccion.Numero))
            query = query.Where(x => x.NumVia != null && x.NumVia == direccion.Numero);
        if (!string.IsNullOrEmpty(direccion.Bloque))
        { var p = $"%{direccion.Bloque}%"; query = query.Where(x => x.DesBloque != null && EF.Functions.Like(x.DesBloque, p)); }
        if (!string.IsNullOrEmpty(direccion.Puerta))
        { var p = $"%{direccion.Puerta}%"; query = query.Where(x => x.DesPuerta != null && EF.Functions.Like(x.DesPuerta, p)); }
        if (!string.IsNullOrEmpty(direccion.CodigoPostal))
            query = query.Where(x => x.NumCodPostal != null && x.NumCodPostal == direccion.CodigoPostal);
        if (!string.IsNullOrEmpty(direccion.Planta))
        { var p = $"%{direccion.Planta}%"; query = query.Where(x => x.DesPlanta != null && EF.Functions.Like(x.DesPlanta, p)); }

        var resultados = await LimitQuery(query, _performance.MaxCandidatesDireccionTipificada).ToListAsync(ct);
        var deduplicados = DeduplicarPorActivo(resultados);
        var razon = deduplicados.Count == 0
            ? "Sin resultados para los filtros tipificados indicados"
            : $"{deduplicados.Count} activos encontrados con filtros tipificados";

        return new DireccionTipificadaSearchResult(deduplicados, resultados.Count, razon);
    }

    private async Task<DireccionTipificadaSearchResultAacc> BuscarPorDireccionTipificadaAaccAsync(
        DireccionTipificadaResolvedValues direccion,
        CancellationToken ct)
    {
        IQueryable<DmPosicionAACC> query = _db.DmPosicionAACC.AsNoTracking();

        if (!string.IsNullOrEmpty(direccion.Pais))
        { var p = $"%{direccion.Pais}%"; query = query.Where(x => x.DesPais != null && EF.Functions.Like(x.DesPais, p)); }
        if (!string.IsNullOrEmpty(direccion.Provincia))
        { var p = $"%{direccion.Provincia}%"; query = query.Where(x => x.DesProvnc != null && EF.Functions.Like(x.DesProvnc, p)); }
        if (!string.IsNullOrEmpty(direccion.ComunidadAutonoma))
        { var p = $"%{direccion.ComunidadAutonoma}%"; query = query.Where(x => x.DesComuniAuto != null && EF.Functions.Like(x.DesComuniAuto, p)); }
        if (!string.IsNullOrEmpty(direccion.Municipio))
        { var p = $"%{direccion.Municipio}%"; query = query.Where(x => x.DesMunicp != null && EF.Functions.Like(x.DesMunicp, p)); }
        if (!string.IsNullOrEmpty(direccion.Poblacion))
        { var p = $"%{direccion.Poblacion}%"; query = query.Where(x => x.DesPoblcn != null && EF.Functions.Like(x.DesPoblcn, p)); }
        if (!string.IsNullOrEmpty(direccion.TipoVia))
        { var p = $"%{direccion.TipoVia}%"; query = query.Where(x => x.DesTipoVia != null && EF.Functions.Like(x.DesTipoVia, p)); }
        if (!string.IsNullOrEmpty(direccion.Calle))
        { var p = $"%{direccion.Calle}%"; query = query.Where(x => x.DesNombreVia != null && EF.Functions.Like(x.DesNombreVia, p)); }
        if (!string.IsNullOrEmpty(direccion.Numero))
            query = query.Where(x => x.NumVia != null && x.NumVia == direccion.Numero);
        if (!string.IsNullOrEmpty(direccion.Bloque))
        { var p = $"%{direccion.Bloque}%"; query = query.Where(x => x.DesBloque != null && EF.Functions.Like(x.DesBloque, p)); }
        if (!string.IsNullOrEmpty(direccion.Puerta))
        { var p = $"%{direccion.Puerta}%"; query = query.Where(x => x.DesPuerta != null && EF.Functions.Like(x.DesPuerta, p)); }
        if (!string.IsNullOrEmpty(direccion.CodigoPostal))
            query = query.Where(x => x.NumCodPostal != null && x.NumCodPostal == direccion.CodigoPostal);
        if (!string.IsNullOrEmpty(direccion.Planta))
        { var p = $"%{direccion.Planta}%"; query = query.Where(x => x.DesPlanta != null && EF.Functions.Like(x.DesPlanta, p)); }

        var resultados = await LimitQuery(query, _performance.MaxCandidatesDireccionTipificada).ToListAsync(ct);
        var deduplicados = DeduplicarPorActivoAacc(resultados);
        var razon = deduplicados.Count == 0
            ? "Sin resultados para los filtros tipificados indicados"
            : $"{deduplicados.Count} activos encontrados con filtros tipificados";

        return new DireccionTipificadaSearchResultAacc(deduplicados, resultados.Count, razon);
    }

    private static string? NormalizeTextForFilter(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return Regex.Replace(input.Trim(), "\\s+", " ");
    }

    private static int NormalizeMax(int value, int fallback)
        => value > 0 ? value : fallback;

    private static IQueryable<T> LimitQuery<T>(IQueryable<T> query, int maxRows)
    {
        var normalizedMax = NormalizeMax(maxRows, 2000);
        return query.Take(normalizedMax);
    }

    private static Dictionary<string, FieldProjection> BuildValidFieldsByRequestName()
        => BuildValidFieldsByRequestName(typeof(DmPosicionAAII));

    private static Dictionary<string, FieldProjection> BuildValidFieldsByRequestName(Type entityType)
    {
        var fields = new Dictionary<string, FieldProjection>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
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

    private static List<string> ResolveRequestedFields(
        List<string>? requestedFields,
        Dictionary<string, FieldProjection> validFieldsByRequestName,
        List<string> allColumnNames,
        out List<string> camposConError)
    {
        camposConError = new List<string>();

        if (requestedFields == null || requestedFields.Count == 0)
        {
            return [];
        }

        if (requestedFields.Any(field => string.Equals(field?.Trim(), AllFieldsToken, StringComparison.OrdinalIgnoreCase)))
        {
            return [.. allColumnNames];
        }

        var camposValidos = new List<string>();

        foreach (var requestedField in requestedFields)
        {
            var normalized = requestedField?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (validFieldsByRequestName.TryGetValue(normalized, out var fieldProjection))
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

    private static ActivoEncontrado BuildActivoEncontradoAacc(DmPosicionAACC entity, List<string> camposValidos)
    {
        var activo = new ActivoEncontrado
        {
            IdActivo = entity.IdActivoSareb.ToString("F0"),
            FchCierre = entity.FchCierre
        };

        var campos = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var campo in camposValidos)
        {
            if (ValidFieldsByColumnNameAacc.TryGetValue(campo, out var fieldProjection))
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
        public string ModoCombinacionCriterios { get; set; } = "OR";
        /// <summary>Detalle del criterio de dirección utilizado (solo presente si se usó búsqueda fuzzy).</summary>
        public DireccionCriterio? Direccion { get; set; }
        /// <summary>Detalle del criterio de dirección tipificada (solo presente si se usó).</summary>
        public DireccionTipificadaCriterio? DireccionTipificada { get; set; }
    }

    public class DireccionCriterio
    {
        public string? DireccionCompleta { get; set; }
        public string? NombreVia { get; set; }
        public string? Numero { get; set; }
        public string? Municipio { get; set; }
        public string? CodigoPostal { get; set; }
        /// <summary>Dirección concatenada normalizada que se usó en la búsqueda.</summary>
        public string? DireccionNormalizada { get; set; }
        /// <summary>Score de similitud del candidato seleccionado (0.0–1.0).</summary>
        public double Score { get; set; }
        /// <summary>Número de candidatos evaluados antes de la selección.</summary>
        public int CandidatosEvaluados { get; set; }
        /// <summary>Razón del resultado (seleccionado/ambiguo/score bajo/sin datos).</summary>
        public string? Razon { get; set; }
    }

    public class DireccionTipificadaCriterio
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

    public class ActivoEncontrado
    {
        public string IdActivo { get; set; } = string.Empty;
        public DateTime? FchCierre { get; set; }
        public Dictionary<string, object?> CamposSolicitados { get; set; } = new();
    }
}
