using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class ObtenerMarkdownActivityTests
{
    private static TarifaRegistryLoader CargadorCon(params TarifaIA[] tarifas)
    {
        var mock = new Mock<TarifaRegistryLoader>();
        mock.Setup(c => c.Load()).Returns(new TarifaRegistry { Tarifas = tarifas.ToList() });
        return mock.Object;
    }

    [Fact]
    public async Task Run_DelegaEnElResolutorYTarificaLosConsumos()
    {
        var resolver = new Mock<IMarkdownResolver>();
        resolver.Setup(r => r.ResolverAsync(It.IsAny<NecesidadMarkdown>(), It.IsAny<ContextoMarkdown>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoMarkdown
            {
                Markdown = "# x",
                Paginas = 10,
                Completo = true,
                Fuente = FuenteMarkdown.Layout,
                Consumos = { new ConsumoIA { Actividad = ActividadesIA.Layout, Operacion = "layout.prebuilt-layout", Proveedor = ProveedoresIA.DocumentIntelligence, Modelo = "prebuilt-layout", Paginas = 10 } }
            });

        var actividad = new ObtenerMarkdownActivity(
            new Mock<ILogger<ObtenerMarkdownActivity>>().Object,
            resolver.Object,
            CargadorCon(new TarifaIA { Modelo = "prebuilt-layout", VigenteDesde = new DateTime(2026, 4, 1), EurPorPagina = 0.01m }));

        var input = new ObtenerMarkdownInput
        {
            Necesidad = NecesidadMarkdown.Paginas(3),
            Contexto = new ContextoMarkdown { Sha256 = "sha", NombreDocumento = "d.pdf" }
        };

        var r = await actividad.Run(input);

        resolver.Verify(x => x.ResolverAsync(input.Necesidad, input.Contexto, It.IsAny<CancellationToken>()), Times.Once);
        r.Consumos[0].CosteEur.Should().Be(0.10m);
        r.Consumos[0].TarifaAplicada.Should().Be("prebuilt-layout@2026-04-01");
    }

    [Fact]
    public async Task Run_SinConsumos_NoFalla()
    {
        var resolver = new Mock<IMarkdownResolver>();
        resolver.Setup(r => r.ResolverAsync(It.IsAny<NecesidadMarkdown>(), It.IsAny<ContextoMarkdown>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoMarkdown { Markdown = "# bd", Fuente = FuenteMarkdown.BaseDatos });

        var actividad = new ObtenerMarkdownActivity(
            new Mock<ILogger<ObtenerMarkdownActivity>>().Object, resolver.Object, CargadorCon());

        var r = await actividad.Run(new ObtenerMarkdownInput());

        r.Fuente.Should().Be(FuenteMarkdown.BaseDatos);
        r.Consumos.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_SinCatalogoDeTarifas_NoFalla()
    {
        // Una tarifa que falta nunca puede tumbar un procesamiento.
        var resolver = new Mock<IMarkdownResolver>();
        resolver.Setup(r => r.ResolverAsync(It.IsAny<NecesidadMarkdown>(), It.IsAny<ContextoMarkdown>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoMarkdown
            {
                Markdown = "# x", Paginas = 10, Fuente = FuenteMarkdown.Layout,
                Consumos = { new ConsumoIA { Actividad = ActividadesIA.Layout, Operacion = "layout.prebuilt-layout", Proveedor = ProveedoresIA.DocumentIntelligence, Modelo = "prebuilt-layout", Paginas = 10 } }
            });
        var actividad = new ObtenerMarkdownActivity(new Mock<ILogger<ObtenerMarkdownActivity>>().Object, resolver.Object, CargadorCon());

        var r = await actividad.Run(new ObtenerMarkdownInput());

        r.Consumos[0].CosteEur.Should().BeNull();
        r.Consumos[0].TarifaAplicada.Should().BeNull();
    }

    [Fact]
    public async Task Run_SiElCatalogoRevienta_ElDocumentoSigueProcesandose()
    {
        // Migrado desde TarificacionEnActividadesTests (AB#100255): la resiliencia frente a un
        // catalogo de tarifas caido ya no depende de ExtraerMarkdownLayoutActivity, sino de
        // ObtenerMarkdownActivity, que es quien ahora invoca a TarificadorDeConsumos.
        var resolver = new Mock<IMarkdownResolver>();
        resolver.Setup(r => r.ResolverAsync(It.IsAny<NecesidadMarkdown>(), It.IsAny<ContextoMarkdown>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoMarkdown
            {
                Markdown = "# x", Paginas = 10, Fuente = FuenteMarkdown.Layout,
                Consumos = { new ConsumoIA { Actividad = ActividadesIA.Layout, Operacion = "layout.prebuilt-layout", Proveedor = ProveedoresIA.DocumentIntelligence, Modelo = "prebuilt-layout", Paginas = 10 } }
            });

        var cargadorRoto = new Mock<TarifaRegistryLoader>();
        cargadorRoto.Setup(c => c.Load()).Throws(new InvalidOperationException("base de datos caida"));

        var actividad = new ObtenerMarkdownActivity(new Mock<ILogger<ObtenerMarkdownActivity>>().Object, resolver.Object, cargadorRoto.Object);

        var r = await actividad.Run(new ObtenerMarkdownInput());

        r.Paginas.Should().Be(10);
        r.Consumos[0].CosteEur.Should().BeNull();
    }

    [Fact]
    public async Task PersistirMarkdownActivity_DelegaEnElResolutor()
    {
        var resolver = new Mock<IMarkdownResolver>();
        resolver.Setup(r => r.PersistirAportadoAsync(It.IsAny<PersistirMarkdownInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var actividad = new PersistirMarkdownActivity(new Mock<ILogger<PersistirMarkdownActivity>>().Object, resolver.Object);
        var input = new PersistirMarkdownInput { Sha256 = "sha", Markdown = "# c", Paginas = 3 };

        (await actividad.Run(input)).Should().BeTrue();
        resolver.Verify(x => x.PersistirAportadoAsync(input, It.IsAny<CancellationToken>()), Times.Once);
    }
}
