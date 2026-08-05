using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSubmittedByEjecucion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubmittedBy",
                table: "DocumentoEjecuciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 8, 5, 10, 3, 49, 922, DateTimeKind.Utc).AddTicks(1216), new DateTime(2026, 8, 5, 10, 3, 49, 922, DateTimeKind.Utc).AddTicks(1212) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                table: "DocumentoEjecuciones");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 6, 15, 5, 34, 32, 346, DateTimeKind.Utc).AddTicks(3061), new DateTime(2026, 6, 15, 5, 34, 32, 346, DateTimeKind.Utc).AddTicks(3056) });
        }
    }
}
