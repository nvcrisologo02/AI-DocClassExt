using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

public class DocumentoEjecucionRepositoryFiltroTests
{
    private static readonly DateTime Base = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPagedAsync_Should_AcotarPorRangoDeFechas()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var (items, total) = await repo.GetPagedAsync(
            Filtro(Base, Base.AddDays(1)), page: 1, pageSize: 25);

        total.Should().Be(2);
        items.Should().OnlyContain(e => e.FechaEjecucion < Base.AddDays(1));
    }

    [Fact]
    public async Task GetPagedAsync_Should_DevolverTotalDelFiltroNoDeLaPagina()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var (items, total) = await repo.GetPagedAsync(
            Filtro(Base, Base.AddDays(10)), page: 1, pageSize: 2);

        items.Should().HaveCount(2);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPagedAsync_Should_OrdenarPorFechaDescendente()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var (items, _) = await repo.GetPagedAsync(
            Filtro(Base, Base.AddDays(10)), page: 1, pageSize: 25);

        items.Select(e => e.FechaEjecucion).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetPagedAsync_Should_DevolverLaSegundaPagina()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var (pagina1, _) = await repo.GetPagedAsync(Filtro(Base, Base.AddDays(10)), 1, 2);
        var (pagina2, _) = await repo.GetPagedAsync(Filtro(Base, Base.AddDays(10)), 2, 2);

        pagina2.Should().HaveCount(2);
        pagina2.Select(e => e.Id).Should().NotIntersectWith(pagina1.Select(e => e.Id));
    }

    [Fact]
    public async Task GetPagedAsync_Should_FiltrarPorTipologia()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Tipologia = "NOTS";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(2);
        items.Should().OnlyContain(e => e.Tipologia == "NOTS");
    }

    [Fact]
    public async Task GetPagedAsync_Should_FiltrarPorEstadoNormalizado()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Estado = "ERROR";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(2, "hay una ejecucion 'Error' y otra 'Fallido'");
        items.Should().OnlyContain(e => EstadoEjecucion.Error.Contains(e.EstadoFinal));
    }

    [Fact]
    public async Task GetPagedAsync_Should_FiltrarPorFlujo()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Flujo = "Clasificacion";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(2);
        items.Should().OnlyContain(e => e.ClassificationOnly);
    }

    [Fact]
    public async Task GetPagedAsync_Should_BuscarPorGuidExacto()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Busqueda = "11111111-1111-1111-1111-111111111111";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(1);
        items[0].EjecucionGuid.Should().Be("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public async Task GetPagedAsync_Should_BuscarPorGuidSinGuiones()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Busqueda = "11111111111111111111111111111111";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(1, "un GUID valido pero no canonico debe normalizarse antes de comparar, no caer al fallback de nombre");
        items[0].EjecucionGuid.Should().Be("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public async Task GetPagedAsync_Should_BuscarPorFragmentoDeNombreDeDocumento()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Busqueda = "escritura";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(1);
        items[0].NombreDocumento.Should().Contain("escritura");
    }

    [Fact]
    public async Task GetPagedAsync_Should_FiltrarPorSubmittedByFragmento()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.SubmittedBy = "integracion";

        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(4, "solo el documento 1 tiene 'batch-integracion' en SubmittedBy");
        // El DTO expone el SubmittedBy ya coalescido (propio de la ejecucion o, en su defecto,
        // el del documento), que es exactamente el valor sobre el que filtra el repositorio.
        items.Should().OnlyContain(e => e.SubmittedBy!.Contains("integracion"));
    }

    [Fact]
    public async Task GetPagedAsync_Should_NoRomperCuandoElDocumentoTieneSubmittedByNulo()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        // doc2 (ejecucion 3) tiene SubmittedBy nulo; filtrar no debe reventar
        // ni devolverlo como falso positivo.
        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.SubmittedBy = "compraventa";

        Func<Task> act = () => repo.GetPagedAsync(filtro, 1, 25);

        await act.Should().NotThrowAsync("un SubmittedBy nulo en el documento no debe reventar la consulta");
        var (_, total) = await repo.GetPagedAsync(filtro, 1, 25);
        total.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_Should_PriorizarSubmittedByPropioDeLaEjecucionSobreElDelDocumento()
    {
        // El mismo documento deduplicado puede reprocesarse desde otro origen: el
        // SubmittedBy de la ejecucion (si existe) debe ganar sobre el del documento.
        await using var context = CreateContext();
        var doc = new DocumentoEntity { Id = 1, NombreArchivo = "doc.pdf", SHA256 = "a", SubmittedBy = "batch-integracion" };
        context.Documentos.Add(doc);
        var ejecucion = Ejecucion(1, "11111111-1111-1111-1111-111111111111", Base, "NOTS", "OK", docId: 1, soloClasificacion: true);
        ejecucion.SubmittedBy = "usuario-manual";
        context.DocumentoEjecuciones.Add(ejecucion);
        context.SaveChanges();
        var repo = new DocumentoEjecucionRepository(context);

        var filtroPorPropio = Filtro(Base, Base.AddDays(1));
        filtroPorPropio.SubmittedBy = "manual";
        var (itemsPropio, totalPropio) = await repo.GetPagedAsync(filtroPorPropio, 1, 25);

        var filtroPorDocumento = Filtro(Base, Base.AddDays(1));
        filtroPorDocumento.SubmittedBy = "integracion";
        var (_, totalDocumento) = await repo.GetPagedAsync(filtroPorDocumento, 1, 25);

        totalPropio.Should().Be(1, "el SubmittedBy propio de la ejecucion debe encontrarse");
        itemsPropio[0].SubmittedBy.Should().Be("usuario-manual");
        totalDocumento.Should().Be(0, "el SubmittedBy propio gana: el del documento ya no debe usarse cuando hay uno propio");
    }

    [Fact]
    public async Task GetPagedAsync_Should_CaerAlSubmittedByDelDocumentoCuandoLaEjecucionNoLoTiene()
    {
        await using var context = CreateContext();
        var doc = new DocumentoEntity { Id = 1, NombreArchivo = "doc.pdf", SHA256 = "a", SubmittedBy = "batch-integracion" };
        context.Documentos.Add(doc);
        var ejecucion = Ejecucion(1, "11111111-1111-1111-1111-111111111111", Base, "NOTS", "OK", docId: 1, soloClasificacion: true);
        // Ejecucion sin SubmittedBy propio: debe caer al del documento (COALESCE).
        context.DocumentoEjecuciones.Add(ejecucion);
        context.SaveChanges();
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(1));
        filtro.SubmittedBy = "integracion";
        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(1);
        // El DTO del listado expone el SubmittedBy efectivo (COALESCE ejecucion -> documento),
        // que es el que la API ha devuelto siempre; antes el coalesce lo hacia la Function
        // sobre la entidad, donde este campo llegaba a null.
        items[0].SubmittedBy.Should().Be("batch-integracion", "sin valor propio, el efectivo viene del documento via COALESCE");
    }

    [Fact]
    public async Task GetPagedAsync_Should_EncontrarConElMismoFiltroTantoElValorPropioComoElDelDocumento()
    {
        await using var context = CreateContext();
        var doc1 = new DocumentoEntity { Id = 1, NombreArchivo = "doc1.pdf", SHA256 = "a", SubmittedBy = "equipo-test-uno" };
        var doc2 = new DocumentoEntity { Id = 2, NombreArchivo = "doc2.pdf", SHA256 = "b", SubmittedBy = "otro-origen" };
        context.Documentos.AddRange(doc1, doc2);

        var ejecucionConPropio = Ejecucion(1, "11111111-1111-1111-1111-111111111111", Base, "NOTS", "OK", docId: 2, soloClasificacion: true);
        ejecucionConPropio.SubmittedBy = "equipo-test-dos";
        var ejecucionSinPropio = Ejecucion(2, "22222222-2222-2222-2222-222222222222", Base, "NOTS", "OK", docId: 1, soloClasificacion: true);

        context.DocumentoEjecuciones.AddRange(ejecucionConPropio, ejecucionSinPropio);
        context.SaveChanges();
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(1));
        filtro.SubmittedBy = "equipo-test";
        var (items, total) = await repo.GetPagedAsync(filtro, 1, 25);

        total.Should().Be(2, "debe encontrar tanto la ejecucion con valor propio como la que cae al valor del documento");
        items.Select(i => i.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task GetPagedAsync_Should_NoRomperCuandoLaEjecucionNoTieneDocumentoAsociado()
    {
        await using var context = CreateContext();
        Seed(context);
        // Ejecucion huerfana: DocumentoId que no existe en Documentos. Solo afecta a
        // este contexto en memoria (CreateContext crea una base aislada por test), no
        // altera los totales del resto de pruebas.
        context.DocumentoEjecuciones.Add(
            Ejecucion(6, "66666666-6666-6666-6666-666666666666", Base.AddDays(1), "NOTS", "OK", docId: 999, soloClasificacion: true));
        context.SaveChanges();
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.SubmittedBy = "integracion";

        Func<Task> act = () => repo.GetPagedAsync(filtro, 1, 25);

        await act.Should().NotThrowAsync("una ejecucion sin documento asociado no debe reventar el filtro por SubmittedBy");
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_RespetarElMismoFiltroQueElListadoConSubmittedBy()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.SubmittedBy = "integracion";

        var agregados = await repo.GetAgregadosAsync(filtro);
        var (_, totalListado) = await repo.GetPagedAsync(filtro, 1, 25);

        agregados.TotalEjecuciones.Should().Be(totalListado,
            "la cabecera de KPIs y la tabla deben describir el mismo conjunto tambien con SubmittedBy");
        agregados.Serie.Sum(p => p.Total).Should().Be(totalListado);
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_ContarPorCategoriaDeEstado()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var r = await repo.GetAgregadosAsync(Filtro(Base, Base.AddDays(10)));

        r.TotalEjecuciones.Should().Be(5);
        r.Ok.Should().Be(2, "OK y Completado cuentan como exito");
        r.Error.Should().Be(2, "Error y Fallido cuentan como error");
        r.Revision.Should().Be(1);
    }

    // Una ejecucion sin contenido del documento es un fallo de proceso: debe
    // sumar al agregado de error y salir en el filtro Estado=ERROR igual que
    // Error/Fallido, no quedarse fuera como estado neutro.
    [Fact]
    public async Task GetAgregadosAsync_Should_ContarSinContenidoDocumentoComoError()
    {
        await using var context = CreateContext();
        Seed(context);
        context.DocumentoEjecuciones.Add(
            Ejecucion(6, "66666666-6666-6666-6666-666666666666", Base.AddDays(1), "NOTS", "SIN_CONTENIDO_DOCUMENTO", docId: 1, soloClasificacion: true));
        context.SaveChanges();
        var repo = new DocumentoEjecucionRepository(context);

        var r = await repo.GetAgregadosAsync(Filtro(Base, Base.AddDays(10)));
        var filtroError = Filtro(Base, Base.AddDays(10));
        filtroError.Estado = "ERROR";
        var (items, total) = await repo.GetPagedAsync(filtroError, 1, 25);

        r.Error.Should().Be(3, "Error, Fallido y SIN_CONTENIDO_DOCUMENTO cuentan como error");
        total.Should().Be(3, "el filtro Estado=ERROR debe incluir las ejecuciones sin contenido de documento");
        items.Should().Contain(e => e.EstadoFinal == "SIN_CONTENIDO_DOCUMENTO");
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_IncluirDiasSinEjecucionesAcero()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var r = await repo.GetAgregadosAsync(Filtro(Base, Base.AddDays(7)));

        r.Serie.Should().HaveCount(7, "un punto por dia natural del rango, con huecos incluidos");
        r.Serie.Select(p => p.Fecha).Should().BeInAscendingOrder();
        r.Serie.Should().Contain(p => p.Fecha.Date == Base.AddDays(1).Date && p.Total == 0);
        r.Serie.Single(p => p.Fecha.Date == Base.Date).Total.Should().Be(2);
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_IncluirElDiaEnCursoCuandoHastaNoEsMedianoche()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(3).AddHours(15));

        var r = await repo.GetAgregadosAsync(filtro);
        var (_, totalListado) = await repo.GetPagedAsync(filtro, 1, 25);

        r.Serie.Should().HaveCount(4, "el dia en curso debe entrar en la serie aunque Hasta no sea medianoche");
        r.Serie.Select(p => p.Fecha).Should().BeInAscendingOrder();
        r.Serie.Last().Fecha.Date.Should().Be(Base.AddDays(3).Date);
        r.Serie.Sum(p => p.Total).Should().Be(totalListado,
            "la serie debe seguir sumando el total aunque Hasta no sea medianoche");
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_DevolverUnSoloDiaCuandoDesdeYHastaCoincidenElMismoDia()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddHours(12));

        var r = await repo.GetAgregadosAsync(filtro);
        var (_, totalListado) = await repo.GetPagedAsync(filtro, 1, 25);

        r.Serie.Should().HaveCount(1, "Desde y Hasta caen el mismo dia natural");
        r.Serie.Single().Fecha.Date.Should().Be(Base.Date);
        r.Serie.Sum(p => p.Total).Should().Be(totalListado,
            "la serie no debe salir vacia cuando el rango cae en un solo dia con ejecuciones");
    }

    [Fact]
    public async Task GetAgregadosAsync_Should_RespetarElMismoFiltroQueElListado()
    {
        await using var context = CreateContext();
        Seed(context);
        var repo = new DocumentoEjecucionRepository(context);

        var filtro = Filtro(Base, Base.AddDays(10));
        filtro.Tipologia = "NOTS";

        var agregados = await repo.GetAgregadosAsync(filtro);
        var (_, totalListado) = await repo.GetPagedAsync(filtro, 1, 25);

        agregados.TotalEjecuciones.Should().Be(totalListado,
            "la cabecera de KPIs y la tabla deben describir el mismo conjunto");
        agregados.Serie.Sum(p => p.Total).Should().Be(totalListado,
            "la serie debe sumar exactamente el total del filtro");
    }

    private static EjecucionFiltro Filtro(DateTime desde, DateTime hasta) =>
        new() { Desde = desde, Hasta = hasta };

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"ejecuciones-filtro-tests-{Guid.NewGuid()}")
            .Options;
        return new DocumentIADbContext(options);
    }

    private static void Seed(DocumentIADbContext context)
    {
        // doc2 se deja con SubmittedBy nulo a proposito: cubre documentos sin
        // trazabilidad de origen registrada (ejecuciones previas a AB#99966).
        var doc1 = new DocumentoEntity { Id = 1, NombreArchivo = "nota-simple.pdf", SHA256 = "a", SubmittedBy = "batch-integracion" };
        var doc2 = new DocumentoEntity { Id = 2, NombreArchivo = "escritura-compraventa.pdf", SHA256 = "b", SubmittedBy = null };
        context.Documentos.AddRange(doc1, doc2);

        context.DocumentoEjecuciones.AddRange(
            Ejecucion(1, "11111111-1111-1111-1111-111111111111", Base,             "NOTS", "OK",        docId: 1, soloClasificacion: true),
            Ejecucion(2, "22222222-2222-2222-2222-222222222222", Base.AddHours(5), "NOTS", "Error",     docId: 1, soloClasificacion: true),
            Ejecucion(3, "33333333-3333-3333-3333-333333333333", Base.AddDays(2),  "ESCR", "Fallido",   docId: 2, soloClasificacion: false),
            Ejecucion(4, "44444444-4444-4444-4444-444444444444", Base.AddDays(4),  "ESCR", "Completado", docId: 1, soloClasificacion: false),
            Ejecucion(5, "55555555-5555-5555-5555-555555555555", Base.AddDays(6),  "CERA", "REVISION",  docId: 1, soloClasificacion: false));

        context.SaveChanges();
    }

    private static DocumentoEjecucionEntity Ejecucion(
        int id, string guid, DateTime fecha, string tipologia, string estado, int docId, bool soloClasificacion) =>
        new()
        {
            Id = id,
            DocumentoId = docId,
            EjecucionGuid = guid,
            FechaEjecucion = fecha,
            Tipologia = tipologia,
            EstadoFinal = estado,
            ClassificationOnly = soloClasificacion,
            ConfianzaGlobal = 0.9,
            DuracionTotalMs = 1000
        };
}
