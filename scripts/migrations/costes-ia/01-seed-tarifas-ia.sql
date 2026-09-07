-- =====================================================================
-- 01 - Catalogo de tarifas de servicios de IA
-- =====================================================================
-- AB#100234. Da de alta la fila unica Tipo=4 (Tarifas), Key='tarifas.ia'
-- con el catalogo de precios por modelo fisico y fecha de vigencia.
--
-- IMPORTANTE - ANTES DE EJECUTAR
--   Los importes de este script van a 0.0 a proposito. Hay que sustituirlos
--   por los precios vigentes de la suscripcion ANTES de ejecutarlo. Un
--   precio inventado es peor que ningun precio: con el catalogo vacio o
--   incompleto el sistema funciona igual, registra tokens y paginas, deja
--   el coste a nulo y marca el agregado como TarifasCompletas=false.
--
--   Los precios dependen de la region, del tipo de despliegue (Global,
--   DataZone o Regional) y de los acuerdos de la suscripcion, asi que no se
--   pueden dar por buenos sin comprobarlos. Fuentes:
--     - Portal de Azure > Cost Management > Analisis de costes, agrupando
--       por Medidor sobre el grupo de recursos SRBRGDOCSAIPROD.
--     - Paginas de precios de Azure OpenAI, Content Understanding y
--       Document Intelligence.
--     - El informe de costes en docs/auxiliares/temps/2026-07-21/ tiene el
--       mapeo de medidor a proceso y sirve de contraste.
--
-- REGLA DE MANTENIMIENTO
--   Cambiar un precio es ANADIR una linea nueva al array con su
--   VigenteDesde, NUNCA editar una existente. Las ejecuciones antiguas
--   deben seguir cuadrando con lo que se facturo entonces.
--
-- El paso 0 lista los modelos que el sistema tiene configurados: el
-- catalogo debe cubrirlos todos para que el coste salga completo.
--
-- Idempotente: reejecutar no duplica la fila ni pisa un catalogo existente.
-- La app refresca el catalogo en <= 5 min (cache de memoria).
--
-- Ejecutar con (cambiar servidor segun entorno):
--   sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i 01-seed-tarifas-ia.sql
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 0. Modelos configurados que hay que tarifar
PRINT '--- Modelos configurados (deben aparecer todos en el catalogo) ---';
SELECT
    Tipo,
    [Key],
    Provider,
    Activo,
    JSON_VALUE(ConfiguracionJson, '$.DeploymentName') AS DeploymentName,
    JSON_VALUE(ConfiguracionJson, '$.ClassifierId')   AS ClassifierId,
    JSON_VALUE(ConfiguracionJson, '$.AnalyzerId')     AS AnalyzerId
FROM dbo.ModeloConfigs
WHERE Activo = 1 AND Tipo <> 4
ORDER BY Tipo, [Key];

-- 1. Backup de la tabla destino (convencion del proyecto)
DECLARE @bak SYSNAME = CONCAT('ModeloConfigs__bak_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@bak), ' FROM dbo.ModeloConfigs;');
EXEC sp_executesql @sql;
PRINT CONCAT('Backup creado: dbo.', @bak);

BEGIN TRAN;

-- 2. Catalogo
--    Claves de modelo: el nombre fisico consumido, tal como llega al contrato.
--      - Azure OpenAI ......... DeploymentName ('gpt-4o-mini', 'gpt-5-mini')
--      - Layout ............... 'prebuilt-layout'
--      - Clasificador DI ...... ClassifierId
--      - Content Understanding  AnalyzerId
--      - Modelo interno de CU . el que declara el servicio en usage.tokens,
--                               por ejemplo 'gpt-4.1' y 'text-embedding-3-large'
--
--    Campos de precio (todos opcionales; se aplica solo lo informado):
--      EurEntradaPor1M, EurEntradaCachePor1M, EurSalidaPor1M
--      EurContextualizacionPor1M, EurPorPagina
--
--    Los cacheados son un SUBCONJUNTO de la entrada, no un sumando: el
--    sistema resta y aplica a esa parte el precio reducido.
DECLARE @catalogo NVARCHAR(MAX) = N'{
  "Moneda": "EUR",
  "Tarifas": [
    { "Modelo": "gpt-4o-mini", "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 0.0, "EurEntradaCachePor1M": 0.0, "EurSalidaPor1M": 0.0 },

    { "Modelo": "gpt-5-mini", "VigenteDesde": "2026-07-21",
      "EurEntradaPor1M": 0.0, "EurEntradaCachePor1M": 0.0, "EurSalidaPor1M": 0.0 },

    { "Modelo": "gpt-4.1", "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 0.0, "EurEntradaCachePor1M": 0.0, "EurSalidaPor1M": 0.0 },

    { "Modelo": "text-embedding-3-large", "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 0.0 },

    { "Modelo": "prebuilt-layout", "VigenteDesde": "2026-04-01",
      "EurPorPagina": 0.0 }
  ]
}';

INSERT INTO dbo.ModeloConfigs (Tipo, [Key], Provider, Activo, ConfiguracionJson, FechaCreacion, CreadoPor)
SELECT 4, 'tarifas.ia', 'catalogo', 1, @catalogo, SYSUTCDATETIME(), 'script-01-seed-tarifas-ia'
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ModeloConfigs WHERE [Key] = 'tarifas.ia' AND Tipo = 4
);
PRINT CONCAT('Filas de catalogo insertadas: ', @@ROWCOUNT);

COMMIT;

-- 3. Verificacion
PRINT '--- Catalogo resultante ---';
SELECT Id, Tipo, [Key], Activo, ConfiguracionJson
FROM dbo.ModeloConfigs
WHERE Tipo = 4;

-- 4. Lineas del catalogo, desglosadas
SELECT
    JSON_VALUE(t.value, '$.Modelo')                    AS Modelo,
    JSON_VALUE(t.value, '$.VigenteDesde')              AS VigenteDesde,
    JSON_VALUE(t.value, '$.EurEntradaPor1M')           AS EurEntradaPor1M,
    JSON_VALUE(t.value, '$.EurEntradaCachePor1M')      AS EurEntradaCachePor1M,
    JSON_VALUE(t.value, '$.EurSalidaPor1M')            AS EurSalidaPor1M,
    JSON_VALUE(t.value, '$.EurContextualizacionPor1M') AS EurContextualizacionPor1M,
    JSON_VALUE(t.value, '$.EurPorPagina')              AS EurPorPagina
FROM dbo.ModeloConfigs m
CROSS APPLY OPENJSON(m.ConfiguracionJson, '$.Tarifas') t
WHERE m.Tipo = 4 AND m.[Key] = 'tarifas.ia';
