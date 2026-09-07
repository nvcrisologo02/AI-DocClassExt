using System.Text.Json;
using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services;

/// <summary>
/// Traduce el bloque "usage" de una respuesta de Content Understanding a consumos.
///
/// El servicio factura por su cuenta las paginas y la contextualizacion, pero los
/// tokens del modelo generativo que usa por dentro se cargan al deployment de
/// Foundry conectado y los declara aparte, en "usage.tokens", con claves del tipo
/// "gpt-4.1-input" y "gpt-4.1-output". Se separan en consumos distintos para que
/// cada parte se tarifique con el precio que le corresponde, y para que el dia que
/// el servicio cambie de modelo generativo el coste se ajuste solo.
/// </summary>
public static class UsoContentUnderstandingMapper
{
    private const string SufijoSalida = "-output";
    private const string SufijoEntrada = "-input";

    private static readonly string[] MedidoresDePagina =
    {
        "documentPagesMinimal",
        "documentPagesBasic",
        "documentPagesStandard"
    };

    /// <summary>
    /// Devuelve un consumo del servicio (paginas y contextualizacion) y uno por cada
    /// modelo generativo declarado. Lista vacia si no hay bloque de uso reconocible:
    /// preferimos no tarificar a dar una cifra inventada.
    /// </summary>
    public static List<ConsumoIA> Mapear(JsonElement raiz, string? analyzerId)
    {
        var consumos = new List<ConsumoIA>();

        if (!TryGetUsage(raiz, out var usage))
        {
            return consumos;
        }

        var paginas = MedidoresDePagina.Sum(nombre => LeerEntero(usage, nombre));
        var contextualizacion = LeerEntero(usage, "contextualizationTokens");

        if (paginas > 0 || contextualizacion > 0)
        {
            consumos.Add(new ConsumoIA
            {
                Actividad = ActividadesIA.Extraer,
                Operacion = "extraction.cu.servicio",
                Proveedor = ProveedoresIA.ContentUnderstanding,
                Modelo = analyzerId ?? string.Empty,
                Paginas = paginas > 0 ? paginas : null,
                TokensContextualizacion = contextualizacion > 0 ? contextualizacion : null
            });
        }

        consumos.AddRange(MapearModelosGenerativos(usage));

        return consumos;
    }

    /// <summary>
    /// Agrupa las claves "modelo-input" y "modelo-output" en un consumo por modelo.
    /// Una clave sin sufijo, como la de embeddings, cuenta como entrada.
    /// </summary>
    private static IEnumerable<ConsumoIA> MapearModelosGenerativos(JsonElement usage)
    {
        if (!usage.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        var porModelo = new Dictionary<string, ConsumoIA>(StringComparer.OrdinalIgnoreCase);
        var orden = new List<string>();

        foreach (var propiedad in tokens.EnumerateObject())
        {
            if (propiedad.Value.ValueKind != JsonValueKind.Number ||
                !propiedad.Value.TryGetInt32(out var cantidad) ||
                cantidad <= 0)
            {
                continue;
            }

            var (modelo, esSalida) = PartirClave(propiedad.Name);

            if (!porModelo.TryGetValue(modelo, out var consumo))
            {
                consumo = new ConsumoIA
                {
                    Actividad = ActividadesIA.Extraer,
                    Operacion = "extraction.cu.modelo",
                    Proveedor = ProveedoresIA.AzureOpenAI,
                    Modelo = modelo
                };
                porModelo[modelo] = consumo;
                orden.Add(modelo);
            }

            if (esSalida)
            {
                consumo.TokensSalida = (consumo.TokensSalida ?? 0) + cantidad;
            }
            else
            {
                consumo.TokensEntrada = (consumo.TokensEntrada ?? 0) + cantidad;
            }
        }

        foreach (var modelo in orden)
        {
            yield return porModelo[modelo];
        }
    }

    private static (string Modelo, bool EsSalida) PartirClave(string clave)
    {
        if (clave.EndsWith(SufijoSalida, StringComparison.OrdinalIgnoreCase))
        {
            return (clave[..^SufijoSalida.Length], true);
        }

        if (clave.EndsWith(SufijoEntrada, StringComparison.OrdinalIgnoreCase))
        {
            return (clave[..^SufijoEntrada.Length], false);
        }

        return (clave, false);
    }

    private static bool TryGetUsage(JsonElement raiz, out JsonElement usage)
    {
        usage = default;

        if (raiz.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (raiz.TryGetProperty("usage", out var directo) && directo.ValueKind == JsonValueKind.Object)
        {
            usage = directo;
            return true;
        }

        if (raiz.TryGetProperty("result", out var resultado) &&
            resultado.ValueKind == JsonValueKind.Object &&
            resultado.TryGetProperty("usage", out var anidado) &&
            anidado.ValueKind == JsonValueKind.Object)
        {
            usage = anidado;
            return true;
        }

        return false;
    }

    private static int LeerEntero(JsonElement objeto, string propiedad)
    {
        return objeto.TryGetProperty(propiedad, out var valor) &&
               valor.ValueKind == JsonValueKind.Number &&
               valor.TryGetInt32(out var entero)
            ? entero
            : 0;
    }
}
