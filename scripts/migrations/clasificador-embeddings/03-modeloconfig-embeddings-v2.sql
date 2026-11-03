-- =====================================================================
-- 03 - Apunta el clasificador por embeddings al artefacto v2 (AB#100780)
-- =====================================================================
-- Cambia SOLO Artefacto.BlobPath de la fila ModeloConfigs Tipo=5,
-- Key='clasificador.embeddings'. Modo, Restringido.Modo, umbrales y el resto
-- de la configuracion quedan intactos.
--
-- SOLO DEV (guardarrail por servidor). PRE y PRO se promueven con AB#100781
-- y ADR-001; no ejecutar este script alli.
--
-- ARTEFACTO: modelos/clasificador-embeddings/v2/clasificador-embeddings-v2.json
-- en el contenedor 'documentai' de srbstgdevdocai. Debe estar subido ANTES de
-- ejecutar este script. v1 se conserva en el blob (rollback: repetir con la
-- ruta v1).
--
-- Idempotente: si la ruta ya es v2, no actualiza ninguna fila. Backup previo.
-- La app refresca la configuracion en <= 5 min (cache de memoria).
--
-- Se ejecuta con scripts/database/query-sql.ps1 (no separa por GO).
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ServidorEsperado sysname = N'srbsqldevdocai';
DECLARE @Ruta NVARCHAR(400) = N'modelos/clasificador-embeddings/v2/clasificador-embeddings-v2.json';

-- 0. Guardarrail: aborta si la conexion no apunta al destino previsto
IF @@SERVERNAME <> @ServidorEsperado OR DB_NAME() <> N'DocumentIA'
    THROW 50000, 'ABORTADO: la conexion no apunta al servidor/BD esperados (ver @ServidorEsperado).', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.ModeloConfigs WHERE [Key] = N'clasificador.embeddings' AND Tipo = 5)
    THROW 50003, 'No existe la fila clasificador.embeddings (Tipo=5): ejecutar antes el script 01.', 1;

-- 1. Backup (convencion del proyecto)
DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
EXEC sp_executesql @sql;
PRINT CONCAT('Backup creado: dbo.', @bak);

BEGIN TRAN;

-- 2. Actualizacion idempotente de la ruta del artefacto
UPDATE dbo.ModeloConfigs
SET ConfiguracionJson = JSON_MODIFY(ConfiguracionJson, '$.Artefacto.BlobPath', @Ruta)
WHERE [Key] = N'clasificador.embeddings'
  AND Tipo = 5
  AND JSON_VALUE(ConfiguracionJson, '$.Artefacto.BlobPath') <> @Ruta;

PRINT CONCAT(@@ROWCOUNT, ' fila(s) actualizada(s)');

COMMIT TRAN;

-- 3. Verificacion
SELECT Id, [Key],
       JSON_VALUE(ConfiguracionJson, '$.Artefacto.BlobPath') AS BlobPath,
       JSON_VALUE(ConfiguracionJson, '$.Modo') AS Modo,
       JSON_VALUE(ConfiguracionJson, '$.Restringido.Modo') AS ModoRestringido
FROM dbo.ModeloConfigs
WHERE [Key] = N'clasificador.embeddings';
