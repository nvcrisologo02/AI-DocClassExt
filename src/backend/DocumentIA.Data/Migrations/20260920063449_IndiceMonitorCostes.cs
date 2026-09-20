using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndiceMonitorCostes : Migration
    {
        // Nota: EF scaffoldea aqui un UpdateData sobre Tipologias por el seed no
        // determinista (DateTime.UtcNow en HasData). Se ha retirado a proposito, igual que
        // en 20260911093509_ReutilizacionPorDuplicado: esta migracion solo recrea un indice
        // y no debe reescribir datos en PRO.
        //
        // AB#100662: el indice cubriente del Monitor se recrea con las siete columnas de
        // coste (CosteIAEur, CosteEstimado, CosteLayoutEur, CosteClasificacionEur,
        // CosteExtraccionEur, CostePromptEur, TokensIA) en el INCLUDE. La seccion de costes
        // del Admin agrega esas columnas sobre el mismo rango de FechaEjecucion; sin ellas
        // cada consulta hacia key lookup por fila y el agregado de 90 dias en PRO
        // (~68k filas, 19,5 s) superaba el timeout de 30 s de Admin_GetCostes.
        //
        // En PRO esta migracion NO se aplica tal cual: la recreacion del cubriente sobre mas
        // de 70k filas bloquea la tabla. Usar scripts/database/indice-monitor-costes-pro.sql,
        // que hace lo mismo con DROP_EXISTING + ONLINE = ON y registra la migracion.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion_Monitor",
                table: "DocumentoEjecuciones");


            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion_Monitor",
                table: "DocumentoEjecuciones",
                column: "FechaEjecucion")
                .Annotation("SqlServer:Include", new[] { "EstadoFinal", "ConfianzaGlobal", "UseFallbackLLM", "Tipologia", "ModeloClasificacion", "ClassificationOnly", "DuracionTotalMs", "DocumentoId", "EjecucionGuid", "SubmittedBy", "ConfianzaClasificacion", "DuracionClasificacionMs", "DuracionExtraccionMs", "DuracionGDCMs", "DuracionValidacionMs", "DuracionIntegracionMs", "DuracionPersistenciaMs", "ReutilizadaPorDuplicado", "EjecucionOriginalId", "CosteIAEur", "CosteEstimado", "CosteLayoutEur", "CosteClasificacionEur", "CosteExtraccionEur", "CostePromptEur", "TokensIA" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion_Monitor",
                table: "DocumentoEjecuciones");


            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion_Monitor",
                table: "DocumentoEjecuciones",
                column: "FechaEjecucion")
                .Annotation("SqlServer:Include", new[] { "EstadoFinal", "ConfianzaGlobal", "UseFallbackLLM", "Tipologia", "ModeloClasificacion", "ClassificationOnly", "DuracionTotalMs", "DocumentoId", "EjecucionGuid", "SubmittedBy", "ConfianzaClasificacion", "DuracionClasificacionMs", "DuracionExtraccionMs", "DuracionGDCMs", "DuracionValidacionMs", "DuracionIntegracionMs", "DuracionPersistenciaMs", "ReutilizadaPorDuplicado", "EjecucionOriginalId" });
        }
    }
}
