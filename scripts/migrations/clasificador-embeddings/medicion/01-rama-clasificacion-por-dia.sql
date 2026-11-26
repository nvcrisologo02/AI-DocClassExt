-- =====================================================================
-- medicion/01 - Rama de clasificacion por dia y modo (clasificador por embeddings)
-- =====================================================================
-- AB#101570 / ADR-006. Por dia UTC de FechaEjecucion y por modo de embeddings
-- persistido, sobre ejecuciones no reutilizadas por duplicado: cuantas clasifico
-- cada rama, la confianza media de la rama embeddings y el acuerdo de TDN1 entre
-- embeddings y GPT en las ejecuciones derivadas a GPT.
--
-- Donde se persiste: dbo.DocumentoEjecuciones.ContratoSalidaCompletoJson,
--   $.DetalleEjecucion.Clasificacion.RamaClasificacion  ("gpt" | "embeddings" | "expectedtype")
--   $.DetalleEjecucion.Clasificacion.Embeddings.{Tdn1,Confianza,Decision,Modo,ModoRestringido}
--   $.DetalleEjecucion.Clasificacion.Tdn1Detectado      (TDN1 de la clasificacion final)
-- Cada ruta se lee en PascalCase y camelCase (COALESCE), segun la fila. El modo
-- (columna Modo) es el efectivo de la ejecucion: ModoRestringido si la peticion
-- traia restriccion y, si no, Modo; vacio si no hay bloque Embeddings.
--
-- LECTURA DEL ACUERDO (AcuerdoTdn1Derivadas sobre ConAmbos): solo hay acuerdo
-- medible en filas con Rama = gpt y bloque Embeddings presente, donde el TDN1
-- final se compara con Embeddings.Tdn1. El TDN1 final es Tdn1Detectado cuando
-- existe (solo lo escribe el proveedor GPT directo) y, si no, el prefijo de
-- TipologiaDetectada hasta el primer '.' o '-', en mayusculas: esa columna vale
-- "ESIN" (TDN1), "ESIN-89" (TDN1-TDN2) o "deac.07" (codigo de tipologia) segun el
-- clasificador que decidio (HybridTDN y FoundryRescue no rellenan Tdn1Detectado).
-- Comprobado en DEV el 2026-11-26: sin esta derivacion ConAmbos quedaba en 0 en
-- casi todos los dias y modos.
--   - En sombra son todas las ejecuciones con embeddings: el acuerdo es una
--     muestra representativa.
--   - En hibrido esas filas son SOLO las que el clasificador de embeddings
--     rechazo por baja confianza y se derivaron a GPT: el acuerdo se calcula
--     unicamente sobre predicciones rechazadas y sale sesgado a la baja. No es
--     el acuerdo del clasificador; las aceptadas (Rama = embeddings) no tienen
--     resultado GPT con el que compararse.
--   - El dia del cambio de modo mezcla sombra e hibrido: por eso se agrupa
--     tambien por Modo y no se deben sumar filas de modos distintos.
-- Ejecuciones sin RamaClasificacion (previas a AB#100779 o con modo off) salen
-- en SinRama.
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
           COALESCE(JSON_VALUE(j.J, '$.DetalleEjecucion.Clasificacion.RamaClasificacion'),
                    JSON_VALUE(j.J, '$.detalleEjecucion.clasificacion.ramaClasificacion')) AS Rama,
           COALESCE(JSON_VALUE(j.J, '$.DetalleEjecucion.Clasificacion.Embeddings.ModoRestringido'),
                    JSON_VALUE(j.J, '$.detalleEjecucion.clasificacion.embeddings.modoRestringido'),
                    JSON_VALUE(j.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Modo'),
                    JSON_VALUE(j.J, '$.detalleEjecucion.clasificacion.embeddings.modo'),
                    N'') AS ModoEmb,
           TRY_CAST(COALESCE(JSON_VALUE(j.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Confianza'),
                             JSON_VALUE(j.J, '$.detalleEjecucion.clasificacion.embeddings.confianza')) AS FLOAT) AS ConfEmb,
           COALESCE(JSON_VALUE(j.J, '$.DetalleEjecucion.Clasificacion.Embeddings.Tdn1'),
                    JSON_VALUE(j.J, '$.detalleEjecucion.clasificacion.embeddings.tdn1')) AS Tdn1Emb,
           COALESCE(JSON_VALUE(j.J, '$.DetalleEjecucion.Clasificacion.Tdn1Detectado'),
                    JSON_VALUE(j.J, '$.detalleEjecucion.clasificacion.tdn1Detectado'),
                    t.Tdn1DeTipologia) AS Tdn1Final
    FROM dbo.DocumentoEjecuciones e
    CROSS APPLY (SELECT CASE WHEN ISJSON(e.ContratoSalidaCompletoJson) = 1 THEN e.ContratoSalidaCompletoJson END AS J) j
    CROSS APPLY (SELECT NULLIF(COALESCE(JSON_VALUE(j.J, '$.DetalleEjecucion.Clasificacion.TipologiaDetectada'),
                                        JSON_VALUE(j.J, '$.detalleEjecucion.clasificacion.tipologiaDetectada')), N'') AS Tip) tp
    CROSS APPLY (SELECT UPPER(LEFT(tp.Tip, PATINDEX('%[.-]%', tp.Tip + N'.') - 1)) AS Tdn1DeTipologia) t
    WHERE e.ReutilizadaPorDuplicado = 0
      AND e.FechaEjecucion >= @From AND e.FechaEjecucion < @To
      AND (@SubmittedByPrefix = N'' OR e.SubmittedBy LIKE @SubmittedByPrefix + '%')
      AND j.J IS NOT NULL
)
SELECT Dia,
       ModoEmb                                                   AS Modo,
       COUNT(*)                                                  AS Ejecuciones,
       SUM(CASE WHEN Rama = N'embeddings' THEN 1 ELSE 0 END)     AS RamaEmbeddings,
       SUM(CASE WHEN Rama = N'gpt' THEN 1 ELSE 0 END)            AS RamaGpt,
       SUM(CASE WHEN Rama = N'expectedtype' THEN 1 ELSE 0 END)   AS RamaExpectedType,
       SUM(CASE WHEN Rama IS NULL THEN 1 ELSE 0 END)             AS SinRama,
       AVG(CASE WHEN Rama = N'embeddings' THEN ConfEmb END)      AS ConfianzaMediaEmbeddings,
       SUM(CASE WHEN Rama = N'gpt' AND Tdn1Emb IS NOT NULL AND Tdn1Final IS NOT NULL THEN 1 ELSE 0 END) AS ConAmbos,
       SUM(CASE WHEN Rama = N'gpt' AND Tdn1Emb IS NOT NULL AND Tdn1Final IS NOT NULL AND Tdn1Emb = Tdn1Final THEN 1 ELSE 0 END) AS AcuerdoTdn1Derivadas
FROM Ej
GROUP BY Dia, ModoEmb
ORDER BY Dia, ModoEmb;
