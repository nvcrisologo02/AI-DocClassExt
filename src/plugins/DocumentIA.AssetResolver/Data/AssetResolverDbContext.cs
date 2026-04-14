using Microsoft.EntityFrameworkCore;

namespace DocumentIA.AssetResolver.Data;

/// <summary>
/// DbContext para la base de datos propia de AssetResolver.
/// Añade aquí los DbSet correspondientes a cada entidad del plugin.
/// </summary>
public class AssetResolverDbContext : DbContext
{
    public AssetResolverDbContext(DbContextOptions<AssetResolverDbContext> options)
        : base(options)
    {
    }

    // TODO: añadir DbSet<T> por cada entidad del dominio del plugin
    // Ejemplo:
    // public DbSet<Asset> Assets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configuraciones Fluent API aquí
    }
}
