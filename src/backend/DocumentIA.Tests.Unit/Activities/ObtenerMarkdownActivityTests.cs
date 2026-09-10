using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Activities;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

        var r = await actividad.Run(input, CancellationToken.None);

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

        var r = await actividad.Run(new ObtenerMarkdownInput(), CancellationToken.None);

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

        var r = await actividad.Run(new ObtenerMarkdownInput(), CancellationToken.None);

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

        var r = await actividad.Run(new ObtenerMarkdownInput(), CancellationToken.None);

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

        (await actividad.Run(input, CancellationToken.None)).Should().BeTrue();
        resolver.Verify(x => x.PersistirAportadoAsync(input, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Resolutor real con Layout y repositorio controlados. Es lo que hace falta para demostrar la
    /// propagacion del token: con un IMarkdownResolver simulado el test pasaria igual aunque la
    /// actividad siguiera llamando sin token.
    /// </summary>
    private static MarkdownResolver ResolutorReal(
        Mock<ILayoutMarkdownProvider> layout, Mock<IDocumentoRepository> repo)
    {
        var servicios = new ServiceCollection();
        servicios.AddScoped(_ => repo.Object);
        return new MarkdownResolver(
            layout.Object,
            servicios.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MarkdownResolver>.Instance);
    }

    [Fact]
    public async Task Run_CancelacionDelHost_SePropagaEnVezDeEnmascararseComoFalloDeLayout()
    {
        // AB#100251: la actividad recibe el token del host de Functions y lo propaga al resolutor.
        // Sin el llegaba default, el filtro "when (cancellationToken.IsCancellationRequested)" no
        // se cumplia nunca y una cancelacion real -el reciclado del worker a los 30 minutos, un
        // modo de fallo documentado en este proyecto- caia en el catch generico: se registraba
        // como "Layout fallo" y quedaba enmascarada por el respaldo de base de datos.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var layout = new Mock<ILayoutMarkdownProvider>();
        layout.Setup(l => l.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var repo = new Mock<IDocumentoRepository>();
        // Hay respaldo en base de datos: es lo que enmascaraba la cancelacion.
        repo.Setup(r => r.GetBySHA256Async("sha")).ReturnsAsync(new DocumentoEntity
        {
            SHA256 = "sha",
            NormalizacionMarkdownGzip = MarkdownCompression.Compress("# respaldo de bd"),
            MarkdownPaginas = 1,
            MarkdownCompleto = false
        });

        var actividad = new ObtenerMarkdownActivity(
            new Mock<ILogger<ObtenerMarkdownActivity>>().Object, ResolutorReal(layout, repo), CargadorCon());

        var input = new ObtenerMarkdownInput
        {
            Necesidad = NecesidadMarkdown.Completo(),
            Contexto = new ContextoMarkdown { Sha256 = "sha", NombreDocumento = "d.pdf", BlobPath = "c/d.pdf", TotalPaginas = 10 }
        };

        var ejecutar = async () => await actividad.Run(input, cts.Token);

        await ejecutar.Should().ThrowAsync<OperationCanceledException>(
            "una cancelacion real no es un fallo de Layout y no debe taparse con el respaldo de base de datos");
    }

    [Fact]
    public async Task PersistirMarkdownActivity_CancelacionDelHost_SePropagaEnVezDeRegistrarseComoNoPersistido()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.ActualizarMarkdownSiMejoraAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new OperationCanceledException());

        var actividad = new PersistirMarkdownActivity(
            new Mock<ILogger<PersistirMarkdownActivity>>().Object,
            ResolutorReal(new Mock<ILayoutMarkdownProvider>(), repo));

        var ejecutar = async () => await actividad.Run(
            new PersistirMarkdownInput { Sha256 = "sha", Markdown = "# c", Paginas = 3 }, cts.Token);

        await ejecutar.Should().ThrowAsync<OperationCanceledException>(
            "sin el token la cancelacion se registraba como un simple 'no se pudo persistir'");
    }
}
