/* ============================================================================
   DocumentIA - Alta de la tipologia fiav.06 con alcance solo de clasificacion
   (AB#100780, reentreno v2 del clasificador por embeddings)

   DESTINO : srbsqldevdocai.database.windows.net / DocumentIA
   PRE/PRO : no se ejecuta a mano. Llegan con el alta por clave natural de
             la etapa ConfigSeed del pipeline 832 (AB#101075), que toma DEV
             como origen.

   TDN2 (1): FIAV-06
   FIAV y FIAV-06 verificados en CatalogoTdn1/CatalogoTdn2 de DEV el 2026-11-03.

   ALCANCE
   - Solo clasificacion: extraction.enabled = false, promptConfig.enabled = false,
     assetResolver.enabled = false y SIN subida a GDC (gdc.skipUpload = true).
     La matricula AI-01-XXXX-NN se deja rellena para activar GDC mas adelante
     cambiando solo skipUpload.
   - Misma forma de ConfiguracionJson que seed-tipologias-ab101074.sql, incluidos
     los campos legacy de raiz.

   NOTAS
   - Nombre y gptDescripcion salen del CatalogoTdn2 DEL DESTINO, no se escriben
     a mano. Si el TDN2 no esta en el catalogo, se omite con aviso.
   - Codigo tecnico = TDN2 en minusculas con punto: FIAV-06 -> fiav.06.
   - IDEMPOTENTE: solo inserta si el Codigo no existe. Reejecutar no duplica.
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
INSERT INTO @Codigos (Tdn2) VALUES (N'FIAV-06');

DECLARE @Ahora datetime2(7) = SYSUTCDATETIME();
DECLARE @Autor nvarchar(200) = ORIGINAL_LOGIN();

-- ---------------------------------------------------------------------------
-- 1. Carga resuelta contra el catalogo del destino
-- ---------------------------------------------------------------------------
DECLARE @Carga TABLE
(
    Tdn2 nvarchar(20) NOT NULL,
    Codigo nvarchar(100) NULL,
    Nombre nvarchar(200) NULL,
    ConfigJson nvarchar(max) NULL,
    Accion nvarchar(40) NOT NULL
);

INSERT INTO @Carga (Tdn2, Codigo, Nombre, ConfigJson, Accion)
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
    END
FROM @Codigos c
CROSS APPLY (SELECT
    Codigo    = LOWER(REPLACE(c.Tdn2, N'-', N'.')),
    Matricula = N'AI-01-' + c.Tdn2,
    Subtipo   = REPLACE(c.Tdn2, N'-', N'')) x
LEFT JOIN dbo.CatalogoTdn2 t2 ON t2.Codigo = c.Tdn2;

IF EXISTS (SELECT 1 FROM @Carga WHERE Accion = N'INSERTAR' AND ISJSON(ConfigJson) = 0)
    THROW 50002, 'ABORTADO: algun ConfiguracionJson generado no es JSON valido.', 1;

-- ---------------------------------------------------------------------------
-- 2. Simulacion o alta
-- ---------------------------------------------------------------------------
IF @SoloSimular = 1
BEGIN
    SELECT Tdn2, Codigo, Nombre, Accion,
           JsonValido = ISJSON(ConfigJson),
           Tdn2EnJson = JSON_VALUE(ConfigJson, '$.classification.tdn2'),
           SkipGdc    = JSON_VALUE(ConfigJson, '$.gdc.skipUpload')
    FROM @Carga ORDER BY Tdn2;
    PRINT 'SIMULACION: no se ha escrito nada. Pon @SoloSimular = 0 para ejecutar.';
    RETURN;
END;

DECLARE @Insertadas TABLE (Codigo nvarchar(100) NOT NULL);

BEGIN TRANSACTION;

INSERT INTO dbo.Tipologias
    ([Codigo], [Nombre], [Version], [Activa], [ConfiguracionJson], [FechaCreacion], [FechaActualizacion], [CreadoPor], [Estado], [PublicadaEn], [PublicadaPor], [VersionPublicada])
OUTPUT inserted.Codigo INTO @Insertadas (Codigo)
SELECT Codigo, Nombre, N'1.0', 1, ConfigJson, @Ahora, @Ahora, @Autor, 1, @Ahora, @Autor, N'1.0'
FROM @Carga c
WHERE c.Accion = N'INSERTAR'
  AND NOT EXISTS (SELECT 1 FROM dbo.Tipologias t WHERE t.Codigo = c.Codigo);

COMMIT TRANSACTION;

SELECT c.Tdn2, c.Codigo, Resultado = CASE WHEN i.Codigo IS NOT NULL THEN N'INSERTADA' ELSE c.Accion END
FROM @Carga c LEFT JOIN @Insertadas i ON i.Codigo = c.Codigo
ORDER BY c.Tdn2;
