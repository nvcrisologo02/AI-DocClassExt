/* ============================================================================
   DocumentIA - Alta de tipologias con alcance solo de clasificacion (AB#101074)

   DESTINO : srbsqldevdocai.database.windows.net / DocumentIA
   Aplicado: DEV el 2026-10-21 (15 insertadas; relanzado: 15 omitidas).
             Ampliado a 19 el mismo dia (ACUE-12, COMU-18, ESIN-AI, FICH-25).
   PRE/PRO : no se ejecuta a mano. Llegan con el alta por clave natural de
             la etapa ConfigSeed del pipeline 832 (AB#101075), que toma DEV
             como origen. Hasta que exista, este script es el registro de
             lo dado de alta en DEV.

   TDN2 (19): ACTT-04, ACUE-12, CERA-04, CERJ-54, CERT-19, CNCV-05, CNCV-29,
              CNCV-70, COMU-18, COMU-66, COMU-84, COMU-87, COMU-88, DOCA-39,
              ESCR-27, ESIN-AH, ESIN-AI, FICH-25, LIPR-13
   LIPR-13 no existe en CatalogoTdn2: el paso 0.b la da de alta antes (TDN1
   LIPR, resuelto por codigo). Exige rellenar @NombreLipr13.

   ALCANCE
   - Solo clasificacion: extraction.enabled = false, promptConfig.enabled = false,
     assetResolver.enabled = false y SIN subida a GDC (gdc.skipUpload = true).
     La matricula AI-01-XXXX-NN se deja rellena para activar GDC mas adelante
     cambiando solo skipUpload.
   - Misma forma de ConfiguracionJson que las tipologias del alta de mayo
     (p. ej. cncv.04), incluidos los campos legacy de raiz.

   NOTAS
   - Nombre y gptDescripcion salen del CatalogoTdn2 DEL DESTINO, no se escriben
     a mano. Si un TDN2 no esta en el catalogo, se omite con aviso.
   - Codigo tecnico = TDN2 en minusculas con punto: CNCV-05 -> cncv.05.
   - IDEMPOTENTE: solo inserta si el Codigo no existe. Reejecutar no duplica.
   - Transaccional: o entran todas o ninguna.
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

-- ---------------------------------------------------------------------------
-- 0.a Datos de catalogo de LIPR-13
-- ---------------------------------------------------------------------------
DECLARE @NombreLipr13 nvarchar(400) = N'Obra: licencia segunda ocupación';
DECLARE @DescripcionLipr13 nvarchar(4000) = N'Documento administrativo de carácter municipal que tiene por objeto acreditar que la licencia fue concedida, y que se encuentra apta según las determinaciones urbanísticas, ambientales y de seguridad de su destino específico.';

DECLARE @Codigos TABLE (Tdn2 nvarchar(20) NOT NULL PRIMARY KEY);
INSERT INTO @Codigos (Tdn2) VALUES
    (N'ACTT-04'), (N'CERA-04'), (N'CERJ-54'), (N'CERT-19'), (N'CNCV-05'),
    (N'CNCV-29'), (N'CNCV-70'), (N'COMU-66'), (N'COMU-84'), (N'COMU-87'),
    (N'COMU-88'), (N'DOCA-39'), (N'ESCR-27'), (N'ESIN-AH'), (N'LIPR-13'),
    (N'ACUE-12'), (N'COMU-18'), (N'ESIN-AI'), (N'FICH-25');

IF NOT EXISTS (SELECT 1 FROM dbo.CatalogoTdn2 WHERE Codigo = N'LIPR-13')
BEGIN
    IF NULLIF(LTRIM(RTRIM(@NombreLipr13)), N'') IS NULL
        THROW 50003, 'ABORTADO: LIPR-13 no esta en CatalogoTdn2 y @NombreLipr13 esta vacio.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.CatalogoTdn1 WHERE Codigo = N'LIPR')
        THROW 50004, 'ABORTADO: no existe CatalogoTdn1 con Codigo LIPR en el destino.', 1;
END;

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
LEFT JOIN
(
    SELECT Codigo, Nombre, Descripcion, CodigoTdn1 FROM dbo.CatalogoTdn2
    UNION ALL
    -- LIPR-13 pendiente de alta en el catalogo: se usa el dato de 0.a
    SELECT N'LIPR-13', @NombreLipr13, @DescripcionLipr13, N'LIPR'
    WHERE NOT EXISTS (SELECT 1 FROM dbo.CatalogoTdn2 WHERE Codigo = N'LIPR-13')
) t2 ON t2.Codigo = c.Tdn2;

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

-- 0.b LIPR-13 en el catalogo, resolviendo Tdn1Id por codigo
IF NOT EXISTS (SELECT 1 FROM dbo.CatalogoTdn2 WHERE Codigo = N'LIPR-13')
BEGIN
    INSERT INTO dbo.CatalogoTdn2 ([Codigo], [Nombre], [Descripcion], [CodigoTdn1], [Tdn1Id])
    SELECT N'LIPR-13', @NombreLipr13, @DescripcionLipr13, N'LIPR', t1.Id
    FROM dbo.CatalogoTdn1 t1 WHERE t1.Codigo = N'LIPR';
    PRINT '[INSERTADA] CatalogoTdn2 LIPR-13';
END;

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
