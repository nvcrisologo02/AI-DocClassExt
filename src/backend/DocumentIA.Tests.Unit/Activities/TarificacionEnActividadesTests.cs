#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

/// <summary>
/// AB#100230: la tarifa se aplica en la actividad y no en el orquestador, porque el
/// catalogo esta en base de datos y el orquestador debe seguir siendo determinista.
/// </summary>
public class TarificacionEnActividadesTests
{
    private static TarifaRegistryLoader CargadorCon(params TarifaIA[] tarifas)
    {
        var mock = new Mock<TarifaRegistryLoader>();
        mock.Setup(c => c.Load()).Returns(new TarifaRegistry { Tarifas = tarifas.ToList() });
        return mock.Object;
    }

    [Fact]
    public async Task ClasificarActivity_ForzadaPorExpectedType_NoRegistraConsumo()
    {
        // No se llama a ningun servicio: no hay nada que tarificar.
        var providerMock = new Mock<IClasificarDataProvider>();

        var actividad = new ClasificarActivity(
            new Mock<ILogger<ClasificarActivity>>().Object,
            providerMock.Object,
            CargadorCon());

        var input = new ClasificacionInput
        {
            Entrada = new ContratoEntrada
            {
                Instrucciones = new Instrucciones { ExpectedType = "nota-simple" }
            }
        };

        var resultado = await actividad.Run(input);

        resultado.Consumos.Should().BeEmpty();
        providerMock.Verify(
            p => p.ClasificarAsync(It.IsAny<ClasificacionInput>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
