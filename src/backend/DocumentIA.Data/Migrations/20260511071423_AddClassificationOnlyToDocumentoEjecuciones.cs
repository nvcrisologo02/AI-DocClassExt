using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassificationOnlyToDocumentoEjecuciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClassificationOnly",
                table: "DocumentoEjecuciones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 11, 7, 14, 23, 356, DateTimeKind.Utc).AddTicks(3459), new DateTime(2026, 5, 11, 7, 14, 23, 356, DateTimeKind.Utc).AddTicks(3454) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassificationOnly",
                table: "DocumentoEjecuciones");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 5, 14, 7, 29, 22, DateTimeKind.Utc).AddTicks(2912), new DateTime(2026, 5, 5, 14, 7, 29, 22, DateTimeKind.Utc).AddTicks(2907) });
        }
    }
}
