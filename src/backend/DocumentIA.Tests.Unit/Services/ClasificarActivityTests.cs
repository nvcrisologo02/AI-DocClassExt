#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Services;

public class ClasificarActivityTests
{
    [Fact]
    public async Task Run_WithExpectedType_ReturnsForcedResult_AndSkipsProvider()
    {
        var provider = new Mock<IClasificarDataProvider>(MockBehavior.Strict);
        var logger = new Mock<ILogger<ClasificarActivity>>();
        var sut = new ClasificarActivity(logger.Object, provider.Object);

        var input = new ClasificacionInput
        {
            Entrada = new ContratoEntrada
            {
                Instrucciones = new Instrucciones
                {
                    ExpectedType = "nota.simple.1_4"
                }
            }
        };

        var result = await sut.Run(input);

        result.Modelo.Should().Be("expectedtype-input");
        result.Confianza.Should().Be(1.0);
        result.TipologiaDetectada.Should().Be("nota.simple.1_4");
        provider.Verify(p => p.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_WithoutExpectedType_UsesProvider()
    {
        var providerResult = new ResultadoClasificacion
        {
            Modelo = "di-model-1",
            Confianza = 0.91,
            TipologiaDetectada = "tasacion"
        };

        var provider = new Mock<IClasificarDataProvider>();
        provider
            .Setup(p => p.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerResult);

        var logger = new Mock<ILogger<ClasificarActivity>>();
        var sut = new ClasificarActivity(logger.Object, provider.Object);

        var input = new ClasificacionInput
        {
            Entrada = new ContratoEntrada
            {
                Instrucciones = new Instrucciones
                {
                    ExpectedType = string.Empty
                }
            }
        };

        var result = await sut.Run(input);

        result.Should().BeSameAs(providerResult);
        provider.Verify(p => p.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
