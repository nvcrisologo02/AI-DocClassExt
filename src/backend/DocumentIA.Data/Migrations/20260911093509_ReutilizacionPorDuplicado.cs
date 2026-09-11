using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReutilizacionPorDuplicado : Migration
    {
        // Nota: EF scaffoldea aqui un UpdateData sobre Tipologias por el seed no
        // determinista (DateTime.UtcNow en HasData). Se ha retirado a proposito, igual que
        // en 20260902080254_IndiceCubrienteMonitorEjecuciones: esta migracion solo anade
        // columnas e indices y no debe reescribir datos en PRO.
        //
        // AB#100258: el indice cubriente del Monitor se recrea con ReutilizadaPorDuplicado y
        // EjecucionOriginalId en el INCLUDE. Sin ellas, el filtro por defecto del Monitor
        // (ReutilizadaPorDuplicado = 0) dejaria de resolverse sobre el indice y se perderia
        // la cobertura ganada en AB#100182 / AB#100185.
        //
        // En PRO esta migracion NO se aplica tal cual: la recreacion del cubriente sobre mas
        // de 60k filas bloquea la tabla. Usar scripts/database/indice-monitor-reutilizacion-pro.sql,
        // que hace lo mismo con ONLINE = ON.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion_Monitor",
                table: "DocumentoEjecuciones");

            migrationBuilder.AddColumn<int>(
                name: "EjecucionOriginalId",
                table: "DocumentoEjecuciones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReutilizadaPorDuplicado",
                table: "DocumentoEjecuciones",
                type: "bit",
                nullable: false,
                defaultValue: false);


            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_EjecucionOriginalId",
                table: "DocumentoEjecuciones",
                column: "EjecucionOriginalId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion_Monitor",
                table: "DocumentoEjecuciones",
                column: "FechaEjecucion")
                .Annotation("SqlServer:Include", new[] { "EstadoFinal", "ConfianzaGlobal", "UseFallbackLLM", "Tipologia", "ModeloClasificacion", "ClassificationOnly", "DuracionTotalMs", "DocumentoId", "EjecucionGuid", "SubmittedBy", "ConfianzaClasificacion", "DuracionClasificacionMs", "DuracionExtraccionMs", "DuracionGDCMs", "DuracionValidacionMs", "DuracionIntegracionMs", "DuracionPersistenciaMs", "ReutilizadaPorDuplicado", "EjecucionOriginalId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_InstanceId_Reutilizadas",
                table: "DocumentoEjecuciones",
                column: "InstanceId",
                filter: "[ReutilizadaPorDuplicado] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentoEjecuciones_DocumentoEjecuciones_EjecucionOriginalId",
                table: "DocumentoEjecuciones",
                column: "EjecucionOriginalId",
                principalTable: "DocumentoEjecuciones",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentoEjecuciones_DocumentoEjecuciones_EjecucionOriginalId",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_EjecucionOriginalId",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion_Monitor",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_InstanceId_Reutilizadas",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "EjecucionOriginalId",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "ReutilizadaPorDuplicado",
                table: "DocumentoEjecuciones");


            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion_Monitor",
                table: "DocumentoEjecuciones",
                column: "FechaEjecucion")
                .Annotation("SqlServer:Include", new[] { "EstadoFinal", "ConfianzaGlobal", "UseFallbackLLM", "Tipologia", "ModeloClasificacion", "ClassificationOnly", "DuracionTotalMs", "DocumentoId", "EjecucionGuid", "SubmittedBy", "ConfianzaClasificacion", "DuracionClasificacionMs", "DuracionExtraccionMs", "DuracionGDCMs", "DuracionValidacionMs", "DuracionIntegracionMs", "DuracionPersistenciaMs" });
        }
    }
}
