/*  Activa AssetResolver en tipologias Publicadas/Activas cuyo esquema declare
    referencia catastral o IDUFIR (plano o en colecciones de objetos).

    ROBUSTO A CASING: en prod conviven ConfiguracionJson en camelCase ('fields',
    'assetResolver', 'name'...) y en PascalCase ('Fields', 'AssetResolver', 'Name'...).
    Como los paths de OPENJSON/JSON_VALUE/JSON_QUERY son SENSIBLES A MAYUSCULAS, el
    descubrimiento lee cada clave con COALESCE(camel, Pascal), y la ESCRITURA se hace en
    DOS ramas segun el casing de la fila: escribir en el casing contrario crearia una
    clave duplicada (p.ej. 'assetResolver' junto a 'AssetResolver') que rompe la lectura
    case-insensitive de System.Text.Json en el backend.

    Uso:
      sqlcmd -S <srv> -d <DocumentIA> -G -v WhatIf=1 -i enable-assetresolver-by-schema.sql
      @WhatIf=1 (default) hace ROLLBACK tras mostrar el dry-run; @WhatIf=0 hace COMMIT.

    Nota: si se invoca sin "-v WhatIf=...", NO usar el flag "-b" de sqlcmd, porque una
    variable de scripting no definida solo genera un aviso ("scripting variable not
    defined") y continua sustituyendo el texto literal "$(WhatIf)"; con "-b" sqlcmd
    aborta en ese aviso. El TRY_CAST de abajo asume ese comportamiento por defecto
    (sin -b) para resolver el default a 1 cuando no se pasa la variable.

    Semantica: SOBRESCRIBE (normaliza) mapeoReferenciaCatastral / mapeoIdufir /
    mapeoColeccionActivos con lo que dicta el esquema real; preserva el resto de claves
    del bloque assetResolver preexistente. Descubrimiento en dos niveles: campos planos
    ($.fields[*]) y sub-propiedades de colecciones ($.fields[*].items.properties[*] con
    padre type='array'); una sub-propiedad refcat/idufir mapea su array padre a
    mapeoColeccionActivos. STRING_AGG ... WITHIN GROUP (ORDER BY ...) da orden
    deterministico (idempotencia). */
SET NOCOUNT ON;
DECLARE @WhatIf BIT = TRY_CAST('$(WhatIf)' AS BIT);
IF @WhatIf IS NULL SET @WhatIf = 1;

DECLARE @stamp NVARCHAR(20) = FORMAT(SYSUTCDATETIME(),'yyyyMMdd_HHmmss');
DECLARE @bak SYSNAME = N'Tipologias_AssetResolverBak_' + @stamp;

BEGIN TRAN;

-- 1) Descubrimiento robusto a casing -> #obj (Id, arrays, IsPascal)
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

-- 2) Backup de filas afectadas (solo si hay) — casing-agnostico (copia el JSON entero)
IF EXISTS (SELECT 1 FROM #obj)
BEGIN
    DECLARE @mk NVARCHAR(MAX) =
      N'SELECT t.Id, t.Codigo, t.Version, t.ConfiguracionJson, SYSUTCDATETIME() AS FechaBackup '
    + N'INTO dbo.' + QUOTENAME(@bak) + N' FROM dbo.Tipologias t JOIN #obj o ON o.Id=t.Id;';
    EXEC sys.sp_executesql @mk;
    PRINT 'Backup preparado (persiste solo con @WhatIf=0): dbo.' + @bak;
END

-- 3) Escritura rama camelCase (IsPascal=0): setea claves camelCase; arrays NULL -> se
--    borra la clave (JSON_MODIFY con JSON_QUERY(NULL) en modo lax).
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

-- 4) Escritura rama PascalCase (IsPascal=1): mismas claves en PascalCase para overwrite
--    en sitio del bloque AssetResolver existente (no crear duplicado camelCase).
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

-- 5) Dry-run: mostrar el bloque resultante (leido en cualquier casing)
SELECT t.Id, t.Codigo, t.Version, o.IsPascal,
       COALESCE(JSON_QUERY(t.ConfiguracionJson,'$.assetResolver'),
                JSON_QUERY(t.ConfiguracionJson,'$.AssetResolver')) AS assetResolver
FROM dbo.Tipologias t JOIN #obj o ON o.Id=t.Id
ORDER BY t.Codigo;

IF @WhatIf = 1 BEGIN PRINT 'WhatIf=1: ROLLBACK'; ROLLBACK TRAN; END
ELSE BEGIN PRINT 'WhatIf=0: COMMIT'; COMMIT TRAN; END
