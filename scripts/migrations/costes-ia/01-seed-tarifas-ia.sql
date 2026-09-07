-- =====================================================================
-- 01 - Catalogo de tarifas de servicios de IA
-- =====================================================================
-- AB#100234. Da de alta la fila unica Tipo=4 (Tarifas), Key='tarifas.ia'
-- con el catalogo de precios por modelo fisico y fecha de vigencia.
--
-- ORIGEN DE LOS PRECIOS
--   Precios de LISTA (retail) de la Retail Prices API de Azure en EUR,
--   consultados el 2026-09-07 para la region westeurope, que es donde estan
--   los recursos (srbaisrv-westeurope y srbdiprodocai en SRBRGDOCSAIPROD).
--
--   El tipo de despliegue determina el medidor que factura. Los SKU reales de
--   produccion, leidos con 'az cognitiveservices account deployment list':
--     gpt-4.1-892749                 gpt-4.1                 GlobalStandard
--     gpt-4.1-mini-590191            gpt-4.1-mini            GlobalStandard
--     text-embedding-3-large-010650  text-embedding-3-large  GlobalStandard
--     gpt-5-mini                     gpt-5-mini              DataZoneStandard
--   En DEV existe ademas el deployment 'gpt-4o-mini', que en realidad sirve
--   el modelo gpt-4.1-mini con SKU Standard, es decir precio REGIONAL. Por eso
--   el catalogo lleva las dos lineas: la clave es el nombre del deployment.
--
--   SI LA SUSCRIPCION TIENE DESCUENTO sobre precio de lista, estos importes
--   quedan por encima del coste real. Contrastar contra Cost Management antes
--   de darlos por definitivos, y anadir lineas nuevas con la fecha de vigencia
--   en lugar de editar estas.
--
--   Los identificadores del clasificador DI y de los analizadores de CU se
--   leyeron de las APIs de los recursos de produccion el 2026-09-07, asi que el
--   catalogo esta completo. El paso 0 sigue estando para verificar que los que
--   usa la configuracion coinciden con los que aqui se tarifan.
--
--   Reproducir la consulta (el proxy corporativo obliga a --ssl-no-revoke):
--     curl -sS --ssl-no-revoke --get https://prices.azure.com/api/retail/prices
--          --data-urlencode "currencyCode=EUR"
--          --data-urlencode "$filter=productName eq 'Azure OpenAI' and armRegionName eq 'westeurope'"
--   Medidores relevantes en westeurope (EUR):
--     gpt 4.1 mini Inp/cached/Outp regnl .... 0,0005 / 0,0001 / 0,0018 por 1K tokens
--     gpt 4.1 mini Inp/cached/Outp glbl ..... 0,0003 / 0,0001 / 0,0014 por 1K tokens
--     5 mini pp Inp/cd Inp/Opt Dz ........... 0,4250 / 0,0425 / 3,4003 por 1M tokens
--     gpt 4.1 Inp/cached/Outp glbl .......... 0,0017 / 0,0004 / 0,0069 por 1K tokens
--     text-embedding-3-large-glbl ........... 0,0001 por 1K tokens
--     S0 Pre-built Pages (DI layout) ........ 8,5866 por 1.000 paginas
--     S0 pages for doc classifier ........... 2,5760 por 1.000 paginas
--     Doc Content Extraction Standard Pages . 4,2933 por 1.000 paginas
--     Std Contextualization Tokens .......... 0,0009 por 1K tokens
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
-- ATENCION: el literal siguiente debe ser JSON valido ESTRICTO. El cargador de
--    la aplicacion rechaza comentarios y, al ser tolerante a errores, un JSON
--    invalido no da error visible: deja el catalogo vacio en silencio y todos
--    los costes salen a nulo. Por eso las anotaciones van aqui fuera.
--
--    AZURE OPENAI. Clave = nombre del DEPLOYMENT, no del modelo, porque el tipo
--    de despliegue determina el medidor y cambia el precio hasta un 70 %:
--      gpt-4o-mini ............ sirve gpt-4.1-mini con SKU Standard  -> "gpt 4.1 mini ... regnl"
--      gpt-4.1-mini ........... gpt-4.1-mini-590191, GlobalStandard  -> "gpt 4.1 mini ... glbl"
--      gpt-5-mini ............. DataZoneStandard                     -> "5 mini pp ... Dz"
--      gpt-4.1 ................ lo consume Content Understanding      -> "gpt 4.1 ... glbl"
--      text-embedding-3-large . embeddings de CU, GlobalStandard      -> "text-embedding-3-large-glbl"
--
--    DOCUMENT INTELLIGENCE. Clave = identificador del modelo:
--      prebuilt-layout ........ layout a markdown  -> "S0 Pre-built Pages" 8,5866 / 1.000 pag
--      DocumentAICC_v0/_v1 .... clasificadores del recurso srbdiprodocai,
--                               leidos de la API el 2026-09-07
--                               -> "S0 pages for doc classifier" 2,576 / 1.000 pag
--
--    CONTENT UNDERSTANDING. Las paginas NO se tarifan por analizador sino por
--    medidor, porque el precio depende del procesamiento aplicado y la
--    diferencia es de casi 500 veces:
--      cu.documentPagesMinimal .. documentos digitales (DOCX, XLSX, HTML, TXT)
--                                 -> "Doc Content Extraction Min Pages" 0,0086 / 1.000 pag
--      cu.documentPagesBasic .... imagen con OCR simple
--                                 -> "Doc Content Extraction Basic Pages" 0,8587 / 1.000 pag
--      cu.documentPagesStandard . imagen con analisis de layout
--                                 -> "Doc Content Extraction Standard Pages" 4,2933 / 1.000 pag
--    La contextualizacion si va por analizador, porque su precio depende del
--    workflow que resuelva. Los dos analizadores del recurso srbaisrv-westeurope
--    (CU_NS_1.5_0 y CU_NS_1.6_0_GGAA) tienen enableLayout=true y enableOcr=true
--    sobre prebuilt-document, workflow estandar
--      -> "Std Contextualization Tokens" 0,0009 / 1K tokens
--
--    LIMITACION CONOCIDA: los analizadores llevan enableFormula=true, que activa
--    el medidor "Add-On Formula Pages" (2,576 / 1.000 pag). El bloque usage de la
--    respuesta no lo declara por separado, asi que ese add-on no se puede imputar
--    desde el consumo y queda fuera del coste calculado.
DECLARE @catalogo NVARCHAR(MAX) = N'{
  "Moneda": "EUR",
  "Tarifas": [
    {
      "Modelo": "gpt-4o-mini",
      "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 0.5,
      "EurEntradaCachePor1M": 0.1,
      "EurSalidaPor1M": 1.8
    },
    {
      "Modelo": "gpt-4.1-mini",
      "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 0.3,
      "EurEntradaCachePor1M": 0.1,
      "EurSalidaPor1M": 1.4
    },
    {
      "Modelo": "gpt-5-mini",
      "VigenteDesde": "2026-07-21",
      "EurEntradaPor1M": 0.425,
      "EurEntradaCachePor1M": 0.0425,
      "EurSalidaPor1M": 3.4003
    },
    {
      "Modelo": "gpt-4.1",
      "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 1.7,
      "EurEntradaCachePor1M": 0.4,
      "EurSalidaPor1M": 6.9
    },
    {
      "Modelo": "text-embedding-3-large",
      "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 0.1
    },
    {
      "Modelo": "prebuilt-layout",
      "VigenteDesde": "2026-04-01",
      "EurPorPagina": 0.0085866
    },
    {
      "Modelo": "DocumentAICC_v0",
      "VigenteDesde": "2026-04-01",
      "EurPorPagina": 0.002576
    },
    {
      "Modelo": "DocumentAICC_v1",
      "VigenteDesde": "2026-05-13",
      "EurPorPagina": 0.002576
    },
    {
      "Modelo": "cu.documentPagesMinimal",
      "VigenteDesde": "2026-04-01",
      "EurPorPagina": 8.6e-06
    },
    {
      "Modelo": "cu.documentPagesBasic",
      "VigenteDesde": "2026-04-01",
      "EurPorPagina": 0.0008587
    },
    {
      "Modelo": "cu.documentPagesStandard",
      "VigenteDesde": "2026-04-01",
      "EurPorPagina": 0.0042933
    },
    {
      "Modelo": "CU_NS_1.5_0",
      "VigenteDesde": "2026-06-01",
      "EurContextualizacionPor1M": 0.9
    },
    {
      "Modelo": "CU_NS_1.6_0_GGAA",
      "VigenteDesde": "2026-07-17",
      "EurContextualizacionPor1M": 0.9
    }
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
