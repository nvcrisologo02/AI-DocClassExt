using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetResolverColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssetResolverResultJson",
                table: "DocumentoEjecuciones",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracionAssetResolverMs",
                table: "DocumentoEjecuciones",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 4, 14, 13, 58, 24, 367, DateTimeKind.Utc).AddTicks(7842), new DateTime(2026, 4, 14, 13, 58, 24, 367, DateTimeKind.Utc).AddTicks(7838) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssetResolverResultJson",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "DuracionAssetResolverMs",
                table: "DocumentoEjecuciones");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 4, 8, 9, 30, 14, 495, DateTimeKind.Utc).AddTicks(6871), new DateTime(2026, 4, 8, 9, 30, 14, 495, DateTimeKind.Utc).AddTicks(6865) });
        }
    }
}
