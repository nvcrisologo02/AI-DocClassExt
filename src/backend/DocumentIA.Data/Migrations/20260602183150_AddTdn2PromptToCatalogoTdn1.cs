using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTdn2PromptToCatalogoTdn1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TDN2_Prompt",
                table: "CatalogoTdn1",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 1,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 2,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 3,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 4,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 5,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 6,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 7,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 8,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 9,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 10,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 11,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 12,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 13,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 14,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 15,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 16,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 17,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 18,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 19,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 20,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 21,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 22,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 23,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 24,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 25,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 26,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 27,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 28,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 29,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 30,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 31,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 32,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 33,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 34,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 35,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 36,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 37,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 38,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 39,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 40,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 41,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 42,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 43,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 44,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 45,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 46,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 47,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 48,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 49,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 50,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 51,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 52,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 53,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 54,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 55,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 56,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 57,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 58,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 59,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 60,
                column: "TDN2_Prompt",
                value: null);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 6, 2, 18, 31, 49, 961, DateTimeKind.Utc).AddTicks(360), new DateTime(2026, 6, 2, 18, 31, 49, 961, DateTimeKind.Utc).AddTicks(356) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TDN2_Prompt",
                table: "CatalogoTdn1");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 26, 10, 9, 47, 957, DateTimeKind.Utc).AddTicks(3495), new DateTime(2026, 5, 26, 10, 9, 47, 957, DateTimeKind.Utc).AddTicks(3479) });
        }
    }
}
