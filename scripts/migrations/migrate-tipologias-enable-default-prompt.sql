/*
M2 - Activacion idempotente de promptConfig en Tipologias.ConfiguracionJson.

Objetivo:
- Activar promptConfig.enabled=true en tipologias sin prompt activo.
- Mantener prompts vacios: no dispara ResultadoPrompt propio hasta que exista definicion real.
- El resumen por defecto lo controla PromptDefaults y se expone como Resumen cuando el backend llega a GPT
    o cuando la peticion informa forzarResumenPorDefecto=true.
- No modificar tipologias que ya tenian prompt activo.

Notas:
- Requiere backend con soporte de PromptDefaults desplegado.
- Script seguro para re-ejecucion.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Tipologias', N'U') IS NULL
BEGIN
    THROW 50001, 'No existe la tabla dbo.Tipologias.', 1;
END;

PRINT 'Prevalidacion: tipologias con JSON invalido';
SELECT
    t.Id,
    t.Codigo
FROM dbo.Tipologias AS t
WHERE t.ConfiguracionJson IS NULL
   OR ISJSON(t.ConfiguracionJson) <> 1;

PRINT 'Prevalidacion: tipologias candidatas a activar prompt';
SELECT
    t.Id,
    t.Codigo,
    PromptEnabled = JSON_VALUE(t.ConfiguracionJson, '$.promptConfig.enabled'),
    PromptModelKey = JSON_VALUE(t.ConfiguracionJson, '$.promptConfig.modelKey')
FROM dbo.Tipologias AS t
WHERE ISJSON(t.ConfiguracionJson) = 1
  AND ISNULL(JSON_VALUE(t.ConfiguracionJson, '$.promptConfig.enabled'), 'false') NOT IN ('true', '1')
ORDER BY t.Codigo;

BEGIN TRANSACTION;

;WITH Candidates AS
(
    SELECT
        t.Id,
        ConfiguracionJsonBase = CASE
            WHEN JSON_QUERY(t.ConfiguracionJson, '$.promptConfig') IS NULL THEN
                JSON_MODIFY(t.ConfiguracionJson, '$.promptConfig', JSON_QUERY(N'{}'))
            ELSE t.ConfiguracionJson
        END,
        CurrentModelKey = NULLIF(JSON_VALUE(t.ConfiguracionJson, '$.promptConfig.modelKey'), '')
    FROM dbo.Tipologias AS t
    WHERE ISJSON(t.ConfiguracionJson) = 1
      AND ISNULL(JSON_VALUE(t.ConfiguracionJson, '$.promptConfig.enabled'), 'false') NOT IN ('true', '1')
)
UPDATE t
SET ConfiguracionJson = JSON_MODIFY(
    JSON_MODIFY(
    JSON_MODIFY(
    JSON_MODIFY(
    JSON_MODIFY(
    JSON_MODIFY(
    JSON_MODIFY(c.ConfiguracionJsonBase,
        '$.promptConfig.enabled', CAST(1 AS bit)),
        '$.promptConfig.modelKey', COALESCE(c.CurrentModelKey, N'default.gpt4o-mini')),
        '$.promptConfig.systemPrompt', N''),
        '$.promptConfig.userPromptTemplate', N''),
        '$.promptConfig.maxTokens', 1600),
        '$.promptConfig.temperature', 0.0),
        '$.promptConfig.contentMode', N'markdown')
FROM dbo.Tipologias AS t
INNER JOIN Candidates AS c ON c.Id = t.Id;

DECLARE @updated int = @@ROWCOUNT;

COMMIT TRANSACTION;

PRINT CONCAT('Tipologias actualizadas: ', @updated);

PRINT 'Postvalidacion: estado de prompt por tipologia';
SELECT
    t.Id,
    t.Codigo,
    PromptEnabled = JSON_VALUE(t.ConfiguracionJson, '$.promptConfig.enabled'),
    PromptModelKey = JSON_VALUE(t.ConfiguracionJson, '$.promptConfig.modelKey'),
    ContentMode = JSON_VALUE(t.ConfiguracionJson, '$.promptConfig.contentMode'),
    SystemPromptLength = LEN(COALESCE(JSON_VALUE(t.ConfiguracionJson, '$.promptConfig.systemPrompt'), N'')),
    UserPromptLength = LEN(COALESCE(JSON_VALUE(t.ConfiguracionJson, '$.promptConfig.userPromptTemplate'), N''))
FROM dbo.Tipologias AS t
WHERE ISJSON(t.ConfiguracionJson) = 1
ORDER BY t.Codigo;
