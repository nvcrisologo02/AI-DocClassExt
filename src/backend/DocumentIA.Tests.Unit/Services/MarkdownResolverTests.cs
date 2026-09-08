using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentIA.Tests.Unit.Services;

public class MarkdownResolverTests
{
    private readonly Mock<ILayoutMarkdownProvider> _layout = new();
    private readonly Mock<IDocumentoRepository> _repo = new();
    private readonly MarkdownResolver _sut;

    public MarkdownResolverTests()
    {
        // El resolutor es Singleton y abre un scope por operacion de BD (el repositorio es Scoped).
        var servicios = new ServiceCollection();
        servicios.AddScoped(_ => _repo.Object);
        var scopeFactory = servicios.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        _sut = new MarkdownResolver(_layout.Object, scopeFactory, NullLogger<MarkdownResolver>.Instance);

        // Por defecto el UPDATE afecta a una fila.
        _repo.Setup(r => r.ActualizarMarkdownSiMejoraAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(1);
    }

    private static ContextoMarkdown Contexto(int totalPaginas = 14, bool force = false) => new()
    {
        Sha256 = "sha-1",
        Md5 = "md5-1",
        BlobPath = "documents/2026/09/sha-1.pdf",
        NombreDocumento = "doc.pdf",
        TotalPaginas = totalPaginas,
        ForceReprocess = force
    };

    private void LayoutDevuelve(string markdown, int paginas)
        => _layout.Setup(l => l.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtraerMarkdownLayoutResultado
            {
                Markdown = markdown,
                Paginas = paginas,
                Consumos = { new ConsumoIA { Actividad = ActividadesIA.Layout, Operacion = "layout.prebuilt-layout", Modelo = "prebuilt-layout", Paginas = paginas } }
            });

    private void BdTiene(string markdown, int? paginas, bool completo)
        => _repo.Setup(r => r.GetBySHA256Async("sha-1")).ReturnsAsync(new DocumentoEntity
        {
            SHA256 = "sha-1",
            NormalizacionMarkdownGzip = MarkdownCompression.Compress(markdown),
            MarkdownPaginas = paginas,
            MarkdownCompleto = completo
        });

    private void BdVacia()
    {
        _repo.Setup(r => r.GetBySHA256Async("sha-1")).ReturnsAsync((DocumentoEntity?)null);
        _repo.Setup(r => r.GetByMD5Async("md5-1")).ReturnsAsync((DocumentoEntity?)null);
    }

    [Fact]
    public async Task Caller_GanaSiempreYNoSePersiste()
    {
        var ctx = Contexto();
        ctx.MarkdownCaller = "# del caller";
        BdTiene("# de bd", 14, true);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Completo(), ctx);

        r.Markdown.Should().Be("# del caller");
        r.Fuente.Should().Be(FuenteMarkdown.Caller);
        r.Completo.Should().BeTrue();
        r.Persistido.Should().BeFalse();
        _layout.VerifyNoOtherCalls();
        _repo.Verify(x => x.ActualizarMarkdownSiMejoraAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task CacheQueCubre_SeDevuelveSinTocarBdNiLayout()
    {
        var ctx = Contexto();
        ctx.CacheEjecucion = new ResultadoMarkdown { Markdown = "# cache", Paginas = 5, Completo = false, Fuente = FuenteMarkdown.Layout };

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), ctx);

        r.Markdown.Should().Be("# cache");
        r.Fuente.Should().Be(FuenteMarkdown.CacheEjecucion);
        _repo.Verify(x => x.GetBySHA256Async(It.IsAny<string>()), Times.Never);
        _layout.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CacheQueNoCubre_SePasaABd()
    {
        var ctx = Contexto();
        ctx.CacheEjecucion = new ResultadoMarkdown { Markdown = "# cache", Paginas = 3, Completo = false };
        BdTiene("# de bd completo", 14, true);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Completo(), ctx);

        r.Markdown.Should().Be("# de bd completo");
        r.Fuente.Should().Be(FuenteMarkdown.BaseDatos);
        r.Completo.Should().BeTrue();
        r.Consumos.Should().BeEmpty();
        _layout.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BdConMasPaginasQueLasPedidas_SeUsaTalCual()
    {
        BdTiene("# cinco paginas", 5, false);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), Contexto());

        r.Markdown.Should().Be("# cinco paginas");
        r.Paginas.Should().Be(5);
        r.Fuente.Should().Be(FuenteMarkdown.BaseDatos);
        _layout.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BdConCoberturaDesconocida_NoCubre_YSeVaALayout()
    {
        BdTiene("# historico", null, false);
        LayoutDevuelve("# recorte nuevo", 3);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), Contexto());

        r.Fuente.Should().Be(FuenteMarkdown.Layout);
        r.Markdown.Should().Be("# recorte nuevo");
    }

    [Fact]
    public async Task Layout_PideElRangoYPersisteParcial()
    {
        BdVacia();
        LayoutDevuelve("# tres", 3);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), Contexto(totalPaginas: 14));

        _layout.Verify(l => l.ExtraerMarkdownAsync(
            It.Is<ExtraerMarkdownLayoutInput>(i => i.PaginasSolicitadas == 3 && i.BlobPath == "documents/2026/09/sha-1.pdf"),
            It.IsAny<CancellationToken>()), Times.Once);
        r.Paginas.Should().Be(3);
        r.Completo.Should().BeFalse();
        r.Persistido.Should().BeTrue();
        r.Consumos.Should().HaveCount(1);
        _repo.Verify(x => x.ActualizarMarkdownSiMejoraAsync("sha-1", It.IsAny<byte[]>(), It.IsAny<string>(), 3, false, false), Times.Once);
    }

    [Fact]
    public async Task Layout_Completo_SePersisteComoCompleto()
    {
        BdVacia();
        LayoutDevuelve("# entero", 14);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Completo(), Contexto());

        _layout.Verify(l => l.ExtraerMarkdownAsync(It.Is<ExtraerMarkdownLayoutInput>(i => i.PaginasSolicitadas == null), It.IsAny<CancellationToken>()), Times.Once);
        r.Completo.Should().BeTrue();
        r.Paginas.Should().Be(14);
        _repo.Verify(x => x.ActualizarMarkdownSiMejoraAsync("sha-1", It.IsAny<byte[]>(), It.IsAny<string>(), 14, true, false), Times.Once);
    }

    [Fact]
    public async Task Layout_RangoQueSuperaElDocumento_SeInfiereCompleto()
    {
        // Documento de 2 paginas, se piden 3: DI devuelve 2 y eso es el documento entero.
        BdVacia();
        LayoutDevuelve("# dos", 2);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), Contexto(totalPaginas: 2));

        r.Completo.Should().BeTrue();
        r.Paginas.Should().Be(2);
    }

    [Fact]
    public async Task LayoutFalla_ConAlgoEnBd_SeUsaComoFallbackAunqueNoCubra()
    {
        BdTiene("# viejo de 3", 3, false);
        _layout.Setup(l => l.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DI caido"));

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Completo(), Contexto());

        r.Markdown.Should().Be("# viejo de 3");
        r.Fuente.Should().Be(FuenteMarkdown.BaseDatos);
        r.Completo.Should().BeFalse();
        r.Paginas.Should().Be(3);
    }

    [Fact]
    public async Task LayoutVacio_SinNadaEnBd_DevuelveNingunaConSusConsumos()
    {
        BdVacia();
        LayoutDevuelve("   ", 0);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), Contexto());

        r.TieneContenido.Should().BeFalse();
        r.Fuente.Should().Be(FuenteMarkdown.Ninguna);
        r.Consumos.Should().HaveCount(1, "el layout se pago aunque no devolviera texto");
        r.Persistido.Should().BeFalse();
    }

    [Fact]
    public async Task ForceReprocess_IgnoraBdAlLeer_PeroLaConservaComoFallback()
    {
        BdTiene("# completo viejo", 14, true);
        _layout.Setup(l => l.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DI caido"));

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), Contexto(force: true));

        _layout.Verify(l => l.ExtraerMarkdownAsync(It.IsAny<ExtraerMarkdownLayoutInput>(), It.IsAny<CancellationToken>()), Times.Once);
        r.Fuente.Should().Be(FuenteMarkdown.BaseDatos);
        r.Markdown.Should().Be("# completo viejo");
    }

    [Fact]
    public async Task ForceReprocess_EscribeConForzar()
    {
        BdTiene("# completo viejo", 14, true);
        LayoutDevuelve("# tres nuevas", 3);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), Contexto(force: true));

        r.Fuente.Should().Be(FuenteMarkdown.Layout);
        _repo.Verify(x => x.ActualizarMarkdownSiMejoraAsync("sha-1", It.IsAny<byte[]>(), It.IsAny<string>(), 3, false, true), Times.Once);
    }

    [Fact]
    public async Task BdPorMd5_CuandoNoHaySha()
    {
        _repo.Setup(r => r.GetBySHA256Async("sha-1")).ReturnsAsync((DocumentoEntity?)null);
        _repo.Setup(r => r.GetByMD5Async("md5-1")).ReturnsAsync(new DocumentoEntity
        {
            SHA256 = "otro", NormalizacionMarkdownGzip = MarkdownCompression.Compress("# por md5"), MarkdownPaginas = 14, MarkdownCompleto = true
        });

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Completo(), Contexto());

        r.Markdown.Should().Be("# por md5");
        r.Fuente.Should().Be(FuenteMarkdown.BaseDatos);
    }

    [Fact]
    public async Task ErrorDeBdAlLeer_SeTrataComoSinPersistido()
    {
        _repo.Setup(r => r.GetBySHA256Async(It.IsAny<string>())).ThrowsAsync(new TimeoutException("sql"));
        LayoutDevuelve("# de layout", 3);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), Contexto());

        r.Fuente.Should().Be(FuenteMarkdown.Layout);
    }

    [Fact]
    public async Task ErrorDeBdAlEscribir_NoTumbaLaResolucion()
    {
        BdVacia();
        _repo.Setup(r => r.ActualizarMarkdownSiMejoraAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new TimeoutException("sql"));
        LayoutDevuelve("# de layout", 3);

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), Contexto());

        r.Markdown.Should().Be("# de layout");
        r.Persistido.Should().BeFalse();
    }

    [Fact]
    public async Task SinFuentesDeDocumento_NoLlamaALayout()
    {
        var ctx = Contexto();
        ctx.BlobPath = null;
        ctx.DocumentoBase64 = null;
        BdVacia();

        var r = await _sut.ResolverAsync(NecesidadMarkdown.Paginas(3), ctx);

        r.Fuente.Should().Be(FuenteMarkdown.Ninguna);
        _layout.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PersistirAportado_UsaLaMismaRegla()
    {
        var ok = await _sut.PersistirAportadoAsync(new PersistirMarkdownInput
        {
            Sha256 = "sha-1", Markdown = "# del clasificador", Paginas = 3, Completo = false
        });

        ok.Should().BeTrue();
        _repo.Verify(x => x.ActualizarMarkdownSiMejoraAsync("sha-1", It.IsAny<byte[]>(), It.IsAny<string>(), 3, false, false), Times.Once);
    }

    [Fact]
    public async Task PersistirAportado_SinShaOSinTexto_NoEscribe()
    {
        (await _sut.PersistirAportadoAsync(new PersistirMarkdownInput { Sha256 = null, Markdown = "# x", Paginas = 1 })).Should().BeFalse();
        (await _sut.PersistirAportadoAsync(new PersistirMarkdownInput { Sha256 = "sha-1", Markdown = " ", Paginas = 1 })).Should().BeFalse();
        _repo.Verify(x => x.ActualizarMarkdownSiMejoraAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }
}
