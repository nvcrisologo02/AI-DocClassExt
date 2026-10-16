/*
    AB#100930 - Aplicacion en PRO de la migracion 20261013092225_ClaveIntencionDedup (DocumentIA).

    Aplicacion MANUAL en PRO: sustituye a la migracion EF, cuyo CreateIndex no emite
    ONLINE = ON y mantendria el bloqueo de esquema mientras lee DocumentoEjecuciones
    (>100k filas con contrato nvarchar(max), S0).

    MOTIVO: la reutilizacion por duplicado pasa a seleccionar la historica por
    (DocumentoId, ClaveIntencion, FechaEjecucion DESC). Sin el indice, cada duplicado
    recorreria todas las ejecuciones del documento con sus LOB.

    DISPONIBILIDAD: mismo patron que indice-documentos-md5-pro.sql (AB#100863). SIN
    transaccion global: cada bloque auto-commita; ADD COLUMN NULL es un cambio de
    metadatos; el build ONLINE convive con lecturas y escrituras.

    Idempotente por bloque y re-ejecutable.

    Ejecucion (token Entra, sin credenciales):
        sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i indice-documento-clave-pro.sql

    Verificacion posterior:
        SELECT name, max_length FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
          AND name IN ('ClaveIntencion','ClaveIntencionJson','MotivoReutilizacion');
        -- Esperado: 3 filas (64, -1, 80 bytes).
        SELECT i.name, ic.key_ordinal, ic.is_descending_key, ic.is_included_column, c.name
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
          AND i.name = 'IX_DocumentoEjecuciones_Documento_Clave'
        ORDER BY ic.is_included_column, ic.key_ordinal;
        -- Esperado: 3 claves (DocumentoId, ClaveIntencion, FechaEjecucion DESC) + 4 includes.
    El pipeline Migrations-BD debe quedar en "Pendientes: 0" tras registrar la migracion.
*/

SET NOCOUNT ON;
GO

-- 1. Columnas (metadatos, instantaneo).
IF COL_LENGTH('dbo.DocumentoEjecuciones', 'ClaveIntencion') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentoEjecuciones ADD ClaveIntencion nvarchar(32) NULL;
    PRINT 'ClaveIntencion anadida.';
END
ELSE PRINT 'ClaveIntencion ya existe.';
GO

IF COL_LENGTH('dbo.DocumentoEjecuciones', 'ClaveIntencionJson') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentoEjecuciones ADD ClaveIntencionJson nvarchar(max) NULL;
    PRINT 'ClaveIntencionJson anadida.';
END
ELSE PRINT 'ClaveIntencionJson ya existe.';
GO

IF COL_LENGTH('dbo.DocumentoEjecuciones', 'MotivoReutilizacion') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentoEjecuciones ADD MotivoReutilizacion nvarchar(40) NULL;
    PRINT 'MotivoReutilizacion anadida.';
END
ELSE PRINT 'MotivoReutilizacion ya existe.';
GO

-- 2. Indice ONLINE (bloque caro: lee la tabla una vez sin bloquear).
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
             AND name = 'IX_DocumentoEjecuciones_Documento_Clave')
    PRINT 'IX_DocumentoEjecuciones_Documento_Clave ya existe.';
ELSE
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentoEjecuciones_Documento_Clave
        ON dbo.DocumentoEjecuciones (DocumentoId, ClaveIntencion, FechaEjecucion DESC)
        INCLUDE (EjecucionGuid, Tipologia, EstadoFinal, ReutilizadaPorDuplicado)
        WITH (ONLINE = ON);
    PRINT 'IX_DocumentoEjecuciones_Documento_Clave creado.';
END
GO

-- 3. Registrar la migracion para que el pipeline Migrations-BD no la repita.
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261013092225_ClaveIntencionDedup')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT '20261013092225_ClaveIntencionDedup', MAX(ProductVersion) FROM dbo.__EFMigrationsHistory;
    PRINT 'Migracion registrada en __EFMigrationsHistory.';
END
ELSE PRINT 'Migracion ya registrada en __EFMigrationsHistory.';
GO
