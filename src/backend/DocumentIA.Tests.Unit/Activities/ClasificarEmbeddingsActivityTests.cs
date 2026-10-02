#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Activities;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Classification;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class ClasificarEmbeddingsActivityTests
{
    private sealed class TarifasFijas : TarifaRegistryLoader
    {
        public override TarifaRegistry Load() => new()
        {
            Tarifas = { new TarifaIA { Modelo = "text-embedding-3-large-030358", VigenteDesde = new DateTime(2026, 4, 1), EurEntradaPor1M = 0.112m } }
        };
    }

    [Fact]
    public async Task Ejecutar_TarificaElConsumoDelProveedor()
    {
        var provider = new Mock<IEmbeddingsClasificarProvider>();
        provider.Setup(p => p.ClasificarAsync(It.IsAny<ClasificarEmbeddingsInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoEmbeddings
            {
                Decision = DecisionesEmbeddings.DerivarGpt,
                Motivo = MotivosEmbeddings.Sombra,
                Consumos = new List<ConsumoIA>
                {
                    new() { Actividad = ActividadesIA.Clasificar, Operacion = UsoEmbeddingsMapper.Operacion, Modelo = "text-embedding-3-large-030358", TokensEntrada = 500_000 }
                }
            });
        var activity = new ClasificarEmbeddingsActivity(NullLogger<ClasificarEmbeddingsActivity>.Instance, provider.Object, new TarifasFijas());

        var r = await activity.EjecutarAsync(new ClasificarEmbeddingsInput { Texto = "x" }, CancellationToken.None);

        r.Consumos.Should().ContainSingle().Which.CosteEur.Should().Be(0.056m);
    }

    [Fact]
    public async Task Ejecutar_ElProveedorLanza_DevuelveDerivarGptConErrorSinPropagar()
    {
        var provider = new Mock<IEmbeddingsClasificarProvider>();
        provider.Setup(p => p.ClasificarAsync(It.IsAny<ClasificarEmbeddingsInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fallo inesperado"));
        var activity = new ClasificarEmbeddingsActivity(NullLogger<ClasificarEmbeddingsActivity>.Instance, provider.Object, new TarifasFijas());

        var r = await activity.EjecutarAsync(new ClasificarEmbeddingsInput { Texto = "x" }, CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        r.Motivo.Should().Be(MotivosEmbeddings.Error);
        r.Error.Should().Contain("fallo inesperado");
    }

    [Fact]
    public async Task Ejecutar_InputNulo_DevuelveOmitido()
    {
        var provider = new Mock<IEmbeddingsClasificarProvider>();
        var activity = new ClasificarEmbeddingsActivity(NullLogger<ClasificarEmbeddingsActivity>.Instance, provider.Object, new TarifasFijas());

        var r = await activity.EjecutarAsync(null, CancellationToken.None);

        r.Decision.Should().Be(DecisionesEmbeddings.Omitido);
        provider.Verify(p => p.ClasificarAsync(It.IsAny<ClasificarEmbeddingsInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
