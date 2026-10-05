#nullable enable
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocumentIA.Tests.Unit.Repositories;

/// <summary>
/// AB#100863: la busqueda de duplicados por MD5 hacia scan completo de Documentos (sin indice).
/// El modelo debe declarar un indice sobre MD5 que incluya SHA256 para resolver la consulta
/// entera en el indice.
/// </summary>
public class DocumentoIndiceMd5ModeloTests
{
    [Fact]
    public void Modelo_Should_DeclararIndiceSobreMd5ConIncludeSha256()
    {
        using var context = CreateContext();

        // Las anotaciones de proveedor (INCLUDE) solo estan en el modelo de diseno.
        var indice = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(DocumentoEntity))!
            .GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "IX_Documentos_MD5");

        indice.Should().NotBeNull();
        indice!.Properties.Select(p => p.Name).Should().Equal(nameof(DocumentoEntity.MD5));
        indice.GetIncludeProperties().Should().Equal(nameof(DocumentoEntity.SHA256));
    }

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DocumentIADbContext(options);
    }
}
