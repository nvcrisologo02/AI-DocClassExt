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

        // Seed data inicial
        modelBuilder.Entity<TipologiaEntity>().HasData(
            new TipologiaEntity
            {
                Id = 1,
                Codigo = "tasacion",
                Nombre = "Tasación",
                Version = "1.0",
                Activa = true,
                UmbralClasificacion = 0.85,
                UmbralExtraccion = 0.80,
                FechaCreacion = DateTime.UtcNow
            }
        );
    }
}
