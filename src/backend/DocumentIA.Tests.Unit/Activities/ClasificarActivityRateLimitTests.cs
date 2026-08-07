#nullable enable
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
    [Fact]
    public async Task Run_WhenProviderThrowsRateLimitExhausted_ReturnsFlaggedResultWithoutThrow()
    {
        var provider = new Mock<IClasificarDataProvider>();
        provider
            .Setup(x => x.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RateLimitExhaustedException("cuota agotada"));

        var activity = new ClasificarActivity(
            Mock.Of<ILogger<ClasificarActivity>>(),
            provider.Object);

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
