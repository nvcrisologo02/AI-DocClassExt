using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarHistoricoEjecuciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumeroEjecucion",
                table: "ResultadosProcesamiento",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "PorcentajeCompletitud",
                table: "ResultadosProcesamiento",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TotalErroresValidacion",
                table: "ResultadosProcesamiento",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalPluginsEjecutados",
                table: "ResultadosProcesamiento",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalValidacionesAplicadas",
                table: "ResultadosProcesamiento",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalWarningsValidacion",
                table: "ResultadosProcesamiento",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DocumentoEjecuciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    EjecucionGuid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    FechaEjecucion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipologia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EstadoFinal = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConfianzaGlobal = table.Column<double>(type: "float", nullable: false),
                    ModeloClasificacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConfianzaClasificacion = table.Column<double>(type: "float", nullable: false),
                    UseFallbackLLM = table.Column<bool>(type: "bit", nullable: false),
                    DatosOriginalesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DatosFinalesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DuracionTotalMs = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoEjecuciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentoEjecuciones_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PluginEjecuciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EjecucionId = table.Column<int>(type: "int", nullable: false),
                    PluginKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DatosEnriquecidosJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaEjecucion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginEjecuciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginEjecuciones_DocumentoEjecuciones_EjecucionId",
                        column: x => x.EjecucionId,
                        principalTable: "DocumentoEjecuciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidacionResultados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EjecucionId = table.Column<int>(type: "int", nullable: false),
                    Campo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Severidad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorOriginal = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValorEsperado = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Pasado = table.Column<bool>(type: "bit", nullable: false),
                    FechaValidacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidacionResultados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidacionResultados_DocumentoEjecuciones_EjecucionId",
                        column: x => x.EjecucionId,
                        principalTable: "DocumentoEjecuciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaCreacion",
                value: new DateTime(2026, 2, 16, 11, 17, 44, 283, DateTimeKind.Utc).AddTicks(6168));

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_DocumentoId",
                table: "DocumentoEjecuciones",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_EjecucionGuid",
                table: "DocumentoEjecuciones",
                column: "EjecucionGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_FechaEjecucion",
                table: "DocumentoEjecuciones",
                column: "FechaEjecucion");

            migrationBuilder.CreateIndex(
                name: "IX_PluginEjecuciones_EjecucionId_PluginKey",
                table: "PluginEjecuciones",
                columns: new[] { "EjecucionId", "PluginKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ValidacionResultados_EjecucionId_Campo",
                table: "ValidacionResultados",
                columns: new[] { "EjecucionId", "Campo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PluginEjecuciones");

            migrationBuilder.DropTable(
                name: "ValidacionResultados");

            migrationBuilder.DropTable(
                name: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "NumeroEjecucion",
                table: "ResultadosProcesamiento");

            migrationBuilder.DropColumn(
                name: "PorcentajeCompletitud",
                table: "ResultadosProcesamiento");

            migrationBuilder.DropColumn(
                name: "TotalErroresValidacion",
                table: "ResultadosProcesamiento");

            migrationBuilder.DropColumn(
                name: "TotalPluginsEjecutados",
                table: "ResultadosProcesamiento");

            migrationBuilder.DropColumn(
                name: "TotalValidacionesAplicadas",
                table: "ResultadosProcesamiento");

            migrationBuilder.DropColumn(
                name: "TotalWarningsValidacion",
                table: "ResultadosProcesamiento");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaCreacion",
                value: new DateTime(2026, 1, 30, 12, 25, 42, 817, DateTimeKind.Utc).AddTicks(8190));
        }
    }
}
