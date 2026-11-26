#nullable enable
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Azure;
using Azure.AI.OpenAI;
using DocumentIA.Functions.Services;
using FluentAssertions;
using OpenAI.Chat;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// AB#101428: el mensaje de usuario viaja en dos partes (tramo estable y documento) solo con caché de prompt
/// encendida, y la política deja el breakpoint en la primera. Sin caché, el cuerpo es el de siempre.
/// </summary>
public sealed class PromptCacheUserSplitWireTests
{
    private const string RespuestaOk =
        "{\"id\":\"x\",\"object\":\"chat.completion\",\"created\":0,\"model\":\"m\",\"choices\":[{\"index\":0,\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"total_tokens\":2}}";

    [Fact]
    public async Task ConCache_UsuarioViajaEnDosPartesYLaPrimeraLlevaBreakpoint()
    {
        var politica = new PromptCacheRequestPolicy("k", "30m", breakpoint: true, mode: "explicit");
        var mensaje = GptClasificarDataProvider.ConstruirMensajeUsuario("ESTABLE", "DOC", dividir: true);

        var cuerpo = JsonNode.Parse(await EnviarAsync(mensaje, politica))!;

        var partes = cuerpo["messages"]![1]!["content"]!.AsArray();
        partes.Should().HaveCount(2);
        partes[0]!["text"]!.GetValue<string>().Should().Be("ESTABLE");
        partes[0]!["prompt_cache_breakpoint"]!["mode"]!.GetValue<string>().Should().Be("explicit");
        partes[1]!["text"]!.GetValue<string>().Should().Be("DOC");
        partes[1]!.AsObject().ContainsKey("prompt_cache_breakpoint").Should().BeFalse();
    }

    [Fact]
    public async Task SinCache_CuerpoIgualAlDeUnaSolaParteConcatenada()
    {
        var dividido = GptClasificarDataProvider.ConstruirMensajeUsuario("ESTABLE", "DOC", dividir: false);
        var original = new UserChatMessage(ChatMessageContentPart.CreateTextPart("ESTABLEDOC"));

        var cuerpoDividido = await EnviarAsync(dividido, politica: null);
        var cuerpoOriginal = await EnviarAsync(original, politica: null);

        cuerpoDividido.Should().Be(cuerpoOriginal);
    }

    [Fact]
    public void ConstruirMensajeUsuario_RestoVacio_UnaSolaParte()
    {
        var mensaje = GptClasificarDataProvider.ConstruirMensajeUsuario("ESTABLE", string.Empty, dividir: true);

        mensaje.Content.Should().HaveCount(1);
        mensaje.Content[0].Text.Should().Be("ESTABLE");
    }

    [Fact]
    public void MontarUsuarioFase1_ResumenVaEnElResto()
    {
        var (estable, resto) = GptClasificarDataProvider.MontarUsuarioFase1(
            "Contexto: {CONTEXT_PROMPT}\nCatálogo: {TDN1_CATALOG}\nDOC:\n{DOCUMENT_TEXT}",
            "ctx",
            "cat",
            "texto",
            instruccionResumen: "R");

        estable.Should().Be("Contexto: ctx\nCatálogo: cat\nDOC:\n");
        estable.Should().NotContain("Instrucción adicional");
        resto.Should().Be("texto\n\nInstrucción adicional para devolver en resumen:\nR");
    }

    private static async Task<string> EnviarAsync(UserChatMessage usuario, PromptCacheRequestPolicy? politica)
    {
        var handler = new FakeHandler();
        var opciones = new AzureOpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(new HttpClient(handler))
        };
        if (politica is not null)
        {
            opciones.AddPolicy(politica, PipelinePosition.PerCall);
        }

        var cliente = new AzureOpenAIClient(new Uri("https://fake.openai.azure.com/"), new AzureKeyCredential("fake"), opciones);
        await cliente.GetChatClient("gpt-5.6-luna").CompleteChatAsync([new SystemChatMessage("S"), usuario]);

        handler.Cuerpos.Should().HaveCount(1);
        return handler.Cuerpos[0];
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<string> Cuerpos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Cuerpos.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RespuestaOk, Encoding.UTF8, "application/json")
            };
        }
    }
}
