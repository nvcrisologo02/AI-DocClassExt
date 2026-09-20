using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

public class DocumentoEjecucionReutilizacionModeloTests
{
    [Fact]
    public void Modelo_Should_DeclararReutilizadaPorDuplicadoNoNulable()
    {
        using var context = CreateContext();

        var propiedad = context.Model
            .FindEntityType(typeof(DocumentoEjecucionEntity))!
            .FindProperty(nameof(DocumentoEjecucionEntity.ReutilizadaPorDuplicado));

        propiedad.Should().NotBeNull();
        propiedad!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Modelo_Should_DeclararEjecucionOriginalIdNulable()
    {
        using var context = CreateContext();

        var propiedad = context.Model
            .FindEntityType(typeof(DocumentoEjecucionEntity))!
            .FindProperty(nameof(DocumentoEjecucionEntity.EjecucionOriginalId));

        propiedad.Should().NotBeNull();
        propiedad!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task Guardar_Should_ConservarElVinculoConLaEjecucionOriginal()
    {
        await using var context = CreateContext();
        context.Documentos.Add(new DocumentoEntity { Id = 1, NombreArchivo = "doc.pdf", SHA256 = "sha-1" });
        context.DocumentoEjecuciones.Add(new DocumentoEjecucionEntity
        {
            Id = 10,
            DocumentoId = 1,
            EjecucionGuid = "11111111-1111-1111-1111-111111111111",
            FechaEjecucion = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EstadoFinal = "OK"
        });
        context.DocumentoEjecuciones.Add(new DocumentoEjecucionEntity
        {
            Id = 11,
            DocumentoId = 1,
            EjecucionGuid = "22222222-2222-2222-2222-222222222222",
            FechaEjecucion = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
            EstadoFinal = "OK",
            ReutilizadaPorDuplicado = true,
            EjecucionOriginalId = 10
        });
        await context.SaveChangesAsync();

        var reutilizacion = await context.DocumentoEjecuciones.SingleAsync(e => e.Id == 11);

        reutilizacion.ReutilizadaPorDuplicado.Should().BeTrue();
        reutilizacion.EjecucionOriginalId.Should().Be(10);
    }

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"reutilizacion-modelo-{Guid.NewGuid()}")
            .Options;
        return new DocumentIADbContext(options);
    }
}
