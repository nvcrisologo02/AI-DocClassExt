#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Configuration;

/// <summary>
/// Tests para ClassificationTipologiaPromptBuilder - Verificación de fallback logic para prompts TDN2 personalizados
/// 
/// NOTA IMPORTANTE: Los tests de la lógica de fallback dentro de BuildTdn2CatalogByFamilia requieren
/// integración completa con DI (scope/serviceProvider) que es complejo de mockear correctamente.
/// 
/// En su lugar, los tests principales están en CatalogoTdnRepositoryTests que validan:
/// - GetTdn2PromptByFamiliaAsync retorna NULL cuando no hay prompt personalizado (fallback logic)
/// - GetTdn2PromptByFamiliaAsync retorna el valor cuando existe (custom logic)
/// - Normalización de códigos
/// - Manejo de errores
/// 
/// Los tests E2E validarán el flujo completo end-to-end.
/// </summary>
public class ClassificationTipologiaPromptBuilderTests : IDisposable
{
    private readonly IMemoryCache _cache;

    public ClassificationTipologiaPromptBuilderTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    public void Dispose()
    {
        _cache.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void BuildTdn2CatalogByFamilia_WhenCodigoIsEmpty_ShouldThrow()
    {
        // Arrange
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var loggerMock = new Mock<ILogger<ClassificationTipologiaPromptBuilder>>();
        var builder = new ClassificationTipologiaPromptBuilder(_cache, scopeFactoryMock.Object, loggerMock.Object);

        // Act
        var act = () => builder.BuildTdn2CatalogByFamilia(" ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*código de familia TDN1 es obligatorio*");
    }

    [Fact]
    public void BuildTdn2CatalogByFamilia_WhenCodigoIsNull_ShouldThrow()
    {
        // Arrange
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var loggerMock = new Mock<ILogger<ClassificationTipologiaPromptBuilder>>();
        var builder = new ClassificationTipologiaPromptBuilder(_cache, scopeFactoryMock.Object, loggerMock.Object);

        // Act
        var act = () => builder.BuildTdn2CatalogByFamilia(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldAcceptCacheAndScopeFactoryAndLogger()
    {
        // Arrange
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var loggerMock = new Mock<ILogger<ClassificationTipologiaPromptBuilder>>();

        // Act
        var builder = new ClassificationTipologiaPromptBuilder(_cache, scopeFactoryMock.Object, loggerMock.Object);

        // Assert
        builder.Should().NotBeNull();
    }
}
