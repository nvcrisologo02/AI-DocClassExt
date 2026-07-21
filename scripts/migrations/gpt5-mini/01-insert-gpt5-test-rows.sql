-- =====================================================================
-- 01 - Filas de PRUEBA gpt-5-mini en ModeloConfigs (no afectan al flujo)
-- =====================================================================
-- Clona las filas GPT actuales (deployment gpt-4o-mini) como nuevas filas
-- *-gpt5-mini-test apuntando al deployment gpt-5-mini, SIN marcar
-- IsDefault/UseAsFallback: solo resolubles pidiendo el modelKey explicito
-- (A/B testing). Reutiliza Endpoint/ApiKey de la fila origen, no expone claves.
--
-- Ejecutar con:
--   sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i 01-insert-gpt5-test-rows.sql
-- Idempotente: no inserta si la clave ya existe.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @nuevas TABLE (KeyOrigen NVARCHAR(200), KeyNueva NVARCHAR(200));
INSERT INTO @nuevas VALUES
    ('classification.gpt4o-mini-fallback', 'classification.gpt5-mini-test'),
    ('default.gpt4o-mini_ex',              'extraction.gpt5-mini-test'),
    ('default.gpt4o-mini',                 'prompt.gpt5-mini-test');

INSERT INTO dbo.ModeloConfigs (Tipo, [Key], Provider, Activo, ConfiguracionJson, FechaCreacion, FechaActualizacion, CreadoPor)
SELECT m.Tipo,
       n.KeyNueva,
       m.Provider,
       1,
       JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(
           m.ConfiguracionJson,
           '$.Key', n.KeyNueva),
           '$.DeploymentName', 'gpt-5-mini'),
           '$.IsDefault', CAST(0 AS BIT)),
           '$.UseAsFallback', CAST(0 AS BIT)),
       SYSUTCDATETIME(),
       SYSUTCDATETIME(),
       'migracion-gpt5'
FROM dbo.ModeloConfigs m
JOIN @nuevas n ON n.KeyOrigen = m.[Key]
WHERE m.Activo = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.ModeloConfigs x WHERE x.[Key] = n.KeyNueva);

PRINT CONCAT('Filas de prueba insertadas: ', @@ROWCOUNT);

COMMIT;

SELECT Id, Tipo, [Key], Activo,
       JSON_VALUE(ConfiguracionJson, '$.DeploymentName') AS DeploymentName,
       JSON_VALUE(ConfiguracionJson, '$.UseAsFallback')  AS UseAsFallback
FROM dbo.ModeloConfigs
WHERE [Key] LIKE '%gpt5-mini-test%';
