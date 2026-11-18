using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json.Nodes;
using DocumentIA.Core.Configuration;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Política de pipeline que reescribe el cuerpo JSON de la petición de Chat Completions para añadir
/// los campos de caché de prompt (<c>prompt_cache_key</c>, <c>prompt_cache_options</c> y, si procede,
/// <c>prompt_cache_breakpoint</c> en el mensaje de sistema), que el SDK instalado no expone.
/// </summary>
public sealed class PromptCacheRequestPolicy : PipelinePolicy
{
    private readonly string _cacheKey;
    private readonly string _ttl;
    private readonly bool _breakpoint;

    /// <summary>Clave de cach� que la pol�tica inyecta en la petici�n.</summary>
    public string CacheKey => _cacheKey;

    public PromptCacheRequestPolicy(string cacheKey, string ttl, bool breakpoint)
    {
        _cacheKey = cacheKey;
        _ttl = ttl;
        _breakpoint = breakpoint;
    }

    /// <summary>
    /// Devuelve la política del modelo, o nulo cuando no declara caché de prompt o la tiene apagada
    /// (en ese caso la petición viaja sin ninguna modificación).
    /// </summary>
    public static PromptCacheRequestPolicy? Crear(ClassificationModelConfig model, string operation, string systemText)
    {
        var config = model.PromptCache;
        if (config is null || !config.Enabled)
        {
            return null;
        }

        var clave = PromptCacheKeyBuilder.Build(config.KeyPrefix, operation, systemText);
        return new PromptCacheRequestPolicy(clave, config.Ttl, config.Breakpoint);
    }

    /// <summary>
    /// Añade al cuerpo la clave y las opciones de caché y, con <paramref name="breakpoint"/>, marca el
    /// mensaje de sistema como punto de corte. Sin mensaje de sistema solo se añaden clave y opciones.
    /// </summary>
    public static string Reescribir(string bodyJson, string cacheKey, string ttl, bool breakpoint)
    {
        var raiz = JsonNode.Parse(bodyJson)!.AsObject();

        raiz["prompt_cache_key"] = cacheKey;
        raiz["prompt_cache_options"] = new JsonObject
        {
            ["mode"] = "implicit",
            ["ttl"] = ttl
        };

        if (breakpoint && raiz["messages"] is JsonArray mensajes)
        {
            var system = mensajes
                .OfType<JsonObject>()
                .FirstOrDefault(m => m["role"]?.GetValue<string>() == "system");

            if (system is not null)
            {
                MarcarBreakpoint(system);
            }
        }

        return raiz.ToJsonString();
    }

    private static void MarcarBreakpoint(JsonObject system)
    {
        var contenido = system["content"];

        if (contenido is JsonValue valor && valor.TryGetValue<string>(out var texto))
        {
            system["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = texto,
                ["prompt_cache_breakpoint"] = new JsonObject { ["mode"] = "explicit" }
            });
        }
        else if (contenido is JsonArray partes && partes.Count > 0 && partes[^1] is JsonObject ultima)
        {
            ultima["prompt_cache_breakpoint"] = new JsonObject { ["mode"] = "explicit" };
        }
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        var contenido = message.Request.Content;
        if (EsPost(message) && contenido is not null)
        {
            using var stream = new MemoryStream();
            contenido.WriteTo(stream);
            Sustituir(message, stream);
        }

        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        var contenido = message.Request.Content;
        if (EsPost(message) && contenido is not null)
        {
            using var stream = new MemoryStream();
            await contenido.WriteToAsync(stream).ConfigureAwait(false);
            Sustituir(message, stream);
        }

        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
    }

    private static bool EsPost(PipelineMessage message) =>
        string.Equals(message.Request.Method, "POST", StringComparison.OrdinalIgnoreCase);

    private void Sustituir(PipelineMessage message, MemoryStream stream)
    {
        var cuerpo = Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        if (!cuerpo.TrimStart().StartsWith('{'))
        {
            // No es JSON de objeto: se deja intacto, reponiendo el contenido ya consumido.
            message.Request.Content = BinaryContent.Create(BinaryData.FromBytes(stream.ToArray()));
            return;
        }

        message.Request.Content = BinaryContent.Create(
            BinaryData.FromString(Reescribir(cuerpo, _cacheKey, _ttl, _breakpoint)));
    }
}
