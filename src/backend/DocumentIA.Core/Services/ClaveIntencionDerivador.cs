// src/backend/DocumentIA.Core/Services/ClaveIntencionDerivador.cs
using System.Text.Json;
using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services;

/// <summary>
/// Deriva la clave de intencion de un contrato persistido antes del cambio (AB#100930). Lee
/// con busqueda de propiedad sin distinguir mayusculas porque el historico mezcla PascalCase
/// (System.Text.Json por defecto) y camelCase en algunos contratos.
/// </summary>
public static class ClaveIntencionDerivador
{
    public static ClaveIntencion? DesdeContrato(string? contratoJson, string nivelDefault)
    {
        if (string.IsNullOrWhiteSpace(contratoJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(contratoJson);
            var raiz = doc.RootElement;

            var estado = Texto(Buscar(raiz, "Resultado", "Estado"));
            if (string.Equals(estado, "DUPLICADO", StringComparison.OrdinalIgnoreCase))
            {
                // Fila del camino "duplicado sin historica" (AB#100178): contrato sin contenido.
                return null;
            }

            var detalle = Buscar(raiz, "DetalleEjecucion");
            var clasificacion = Buscar(detalle, "Clasificacion");
            var modelo = Texto(Buscar(clasificacion, "Modelo"));
            var expectedType = string.Equals(modelo, ClaveIntencionBuilder.ModeloExpectedType, StringComparison.OrdinalIgnoreCase)
                ? Texto(Buscar(raiz, "Identificacion", "Tipologia"))
                : null;

            var componentes = ClaveIntencionBuilder.Normalizar(
                classificationOnly: Booleano(Buscar(detalle, "ClassificationOnly")),
                nivelClasificacion: Texto(Buscar(detalle, "NivelClasificacion")),
                nivelDefault: nivelDefault,
                codigosRestriccion: Lista(Buscar(clasificacion, "RestriccionTipologias", "Codigos")),
                expectedType: expectedType,
                modeloClasificacion: modelo);

            return ClaveIntencionBuilder.Construir(componentes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? Buscar(JsonElement? origen, params string[] ruta)
    {
        var actual = origen;
        foreach (var nombre in ruta)
        {
            if (actual is not { ValueKind: JsonValueKind.Object } objeto)
            {
                return null;
            }

            JsonElement? encontrado = null;
            foreach (var propiedad in objeto.EnumerateObject())
            {
                if (string.Equals(propiedad.Name, nombre, StringComparison.OrdinalIgnoreCase))
                {
                    encontrado = propiedad.Value;
                    break;
                }
            }

            actual = encontrado;
        }

        return actual;
    }

    private static string? Texto(JsonElement? e)
        => e is { ValueKind: JsonValueKind.String } ? e.Value.GetString() : null;

    private static bool Booleano(JsonElement? e)
        => e is { ValueKind: JsonValueKind.True };

    private static List<string>? Lista(JsonElement? e)
        => e is { ValueKind: JsonValueKind.Array }
            ? e.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? string.Empty).ToList()
            : null;
}
