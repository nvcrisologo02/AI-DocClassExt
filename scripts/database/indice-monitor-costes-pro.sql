/*
    AB#100662 - Aplicacion en PRO de la recreacion del indice cubriente del Monitor con las
    columnas de coste en el INCLUDE (DocumentIA).

    Aplicacion MANUAL: el Apply del pipeline Migrations-BD no alcanza PRO (Deny Public
    Network Access). Este script sustituye a la migracion 20260920063449_IndiceMonitorCostes,
    que recrea el indice SIN ONLINE = ON y por tanto bloquearia la tabla durante el build.

    MOTIVO: la seccion /costes del Admin agrega CosteIAEur, CosteEstimado, CosteLayoutEur,
    CosteClasificacionEur, CosteExtraccionEur, CostePromptEur y TokensIA sobre el rango de
    FechaEjecucion. Sin esas columnas en el indice cada consulta hacia key lookup por fila y
    el agregado de 90 dias (~68k filas) tardaba 19,5 s: Admin_GetCostes agotaba el timeout
    de 30 s y la pagina mostraba ceros.

    DISPONIBILIDAD: mismo patron que indice-monitor-reutilizacion-pro.sql de AB#100258. SIN
    transaccion global: cada bloque auto-commita, los Sch-M duran milisegundos y el build
    ONLINE convive con lecturas y escrituras. DROP_EXISTING reconstruye en una sola operacion
    sin dejar la tabla sin indice de FechaEjecucion en ningun momento.

    Idempotente por bloque y re-ejecutable.

    Ejecucion (token Entra, sin credenciales):
        pwsh ./docs/auxiliares/temps/2026-09-20/run-sql-token.ps1 -ScriptPath scripts/database/indice-monitor-costes-pro.sql
    o bien:
        sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i indice-monitor-costes-pro.sql

    Verificacion posterior:
        SELECT i.name,
               SUM(CASE WHEN ic.is_included_column = 0 THEN 1 ELSE 0 END) AS cols_clave,
               SUM(CASE WHEN ic.is_included_column = 1 THEN 1 ELSE 0 END) AS cols_include
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        WHERE i.object_id = OBJECT_ID('DocumentoEjecuciones')
          AND i.name = 'IX_DocumentoEjecuciones_FechaEjecucion_Monitor'
        GROUP BY i.name;
    Esperado: 1 clave + 26 include (19 previas + 7 de coste).
*/

SET NOCOUNT ON;
GO

-- 1. Recreacion del indice cubriente del Monitor con las siete columnas de coste en el
--    INCLUDE. Bloque caro: DROP_EXISTING + ONLINE reconstruye sin dejar la tabla sin indice
--    y sin bloquear lecturas ni escrituras.
IF EXISTS (SELECT 1 FROM sys.index_columns ic
           JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
           JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
           WHERE i.object_id = OBJECT_ID('dbo.DocumentoEjecuciones')
             AND i.name = 'IX_DocumentoEjecuciones_FechaEjecucion_Monitor'
             AND c.name = 'CosteIAEur')
BEGIN
    PRINT 'IX_DocumentoEjecuciones_FechaEjecucion_Monitor ya incluye las columnas de coste.';
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
                 ReutilizadaPorDuplicado, EjecucionOriginalId,
                 CosteIAEur, CosteEstimado, CosteLayoutEur, CosteClasificacionEur,
                 CosteExtraccionEur, CostePromptEur, TokensIA)
        WITH (DROP_EXISTING = ON, ONLINE = ON);
    PRINT 'IX_DocumentoEjecuciones_FechaEjecucion_Monitor recreado con 26 include.';
END
GO

-- 2. Registrar la migracion como aplicada para que EF no intente repetirla.
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId = '20260920063449_IndiceMonitorCostes')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT '20260920063449_IndiceMonitorCostes', MAX(ProductVersion)
    FROM dbo.__EFMigrationsHistory;
    PRINT 'Migracion registrada en __EFMigrationsHistory.';
END
ELSE
    PRINT 'Migracion ya registrada en __EFMigrationsHistory.';
GO
