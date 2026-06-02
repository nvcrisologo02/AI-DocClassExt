# Analisis de rendimiento en extracciones con Azure Content Understanding

Fecha: 2026-05-28  
Ambito: extracciones de notas simples ejecutadas desde el frontend batch  
Ventana principal analizada: desde 2026-05-27 14:45 UTC

> Documento histórico de línea base (pre-endurecimiento). Para operación actual y medidas implementadas consultar también `docs/plans/20_ANALISIS_CRISIS_CU_20260529_Y_PLAN_RESILIENCIA.md`.

## 1. Resumen ejecutivo

Durante la prueba de extraccion de notas simples se lanzaron aproximadamente 150 ejecuciones desde el frontend batch, usando colas de 10 documentos. Conforme avanzaba el lote, los tiempos de ejecucion aumentaban de forma progresiva.

La investigacion en Application Insights y Azure SQL apunta a que el cuello de botella no esta en el frontend, SQL, Durable Functions como orquestador, ni AssetResolver. El tiempo dominante se concentra en la actividad de extraccion, concretamente en la llamada a Azure Content Understanding mediante `ContentUnderstandingClient.AnalyzeBinary`.

No se ha encontrado una cuota visible de Azure que exponga el throughput, TPS o concurrencia real de Content Understanding. Las cuotas consultables muestran margen en numero de cuentas AI Services, pero no muestran limites especificos de `AnalyzeBinary` ni de Content Understanding.

La hipotesis mas consistente es saturacion o cola interna del servicio bajo concurrencia, posiblemente agravada por documentos repetidos y por errores transitorios `InternalServerError` observados en la ventana analizada.

## 2. Contexto de la prueba

La prueba se realizo desde el frontend batch de clasificacion/extraccion, configurando colas de 10 documentos.

Elementos relevantes de configuracion:

- Frontend batch: procesamiento paralelo limitado a un maximo de 10 colas.
- Azure Durable Functions: `maxConcurrentActivityFunctions = 10`.
- Actividad critica: `ExtraerActivity`.
- Proveedor de extraccion: Azure Content Understanding.
- Metodo observado como cuello de botella: `ContentUnderstandingClient.AnalyzeBinary`.
- Recurso Azure AI Services: `upe48-mm2avmdm-swedencentral`.
- Region del recurso: `swedencentral`.
- SKU: `S0`.
- Analyzer configurado: `CU_NS_1.4_2`.
- Processing location configurado: `geography`.
- Rango de entrada configurado: `InputRange` vacio, por lo que se procesa el documento completo.

## 3. Evidencias de Application Insights

La telemetria revisada muestra que la degradacion aparece en la fase de extraccion.

Hallazgos principales:

- `IngestDocument` mantiene tiempos bajos frente al total de proceso.
- El orquestador Durable no aparece como consumidor principal de tiempo.
- Las dependencias SQL no presentan latencias compatibles con el problema observado.
- AssetResolver tampoco explica el incremento progresivo de duraciones.
- La dependencia lenta es `ContentUnderstandingClient.AnalyzeBinary`.
- Se observaron errores `InternalServerError` asociados a Azure Content Understanding durante la ventana de carga.

En la ventana analizada, `ExtraerActivity` concentro los mayores tiempos, con latencias elevadas y alta variabilidad. El comportamiento es compatible con cola interna, throttling no expuesto o degradacion temporal del servicio externo.

## 4. Evidencias de Azure SQL

La revision de la base de datos `DocumentIA` confirmo las observaciones de Application Insights.

Datos relevantes:

- Se localizaron aproximadamente 160 ejecuciones persistidas en la ventana objetivo.
- Todas las ejecuciones consultadas terminaron con estado `OK`.
- La duracion de extraccion aumenta por ventanas temporales conforme avanza el lote.
- Las columnas persistidas de duracion permiten separar extraccion, clasificacion, validacion, integracion, persistencia, GDC y AssetResolver.
- El incremento se concentra en `DuracionExtraccionMs`.

Tambien se identifico un documento repetido:

- Documento: `153266_Nota Simple.pdf`.
- Apariciones: 14.
- Paginas: 38.
- Duraciones de extraccion observadas: desde aproximadamente 228 s hasta 538 s.

Este documento puede haber amplificado el problema por reenvio repetido, pero no lo explica por completo. Tambien se observaron documentos mas pequenos, de 5 a 7 paginas, con duraciones superiores a 230 s. Por tanto, el problema no parece depender exclusivamente del numero de paginas.

## 5. Revision de cuotas, limites y region

Se comprobo directamente el recurso usado para Content Understanding.

Resultado de la cuenta:

```text
name: upe48-mm2avmdm-swedencentral
kind: AIServices
sku: S0
location: swedencentral
provisioningState: Succeeded
publicNetworkAccess: Enabled
endpoint ARM: https://upe48-mm2avmdm-swedencentral.cognitiveservices.azure.com/
endpoint usado por la aplicacion: https://upe48-mm2avmdm-swedencentral.services.ai.azure.com/
```

Recursos relacionados encontrados:

```text
upe48-mm2avmdm-swedencentral                                  AIServices  S0  swedencentral
upe48-mm2avmdm-swedencentral/upe48-mm2avmdm-swedence-project  AIServices      swedencentral
```

Cuentas Cognitive Services relevantes:

```text
srbdiprodocai                 FormRecognizer  S0  westeurope
srboaiprodocai                OpenAI          S0  westeurope
upe48-mm2avmdm-swedencentral  AIServices      S0  swedencentral
```

Uso visible de la cuenta concreta:

```text
AccountUsageRows=0
```

El endpoint regional de usos de `Microsoft.CognitiveServices` en `swedencentral` devuelve cuotas genericas y de modelos, pero no expone cuotas especificas de Content Understanding.

Cuotas visibles relevantes:

```text
AIServices.S0.AccountCount  1 / 100
AccountCount                1 / 200
```

SKU disponible para AI Services en la region:

```text
S0 Standard
```

No aparecen limites visibles para:

- `ContentUnderstanding.*`
- `AnalyzeBinary`
- `DocumentUnderstanding`
- concurrencia de operaciones
- TPS de Content Understanding
- operaciones pendientes de Content Understanding

Conclusion de esta revision: no hay agotamiento de cuota visible en Azure Resource Manager. Sin embargo, Azure no expone por CLI/ARM el limite fino que interesa para esta incidencia: throughput y concurrencia real de Content Understanding.

## 6. Interpretacion tecnica

La configuracion actual permite que el batch lance hasta 10 documentos en paralelo y que Durable Functions ejecute hasta 10 actividades concurrentes. En la practica, esto puede traducirse en hasta 10 llamadas simultaneas a `AnalyzeBinary`.

Aunque esa concurrencia sea valida para la infraestructura propia, no garantiza que Content Understanding procese ese volumen con latencia estable. Si el servicio aplica limites internos no visibles, colas regionales o throttling no expuesto como `429`, el efecto esperado puede ser:

- incremento progresivo de la latencia;
- alta variabilidad entre documentos;
- errores `InternalServerError`;
- actividades de extraccion ocupadas durante varios minutos;
- degradacion del lote completo al mantenerse la presion sobre el servicio.

El uso de `WaitUntil.Completed` hace que la actividad permanezca ocupada hasta que Content Understanding termina el analisis. Esto simplifica el flujo, pero tambien hace que los workers de actividad queden retenidos durante toda la duracion de la operacion externa.

## 7. Opciones de actuacion

### 7.1 Medidas en codigo

Se recomienda introducir un control especifico para Content Understanding, separado de la concurrencia general de Durable Functions.

Acciones propuestas:

- Crear un limiter especifico para llamadas a Content Understanding.
- Empezar con una concurrencia efectiva de 2 o 3 llamadas simultaneas y medir.
- Anadir backoff exponencial con jitter para errores transitorios.
- Tratar como transitorios, al menos, `429`, `500`, `502`, `503`, `504`, timeouts e `InternalServerError`.
- Registrar telemetria de intento, espera por limiter, codigo de error, duracion y numero de reintentos.
- Evitar reintentos inmediatos y sincronizados que aumenten la presion sobre el servicio.

Punto de aplicacion recomendado:

- `AzureContentUnderstandingProvider`, alrededor de la llamada a `AnalyzeBinaryAsync`.

No se recomienda introducir esperas no deterministas dentro del orquestador Durable. El backoff debe situarse en actividad/proveedor o en politicas de retry controladas.

### 7.2 Medidas de configuracion funcional

Revisar si es viable limitar el rango de paginas enviado a Content Understanding.

La configuracion actual tiene `InputRange` vacio, por lo que se procesa el documento completo. Si para notas simples basta con un subconjunto de paginas, se podria probar un rango limitado para reducir coste, latencia y presion sobre el servicio.

Esta medida debe validarse funcionalmente, porque puede afectar a la completitud de la extraccion.

### 7.3 Medidas operativas

Acciones recomendadas:

- Ejecutar pruebas controladas con concurrencia 1, 2, 3, 5 y 10.
- Medir media, p50, p90, p95, maximo y tasa de error.
- Separar en telemetria el tiempo de descarga/binario, envio a CU, espera de CU y parseo del resultado.
- Detectar duplicados antes de enviar a extraccion cuando sea posible.
- Evitar reenviar multiples veces el mismo documento si ya existe una ejecucion equivalente reciente.

## 8. Informacion recomendada para ticket a Microsoft

Si se escala a Microsoft, conviene aportar una evidencia concreta y acotada:

- Servicio: Azure AI Services / Content Understanding.
- Recurso: `upe48-mm2avmdm-swedencentral`.
- Region: `swedencentral`.
- SKU: `S0`.
- Analyzer: `CU_NS_1.4_2`.
- Ventana: 2026-05-27 desde 14:45 UTC, especialmente 15:15-15:45 UTC.
- Volumen: aproximadamente 150/160 ejecuciones.
- Concurrencia aplicada: batch con colas de 10 y Durable `maxConcurrentActivityFunctions = 10`.
- Metodo afectado: `ContentUnderstandingClient.AnalyzeBinary`.
- Sintomas: aumento progresivo de latencia, duraciones superiores a 200 s y maximos cercanos a 538 s.
- Errores observados: `InternalServerError`.
- Cuotas visibles: `AIServices.S0.AccountCount = 1/100`, `AccountCount = 1/200`.
- Limitacion de diagnostico: no aparece cuota visible de throughput/concurrencia para Content Understanding.
- Evidencia adicional: `az cognitiveservices account list-usage` sobre la cuenta devuelve 0 filas.

Preguntas concretas para Microsoft:

- Cual es el limite real de concurrencia o throughput para Content Understanding en `swedencentral` y SKU `S0`.
- Si existen metricas internas de cola, throttling o saturacion para `AnalyzeBinary`.
- Si los `InternalServerError` de la ventana corresponden a saturacion del servicio.
- Que patron oficial de retry/backoff recomiendan para cargas batch concurrentes.
- Si existe posibilidad de aumento de capacidad, cambio de region o configuracion adicional.

## 9. Conclusion

La evidencia disponible indica que el principal cuello de botella esta en Azure Content Understanding, concretamente en `ContentUnderstandingClient.AnalyzeBinary`, bajo una carga concurrente de hasta 10 extracciones simultaneas.

No hay indicios de que SQL, AssetResolver o el frontend sean la causa principal de la degradacion. Tampoco se observa agotamiento de cuotas visibles de Azure AI Services. El limite relevante parece estar en capacidad interna, cola, throughput o concurrencia de Content Understanding, que no se expone en las consultas de cuotas disponibles.

La accion mas practica desde el lado de la aplicacion es implementar control explicito de concurrencia y backoff adaptativo para Content Understanding, acompanado de pruebas controladas para encontrar el punto de equilibrio entre throughput y estabilidad. En paralelo, conviene abrir ticket a Microsoft con las evidencias anteriores para confirmar limites reales y opciones de capacidad del servicio.

## 10. Estado actual implementado (actualización)

Tras este análisis base, el sistema se endureció con cambios ya aplicados en backend y configuración:

- Durable Functions: `maxConcurrentActivityFunctions = 4` y `maxConcurrentOrchestratorFunctions = 4`.
- CU limiter: `Extraction:AzureContentUnderstanding:MaxConcurrentCalls = 4`.
- Timeout duro por intento CU: `Extraction:AzureContentUnderstanding:HardTimeoutSeconds = 90`.
- Circuit breaker por `ModelKey` habilitado: `EnableCircuitBreaker = true`, `CircuitBreakerFailureThreshold = 5`, `CircuitBreakerOpenSeconds = 45`.
- Telemetría ampliada para resiliencia: `CU.HardTimeout`, `CU.CircuitOpen`, `CU.CircuitClosed`, `CU.CircuitFailover`, `CU.CircuitRejected`.

Estos valores sustituyen las recomendaciones iniciales de este documento cuando se use como referencia operativa.