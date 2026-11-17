#nullable enable
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using DocumentIA.Functions.Services;
using FluentAssertions;
using OpenAI.Chat;
using Xunit.Abstractions;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// AB#101371: puerta de serialización en el cable. Comprueba, con un transporte falso, que el cliente
/// de Azure pone en el cuerpo HTTP <c>reasoning_effort</c> y <c>max_completion_tokens</c> (y no
/// <c>max_tokens</c> ni <c>temperature</c>) para un deployment de razonamiento, y lo contrario para uno clásico.
/// </summary>
public sealed class OpenAiReasoningWireTests
{
    private const string RespuestaOk =
        "{\"id\":\"x\",\"object\":\"chat.completion\",\"created\":0,\"model\":\"m\",\"choices\":[{\"index\":0,\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"total_tokens\":2}}";

    private readonly ITestOutputHelper _salida;

    public OpenAiReasoningWireTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    [Fact]
    public async Task CompleteChat_GptLuna_EnviaReasoningEffortYMaxCompletionTokens()
    {
        using var cuerpo = await EnviarPeticionAsync("gpt-5.6-luna", 150, "none");
        var raiz = cuerpo.RootElement;

        raiz.GetProperty("reasoning_effort").GetString().Should().Be("none");
        raiz.GetProperty("max_completion_tokens").GetInt32().Should().Be(2000);
        raiz.TryGetProperty("temperature", out _).Should().BeFalse();
        raiz.TryGetProperty("max_tokens", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CompleteChat_Gpt4oMini_EnviaTemperatureYNoReasoningEffort()
    {
        using var cuerpo = await EnviarPeticionAsync("gpt-4o-mini", 150, "none");
        var raiz = cuerpo.RootElement;

        raiz.GetProperty("temperature").GetDouble().Should().Be(0);
        raiz.TryGetProperty("reasoning_effort", out _).Should().BeFalse();
        raiz.TryGetProperty("max_completion_tokens", out _).Should().BeFalse();
    }

    private async Task<JsonDocument> EnviarPeticionAsync(string deployment, int maxTokensClasico, string reasoningEffort)
    {
        var handler = new FakeHandler();
        handler.Respuestas.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(RespuestaOk, Encoding.UTF8, "application/json")
        });

        var opciones = new AzureOpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(new HttpClient(handler))
        };
        var cliente = new AzureOpenAIClient(new Uri("https://fake.openai.azure.com/"), new AzureKeyCredential("fake"), opciones);
        var chat = cliente.GetChatClient(deployment);

        var options = new ChatCompletionOptions { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() };
        var limite = deployment.StartsWith("gpt-5", StringComparison.Ordinal) ? 2000 : maxTokensClasico;
        OpenAiModelCapabilities.ConfigureChatOptions(options, deployment, 0.0, limite, reasoningEffort);

        await chat.CompleteChatAsync([new SystemChatMessage("s"), new UserChatMessage("u")], options);

        handler.Urls.Should().HaveCount(1);
        _salida.WriteLine($"URL ({deployment}): {handler.Urls[0]}");
        _salida.WriteLine($"Cuerpo ({deployment}): {handler.CuerpoPrimeraPeticion}");
        return JsonDocument.Parse(handler.CuerpoPrimeraPeticion);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public Queue<HttpResponseMessage> Respuestas { get; } = new();
        public List<string> Cuerpos { get; } = [];
        public List<string> Urls { get; } = [];
        public string CuerpoPrimeraPeticion => Cuerpos.Count > 0 ? Cuerpos[0] : string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                Urls.Add(request.RequestUri?.ToString() ?? string.Empty);
                Cuerpos.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return Respuestas.Dequeue();
        }
    }
}
