#nullable enable
using System.ClientModel;
using System.ClientModel.Primitives;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services.Classification;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentIA.Tests.Unit.Services.Classification;

/// <summary>Reintento con la mitad del texto ante el 400 de longitud del endpoint de embeddings. AB#101245.</summary>
public class EmbeddingsClienteConRecorteTests
{
    private const string MensajeLongitud = "HTTP 400 (invalid_request_error: )\n\nInvalid 'input[0]': maximum input length is 8192 tokens.";
    private static readonly string Texto = new('x', 24_000);
    private static readonly RespuestaEmbedding Respuesta = new(new[] { 0.1f, 0.2f }, new ConsumoIA { Actividad = ActividadesIA.Clasificar });

    /// <summary>Cliente interno con un guion de resultados por llamada; registra los textos recibidos.</summary>
    private sealed class ClienteGuion : IEmbeddingsCliente
    {
        private readonly Queue<Func<RespuestaEmbedding>> _guion = new();
        public List<string> Textos { get; } = new();

        public ClienteGuion Devuelve(RespuestaEmbedding respuesta)
        {
            _guion.Enqueue(() => respuesta);
            return this;
        }

        public ClienteGuion Lanza(Exception ex)
        {
            _guion.Enqueue(() => throw ex);
            return this;
        }

        public Task<RespuestaEmbedding> GenerarAsync(string texto, CancellationToken cancellationToken)
        {
            Textos.Add(texto);
            if (_guion.Count == 0)
            {
                throw new InvalidOperationException("El guion del cliente se ha agotado.");
            }

            return Task.FromResult(_guion.Dequeue()());
        }
    }

    private static ClientResultException Error(int status, string mensaje)
    {
        var respuesta = new Mock<PipelineResponse>();
        respuesta.SetupGet(r => r.Status).Returns(status);
        return new ClientResultException(mensaje, respuesta.Object);
    }

    private static EmbeddingsClienteConRecorte Crear(ClienteGuion interno, int maxRecortes = 3) =>
        new(interno, NullLogger<EmbeddingsClienteConRecorte>.Instance, maxRecortes);

    [Fact]
    public async Task EntraALaPrimera_NoRecortaNiRepite()
    {
        var interno = new ClienteGuion().Devuelve(Respuesta);

        var resultado = await Crear(interno).GenerarAsync(Texto, CancellationToken.None);

        resultado.Should().BeSameAs(Respuesta);
        interno.Textos.Should().Equal(Texto);
    }

    [Fact]
    public async Task Longitud400_ReintentaConLaPrimeraMitadDelTexto()
    {
        var interno = new ClienteGuion().Lanza(Error(400, MensajeLongitud)).Devuelve(Respuesta);

        var resultado = await Crear(interno).GenerarAsync(Texto, CancellationToken.None);

        resultado.Should().BeSameAs(Respuesta);
        interno.Textos.Should().Equal(Texto, Texto[..12_000]);
    }

    [Fact]
    public async Task Longitud400Persistente_AgotaElTopeYRelanza()
    {
        var interno = new ClienteGuion();
        for (var i = 0; i < 4; i++)
        {
            interno.Lanza(Error(400, MensajeLongitud));
        }

        Func<Task<RespuestaEmbedding>> accion = () => Crear(interno, maxRecortes: 3).GenerarAsync(Texto, CancellationToken.None);

        (await accion.Should().ThrowAsync<ClientResultException>()).Which.Status.Should().Be(400);
        interno.Textos.Select(t => t.Length).Should().Equal(24_000, 12_000, 6_000, 3_000);
    }

    [Fact]
    public async Task Otro400_RelanzaSinReintentar()
    {
        var interno = new ClienteGuion().Lanza(Error(400, "HTTP 400 (content_filter: )"));

        Func<Task<RespuestaEmbedding>> accion = () => Crear(interno).GenerarAsync(Texto, CancellationToken.None);

        await accion.Should().ThrowAsync<ClientResultException>();
        interno.Textos.Should().HaveCount(1);
    }

    [Fact]
    public async Task Error429_RelanzaSinReintentar()
    {
        var interno = new ClienteGuion().Lanza(Error(429, MensajeLongitud));

        Func<Task<RespuestaEmbedding>> accion = () => Crear(interno).GenerarAsync(Texto, CancellationToken.None);

        (await accion.Should().ThrowAsync<ClientResultException>()).Which.Status.Should().Be(429);
        interno.Textos.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("Invalid 'input[0]': maximum input length is 8192 tokens.", true)]
    [InlineData("This model's Maximum Context Length is 8192 tokens.", true)]
    [InlineData("HTTP 400 (content_filter: )", false)]
    [InlineData("", false)]
    public void EsLongitudExcedida_ReconoceSoloLosMensajesDeLongitud(string mensaje, bool esperado)
    {
        EmbeddingsClienteConRecorte.EsLongitudExcedida(mensaje).Should().Be(esperado);
    }
}
