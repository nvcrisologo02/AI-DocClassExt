#nullable enable
using System.Text.Json.Nodes;
using DocumentIA.Core.Configuration;
using DocumentIA.Functions.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class PromptCacheRequestPolicyTests
{
    private const string Clave = "documentia-clasif:phase1:abcdef123456";

    private const string CuerpoBase =
        "{\"messages\":[{\"role\":\"system\",\"content\":\"S\"},{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":\"U\"}]}],\"response_format\":{\"type\":\"json_object\"},\"model\":\"gpt-5.6-luna\"}";

    private const string CuerpoDosPartes =
        "{\"messages\":[{\"role\":\"system\",\"content\":\"S\"},{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":\"ESTABLE\"},{\"type\":\"text\",\"text\":\"DOC\"}]}],\"model\":\"gpt-5.6-luna\"}";

    private const string CuerpoUsuarioString =
        "{\"messages\":[{\"role\":\"system\",\"content\":\"S\"},{\"role\":\"user\",\"content\":\"U\"}],\"model\":\"gpt-5.6-luna\"}";

    private static JsonNode Parse(string json) => JsonNode.Parse(json)!;

    [Fact]
    public void Reescribir_UsuarioEnDosPartes_MarcaSoloLaPrimera()
    {
        var r = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoDosPartes, Clave, "30m", breakpoint: true, mode: "explicit"));

        r["messages"]![1]!["content"]![0]!["prompt_cache_breakpoint"]!["mode"]!.GetValue<string>().Should().Be("explicit");
        r["messages"]![1]!["content"]![0]!["text"]!.GetValue<string>().Should().Be("ESTABLE");
        r["messages"]![1]!["content"]![1]!.AsObject().ContainsKey("prompt_cache_breakpoint").Should().BeFalse();
        r["messages"]![0]!["content"]![0]!["prompt_cache_breakpoint"]!["mode"]!.GetValue<string>().Should().Be("explicit");
    }

    [Fact]
    public void Reescribir_UsuarioUnaParte_NoMarcaElUsuario()
    {
        var r = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoBase, Clave, "30m", breakpoint: true, mode: "explicit"));

        r["messages"]![1]!["content"]![0]!.AsObject().ContainsKey("prompt_cache_breakpoint").Should().BeFalse();
    }

    [Fact]
    public void Reescribir_UsuarioComoString_LoDejaComoString()
    {
        var r = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoUsuarioString, Clave, "30m", breakpoint: true, mode: "explicit"));

        r["messages"]![1]!["content"]!.GetValue<string>().Should().Be("U");
    }

    [Fact]
    public void Reescribir_SinBreakpoint_NoMarcaElUsuarioEnDosPartes()
    {
        var r = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoDosPartes, Clave, "30m", breakpoint: false, mode: "explicit"));

        r["messages"]![1]!["content"]![0]!.AsObject().ContainsKey("prompt_cache_breakpoint").Should().BeFalse();
        r["messages"]![1]!["content"]![1]!.AsObject().ContainsKey("prompt_cache_breakpoint").Should().BeFalse();
    }

    [Fact]
    public void Reescribir_SinModo_EnviaImplicit()
    {
        var resultado = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoBase, Clave, "30m", breakpoint: true));

        resultado["prompt_cache_options"]!["mode"]!.GetValue<string>().Should().Be("implicit");
    }

    [Fact]
    public void Reescribir_ModoExplicit_EnviaExplicitYConservaElBreakpointDelSystem()
    {
        var resultado = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoBase, Clave, "30m", breakpoint: true, mode: "explicit"));

        resultado["prompt_cache_options"]!["mode"]!.GetValue<string>().Should().Be("explicit");
        resultado["prompt_cache_options"]!["ttl"]!.GetValue<string>().Should().Be("30m");
        resultado["messages"]![0]!["content"]![0]!["prompt_cache_breakpoint"]!["mode"]!.GetValue<string>().Should().Be("explicit");
    }

    [Theory]
    [InlineData("EXPLICIT", "explicit")]
    [InlineData("Implicit", "implicit")]
    [InlineData("otro", "implicit")]
    [InlineData("", "implicit")]
    [InlineData(null, "implicit")]
    public void Reescribir_ModoNormalizado_SoloAceptaImplicitOExplicit(string? mode, string esperado)
    {
        var resultado = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoBase, Clave, "30m", breakpoint: false, mode: mode));

        resultado["prompt_cache_options"]!["mode"]!.GetValue<string>().Should().Be(esperado);
    }

    [Fact]
    public void Crear_ModoExplicit_LoPropagaAlCuerpoReescrito()
    {
        var model = new ClassificationModelConfig
        {
            PromptCache = new PromptCacheConfig { Enabled = true, Mode = "explicit" }
        };

        var politica = PromptCacheRequestPolicy.Crear(model, "phase1", "sistema");

        politica.Should().NotBeNull();
        politica!.Mode.Should().Be("explicit");
    }

    [Fact]
    public void Reescribir_AnadeClaveYOpciones()
    {
        var resultado = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoBase, Clave, "30m", breakpoint: false));

        resultado["prompt_cache_key"]!.GetValue<string>().Should().Be(Clave);
        JsonNode.DeepEquals(resultado["prompt_cache_options"], Parse("{\"mode\":\"implicit\",\"ttl\":\"30m\"}")).Should().BeTrue();
    }

    [Fact]
    public void Reescribir_ConBreakpoint_ConvierteElSystemEnArrayConBreakpoint()
    {
        var resultado = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoBase, Clave, "30m", breakpoint: true));

        var contenido = resultado["messages"]![0]!["content"]!;
        contenido.Should().BeAssignableTo<JsonArray>();
        contenido.AsArray().Should().HaveCount(1);
        JsonNode.DeepEquals(contenido[0],
            Parse("{\"type\":\"text\",\"text\":\"S\",\"prompt_cache_breakpoint\":{\"mode\":\"explicit\"}}")).Should().BeTrue();
    }

    [Fact]
    public void Reescribir_SinBreakpoint_DejaElSystemComoString()
    {
        var resultado = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoBase, Clave, "30m", breakpoint: false));

        resultado["messages"]![0]!["content"]!.GetValue<string>().Should().Be("S");
    }

    [Fact]
    public void Reescribir_SystemYaEnArray_SoloAnadeElBreakpointALaUltimaParte()
    {
        const string cuerpo =
            "{\"messages\":[{\"role\":\"system\",\"content\":[{\"type\":\"text\",\"text\":\"A\"},{\"type\":\"text\",\"text\":\"B\"}]},{\"role\":\"user\",\"content\":\"U\"}],\"model\":\"gpt-5.6-luna\"}";

        var resultado = Parse(PromptCacheRequestPolicy.Reescribir(cuerpo, Clave, "30m", breakpoint: true));

        var partes = resultado["messages"]![0]!["content"]!.AsArray();
        partes.Should().HaveCount(2);
        JsonNode.DeepEquals(partes[0], Parse("{\"type\":\"text\",\"text\":\"A\"}")).Should().BeTrue();
        JsonNode.DeepEquals(partes[1],
            Parse("{\"type\":\"text\",\"text\":\"B\",\"prompt_cache_breakpoint\":{\"mode\":\"explicit\"}}")).Should().BeTrue();
    }

    [Fact]
    public void Reescribir_ConservaElRestoDelCuerpo()
    {
        var resultado = Parse(PromptCacheRequestPolicy.Reescribir(CuerpoBase, Clave, "30m", breakpoint: true));
        var original = Parse(CuerpoBase);

        JsonNode.DeepEquals(resultado["model"], original["model"]).Should().BeTrue();
        JsonNode.DeepEquals(resultado["response_format"], original["response_format"]).Should().BeTrue();
        JsonNode.DeepEquals(resultado["messages"]![1], original["messages"]![1]).Should().BeTrue();
        resultado["messages"]!.AsArray().Should().HaveCount(2);

        // Sin mensaje de sistema: el cuerpo queda igual más la clave y las opciones.
        const string sinSystem = "{\"messages\":[{\"role\":\"user\",\"content\":\"U\"}],\"model\":\"gpt-5.6-luna\"}";
        var esperado = Parse(sinSystem);
        esperado["prompt_cache_key"] = Clave;
        esperado["prompt_cache_options"] = Parse("{\"mode\":\"implicit\",\"ttl\":\"30m\"}");

        var obtenido = Parse(PromptCacheRequestPolicy.Reescribir(sinSystem, Clave, "30m", breakpoint: true));

        JsonNode.DeepEquals(obtenido, esperado).Should().BeTrue();
    }

    [Fact]
    public void Reescribir_SystemCorto_SigueBienFormado()
    {
        var json = PromptCacheRequestPolicy.Reescribir(CuerpoBase, Clave, "30m", breakpoint: true);

        var act = () => JsonNode.Parse(json);
        act.Should().NotThrow();
        Parse(json)["messages"]![0]!["content"]![0]!["text"]!.GetValue<string>().Should().Be("S");
    }

    [Fact]
    public void Crear_SinPromptCache_DevuelveNulo()
    {
        var model = new ClassificationModelConfig { PromptCache = null };

        PromptCacheRequestPolicy.Crear(model, "phase1", "sistema").Should().BeNull();
    }

    [Fact]
    public void Crear_PromptCacheApagado_DevuelveNulo()
    {
        var model = new ClassificationModelConfig { PromptCache = new PromptCacheConfig { Enabled = false } };

        PromptCacheRequestPolicy.Crear(model, "phase1", "sistema").Should().BeNull();
    }

    [Fact]
    public void Crear_PromptCacheEncendido_DevuelvePoliticaConLaClaveEsperada()
    {
        var model = new ClassificationModelConfig
        {
            PromptCache = new PromptCacheConfig { Enabled = true, KeyPrefix = "documentia-clasif", Ttl = "30m", Breakpoint = false }
        };

        var politica = PromptCacheRequestPolicy.Crear(model, "phase1", "sistema");

        politica.Should().NotBeNull();
        politica!.CacheKey.Should().Be(PromptCacheKeyBuilder.Build("documentia-clasif", "phase1", "sistema"));
    }
}
