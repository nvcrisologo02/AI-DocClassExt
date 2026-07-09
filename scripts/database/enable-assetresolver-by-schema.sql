/*  Activa AssetResolver en tipologias Publicadas/Activas cuyo esquema declare
    referencia catastral o IDUFIR (plano o en colecciones de objetos).

    Uso:
      sqlcmd -S <srv> -d <DocumentIA> -E -v WhatIf=1 -i enable-assetresolver-by-schema.sql
      @WhatIf=1 (default) hace ROLLBACK tras mostrar el dry-run; @WhatIf=0 hace COMMIT.

    Nota: si se invoca sin "-v WhatIf=...", NO usar el flag "-b" de sqlcmd, porque una
    variable de scripting no definida solo genera un aviso ("scripting variable not
    defined") y continua sustituyendo el texto literal "$(WhatIf)"; con "-b" sqlcmd
    aborta en ese aviso. El TRY_CAST de abajo asume ese comportamiento por defecto
    (sin -b) para resolver el default a 1 cuando no se pasa la variable.

    Descubrimiento: reutiliza tal cual el modulo de descubrimiento corregido en
    scripts/database/tests/test-assetresolver-by-schema.sql (pre-filtro ISJSON en CTE
    propia antes de cualquier OPENJSON, ISJSON(rules)=1 antes de cada EXISTS sobre
    OPENJSON(rules), y STRING_AGG ... WITHIN GROUP (ORDER BY ...) para orden
    deterministico, necesario para la idempotencia). */
SET NOCOUNT ON;
DECLARE @WhatIf BIT = TRY_CAST('$(WhatIf)' AS BIT);
IF @WhatIf IS NULL SET @WhatIf = 1;

DECLARE @stamp NVARCHAR(20) = FORMAT(SYSUTCDATETIME(),'yyyyMMdd_HHmmss');
DECLARE @bak SYSNAME = N'Tipologias_AssetResolverBak_' + @stamp;

BEGIN TRAN;

-- 1) Descubrimiento -> #obj (mismo modulo corregido que el harness de test)
;WITH TipologiasJsonValido AS (
    SELECT Id, ConfiguracionJson
    FROM dbo.Tipologias
    WHERE ISJSON(ConfiguracionJson) = 1
),
FieldsFlat AS (
    SELECT t.Id, j.[name], j.[type], j.rules, j.itemsProps
    FROM TipologiasJsonValido t
    CROSS APPLY OPENJSON(t.ConfiguracionJson, '$.fields')
        WITH ([name] NVARCHAR(200) '$.name',
              [type] NVARCHAR(50)  '$.type',
              rules  NVARCHAR(MAX) '$.rules' AS JSON,
              itemsProps NVARCHAR(MAX) '$.items.properties' AS JSON) j
),
FieldsNested AS (
    SELECT f.Id, f.[name] AS ColeccionName, p.[name] AS PropName, p.rules
    FROM FieldsFlat f
    CROSS APPLY OPENJSON(f.itemsProps)
        WITH ([name] NVARCHAR(200) '$.name', rules NVARCHAR(MAX) '$.rules' AS JSON) p
    WHERE f.[type] = 'array' AND f.itemsProps IS NOT NULL
),
Qualified AS (
    -- planos refcat
    SELECT Id, 'REF' AS Kind, [name] AS FieldName, CAST(NULL AS NVARCHAR(200)) AS ColeccionName
    FROM FieldsFlat f
    WHERE LOWER([name]) IN ('referenciacatastral','refcatastral','catastral')
       OR (ISJSON(f.rules) = 1 AND EXISTS (SELECT 1 FROM OPENJSON(f.rules) WITH (ruleType NVARCHAR(100) '$.ruleType') r WHERE LOWER(r.ruleType)='catastral'))
    UNION ALL
    -- planos idufir
    SELECT Id, 'IDU', [name], NULL
    FROM FieldsFlat
    WHERE LOWER([name]) IN ('idufir_cru','idufir','cru','codigoregistrounico')
    UNION ALL
    -- anidados refcat
    SELECT Id, 'REF', PropName, ColeccionName
    FROM FieldsNested n
    WHERE LOWER(PropName) IN ('referenciacatastral','refcatastral','catastral')
       OR (ISJSON(n.rules) = 1 AND EXISTS (SELECT 1 FROM OPENJSON(n.rules) WITH (ruleType NVARCHAR(100) '$.ruleType') r WHERE LOWER(r.ruleType)='catastral'))
    UNION ALL
    -- anidados idufir
    SELECT Id, 'IDU', PropName, ColeccionName
    FROM FieldsNested
    WHERE LOWER(PropName) IN ('idufir_cru','idufir','cru','codigoregistrounico')
)
SELECT q.Id,
    (SELECT '[' + STRING_AGG('"' + STRING_ESCAPE(FieldName,'json') + '"', ',') WITHIN GROUP (ORDER BY FieldName) + ']'
     FROM (SELECT DISTINCT FieldName FROM Qualified x WHERE x.Id=q.Id AND x.Kind='REF') a) AS RefcatArr,
    (SELECT '[' + STRING_AGG('"' + STRING_ESCAPE(FieldName,'json') + '"', ',') WITHIN GROUP (ORDER BY FieldName) + ']'
     FROM (SELECT DISTINCT FieldName FROM Qualified x WHERE x.Id=q.Id AND x.Kind='IDU') b) AS IdufirArr,
    (SELECT '[' + STRING_AGG('"' + STRING_ESCAPE(ColeccionName,'json') + '"', ',') WITHIN GROUP (ORDER BY ColeccionName) + ']'
     FROM (SELECT DISTINCT ColeccionName FROM Qualified x WHERE x.Id=q.Id AND x.ColeccionName IS NOT NULL) c) AS ColeccionArr
INTO #obj
FROM (SELECT DISTINCT Id FROM Qualified) q
JOIN dbo.Tipologias t ON t.Id = q.Id AND t.Estado = 1 AND t.Activa = 1;

-- 2) Backup de filas afectadas (solo si hay)
IF EXISTS (SELECT 1 FROM #obj)
BEGIN
    DECLARE @mk NVARCHAR(MAX) =
      N'SELECT t.Id, t.Codigo, t.Version, t.ConfiguracionJson, SYSUTCDATETIME() AS FechaBackup '
    + N'INTO dbo.' + QUOTENAME(@bak) + N' FROM dbo.Tipologias t JOIN #obj o ON o.Id=t.Id;';
    EXEC sys.sp_executesql @mk;
    PRINT 'Backup creado: dbo.' + @bak;
END

-- 3) Construir nuevo JSON preservando assetResolver preexistente
;WITH NuevoJson AS (
    SELECT t.Id,
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
          '$.assetResolver.mapeoIdufir', JSON_QUERY(o.IdufirArr)) AS Base,
        o.ColeccionArr
    FROM dbo.Tipologias t JOIN #obj o ON o.Id=t.Id
)
UPDATE t SET ConfiguracionJson =
    JSON_MODIFY(n.Base,'$.assetResolver.mapeoColeccionActivos', JSON_QUERY(n.ColeccionArr))
FROM dbo.Tipologias t JOIN NuevoJson n ON n.Id=t.Id;

-- 4) Dry-run: mostrar resultado
SELECT t.Id, t.Codigo, t.Version, JSON_QUERY(t.ConfiguracionJson,'$.assetResolver') AS assetResolver
FROM dbo.Tipologias t JOIN #obj o ON o.Id=t.Id
ORDER BY t.Codigo;

IF @WhatIf = 1 BEGIN PRINT 'WhatIf=1: ROLLBACK'; ROLLBACK TRAN; END
ELSE BEGIN PRINT 'WhatIf=0: COMMIT'; COMMIT TRAN; END
