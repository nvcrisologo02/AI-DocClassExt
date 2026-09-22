-- Cutover de un entorno a sus recursos de IA propios (Tarea 15 del plan
-- "IA propia por entorno", AB#100317): asigna ResourceAlias a cada fila activa
-- de ModeloConfigs y elimina el Endpoint explicito, de modo que el registro
-- resuelva el endpoint desde el App Setting AI__Resources__<alias>__Endpoint
-- del entorno (AiEndpointResolver: el Endpoint explicito gana; si no hay, alias).
--
-- Mapeo (mismos alias que infra/ai/resources.<env>.json y el pipeline):
--   azure-openai                                   -> openai_primary
--   azure-content-understanding, [Key] LIKE '%-we' -> cu_secondary
--   azure-content-understanding (resto)            -> cu_primary
--   azure-document-intelligence(-layout)           -> di
--
-- Idempotente: solo toca filas activas (Tipo <> 4, catalogo de tarifas fuera)
-- que aun tengan Endpoint o no tengan el alias esperado; una segunda pasada no
-- cambia nada. Hace copia previa en ModeloConfigs__bak_<yyyyMMdd_HHmmss>; para
-- volver atras: UPDATE m SET m.ConfiguracionJson = b.ConfiguracionJson FROM
-- ModeloConfigs m JOIN ModeloConfigs__bak_<...> b ON b.Id = m.Id.
--
-- Orden del cutover (ver infra/ai/README.md, "Cutover de un entorno"):
--   1. keys del entorno en su Key Vault (modo ApiKey de los App Settings),
--   2. desplegar el pipeline con las variables AI_* del entorno,
--   3. este script contra la BD del entorno,
--   4. reiniciar la Function App (cache de registros).
-- Requisitos previos a comprobar antes de ejecutarlo:
--   - los 4 App Settings AI__Resources__*__Endpoint existen en la Function App
--     con los valores del entorno (si faltan, el registro lanza
--     InvalidOperationException al cargar);
--   - los seeds config/*/models.json no llevan Endpoint, o
--     ConfigurationSeedService.MergeMissingJsonProperties lo reinyectara desde
--     el seed (el de PRO) en el siguiente arranque al encontrarlo vacio.
-- Ejecutar con lotes GO, p. ej. docs/auxiliares/temps/2026-09-07/aplicar-sql-dev.ps1.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @bak sysname = 'ModeloConfigs__bak_' + FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss');
DECLARE @pendientes int;

SELECT @pendientes = COUNT(*)
FROM ModeloConfigs
WHERE Activo = 1 AND Tipo <> 4
  AND Provider IN ('azure-openai', 'azure-content-understanding', 'azure-document-intelligence', 'azure-document-intelligence-layout')
  AND (
        JSON_VALUE(ConfiguracionJson, '$.Endpoint') IS NOT NULL
     OR JSON_VALUE(ConfiguracionJson, '$.endpoint') IS NOT NULL
     OR ISNULL(JSON_VALUE(ConfiguracionJson, '$.ResourceAlias'), '') <>
        CASE
            WHEN Provider = 'azure-openai' THEN 'openai_primary'
            WHEN Provider = 'azure-content-understanding' AND [Key] LIKE '%-we' THEN 'cu_secondary'
            WHEN Provider = 'azure-content-understanding' THEN 'cu_primary'
            ELSE 'di'
        END
  );

IF @pendientes = 0
BEGIN
    PRINT 'set-resource-aliases: nada que hacer (todas las filas activas ya usan alias sin Endpoint explicito).';
END
ELSE
BEGIN
    PRINT 'set-resource-aliases: ' + CAST(@pendientes AS varchar(10)) + ' fila(s) a actualizar; copia previa en ' + @bak;
    EXEC('SELECT * INTO ' + @bak + ' FROM ModeloConfigs');

    BEGIN TRANSACTION;

    UPDATE ModeloConfigs
    SET ConfiguracionJson = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(ConfiguracionJson,
            '$.ResourceAlias',
            CASE
                WHEN Provider = 'azure-openai' THEN 'openai_primary'
                WHEN Provider = 'azure-content-understanding' AND [Key] LIKE '%-we' THEN 'cu_secondary'
                WHEN Provider = 'azure-content-understanding' THEN 'cu_primary'
                ELSE 'di'
            END),
            '$.resourceAlias', NULL),
            '$.Endpoint', NULL),
            '$.endpoint', NULL),
        FechaActualizacion = SYSUTCDATETIME()
    WHERE Activo = 1 AND Tipo <> 4
      AND Provider IN ('azure-openai', 'azure-content-understanding', 'azure-document-intelligence', 'azure-document-intelligence-layout')
      AND (
            JSON_VALUE(ConfiguracionJson, '$.Endpoint') IS NOT NULL
         OR JSON_VALUE(ConfiguracionJson, '$.endpoint') IS NOT NULL
         OR ISNULL(JSON_VALUE(ConfiguracionJson, '$.ResourceAlias'), '') <>
            CASE
                WHEN Provider = 'azure-openai' THEN 'openai_primary'
                WHEN Provider = 'azure-content-understanding' AND [Key] LIKE '%-we' THEN 'cu_secondary'
                WHEN Provider = 'azure-content-understanding' THEN 'cu_primary'
                ELSE 'di'
            END
      );

    PRINT 'set-resource-aliases: ' + CAST(@@ROWCOUNT AS varchar(10)) + ' fila(s) actualizadas.';
    COMMIT TRANSACTION;
END
GO

-- Verificacion: ninguna fila activa debe conservar Endpoint y todas deben tener alias.
SELECT Id, Tipo, [Key], Provider,
       JSON_VALUE(ConfiguracionJson, '$.ResourceAlias') AS Alias,
       COALESCE(JSON_VALUE(ConfiguracionJson, '$.Endpoint'), JSON_VALUE(ConfiguracionJson, '$.endpoint')) AS Endpoint
FROM ModeloConfigs
WHERE Activo = 1 AND Tipo <> 4
ORDER BY Tipo, [Key];
GO
