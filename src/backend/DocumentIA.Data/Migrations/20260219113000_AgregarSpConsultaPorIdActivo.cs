using DocumentIA.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    [DbContext(typeof(DocumentIADbContext))]
    [Migration("20260219113000_AgregarSpConsultaPorIdActivo")]
    public partial class AgregarSpConsultaPorIdActivo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
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
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_DocumentoEjecuciones_IdActivoNormalizado_DocumentoId'
      AND object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
)
BEGIN
    CREATE INDEX IX_DocumentoEjecuciones_IdActivoNormalizado_DocumentoId
        ON dbo.DocumentoEjecuciones (IdActivoNormalizado, DocumentoId);
END;
");

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
        WHERE de.IdActivoNormalizado = @IdActivoNorm
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
        de.DatosOriginalesJson,
        de.DatosFinalesJson,
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.sp_ObtenerDocumentoEjecucionesPorIdActivo', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.sp_ObtenerDocumentoEjecucionesPorIdActivo;
END;
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_DocumentoEjecuciones_IdActivoNormalizado_DocumentoId'
      AND object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
)
BEGIN
    DROP INDEX IX_DocumentoEjecuciones_IdActivoNormalizado_DocumentoId
        ON dbo.DocumentoEjecuciones;
END;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.DocumentoEjecuciones', 'IdActivoNormalizado') IS NOT NULL
BEGIN
    ALTER TABLE dbo.DocumentoEjecuciones
    DROP COLUMN IdActivoNormalizado;
END;
");
        }
    }
}
