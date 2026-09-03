using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class IdActivoEscalarYSpPorIdActivo : Migration
    {
        /// <inheritdoc />
        // Nota: EF scaffoldea aqui un UpdateData sobre Tipologias por el seed no
        // determinista (DateTime.UtcNow en HasData). Se ha retirado a proposito: esta
        // migracion no debe reescribir datos en PRO.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdActivo",
                table: "DocumentoEjecuciones",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoEjecuciones_IdActivo_DocumentoId",
                table: "DocumentoEjecuciones",
                columns: new[] { "IdActivo", "DocumentoId" });

            // AB#100168: retirar la columna calculada persistida y su indice. Se derivaba de
            // DatosFinalesJson, que dejo de grabarse en AB#100166: para filas nuevas habria
            // quedado siempre vacia.
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_DocumentoEjecuciones_IdActivoNormalizado_DocumentoId'
      AND object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
)
BEGIN
    DROP INDEX IX_DocumentoEjecuciones_IdActivoNormalizado_DocumentoId ON dbo.DocumentoEjecuciones;
END;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.DocumentoEjecuciones', 'IdActivoNormalizado') IS NOT NULL
BEGIN
    ALTER TABLE dbo.DocumentoEjecuciones DROP COLUMN IdActivoNormalizado;
END;
");

            // El backfill del historico NO va aqui a proposito: recorrer JSON_VALUE sobre el
            // contrato (LOB de ~14 KB de media) en toda la tabla agota el timeout de EF ya con
            // 8.000 filas, y dentro de la transaccion de la migracion mantendria bloqueos sobre
            // una tabla de 1,7 GB en PRO. Se hace por lotes con
            // scripts/database/backfill-idactivo.ps1, que es idempotente y reanudable.
            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerDocumentoEjecucionesPorIdActivo
    @IdActivo NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdActivoNorm NVARCHAR(100) = UPPER(LTRIM(RTRIM(@IdActivo)));

    ;WITH DocsObjetivo AS (
        SELECT DISTINCT de.DocumentoId
        FROM dbo.DocumentoEjecuciones de
        WHERE de.IdActivo = @IdActivoNorm
    )
    SELECT
        d.Id                                  AS DocumentoId,
        d.Guid,
        d.NombreArchivo,
        d.IdActivo                            AS IdActivoDocumento,
        d.Estado,
        d.Tipologia                           AS TipologiaDocumento,
        d.FechaCreacion,
        de.Id                                 AS EjecucionId,
        de.EjecucionGuid,
        de.FechaEjecucion,
        de.Tipologia                          AS TipologiaEjecucion,
        de.EstadoFinal,
        de.ConfianzaGlobal                    AS ConfianzaGlobalEjecucion,
        de.ModeloClasificacion                AS ModeloClasificacionEjecucion,
        de.ConfianzaClasificacion             AS ConfianzaClasificacionEjecucion,
        de.UseFallbackLLM,
        de.DuracionTotalMs,
        -- AB#100168: DatosOriginalesJson y DatosFinalesJson dejaron de grabarse (AB#100166).
        -- Se mantienen los mismos nombres de columna en el resultset, extrayendo del contrato
        -- cuando la columna historica no existe.
        COALESCE(de.DatosOriginalesJson,
                 JSON_QUERY(de.ContratoSalidaCompletoJson, '$.DetalleEjecucion.Integracion.DatosOriginales')) AS DatosOriginalesJson,
        COALESCE(de.DatosFinalesJson,
                 JSON_QUERY(de.ContratoSalidaCompletoJson, '$.DatosExtraidos'))                               AS DatosFinalesJson,
        de.ContratoSalidaCompletoJson
    FROM DocsObjetivo x
    JOIN dbo.Documentos d
        ON d.Id = x.DocumentoId
    JOIN dbo.DocumentoEjecuciones de
        ON de.DocumentoId = d.Id
    ORDER BY d.Id, de.FechaEjecucion DESC, de.Id DESC;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.DocumentoEjecuciones', 'IdActivoNormalizado') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentoEjecuciones
    ADD IdActivoNormalizado AS
    CAST(
        UPPER(LTRIM(RTRIM(
            COALESCE(
                JSON_VALUE([DatosFinalesJson], '$.IdActivo'),
                JSON_VALUE([DatosFinalesJson], '$.idActivo'),
                JSON_VALUE([DatosFinalesJson], '$.id_activo'),
                JSON_VALUE([DatosFinalesJson], '$.id_activo_sareb'),
                ''
            )
        )))
    AS NVARCHAR(100)) PERSISTED;
END;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_DocumentoEjecuciones_IdActivoNormalizado_DocumentoId'
      AND object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
)
BEGIN
    CREATE INDEX IX_DocumentoEjecuciones_IdActivoNormalizado_DocumentoId
        ON dbo.DocumentoEjecuciones (IdActivoNormalizado, DocumentoId);
END;
");

            migrationBuilder.DropIndex(
                name: "IX_DocumentoEjecuciones_IdActivo_DocumentoId",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "IdActivo",
                table: "DocumentoEjecuciones");
        }
    }
}
