/*  Restaura ConfiguracionJson original desde una tabla de backup creada por
    enable-assetresolver-by-schema.sql (columnas Id, Codigo, Version,
    ConfiguracionJson [valor original], FechaBackup).

    VARIANTE SSMS (ventana normal, sin SQLCMD Mode): edita mas abajo las dos lineas
    "DECLARE @bak ..." y "DECLARE @WhatIf ...". Pon en @bak el nombre REAL de la tabla
    de backup (Tipologias_AssetResolverBak_YYYYMMDD_HHMMSS) que imprimio el enable; con
    @bak vacio el script aborta (THROW 50010) a proposito. @WhatIf=1 muestra y hace
    ROLLBACK; @WhatIf=0 aplica (COMMIT). Ejecuta el fichero entero (F5).

    Restaura el JSON EXACTO (byte a byte) capturado en el backup, por Id; no
    reconstruye el bloque assetResolver, por lo que revierte tambien cualquier
    assetResolver preexistente que la activacion hubiera tocado. */
SET NOCOUNT ON;

DECLARE @WhatIf BIT = 1;   -- 1 = DRY-RUN (ROLLBACK) | 0 = APLICAR (COMMIT)

-- Pon aqui el nombre REAL de la tabla de backup a restaurar; con cadena vacia el
-- script aborta a proposito (THROW 50010) para no ejecutarse sin objetivo.
DECLARE @bak SYSNAME = N'';   -- <-- ej: N'Tipologias_AssetResolverBak_20260709_101530'
IF @bak IS NULL OR @bak = N''
    THROW 50010, 'Falta el nombre de la tabla de backup (@bak) a restaurar', 1;

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
