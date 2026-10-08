-- Hash de deriva por tabla de configuracion (columnas estables, filas activas).
-- Excluye columnas de auditoria (Fecha*, *AtUtc, *Por, *By). Un SELECT por tabla,
-- cada uno con columnas Tabla / Hash (SHA2_256 sobre FOR JSON AUTO).
-- Usado por export-config-release.ps1 y por el pipeline config-seed para detectar
-- deriva de configuracion entre releases.
SET NOCOUNT ON;
DECLARE @t TABLE (Tabla sysname, Filtro nvarchar(200), Orden nvarchar(200));
INSERT @t VALUES
 ('ModeloConfigs',  'WHERE Activo = 1 AND Tipo <> 4', 'ORDER BY Tipo, [Key]'),
 ('PromptTemplates','WHERE IsActive = 1',              'ORDER BY PromptKey'),
 ('Tipologias',     '',                                'ORDER BY 1'),
 ('CatalogoTdn1',   '',                                'ORDER BY Id'),
 ('CatalogoTdn2',   '',                                'ORDER BY Id');
DECLARE @tabla sysname, @filtro nvarchar(200), @orden nvarchar(200), @cols nvarchar(max), @sql nvarchar(max);
DECLARE c CURSOR FOR SELECT Tabla, Filtro, Orden FROM @t;
OPEN c; FETCH NEXT FROM c INTO @tabla, @filtro, @orden;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @cols = STRING_AGG(QUOTENAME(COLUMN_NAME), ',') WITHIN GROUP (ORDER BY ORDINAL_POSITION)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tabla
      AND COLUMN_NAME NOT LIKE 'Fecha%' AND COLUMN_NAME NOT LIKE '%AtUtc'
      AND COLUMN_NAME NOT LIKE '%Por' AND COLUMN_NAME NOT LIKE '%By';
    SET @sql = N'SELECT ''' + @tabla + N''' AS Tabla, CONVERT(varchar(64), HASHBYTES(''SHA2_256'', '
             + N'(SELECT ' + @cols + N' FROM ' + QUOTENAME(@tabla) + N' ' + @filtro + N' ' + @orden + N' FOR JSON AUTO)), 2) AS Hash';
    EXEC sp_executesql @sql;
    FETCH NEXT FROM c INTO @tabla, @filtro, @orden;
END
CLOSE c; DEALLOCATE c;
