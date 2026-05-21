using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class PromptGptToMax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PromptGPT",
                table: "Tipologias",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4096)",
                oldMaxLength: 4096,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 14, 15, 37, 22, 431, DateTimeKind.Utc).AddTicks(4530), new DateTime(2026, 5, 14, 15, 37, 22, 431, DateTimeKind.Utc).AddTicks(4526) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PromptGPT",
                table: "Tipologias",
                type: "nvarchar(4096)",
                maxLength: 4096,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 14, 15, 20, 53, 192, DateTimeKind.Utc).AddTicks(6632), new DateTime(2026, 5, 14, 15, 20, 53, 192, DateTimeKind.Utc).AddTicks(6628) });
        }
    }
}
