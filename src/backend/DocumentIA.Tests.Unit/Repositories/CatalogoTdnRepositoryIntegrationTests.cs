#nullable enable
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DocumentIA.Tests.Unit.Repositories;

/// <summary>
/// Test de integración para verificar que GetTdn2PromptByFamiliaAsync obtiene datos de BD correctamente
/// </summary>
public class CatalogoTdnRepositoryIntegrationTests
{
    [Fact]
    public async Task GetTdn2PromptByFamiliaAsync_WithSereInDatabase_ShouldReturnCustomPrompt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;

        using (var context = new DocumentIADbContext(options))
        {
            // Insertar registro de test
            context.CatalogoTdn1.Add(new CatalogoTdn1Entity
            {
                Codigo = "SERE",
                Nombre = "Sentencias y resoluciones judiciales",
                Descripcion = "Test description",
                TDN2_Prompt = "Este es un prompt personalizado para SERE"
            });
            await context.SaveChangesAsync();
        }

        // Act
        using (var context = new DocumentIADbContext(options))
        {
            var repository = new CatalogoTdnRepository(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<CatalogoTdnRepository>.Instance);
            
            var result = await repository.GetTdn2PromptByFamiliaAsync("SERE");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Este es un prompt personalizado para SERE", result);
        }
    }

    [Fact]
    public async Task GetTdn2PromptByFamiliaAsync_WithNormalization_ShouldFindByLowercase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;

        using (var context = new DocumentIADbContext(options))
        {
            context.CatalogoTdn1.Add(new CatalogoTdn1Entity
            {
                Codigo = "SERE",
                Nombre = "Sentencias y resoluciones judiciales",
                Descripcion = "Test",
                TDN2_Prompt = "Prompt personalizado"
            });
            await context.SaveChangesAsync();
        }

        // Act - llamar con minúsculas
        using (var context = new DocumentIADbContext(options))
        {
            var repository = new CatalogoTdnRepository(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<CatalogoTdnRepository>.Instance);
            
            var result = await repository.GetTdn2PromptByFamiliaAsync("sere");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Prompt personalizado", result);
        }
    }

    [Fact]
    public async Task GetTdn2PromptByFamiliaAsync_WithNullPrompt_ShouldReturnNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;

        using (var context = new DocumentIADbContext(options))
        {
            context.CatalogoTdn1.Add(new CatalogoTdn1Entity
            {
                Codigo = "ESCR",
                Nombre = "Escrituras",
                Descripcion = "Test",
                TDN2_Prompt = null
            });
            await context.SaveChangesAsync();
        }

        // Act
        using (var context = new DocumentIADbContext(options))
        {
            var repository = new CatalogoTdnRepository(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<CatalogoTdnRepository>.Instance);
            
            var result = await repository.GetTdn2PromptByFamiliaAsync("ESCR");

            // Assert
            Assert.Null(result);
        }
    }
}
