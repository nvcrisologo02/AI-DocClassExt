using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogoTdn1Tdn2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogoTdn1",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogoTdn1", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogoTdn2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CodigoTdn1 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Tdn1Id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogoTdn2", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogoTdn2_CatalogoTdn1_Tdn1Id",
                        column: x => x.Tdn1Id,
                        principalTable: "CatalogoTdn1",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 21, 13, 52, 46, 674, DateTimeKind.Utc).AddTicks(9085), new DateTime(2026, 5, 21, 13, 52, 46, 674, DateTimeKind.Utc).AddTicks(9075) });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogoTdn1_Codigo",
                table: "CatalogoTdn1",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogoTdn2_Codigo",
                table: "CatalogoTdn2",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogoTdn2_Tdn1Id",
                table: "CatalogoTdn2",
                column: "Tdn1Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogoTdn2");

            migrationBuilder.DropTable(
                name: "CatalogoTdn1");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 14, 15, 37, 22, 431, DateTimeKind.Utc).AddTicks(4530), new DateTime(2026, 5, 14, 15, 37, 22, 431, DateTimeKind.Utc).AddTicks(4526) });
        }
    }
}
