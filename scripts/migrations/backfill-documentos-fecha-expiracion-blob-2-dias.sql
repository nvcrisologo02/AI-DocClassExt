-- ============================================================================
-- EP9 - Backfill historico FechaExpiracionBlob
-- Objetivo: aplicar una retencion uniforme de 2 dias a TODAS las tipologias
-- Alcance: solo documentos con blob y sin FechaExpiracionBlob informada
-- Seguridad: idempotente (re-ejecutable sin modificar filas ya backfilleadas)
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @RetentionDays INT = 2;
DECLARE @NowUtc DATETIME2(7) = SYSUTCDATETIME();

;WITH Candidatos AS
(
    SELECT d.Id,
           FechaBaseUtc = COALESCE(d.FechaProceso, d.FechaCreacion, @NowUtc)
    FROM dbo.Documentos d
    WHERE ISNULL(d.RutaBlobStorage, '') <> ''
      AND d.FechaExpiracionBlob IS NULL
)
UPDATE d
SET d.FechaExpiracionBlob = DATEADD(DAY, @RetentionDays, c.FechaBaseUtc),
    d.FechaActualizacion = @NowUtc
FROM dbo.Documentos d
INNER JOIN Candidatos c
    ON c.Id = d.Id;

DECLARE @RowsUpdated INT = @@ROWCOUNT;

COMMIT TRANSACTION;

SELECT
    @RowsUpdated AS RowsUpdated,
    @RetentionDays AS RetentionDaysApplied,
    @NowUtc AS ExecutedAtUtc;
