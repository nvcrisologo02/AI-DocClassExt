/*  AB#100258 — sp_ObtenerDocumentoEjecucionesPorIdActivo con @IncluirReutilizadas.

    Desde AB#100258, DocumentoEjecuciones contiene tambien filas que registran
    peticiones servidas por deduplicacion: no reprocesaron el documento y por eso
    tienen ContratoSalidaCompletoJson, DatosOriginalesJson y DatosFinalesJson a NULL.

    Este procedimiento lo consume un sistema externo (no hay ningun llamante en el
    repositorio), asi que esas filas NO pueden aparecer sin avisar: romperian su
    resultset con contratos vacios. Se excluyen por defecto y solo entran si se pide
    explicitamente @IncluirReutilizadas = 1.

    COMPATIBILIDAD: las FILAS devueltas por defecto son exactamente las de antes de la
    migracion. Lo que si cambia son las COLUMNAS: se anaden ReutilizadaPorDuplicado y
    EjecucionOriginalId al final del SELECT, para que quien pida las reutilizaciones
    pueda distinguirlas sin deducirlo de un contrato vacio. Van al final y no desplazan
    a ninguna existente, de modo que un consumidor que mapee por nombre o por posicion
    no se ve afectado; solo lo notaria uno que rechace columnas desconocidas.

    El resto del cuerpo es identico al de la migracion 20260902093812.

    Uso:
      sqlcmd -S <srv> -d DocumentIA -G -i sp-obtener-ejecuciones-por-idactivo-reutilizaciones.sql

    Verificacion (con un IdActivo real):
      EXEC sp_ObtenerDocumentoEjecucionesPorIdActivo @IdActivo = '<idactivo>';
      -- ninguna fila debe traer ContratoSalidaCompletoJson NULL por reutilizacion
      EXEC sp_ObtenerDocumentoEjecucionesPorIdActivo @IdActivo = '<idactivo>', @IncluirReutilizadas = 1;
      -- ahora si aparecen, con contrato NULL y ReutilizadaPorDuplicado = 1
*/

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerDocumentoEjecucionesPorIdActivo
    @IdActivo NVARCHAR(100),
    @IncluirReutilizadas BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdActivoNorm NVARCHAR(100) = UPPER(LTRIM(RTRIM(@IdActivo)));

    ;WITH DocsObjetivo AS (
        SELECT DISTINCT de.DocumentoId
        FROM dbo.DocumentoEjecuciones de
        WHERE de.IdActivo = @IdActivoNorm
    )
    SELECT
        d.Id                                  AS DocumentoId,
        d.Guid,
        d.NombreArchivo,
        d.IdActivo                            AS IdActivoDocumento,
        d.Estado,
        d.Tipologia                           AS TipologiaDocumento,
        d.FechaCreacion,
        de.Id                                 AS EjecucionId,
        de.EjecucionGuid,
        de.FechaEjecucion,
        de.Tipologia                          AS TipologiaEjecucion,
        de.EstadoFinal,
        de.ConfianzaGlobal                    AS ConfianzaGlobalEjecucion,
        de.ModeloClasificacion                AS ModeloClasificacionEjecucion,
        de.ConfianzaClasificacion             AS ConfianzaClasificacionEjecucion,
        de.UseFallbackLLM,
        de.DuracionTotalMs,
        -- AB#100168: DatosOriginalesJson y DatosFinalesJson dejaron de grabarse (AB#100166).
        -- Se mantienen los mismos nombres de columna en el resultset, extrayendo del contrato
        -- cuando la columna historica no existe.
        COALESCE(de.DatosOriginalesJson,
                 JSON_QUERY(de.ContratoSalidaCompletoJson, '$.DetalleEjecucion.Integracion.DatosOriginales')) AS DatosOriginalesJson,
        COALESCE(de.DatosFinalesJson,
                 JSON_QUERY(de.ContratoSalidaCompletoJson, '$.DatosExtraidos'))                               AS DatosFinalesJson,
        de.ContratoSalidaCompletoJson,
        -- AB#100258: solo llega informado cuando se piden las reutilizaciones; asi el
        -- consumidor puede distinguirlas sin tener que deducirlo del contrato vacio.
        de.ReutilizadaPorDuplicado,
        de.EjecucionOriginalId
    FROM DocsObjetivo x
    JOIN dbo.Documentos d
        ON d.Id = x.DocumentoId
    JOIN dbo.DocumentoEjecuciones de
        ON de.DocumentoId = d.Id
    WHERE (@IncluirReutilizadas = 1 OR de.ReutilizadaPorDuplicado = 0)
    ORDER BY d.Id, de.FechaEjecucion DESC, de.Id DESC;
END;
GO
