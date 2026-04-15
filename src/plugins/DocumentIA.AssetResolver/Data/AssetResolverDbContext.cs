using DocumentIA.AssetResolver.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.AssetResolver.Data;

/// <summary>
/// DbContext para la base de datos propia de AssetResolver.
/// </summary>
public class AssetResolverDbContext : DbContext
{
    public AssetResolverDbContext(DbContextOptions<AssetResolverDbContext> options)
        : base(options)
    {
    }

    public DbSet<DmPosicionAAII> DmPosicionAAII { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DmPosicionAAII>(entity =>
        {
            entity.HasKey(e => new { e.IdActivoSareb, e.FchCierreDt });
            entity.ToTable("DM_POSICION_AAII_TB");
        });
    }
}
