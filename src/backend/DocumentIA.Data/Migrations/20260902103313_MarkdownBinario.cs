using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class MarkdownBinario : Migration
    {
        /// <inheritdoc />
        // Nota: EF scaffoldea aqui un UpdateData sobre Tipologias por el seed no
        // determinista (DateTime.UtcNow en HasData). Se ha retirado a proposito.
        //
        // Vuelta atras: el Down borra SOLO la columna nueva. La columna Base64 sigue
        // escribiendose en paralelo (AB#100169), asi que revertir no pierde ningun
        // markdown, ni el historico ni el escrito con la version nueva.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "NormalizacionMarkdownGzip",
                table: "Documentos",
                type: "varbinary(max)",
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NormalizacionMarkdownGzip",
                table: "Documentos");

        }
    }
}
