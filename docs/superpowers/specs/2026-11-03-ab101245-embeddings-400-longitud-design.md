# Clasificador por embeddings: reintento con la mitad del texto ante el 400 de longitud

- **Fecha**: 2026-11-03. **Tamaño**: cambio pequeño (diseño corto).
- **Work item**: Bug AB#101245 "Clasificador por embeddings: HTTP 400 invalid_request del endpoint con textos largos" (Committed), padre AB#99948, relacionado con AB#100779 y AB#100780.
- **Antecedentes**: spec `2026-10-01-clasificador-embeddings-hibrido-design.md`, ADR-002, `embeddings.py` del repo DocumentIA.Batch (`eval/clasificador-embeddings/`, funciones `_llamar` y `_pedir_uno`).

## Causa confirmada

En la sombra de DEV (02/10 a 22/10, modelo v1) 15 ejecuciones derivaron al GPT con el error persistido `ClientResultException: HTTP 400 (invalid_request_error: ) Invalid 'input[0]': maximum input length is 8192 tokens.` El texto llega al servicio recortado por caracteres (24.000 en el orquestador, `MaxChars` de la configuración en el proveedor) y en documentos OCR con muchas cifras y símbolos esos caracteres superan los 8.192 tokens que admite el modelo por entrada. El mensaje se persiste completo (115 caracteres); la consola de PowerShell lo recortaba en el análisis previo.

## Intención

Que un documento largo no pierda la clasificación por embeddings por un recorte que el servicio rechaza, aplicando el mismo tratamiento que usó el entrenamiento de v1 y v2 en el Batch: ante ese 400, reintentar con la mitad del texto hasta que entre.

## Objetivos

1. Ante `ClientResultException` con estado 400 y mensaje de longitud máxima (`maximum input length` o `maximum context length`), reintentar con la primera mitad del texto, con tope de tres recortes. Con `MaxChars = 24.000` el segundo recorte deja 6.000 caracteres, que nunca superan 8.192 tokens.
2. Cualquier otro error (400 de otro tipo, 429, 5xx, timeout) se relanza sin reintentar: lo cuenta el ejecutor de resiliencia como hasta ahora.
3. Tests unitarios del nuevo comportamiento y verificación en DEV con un documento de más de 8.192 tokens.

## No objetivos

- Recortar por tokens con un tokenizador en el worker (añadiría dos paquetes NuGet y un corte distinto al del entrenamiento).
- Cambiar `MaxCharsPorDefecto`, el orquestador, el decisor o el contrato de salida.
- Registrar en el contrato que hubo recorte: se deja en el log del worker.

## Cómo se hace

- Clase nueva `EmbeddingsClienteConRecorte : IEmbeddingsCliente` en `src/backend/DocumentIA.Functions/Services/Classification/`, decorador del cliente real. Constructor: cliente interno, `ILogger<EmbeddingsClienteConRecorte>` y tope de recortes (3 por defecto). `GenerarAsync` llama al interno; si captura el 400 de longitud y quedan recortes, parte el texto por la mitad (`texto[..(texto.Length / 2)]`), escribe un warning con las longitudes y repite; agotado el tope, relanza la última excepción. El consumo que se devuelve es el de la llamada que entra; las llamadas rechazadas no devuelven `usage`.
- `AzureOpenAIEmbeddingsClienteFactory` recibe `ILoggerFactory` por DI y envuelve el cliente que construye. `Program.cs` no cambia; `EmbeddingsClasificarProvider` tampoco.
- Detección del mensaje en un método estático interno (`EsLongitudExcedida`) para probarlo aparte; comparación sin distinguir mayúsculas.
- Tests en `DocumentIA.Tests.Unit/Services/Classification/EmbeddingsClienteConRecorteTests.cs` con Moq sobre `IEmbeddingsCliente` y `PipelineResponse` (estado 400): entra a la primera sin recorte; entra tras un recorte y el texto de la segunda llamada es la primera mitad; tope agotado relanza; 400 con otro mensaje relanza sin segunda llamada; 429 relanza sin segunda llamada.
- Rama `bugfix/101245-embeddings-400-longitud` desde develop; un commit `fix(embeddings)` con tests e implementación y, si hace falta, otro de documentación. Sin PR; integración en develop con sí.

## Verificación

- `dotnet build src/backend/DocumentIA.sln` sin warnings nuevos; `dotnet test src/backend/DocumentIA.Tests.Unit`; `dotnet format --verify-no-changes`.
- En DEV, tras desplegar develop (pipeline con sí): reprocesar uno de los 15 documentos si su fichero sigue en el blob o, si el cron lo ha borrado, un documento del corpus del share con texto OCR largo. Criterio: bloque `Embeddings` sin `Error`, con `Decision` y `Tipologia` del modelo, y un warning de recorte en el log del worker.

## Riesgos

- Una llamada extra (unos cientos de milisegundos) solo en los documentos afectados, dentro del `TimeoutSeconds` del proveedor.
- Si en el futuro `MaxChars` sube mucho, tres recortes pueden no bastar; el error se persiste igual que hoy.
