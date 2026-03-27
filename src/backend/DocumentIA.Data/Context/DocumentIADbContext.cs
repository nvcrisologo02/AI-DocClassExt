using Microsoft.EntityFrameworkCore;
using DocumentIA.Data.Entities;

namespace DocumentIA.Data.Context;

public class DocumentIADbContext : DbContext
{
    public DocumentIADbContext(DbContextOptions<DocumentIADbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentoEntity> Documentos { get; set; } = null!;
    public DbSet<ResultadoProcesamientoEntity> ResultadosProcesamiento { get; set; } = null!;
    public DbSet<TipologiaEntity> Tipologias { get; set; } = null!;
    public DbSet<AuditoriaEntity> Auditoria { get; set; } = null!;
    public DbSet<DocumentoEjecucionEntity> DocumentoEjecuciones { get; set; } = null!;
    public DbSet<PluginEjecucionEntity> PluginEjecuciones { get; set; } = null!;
    public DbSet<ValidacionResultadoEntity> ValidacionResultados { get; set; } = null!;
    public DbSet<ModeloConfigEntity> ModeloConfigs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurar relaciones
        modelBuilder.Entity<DocumentoEntity>()
            .HasOne(d => d.Resultado)
            .WithOne(r => r.Documento)
            .HasForeignKey<ResultadoProcesamientoEntity>(r => r.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentoEntity>()
            .HasMany(d => d.Auditorias)
            .WithOne(a => a.Documento)
            .HasForeignKey(a => a.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índices
        modelBuilder.Entity<DocumentoEntity>()
            .HasIndex(d => d.SHA256)
            .IsUnique();

        modelBuilder.Entity<DocumentoEntity>()
            .HasIndex(d => d.CorrelationId);

        modelBuilder.Entity<DocumentoEntity>()
            .HasIndex(d => d.Estado);

        modelBuilder.Entity<TipologiaEntity>()
            .HasIndex(t => t.Codigo)
            .IsUnique();

        modelBuilder.Entity<ModeloConfigEntity>()
            .HasIndex(m => m.Key)
            .IsUnique();

        // Seed data inicial
        modelBuilder.Entity<TipologiaEntity>().HasData(
            new TipologiaEntity
            {
                Id = 1,
                Codigo = "tasacion",
                Nombre = "Tasación",
                Version = "1.0",
                Activa = true,
                Estado = EstadoTipologia.Published,
                PublicadaEn = DateTime.UtcNow,
                PublicadaPor = "seed",
                VersionPublicada = "1.0",
                UmbralClasificacion = 0.85,
                UmbralExtraccion = 0.80,
                FechaCreacion = DateTime.UtcNow
            }
        );
        // Relacion Documento -> Ejecuciones (1:N)
        modelBuilder.Entity<DocumentoEntity>()
            .HasMany(d => d.Ejecuciones)
            .WithOne(e => e.Documento)
            .HasForeignKey(e => e.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relacion Ejecucion -> Plugins (1:N)
        modelBuilder.Entity<DocumentoEjecucionEntity>()
            .HasMany(e => e.PluginsEjecutados)
            .WithOne(p => p.Ejecucion)
            .HasForeignKey(p => p.EjecucionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relacion Ejecucion -> Validaciones (1:N)
        modelBuilder.Entity<DocumentoEjecucionEntity>()
            .HasMany(e => e.Validaciones)
            .WithOne(v => v.Ejecucion)
            .HasForeignKey(v => v.EjecucionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indices para rendimiento
        modelBuilder.Entity<DocumentoEjecucionEntity>()
            .HasIndex(e => e.EjecucionGuid)
            .IsUnique();

        modelBuilder.Entity<DocumentoEjecucionEntity>()
            .HasIndex(e => e.FechaEjecucion);

        modelBuilder.Entity<PluginEjecucionEntity>()
            .HasIndex(p => new { p.EjecucionId, p.PluginKey });

        modelBuilder.Entity<ValidacionResultadoEntity>()
            .HasIndex(v => new { v.EjecucionId, v.Campo });
    }
}
