using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHybridTdnFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Estado",
                table: "Documentos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "ClassifierVersion",
                table: "Documentos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedupSha256",
                table: "Documentos",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceUri",
                table: "Documentos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Matricula",
                table: "Documentos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PagesProcessed",
                table: "Documentos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Tdn1",
                table: "Documentos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tdn2",
                table: "Documentos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 13, 14, 15, 33, 118, DateTimeKind.Utc).AddTicks(2199), new DateTime(2026, 5, 13, 14, 15, 33, 118, DateTimeKind.Utc).AddTicks(2195) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassifierVersion",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "DedupSha256",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "EvidenceUri",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "Matricula",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "PagesProcessed",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "Tdn1",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "Tdn2",
                table: "Documentos");

            migrationBuilder.AlterColumn<string>(
                name: "Estado",
                table: "Documentos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 11, 7, 14, 23, 356, DateTimeKind.Utc).AddTicks(3459), new DateTime(2026, 5, 11, 7, 14, 23, 356, DateTimeKind.Utc).AddTicks(3454) });
        }
    }
}
