using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTipologiaConfigAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TipologiaConfigAudit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipologiaId = table.Column<int>(type: "int", nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DetallesJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipologiaConfigAudit", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 4, 24, 14, 51, 31, 704, DateTimeKind.Utc).AddTicks(2522), new DateTime(2026, 4, 24, 14, 51, 31, 704, DateTimeKind.Utc).AddTicks(2517) });

            migrationBuilder.CreateIndex(
                name: "IX_TipologiaConfigAudit_FechaHora",
                table: "TipologiaConfigAudit",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_TipologiaConfigAudit_TipologiaId",
                table: "TipologiaConfigAudit",
                column: "TipologiaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TipologiaConfigAudit");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 4, 14, 13, 58, 24, 367, DateTimeKind.Utc).AddTicks(7842), new DateTime(2026, 4, 14, 13, 58, 24, 367, DateTimeKind.Utc).AddTicks(7838) });
        }
    }
}
