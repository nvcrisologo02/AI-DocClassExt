using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndiceCubrienteMonitorEjecuciones : Migration
    {
        /// <inheritdoc />
        // Nota: EF scaffoldea aqui un UpdateData sobre Tipologias por el seed no
        // determinista (DateTime.UtcNow en HasData). Se ha retirado a proposito: esta
        // migracion solo crea el indice del Monitor y no debe reescribir datos en PRO.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion",
                table: "DocumentoEjecuciones");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion_Monitor",
                table: "DocumentoEjecuciones",
                column: "FechaEjecucion")
                .Annotation("SqlServer:Include", new[] { "EstadoFinal", "ConfianzaGlobal", "UseFallbackLLM", "Tipologia", "ModeloClasificacion", "ClassificationOnly", "DuracionTotalMs", "DocumentoId", "EjecucionGuid", "SubmittedBy", "ConfianzaClasificacion", "DuracionClasificacionMs", "DuracionExtraccionMs", "DuracionGDCMs", "DuracionValidacionMs", "DuracionIntegracionMs", "DuracionPersistenciaMs" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion_Monitor",
                table: "DocumentoEjecuciones");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion",
                table: "DocumentoEjecuciones",
                column: "FechaEjecucion");
        }
    }
}
