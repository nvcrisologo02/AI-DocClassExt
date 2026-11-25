/* ============================================================================
   DocumentIA - Alta de tipologias con alcance solo de clasificacion
   (AB#101564, reentreno v5 del clasificador por embeddings, Lote4 de GDC)

   DESTINO : srbsqldevdocai.database.windows.net / DocumentIA
   PRE/PRO : no se ejecuta a mano. Llegan con el alta por clave natural de
             la etapa ConfigSeed del pipeline 832 (AB#101075), que toma DEV
             como origen. La fila nueva de CatalogoTdn2 (CERJ-71) NO viaja por
             ConfigSeed: se anota como pendiente para PRE/PRO.

   TDN2 (4): DOCT-15, ESIN-02, ESIN-45 (ya en CatalogoTdn2 de DEV, verificados
             el 2026-11-25) y CERJ-71, que es nuevo en el glosario TDN2 del
             Departamento de Gestion Documental (captura del 2026-11-25) y se
             inserta antes en CatalogoTdn2 bajo CERJ (Tdn1Id resuelto por codigo).

   ALCANCE
   - Solo clasificacion: extraction.enabled = false, promptConfig.enabled = false,
     assetResolver.enabled = false y SIN subida a GDC (gdc.skipUpload = true).
     La matricula AI-01-XXXX-NN se deja rellena para activar GDC mas adelante
     cambiando solo skipUpload.
   - Misma forma de ConfiguracionJson que seed-tipologia-fiav06-ab100780.sql,
     incluidos los campos legacy de raiz.

   NOTAS
   - Nombre y gptDescripcion salen del CatalogoTdn2 DEL DESTINO (o de la fila
     nueva de @CatalogoNuevo para CERJ-71), no se escriben a mano en el JSON.
   - Codigo tecnico = TDN2 en minusculas con punto: DOCT-15 -> doct.15.
   - IDEMPOTENTE: solo inserta lo que no existe (catalogo y tipologias).
   - Transaccional: o entra todo o nada.
   - @SoloSimular = 1 (por defecto) no escribe: devuelve lo que insertaria y
     valida el JSON. Poner 0 para ejecutar el alta.
============================================================================ */

SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @SoloSimular bit = 1;
DECLARE @ServidorEsperado sysname = N'srbsqldevdocai';

-- ---------------------------------------------------------------------------
-- 0. Guardarrail: aborta si la conexion no apunta al destino previsto
-- ---------------------------------------------------------------------------
IF @@SERVERNAME <> @ServidorEsperado OR DB_NAME() <> N'DocumentIA'
    THROW 50000, 'ABORTADO: la conexion no apunta al servidor/BD esperados (ver @ServidorEsperado).', 1;

DECLARE @Codigos TABLE (Tdn2 nvarchar(20) NOT NULL PRIMARY KEY);
INSERT INTO @Codigos (Tdn2) VALUES (N'DOCT-15'), (N'ESIN-02'), (N'ESIN-45'), (N'CERJ-71');

-- Filas nuevas del glosario TDN2 que faltan en CatalogoTdn2 del destino
DECLARE @CatalogoNuevo TABLE
(
    Tdn2 nvarchar(15) NOT NULL PRIMARY KEY,
    Nombre nvarchar(200) NOT NULL,
    Descripcion nvarchar(2000) NOT NULL,
    CodigoTdn1 nvarchar(10) NOT NULL
);
INSERT INTO @CatalogoNuevo (Tdn2, Nombre, Descripcion, CodigoTdn1) VALUES
(N'CERJ-71',
 N'Certificado de Índice de Referencia de Precios de Alquiler',
 N'Certificado informativo emitido a partir del sistema estatal de referencia de precios del alquiler del Ministerio de Vivienda. Su finalidad es indicar el rango orientativo o de referencia de renta mensual aplicable a una vivienda concreta, teniendo en cuenta sus características y ubicación. Incluye el concepto de "Zona Tensionada: SI/NO".',
 N'CERJ');

IF EXISTS (SELECT 1 FROM @CatalogoNuevo n LEFT JOIN dbo.CatalogoTdn1 c1 ON c1.Codigo = n.CodigoTdn1 WHERE c1.Id IS NULL)
    THROW 50001, 'ABORTADO: algun CodigoTdn1 de @CatalogoNuevo no existe en CatalogoTdn1.', 1;

DECLARE @Ahora datetime2(7) = SYSUTCDATETIME();
DECLARE @Autor nvarchar(200) = ORIGINAL_LOGIN();

-- ---------------------------------------------------------------------------
-- 1. Carga resuelta contra el catalogo del destino (mas las filas nuevas)
-- ---------------------------------------------------------------------------
DECLARE @Carga TABLE
(
    Tdn2 nvarchar(20) NOT NULL,
    Codigo nvarchar(100) NULL,
    Nombre nvarchar(200) NULL,
    ConfigJson nvarchar(max) NULL,
    Accion nvarchar(40) NOT NULL,
    AccionCatalogo nvarchar(40) NOT NULL
);

INSERT INTO @Carga (Tdn2, Codigo, Nombre, ConfigJson, Accion, AccionCatalogo)
SELECT
    c.Tdn2,
    x.Codigo,
    LEFT(t2.Nombre, 200),
    CASE WHEN t2.Codigo IS NULL THEN NULL ELSE CONCAT(
        N'{"tipologiaId":"', x.Codigo, N'"',
        N',"tipologiaNombre":"', STRING_ESCAPE(t2.Nombre, 'json'), N'"',
        N',"version":"1.0","isDefault":false',
        N',"tipologiaMGDCMatricula":"', x.Matricula, N'"',
        N',"gdcTipoDocumento":"', t2.CodigoTdn1, N'"',
        N',"gdcSubtipoDocumento":"', x.Subtipo, N'"',
        N',"gdcSerie":"AI01","skipGDCUpload":true',
        N',"gdc":{"skipUpload":true,"matricula":"', x.Matricula, N'","tipoDocumento":"', t2.CodigoTdn1, N'","subtipoDocumento":"', x.Subtipo, N'","serie":"AI01"}',
        N',"gptDescripcion":"', STRING_ESCAPE(t2.Descripcion, 'json'), N'"',
        N',"classification":{"gptDescripcion":"', STRING_ESCAPE(t2.Descripcion, 'json'), N'","tdn1":"', t2.CodigoTdn1, N'","tdn2":"', c.Tdn2, N'","enableRules":true}',
        N',"classificationConfig":{"priority":100,"allowAsFallback":false,"pagesToInspect":3,"minimumSignalScore":0.15,"minimumMarginOverSecond":0.10}',
        N',"extraction":{"enabled":false,"provider":"azure-content-understanding","modelKey":"', x.Codigo, N'.1_0.azure-cu","autoMapUnmappedFields":true,"fieldMappings":[]}',
        N',"fields":[]',
        N',"confidenceConfig":{"clasifUmbralFallback":0.85,"extracUmbralFallback":0.85,"extracUmbralFallbackCompletitud":0.85,"extracUmbralFallbackConfianza":0.85,"extracWeightCampos":0.50,"extracWeightRequeridos":0.25,"extracWeightWarnings":0.15,"umbralOK":0.85,"umbralRevision":0.70}',
        N',"promptConfig":{"enabled":false,"modelKey":"default.gpt4o-mini","systemPrompt":"Eres un analista documental. Responde en espanol neutro, sin inventar datos y con formato estructurado.","userPromptTemplate":"Genera un resumen ejecutivo del documento usando esta estructura exacta:\n1) Objetivo del documento\n2) Datos clave\n3) Riesgos o alertas\n4) Acciones recomendadas\n\nContenido:\n{contenido}","maxTokens":1600,"temperature":0,"contentMode":"markdown"}',
        N',"assetResolver":{"enabled":false,"camposSolicitados":["#ALL#"],"mapeoIdufir":["IDUFIR_CRU"],"mapeoReferenciaCatastral":["ReferenciaCatastral"]}}'
    ) END,
    CASE
        WHEN t2.Codigo IS NULL THEN N'OMITIDA: no esta en CatalogoTdn2'
        WHEN NULLIF(LTRIM(RTRIM(t2.Descripcion)), N'') IS NULL THEN N'OMITIDA: catalogo sin descripcion'
        WHEN EXISTS (SELECT 1 FROM dbo.Tipologias t WHERE t.Codigo = x.Codigo) THEN N'OMITIDA: el codigo ya existe'
        ELSE N'INSERTAR'
    END,
    CASE
        WHEN t2.Codigo IS NULL THEN N'-'
        WHEN t2.Origen = N'nuevo' THEN N'INSERTAR EN CATALOGO'
        ELSE N'ya en catalogo'
    END
FROM @Codigos c
CROSS APPLY (SELECT
    Codigo    = LOWER(REPLACE(c.Tdn2, N'-', N'.')),
    Matricula = N'AI-01-' + c.Tdn2,
    Subtipo   = REPLACE(c.Tdn2, N'-', N'')) x
LEFT JOIN (
    SELECT Codigo, Nombre, Descripcion, CodigoTdn1, Origen = N'existente' FROM dbo.CatalogoTdn2
    UNION ALL
    SELECT n.Tdn2, n.Nombre, n.Descripcion, n.CodigoTdn1, N'nuevo'
    FROM @CatalogoNuevo n
    WHERE NOT EXISTS (SELECT 1 FROM dbo.CatalogoTdn2 e WHERE e.Codigo = n.Tdn2)
) t2 ON t2.Codigo = c.Tdn2;

IF EXISTS (SELECT 1 FROM @Carga WHERE Accion = N'INSERTAR' AND ISJSON(ConfigJson) = 0)
    THROW 50002, 'ABORTADO: algun ConfiguracionJson generado no es JSON valido.', 1;

-- ---------------------------------------------------------------------------
-- 2. Simulacion o alta
-- ---------------------------------------------------------------------------
IF @SoloSimular = 1
BEGIN
    SELECT Tdn2, Codigo, Nombre, AccionCatalogo, Accion,
           JsonValido = ISJSON(ConfigJson),
           Tdn1EnJson = JSON_VALUE(ConfigJson, '$.classification.tdn1'),
           Tdn2EnJson = JSON_VALUE(ConfigJson, '$.classification.tdn2'),
           SkipGdc    = JSON_VALUE(ConfigJson, '$.gdc.skipUpload')
    FROM @Carga ORDER BY Tdn2;
    PRINT 'SIMULACION: no se ha escrito nada. Pon @SoloSimular = 0 para ejecutar.';
    RETURN;
END;

DECLARE @Insertadas TABLE (Codigo nvarchar(100) NOT NULL);
DECLARE @CatalogoInsertado TABLE (Codigo nvarchar(15) NOT NULL);

BEGIN TRANSACTION;

INSERT INTO dbo.CatalogoTdn2 (Codigo, Nombre, Descripcion, CodigoTdn1, Tdn1Id)
OUTPUT inserted.Codigo INTO @CatalogoInsertado (Codigo)
SELECT n.Tdn2, n.Nombre, n.Descripcion, n.CodigoTdn1, c1.Id
FROM @CatalogoNuevo n
JOIN dbo.CatalogoTdn1 c1 ON c1.Codigo = n.CodigoTdn1
WHERE NOT EXISTS (SELECT 1 FROM dbo.CatalogoTdn2 e WHERE e.Codigo = n.Tdn2);

INSERT INTO dbo.Tipologias
    ([Codigo], [Nombre], [Version], [Activa], [ConfiguracionJson], [FechaCreacion], [FechaActualizacion], [CreadoPor], [Estado], [PublicadaEn], [PublicadaPor], [VersionPublicada])
OUTPUT inserted.Codigo INTO @Insertadas (Codigo)
SELECT Codigo, Nombre, N'1.0', 1, ConfigJson, @Ahora, @Ahora, @Autor, 1, @Ahora, @Autor, N'1.0'
FROM @Carga c
WHERE c.Accion = N'INSERTAR'
  AND NOT EXISTS (SELECT 1 FROM dbo.Tipologias t WHERE t.Codigo = c.Codigo);

COMMIT TRANSACTION;

SELECT c.Tdn2, c.Codigo,
       Catalogo  = CASE WHEN k.Codigo IS NOT NULL THEN N'INSERTADO EN CATALOGO' ELSE c.AccionCatalogo END,
       Resultado = CASE WHEN i.Codigo IS NOT NULL THEN N'INSERTADA' ELSE c.Accion END
FROM @Carga c
LEFT JOIN @Insertadas i ON i.Codigo = c.Codigo
LEFT JOIN @CatalogoInsertado k ON k.Codigo = c.Tdn2
ORDER BY c.Tdn2;
