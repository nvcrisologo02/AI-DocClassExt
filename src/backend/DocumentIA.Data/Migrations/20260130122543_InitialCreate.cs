using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Documentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Guid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NombreArchivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SHA256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CRC32 = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Tipologia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfianzaGlobal = table.Column<double>(type: "float", nullable: true),
                    Paginas = table.Column<int>(type: "int", nullable: false),
                    RutaBlobStorage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IdGDC = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdActivo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaProceso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tipologias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    ModeloClasificacionDI = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UmbralClasificacion = table.Column<double>(type: "float", nullable: false),
                    ModeloExtraccionDI = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UmbralExtraccion = table.Column<double>(type: "float", nullable: false),
                    PromptGPT = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConfiguracionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tipologias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nivel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetallesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auditoria_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResultadosProcesamiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    ModeloClasificacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConfianzaClasificacion = table.Column<double>(type: "float", nullable: true),
                    FallbackLLM = table.Column<bool>(type: "bit", nullable: false),
                    ModeloExtraccion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LayoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DatosExtraidosJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizacionesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidacionesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InconsistenciasJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModuloIntegracion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResultadoIntegracion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TiempoNormalizacionMs = table.Column<int>(type: "int", nullable: true),
                    TiempoClasificacionMs = table.Column<int>(type: "int", nullable: true),
                    TiempoExtraccionMs = table.Column<int>(type: "int", nullable: true),
                    TiempoValidacionMs = table.Column<int>(type: "int", nullable: true),
                    TiempoIntegracionMs = table.Column<int>(type: "int", nullable: true),
                    TiempoTotalMs = table.Column<int>(type: "int", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadosProcesamiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultadosProcesamiento_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Tipologias",
                columns: new[] { "Id", "Activa", "Codigo", "ConfiguracionJson", "CreadoPor", "FechaActualizacion", "FechaCreacion", "ModeloClasificacionDI", "ModeloExtraccionDI", "Nombre", "PromptGPT", "UmbralClasificacion", "UmbralExtraccion", "Version" },
                values: new object[] { 1, true, "tasacion", null, null, null, new DateTime(2026, 1, 30, 12, 25, 42, 817, DateTimeKind.Utc).AddTicks(8190), null, null, "Tasación", null, 0.84999999999999998, 0.80000000000000004, "1.0" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_DocumentoId",
                table: "Auditoria",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_CorrelationId",
                table: "Documentos",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_Estado",
                table: "Documentos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_SHA256",
                table: "Documentos",
                column: "SHA256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosProcesamiento_DocumentoId",
                table: "ResultadosProcesamiento",
                column: "DocumentoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tipologias_Codigo",
                table: "Tipologias",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "ResultadosProcesamiento");

            migrationBuilder.DropTable(
                name: "Tipologias");

            migrationBuilder.DropTable(
                name: "Documentos");
        }
    }
}
