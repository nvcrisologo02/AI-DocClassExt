-- =====================================================================
-- 07 - Fija version del artefacto y modo del clasificador por embeddings (AB#101570)
-- =====================================================================
-- Actualiza en un solo paso, en la fila ModeloConfigs Tipo=5,
-- Key='clasificador.embeddings':
--   $.Artefacto.BlobPath  -> modelos/clasificador-embeddings/<@Version>/clasificador-embeddings-<@Version>.json
--   $.Modo                -> @Modo
--   $.Restringido.Modo    -> @Modo (gobierna las peticiones con restriccionTipologias)
-- Umbrales, deployment y el resto de la configuracion quedan intactos.
--
-- VALE PARA CUALQUIER ENTORNO (DEV, PRE, PRO): no lleva el guardarrail por servidor
-- de los scripts 03 a 06. La release v1.2.0 lo aplica con @Modo = 'hibrido' y
-- @Version = 'v5' (ADR-006). Se ejecuta con scripts/database/apply-sql-dev.ps1
-- -ScriptPath <este fichero> [-Server <servidor> -PermitirNoDev]; fuera de DEV el
-- switch es obligatorio. No usa GO: es un solo lote.
--
-- PARAMETROS (editar los dos DECLARE de cabecera antes de ejecutar):
--   @Modo     off | sombra | hibrido
--   @Version  vN (p. ej. v5)
--
-- ARTEFACTO: el blob de @Version debe existir ANTES de ejecutar en el contenedor
-- 'documentai' del storage del entorno. Si se ejecuta antes de subirlo, el loader
-- conserva el ultimo modelo bueno y cachea el fallo 5 min.
--
-- Idempotente: si las tres claves ya valen lo pedido, imprime '0 cambios' y sale sin
-- backup ni transaccion. Si hay cambios: backup ModeloConfigs__bak_<ts> y un unico UPDATE.
-- Las validaciones van antes de cualquier backup. La app refresca en <= 5 min.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Modo NVARCHAR(10) = N'hibrido';
DECLARE @Version NVARCHAR(5) = N'v5';

-- 0. Validacion de parametros (antes de cualquier backup)
IF @Modo NOT IN (N'off', N'sombra', N'hibrido')
    THROW 50050, '@Modo invalido: debe ser off, sombra o hibrido.', 1;
IF @Version IS NULL OR @Version NOT LIKE N'v[0-9]%'
    THROW 50051, '@Version invalida: debe tener la forma vN (p. ej. v5).', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.ModeloConfigs WHERE Tipo = 5 AND [Key] = N'clasificador.embeddings' AND Activo = 1)
    THROW 50052, 'No existe la fila clasificador.embeddings activa (Tipo=5): ejecutar antes el script 01.', 1;

DECLARE @Ruta NVARCHAR(400) = CONCAT(N'modelos/clasificador-embeddings/', @Version, N'/clasificador-embeddings-', @Version, N'.json');
PRINT CONCAT('Destino: Modo=', @Modo, ' | BlobPath=', @Ruta);

-- 1. Camino idempotente: nada que cambiar
IF EXISTS (SELECT 1 FROM dbo.ModeloConfigs
           WHERE Tipo = 5 AND [Key] = N'clasificador.embeddings' AND Activo = 1
             AND JSON_VALUE(ConfiguracionJson, '$.Artefacto.BlobPath') = @Ruta
             AND JSON_VALUE(ConfiguracionJson, '$.Modo') = @Modo
             AND JSON_VALUE(ConfiguracionJson, '$.Restringido.Modo') = @Modo)
BEGIN
    PRINT '0 cambios';
    SELECT [Key],
           JSON_VALUE(ConfiguracionJson, '$.Modo') AS Modo,
           JSON_VALUE(ConfiguracionJson, '$.Restringido.Modo') AS ModoRestringido,
           JSON_VALUE(ConfiguracionJson, '$.Artefacto.BlobPath') AS BlobPath,
           JSON_VALUE(ConfiguracionJson, '$.UmbralConfianza') AS UmbralConfianza
    FROM dbo.ModeloConfigs
    WHERE Tipo = 5 AND [Key] = N'clasificador.embeddings';
    RETURN;
END

-- 2. Backup (convencion del proyecto)
DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
EXEC sp_executesql @sql;
PRINT CONCAT('Backup creado: dbo.', @bak);

BEGIN TRAN;

-- 3. Actualizacion de las tres claves en un solo UPDATE
UPDATE dbo.ModeloConfigs
SET ConfiguracionJson = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(ConfiguracionJson,
        '$.Artefacto.BlobPath', @Ruta),
        '$.Modo', @Modo),
        '$.Restringido.Modo', @Modo)
WHERE Tipo = 5 AND [Key] = N'clasificador.embeddings' AND Activo = 1;

PRINT CONCAT(@@ROWCOUNT, ' fila(s) actualizada(s)');

IF EXISTS (SELECT 1 FROM dbo.ModeloConfigs
           WHERE Tipo = 5 AND [Key] = N'clasificador.embeddings' AND ISJSON(ConfiguracionJson) <> 1)
    THROW 50053, 'El JSON resultante no es valido: se revierte.', 1;

COMMIT TRAN;

-- 4. Verificacion
SELECT [Key],
       JSON_VALUE(ConfiguracionJson, '$.Modo') AS Modo,
       JSON_VALUE(ConfiguracionJson, '$.Restringido.Modo') AS ModoRestringido,
       JSON_VALUE(ConfiguracionJson, '$.Artefacto.BlobPath') AS BlobPath,
       JSON_VALUE(ConfiguracionJson, '$.UmbralConfianza') AS UmbralConfianza
FROM dbo.ModeloConfigs
WHERE Tipo = 5 AND [Key] = N'clasificador.embeddings';
