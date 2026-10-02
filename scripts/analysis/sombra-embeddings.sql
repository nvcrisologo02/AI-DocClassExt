-- =====================================================================
-- Analisis de la sombra del clasificador por embeddings (AB#100779)
-- =====================================================================
-- Ejecutar contra DEV (srbsqldevdocai, BD DocumentIA) tras dos semanas o 500
-- ejecuciones en sombra. Lee el bloque Embeddings del contrato persistido
-- (ContratoSalidaCompletoJson, nombres de propiedad en PascalCase).
--
-- Bloques: (1) resumen, (2) acuerdo A/GPT por tramo de confianza, (3) cobertura y
-- acuerdo por umbral, (4) latencia, (5) errores y motivos, (6) listado con SHA256
-- para cruzar fuera de linea con el inventario del corpus (cal/golden) de
-- DocumentIA.Batch: el validado humano no esta en esta BD.
--
-- "Acuerdo" = la tipologia de A coincide con la del GPT (TipologiaDetectada) y
-- TDN1 de A coincide con el TDN1 de esa tipologia en el catalogo. No es acierto:
-- el GPT se equivoca en ~16 % (ver spec); el acierto real sale del cruce del
-- bloque 6 con las etiquetas humanas.
-- =====================================================================
SET NOCOUNT ON;

DECLARE @Desde DATETIME2 = DATEADD(DAY, -14, SYSUTCDATETIME());
DECLARE @Hasta DATETIME2 = SYSUTCDATETIME();

IF OBJECT_ID('tempdb..#sombra') IS NOT NULL DROP TABLE #sombra;

SELECT
    e.Id,
    e.FechaEjecucion,
    e.InstanceId,
    d.SHA256,
    d.NombreArchivo,
    e.Tipologia                                                                   AS TipologiaFinal,
    e.ModeloClasificacion,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.RamaClasificacion')         AS Rama,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.TipologiaDetectada')        AS TipologiaGpt,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Tdn2Detectado')             AS Tdn2Gpt,
    TRY_CONVERT(FLOAT, JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Confianza')) AS ConfianzaGpt,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.VersionModelo')  AS VersionModelo,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Modo')           AS Modo,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.ModoRestringido') AS ModoRestringido,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Tdn1')           AS Tdn1A,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Tdn2')           AS Tdn2A,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Tipologia')      AS TipologiaA,
    TRY_CONVERT(FLOAT, JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Confianza')) AS ConfianzaA,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Decision')       AS Decision,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Motivo')         AS Motivo,
    TRY_CONVERT(INT, JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.LatenciaMs')) AS LatenciaMs,
    JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Error')          AS Error,
    TRY_CONVERT(FLOAT, JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Restringido.Masa')) AS Masa,
    TRY_CONVERT(FLOAT, JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Restringido.ConfianzaCondicionada')) AS ConfianzaCondicionada,
    JSON_QUERY(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Top3')           AS Top3,
    -- TDN1 del catalogo para la tipologia del GPT (ambas grafias de ConfiguracionJson)
    COALESCE(JSON_VALUE(t.ConfiguracionJson, '$.classification.tdn1'),
             JSON_VALUE(t.ConfiguracionJson, '$.Classification.Tdn1'),
             JSON_VALUE(t.ConfiguracionJson, '$.tdn1'),
             JSON_VALUE(t.ConfiguracionJson, '$.Tdn1'))                           AS Tdn1Gpt
INTO #sombra
FROM dbo.DocumentoEjecuciones e
JOIN dbo.Documentos d ON d.Id = e.DocumentoId
CROSS APPLY (SELECT e.ContratoSalidaCompletoJson AS J) c
LEFT JOIN dbo.Tipologias t
       ON t.Codigo = JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.TipologiaDetectada')
      AND t.Estado = 1
WHERE e.FechaEjecucion >= @Desde AND e.FechaEjecucion < @Hasta
  AND e.ReutilizadaPorDuplicado = 0
  AND ISJSON(e.ContratoSalidaCompletoJson) = 1
  AND JSON_VALUE(c.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Decision') IS NOT NULL;

-- 1. Resumen
SELECT COUNT(*) AS Ejecuciones,
       SUM(CASE WHEN Error IS NULL THEN 1 ELSE 0 END) AS SinError,
       SUM(CASE WHEN Error IS NOT NULL THEN 1 ELSE 0 END) AS ConError,
       SUM(CASE WHEN ModoRestringido IS NOT NULL THEN 1 ELSE 0 END) AS Restringidas,
       MIN(FechaEjecucion) AS Desde, MAX(FechaEjecucion) AS Hasta,
       MIN(VersionModelo) AS VersionMin, MAX(VersionModelo) AS VersionMax
FROM #sombra;

-- 2. Acuerdo A / GPT por tramo de confianza de A
SELECT CASE WHEN ConfianzaA >= 0.9 THEN '[0.9,1.0]'
            WHEN ConfianzaA >= 0.8 THEN '[0.8,0.9)'
            WHEN ConfianzaA >= 0.7 THEN '[0.7,0.8)'
            WHEN ConfianzaA >= 0.6 THEN '[0.6,0.7)'
            WHEN ConfianzaA >= 0.5 THEN '[0.5,0.6)'
            ELSE '[0,0.5)' END AS Tramo,
       COUNT(*) AS N,
       AVG(CASE WHEN Tdn1A = Tdn1Gpt THEN 1.0 ELSE 0.0 END) AS AcuerdoTdn1,
       AVG(CASE WHEN TipologiaA = TipologiaGpt THEN 1.0 ELSE 0.0 END) AS AcuerdoTipologia,
       AVG(CASE WHEN TipologiaA IS NULL THEN 1.0 ELSE 0.0 END) AS SinTipologia
FROM #sombra
WHERE Error IS NULL AND Rama = 'gpt'
GROUP BY CASE WHEN ConfianzaA >= 0.9 THEN '[0.9,1.0]'
              WHEN ConfianzaA >= 0.8 THEN '[0.8,0.9)'
              WHEN ConfianzaA >= 0.7 THEN '[0.7,0.8)'
              WHEN ConfianzaA >= 0.6 THEN '[0.6,0.7)'
              WHEN ConfianzaA >= 0.5 THEN '[0.5,0.6)'
              ELSE '[0,0.5)' END
ORDER BY Tramo DESC;

-- 3. Cobertura (A contestaria) y acuerdo por umbral candidato
SELECT u.Umbral,
       AVG(CASE WHEN s.ConfianzaA >= u.Umbral THEN 1.0 ELSE 0.0 END) AS Cobertura,
       AVG(CASE WHEN s.ConfianzaA >= u.Umbral AND s.Tdn1A = s.Tdn1Gpt THEN 1.0
                WHEN s.ConfianzaA >= u.Umbral THEN 0.0 END) AS AcuerdoTdn1EnCubiertas,
       AVG(CASE WHEN s.ConfianzaA >= u.Umbral AND s.TipologiaA = s.TipologiaGpt THEN 1.0
                WHEN s.ConfianzaA >= u.Umbral THEN 0.0 END) AS AcuerdoTipologiaEnCubiertas
FROM (VALUES (0.5), (0.6), (0.7), (0.8), (0.9)) AS u(Umbral)
CROSS JOIN #sombra s
WHERE s.Error IS NULL AND s.Rama = 'gpt'
GROUP BY u.Umbral
ORDER BY u.Umbral;

-- 4. Latencia de la activity (ms)
SELECT DISTINCT
       PERCENTILE_CONT(0.5)  WITHIN GROUP (ORDER BY LatenciaMs) OVER () AS P50,
       PERCENTILE_CONT(0.9)  WITHIN GROUP (ORDER BY LatenciaMs) OVER () AS P90,
       PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY LatenciaMs) OVER () AS P99,
       MAX(LatenciaMs) OVER () AS Max
FROM #sombra
WHERE LatenciaMs IS NOT NULL;

-- 5. Decisiones, motivos y errores
SELECT Decision, Motivo, LEFT(Error, 80) AS Error, COUNT(*) AS N
FROM #sombra
GROUP BY Decision, Motivo, LEFT(Error, 80)
ORDER BY N DESC;

-- 6. Listado para cruzar con el inventario del corpus (sha256;particion;tdn1;tdn2)
SELECT SHA256, NombreArchivo, FechaEjecucion, Rama, TipologiaGpt, Tdn1Gpt, Tdn2Gpt, ConfianzaGpt,
       TipologiaA, Tdn1A, Tdn2A, ConfianzaA, Decision, Motivo, Masa, ConfianzaCondicionada, LatenciaMs, Error, Top3
FROM #sombra
ORDER BY FechaEjecucion;
