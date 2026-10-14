using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClaveIntencionDedup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nota: EF scaffoldea aqui un UpdateData sobre Tipologias por el seed no
            // determinista (DateTime.UtcNow en HasData). Se ha retirado a proposito: esta
            // migracion solo anade columnas y un indice y no debe reescribir datos.
            //
            // AB#100930: ClaveIntencion, ClaveIntencionJson y MotivoReutilizacion, mas el indice
            // IX_DocumentoEjecuciones_Documento_Clave que sirve la seleccion de historica.
            //
            // En PRO esta migracion NO se aplica tal cual: el CreateIndex de EF no emite
            // ONLINE = ON y la tabla supera las 100k filas con LOB. Usar
            // scripts/database/indice-documento-clave-pro.sql, que anade las columnas, crea el
            // indice ONLINE y registra la migracion en __EFMigrationsHistory.

            migrationBuilder.AddColumn<string>(
                name: "ClaveIntencion",
                table: "DocumentoEjecuciones",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaveIntencionJson",
                table: "DocumentoEjecuciones",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoReutilizacion",
                table: "DocumentoEjecuciones",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_Documento_Clave",
                table: "DocumentoEjecuciones",
                columns: new[] { "DocumentoId", "ClaveIntencion", "FechaEjecucion" },
                descending: new[] { false, false, true })
                .Annotation("SqlServer:Include", new[] { "EjecucionGuid", "Tipologia", "EstadoFinal", "ReutilizadaPorDuplicado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_Documento_Clave",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "ClaveIntencion",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "ClaveIntencionJson",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "MotivoReutilizacion",
                table: "DocumentoEjecuciones");
        }
    }
}
