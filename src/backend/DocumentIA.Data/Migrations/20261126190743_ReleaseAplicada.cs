using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseAplicada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nota: EF scaffoldea aqui un UpdateData sobre Tipologias por el seed no
            // determinista (DateTime.UtcNow en HasData). Se ha retirado a proposito: esta
            // migracion solo crea la tabla ReleaseAplicada y su indice (AB#101570).

            migrationBuilder.CreateTable(
                name: "ReleaseAplicada",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Paso = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    HashContenido = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BuildId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseAplicada", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseAplicada_Version_Paso_Resultado",
                table: "ReleaseAplicada",
                columns: new[] { "Version", "Paso", "Resultado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReleaseAplicada");
        }
    }
}
