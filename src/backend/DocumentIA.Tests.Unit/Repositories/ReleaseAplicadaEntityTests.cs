#nullable enable
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocumentIA.Tests.Unit.Repositories;

/// <summary>
/// AB#101570: la tabla ReleaseAplicada registra cada paso de release aplicado por version y entorno.
/// El modelo debe fijar nombre de tabla, longitudes, obligatoriedad e indice de consulta.
/// </summary>
public class ReleaseAplicadaEntityTests
{
    [Fact]
    public void ReleaseAplicada_mapea_tabla_y_columnas_del_diseno()
    {
        using var context = CreateContext();

        // El nombre de tabla es una anotacion de proveedor: solo esta en el modelo de diseno.
        var entidad = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ReleaseAplicadaEntity))!;

        entidad.GetTableName().Should().Be("ReleaseAplicada");
        entidad.FindProperty("Version")!.GetMaxLength().Should().Be(32);
        entidad.FindProperty("Paso")!.GetMaxLength().Should().Be(400);
        entidad.FindProperty("HashContenido")!.GetMaxLength().Should().Be(64);
        entidad.FindProperty("Resultado")!.GetMaxLength().Should().Be(16);
        entidad.FindProperty("BuildId")!.GetMaxLength().Should().Be(32);

        foreach (var nombre in new[] { "Version", "Paso", "HashContenido", "Resultado", "BuildId", "FechaUtc" })
        {
            entidad.FindProperty(nombre)!.IsNullable.Should().BeFalse(nombre);
        }

        var indice = entidad.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Version", "Paso", "Resultado" }));
        indice.IsUnique.Should().BeFalse();
    }

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            // Sin abrir conexion: solo se necesita el modelo con las convenciones relacionales de SQL Server.
            .UseSqlServer("Server=tcp:localhost,1433;Database=modelo-sin-conexion;")
            .Options;
        return new DocumentIADbContext(options);
    }
}
