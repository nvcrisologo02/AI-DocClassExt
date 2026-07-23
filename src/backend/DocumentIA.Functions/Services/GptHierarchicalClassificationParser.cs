using System.Text.Json;

namespace DocumentIA.Functions.Services;

public static class GptHierarchicalClassificationParser
{
    public const string Phase1ParsingErrorReason = "fase1_parsing_error";
    public const string Phase2ParsingErrorReason = "fase2_parsing_error";

    /// <summary>
    /// Motivo informado cuando el TDN1 no vino explícito en el JSON de Phase 1 ni fue
    /// extraíble por el prefijo convencional "CODIGO: ..." (<see cref="ExtraerTdn1DePropuesta"/>),
    /// pero sí se pudo resolver mapeando el texto libre de "propuesta" contra el catálogo TDN1
    /// (código o nombre de familia mencionado literalmente). Distingue esta resolución tolerante
    /// de un "Desconocido" legítimo (documento no clasificable, p.ej. ilegible) en las trazas y en
    /// el contrato de salida (AB#99984).
    /// </summary>
    public const string PropuestaCatalogMappingReason = "tdn1_resuelto_por_mapeo_propuesta";

    public static GptHierarchicalParsingResult<GptPhase1Classification> ParsePhase1(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return GptHierarchicalParsingResult<GptPhase1Classification>.Fail(
                Phase1ParsingErrorReason,
                "La respuesta de fase 1 está vacía.");
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(responseText);
            var root = jsonDocument.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return GptHierarchicalParsingResult<GptPhase1Classification>.Fail(
                    Phase1ParsingErrorReason,
                    "La respuesta de fase 1 debe ser un objeto JSON.");
            }

            if (!root.TryGetProperty("propuesta", out var propuestaElement) || propuestaElement.ValueKind != JsonValueKind.String)
            {
                return GptHierarchicalParsingResult<GptPhase1Classification>.Fail(
                    Phase1ParsingErrorReason,
                    "La respuesta de fase 1 debe incluir 'propuesta' como string.");
            }

            if (!root.TryGetProperty("tdn1", out var tdn1Element) ||
                (tdn1Element.ValueKind != JsonValueKind.String && tdn1Element.ValueKind != JsonValueKind.Null))
            {
                return GptHierarchicalParsingResult<GptPhase1Classification>.Fail(
                    Phase1ParsingErrorReason,
                    "La respuesta de fase 1 debe incluir 'tdn1' como string o null.");
            }

            var propuesta = propuestaElement.GetString() ?? string.Empty;
            var tdn1 = tdn1Element.ValueKind == JsonValueKind.String
                ? NormalizeCodeOrNull(tdn1Element.GetString())
                : null;

            string? resumen = null;
            if (root.TryGetProperty("resumen", out var resumenElement) &&
                resumenElement.ValueKind == JsonValueKind.String)
            {
                resumen = resumenElement.GetString();
            }

            double? confianza = null;
            if (root.TryGetProperty("confianza", out var confianzaElement) &&
                confianzaElement.ValueKind == JsonValueKind.Number)
            {
                var rawValue = confianzaElement.GetDouble();
                confianza = Math.Clamp(rawValue, 0.0, 1.0);
            }

            return GptHierarchicalParsingResult<GptPhase1Classification>.Ok(new GptPhase1Classification(tdn1, propuesta, resumen, confianza));
        }
        catch (JsonException ex)
        {
            return GptHierarchicalParsingResult<GptPhase1Classification>.Fail(
                Phase1ParsingErrorReason,
                $"JSON inválido en fase 1: {ex.Message}");
        }
    }

    public static GptHierarchicalParsingResult<GptPhase2Classification> ParsePhase2(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return GptHierarchicalParsingResult<GptPhase2Classification>.Fail(
                Phase2ParsingErrorReason,
                "La respuesta de fase 2 está vacía.");
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(responseText);
            var root = jsonDocument.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return GptHierarchicalParsingResult<GptPhase2Classification>.Fail(
                    Phase2ParsingErrorReason,
                    "La respuesta de fase 2 debe ser un objeto JSON.");
            }

            if (!root.TryGetProperty("tdn2", out var tdn2Element) || tdn2Element.ValueKind != JsonValueKind.String)
            {
                return GptHierarchicalParsingResult<GptPhase2Classification>.Fail(
                    Phase2ParsingErrorReason,
                    "La respuesta de fase 2 debe incluir 'tdn2' como string.");
            }

            var tdn2 = NormalizeCodeOrNull(tdn2Element.GetString());
            if (string.IsNullOrWhiteSpace(tdn2))
            {
                return GptHierarchicalParsingResult<GptPhase2Classification>.Fail(
                    Phase2ParsingErrorReason,
                    "La respuesta de fase 2 contiene un 'tdn2' vacío.");
            }

            string? resultadoPrompt = null;
            if (root.TryGetProperty("resultado_prompt", out var promptElement) &&
                promptElement.ValueKind == JsonValueKind.String)
            {
                resultadoPrompt = promptElement.GetString();
            }

            string? resumen = null;
            if (root.TryGetProperty("resumen", out var resumenElement) &&
                resumenElement.ValueKind == JsonValueKind.String)
            {
                resumen = resumenElement.GetString();
            }

            double? confianza = null;
            if (root.TryGetProperty("confianza", out var confianzaElement) &&
                confianzaElement.ValueKind == JsonValueKind.Number)
            {
                var rawValue = confianzaElement.GetDouble();
                confianza = Math.Clamp(rawValue, 0.0, 1.0);
            }

            return GptHierarchicalParsingResult<GptPhase2Classification>.Ok(new GptPhase2Classification(tdn2, resultadoPrompt, resumen, confianza));
        }
        catch (JsonException ex)
        {
            return GptHierarchicalParsingResult<GptPhase2Classification>.Fail(
                Phase2ParsingErrorReason,
                $"JSON inválido en fase 2: {ex.Message}");
        }
    }

    private static string? NormalizeCodeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Intenta extraer un código TDN1 del inicio de una propuesta.
    /// Busca patrones como "COMU-01: descripción" o "COMU: descripción" y extrae "COMU".
    /// </summary>
    /// <param name="propuesta">La propuesta de clasificación de GPT</param>
    /// <returns>El código TDN1 si se encuentra, null en caso contrario</returns>
    public static string? ExtraerTdn1DePropuesta(string? propuesta)
    {
        if (string.IsNullOrWhiteSpace(propuesta))
        {
            return null;
        }

        // Buscar patrón: XXXX-NN: o XXXX:
        // Ejemplos: "COMU-01: descripción", "COMU: descripción"
        var match = System.Text.RegularExpressions.Regex.Match(
            propuesta.Trim(),
            @"^([A-Z]{4})(?:-\d+)?[\s:]+",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(100));

        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }

        return null;
    }

    /// <summary>
    /// Parsea el catálogo TDN1 en el formato "- CODIGO: Nombre, Descripcion" (una familia por
    /// línea, tal como lo genera <c>ClassificationTipologiaPromptBuilder.BuildTdn1Catalog</c>) y
    /// devuelve el diccionario Codigo -&gt; Nombre. Reutiliza el mismo catálogo que ya se le muestra
    /// a GPT en el prompt de Phase 1, sin necesidad de una consulta adicional a BD.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseTdn1CatalogNombresPorCodigo(string? catalogoTdn1)
    {
        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(catalogoTdn1))
        {
            return mapa;
        }

        foreach (var lineaCruda in catalogoTdn1.Split('\n'))
        {
            var linea = lineaCruda.Trim().TrimStart('-', ' ');
            var separadorCodigo = linea.IndexOf(':');
            if (separadorCodigo <= 0)
            {
                continue;
            }

            var codigo = linea[..separadorCodigo].Trim();
            if (codigo.Length == 0 || mapa.ContainsKey(codigo))
            {
                continue;
            }

            var resto = linea[(separadorCodigo + 1)..].Trim();
            var separadorNombre = resto.IndexOf(',');
            var nombre = (separadorNombre > 0 ? resto[..separadorNombre] : resto).Trim();

            if (nombre.Length > 0)
            {
                mapa[codigo] = nombre;
            }
        }

        return mapa;
    }

    /// <summary>
    /// Resolución tolerante de TDN1 a partir del texto libre de "propuesta" cuando GPT no
    /// devolvió un código explícito en 'tdn1' ni siguió la convención "CODIGO: descripción" al
    /// inicio del texto (ver <see cref="ExtraerTdn1DePropuesta"/>). El prompt de Phase 1 solo pide
    /// a GPT "texto libre" en 'propuesta', por lo que esa convención NO está garantizada y es
    /// habitual que GPT identifique correctamente la familia documental en prosa sin anteponer su
    /// código de catálogo (AB#99984).
    /// <para>
    /// Busca, en este orden:
    ///  1) Un código de catálogo (4 letras) mencionado como palabra completa en mayúsculas, en
    ///     cualquier posición del texto (no solo al inicio).
    ///  2) El nombre de una familia del catálogo citado literalmente en el texto libre (solo
    ///     nombres suficientemente distintivos, para minimizar falsos positivos).
    /// </para>
    /// Debe invocarse únicamente cuando las vías anteriores ya fallaron: así solo actúa en el
    /// camino que hoy degrada a "Desconocido", sin alterar el comportamiento de los "Desconocido"
    /// legítimos (documentos sin propuesta útil, p.ej. ilegibles o vacíos).
    /// </summary>
    public static string? ResolverTdn1PorCatalogoDesdePropuesta(
        string? propuesta,
        IReadOnlyDictionary<string, string> nombresPorCodigo)
    {
        if (string.IsNullOrWhiteSpace(propuesta) || nombresPorCodigo is null || nombresPorCodigo.Count == 0)
        {
            return null;
        }

        var texto = propuesta.Trim();

        // 1) Código de catálogo como palabra completa en mayúsculas, en cualquier posición.
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
            texto,
            @"\b[A-Z]{4}\b",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(100)))
        {
            if (nombresPorCodigo.ContainsKey(match.Value))
            {
                return match.Value.ToUpperInvariant();
            }
        }

        // 2) Nombre de familia mencionado en el texto libre. Umbral mínimo de longitud para
        // evitar falsos positivos con nombres cortos o genéricos que pudieran aparecer por
        // casualidad en la prosa (p.ej. nombres de una sola palabra corta).
        const int longitudMinimaNombre = 10;
        foreach (var entrada in nombresPorCodigo)
        {
            if (entrada.Value.Length >= longitudMinimaNombre &&
                texto.Contains(entrada.Value, StringComparison.OrdinalIgnoreCase))
            {
                return entrada.Key.ToUpperInvariant();
            }
        }

        return null;
    }
}

public sealed record GptPhase1Classification(string? Tdn1, string Propuesta, string? Resumen = null, double? Confianza = null);

public sealed record GptPhase2Classification(string Tdn2, string? ResultadoPrompt = null, string? Resumen = null, double? Confianza = null);

public sealed record GptHierarchicalParsingResult<T>(bool Success, T? Value, string? ErrorReason, string? ErrorMessage)
{
    public static GptHierarchicalParsingResult<T> Ok(T value) => new(true, value, null, null);

    public static GptHierarchicalParsingResult<T> Fail(string reason, string message) => new(false, default, reason, message);
}