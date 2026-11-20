-- =====================================================================
-- medicion/02 - Ejecuciones virtuales con Tdn2 informado y Tdn1 = Tdn2
-- =====================================================================
-- Prerrequisitos del cutover a gpt-5.6-luna (AB#99934, task AB#101412).
-- Por ModeloClasificacion y familia (Tdn1): ejecuciones virtuales
-- (ClasificacionParcial = true, en $.DetalleEjecucion.Clasificacion del
-- ContratoSalidaCompletoJson), cuantas tienen Tdn2 informado y cuantas
-- cumplen Tdn1 = Tdn2 (Tdn1 y Tdn2 en $.Identificacion). Con Tdn1 por
-- familia en la tipologia virtual, el valor esperado en la pasada de luna es
-- Tdn1 = Tdn2 en todas las virtuales con Tdn2; en el control, 0.
--
-- Parametros (UTC): @From inclusive, @To exclusivo, @SubmittedByPrefix.
-- Pasada de luna completa del 2026-11-17 (159 virtuales, 136 con Tdn1 = Tdn2):
--   @From = '2026-11-17 16:10:42', @To = '2026-11-17 17:41:53'.
-- Control (gpt-4o-mini): @From = '2026-11-17 14:50:47', @To = '2026-11-17 16:10:43'.
-- Patron derivado de docs/auxiliares/temps/2026-11-17/golden-luna/coste-latencia.ps1
-- (gitignored). Solo lectura. Un unico resultado, sin GO.
--
--   pwsh -NoProfile -Command "& ./scripts/database/query-sql.ps1 -Server srbsqldevdocai.database.windows.net -Query (Get-Content -Raw scripts/migrations/gpt56-luna/medicion/02-virtuales-tdn1-igual-tdn2.sql)"
-- =====================================================================
DECLARE @From DATETIME2 = '2026-11-17 16:10:42';
DECLARE @To DATETIME2 = '2026-11-17 17:41:53';
DECLARE @SubmittedByPrefix NVARCHAR(200) = N'DocumentIA.Batch.Evaluation/';

WITH Virtuales AS (
    SELECT e.Id, e.ModeloClasificacion,
           JSON_VALUE(e.ContratoSalidaCompletoJson, '$.Identificacion.Tdn1') AS Tdn1,
           JSON_VALUE(e.ContratoSalidaCompletoJson, '$.Identificacion.Tdn2') AS Tdn2
    FROM dbo.DocumentoEjecuciones e
    WHERE e.SubmittedBy LIKE @SubmittedByPrefix + '%'
      AND e.FechaEjecucion >= @From AND e.FechaEjecucion < @To
      AND JSON_VALUE(e.ContratoSalidaCompletoJson, '$.DetalleEjecucion.Clasificacion.ClasificacionParcial') = 'true'
)
SELECT ModeloClasificacion,
       Tdn1 AS Familia,
       COUNT(*)                                                                  AS Virtuales,
       SUM(CASE WHEN Tdn2 IS NOT NULL AND Tdn2 <> N'' THEN 1 ELSE 0 END)         AS ConTdn2,
       SUM(CASE WHEN Tdn2 IS NOT NULL AND Tdn2 <> N'' AND Tdn1 = Tdn2 THEN 1 ELSE 0 END) AS Tdn1IgualTdn2
FROM Virtuales
GROUP BY ROLLUP (ModeloClasificacion, Tdn1)
ORDER BY ModeloClasificacion, Tdn1;
