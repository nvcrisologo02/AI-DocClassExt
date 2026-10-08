/*
    AB#100863 - Aplicacion en PRO del indice IX_Documentos_MD5 (DocumentIA).

    Aplicacion MANUAL en PRO: sustituye a la migracion 20261005140618_IndiceDocumentosMD5,
    cuyo CreateIndex de EF no emite ONLINE = ON y mantendria el bloqueo de esquema mientras
    lee la tabla entera (72k filas, 1,3 GB, S0 sobre HDD).

    MOTIVO: VerificarDuplicadoPorMD5Activity (ingesta desde GDC) consulta Documentos por MD5
    y no habia indice sobre esa columna: scan completo de ~57.000 lecturas logicas por
    ejecucion. Con la cache fria (tras lotes de Batch o agregados del Monitor) la BD S0 se
    clava al 100 % de lectura fisica y la consulta supera el CommandTimeout de 30 s; la
    orquestacion devuelve ERROR al cliente y no persiste (alerta srbalertexcprodocai del
    05/10/2026; timeouts tambien el 21/09, 22/09, 29/09 y 02/10). El INCLUDE de SHA256
    resuelve entera en el indice la proyeccion Id/SHA256 que usa la actividad.

    DISPONIBILIDAD: mismo patron que indice-monitor-costes-pro.sql (AB#100662). SIN
    transaccion global: cada bloque auto-commita, los Sch-M duran milisegundos y el build
    ONLINE convive con lecturas y escrituras.

    Idempotente por bloque y re-ejecutable.

    Ejecucion (token Entra, sin credenciales):
        pwsh ./docs/auxiliares/temps/2026-09-20/run-sql-token.ps1 -ScriptPath scripts/database/indice-documentos-md5-pro.sql
    o bien:
        sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i indice-documentos-md5-pro.sql

    Verificacion posterior:
        SELECT i.name,
               SUM(CASE WHEN ic.is_included_column = 0 THEN 1 ELSE 0 END) AS cols_clave,
               SUM(CASE WHEN ic.is_included_column = 1 THEN 1 ELSE 0 END) AS cols_include
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        WHERE i.object_id = OBJECT_ID('dbo.Documentos')
          AND i.name = 'IX_Documentos_MD5'
        GROUP BY i.name;
    Esperado: 1 clave (MD5) + 1 include (SHA256). En Query Store, la consulta
    "SELECT TOP(1) [d].[Id], [d].[SHA256] ... WHERE [d].[MD5] = @__md5_0" debe bajar de
    ~57.000 a menos de 10 lecturas logicas.
*/

SET NOCOUNT ON;
GO

-- 1. Indice no clustered sobre MD5 con SHA256 incluido. Bloque caro (lee la tabla una vez):
--    ONLINE = ON no bloquea lecturas ni escrituras durante el build.
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE object_id = OBJECT_ID('dbo.Documentos')
             AND name = 'IX_Documentos_MD5')
BEGIN
    PRINT 'IX_Documentos_MD5 ya existe.';
END
ELSE
BEGIN
    CREATE NONCLUSTERED INDEX IX_Documentos_MD5
        ON dbo.Documentos (MD5)
        INCLUDE (SHA256)
        WITH (ONLINE = ON);
    PRINT 'IX_Documentos_MD5 creado.';
END
GO

-- 2. Registrar la migracion como aplicada para que el pipeline Migrations-BD no intente
--    repetirla (su CreateIndex fallaria porque el indice ya existe).
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId = '20261005140618_IndiceDocumentosMD5')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT '20261005140618_IndiceDocumentosMD5', MAX(ProductVersion)
    FROM dbo.__EFMigrationsHistory;
    PRINT 'Migracion registrada en __EFMigrationsHistory.';
END
ELSE
    PRINT 'Migracion ya registrada en __EFMigrationsHistory.';
GO
