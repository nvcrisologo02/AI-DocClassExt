-- =====================================================================
-- medicion/03 - Latencia total (p50 / p95) por modelo de clasificacion
-- =====================================================================
-- Prerrequisitos del cutover a gpt-5.6-luna (AB#99934, task AB#101412).
-- Por ModeloClasificacion: ejecuciones, p50 y p95 de DuracionTotalMs
-- (PERCENTILE_CONT, interpolado) y media. Excluye las reutilizadas por
-- duplicado, que no llaman al modelo.
--
-- Parametros (UTC): @From inclusive, @To exclusivo, @SubmittedByPrefix.
-- Pasada de luna del 2026-11-17 en la ventana limpia de 150K TPM:
--   @From = '2026-11-17 16:34:00', @To = '2026-11-17 17:41:53'.
-- Patron derivado de docs/auxiliares/temps/2026-11-17/golden-luna/coste-latencia.ps1
-- (gitignored). Solo lectura. Un unico resultado, sin GO.
--
--   pwsh -NoProfile -Command "& ./scripts/database/query-sql.ps1 -Server srbsqldevdocai.database.windows.net -Query (Get-Content -Raw scripts/migrations/gpt56-luna/medicion/03-latencia-por-pasada.sql)"
-- =====================================================================
DECLARE @From DATETIME2 = '2026-11-17 16:34:00';
DECLARE @To DATETIME2 = '2026-11-17 17:41:53';
DECLARE @SubmittedByPrefix NVARCHAR(200) = N'DocumentIA.Batch.Evaluation/';

SELECT ModeloClasificacion,
       COUNT(*)                    AS N,
       MAX(P50TotalMs)             AS P50TotalMs,
       MAX(P95TotalMs)             AS P95TotalMs,
       AVG(CAST(DuracionTotalMs AS FLOAT)) AS MediaTotalMs
FROM (
    SELECT ModeloClasificacion, DuracionTotalMs,
           PERCENTILE_CONT(0.5)  WITHIN GROUP (ORDER BY DuracionTotalMs) OVER (PARTITION BY ModeloClasificacion) AS P50TotalMs,
           PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY DuracionTotalMs) OVER (PARTITION BY ModeloClasificacion) AS P95TotalMs
    FROM dbo.DocumentoEjecuciones
    WHERE SubmittedBy LIKE @SubmittedByPrefix + '%'
      AND FechaEjecucion >= @From AND FechaEjecucion < @To
      AND ISNULL(ReutilizadaPorDuplicado, 0) = 0
      AND DuracionTotalMs IS NOT NULL
) x
GROUP BY ModeloClasificacion
ORDER BY ModeloClasificacion;
