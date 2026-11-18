-- =====================================================================
-- 04 - DEV: apaga la cache de prompt en classification.gpt56-luna
-- =====================================================================
-- Prerrequisitos del cutover a gpt-5.6-luna (AB#99934, task AB#101412).
-- Pone promptCache.enabled = false en la fila classification.gpt56-luna
-- (Tipo 0, Activo = 1), conservando el resto del bloque (keyPrefix,
-- breakpoint, ttl) para poder volver a encenderla con el script 03.
-- Solo toca esa fila: no modifica las filas por defecto del entorno ni
-- prompt.gpt56-luna.
--
-- Idempotente: si el bloque no existe o ya tiene enabled = false, PRINT y
-- sin backup ni cambios (un bloque ausente equivale a cache apagada). Backup
-- previo de la tabla (ModeloConfigs__bak_<ts>) solo si hay cambio. El JSON
-- resultante se valida con ISJSON antes de guardar. La app refresca el
-- registro de modelos en <= 5 min (cache de memoria).
--
-- Ejecutar SOLO en DEV:
--   pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/04-dev-promptcache-off-gpt56-luna.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Id INT, @json NVARCHAR(MAX);
SELECT @Id = Id, @json = ConfiguracionJson
FROM dbo.ModeloConfigs
WHERE Tipo = 0 AND [Key] = N'classification.gpt56-luna' AND Activo = 1;

IF @Id IS NULL
    THROW 50030, 'No existe la fila activa classification.gpt56-luna (Tipo 0): ejecutar antes 01-dev-insert-gpt56-luna-rows.sql.', 1;
IF ISJSON(@json) <> 1
    THROW 50031, 'La ConfiguracionJson de classification.gpt56-luna no es JSON valido.', 1;

IF JSON_QUERY(@json, '$.promptCache') IS NULL
   OR JSON_VALUE(@json, '$.promptCache.enabled') = 'false'
BEGIN
    PRINT 'promptCache ya esta apagado (ausente o enabled = false). Sin backup ni cambios.';
END
ELSE
BEGIN
    SET @json = JSON_MODIFY(@json, '$.promptCache.enabled', CAST(0 AS BIT));
    IF ISJSON(@json) <> 1
        THROW 50032, 'La configuracion resultante no es JSON valido; no se guarda.', 1;

    DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
    DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
    EXEC sp_executesql @sql;
    PRINT CONCAT('Backup creado: dbo.', @bak);

    BEGIN TRAN;
    UPDATE dbo.ModeloConfigs
    SET ConfiguracionJson = @json, FechaActualizacion = SYSUTCDATETIME()
    WHERE Id = @Id;
    COMMIT TRAN;
    PRINT 'promptCache apagado en classification.gpt56-luna.';
END

-- Verificacion
SELECT Id, Tipo, [Key], Activo,
       JSON_QUERY(ConfiguracionJson, '$.promptCache') AS PromptCache,
       ISJSON(ConfiguracionJson)                      AS JsonValido
FROM dbo.ModeloConfigs
WHERE Tipo = 0 AND [Key] = N'classification.gpt56-luna';
