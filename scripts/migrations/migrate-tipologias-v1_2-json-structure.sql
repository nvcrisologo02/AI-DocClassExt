/*
M1 - Migración idempotente de Tipologias.ConfiguracionJson a estructura v1.2.

Objetivo:
- Añadir bloques jerárquicos $.gdc y $.classification cuando no existan.
- Mantener compatibilidad backward: NO elimina ni modifica los campos legacy de raíz.

Notas:
- Ejecutar cuando backend/frontend v1.2 estén desplegados en develop.
- Script seguro para re-ejecución (idempotente).
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Tipologias', N'U') IS NULL
BEGIN
    THROW 50001, 'No existe la tabla dbo.Tipologias.', 1;
END;

BEGIN TRANSACTION;

;WITH SourceRows AS
(
    SELECT
        t.Id,
        t.Codigo,
        t.ConfiguracionJson,
        GdcExists = CASE WHEN JSON_QUERY(t.ConfiguracionJson, '$.gdc') IS NULL THEN 0 ELSE 1 END,
        ClassificationExists = CASE WHEN JSON_QUERY(t.ConfiguracionJson, '$.classification') IS NULL THEN 0 ELSE 1 END,

        SkipUploadBit = CASE
            WHEN JSON_VALUE(t.ConfiguracionJson, '$.gdc.skipUpload') IN ('true', '1') THEN CAST(1 AS bit)
            WHEN JSON_VALUE(t.ConfiguracionJson, '$.gdc.skipUpload') IN ('false', '0') THEN CAST(0 AS bit)
            WHEN JSON_VALUE(t.ConfiguracionJson, '$.skipGDCUpload') IN ('true', '1') THEN CAST(1 AS bit)
            WHEN JSON_VALUE(t.ConfiguracionJson, '$.skipGDCUpload') IN ('false', '0') THEN CAST(0 AS bit)
            ELSE CAST(0 AS bit)
        END,

        Matricula = COALESCE(
            JSON_VALUE(t.ConfiguracionJson, '$.gdc.matricula'),
            JSON_VALUE(t.ConfiguracionJson, '$.tipologiaMGDCMatricula'),
            N''),

        TipoDocumento = COALESCE(
            JSON_VALUE(t.ConfiguracionJson, '$.gdc.tipoDocumento'),
            JSON_VALUE(t.ConfiguracionJson, '$.gdcTipoDocumento'),
            N''),

        SubtipoDocumento = COALESCE(
            JSON_VALUE(t.ConfiguracionJson, '$.gdc.subtipoDocumento'),
            JSON_VALUE(t.ConfiguracionJson, '$.gdcSubtipoDocumento'),
            N''),

        Serie = COALESCE(
            JSON_VALUE(t.ConfiguracionJson, '$.gdc.serie'),
            JSON_VALUE(t.ConfiguracionJson, '$.gdcSerie'),
            N''),

        Tdn1 = COALESCE(
            JSON_VALUE(t.ConfiguracionJson, '$.classification.tdn1'),
            JSON_VALUE(t.ConfiguracionJson, '$.tdn1'),
            N''),

        Tdn2 = COALESCE(
            JSON_VALUE(t.ConfiguracionJson, '$.classification.tdn2'),
            JSON_VALUE(t.ConfiguracionJson, '$.tdn2'),
            N''),

        GptDescripcion = COALESCE(
            JSON_VALUE(t.ConfiguracionJson, '$.classification.gptDescripcion'),
            JSON_VALUE(t.ConfiguracionJson, '$.gptDescripcion'),
            N''),

        EnableRulesBit = CASE
            WHEN JSON_VALUE(t.ConfiguracionJson, '$.classification.enableRules') IN ('false', '0') THEN CAST(0 AS bit)
            ELSE CAST(1 AS bit)
        END
    FROM dbo.Tipologias AS t
    WHERE t.ConfiguracionJson IS NOT NULL
      AND ISJSON(t.ConfiguracionJson) = 1
),
Prepared AS
(
    SELECT
        s.Id,
        s.Codigo,
        s.ConfiguracionJson,
        s.GdcExists,
        s.ClassificationExists,
        GdcObject = (
            SELECT
                s.SkipUploadBit AS [skipUpload],
                s.Matricula AS [matricula],
                s.TipoDocumento AS [tipoDocumento],
                s.SubtipoDocumento AS [subtipoDocumento],
                s.Serie AS [serie]
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        ),
        ClassificationObject = (
            SELECT
                s.Tdn1 AS [tdn1],
                s.Tdn2 AS [tdn2],
                s.GptDescripcion AS [gptDescripcion],
                s.EnableRulesBit AS [enableRules]
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        )
    FROM SourceRows AS s
),
Calculated AS
(
    SELECT
        p.Id,
        p.Codigo,
        NewJson =
            CASE
                WHEN p.GdcExists = 0 AND p.ClassificationExists = 0 THEN
                    JSON_MODIFY(
                        JSON_MODIFY(p.ConfiguracionJson, '$.gdc', JSON_QUERY(p.GdcObject)),
                        '$.classification', JSON_QUERY(p.ClassificationObject)
                    )
                WHEN p.GdcExists = 0 AND p.ClassificationExists = 1 THEN
                    JSON_MODIFY(p.ConfiguracionJson, '$.gdc', JSON_QUERY(p.GdcObject))
                WHEN p.GdcExists = 1 AND p.ClassificationExists = 0 THEN
                    JSON_MODIFY(p.ConfiguracionJson, '$.classification', JSON_QUERY(p.ClassificationObject))
                ELSE p.ConfiguracionJson
            END
    FROM Prepared AS p
)
UPDATE t
SET t.ConfiguracionJson = c.NewJson
FROM dbo.Tipologias AS t
INNER JOIN Calculated AS c ON c.Id = t.Id
WHERE c.NewJson <> t.ConfiguracionJson;

DECLARE @UpdatedRows int = @@ROWCOUNT;

COMMIT TRANSACTION;

PRINT 'Migración v1.2 completada.';
PRINT CONCAT('Filas actualizadas: ', @UpdatedRows);

SELECT
    t.Id,
    t.Codigo,
    HasGdc = CASE WHEN JSON_QUERY(t.ConfiguracionJson, '$.gdc') IS NULL THEN 0 ELSE 1 END,
    HasClassification = CASE WHEN JSON_QUERY(t.ConfiguracionJson, '$.classification') IS NULL THEN 0 ELSE 1 END
FROM dbo.Tipologias AS t
WHERE t.ConfiguracionJson IS NOT NULL
  AND ISJSON(t.ConfiguracionJson) = 1
ORDER BY t.Id;
