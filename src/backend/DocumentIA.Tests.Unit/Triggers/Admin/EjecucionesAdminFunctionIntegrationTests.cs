using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Triggers.Admin;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Triggers.Admin;

/// <summary>
/// Cubre ResolverNombresCatalogoAsync de EjecucionesAdminFunction: resolucion de subtipo
/// (CatalogoTdn2) y, cuando no hay subtipo, resolucion directa de familia (CatalogoTdn1)
/// a partir del codigo de tipologia guardado en la ejecucion.
/// El metodo es internal (InternalsVisibleTo a DocumentIA.Tests.Unit ya configurado en
/// DocumentIA.Functions) para poder invocarlo directamente y tipado, sin reflexion sobre
/// una tupla asincrona.
/// </summary>
public class EjecucionesAdminFunctionIntegrationTests
{
    [Fact]
    public async Task ResolverNombresCatalogoAsync_ConCodigoDeSubtipo_ResuelveSubtipoYFamilia()
    {
        await using var db = CreateDbContext();
        var familiaEntity = SembrarFamilia(db, "DECL", "Declaraciones de impuestos, tasas, Seguridad Social, Catastro");
        await db.SaveChangesAsync();
        // Tdn1Id es obligatorio en la FK CatalogoTdn2 -> CatalogoTdn1: se resuelve tras
        // guardar la familia para usar el Id autogenerado por el proveedor InMemory.
        SembrarSubtipo(db, "DECL-08", "Impuesto sociedades: declaracion", "DECL", familiaEntity.Id);
        await db.SaveChangesAsync();

        var function = CreateFunction(db);

        var (subtipo, familia) = await function.ResolverNombresCatalogoAsync("decl.08");

        subtipo.Should().Be("Impuesto sociedades: declaracion");
        familia.Should().Be("Declaraciones de impuestos, tasas, Seguridad Social, Catastro");
    }

    [Fact]
    public async Task ResolverNombresCatalogoAsync_ConSoloCodigoDeFamilia_ResuelveFamiliaSinSubtipo()
    {
        await using var db = CreateDbContext();
        SembrarFamilia(db, "ESIN", "Estudios e informes");
        await db.SaveChangesAsync();

        var function = CreateFunction(db);

        var (subtipo, familia) = await function.ResolverNombresCatalogoAsync("esin");

        subtipo.Should().BeNull();
        familia.Should().Be("Estudios e informes");
    }

    [Fact]
    public async Task ResolverNombresCatalogoAsync_ConCodigoNoResoluble_DevuelveNullSinFallar()
    {
        await using var db = CreateDbContext();
        SembrarFamilia(db, "ESIN", "Estudios e informes");
        await db.SaveChangesAsync();

        var function = CreateFunction(db);

        var (subtipo, familia) = await function.ResolverNombresCatalogoAsync("nota.simple_bal");

        subtipo.Should().BeNull();
        familia.Should().BeNull();
    }

    [Fact]
    public async Task ResolverNombresCatalogoAsync_ConCodigoNuloOVacio_DevuelveNullSinConsultarBd()
    {
        await using var db = CreateDbContext();
        var function = CreateFunction(db);

        var (subtipoNulo, familiaNulo) = await function.ResolverNombresCatalogoAsync(null);
        var (subtipoVacio, familiaVacio) = await function.ResolverNombresCatalogoAsync("   ");

        subtipoNulo.Should().BeNull();
        familiaNulo.Should().BeNull();
        subtipoVacio.Should().BeNull();
        familiaVacio.Should().BeNull();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static CatalogoTdn1Entity SembrarFamilia(DocumentIADbContext db, string codigo, string nombre)
    {
        var entidad = new CatalogoTdn1Entity
        {
            Codigo = codigo,
            Nombre = nombre
        };
        db.CatalogoTdn1.Add(entidad);
        return entidad;
    }

    private static void SembrarSubtipo(DocumentIADbContext db, string codigo, string nombre, string codigoTdn1, int tdn1Id)
    {
        db.CatalogoTdn2.Add(new CatalogoTdn2Entity
        {
            Codigo = codigo,
            Nombre = nombre,
            CodigoTdn1 = codigoTdn1,
            Tdn1Id = tdn1Id
        });
    }

    private static EjecucionesAdminFunction CreateFunction(DocumentIADbContext dbContext)
    {
        return new EjecucionesAdminFunction(
            Mock.Of<IDocumentoEjecucionRepository>(),
            dbContext,
            Mock.Of<ILogger<EjecucionesAdminFunction>>());
    }

    private static DocumentIADbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"ejecuciones-catalogo-tests-{Guid.NewGuid()}")
            .Options;
        return new DocumentIADbContext(options);
    }
}
