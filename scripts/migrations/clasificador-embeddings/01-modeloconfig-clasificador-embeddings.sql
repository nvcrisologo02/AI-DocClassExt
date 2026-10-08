-- =====================================================================
-- 01 - Fila ModeloConfigs del clasificador por embeddings (AB#100779)
-- =====================================================================
-- Da de alta la fila Tipo=5 (Embeddings), Key='clasificador.embeddings'.
--
-- MODO POR ENTORNO: DEV entra en 'sombra' (calcula y persiste sin alterar el
-- resultado). PRE y PRO entran en 'off': el artefacto no existe en su storage
-- hasta AB#100781 y en off no hay llamada de embeddings ni descarga.
--   -> Cambiar @Modo antes de ejecutar en cada entorno.
--
-- CREDENCIAL: AuthMode y ApiKey se copian de la fila activa
-- classification.gpt4o-mini-fallback, que ya resuelve contra la misma cuenta de
-- Azure OpenAI (alias openai_primary). Con DefaultAzureCredential no hay ApiKey.
--
-- ARTEFACTO: contenedor 'documentai' de la cuenta de storage de documentos del
-- entorno (en DEV srbstgdevdocai), ruta modelos/clasificador-embeddings/v1/.
--
-- Idempotente: no duplica la fila ni pisa una existente. Backup previo.
-- La app refresca la configuracion en <= 5 min (cache de memoria).
--
-- Ejecutar con (cambiar servidor segun entorno):
--   sqlcmd -S srbsqldevdocai.database.windows.net -d DocumentIA -G -i 01-modeloconfig-clasificador-embeddings.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Modo NVARCHAR(10) = N'sombra';   -- DEV: 'sombra' | PRE y PRO: 'off'

-- 0. Credencial de la fila de clasificacion GPT (ambas grafias de clave)
DECLARE @AuthMode NVARCHAR(50), @ApiKey NVARCHAR(400);
SELECT TOP (1)
    @AuthMode = COALESCE(JSON_VALUE(ConfiguracionJson, '$.AuthMode'), JSON_VALUE(ConfiguracionJson, '$.authMode')),
    @ApiKey   = COALESCE(JSON_VALUE(ConfiguracionJson, '$.ApiKey'),   JSON_VALUE(ConfiguracionJson, '$.apiKey'))
FROM dbo.ModeloConfigs
WHERE [Key] = N'classification.gpt4o-mini-fallback' AND Activo = 1;

IF @AuthMode IS NULL
    THROW 50001, 'No existe la fila activa classification.gpt4o-mini-fallback: no se puede copiar la credencial.', 1;

PRINT CONCAT('Modo: ', @Modo, ' | AuthMode copiado: ', @AuthMode, ' | ApiKey: ', CASE WHEN @ApiKey IS NULL THEN 'no' ELSE 'si' END);

-- 1. Backup (convencion del proyecto)
DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
EXEC sp_executesql @sql;
PRINT CONCAT('Backup creado: dbo.', @bak);

BEGIN TRAN;

-- 2. JSON de configuracion (claves PascalCase; el loader no distingue mayusculas)
DECLARE @json NVARCHAR(MAX) = N'{
  "DeploymentName": "text-embedding-3-large-030358",
  "ResourceAlias": "openai_primary",
  "AuthMode": "",
  "Artefacto": {
    "Container": "documentai",
    "BlobPath": "modelos/clasificador-embeddings/v1/clasificador-embeddings-v1.json"
  },
  "Modo": "off",
  "UmbralConfianza": 0.6,
  "Restringido": { "Modo": "off", "UmbralMasa": 0.5, "UmbralConfianzaCondicionada": 0.8 },
  "MaxChars": 24000,
  "TimeoutSeconds": 20
}';
SET @json = JSON_MODIFY(@json, '$.AuthMode', @AuthMode);
SET @json = JSON_MODIFY(@json, '$.Modo', @Modo);
SET @json = JSON_MODIFY(@json, '$.Restringido.Modo', @Modo);
IF @ApiKey IS NOT NULL
    SET @json = JSON_MODIFY(@json, '$.ApiKey', @ApiKey);

IF ISJSON(@json) <> 1
    THROW 50002, 'El JSON de configuracion no es valido.', 1;

-- 3. Alta idempotente
IF NOT EXISTS (SELECT 1 FROM dbo.ModeloConfigs WHERE Tipo = 5 AND [Key] = N'clasificador.embeddings')
BEGIN
    INSERT INTO dbo.ModeloConfigs (Tipo, [Key], Provider, Activo, ConfiguracionJson, FechaCreacion, CreadoPor)
    VALUES (5, N'clasificador.embeddings', N'azure-openai', 1, @json, SYSUTCDATETIME(), N'AB#100779');
    PRINT 'Fila clasificador.embeddings insertada.';
END
ELSE
    PRINT 'La fila clasificador.embeddings ya existe: no se modifica.';

COMMIT TRAN;

-- 4. Verificacion
SELECT Id, Tipo, [Key], Provider, Activo,
       JSON_VALUE(ConfiguracionJson, '$.Modo') AS Modo,
       JSON_VALUE(ConfiguracionJson, '$.Restringido.Modo') AS ModoRestringido,
       JSON_VALUE(ConfiguracionJson, '$.DeploymentName') AS DeploymentName,
       JSON_VALUE(ConfiguracionJson, '$.Artefacto.BlobPath') AS BlobPath
FROM dbo.ModeloConfigs
WHERE Tipo = 5;
