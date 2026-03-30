using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginTipologiaConfigsDynamic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PluginTipologiaConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipologiaCodigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfiguracionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicadaEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicadaPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginTipologiaConfigs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 3, 27, 20, 3, 57, 484, DateTimeKind.Utc).AddTicks(4797), new DateTime(2026, 3, 27, 20, 3, 57, 484, DateTimeKind.Utc).AddTicks(4792) });

            migrationBuilder.CreateIndex(
                name: "IX_PluginTipologiaConfigs_TipologiaCodigo",
                table: "PluginTipologiaConfigs",
                column: "TipologiaCodigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PluginTipologiaConfigs");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 3, 27, 19, 48, 58, 565, DateTimeKind.Utc).AddTicks(3353), new DateTime(2026, 3, 27, 19, 48, 58, 565, DateTimeKind.Utc).AddTicks(3349) });
        }
    }
}
