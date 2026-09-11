/*
    AB#100258 - Aplicacion en PRO de las columnas de reutilizacion por duplicado y de la
    recreacion del indice cubriente del Monitor (DocumentIA).

    Aplicacion MANUAL: el Apply del pipeline Migrations-BD no alcanza PRO (Deny Public
    Network Access). Este script sustituye al tramo de la migracion
    20260911093509_ReutilizacionPorDuplicado, que crea el indice SIN ONLINE = ON y por
    tanto bloquearia la tabla durante el build. El resto de la migracion (columnas, FK,
    indices pequenos) se puede aplicar con el script idempotente de EF; aqui va todo junto
    para no depender del orden.

    DISPONIBILIDAD: mismo patron que indice-monitor-pro.sql de AB#100185. SIN transaccion
    global: cada bloque auto-commita, de modo que los Sch-M duran milisegundos y el build
    ONLINE convive con lecturas y escrituras. El indice nuevo se crea con DROP_EXISTING,
    que lo reconstruye en una sola operacion sin dejar la tabla sin indice de
    FechaEjecucion en ningun momento.

    Idempotente por bloque y re-ejecutable: si falla a medias, volver a lanzarlo completa
    lo que falte.

    Ejecucion:
        sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i indice-monitor-reutilizacion-pro.sql

    Despues hay que registrar la migracion como aplicada para que EF no la vuelva a
    intentar (ver ultimo bloque).

    Verificacion posterior:
        SELECT i.name,
               SUM(CASE WHEN ic.is_included_column = 0 THEN 1 ELSE 0 END) AS cols_clave,
               SUM(CASE WHEN ic.is_included_column = 1 THEN 1 ELSE 0 END) AS cols_include
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        WHERE i.object_id = OBJECT_ID('DocumentoEjecuciones')
        GROUP BY i.name ORDER BY i.name;
    Esperado: IX_DocumentoEjecuciones_FechaEjecucion_Monitor con 1 clave + 19 include
    (17 previas + ReutilizadaPorDuplicado + EjecucionOriginalId),
    IX_DocumentoEjecuciones_EjecucionOriginalId con 1 clave, e
    IX_DocumentoEjecuciones_InstanceId_Reutilizadas con 1 clave y filtro.
*/

SET NOCOUNT ON;
GO

-- 1. Columnas. Metadatos: instantaneo, sin bloqueo relevante.
IF COL_LENGTH('dbo.DocumentoEjecuciones', 'ReutilizadaPorDuplicado') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentoEjecuciones
        ADD ReutilizadaPorDuplicado bit NOT NULL CONSTRAINT DF_DocumentoEjecuciones_ReutilizadaPorDuplicado DEFAULT (CAST(0 AS bit));
    PRINT 'Columna ReutilizadaPorDuplicado creada.';
END
ELSE
    PRINT 'Columna ReutilizadaPorDuplicado ya existe.';
GO

IF COL_LENGTH('dbo.DocumentoEjecuciones', 'EjecucionOriginalId') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentoEjecuciones ADD EjecucionOriginalId int NULL;
    PRINT 'Columna EjecucionOriginalId creada.';
END
ELSE
    PRINT 'Columna EjecucionOriginalId ya existe.';
GO

-- 2. Indice de la FK autorreferenciada. Tabla grande pero indice de una sola columna
--    anulable: ONLINE igualmente para no bloquear.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
                 AND name = 'IX_DocumentoEjecuciones_EjecucionOriginalId')
BEGIN
    CREATE INDEX IX_DocumentoEjecuciones_EjecucionOriginalId
        ON dbo.DocumentoEjecuciones (EjecucionOriginalId)
        WITH (ONLINE = ON);
    PRINT 'IX_DocumentoEjecuciones_EjecucionOriginalId creado.';
END
ELSE
    PRINT 'IX_DocumentoEjecuciones_EjecucionOriginalId ya existe.';
GO

-- 3. Indice filtrado de la guarda de idempotencia. Solo cubre filas de reutilizacion,
--    que al aplicar son cero: la creacion es practicamente instantanea.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
                 AND name = 'IX_DocumentoEjecuciones_InstanceId_Reutilizadas')
BEGIN
    CREATE INDEX IX_DocumentoEjecuciones_InstanceId_Reutilizadas
        ON dbo.DocumentoEjecuciones (InstanceId)
        WHERE ReutilizadaPorDuplicado = 1
        WITH (ONLINE = ON);
    PRINT 'IX_DocumentoEjecuciones_InstanceId_Reutilizadas creado.';
END
ELSE
    PRINT 'IX_DocumentoEjecuciones_InstanceId_Reutilizadas ya existe.';
GO

-- 4. FK autorreferenciada. WITH NOCHECK para no validar las filas existentes (todas
--    tienen EjecucionOriginalId NULL, no hay nada que validar) y evitar el escaneo.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = 'FK_DocumentoEjecuciones_DocumentoEjecuciones_EjecucionOriginalId')
BEGIN
    ALTER TABLE dbo.DocumentoEjecuciones WITH NOCHECK
        ADD CONSTRAINT FK_DocumentoEjecuciones_DocumentoEjecuciones_EjecucionOriginalId
        FOREIGN KEY (EjecucionOriginalId) REFERENCES dbo.DocumentoEjecuciones (Id);
    PRINT 'FK autorreferenciada creada.';
END
ELSE
    PRINT 'FK autorreferenciada ya existe.';
GO

-- 5. Recreacion del indice cubriente del Monitor con las dos columnas nuevas en el
--    INCLUDE. Este es el bloque caro: DROP_EXISTING + ONLINE reconstruye sin dejar la
--    tabla sin indice y sin bloquear lecturas ni escrituras.
IF EXISTS (SELECT 1 FROM sys.index_columns ic
           JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
           JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
           WHERE i.object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
             AND i.name = 'IX_DocumentoEjecuciones_FechaEjecucion_Monitor'
             AND c.name = 'ReutilizadaPorDuplicado')
BEGIN
    PRINT 'IX_DocumentoEjecuciones_FechaEjecucion_Monitor ya incluye las columnas nuevas.';
END
ELSE
BEGIN
    CREATE INDEX IX_DocumentoEjecuciones_FechaEjecucion_Monitor
        ON dbo.DocumentoEjecuciones (FechaEjecucion)
        INCLUDE (EstadoFinal, ConfianzaGlobal, UseFallbackLLM, Tipologia,
                 ModeloClasificacion, ClassificationOnly, DuracionTotalMs,
                 DocumentoId, EjecucionGuid, SubmittedBy, ConfianzaClasificacion,
                 DuracionClasificacionMs, DuracionExtraccionMs, DuracionGDCMs,
                 DuracionValidacionMs, DuracionIntegracionMs, DuracionPersistenciaMs,
                 ReutilizadaPorDuplicado, EjecucionOriginalId)
        WITH (DROP_EXISTING = ON, ONLINE = ON);
    PRINT 'IX_DocumentoEjecuciones_FechaEjecucion_Monitor recreado con 19 include.';
END
GO

-- 6. Registrar la migracion como aplicada para que EF no intente repetirla.
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId = '20260911093509_ReutilizacionPorDuplicado')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT '20260911093509_ReutilizacionPorDuplicado', MAX(ProductVersion)
    FROM dbo.__EFMigrationsHistory;
    PRINT 'Migracion registrada en __EFMigrationsHistory.';
END
ELSE
    PRINT 'Migracion ya registrada en __EFMigrationsHistory.';
GO
