using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class MarkdownCobertura : Migration
    {
        // AB#100245: cobertura del markdown persistido. El historico nace con
        // MarkdownPaginas NULL y MarkdownCompleto 0 (solo fallback); lo corrige
        // scripts/database/backfill-markdown-cobertura.ps1, fuera de la migracion.
        // Vuelta atras: el Down solo quita las dos columnas; el markdown no se toca.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MarkdownCompleto",
                table: "Documentos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MarkdownPaginas",
                table: "Documentos",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkdownCompleto",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "MarkdownPaginas",
                table: "Documentos");
        }
    }
}
