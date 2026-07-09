/*  Restaura ConfiguracionJson original desde una tabla de backup creada por
    enable-assetresolver-by-schema.sql (columnas Id, Codigo, Version,
    ConfiguracionJson [valor original], FechaBackup).

    Uso:
      sqlcmd -S <srv> -d <DocumentIA> -E -v BackupTable="Tipologias_AssetResolverBak_YYYYMMDD_HHMMSS" -v WhatIf=1 -i rollback-assetresolver-by-schema.sql
      @WhatIf=1 (default) hace ROLLBACK tras mostrar el resultado; @WhatIf=0 hace COMMIT.
      @BackupTable es obligatorio: sin el, el script aborta (THROW 50010) antes de
      abrir transaccion.

    Restaura el JSON EXACTO (byte a byte) capturado en el backup, por Id; no
    reconstruye el bloque assetResolver, por lo que revierte tambien cualquier
    assetResolver preexistente que la activacion hubiera tocado.

    Nota: si se invoca sin "-v WhatIf=..." NO usar el flag "-b" de sqlcmd (ver
    enable-assetresolver-by-schema.sql para el motivo: sin -b, una variable de
    scripting no definida solo emite un aviso y sqlcmd sustituye el texto
    literal "$(WhatIf)"; el TRY_CAST de abajo asume ese comportamiento por
    defecto para resolver el default a 1). @BackupTable, en cambio, no tiene
    un default razonable (identifica la tabla de respaldo a restaurar), por lo
    que su ausencia (variable no sustituida, quedando el texto literal
    "$(BackupTable)", o cadena vacia) se trata como error explicito. */
SET NOCOUNT ON;

DECLARE @WhatIf BIT = TRY_CAST('$(WhatIf)' AS BIT);
IF @WhatIf IS NULL SET @WhatIf = 1;

DECLARE @bak SYSNAME = N'$(BackupTable)';
IF @bak IS NULL OR @bak = N'' OR @bak = N'$(BackupTable)'
    THROW 50010, 'Falta -v BackupTable (nombre de la tabla de respaldo a restaurar)', 1;

IF OBJECT_ID(N'dbo.' + QUOTENAME(@bak)) IS NULL
    THROW 50011, 'La tabla de backup indicada no existe en dbo', 1;

BEGIN TRAN;

-- Dynamic SQL: el nombre de la tabla de backup solo se conoce en tiempo de
-- ejecucion (via -v BackupTable=...), por lo que no puede referenciarse de
-- forma estatica. QUOTENAME() delimita el identificador para evitar
-- inyeccion via el nombre de tabla.
DECLARE @sql NVARCHAR(MAX) = N'
UPDATE t SET ConfiguracionJson = b.ConfiguracionJson
FROM dbo.Tipologias t
JOIN dbo.' + QUOTENAME(@bak) + N' b ON b.Id = t.Id;

SELECT t.Id, t.Codigo, t.Version
FROM dbo.Tipologias t
JOIN dbo.' + QUOTENAME(@bak) + N' b ON b.Id = t.Id
ORDER BY t.Codigo;';

EXEC sys.sp_executesql @sql;

IF @WhatIf = 1 BEGIN PRINT 'WhatIf=1: ROLLBACK'; ROLLBACK TRAN; END
ELSE BEGIN PRINT 'WhatIf=0: COMMIT'; COMMIT TRAN; END
