using DocumentIA.Data.Repositories;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Repositories;

// La calidad describe cuanta confianza merece el resultado, a diferencia de
// EstadoFinal, que describe si la tuberia llego al final. Son dimensiones
// independientes: una ejecucion puede completarse con confianza baja.
public class CalidadEjecucionTests
{
    [Theory]
    [InlineData(1.00, CalidadEjecucion.Ok)]
    [InlineData(0.90, CalidadEjecucion.Ok)]
    [InlineData(0.85, CalidadEjecucion.Ok)]
    [InlineData(0.8499, CalidadEjecucion.Revision)]
    [InlineData(0.75, CalidadEjecucion.Revision)]
    [InlineData(0.70, CalidadEjecucion.Revision)]
    [InlineData(0.6999, CalidadEjecucion.Error)]
    [InlineData(0.20, CalidadEjecucion.Error)]
    [InlineData(0.00, CalidadEjecucion.Error)]
    public void Clasificar_AplicaLosUmbrales(double confianza, string esperado)
    {
        CalidadEjecucion.Clasificar(confianza).Should().Be(esperado);
    }

    // Los umbrales son los mismos que usa el motor al calcular EstadoCalidad; si
    // divergieran, el badge del detalle y el recuento del panel se contradirian.
    [Fact]
    public void Umbrales_CoincidenConLosDelMotorDeConfianza()
    {
        CalidadEjecucion.UmbralOk.Should().Be(0.85);
        CalidadEjecucion.UmbralRevision.Should().Be(0.70);
    }
}
