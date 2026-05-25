using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

public class CatalogoTdnRepositoryTests
{
    [Fact]
    public async Task GetFamiliasTdnActivasAsync_Should_ReturnSortedFamilies_WithDescriptionFallback()
    {
        await using var context = CreateContext();
        SeedCatalogo(context);
        var repository = new CatalogoTdnRepository(context);

        var result = await repository.GetFamiliasTdnActivasAsync();

        result.Select(x => x.Codigo).Should().ContainInOrder("ESCR", "NOTS", "OTRA");
        result.Should().Contain(x => x.Codigo == "NOTS" && x.Descripcion == "Notas simples");
        result.Should().Contain(x => x.Codigo == "OTRA" && x.Descripcion == "Otra familia");
    }

    [Fact]
    public async Task GetSubtiposByFamiliaAsync_Should_FilterByPrefix_AndSort()
    {
        await using var context = CreateContext();
        SeedCatalogo(context);
        var repository = new CatalogoTdnRepository(context);

        var result = await repository.GetSubtiposByFamiliaAsync("nots");

        result.Select(x => x.Codigo).Should().ContainInOrder("NOTS-01", "NOTS-02");
        result.Should().OnlyContain(x => x.Codigo.StartsWith("NOTS-"));
        result.Should().Contain(x => x.Codigo == "NOTS-01" && x.Descripcion == "Nota simple registral");
        result.Should().Contain(x => x.Codigo == "NOTS-02" && x.Descripcion == "Segunda nota simple");
    }

    [Fact]
    public async Task GetSubtiposByFamiliaAsync_WhenFamilyIsMissing_Should_ReturnEmpty()
    {
        await using var context = CreateContext();
        SeedCatalogo(context);
        var repository = new CatalogoTdnRepository(context);

        var result = await repository.GetSubtiposByFamiliaAsync("MISSING");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSubtiposByFamiliaAsync_WhenFamilyIsEmpty_Should_Throw()
    {
        await using var context = CreateContext();
        var repository = new CatalogoTdnRepository(context);

        var act = () => repository.GetSubtiposByFamiliaAsync(" ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"catalogo-tdn-tests-{Guid.NewGuid()}")
            .Options;

        return new DocumentIADbContext(options);
    }

    private static void SeedCatalogo(DocumentIADbContext context)
    {
        var tdn1Nots = new CatalogoTdn1Entity
        {
            Id = 1001,
            Codigo = "NOTS",
            Nombre = "Notas",
            Descripcion = "Notas simples"
        };

        var tdn1Escr = new CatalogoTdn1Entity
        {
            Id = 1002,
            Codigo = "ESCR",
            Nombre = "Escrituras",
            Descripcion = "Documentos notariales"
        };

        var tdn1Otra = new CatalogoTdn1Entity
        {
            Id = 1003,
            Codigo = "OTRA",
            Nombre = "Otra familia"
        };

        context.CatalogoTdn1.AddRange(tdn1Nots, tdn1Escr, tdn1Otra);

        context.CatalogoTdn2.AddRange(
            new CatalogoTdn2Entity
            {
                Id = 2001,
                Codigo = "NOTS-02",
                Nombre = "Segunda nota simple",
                CodigoTdn1 = "NOTS",
                Tdn1Id = 1001
            },
            new CatalogoTdn2Entity
            {
                Id = 2002,
                Codigo = "NOTS-01",
                Nombre = "Nota simple",
                Descripcion = "Nota simple registral",
                CodigoTdn1 = "NOTS",
                Tdn1Id = 1001
            },
            new CatalogoTdn2Entity
            {
                Id = 2003,
                Codigo = "ESCR-01",
                Nombre = "Escritura compraventa",
                CodigoTdn1 = "ESCR",
                Tdn1Id = 1002
            });

        context.SaveChanges();
    }
}