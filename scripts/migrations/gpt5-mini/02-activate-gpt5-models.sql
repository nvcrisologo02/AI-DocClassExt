-- =====================================================================
-- 02 - ACTIVACION gpt-5-mini: cutover in-place de las filas GPT
-- =====================================================================
-- Cambia DeploymentName 'gpt-4o-mini' -> 'gpt-5-mini' EN LAS FILAS
-- EXISTENTES (no se renombran claves: las tipologias las referencian por
-- ModelKey). En clasificacion sube MaxTokens 150 -> 2000 porque en la
-- familia gpt-5 los reasoning tokens cuentan contra el limite de salida.
--
-- PRERREQUISITOS (ver README.md):
--   1. Deployment gpt-5-mini creado (create-gpt5-deployments.sh)
--   2. Fix de codigo desplegado: Temperature condicional (gpt-5 rechaza
--      temperature != default). SIN ESTE FIX LA ACTIVACION ROMPE EL FLUJO.
--   3. A/B validado con las filas *-gpt5-mini-test (script 01)
--
-- Hace backup de la tabla antes de tocar nada. Rollback: script 03.
-- La app refresca el registro de modelos en <= 5 min (cache de memoria).
--
-- Ejecutar con:
--   sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i 02-activate-gpt5-models.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 1. Backup de la tabla destino (convencion del proyecto)
DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
EXEC sp_executesql @sql;
PRINT CONCAT('Backup creado: dbo.', @bak);

-- 2. Cutover transaccional
BEGIN TRAN;

DECLARE @objetivo TABLE ([Key] NVARCHAR(200), NuevoMaxTokens INT NULL);
INSERT INTO @objetivo VALUES
    ('classification.gpt4o-mini-fallback', 2000),  -- 150 seria insuficiente con reasoning tokens
    ('default.gpt4o-mini_ex',              NULL),
    ('extraction.gpt4o-mini-fallback',     NULL),
    ('default.gpt4o-mini',                 NULL);

UPDATE m
SET ConfiguracionJson =
        CASE WHEN o.NuevoMaxTokens IS NULL
             THEN JSON_MODIFY(m.ConfiguracionJson, '$.DeploymentName', 'gpt-5-mini')
             ELSE JSON_MODIFY(JSON_MODIFY(m.ConfiguracionJson, '$.DeploymentName', 'gpt-5-mini'),
                              '$.MaxTokens', o.NuevoMaxTokens)
        END,
    FechaActualizacion = SYSUTCDATETIME()
FROM dbo.ModeloConfigs m
JOIN @objetivo o ON o.[Key] = m.[Key]
WHERE m.Activo = 1
  AND JSON_VALUE(m.ConfiguracionJson, '$.DeploymentName') = 'gpt-4o-mini';

DECLARE @afectadas INT = @@ROWCOUNT;
IF @afectadas <> 4
BEGIN
    ROLLBACK;
    RAISERROR('Se esperaban 4 filas y se actualizaron %d. ROLLBACK aplicado; revisar estado de ModeloConfigs.', 16, 1, @afectadas);
    RETURN;
END

COMMIT;
PRINT 'Cutover aplicado a 4 filas (clasificacion fallback, extraccion default+fallback, prompt).';

-- 3. Verificacion
SELECT Id, Tipo, [Key], Activo,
       JSON_VALUE(ConfiguracionJson, '$.DeploymentName') AS DeploymentName,
       JSON_VALUE(ConfiguracionJson, '$.MaxTokens')      AS MaxTokens,
       JSON_VALUE(ConfiguracionJson, '$.Endpoint')       AS Endpoint,
       FechaActualizacion
FROM dbo.ModeloConfigs
WHERE [Key] IN ('classification.gpt4o-mini-fallback', 'default.gpt4o-mini_ex',
                'extraction.gpt4o-mini-fallback', 'default.gpt4o-mini');
