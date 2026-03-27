using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicConfigEntitiesAndState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Estado",
                table: "Tipologias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublicadaEn",
                table: "Tipologias",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicadaPor",
                table: "Tipologias",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersionPublicada",
                table: "Tipologias",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModeloConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    ConfiguracionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModeloConfigs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Estado", "FechaCreacion", "PublicadaEn", "PublicadaPor", "VersionPublicada" },
                values: new object[] { 1, new DateTime(2026, 3, 27, 19, 48, 58, 565, DateTimeKind.Utc).AddTicks(3353), new DateTime(2026, 3, 27, 19, 48, 58, 565, DateTimeKind.Utc).AddTicks(3349), "seed", "1.0" });

            migrationBuilder.Sql("UPDATE Tipologias SET Estado = 1, PublicadaEn = COALESCE(PublicadaEn, SYSUTCDATETIME()), PublicadaPor = COALESCE(PublicadaPor, 'migration'), VersionPublicada = COALESCE(VersionPublicada, Version) WHERE Activa = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ModeloConfigs_Key",
                table: "ModeloConfigs",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModeloConfigs");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Tipologias");

            migrationBuilder.DropColumn(
                name: "PublicadaEn",
                table: "Tipologias");

            migrationBuilder.DropColumn(
                name: "PublicadaPor",
                table: "Tipologias");

            migrationBuilder.DropColumn(
                name: "VersionPublicada",
                table: "Tipologias");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaCreacion",
                value: new DateTime(2026, 3, 24, 16, 20, 36, 869, DateTimeKind.Utc).AddTicks(6009));
        }
    }
}
