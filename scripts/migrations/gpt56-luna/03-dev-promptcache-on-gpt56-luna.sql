-- =====================================================================
-- 03 - DEV: enciende la cache de prompt en classification.gpt56-luna
-- =====================================================================
-- Prerrequisitos del cutover a gpt-5.6-luna (AB#99934, task AB#101412).
-- Escribe el bloque promptCache en la ConfiguracionJson de la fila
-- classification.gpt56-luna (Tipo 0, Activo = 1):
--   {"enabled":true,"keyPrefix":"documentia-clasif","breakpoint":true,"ttl":"30m"}
-- Solo toca esa fila: no modifica las filas por defecto del entorno ni
-- prompt.gpt56-luna. Con el bloque ausente o enabled = false la app no envia
-- ningun campo de cache (comportamiento anterior).
--
-- Idempotente: si el bloque ya vale exactamente eso, PRINT y sin backup ni
-- cambios. Backup previo de la tabla (ModeloConfigs__bak_<ts>) solo si hay
-- cambio. El JSON resultante se valida con ISJSON antes de guardar. La app
-- refresca el registro de modelos en <= 5 min (cache de memoria).
-- Para apagarla: 04-dev-promptcache-off-gpt56-luna.sql.
--
-- Ejecutar SOLO en DEV:
--   pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/03-dev-promptcache-on-gpt56-luna.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Id INT, @json NVARCHAR(MAX);
SELECT @Id = Id, @json = ConfiguracionJson
FROM dbo.ModeloConfigs
WHERE Tipo = 0 AND [Key] = N'classification.gpt56-luna' AND Activo = 1;

IF @Id IS NULL
    THROW 50020, 'No existe la fila activa classification.gpt56-luna (Tipo 0): ejecutar antes 01-dev-insert-gpt56-luna-rows.sql.', 1;
IF ISJSON(@json) <> 1
    THROW 50021, 'La ConfiguracionJson de classification.gpt56-luna no es JSON valido.', 1;

DECLARE @cache NVARCHAR(400) = N'{"enabled":true,"keyPrefix":"documentia-clasif","breakpoint":true,"ttl":"30m"}';
IF ISJSON(@cache) <> 1
    THROW 50022, 'El bloque promptCache a escribir no es JSON valido.', 1;

IF JSON_VALUE(@json, '$.promptCache.enabled') = 'true'
   AND JSON_VALUE(@json, '$.promptCache.keyPrefix') = N'documentia-clasif'
   AND JSON_VALUE(@json, '$.promptCache.breakpoint') = 'true'
   AND JSON_VALUE(@json, '$.promptCache.ttl') = N'30m'
BEGIN
    PRINT 'promptCache ya esta encendido con los valores pedidos. Sin backup ni cambios.';
END
ELSE
BEGIN
    SET @json = JSON_MODIFY(@json, '$.promptCache', JSON_QUERY(@cache));
    IF ISJSON(@json) <> 1
        THROW 50023, 'La configuracion resultante no es JSON valido; no se guarda.', 1;

    DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
    DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
    EXEC sp_executesql @sql;
    PRINT CONCAT('Backup creado: dbo.', @bak);

    BEGIN TRAN;
    UPDATE dbo.ModeloConfigs
    SET ConfiguracionJson = @json, FechaActualizacion = SYSUTCDATETIME()
    WHERE Id = @Id;
    COMMIT TRAN;
    PRINT 'promptCache encendido en classification.gpt56-luna.';
END

-- Verificacion
SELECT Id, Tipo, [Key], Activo,
       JSON_QUERY(ConfiguracionJson, '$.promptCache') AS PromptCache,
       ISJSON(ConfiguracionJson)                      AS JsonValido
FROM dbo.ModeloConfigs
WHERE Tipo = 0 AND [Key] = N'classification.gpt56-luna';
