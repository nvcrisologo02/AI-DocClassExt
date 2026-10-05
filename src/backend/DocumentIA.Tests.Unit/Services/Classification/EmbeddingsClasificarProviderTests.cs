#nullable enable
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services.Classification;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Abstractions;
using DocumentIA.Functions.Services.Classification;
using DocumentIA.Functions.Services.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentIA.Tests.Unit.Services.Classification;

public class EmbeddingsClasificarProviderTests
{
    private const string ConfigSombra = """
    {"DeploymentName":"text-embedding-3-large-030358","ResourceAlias":"openai_primary","AuthMode":"DefaultAzureCredential",
     "Artefacto":{"Container":"documentai","BlobPath":"modelos/x.json"},"Modo":"sombra","UmbralConfianza":0.6,
     "Restringido":{"Modo":"sombra","UmbralMasa":0.5,"UmbralConfianzaCondicionada":0.8},"MaxChars":24000,"TimeoutSeconds":20}
    """;

    private sealed class Entorno
    {
        public Mock<EmbeddingsClasificadorConfigLoader> Config { get; } = new();
        public Mock<ModeloEmbeddingsLoader> Modelos { get; } = new();
        public Mock<CatalogoParesTdnLoader> Catalogo { get; } = new();
        public Mock<IEmbeddingsCliente> Cliente { get; } = new();
        public Mock<IEmbeddingsClienteFactory> Factory { get; } = new();
        public Mock<IAzureOpenAIResilienceExecutor> Resiliencia { get; } = new();
        public Mock<ITelemetryService> Telemetria { get; } = new();
        public ModeloEmbeddings Modelo { get; }
        public float[] VectorAAAA { get; }

        public Entorno(string configJson = ConfigSombra, bool conArtefacto = true, EmbeddingsClasificadorConfig? configPropia = null)
        {
            var config = configPropia ?? EmbeddingsClasificadorConfigLoader.Parse(configJson);
            config.Endpoint = "https://ejemplo.openai.azure.com/";
            Config.Setup(c => c.Load()).Returns(config);

            Modelo = ModeloEmbeddings.Parse(File.ReadAllText(ModeloEmbeddingsTests.RutaFixture("clasificador-embeddings-fixture.json")));
            Modelos.Setup(m => m.ObtenerAsync("documentai", "modelos/x.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(conArtefacto ? Modelo : null);

            var paridad = JsonDocument.Parse(File.ReadAllText(ModeloEmbeddingsTests.RutaFixture("paridad-fixture.json")));
            VectorAAAA = paridad.RootElement.GetProperty("casos")[0].GetProperty("vector").EnumerateArray().Select(v => (float)v.GetDouble()).ToArray();

            Catalogo.Setup(c => c.Load()).Returns(new Dictionary<string, TipologiaPar>(StringComparer.OrdinalIgnoreCase)
            {
                ["AAAA|AAAA-01"] = new("a.01", "AAAA", "aaaa-01"),
                ["AAAA|AAAA-02"] = new("a.02", "AAAA", "aaaa-02"),
                ["AAAA|AAAA-03"] = new("a.03", "AAAA", "aaaa-03"),
                ["BBBB|BBBB-01"] = new("b.01", "BBBB", "bbbb-01"),
            });

            Cliente.Setup(c => c.GenerarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RespuestaEmbedding(VectorAAAA, new ConsumoIA
                {
                    Actividad = ActividadesIA.Clasificar,
                    Operacion = UsoEmbeddingsMapper.Operacion,
                    Modelo = "text-embedding-3-large-030358",
                    TokensEntrada = 512,
                    TokensSalida = 0
                }));
            Factory.Setup(f => f.Crear(It.IsAny<EmbeddingsClasificadorConfig>())).Returns(Cliente.Object);

            Resiliencia.Setup(r => r.ExecuteOnceAsync(
                    It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task<RespuestaEmbedding>>>(), It.IsAny<CancellationToken>()))
                .Returns((string _, Func<CancellationToken, Task<RespuestaEmbedding>> op, CancellationToken ct) => op(ct));
        }

        public EmbeddingsClasificarProvider Crear() => new(
            Config.Object, Modelos.Object, Catalogo.Object, Factory.Object, Resiliencia.Object, Telemetria.Object,
            NullLogger<EmbeddingsClasificarProvider>.Instance);
    }

    private static ClasificarEmbeddingsInput Input(string? texto = "Texto   de  prueba", bool expectedType = false, List<string>? restriccion = null) => new()
    {
        Texto = texto,
        ExpectedTypeInformado = expectedType,
        RestriccionCodigos = restriccion,
        InstanceId = "inst-1"
    };

    [Fact]
    public async Task Sombra_CalculaPersisteYDerivaAlGpt()
    {
        var env = new Entorno();

        var r = await env.Crear().ClasificarAsync(Input(), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        r.Motivo.Should().Be(MotivosEmbeddings.Sombra);
        r.Modo.Should().Be(ModosEmbeddings.Sombra);
        r.ModoRestringido.Should().BeNull();
        r.VersionModelo.Should().Be("fixture");
        r.Deployment.Should().Be("text-embedding-3-large-030358");
        r.Tdn1.Should().Be("AAAA");
        r.Tipologia.Should().Be("a.01");
        r.Top3.Should().HaveCount(3);
        r.Top3[0].Tdn1.Should().Be("AAAA");
        r.Consumos.Should().ContainSingle().Which.TokensEntrada.Should().Be(512);
        r.Error.Should().BeNull();
        env.Cliente.Verify(c => c.GenerarAsync("Texto de prueba", It.IsAny<CancellationToken>()), Times.Once, "el texto va con los espacios colapsados");
        env.Telemetria.Verify(t => t.TrackEvent("Classification.Embeddings",
            It.Is<IDictionary<string, string>>(p => p["Decision"] == DecisionesEmbeddings.DerivarGpt && p["InstanceId"] == "inst-1")), Times.Once);
        env.Telemetria.Verify(t => t.TrackMetric("Classification.Embeddings.LatenciaMs", It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task ModoOff_OmitidoSinLlamarANadaNiEmitirTelemetria()
    {
        var env = new Entorno(ConfigSombra.Replace("\"Modo\":\"sombra\",\"UmbralConfianza\"", "\"Modo\":\"off\",\"UmbralConfianza\"")
            .Replace("\"Restringido\":{\"Modo\":\"sombra\"", "\"Restringido\":{\"Modo\":\"off\""));

        var r = await env.Crear().ClasificarAsync(Input(), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.Omitido);
        r.Motivo.Should().Be(MotivosEmbeddings.Off);
        r.Error.Should().BeNull();
        env.Modelos.Verify(m => m.ObtenerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        env.Cliente.Verify(c => c.GenerarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        env.Telemetria.Verify(t => t.TrackEvent(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Never);
        env.Telemetria.Verify(t => t.TrackMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()), Times.Never);
    }

    [Fact]
    public async Task ModoOffPorConfiguracionInvalida_OmiteConErrorYEmiteTelemetria()
    {
        var env = new Entorno(configPropia: EmbeddingsClasificadorConfig.Desactivada("json_invalido"));

        var r = await env.Crear().ClasificarAsync(Input(), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.Omitido);
        r.Motivo.Should().Be(MotivosEmbeddings.Off);
        r.Error.Should().Be("json_invalido");
        env.Telemetria.Verify(t => t.TrackEvent("Classification.Embeddings",
            It.Is<IDictionary<string, string>>(p => p["Error"] == "json_invalido")), Times.Once);
    }

    [Fact]
    public async Task EntradaNula_OmitidoSinTextoSinLanzar()
    {
        var env = new Entorno();

        var r = await env.Crear().ClasificarAsync(null!, CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.Omitido);
        r.Motivo.Should().Be(MotivosEmbeddings.SinTexto);
    }

    [Fact]
    public async Task RestringidaConModoRestringidoOff_OmitidoAunqueElNormalEsteEnSombra()
    {
        var env = new Entorno(ConfigSombra.Replace("\"Restringido\":{\"Modo\":\"sombra\"", "\"Restringido\":{\"Modo\":\"off\""));

        var r = await env.Crear().ClasificarAsync(Input(restriccion: new List<string> { "a.01" }), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.Omitido);
        r.ModoRestringido.Should().Be(ModosEmbeddings.Off);
    }

    [Fact]
    public async Task SinTexto_OmitidoYEmiteTelemetria()
    {
        var env = new Entorno();

        var r = await env.Crear().ClasificarAsync(Input(texto: "  \n "), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.Omitido);
        r.Motivo.Should().Be(MotivosEmbeddings.SinTexto);
        env.Cliente.Verify(c => c.GenerarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        env.Telemetria.Verify(t => t.TrackEvent("Classification.Embeddings", It.IsAny<IDictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task ArtefactoNoDisponible_DerivaConError()
    {
        var env = new Entorno(conArtefacto: false);

        var r = await env.Crear().ClasificarAsync(Input(), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        r.Motivo.Should().Be(MotivosEmbeddings.Error);
        r.Error.Should().Be("artefacto_no_disponible");
        env.Cliente.Verify(c => c.GenerarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CircuitoAbierto_DerivaSinLlamar()
    {
        var env = new Entorno();
        env.Resiliencia.Setup(r => r.ExecuteOnceAsync(
                It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task<RespuestaEmbedding>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RateLimitExhaustedException("Circuito abierto para 'x'."));

        var r = await env.Crear().ClasificarAsync(Input(), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        r.Motivo.Should().Be(MotivosEmbeddings.CircuitoAbierto);
        r.Consumos.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ElClienteLanza_DerivaConErrorSinPropagar()
    {
        var env = new Entorno();
        env.Cliente.Setup(c => c.GenerarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var r = await env.Crear().ClasificarAsync(Input(), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        r.Motivo.Should().Be(MotivosEmbeddings.Error);
        r.Error.Should().Contain("boom");
        env.Telemetria.Verify(t => t.TrackEvent("Classification.Embeddings",
            It.Is<IDictionary<string, string>>(p => p["Error"].Contains("boom"))), Times.Once);
    }

    [Fact]
    public async Task ElClienteLanzaConMensajeLargo_ElErrorLlegaAcotado()
    {
        var env = new Entorno();
        env.Cliente.Setup(c => c.GenerarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(new string('x', 2_000)));

        var r = await env.Crear().ClasificarAsync(Input(), CancellationToken.None);

        r.Motivo.Should().Be(MotivosEmbeddings.Error);
        r.Error.Should().HaveLength(EmbeddingsClasificarProvider.ErrorMaxChars, "el error viaja al contrato y a BD, no solo a App Insights");
        r.Error.Should().StartWith("InvalidOperationException: xxx");
        env.Telemetria.Verify(t => t.TrackEvent("Classification.Embeddings",
            It.Is<IDictionary<string, string>>(p => p["Error"].Length == EmbeddingsClasificarProvider.ErrorMaxChars)), Times.Once);
    }

    [Fact]
    public async Task Timeout_DerivaConErrorTimeout()
    {
        var env = new Entorno();
        env.Cliente.Setup(c => c.GenerarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var r = await env.Crear().ClasificarAsync(Input(), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        r.Error.Should().Be("timeout");
    }

    [Fact]
    public async Task CancelacionDelLlamante_DerivaConErrorCancelado()
    {
        var env = new Entorno();
        using var cts = new CancellationTokenSource();
        env.Cliente.Setup(c => c.GenerarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        var r = await env.Crear().ClasificarAsync(Input(), cts.Token);

        r.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        r.Error.Should().Be("cancelado");
    }

    [Fact]
    public async Task VectorConOtrasDimensiones_DerivaConError()
    {
        var env = new Entorno();
        env.Cliente.Setup(c => c.GenerarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RespuestaEmbedding(new float[3072], null));

        var r = await env.Crear().ClasificarAsync(Input(), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        r.Error.Should().Be($"dimensiones: el vector tiene 3072 y el modelo espera {env.Modelo.Manifiesto.Dimensiones}");
    }

    [Fact]
    public async Task ElCircuitoSeIdentificaPorEndpointYDeployment()
    {
        var env = new Entorno();

        await env.Crear().ClasificarAsync(Input(), CancellationToken.None);

        env.Resiliencia.Verify(r => r.ExecuteOnceAsync(
            "https://ejemplo.openai.azure.com/|text-embedding-3-large-030358",
            It.IsAny<Func<CancellationToken, Task<RespuestaEmbedding>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestringidaEnSombra_PersisteMasaYPrediccionLibre()
    {
        var env = new Entorno();

        var r = await env.Crear().ClasificarAsync(Input(restriccion: new List<string> { "b.01" }), CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        r.ModoRestringido.Should().Be(ModosEmbeddings.Sombra);
        r.Restringido.Should().NotBeNull();
        r.Restringido!.Masa.Should().BeInRange(0, 1);
        r.Restringido.PrediccionSinRestringir.Should().Be("a.01");
    }
}
