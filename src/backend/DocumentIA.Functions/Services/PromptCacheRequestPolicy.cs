using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json.Nodes;
using DocumentIA.Core.Configuration;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Política de pipeline que reescribe el cuerpo JSON de la petición de Chat Completions para añadir
/// los campos de caché de prompt (<c>prompt_cache_key</c>, <c>prompt_cache_options</c> y, si procede,
/// <c>prompt_cache_breakpoint</c> en el mensaje de sistema y al final del tramo estable del mensaje de
/// usuario), que el SDK instalado no expone.
/// </summary>
public sealed class PromptCacheRequestPolicy : PipelinePolicy
{
    private readonly string _cacheKey;
    private readonly string _ttl;
    private readonly bool _breakpoint;
    private readonly string _mode;

    /// <summary>Clave de caché que la política inyecta en la petición.</summary>
    public string CacheKey => _cacheKey;

    /// <summary>Modo de caché normalizado (<c>implicit</c> o <c>explicit</c>) que la política inyecta.</summary>
    public string Mode => _mode;

    public PromptCacheRequestPolicy(string cacheKey, string ttl, bool breakpoint, string? mode = null)
    {
        _mode = NormalizarModo(mode);
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
        return new PromptCacheRequestPolicy(clave, config.Ttl, config.Breakpoint, config.Mode);
    }

    /// <summary>
    /// Añade al cuerpo la clave y las opciones de caché y, con <paramref name="breakpoint"/>, marca el
    /// mensaje de sistema como punto de corte. Si además el primer mensaje de usuario llega en varias partes
    /// (tramo estable y documento, AB#101428), marca también su primera parte. Un usuario en una sola parte o
    /// como string no se toca. Sin mensaje de sistema solo se añaden clave, opciones y el corte del usuario.
    /// El modo solo admite <c>implicit</c> o <c>explicit</c> (sin distinguir mayúsculas); vacío, nulo o
    /// desconocido se normaliza a <c>implicit</c> sin aviso.
    /// </summary>
    public static string Reescribir(string bodyJson, string cacheKey, string ttl, bool breakpoint, string? mode = null)
    {
        var raiz = JsonNode.Parse(bodyJson)!.AsObject();

        raiz["prompt_cache_key"] = cacheKey;
        raiz["prompt_cache_options"] = new JsonObject
        {
            ["mode"] = NormalizarModo(mode),
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

            var usuario = mensajes
                .OfType<JsonObject>()
                .FirstOrDefault(m => m["role"]?.GetValue<string>() == "user");

            if (usuario is not null)
            {
                MarcarBreakpointUsuario(usuario);
            }
        }

        return raiz.ToJsonString();
    }

    private static string NormalizarModo(string? mode) =>
        string.Equals(mode?.Trim(), "explicit", StringComparison.OrdinalIgnoreCase) ? "explicit" : "implicit";

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

    private static void MarcarBreakpointUsuario(JsonObject usuario)
    {
        if (usuario["content"] is JsonArray partes && partes.Count > 1 && partes[0] is JsonObject tramoEstable)
        {
            tramoEstable["prompt_cache_breakpoint"] = new JsonObject { ["mode"] = "explicit" };
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
            BinaryData.FromString(Reescribir(cuerpo, _cacheKey, _ttl, _breakpoint, _mode)));
    }
}
