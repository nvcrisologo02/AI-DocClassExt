-- Ensayo de la pipeline Release en dev: toca solo la marca de actualizacion de una fila.
UPDATE dbo.ModeloConfigs SET FechaActualizacion = SYSUTCDATETIME() WHERE [Key] = N'clasificador.embeddings';
IF @@ROWCOUNT <> 1 THROW 50000, 'fila clasificador.embeddings no encontrada', 1;
