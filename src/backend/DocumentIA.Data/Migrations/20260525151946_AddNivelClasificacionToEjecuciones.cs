using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNivelClasificacionToEjecuciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NivelClasificacion",
                table: "DocumentoEjecuciones",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 25, 15, 19, 45, 528, DateTimeKind.Utc).AddTicks(8087), new DateTime(2026, 5, 25, 15, 19, 45, 528, DateTimeKind.Utc).AddTicks(8082) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NivelClasificacion",
                table: "DocumentoEjecuciones");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 21, 13, 56, 15, 210, DateTimeKind.Utc).AddTicks(3170), new DateTime(2026, 5, 21, 13, 56, 15, 210, DateTimeKind.Utc).AddTicks(3161) });
        }
    }
}
