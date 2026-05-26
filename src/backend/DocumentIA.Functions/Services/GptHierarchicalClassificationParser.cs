using System.Text.Json;

namespace DocumentIA.Functions.Services;

public static class GptHierarchicalClassificationParser
{
    public const string Phase1ParsingErrorReason = "fase1_parsing_error";
    public const string Phase2ParsingErrorReason = "fase2_parsing_error";

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

            return GptHierarchicalParsingResult<GptPhase1Classification>.Ok(new GptPhase1Classification(tdn1, propuesta));
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

            return GptHierarchicalParsingResult<GptPhase2Classification>.Ok(new GptPhase2Classification(tdn2, resultadoPrompt, resumen));
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
}

public sealed record GptPhase1Classification(string? Tdn1, string Propuesta);

public sealed record GptPhase2Classification(string Tdn2, string? ResultadoPrompt = null, string? Resumen = null);

public sealed record GptHierarchicalParsingResult<T>(bool Success, T? Value, string? ErrorReason, string? ErrorMessage)
{
    public static GptHierarchicalParsingResult<T> Ok(T value) => new(true, value, null, null);

    public static GptHierarchicalParsingResult<T> Fail(string reason, string message) => new(false, default, reason, message);
}