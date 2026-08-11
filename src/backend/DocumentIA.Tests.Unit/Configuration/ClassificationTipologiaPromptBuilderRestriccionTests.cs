#nullable enable
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Configuration;

public class ClassificationTipologiaPromptBuilderRestriccionTests : IDisposable
{
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly DocumentIADbContext _db;
    private readonly ServiceProvider _serviceProvider;
    private readonly ClassificationTipologiaPromptBuilder _builder;

    public ClassificationTipologiaPromptBuilderRestriccionTests()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"builder-restriccion-{Guid.NewGuid()}")
            .Options;
        _db = new DocumentIADbContext(options);

        // Catálogos TDN
        _db.CatalogoTdn2.Add(new CatalogoTdn2Entity { Codigo = "ESIN-40", Nombre = "Nota simple", CodigoTdn1 = "REGI" });
        _db.CatalogoTdn2.Add(new CatalogoTdn2Entity { Codigo = "CONT-10", Nombre = "Contrato arrendamiento", CodigoTdn1 = "CONT" });
        _db.SaveChanges();

        // Tipologías publicadas: dos en familia REGI, una en familia CONT
        var tipologias = new List<TipologiaEntity>
        {
            CrearTipologia("nota-simple", "REGI", "ESIN-40", "Nota simple registral"),
            CrearTipologia("REGI-02", "REGI", "ESIN-40", "Otra tipología registral"),
            CrearTipologia("CONT-01", "CONT", "CONT-10", "Contrato de arrendamiento")
        };

        var tipologiaRepoMock = new Mock<ITipologiaRepository>();
        tipologiaRepoMock.Setup(r => r.GetAllPublishedAsync()).ReturnsAsync(tipologias);

        // GetFamiliasTdnActivasAsync devuelve TdnCatalogItem (record Codigo/Nombre/Descripcion),
        // no CatalogoTdn1Entity — ver DocumentIA.Data.Repositories.ICatalogoTdnRepository.
        var familias = new List<TdnCatalogItem>
        {
            new("REGI", "Registral", "Documentos registrales"),
            new("CONT", "Contratos", "Documentos contractuales")
        };
        var catalogoRepoMock = new Mock<ICatalogoTdnRepository>();
        catalogoRepoMock.Setup(r => r.GetFamiliasTdnActivasAsync(It.IsAny<CancellationToken>())).ReturnsAsync(familias);
        catalogoRepoMock.Setup(r => r.GetTdn2PromptByFamiliaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("PROMPT CUSTOM DE FAMILIA COMPLETA");

        var services = new ServiceCollection();
        services.AddSingleton(tipologiaRepoMock.Object);
        services.AddSingleton(catalogoRepoMock.Object);
        services.AddSingleton(_db);
        _serviceProvider = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(() => _serviceProvider.CreateScope());

        _builder = new ClassificationTipologiaPromptBuilder(
            _cache,
            scopeFactoryMock.Object,
            Mock.Of<ILogger<ClassificationTipologiaPromptBuilder>>());
    }

    private static TipologiaEntity CrearTipologia(string codigo, string tdn1, string tdn2, string descripcion)
    {
        // TipologiaValidationConfig.ResolvedGptDescripcion cae a la propiedad obsoleta
        // "GptDescripcion" (ortografía española, sin JsonPropertyName) cuando no hay bloque
        // "classification"; por eso el JSON usa "gptDescripcion" y no "gptDescription".
        var config = new
        {
            tipologiaId = codigo,
            tipologiaNombre = descripcion,
            tdn1,
            tdn2,
            gptDescripcion = descripcion
        };
        return new TipologiaEntity
        {
            Codigo = codigo,
            ConfiguracionJson = JsonSerializer.Serialize(config)
        };
    }

    public void Dispose()
    {
        _cache.Dispose();
        _db.Dispose();
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void BuildTdn1Catalog_ConRestriccion_SoloListaFamiliasDeLosCodigosPermitidos()
    {
        var catalogo = _builder.BuildTdn1Catalog(new[] { "nota-simple" });

        catalogo.Should().Contain("REGI");
        catalogo.Should().NotContain("CONT");
    }

    [Fact]
    public void BuildTdn1Catalog_SinRestriccion_ListaTodasLasFamilias()
    {
        var catalogo = _builder.BuildTdn1Catalog();

        catalogo.Should().Contain("REGI").And.Contain("CONT");
    }

    [Fact]
    public void BuildTdn2CatalogByFamilia_ConRestriccion_IgnoraPromptCustomYFiltraTipologias()
    {
        var catalogo = _builder.BuildTdn2CatalogByFamilia("REGI", new[] { "nota-simple" });

        catalogo.Should().NotContain("PROMPT CUSTOM");
        catalogo.Should().Contain("nota-simple");
        catalogo.Should().NotContain("REGI-02");
    }

    [Fact]
    public void BuildTdn2CatalogByFamilia_SinRestriccion_UsaPromptCustom()
    {
        var catalogo = _builder.BuildTdn2CatalogByFamilia("REGI");

        catalogo.Should().Be("PROMPT CUSTOM DE FAMILIA COMPLETA");
    }

    [Fact]
    public void BuildTdn1Catalog_ConjuntosDistintos_NoCompartenCache()
    {
        var restringido = _builder.BuildTdn1Catalog(new[] { "nota-simple" });
        var completo = _builder.BuildTdn1Catalog();

        restringido.Should().NotBe(completo);
    }

    [Fact]
    public void BuildTdn1Catalog_MismoConjuntoDistintoOrdenYCase_CompartenCache()
    {
        var a = _builder.BuildTdn1Catalog(new[] { "nota-simple", "CONT-01" });
        var b = _builder.BuildTdn1Catalog(new[] { "cont-01", "NOTA-SIMPLE" });

        a.Should().Be(b);
    }
}
