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

    private static ExtraerMarkdownLayoutResultado ResultadoLayoutConConsumo() => new()
    {
        Modelo = "prebuilt-layout",
        Paginas = 10,
        Consumos =
        {
            new ConsumoIA
            {
                Actividad = ActividadesIA.Layout,
                Operacion = "layout.prebuilt-layout",
                Proveedor = ProveedoresIA.DocumentIntelligence,
                Modelo = "prebuilt-layout",
                Paginas = 10
            }
        }
    };

    [Fact]
    public async Task ExtraerMarkdownLayoutActivity_TarificaElConsumoDelProveedor()
    {
        var providerMock = new Mock<ILayoutMarkdownProvider>();
        providerMock
            .Setup(p => p.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>()))
            .ReturnsAsync(ResultadoLayoutConConsumo());

        var actividad = new ExtraerMarkdownLayoutActivity(
            new Mock<ILogger<ExtraerMarkdownLayoutActivity>>().Object,
            providerMock.Object,
            CargadorCon(new TarifaIA
            {
                Modelo = "prebuilt-layout",
                VigenteDesde = new DateTime(2026, 4, 1),
                EurPorPagina = 0.01m
            }));

        var resultado = await actividad.Run(new ExtraerMarkdownLayoutInput());

        resultado.Consumos[0].CosteEur.Should().Be(0.10m);
        resultado.Consumos[0].TarifaAplicada.Should().Be("prebuilt-layout@2026-04-01");
    }

    [Fact]
    public async Task ExtraerMarkdownLayoutActivity_SinCatalogoDeTarifas_NoFalla()
    {
        // Una tarifa que falta nunca puede tumbar un procesamiento.
        var providerMock = new Mock<ILayoutMarkdownProvider>();
        providerMock
            .Setup(p => p.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>()))
            .ReturnsAsync(ResultadoLayoutConConsumo());

        var actividad = new ExtraerMarkdownLayoutActivity(
            new Mock<ILogger<ExtraerMarkdownLayoutActivity>>().Object,
            providerMock.Object,
            CargadorCon());

        var resultado = await actividad.Run(new ExtraerMarkdownLayoutInput());

        resultado.Consumos[0].CosteEur.Should().BeNull();
        resultado.Paginas.Should().Be(10);
    }

    [Fact]
    public async Task ExtraerMarkdownLayoutActivity_SiElCatalogoRevienta_ElDocumentoSigueProcesandose()
    {
        var providerMock = new Mock<ILayoutMarkdownProvider>();
        providerMock
            .Setup(p => p.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>()))
            .ReturnsAsync(ResultadoLayoutConConsumo());

        var cargadorRoto = new Mock<TarifaRegistryLoader>();
        cargadorRoto.Setup(c => c.Load()).Throws(new InvalidOperationException("base de datos caida"));

        var actividad = new ExtraerMarkdownLayoutActivity(
            new Mock<ILogger<ExtraerMarkdownLayoutActivity>>().Object,
            providerMock.Object,
            cargadorRoto.Object);

        var resultado = await actividad.Run(new ExtraerMarkdownLayoutInput());

        resultado.Paginas.Should().Be(10);
        resultado.Consumos[0].CosteEur.Should().BeNull();
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
