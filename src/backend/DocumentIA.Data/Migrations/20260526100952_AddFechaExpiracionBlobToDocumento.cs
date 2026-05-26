using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFechaExpiracionBlobToDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaExpiracionBlob",
                table: "Documentos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 26, 10, 9, 47, 957, DateTimeKind.Utc).AddTicks(3495), new DateTime(2026, 5, 26, 10, 9, 47, 957, DateTimeKind.Utc).AddTicks(3479) });

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_FechaExpiracionBlob",
                table: "Documentos",
                column: "FechaExpiracionBlob");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_FechaExpiracionBlob_RutaBlobStorage",
                table: "Documentos",
                columns: new[] { "FechaExpiracionBlob", "RutaBlobStorage" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documentos_FechaExpiracionBlob",
                table: "Documentos");

            migrationBuilder.DropIndex(
                name: "IX_Documentos_FechaExpiracionBlob_RutaBlobStorage",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "FechaExpiracionBlob",
                table: "Documentos");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 25, 15, 19, 45, 528, DateTimeKind.Utc).AddTicks(8087), new DateTime(2026, 5, 25, 15, 19, 45, 528, DateTimeKind.Utc).AddTicks(8082) });
        }
    }
}
