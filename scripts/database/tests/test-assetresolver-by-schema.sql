:setvar TestDb "DocumentIA_ScriptTest"
SET NOCOUNT ON;
GO
IF DB_ID('$(TestDb)') IS NOT NULL
BEGIN
    ALTER DATABASE [$(TestDb)] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$(TestDb)];
END
GO
CREATE DATABASE [$(TestDb)];
GO
-- STRING_AGG no depende del nivel de compatibilidad (disponible en cualquier compat
-- level sobre motor 2017+); el nivel 150 se fija aqui por robustez general (OPENJSON,
-- etc.), explicitamente porque una instancia con `model` en un nivel inferior
-- propagaria un nivel de compatibilidad insuficiente a la BBDD recien creada.
ALTER DATABASE [$(TestDb)] SET COMPATIBILITY_LEVEL = 150;
GO
USE [$(TestDb)];
GO
CREATE TABLE dbo.Tipologias (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Codigo NVARCHAR(100) NOT NULL,
    Version NVARCHAR(50) NOT NULL DEFAULT '1.0',
    Estado INT NOT NULL DEFAULT 0,   -- Draft=0, Published=1, Retired=2
    Activa BIT NOT NULL DEFAULT 1,
    ConfiguracionJson NVARCHAR(MAX) NULL
);
GO
-- Filas sintéticas (Estado 1 = Published)
INSERT INTO dbo.Tipologias (Codigo, Version, Estado, Activa, ConfiguracionJson) VALUES
-- 1) plano refcat + idufir
('nota-flat-both','1.0',1,1,
 N'{"fields":[{"name":"ReferenciaCatastral","type":"string","rules":[{"ruleType":"regex"}]},{"name":"IDUFIR_CRU","type":"string","rules":[{"ruleType":"regex"}]}]}'),
-- 2) plano solo refcat por regla catastral
('ibi-flat-refcat','1.0',1,1,
 N'{"fields":[{"name":"ReferenciaCatastral","type":"string","rules":[{"ruleType":"catastral"}]}]}'),
-- 3) refcat cualifica por REGLA aunque el nombre no sea alias
('refcat-by-rule','1.0',1,1,
 N'{"fields":[{"name":"RC","type":"string","rules":[{"ruleType":"catastral"}]}]}'),
-- 4) solo idufir por nombre alias
('idufir-only','1.0',1,1,
 N'{"fields":[{"name":"IDUFIR","type":"string"}]}'),
-- 5) anidado en colección
('nested-collection','1.0',1,1,
 N'{"fields":[{"name":"Inmuebles","type":"array","items":{"type":"object","properties":[{"name":"ReferenciaCatastral","type":"string"},{"name":"IDUFIR_CRU","type":"string"}]}}]}'),
-- 6) decoy: valores catastrales, NO cualifica
('valor-decoy','1.0',1,1,
 N'{"fields":[{"name":"ValoracionCatastral","type":"decimal"},{"name":"ValorCatastralTotal","type":"decimal"}]}'),
-- 7) Draft: NO seleccionar
('draft-both','1.0',0,1,
 N'{"fields":[{"name":"ReferenciaCatastral","type":"string"},{"name":"IDUFIR_CRU","type":"string"}]}'),
-- 8) Inactiva: NO seleccionar
('inactive-both','1.0',1,0,
 N'{"fields":[{"name":"ReferenciaCatastral","type":"string"},{"name":"IDUFIR_CRU","type":"string"}]}'),
-- 9) assetResolver preexistente con dirección (debe preservarse)
('preexisting-ar','1.0',1,1,
 N'{"fields":[{"name":"ReferenciaCatastral","type":"string"}],"assetResolver":{"enabled":false,"busquedaDireccionHabilitada":true,"mapeoDireccionMunicipio":["Municipio"]}}'),
-- 10) JSON inválido: no procesable
('invalid-json','1.0',1,1, N'esto no es json'),
-- 11) mezcla plano refcat + colección con idufir
('mixed-flat-collection','1.0',1,1,
 N'{"fields":[{"name":"ReferenciaCatastral","type":"string"},{"name":"Inmuebles","type":"array","items":{"type":"object","properties":[{"name":"IDUFIR_CRU","type":"string"}]}}]}'),
-- 12) PascalCase, refcat anidado array-de-strings en coleccion 'Resumen', con AssetResolver preexistente PascalCase Enabled:false
('cera-pascal-nested','1.0',1,1,
 N'{"Fields":[{"Name":"Titular","Type":"string"},{"Name":"Resumen","Type":"array","Items":{"Type":"object","Properties":[{"Name":"Municipio","Type":"string"},{"Name":"ReferenciaCatastral","Type":"array","Items":{"Type":"string","Properties":[]}}]}}],"AssetResolver":{"Enabled":false,"CamposSolicitados":["#ALL#"],"MapeoIdufir":["IDUFIR_CRU"],"MapeoReferenciaCatastral":["ReferenciaCatastral"],"ModoCombinacionCriterios":"OR","BusquedaDireccionHabilitada":false}}'),
-- 13) PascalCase, refcat + idufir planos
('notasimple-pascal-flat','1.0',1,1,
 N'{"Fields":[{"Name":"ReferenciaCatastral","Type":"string"},{"Name":"IDUFIR_CRU","Type":"string"}],"AssetResolver":{"Enabled":false}}');
GO

-- ===== Módulo de descubrimiento (robusto a casing; se reutiliza en el script de
-- activación) =====
-- NOTA DE CORRECCIÓN: OPENJSON aborta el statement completo si jsonExpression no es
-- JSON válido ("the statement fails if the JSON text is not properly formatted").
-- Filtrar con ISJSON() en el WHERE de FieldsFlat (como proponía el borrador original)
-- NO evita el error, porque WHERE se aplica lógica y físicamente después de que
-- CROSS APPLY ya invocó OPENJSON sobre todas las filas de dbo.Tipologias, incluida
-- 'invalid-json'. Se resuelve pre-filtrando en una CTE propia (V) para que OPENJSON
-- nunca reciba ConfiguracionJson inválido.
-- ROBUSTO A CASING: en prod conviven ConfiguracionJson en camelCase ('fields',
-- 'name'...) y en PascalCase ('Fields', 'Name'...). Como los paths de
-- OPENJSON/JSON_VALUE/JSON_QUERY son sensibles a mayúsculas, toda extracción usa
-- COALESCE(camel, Pascal). IsPascal se calcula por fila (presencia de la clave
-- '$.Fields') y viaja en #Descubrimiento para que la aplicación sepa en qué rama
-- escribir. El filtro Estado=1 AND Activa=1 NO se aplica en V (para que las filas
-- draft/inactive sigan apareciendo en #Descubrimiento con EsObjetivo=0), sino en el
-- cálculo de EsObjetivo.
;WITH V AS (
    SELECT Id, ConfiguracionJson,
        CASE WHEN JSON_QUERY(ConfiguracionJson,'$.Fields') IS NOT NULL THEN 1 ELSE 0 END AS IsPascal,
        COALESCE(JSON_QUERY(ConfiguracionJson,'$.fields'), JSON_QUERY(ConfiguracionJson,'$.Fields')) AS FieldsJson
    FROM dbo.Tipologias
    WHERE ISJSON(ConfiguracionJson) = 1
),
FieldsFlat AS (
    SELECT v.Id, v.IsPascal,
        COALESCE(JSON_VALUE(fe.value,'$.name'), JSON_VALUE(fe.value,'$.Name')) AS [name],
        COALESCE(JSON_VALUE(fe.value,'$.type'), JSON_VALUE(fe.value,'$.Type')) AS [type],
        COALESCE(JSON_QUERY(fe.value,'$.rules'), JSON_QUERY(fe.value,'$.Rules')) AS rules,
        COALESCE(JSON_QUERY(fe.value,'$.items.properties'), JSON_QUERY(fe.value,'$.Items.Properties')) AS itemsProps
    FROM V v
    CROSS APPLY OPENJSON(v.FieldsJson) fe
    WHERE v.FieldsJson IS NOT NULL
),
FieldsNested AS (
    SELECT f.Id, f.[name] AS ColeccionName,
        COALESCE(JSON_VALUE(p.value,'$.name'), JSON_VALUE(p.value,'$.Name')) AS PropName,
        COALESCE(JSON_QUERY(p.value,'$.rules'), JSON_QUERY(p.value,'$.Rules')) AS rules
    FROM FieldsFlat f
    CROSS APPLY OPENJSON(f.itemsProps) p
    WHERE LOWER(f.[type]) = 'array' AND f.itemsProps IS NOT NULL
),
Qualified AS (
    -- planos refcat
    SELECT Id,'REF' AS Kind,[name] AS FieldName, CAST(NULL AS NVARCHAR(200)) AS ColeccionName
    FROM FieldsFlat f
    WHERE LOWER([name]) IN ('referenciacatastral','refcatastral','catastral')
       OR (ISJSON(f.rules)=1 AND EXISTS (SELECT 1 FROM OPENJSON(f.rules) r WHERE LOWER(COALESCE(JSON_VALUE(r.value,'$.ruleType'),JSON_VALUE(r.value,'$.RuleType')))='catastral'))
    UNION ALL
    -- planos idufir
    SELECT Id,'IDU',[name],NULL FROM FieldsFlat
    WHERE LOWER([name]) IN ('idufir_cru','idufir','cru','codigoregistrounico')
    UNION ALL
    -- anidados refcat
    SELECT Id,'REF',PropName,ColeccionName FROM FieldsNested n
    WHERE LOWER(PropName) IN ('referenciacatastral','refcatastral','catastral')
       OR (ISJSON(n.rules)=1 AND EXISTS (SELECT 1 FROM OPENJSON(n.rules) r WHERE LOWER(COALESCE(JSON_VALUE(r.value,'$.ruleType'),JSON_VALUE(r.value,'$.RuleType')))='catastral'))
    UNION ALL
    -- anidados idufir
    SELECT Id,'IDU',PropName,ColeccionName FROM FieldsNested
    WHERE LOWER(PropName) IN ('idufir_cru','idufir','cru','codigoregistrounico')
),
Arrays AS (
    SELECT q.Id,
        (SELECT '[' + STRING_AGG('"' + STRING_ESCAPE(FieldName,'json') + '"', ',') WITHIN GROUP (ORDER BY FieldName) + ']'
         FROM (SELECT DISTINCT FieldName FROM Qualified x WHERE x.Id=q.Id AND x.Kind='REF') a) AS RefcatArr,
        (SELECT '[' + STRING_AGG('"' + STRING_ESCAPE(FieldName,'json') + '"', ',') WITHIN GROUP (ORDER BY FieldName) + ']'
         FROM (SELECT DISTINCT FieldName FROM Qualified x WHERE x.Id=q.Id AND x.Kind='IDU') b) AS IdufirArr,
        (SELECT '[' + STRING_AGG('"' + STRING_ESCAPE(ColeccionName,'json') + '"', ',') WITHIN GROUP (ORDER BY ColeccionName) + ']'
         FROM (SELECT DISTINCT ColeccionName FROM Qualified x WHERE x.Id=q.Id AND x.ColeccionName IS NOT NULL) c) AS ColeccionArr
    FROM (SELECT DISTINCT Id FROM Qualified) q
)
SELECT t.Id, t.Codigo,
       a.RefcatArr, a.IdufirArr, a.ColeccionArr,
       CASE WHEN a.Id IS NOT NULL AND t.Estado=1 AND t.Activa=1 THEN 1 ELSE 0 END AS EsObjetivo,
       ISJSON(t.ConfiguracionJson) AS Procesable,
       v.IsPascal
INTO #Descubrimiento
FROM dbo.Tipologias t
LEFT JOIN V v ON v.Id = t.Id
LEFT JOIN Arrays a ON a.Id = t.Id;

-- ===== Aserciones =====
DECLARE @fail INT = 0;

IF NOT EXISTS (SELECT 1 FROM #Descubrimiento d JOIN dbo.Tipologias t ON t.Id=d.Id
    WHERE t.Codigo='nota-flat-both' AND d.EsObjetivo=1
      AND d.RefcatArr='["ReferenciaCatastral"]' AND d.IdufirArr='["IDUFIR_CRU"]' AND d.ColeccionArr IS NULL)
    BEGIN SET @fail=1; PRINT 'FAIL: nota-flat-both'; END;

IF NOT EXISTS (SELECT 1 FROM #Descubrimiento d JOIN dbo.Tipologias t ON t.Id=d.Id
    WHERE t.Codigo='refcat-by-rule' AND d.EsObjetivo=1 AND d.RefcatArr='["RC"]' AND d.IdufirArr IS NULL)
    BEGIN SET @fail=1; PRINT 'FAIL: refcat-by-rule'; END;

IF NOT EXISTS (SELECT 1 FROM #Descubrimiento d JOIN dbo.Tipologias t ON t.Id=d.Id
    WHERE t.Codigo='nested-collection' AND d.EsObjetivo=1
      AND d.RefcatArr='["ReferenciaCatastral"]' AND d.IdufirArr='["IDUFIR_CRU"]' AND d.ColeccionArr='["Inmuebles"]')
    BEGIN SET @fail=1; PRINT 'FAIL: nested-collection'; END;

IF EXISTS (SELECT 1 FROM #Descubrimiento d JOIN dbo.Tipologias t ON t.Id=d.Id
    WHERE t.Codigo IN ('valor-decoy','draft-both','inactive-both') AND d.EsObjetivo=1)
    BEGIN SET @fail=1; PRINT 'FAIL: decoy/draft/inactive seleccionados'; END;

IF NOT EXISTS (SELECT 1 FROM #Descubrimiento d JOIN dbo.Tipologias t ON t.Id=d.Id
    WHERE t.Codigo='mixed-flat-collection' AND d.EsObjetivo=1
      AND d.RefcatArr='["ReferenciaCatastral"]' AND d.IdufirArr='["IDUFIR_CRU"]' AND d.ColeccionArr='["Inmuebles"]')
    BEGIN SET @fail=1; PRINT 'FAIL: mixed-flat-collection'; END;

IF EXISTS (SELECT 1 FROM #Descubrimiento d JOIN dbo.Tipologias t ON t.Id=d.Id
    WHERE t.Codigo='invalid-json' AND d.Procesable=1)
    BEGIN SET @fail=1; PRINT 'FAIL: invalid-json marcado procesable'; END;

-- PascalCase: refcat anidado como array-de-strings dentro de una colección (forma real
-- de prod, p.ej. cera.46). Cubre el bug original: descubrimiento debe leer $.Fields
-- (no solo $.fields) y marcar IsPascal=1.
IF NOT EXISTS (SELECT 1 FROM #Descubrimiento d JOIN dbo.Tipologias t ON t.Id=d.Id
    WHERE t.Codigo='cera-pascal-nested' AND d.EsObjetivo=1
      AND d.RefcatArr='["ReferenciaCatastral"]' AND d.IdufirArr IS NULL
      AND d.ColeccionArr='["Resumen"]' AND d.IsPascal=1)
    BEGIN PRINT 'FAIL: cera-pascal-nested descubrimiento'; THROW 50007,'cera-pascal-nested descubrimiento',1; END

-- PascalCase: refcat + idufir planos.
IF NOT EXISTS (SELECT 1 FROM #Descubrimiento d JOIN dbo.Tipologias t ON t.Id=d.Id
    WHERE t.Codigo='notasimple-pascal-flat' AND d.EsObjetivo=1
      AND d.RefcatArr='["ReferenciaCatastral"]' AND d.IdufirArr='["IDUFIR_CRU"]'
      AND d.ColeccionArr IS NULL AND d.IsPascal=1)
    BEGIN PRINT 'FAIL: notasimple-pascal-flat descubrimiento'; THROW 50008,'notasimple-pascal-flat descubrimiento',1; END

IF @fail=1 THROW 50001, 'Descubrimiento: aserciones fallidas', 1;
PRINT 'OK: descubrimiento';
GO

-- ===== Aplicación (Task 2): snapshot original + aplicación real + aserciones =====

-- Snapshot ANTES de cualquier aplicación (lo necesita la aserción de rollback de la Task 3).
SELECT Id, ConfiguracionJson INTO #orig FROM dbo.Tipologias;
GO

-- Aplicación 1: reutiliza el módulo de descubrimiento robusto a casing (arriba) + las
-- DOS ramas de escritura del script real (scripts/database/enable-assetresolver-by-schema.sql,
-- secciones 1, 3 y 4), con @WhatIf=0 fijo (copia intencional, es un harness de test). Se
-- omite aquí el paso de backup a tabla física del script real: su nombre depende de un
-- timestamp con resolución de segundo y este bloque se reaplica dos veces en el mismo
-- test (aplicación + idempotencia), lo que podría colisionar de nombre; el backup ya se
-- revisa de forma aislada al ejecutar el script real.
;WITH V AS (
    SELECT Id, ConfiguracionJson,
        CASE WHEN JSON_QUERY(ConfiguracionJson,'$.Fields') IS NOT NULL THEN 1 ELSE 0 END AS IsPascal,
        COALESCE(JSON_QUERY(ConfiguracionJson,'$.fields'), JSON_QUERY(ConfiguracionJson,'$.Fields')) AS FieldsJson
    FROM dbo.Tipologias
    WHERE ISJSON(ConfiguracionJson) = 1 AND Estado = 1 AND Activa = 1
),
FieldsFlat AS (
    SELECT v.Id, v.IsPascal,
        COALESCE(JSON_VALUE(fe.value,'$.name'), JSON_VALUE(fe.value,'$.Name')) AS [name],
        COALESCE(JSON_VALUE(fe.value,'$.type'), JSON_VALUE(fe.value,'$.Type')) AS [type],
        COALESCE(JSON_QUERY(fe.value,'$.rules'), JSON_QUERY(fe.value,'$.Rules')) AS rules,
        COALESCE(JSON_QUERY(fe.value,'$.items.properties'), JSON_QUERY(fe.value,'$.Items.Properties')) AS itemsProps
    FROM V v
    CROSS APPLY OPENJSON(v.FieldsJson) fe
    WHERE v.FieldsJson IS NOT NULL
),
FieldsNested AS (
    SELECT f.Id, f.[name] AS ColeccionName,
        COALESCE(JSON_VALUE(p.value,'$.name'), JSON_VALUE(p.value,'$.Name')) AS PropName,
        COALESCE(JSON_QUERY(p.value,'$.rules'), JSON_QUERY(p.value,'$.Rules')) AS rules
    FROM FieldsFlat f
    CROSS APPLY OPENJSON(f.itemsProps) p
    WHERE LOWER(f.[type]) = 'array' AND f.itemsProps IS NOT NULL
),
Qualified AS (
    -- planos refcat
    SELECT Id,'REF' AS Kind,[name] AS FieldName, CAST(NULL AS NVARCHAR(200)) AS ColeccionName
    FROM FieldsFlat f
    WHERE LOWER([name]) IN ('referenciacatastral','refcatastral','catastral')
       OR (ISJSON(f.rules)=1 AND EXISTS (SELECT 1 FROM OPENJSON(f.rules) r WHERE LOWER(COALESCE(JSON_VALUE(r.value,'$.ruleType'),JSON_VALUE(r.value,'$.RuleType')))='catastral'))
    UNION ALL
    -- planos idufir
    SELECT Id,'IDU',[name],NULL FROM FieldsFlat
    WHERE LOWER([name]) IN ('idufir_cru','idufir','cru','codigoregistrounico')
    UNION ALL
    -- anidados refcat
    SELECT Id,'REF',PropName,ColeccionName FROM FieldsNested n
    WHERE LOWER(PropName) IN ('referenciacatastral','refcatastral','catastral')
       OR (ISJSON(n.rules)=1 AND EXISTS (SELECT 1 FROM OPENJSON(n.rules) r WHERE LOWER(COALESCE(JSON_VALUE(r.value,'$.ruleType'),JSON_VALUE(r.value,'$.RuleType')))='catastral'))
    UNION ALL
    -- anidados idufir
    SELECT Id,'IDU',PropName,ColeccionName FROM FieldsNested
    WHERE LOWER(PropName) IN ('idufir_cru','idufir','cru','codigoregistrounico')
)
SELECT q.Id, v.IsPascal,
    (SELECT '['+STRING_AGG('"'+STRING_ESCAPE(FieldName,'json')+'"',',') WITHIN GROUP (ORDER BY FieldName)+']'
     FROM (SELECT DISTINCT FieldName FROM Qualified x WHERE x.Id=q.Id AND x.Kind='REF') a) AS RefcatArr,
    (SELECT '['+STRING_AGG('"'+STRING_ESCAPE(FieldName,'json')+'"',',') WITHIN GROUP (ORDER BY FieldName)+']'
     FROM (SELECT DISTINCT FieldName FROM Qualified x WHERE x.Id=q.Id AND x.Kind='IDU') b) AS IdufirArr,
    (SELECT '['+STRING_AGG('"'+STRING_ESCAPE(ColeccionName,'json')+'"',',') WITHIN GROUP (ORDER BY ColeccionName)+']'
     FROM (SELECT DISTINCT ColeccionName FROM Qualified x WHERE x.Id=q.Id AND x.ColeccionName IS NOT NULL) c) AS ColeccionArr
INTO #obj
FROM (SELECT DISTINCT Id FROM Qualified) q
JOIN V v ON v.Id = q.Id;

-- Escritura rama camelCase (IsPascal=0): setea claves camelCase; arrays NULL -> se
-- borra la clave (JSON_MODIFY con JSON_QUERY(NULL) en modo lax).
UPDATE t SET ConfiguracionJson =
    JSON_MODIFY(
      JSON_MODIFY(
        JSON_MODIFY(
          JSON_MODIFY(
            JSON_MODIFY(
              JSON_MODIFY(
                JSON_MODIFY(t.ConfiguracionJson,'$.assetResolver',
                   JSON_QUERY(ISNULL(JSON_QUERY(t.ConfiguracionJson,'$.assetResolver'),'{}'))),
                '$.assetResolver.enabled', CAST(1 AS BIT)),
              '$.assetResolver.busquedaReferenciaCatastralHabilitada', CAST(1 AS BIT)),
            '$.assetResolver.busquedaIdufirHabilitada', CAST(1 AS BIT)),
          '$.assetResolver.mapeoReferenciaCatastral', JSON_QUERY(o.RefcatArr)),
        '$.assetResolver.mapeoIdufir', JSON_QUERY(o.IdufirArr)),
      '$.assetResolver.mapeoColeccionActivos', JSON_QUERY(o.ColeccionArr))
FROM dbo.Tipologias t JOIN #obj o ON o.Id=t.Id
WHERE o.IsPascal = 0;

-- Escritura rama PascalCase (IsPascal=1): mismas claves en PascalCase para overwrite en
-- sitio del bloque AssetResolver existente (no crear duplicado camelCase).
UPDATE t SET ConfiguracionJson =
    JSON_MODIFY(
      JSON_MODIFY(
        JSON_MODIFY(
          JSON_MODIFY(
            JSON_MODIFY(
              JSON_MODIFY(
                JSON_MODIFY(t.ConfiguracionJson,'$.AssetResolver',
                   JSON_QUERY(ISNULL(JSON_QUERY(t.ConfiguracionJson,'$.AssetResolver'),'{}'))),
                '$.AssetResolver.Enabled', CAST(1 AS BIT)),
              '$.AssetResolver.BusquedaReferenciaCatastralHabilitada', CAST(1 AS BIT)),
            '$.AssetResolver.BusquedaIdufirHabilitada', CAST(1 AS BIT)),
          '$.AssetResolver.MapeoReferenciaCatastral', JSON_QUERY(o.RefcatArr)),
        '$.AssetResolver.MapeoIdufir', JSON_QUERY(o.IdufirArr)),
      '$.AssetResolver.MapeoColeccionActivos', JSON_QUERY(o.ColeccionArr))
FROM dbo.Tipologias t JOIN #obj o ON o.Id=t.Id
WHERE o.IsPascal = 1;

DROP TABLE #obj;
GO

-- ===== Aserciones de aplicación =====

-- a) preservación de assetResolver preexistente
IF NOT EXISTS (SELECT 1 FROM dbo.Tipologias
    WHERE Codigo='preexisting-ar'
      AND JSON_VALUE(ConfiguracionJson,'$.assetResolver.enabled')='true'
      AND JSON_VALUE(ConfiguracionJson,'$.assetResolver.busquedaDireccionHabilitada')='true'
      AND JSON_VALUE(ConfiguracionJson,'$.assetResolver.mapeoReferenciaCatastral[0]')='ReferenciaCatastral')
    BEGIN PRINT 'FAIL: preexisting-ar no preservado'; THROW 50002,'preexisting-ar',1; END

-- b) omisión de mapeoIdufir cuando no hay idufir (ibi-flat-refcat)
IF JSON_QUERY((SELECT ConfiguracionJson FROM dbo.Tipologias WHERE Codigo='ibi-flat-refcat'),'$.assetResolver.mapeoIdufir') IS NOT NULL
    BEGIN PRINT 'FAIL: ibi-flat-refcat no debe tener mapeoIdufir'; THROW 50003,'ibi mapeoIdufir',1; END

-- c) decoy sin assetResolver
IF JSON_QUERY((SELECT ConfiguracionJson FROM dbo.Tipologias WHERE Codigo='valor-decoy'),'$.assetResolver') IS NOT NULL
    BEGIN PRINT 'FAIL: valor-decoy no debe tener assetResolver'; THROW 50004,'decoy',1; END

-- d) cera-pascal-nested: escritura en rama PascalCase; refcat anidado en 'Resumen'
-- mapea la colección padre; sin idufir se borra la clave; se preserva CamposSolicitados
IF NOT EXISTS (SELECT 1 FROM dbo.Tipologias
    WHERE Codigo='cera-pascal-nested'
      AND JSON_VALUE(ConfiguracionJson,'$.AssetResolver.Enabled')='true'
      AND JSON_VALUE(ConfiguracionJson,'$.AssetResolver.MapeoColeccionActivos[0]')='Resumen'
      AND JSON_QUERY(ConfiguracionJson,'$.AssetResolver.MapeoIdufir') IS NULL
      AND JSON_VALUE(ConfiguracionJson,'$.AssetResolver.CamposSolicitados[0]')='#ALL#')
    BEGIN PRINT 'FAIL: cera-pascal-nested aplicacion'; THROW 50009,'cera-pascal-nested aplicacion',1; END

-- e) cera-pascal-nested: no debe crearse una clave camelCase duplicada ('$.assetResolver')
IF EXISTS (SELECT 1 FROM dbo.Tipologias
    WHERE Codigo='cera-pascal-nested' AND JSON_QUERY(ConfiguracionJson,'$.assetResolver') IS NOT NULL)
    BEGIN PRINT 'FAIL: cera-pascal-nested no debe crear assetResolver camelCase duplicado'; THROW 50010,'cera-pascal-nested duplicado',1; END

-- f) notasimple-pascal-flat: escritura en rama PascalCase; refcat + idufir planos, sin colección
IF NOT EXISTS (SELECT 1 FROM dbo.Tipologias
    WHERE Codigo='notasimple-pascal-flat'
      AND JSON_VALUE(ConfiguracionJson,'$.AssetResolver.Enabled')='true'
      AND JSON_VALUE(ConfiguracionJson,'$.AssetResolver.MapeoReferenciaCatastral[0]')='ReferenciaCatastral'
      AND JSON_VALUE(ConfiguracionJson,'$.AssetResolver.MapeoIdufir[0]')='IDUFIR_CRU'
      AND JSON_QUERY(ConfiguracionJson,'$.AssetResolver.MapeoColeccionActivos') IS NULL)
    BEGIN PRINT 'FAIL: notasimple-pascal-flat aplicacion'; THROW 50011,'notasimple-pascal-flat aplicacion',1; END

-- g) notasimple-pascal-flat: no debe crearse una clave camelCase duplicada ('$.assetResolver')
IF EXISTS (SELECT 1 FROM dbo.Tipologias
    WHERE Codigo='notasimple-pascal-flat' AND JSON_QUERY(ConfiguracionJson,'$.assetResolver') IS NOT NULL)
    BEGIN PRINT 'FAIL: notasimple-pascal-flat no debe crear assetResolver camelCase duplicado'; THROW 50012,'notasimple-pascal-flat duplicado',1; END

PRINT 'OK: aplicacion';
GO

-- ===== Idempotencia: snapshot tras la 1ª aplicación, reaplicar, comparar =====
SELECT Id, ConfiguracionJson INTO #snap FROM dbo.Tipologias;
GO

-- Aplicación 2: bloque de descubrimiento + escritura en dos ramas idéntico a la
-- aplicación 1.
;WITH V AS (
    SELECT Id, ConfiguracionJson,
        CASE WHEN JSON_QUERY(ConfiguracionJson,'$.Fields') IS NOT NULL THEN 1 ELSE 0 END AS IsPascal,
        COALESCE(JSON_QUERY(ConfiguracionJson,'$.fields'), JSON_QUERY(ConfiguracionJson,'$.Fields')) AS FieldsJson
    FROM dbo.Tipologias
    WHERE ISJSON(ConfiguracionJson) = 1 AND Estado = 1 AND Activa = 1
),
FieldsFlat AS (
    SELECT v.Id, v.IsPascal,
        COALESCE(JSON_VALUE(fe.value,'$.name'), JSON_VALUE(fe.value,'$.Name')) AS [name],
        COALESCE(JSON_VALUE(fe.value,'$.type'), JSON_VALUE(fe.value,'$.Type')) AS [type],
        COALESCE(JSON_QUERY(fe.value,'$.rules'), JSON_QUERY(fe.value,'$.Rules')) AS rules,
        COALESCE(JSON_QUERY(fe.value,'$.items.properties'), JSON_QUERY(fe.value,'$.Items.Properties')) AS itemsProps
    FROM V v
    CROSS APPLY OPENJSON(v.FieldsJson) fe
    WHERE v.FieldsJson IS NOT NULL
),
FieldsNested AS (
    SELECT f.Id, f.[name] AS ColeccionName,
        COALESCE(JSON_VALUE(p.value,'$.name'), JSON_VALUE(p.value,'$.Name')) AS PropName,
        COALESCE(JSON_QUERY(p.value,'$.rules'), JSON_QUERY(p.value,'$.Rules')) AS rules
    FROM FieldsFlat f
    CROSS APPLY OPENJSON(f.itemsProps) p
    WHERE LOWER(f.[type]) = 'array' AND f.itemsProps IS NOT NULL
),
Qualified AS (
    -- planos refcat
    SELECT Id,'REF' AS Kind,[name] AS FieldName, CAST(NULL AS NVARCHAR(200)) AS ColeccionName
    FROM FieldsFlat f
    WHERE LOWER([name]) IN ('referenciacatastral','refcatastral','catastral')
       OR (ISJSON(f.rules)=1 AND EXISTS (SELECT 1 FROM OPENJSON(f.rules) r WHERE LOWER(COALESCE(JSON_VALUE(r.value,'$.ruleType'),JSON_VALUE(r.value,'$.RuleType')))='catastral'))
    UNION ALL
    -- planos idufir
    SELECT Id,'IDU',[name],NULL FROM FieldsFlat
    WHERE LOWER([name]) IN ('idufir_cru','idufir','cru','codigoregistrounico')
    UNION ALL
    -- anidados refcat
    SELECT Id,'REF',PropName,ColeccionName FROM FieldsNested n
    WHERE LOWER(PropName) IN ('referenciacatastral','refcatastral','catastral')
       OR (ISJSON(n.rules)=1 AND EXISTS (SELECT 1 FROM OPENJSON(n.rules) r WHERE LOWER(COALESCE(JSON_VALUE(r.value,'$.ruleType'),JSON_VALUE(r.value,'$.RuleType')))='catastral'))
    UNION ALL
    -- anidados idufir
    SELECT Id,'IDU',PropName,ColeccionName FROM FieldsNested
    WHERE LOWER(PropName) IN ('idufir_cru','idufir','cru','codigoregistrounico')
)
SELECT q.Id, v.IsPascal,
    (SELECT '['+STRING_AGG('"'+STRING_ESCAPE(FieldName,'json')+'"',',') WITHIN GROUP (ORDER BY FieldName)+']'
     FROM (SELECT DISTINCT FieldName FROM Qualified x WHERE x.Id=q.Id AND x.Kind='REF') a) AS RefcatArr,
    (SELECT '['+STRING_AGG('"'+STRING_ESCAPE(FieldName,'json')+'"',',') WITHIN GROUP (ORDER BY FieldName)+']'
     FROM (SELECT DISTINCT FieldName FROM Qualified x WHERE x.Id=q.Id AND x.Kind='IDU') b) AS IdufirArr,
    (SELECT '['+STRING_AGG('"'+STRING_ESCAPE(ColeccionName,'json')+'"',',') WITHIN GROUP (ORDER BY ColeccionName)+']'
     FROM (SELECT DISTINCT ColeccionName FROM Qualified x WHERE x.Id=q.Id AND x.ColeccionName IS NOT NULL) c) AS ColeccionArr
INTO #obj
FROM (SELECT DISTINCT Id FROM Qualified) q
JOIN V v ON v.Id = q.Id;

UPDATE t SET ConfiguracionJson =
    JSON_MODIFY(
      JSON_MODIFY(
        JSON_MODIFY(
          JSON_MODIFY(
            JSON_MODIFY(
              JSON_MODIFY(
                JSON_MODIFY(t.ConfiguracionJson,'$.assetResolver',
                   JSON_QUERY(ISNULL(JSON_QUERY(t.ConfiguracionJson,'$.assetResolver'),'{}'))),
                '$.assetResolver.enabled', CAST(1 AS BIT)),
              '$.assetResolver.busquedaReferenciaCatastralHabilitada', CAST(1 AS BIT)),
            '$.assetResolver.busquedaIdufirHabilitada', CAST(1 AS BIT)),
          '$.assetResolver.mapeoReferenciaCatastral', JSON_QUERY(o.RefcatArr)),
        '$.assetResolver.mapeoIdufir', JSON_QUERY(o.IdufirArr)),
      '$.assetResolver.mapeoColeccionActivos', JSON_QUERY(o.ColeccionArr))
FROM dbo.Tipologias t JOIN #obj o ON o.Id=t.Id
WHERE o.IsPascal = 0;

UPDATE t SET ConfiguracionJson =
    JSON_MODIFY(
      JSON_MODIFY(
        JSON_MODIFY(
          JSON_MODIFY(
            JSON_MODIFY(
              JSON_MODIFY(
                JSON_MODIFY(t.ConfiguracionJson,'$.AssetResolver',
                   JSON_QUERY(ISNULL(JSON_QUERY(t.ConfiguracionJson,'$.AssetResolver'),'{}'))),
                '$.AssetResolver.Enabled', CAST(1 AS BIT)),
              '$.AssetResolver.BusquedaReferenciaCatastralHabilitada', CAST(1 AS BIT)),
            '$.AssetResolver.BusquedaIdufirHabilitada', CAST(1 AS BIT)),
          '$.AssetResolver.MapeoReferenciaCatastral', JSON_QUERY(o.RefcatArr)),
        '$.AssetResolver.MapeoIdufir', JSON_QUERY(o.IdufirArr)),
      '$.AssetResolver.MapeoColeccionActivos', JSON_QUERY(o.ColeccionArr))
FROM dbo.Tipologias t JOIN #obj o ON o.Id=t.Id
WHERE o.IsPascal = 1;

DROP TABLE #obj;
GO

IF EXISTS (SELECT 1 FROM dbo.Tipologias t JOIN #snap s ON s.Id=t.Id
           WHERE ISNULL(t.ConfiguracionJson,'') COLLATE Latin1_General_BIN2 <> ISNULL(s.ConfiguracionJson,'') COLLATE Latin1_General_BIN2)
    BEGIN PRINT 'FAIL: no idempotente'; THROW 50005,'idempotencia',1; END
PRINT 'OK: idempotencia';
GO

-- ===== Rollback (Task 3): restaurar desde el snapshot original y verificar byte a byte =====
-- El script real (scripts/database/rollback-assetresolver-by-schema.sql) restaura desde
-- la tabla de backup fisica Tipologias_AssetResolverBak_<stamp> creada por
-- enable-assetresolver-by-schema.sql (@BackupTable + OBJECT_ID + sp_executesql). Este
-- harness omitio a proposito esa tabla fisica en sus aplicaciones (ver la nota de la
-- seccion "Aplicacion (Task 2)": el timestamp con resolucion de segundo podria
-- colisionar entre las dos aplicaciones del mismo test). Para una verificacion
-- autocontenida se restaura aqui directamente desde #orig -- el snapshot de
-- ConfiguracionJson tomado ANTES de la primera aplicacion, la misma fuente de verdad
-- que contendria la tabla de backup real -- por Id, y se compara el resultado byte a
-- byte contra ese snapshot.
UPDATE t SET ConfiguracionJson = o.ConfiguracionJson
FROM dbo.Tipologias t
JOIN #orig o ON o.Id = t.Id;

DECLARE @fail INT = 0;
IF EXISTS (SELECT 1 FROM dbo.Tipologias t JOIN #orig o ON o.Id=t.Id
           WHERE ISNULL(t.ConfiguracionJson,'') COLLATE Latin1_General_BIN2 <> ISNULL(o.ConfiguracionJson,'') COLLATE Latin1_General_BIN2)
    BEGIN SET @fail=1; PRINT 'FAIL: rollback no restaura original'; END;

IF @fail=1 THROW 50006,'rollback',1;
PRINT 'OK: rollback';
GO
