using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations;

/// <summary>
/// Temporary DEV-only rollback marker used to execute the Down-equivalent through
/// the Function App managed identity. This file must be removed after the rollback.
/// </summary>
[Migration("20260902190000_RevertSourceSystemEjecucionDev")]
public partial class RevertSourceSystemEjecucionDev : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_DocumentoEjecuciones_SourceSystem_FechaEjecucion'
                  AND object_id = OBJECT_ID(N'dbo.DocumentoEjecuciones'))
            BEGIN
                DROP INDEX IX_DocumentoEjecuciones_SourceSystem_FechaEjecucion
                    ON dbo.DocumentoEjecuciones;
            END;

            IF COL_LENGTH(N'dbo.DocumentoEjecuciones', N'SourceSystem') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.DocumentoEjecuciones DROP COLUMN SourceSystem;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SourceSystem",
            table: "DocumentoEjecuciones",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_DocumentoEjecuciones_SourceSystem_FechaEjecucion",
            table: "DocumentoEjecuciones",
            columns: new[] { "SourceSystem", "FechaEjecucion" });
    }
}
