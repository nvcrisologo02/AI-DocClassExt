using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSourceSystemEjecucion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_SourceSystem_FechaEjecucion",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "SourceSystem",
                table: "DocumentoEjecuciones");

        }
    }
}
