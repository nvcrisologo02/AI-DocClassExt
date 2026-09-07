#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Activities;
using DocumentIA.Functions.Services.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Activities;

public class ClasificarActivityRateLimitTests
{
    /// <summary>
    /// Cargador de tarifas vacio: estos tests no verifican coste, solo necesitan
    /// satisfacer la dependencia de la actividad (AB#100230).
    /// </summary>
    private static TarifaRegistryLoader CargadorDeTarifasVacio()
    {
        var mock = new Mock<TarifaRegistryLoader>();
        mock.Setup(c => c.Load()).Returns(new TarifaRegistry());
        return mock.Object;
    }

    [Fact]
    public async Task Run_WhenProviderThrowsRateLimitExhausted_ReturnsFlaggedResultWithoutThrow()
    {
        var provider = new Mock<IClasificarDataProvider>();
        provider
            .Setup(x => x.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RateLimitExhaustedException("cuota agotada"));

        var activity = new ClasificarActivity(
            Mock.Of<ILogger<ClasificarActivity>>(),
            provider.Object,
            CargadorDeTarifasVacio());

        var input = new ClasificacionInput
        {
            Entrada = new ContratoEntrada
            {
                Documento = new Documento { Name = "doc.pdf", Content = new ContenidoDocumento { Base64 = string.Empty } },
                Instrucciones = new Instrucciones { ExpectedType = string.Empty }
            }
        };

        var result = await activity.Run(input);

        result.RateLimitExcedido.Should().BeTrue();
        result.FallbackRazon.Should().Be("rate_limit_exhausted");
        result.TipologiaDetectada.Should().Be("Desconocido");
        result.Confianza.Should().Be(0);
    }
}
