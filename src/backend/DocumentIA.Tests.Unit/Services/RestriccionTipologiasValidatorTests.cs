#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Services;

public class RestriccionTipologiasValidatorTests : IDisposable
{
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public void Dispose()
    {
        _cache.Dispose();
        GC.SuppressFinalize(this);
    }

    private RestriccionTipologiasValidator CreateValidator(params string[] codigosPublicados)
    {
        var repoMock = new Mock<ITipologiaRepository>();
        repoMock.Setup(r => r.GetAllPublishedAsync())
            .ReturnsAsync(codigosPublicados
                .Select(c => new TipologiaEntity { Codigo = c })
                .ToList());

        var services = new ServiceCollection();
        services.AddSingleton(repoMock.Object);
        var provider = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope())
            .Returns(() => provider.CreateScope());

        return new RestriccionTipologiasValidator(
            _cache,
            scopeFactoryMock.Object,
            Mock.Of<ILogger<RestriccionTipologiasValidator>>());
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_NullRestriccion_EsValida()
    {
        var validator = CreateValidator("SERE-25");

        var result = await validator.ValidateAndNormalizeAsync(null);

        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_CodigosValidos_NormalizaACanonicoYSinIgnorados()
    {
        var validator = CreateValidator("SERE-25", "nota-simple", "ESCR-01");
        var restriccion = new RestriccionTipologias
        {
            Codigos = new List<string> { " sere-25 ", "NOTA-SIMPLE" }
        };

        var result = await validator.ValidateAndNormalizeAsync(restriccion);

        result.IsValid.Should().BeTrue();
        restriccion.Codigos.Should().BeEquivalentTo("SERE-25", "nota-simple");
        restriccion.CodigosIgnorados.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_CodigosParcialmenteInvalidos_IgnoraConAviso()
    {
        var validator = CreateValidator("SERE-25", "ESCR-01");
        var restriccion = new RestriccionTipologias
        {
            Codigos = new List<string> { "SERE-25", "SERE-99", "NO-EXISTE" }
        };

        var result = await validator.ValidateAndNormalizeAsync(restriccion);

        result.IsValid.Should().BeTrue();
        restriccion.Codigos.Should().BeEquivalentTo(new[] { "SERE-25" });
        restriccion.CodigosIgnorados.Should().BeEquivalentTo("SERE-99", "NO-EXISTE");
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_TodosInvalidos_EsInvalidaConDetalle()
    {
        var validator = CreateValidator("SERE-25");
        var restriccion = new RestriccionTipologias
        {
            Codigos = new List<string> { "SERE-99", "XXXX-01" }
        };

        var result = await validator.ValidateAndNormalizeAsync(restriccion);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("SERE-99").And.Contain("XXXX-01");
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_ListaVacia_EsInvalida()
    {
        var validator = CreateValidator("SERE-25");
        var restriccion = new RestriccionTipologias { Codigos = new List<string>() };

        var result = await validator.ValidateAndNormalizeAsync(restriccion);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("codigos");
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_Duplicados_SeDeduplican()
    {
        var validator = CreateValidator("SERE-25");
        var restriccion = new RestriccionTipologias
        {
            Codigos = new List<string> { "SERE-25", "sere-25", " SERE-25 " }
        };

        var result = await validator.ValidateAndNormalizeAsync(restriccion);

        result.IsValid.Should().BeTrue();
        restriccion.Codigos.Should().ContainSingle().Which.Should().Be("SERE-25");
    }
}
