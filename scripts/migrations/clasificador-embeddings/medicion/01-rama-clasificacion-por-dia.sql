-- =====================================================================
-- medicion/01 - Rama de clasificacion por dia (clasificador por embeddings)
-- =====================================================================
-- AB#101570 / ADR-006. Por dia UTC de FechaEjecucion, sobre ejecuciones no
-- reutilizadas por duplicado: cuantas clasifico cada rama, la confianza media
-- de la rama embeddings y el acuerdo de TDN1 entre embeddings y GPT.
--
-- Donde se persiste: dbo.DocumentoEjecuciones.ContratoSalidaCompletoJson,
--   $.DetalleEjecucion.Clasificacion.RamaClasificacion  ("gpt" | "embeddings" | "expectedtype")
--   $.DetalleEjecucion.Clasificacion.Embeddings.{Tdn1,Confianza,Decision,Modo}
--   $.DetalleEjecucion.Clasificacion.Tdn1Detectado      (TDN1 de la clasificacion final)
-- Cada ruta se lee en PascalCase y camelCase (COALESCE), segun la fila.
--
-- Limite: GPT y embeddings no se persisten a la vez salvo cuando contesta GPT.
-- Si contesta embeddings (hibrido), no hay resultado GPT y no hay acuerdo
-- medible; si contesta GPT con el bloque Embeddings presente (sombra, o
-- hibrido que derivo a GPT), Tdn1Detectado es el de GPT y se compara con
-- Embeddings.Tdn1. Por eso el acuerdo se calcula solo en esas filas
-- (AcuerdoTdn1 sobre ConAmbos). Ejecuciones sin RamaClasificacion (previas a
-- AB#100779 o con modo off) salen en SinRama.
--
-- Parametros (UTC): @From inclusive, @To exclusivo, @SubmittedByPrefix
-- (cadena vacia = sin filtro de origen).
-- Solo lectura. Un unico resultado, sin GO.
--
--   pwsh -NoProfile -Command "& ./scripts/database/query-sql.ps1 -Server srbsqldevdocai.database.windows.net -Query (Get-Content -Raw scripts/migrations/clasificador-embeddings/medicion/01-rama-clasificacion-por-dia.sql)"
-- =====================================================================
DECLARE @From DATETIME2 = '2026-11-26 00:00:00';
DECLARE @To DATETIME2 = '2026-11-27 00:00:00';
DECLARE @SubmittedByPrefix NVARCHAR(200) = N'';

WITH Ej AS (
    SELECT CAST(e.FechaEjecucion AS DATE) AS Dia,
           COALESCE(JSON_VALUE(e.ContratoSalidaCompletoJson, '$.DetalleEjecucion.Clasificacion.RamaClasificacion'),
                    JSON_VALUE(e.ContratoSalidaCompletoJson, '$.detalleEjecucion.clasificacion.ramaClasificacion')) AS Rama,
           TRY_CAST(COALESCE(JSON_VALUE(e.ContratoSalidaCompletoJson, '$.DetalleEjecucion.Clasificacion.Embeddings.Confianza'),
                             JSON_VALUE(e.ContratoSalidaCompletoJson, '$.detalleEjecucion.clasificacion.embeddings.confianza')) AS FLOAT) AS ConfEmb,
           COALESCE(JSON_VALUE(e.ContratoSalidaCompletoJson, '$.DetalleEjecucion.Clasificacion.Embeddings.Tdn1'),
                    JSON_VALUE(e.ContratoSalidaCompletoJson, '$.detalleEjecucion.clasificacion.embeddings.tdn1')) AS Tdn1Emb,
           COALESCE(JSON_VALUE(e.ContratoSalidaCompletoJson, '$.DetalleEjecucion.Clasificacion.Tdn1Detectado'),
                    JSON_VALUE(e.ContratoSalidaCompletoJson, '$.detalleEjecucion.clasificacion.tdn1Detectado')) AS Tdn1Final
    FROM dbo.DocumentoEjecuciones e
    WHERE e.ReutilizadaPorDuplicado = 0
      AND e.FechaEjecucion >= @From AND e.FechaEjecucion < @To
      AND (@SubmittedByPrefix = N'' OR e.SubmittedBy LIKE @SubmittedByPrefix + '%')
      AND ISJSON(e.ContratoSalidaCompletoJson) = 1
)
SELECT Dia,
       COUNT(*)                                                  AS Ejecuciones,
       SUM(CASE WHEN Rama = N'embeddings' THEN 1 ELSE 0 END)     AS RamaEmbeddings,
       SUM(CASE WHEN Rama = N'gpt' THEN 1 ELSE 0 END)            AS RamaGpt,
       SUM(CASE WHEN Rama = N'expectedtype' THEN 1 ELSE 0 END)   AS RamaExpectedType,
       SUM(CASE WHEN Rama IS NULL THEN 1 ELSE 0 END)             AS SinRama,
       AVG(CASE WHEN Rama = N'embeddings' THEN ConfEmb END)      AS ConfianzaMediaEmbeddings,
       SUM(CASE WHEN Rama = N'gpt' AND Tdn1Emb IS NOT NULL AND Tdn1Final IS NOT NULL THEN 1 ELSE 0 END) AS ConAmbos,
       SUM(CASE WHEN Rama = N'gpt' AND Tdn1Emb IS NOT NULL AND Tdn1Final IS NOT NULL AND Tdn1Emb = Tdn1Final THEN 1 ELSE 0 END) AS AcuerdoTdn1
FROM Ej
GROUP BY Dia
ORDER BY Dia;
