using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <summary>
    /// AB#100232: coste en euros y tokens de servicios de IA por ejecucion.
    ///
    /// Migracion puramente aditiva: dos columnas nullable, sin relleno retroactivo.
    /// Las ejecuciones anteriores quedan a NULL, que se distingue del cero explicito
    /// que graban las ejecuciones nuevas sin consumo de IA.
    ///
    /// Nota: EF scaffoldea aqui un UpdateData sobre Tipologias por el seed no
    /// determinista (DateTime.UtcNow en HasData). Se ha retirado a proposito, igual
    /// que en las migraciones anteriores: pisaria las fechas de una tipologia real.
    ///
    /// Vuelta atras: el Down borra solo las dos columnas nuevas. El desglose por
    /// llamada vive dentro de ContratoSalidaCompletoJson y no se pierde al revertir.
    /// </summary>
    public partial class AddCostesIAToEjecuciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CosteIAEur",
                table: "DocumentoEjecuciones",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokensIA",
                table: "DocumentoEjecuciones",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CosteIAEur",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "TokensIA",
                table: "DocumentoEjecuciones");
        }
    }
}
