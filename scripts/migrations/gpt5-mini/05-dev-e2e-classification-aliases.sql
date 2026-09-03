-- =====================================================================
-- 05 - DEV: alta de alias de clasificacion 'gpt-4o-mini' y 'gpt-5-mini'
-- =====================================================================
-- Los casos E2E post-despliegue FC-FC4/FC-FC5 piden el modelo de
-- clasificacion explicito por modelKey 'gpt-4o-mini' / 'gpt-5-mini'
-- (AB#100131). El registro resuelve por igualdad exacta de Key contra
-- ModeloConfigs (Tipo=0), asi que se dan de alta dos filas alias que
-- clonan la configuracion de las filas reales del script 04:
--   - 'gpt-4o-mini'  <- classification.gpt4o-mini-fallback (sin flags)
--   - 'gpt-5-mini'   <- classification.gpt5-mini           (sin flags)
-- Sin IsDefault ni UseAsFallback: no entran en la resolucion por defecto
-- ni rompen la unicidad del fallback; solo se resuelven por modelKey.
--
-- Idempotente: no inserta si la Key ya existe.
-- La app refresca el registro de modelos en <= 5 min (cache de memoria).
--
-- Ejecutar con:
--   sqlcmd -S srbsqldevdocai.database.windows.net -d DocumentIA -G -i 05-dev-e2e-classification-aliases.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @aliases TABLE (Alias NVARCHAR(200), Origen NVARCHAR(200));
INSERT INTO @aliases VALUES
    ('gpt-4o-mini', 'classification.gpt4o-mini-fallback'),
    ('gpt-5-mini',  'classification.gpt5-mini');

INSERT INTO dbo.ModeloConfigs (Tipo, [Key], Provider, Activo, ConfiguracionJson, FechaCreacion, CreadoPor)
SELECT m.Tipo,
       a.Alias,
       m.Provider,
       1,
       JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(m.ConfiguracionJson,
           '$.Key', a.Alias),
           '$.IsDefault', CAST(0 AS BIT)),
           '$.UseAsFallback', CAST(0 AS BIT)),
       SYSUTCDATETIME(),
       'script-05-e2e-aliases'
FROM @aliases a
JOIN dbo.ModeloConfigs m
  ON m.[Key] = a.Origen AND m.Tipo = 0 AND m.Activo = 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ModeloConfigs x WHERE x.[Key] = a.Alias AND x.Tipo = 0
);

PRINT CONCAT('Alias insertados: ', @@ROWCOUNT);
COMMIT;

-- Verificacion
SELECT Id, Tipo, [Key], Activo,
       JSON_VALUE(ConfiguracionJson, '$.DeploymentName') AS DeploymentName,
       JSON_VALUE(ConfiguracionJson, '$.MaxTokens')      AS MaxTokens,
       JSON_VALUE(ConfiguracionJson, '$.IsDefault')      AS IsDefault,
       JSON_VALUE(ConfiguracionJson, '$.UseAsFallback')  AS UseAsFallback
FROM dbo.ModeloConfigs
WHERE Tipo = 0
ORDER BY Id;
