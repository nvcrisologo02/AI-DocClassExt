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
    public DbSet<PluginTipologiaConfigEntity> PluginTipologiaConfigs { get; set; } = null!;
    public DbSet<TipologiaConfigAuditEntity> TipologiaConfigAudit { get; set; } = null!;
    public DbSet<CatalogoTdn1Entity> CatalogoTdn1 { get; set; } = null!;
    public DbSet<CatalogoTdn2Entity> CatalogoTdn2 { get; set; } = null!;

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

        modelBuilder.Entity<DocumentoEntity>()
            .HasIndex(d => d.FechaExpiracionBlob);

        modelBuilder.Entity<DocumentoEntity>()
            .HasIndex(d => new { d.FechaExpiracionBlob, d.RutaBlobStorage });

        modelBuilder.Entity<TipologiaEntity>()
            .HasIndex(t => t.Codigo)
            .IsUnique();

        modelBuilder.Entity<TipologiaConfigAuditEntity>()
            .HasIndex(t => t.TipologiaId);

        modelBuilder.Entity<TipologiaConfigAuditEntity>()
            .HasIndex(t => t.FechaHora);

        modelBuilder.Entity<ModeloConfigEntity>()
            .HasIndex(m => m.Key)
            .IsUnique();

        modelBuilder.Entity<PluginTipologiaConfigEntity>()
            .HasIndex(p => p.TipologiaCodigo)
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

        // CatalogoTdn1 / CatalogoTdn2
        modelBuilder.Entity<CatalogoTdn1Entity>()
            .HasIndex(t => t.Codigo)
            .IsUnique();

        modelBuilder.Entity<CatalogoTdn2Entity>()
            .HasIndex(t => t.Codigo)
            .IsUnique();

        modelBuilder.Entity<CatalogoTdn2Entity>()
            .HasOne(t => t.Tdn1)
            .WithMany(p => p.SubTipos)
            .HasForeignKey(t => t.Tdn1Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CatalogoTdn1Entity>().HasData(CatalogoTdn1Seed.GetData());
        modelBuilder.Entity<CatalogoTdn2Entity>().HasData(CatalogoTdn2Seed.GetData());
    }
}
