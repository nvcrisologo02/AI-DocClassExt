using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DocumentIA.AssetResolver.Services;

/// <summary>
/// Normaliza componentes de dirección española para comparación fuzzy.
/// Aplica: minúsculas, eliminación de diacríticos, puntuación y expansión de abreviaturas de tipo de vía.
/// </summary>
public static class DireccionNormalizer
{
    private static readonly Dictionary<string, string> ViaAbreviaciones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CL"]   = "CALLE",
        ["C/"]   = "CALLE",
        ["AV"]   = "AVENIDA",
        ["AVD"]  = "AVENIDA",
        ["AVDA"] = "AVENIDA",
        ["PZ"]   = "PLAZA",
        ["PL"]   = "PLAZA",
        ["PLZ"]  = "PLAZA",
        ["PS"]   = "PASEO",
        ["PO"]   = "PASEO",
        ["PJ"]   = "PASAJE",
        ["BV"]   = "BULEVAR",
        ["BLVD"] = "BULEVAR",
        ["CR"]   = "CARRETERA",
        ["CTRA"] = "CARRETERA",
        ["CRA"]  = "CARRETERA",
        ["UR"]   = "URBANIZACION",
        ["URB"]  = "URBANIZACION",
        ["RB"]   = "RAMBLA",
        ["RBLA"] = "RAMBLA",
        ["GL"]   = "GLORIETA",
        ["PG"]   = "POLIGONO",
        ["CM"]   = "CAMINO",
        ["TR"]   = "TRAVESIA",
        ["TRAV"] = "TRAVESIA",
        ["RD"]   = "RONDA",
        ["PQ"]   = "PARQUE",
        ["AC"]   = "ACCESO",
    };

    private static readonly Regex WhitespaceRegex  = new(@"\s+",        RegexOptions.Compiled);
    private static readonly Regex PunctuationRegex = new(@"[^\w\s]",    RegexOptions.Compiled);
    private static readonly Regex NumeroRegex      = new(@"^(\d+)",     RegexOptions.Compiled);
    private static readonly Regex ArticuloRegex    = new(@"^(el|la|los|las|l'|els|les|lo)\s+", RegexOptions.Compiled);

    /// <summary>
    /// Normaliza un nombre de vía: minúsculas, sin diacríticos, sin puntuación,
    /// abreviatura del tipo de vía expandida, espacios normalizados.
    /// </summary>
    public static string NormalizeNombreVia(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Uppercase para matching de abreviaturas
        var upper = PunctuationRegex.Replace(input.Trim().ToUpperInvariant(), " ");
        upper = WhitespaceRegex.Replace(upper, " ").Trim();

        // Expandir abreviatura del tipo de vía (primera palabra)
        var tokens = upper.Split(' ');
        if (tokens.Length > 0 && ViaAbreviaciones.TryGetValue(tokens[0], out var expansion))
        {
            tokens[0] = expansion;
            upper = string.Join(" ", tokens);
        }

        return RemoveDiacritics(upper).ToLowerInvariant();
    }

    /// <summary>
    /// Normaliza un número de vía: extrae solo la parte numérica inicial.
    /// </summary>
    public static string NormalizeNumero(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var m = NumeroRegex.Match(input.Trim());
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    /// <summary>
    /// Normaliza un municipio: minúsculas, sin diacríticos, sin artículos iniciales comunes.
    /// </summary>
    public static string NormalizeMunicipio(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var normalized = RemoveDiacritics(input.Trim().ToLowerInvariant());
        normalized = PunctuationRegex.Replace(normalized, " ");
        normalized = WhitespaceRegex.Replace(normalized, " ").Trim();
        // Eliminar artículos iniciales comunes
        normalized = ArticuloRegex.Replace(normalized, string.Empty);
        return normalized;
    }

    /// <summary>
    /// Normaliza un código postal: solo dígitos, sin espacios.
    /// </summary>
    public static string NormalizeCodigoPostal(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return Regex.Replace(input.Trim(), @"\D", string.Empty);
    }

    /// <summary>
    /// Similitud de Jaccard entre conjuntos de tokens de dos cadenas ya normalizadas.
    /// Devuelve valor entre 0.0 y 1.0.
    /// </summary>
    public static double JaccardSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var setA = new HashSet<string>(a.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var setB = new HashSet<string>(b.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();
        return union == 0 ? 1.0 : (double)intersection / union;
    }

    /// <summary>
    /// Calcula un score ponderado de similitud de dirección entre la query y un candidato.
    /// Pesos: NombreVia=0.40, Numero=0.30, Municipio=0.20, CodigoPostal=0.10.
    /// Campos ausentes en la query no penalizan (se redistribuye el peso a los presentes).
    /// </summary>
    public static double ScoreDireccion(DireccionQuery query, DireccionCandidate candidate)
    {
        double totalPeso = 0.0;
        double scoreAcumulado = 0.0;

        void AgregarComponente(string queryVal, string candidateVal, double peso)
        {
            if (string.IsNullOrEmpty(queryVal)) return;  // campo ausente → no penaliza
            totalPeso += peso;

            if (string.IsNullOrEmpty(candidateVal))
            {
                // candidato sin dato → contribución 0
                return;
            }

            double sim = queryVal == candidateVal
                ? 1.0
                : JaccardSimilarity(queryVal, candidateVal);
            scoreAcumulado += sim * peso;
        }

        AgregarComponente(query.NombreVia,    candidate.NombreVia,    0.40);
        AgregarComponente(query.Numero,       candidate.Numero,       0.30);
        AgregarComponente(query.Municipio,    candidate.Municipio,    0.20);
        AgregarComponente(query.CodigoPostal, candidate.CodigoPostal, 0.10);

        return totalPeso > 0.0 ? scoreAcumulado / totalPeso : 0.0;
    }

    private static string RemoveDiacritics(string text)
    {
        var formD = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

/// <summary>
/// Componentes normalizados de la dirección de búsqueda.
/// </summary>
public record DireccionQuery(
    string NombreVia,
    string Numero,
    string Municipio,
    string CodigoPostal);

/// <summary>
/// Componentes normalizados de la dirección de un candidato en BD.
/// </summary>
public record DireccionCandidate(
    string NombreVia,
    string Numero,
    string Municipio,
    string CodigoPostal);
