-- =====================================================================
-- medicion/01 - Coste y tokens por modelo y fase de clasificacion
-- =====================================================================
-- Prerrequisitos del cutover a gpt-5.6-luna (AB#99934, task AB#101412).
-- Por ModeloClasificacion y operacion (classification.phase1 / phase2):
-- ejecuciones, documentos distintos, coste medio y total de clasificacion,
-- tokens medios de entrada, en cache y escritos en cache por llamada,
-- porcentaje de cache (en cache / entrada) y ejecuciones marcadas con
-- '|sin-precio-escritura' en TarifaAplicada (escritura en cache sin precio).
-- El detalle por fase sale de $.DetalleEjecucion.Costes.Consumos del
-- ContratoSalidaCompletoJson. TokensEscrituraCache es NULL en las filas
-- persistidas antes de desplegar AB#101412 (Tarea 6).
--
-- Parametros (UTC): @From inclusive, @To exclusivo, @SubmittedByPrefix.
-- Pasada de luna del 2026-11-17 en la ventana limpia de 150K TPM:
--   @From = '2026-11-17 16:34:00', @To = '2026-11-17 17:41:53'.
-- Patron derivado de docs/auxiliares/temps/2026-11-17/golden-luna/coste-latencia.ps1
-- (gitignored). Solo lectura. Un unico resultado, sin GO.
--
--   pwsh -NoProfile -Command "& ./scripts/database/query-sql.ps1 -Server srbsqldevdocai.database.windows.net -Query (Get-Content -Raw scripts/migrations/gpt56-luna/medicion/01-coste-tokens-por-pasada.sql)"
-- =====================================================================
DECLARE @From DATETIME2 = '2026-11-17 16:34:00';
DECLARE @To DATETIME2 = '2026-11-17 17:41:53';
DECLARE @SubmittedByPrefix NVARCHAR(200) = N'DocumentIA.Batch.Evaluation/';

WITH Fase AS (
    SELECT e.Id, e.DocumentoId, e.ModeloClasificacion, e.CosteClasificacionEur,
           c.Operacion,
           SUM(c.TokensEntrada)        AS TokensEntrada,
           SUM(c.TokensEntradaCache)   AS TokensEntradaCache,
           SUM(c.TokensEscrituraCache) AS TokensEscrituraCache,
           MAX(CASE WHEN c.TarifaAplicada LIKE N'%|sin-precio-escritura' THEN 1 ELSE 0 END) AS SinPrecioEscritura
    FROM dbo.DocumentoEjecuciones e
    CROSS APPLY OPENJSON(e.ContratoSalidaCompletoJson, '$.DetalleEjecucion.Costes.Consumos')
        WITH (Operacion NVARCHAR(100) '$.Operacion',
              Modelo NVARCHAR(200) '$.Modelo',
              TokensEntrada INT '$.TokensEntrada',
              TokensEntradaCache INT '$.TokensEntradaCache',
              TokensEscrituraCache INT '$.TokensEscrituraCache',
              TokensSalida INT '$.TokensSalida',
              CosteEur DECIMAL(12,6) '$.CosteEur',
              TarifaAplicada NVARCHAR(200) '$.TarifaAplicada') c
    WHERE e.SubmittedBy LIKE @SubmittedByPrefix + '%'
      AND e.FechaEjecucion >= @From AND e.FechaEjecucion < @To
      AND c.Operacion IN (N'classification.phase1', N'classification.phase2')
    GROUP BY e.Id, e.DocumentoId, e.ModeloClasificacion, e.CosteClasificacionEur, c.Operacion
)
SELECT ModeloClasificacion, Operacion,
       COUNT(*)                                   AS N,
       COUNT(DISTINCT DocumentoId)                AS DocsDistintos,
       AVG(CosteClasificacionEur)                 AS CosteClasifMedioEur,
       SUM(CosteClasificacionEur)                 AS CosteClasifTotalEur,
       AVG(CAST(TokensEntrada AS FLOAT))          AS TokensEntradaMedio,
       AVG(CAST(TokensEntradaCache AS FLOAT))     AS TokensEnCacheMedio,
       AVG(CAST(TokensEscrituraCache AS FLOAT))   AS TokensEscritosCacheMedio,
       100.0 * SUM(CAST(TokensEntradaCache AS FLOAT)) / NULLIF(SUM(CAST(TokensEntrada AS FLOAT)), 0) AS PctCache,
       SUM(SinPrecioEscritura)                    AS SinPrecioEscritura
FROM Fase
GROUP BY ModeloClasificacion, Operacion
ORDER BY ModeloClasificacion, Operacion;
