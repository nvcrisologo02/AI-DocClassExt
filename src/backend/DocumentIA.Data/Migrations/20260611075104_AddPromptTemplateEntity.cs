using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptTemplateEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromptKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptTemplates", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 6, 11, 7, 51, 3, 479, DateTimeKind.Utc).AddTicks(5413), new DateTime(2026, 6, 11, 7, 51, 3, 479, DateTimeKind.Utc).AddTicks(5410) });

            migrationBuilder.CreateIndex(
                name: "IX_PromptTemplate_Key_Active",
                table: "PromptTemplates",
                columns: new[] { "PromptKey", "IsActive" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "UQ_PromptTemplate_Key_Version",
                table: "PromptTemplates",
                columns: new[] { "PromptKey", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptTemplates");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 6, 11, 7, 48, 18, 940, DateTimeKind.Utc).AddTicks(7931), new DateTime(2026, 6, 11, 7, 48, 18, 940, DateTimeKind.Utc).AddTicks(7927) });
        }
    }
}
