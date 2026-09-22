-- Limpieza de ApiKey en ModeloConfigs de un entorno que autentica por identidad
-- (plan "IA propia por entorno", Epic AB#100298; hallazgo de la Tarea 15, AB#100317).
--
-- Tras el cutover de DEV, sus filas de ModeloConfigs seguian guardando en claro en
-- ConfiguracionJson.ApiKey las keys de los recursos de IA de PRO, heredadas de las
-- copias DEV<-PRO. Las filas llevan AuthMode = DefaultAzureCredential, asi que la key
-- no se usa (AzureContentUnderstandingProvider/DocumentIntelligenceAuthHelper solo la
-- leen con AuthMode = ApiKey), pero una credencial de otro entorno no debe vivir en la
-- BD de este.
--
-- Que hace: en filas activas con AuthMode = DefaultAzureCredential y ApiKey no vacia,
-- deja "$.ApiKey" en "" y elimina "$.apiKey" si existiera (casing mixto historico).
-- Se deja "" y no se elimina la propiedad porque el seed config/*/models.json lleva
-- "ApiKey": "" y ConfigurationSeedService.MergeMissingJsonProperties solo reinyecta
-- propiedades ausentes o vacias: con "" el merge no cambia nada en el siguiente
-- arranque. Las filas con AuthMode = ApiKey no se tocan: su key si se usa.
--
-- Idempotente: una segunda pasada no encuentra filas. Hace copia previa en
-- ModeloConfigs__bak_<yyyyMMdd_HHmmss>; para volver atras:
--   UPDATE m SET m.ConfiguracionJson = b.ConfiguracionJson
--   FROM ModeloConfigs m JOIN ModeloConfigs__bak_<...> b ON b.Id = m.Id;
-- Ejecutar con lotes GO, p. ej. docs/auxiliares/temps/2026-09-07/aplicar-sql-dev.ps1.
-- No aplica a PRO mientras sus filas autentiquen con su propia key (AuthMode = ApiKey).
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @bak sysname = 'ModeloConfigs__bak_' + FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss');
DECLARE @pendientes int;

SELECT @pendientes = COUNT(*)
FROM ModeloConfigs
WHERE Activo = 1
  AND COALESCE(JSON_VALUE(ConfiguracionJson, '$.AuthMode'), JSON_VALUE(ConfiguracionJson, '$.authMode')) = 'DefaultAzureCredential'
  AND (
        NULLIF(JSON_VALUE(ConfiguracionJson, '$.ApiKey'), '') IS NOT NULL
     OR NULLIF(JSON_VALUE(ConfiguracionJson, '$.apiKey'), '') IS NOT NULL
  );

IF @pendientes = 0
BEGIN
    PRINT 'clear-model-api-keys: nada que hacer (ninguna fila activa con identidad conserva ApiKey).';
END
ELSE
BEGIN
    PRINT 'clear-model-api-keys: ' + CAST(@pendientes AS varchar(10)) + ' fila(s) a limpiar; copia previa en ' + @bak;
    EXEC('SELECT * INTO ' + @bak + ' FROM ModeloConfigs');

    BEGIN TRANSACTION;

    UPDATE ModeloConfigs
    SET ConfiguracionJson = JSON_MODIFY(JSON_MODIFY(ConfiguracionJson, '$.ApiKey', ''), '$.apiKey', NULL),
        FechaActualizacion = SYSUTCDATETIME()
    WHERE Activo = 1
      AND COALESCE(JSON_VALUE(ConfiguracionJson, '$.AuthMode'), JSON_VALUE(ConfiguracionJson, '$.authMode')) = 'DefaultAzureCredential'
      AND (
            NULLIF(JSON_VALUE(ConfiguracionJson, '$.ApiKey'), '') IS NOT NULL
         OR NULLIF(JSON_VALUE(ConfiguracionJson, '$.apiKey'), '') IS NOT NULL
      );

    PRINT 'clear-model-api-keys: ' + CAST(@@ROWCOUNT AS varchar(10)) + ' fila(s) limpiadas.';
    COMMIT TRANSACTION;
END
GO

-- Verificacion: ninguna fila activa con identidad debe conservar key; las de AuthMode = ApiKey
-- se listan para dejar constancia de que conservan la suya.
SELECT Id, Tipo, [Key], Provider,
       COALESCE(JSON_VALUE(ConfiguracionJson, '$.AuthMode'), JSON_VALUE(ConfiguracionJson, '$.authMode')) AS AuthMode,
       LEN(COALESCE(NULLIF(JSON_VALUE(ConfiguracionJson, '$.ApiKey'), ''), NULLIF(JSON_VALUE(ConfiguracionJson, '$.apiKey'), ''), '')) AS ApiKeyLen
FROM ModeloConfigs
WHERE Activo = 1
ORDER BY Tipo, [Key];
GO
