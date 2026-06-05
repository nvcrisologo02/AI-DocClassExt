using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class v15_MarkPromptGPTObsolete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 6, 5, 9, 54, 43, 370, DateTimeKind.Utc).AddTicks(1450), new DateTime(2026, 6, 5, 9, 54, 43, 370, DateTimeKind.Utc).AddTicks(1446) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 6, 2, 18, 31, 49, 961, DateTimeKind.Utc).AddTicks(360), new DateTime(2026, 6, 2, 18, 31, 49, 961, DateTimeKind.Utc).AddTicks(356) });
        }
    }
}
