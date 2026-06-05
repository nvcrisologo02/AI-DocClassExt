using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class v31_DropLegacyModelosUmbrales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModeloClasificacionDI",
                table: "Tipologias");

            migrationBuilder.DropColumn(
                name: "ModeloExtraccionDI",
                table: "Tipologias");

            migrationBuilder.DropColumn(
                name: "UmbralClasificacion",
                table: "Tipologias");

            migrationBuilder.DropColumn(
                name: "UmbralExtraccion",
                table: "Tipologias");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 6, 5, 11, 39, 19, 802, DateTimeKind.Utc).AddTicks(2796), new DateTime(2026, 6, 5, 11, 39, 19, 802, DateTimeKind.Utc).AddTicks(2646) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModeloClasificacionDI",
                table: "Tipologias",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeloExtraccionDI",
                table: "Tipologias",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "UmbralClasificacion",
                table: "Tipologias",
                type: "float",
                nullable: false,
                defaultValue: 0.85);

            migrationBuilder.AddColumn<double>(
                name: "UmbralExtraccion",
                table: "Tipologias",
                type: "float",
                nullable: false,
                defaultValue: 0.80);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 6, 5, 9, 54, 56, 32, DateTimeKind.Utc).AddTicks(7719), new DateTime(2026, 6, 5, 9, 54, 56, 32, DateTimeKind.Utc).AddTicks(7714) });
        }
    }
}
