-- Cutover de un entorno a sus recursos de IA propios (plan "IA propia por
-- entorno", Epic AB#100298; fase 2a PRE, AB#100320): pone AuthMode =
-- DefaultAzureCredential en cada fila activa de ModeloConfigs para que los
-- proveedores (AzureContentUnderstandingProvider, DocumentIntelligenceAuthHelper,
-- GptClasificarDataProvider) autentiquen con la identidad administrada de la
-- Function App en lugar de con la ApiKey guardada en ConfiguracionJson.
--
-- Por que existe: set-resource-aliases.sql solo cambia el endpoint (alias) y no
-- toca AuthMode. Las copias PRE<-PRO dejaron las filas en ApiKey; DEV quedo con
-- identidad en el cutover del 2026-09-22 y PRE tiene que quedar igual.
--
-- Que hace: en filas activas (Tipo <> 4, catalogo de tarifas fuera) de los
-- proveedores de IA cuyo AuthMode no sea ya DefaultAzureCredential (incluidas las
-- que no tienen la propiedad), escribe "$.AuthMode" = "DefaultAzureCredential" y
-- elimina "$.authMode" si existiera (casing mixto historico). No toca ApiKey: eso
-- lo hace clear-model-api-keys.sql una vez comprobado el entorno.
--
-- Idempotente: una segunda pasada no encuentra filas. El seed config/*/models.json
-- lleva "AuthMode": "ApiKey", pero ConfigurationSeedService.MergeMissingJsonProperties
-- solo reinyecta propiedades ausentes o vacias, asi que el valor se conserva en
-- cada arranque. Hace copia previa en ModeloConfigs__bak_<yyyyMMdd_HHmmss>; para
-- volver atras:
--   UPDATE m SET m.ConfiguracionJson = b.ConfiguracionJson
--   FROM ModeloConfigs m JOIN ModeloConfigs__bak_<...> b ON b.Id = m.Id;
--
-- Requisitos previos (si faltan, las llamadas devuelven 401/403 tras el reinicio):
--   - la identidad administrada de la Function App tiene Cognitive Services User
--     (y Azure AI User en las cuentas Foundry) sobre las cuentas del entorno a las
--     que apuntan los alias (infra/ai/resources.<env>.json);
--   - set-resource-aliases.sql ya aplicado, o se aplica en la misma ventana: con
--     identidad del entorno contra un Endpoint de otro entorno la llamada falla.
-- Orden en el cutover (infra/ai/README.md, "Cutover de un entorno"): despues de
-- set-resource-aliases.sql y antes del reinicio de la Function App.
-- Ejecutar con lotes GO, p. ej. docs/auxiliares/temps/2026-09-07/aplicar-sql-dev.ps1.
-- No aplica a PRO mientras sus filas autentiquen con su propia key (fase 3).
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @bak sysname = 'ModeloConfigs__bak_' + FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss');
DECLARE @pendientes int;

SELECT @pendientes = COUNT(*)
FROM ModeloConfigs
WHERE Activo = 1 AND Tipo <> 4
  AND Provider IN ('azure-openai', 'azure-content-understanding', 'azure-document-intelligence', 'azure-document-intelligence-layout')
  AND ISNULL(COALESCE(JSON_VALUE(ConfiguracionJson, '$.AuthMode'), JSON_VALUE(ConfiguracionJson, '$.authMode')), '') <> 'DefaultAzureCredential';

IF @pendientes = 0
BEGIN
    PRINT 'set-auth-mode-identity: nada que hacer (todas las filas activas ya autentican con DefaultAzureCredential).';
END
ELSE
BEGIN
    PRINT 'set-auth-mode-identity: ' + CAST(@pendientes AS varchar(10)) + ' fila(s) a actualizar; copia previa en ' + @bak;
    EXEC('SELECT * INTO ' + @bak + ' FROM ModeloConfigs');

    BEGIN TRANSACTION;

    UPDATE ModeloConfigs
    SET ConfiguracionJson = JSON_MODIFY(JSON_MODIFY(ConfiguracionJson, '$.AuthMode', 'DefaultAzureCredential'), '$.authMode', NULL),
        FechaActualizacion = SYSUTCDATETIME()
    WHERE Activo = 1 AND Tipo <> 4
      AND Provider IN ('azure-openai', 'azure-content-understanding', 'azure-document-intelligence', 'azure-document-intelligence-layout')
      AND ISNULL(COALESCE(JSON_VALUE(ConfiguracionJson, '$.AuthMode'), JSON_VALUE(ConfiguracionJson, '$.authMode')), '') <> 'DefaultAzureCredential';

    PRINT 'set-auth-mode-identity: ' + CAST(@@ROWCOUNT AS varchar(10)) + ' fila(s) actualizadas.';
    COMMIT TRANSACTION;
END
GO

-- Verificacion: toda fila activa de los proveedores de IA debe llevar DefaultAzureCredential.
SELECT Id, Tipo, [Key], Provider,
       COALESCE(JSON_VALUE(ConfiguracionJson, '$.AuthMode'), JSON_VALUE(ConfiguracionJson, '$.authMode')) AS AuthMode,
       JSON_VALUE(ConfiguracionJson, '$.ResourceAlias') AS Alias
FROM ModeloConfigs
WHERE Activo = 1 AND Tipo <> 4
ORDER BY Tipo, [Key];
GO
