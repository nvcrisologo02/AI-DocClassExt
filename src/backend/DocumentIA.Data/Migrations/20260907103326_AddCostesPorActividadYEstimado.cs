using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <summary>
    /// AB#100236: desglose del coste de IA por actividad (layout, clasificacion,
    /// extraccion y prompt) y marca de coste estimado.
    ///
    /// Migracion puramente aditiva: cuatro decimales nullable y un bit con valor
    /// por defecto false, sin relleno retroactivo. Las columnas de actividad permiten
    /// agregar desde Admin sin abrir el contrato JSON; la marca separa lo medido de lo
    /// estimado por el script de relleno.
    ///
    /// Nota: EF scaffoldea aqui un UpdateData sobre Tipologias por el seed no
    /// determinista. Retirado a proposito, como en las migraciones anteriores.
    /// </summary>
    public partial class AddCostesPorActividadYEstimado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CosteClasificacionEur",
                table: "DocumentoEjecuciones",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CosteEstimado",
                table: "DocumentoEjecuciones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "CosteExtraccionEur",
                table: "DocumentoEjecuciones",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CosteLayoutEur",
                table: "DocumentoEjecuciones",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostePromptEur",
                table: "DocumentoEjecuciones",
                type: "decimal(18,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CosteClasificacionEur",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "CosteEstimado",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "CosteExtraccionEur",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "CosteLayoutEur",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "CostePromptEur",
                table: "DocumentoEjecuciones");
        }
    }
}
