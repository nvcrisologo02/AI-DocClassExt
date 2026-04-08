using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizacionMarkdownCompressedToDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizacionMarkdownCompressed",
                table: "Documentos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 4, 8, 9, 30, 14, 495, DateTimeKind.Utc).AddTicks(6871), new DateTime(2026, 4, 8, 9, 30, 14, 495, DateTimeKind.Utc).AddTicks(6865) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NormalizacionMarkdownCompressed",
                table: "Documentos");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 3, 27, 20, 3, 57, 484, DateTimeKind.Utc).AddTicks(4797), new DateTime(2026, 3, 27, 20, 3, 57, 484, DateTimeKind.Utc).AddTicks(4792) });
        }
    }
}
