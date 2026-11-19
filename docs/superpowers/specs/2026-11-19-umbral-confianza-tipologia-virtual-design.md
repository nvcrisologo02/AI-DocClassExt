# Umbral de confianza de clasificación en la tipología virtual

Fecha: 2026-11-19 · Línea: core · Padre: AB#99934 (migración de la clasificación a gpt-5.6-luna) · Task: AB#101435 · Tamaño: cambio pequeño (diseño corto) · Estado: aprobado por el usuario el 2026-11-19

## Intención

Antes del cutover a luna (AB#99939) se revisa el umbral de confianza 0,6. El criterio del usuario es que **no se cuelen clasificaciones malas como OK**: pesa más que el volumen que acaba en revisión.

## Situación actual (medida, golden AB#101433, runs `eval/runs/20261119-103903-*` del Batch)

El umbral no es un 0,6 global. `DocumentProcessOrchestrator` lo resuelve así:

- **Tipología de catálogo** (`DocumentProcessOrchestrator.cs`, comprobación tras resolver la tipología): umbral de la petición → `ConfidenceConfig.ClasifUmbralFallback` de la tipología → `UmbralFallbackAplicado` del proveedor → 0,6. En DEV, 223 de las 233 tipologías activas tienen 0,85 y 10 no tienen `ConfidenceConfig`. Por debajo del umbral, sale `BAJA_CONFIANZA_CLASIFICACION` **sin persistir**.
- **Tipología virtual** (`ClasificacionParcial` con `esVirtual`: familia sin código de catálogo, Desconocido, `tdn1_extraido_de_propuesta`, `global_fallback_final`, `global_fallback_baja_confianza` y Phase 2 sin TDN2 parseable): **no se aplica ningún umbral**. Persiste y devuelve `Estado = "OK"`.
- El umbral del proveedor sale de la petición o, si no la trae, del `FallbackThreshold` del modelo de fallback. En DEV es 0,6 (`classification.gpt4o-mini-fallback`, Id 1003). En PRE y PRO **no se ha leído**.

Errores de TDN1 que salen como OK:

| | Hoy | Con 0,6 también en la virtual (estimado a partir del CSV) |
|---|---|---|
| Control (gpt-4o-mini) | 195 de 459 (42,5%), 8 en baja | 189 de 451 (41,9%), 16 en baja |
| Luna | 131 de 419 (31,3%), 48 en baja | 118 de 401 (29,4%), 66 en baja (25 de ellos con el TDN1 bien) |

Subir el umbral apenas cambia el resultado hasta 0,9: la mayoría de los errores de luna llegan con confianza ≥ 0,95 (67 de ellos). El golden está sesgado hacia casos difíciles (59% frente al ~86% real), así que estas cifras sirven para comparar opciones, no como porcentajes de producción.

## Objetivos

- Que la rama virtual aplique el mismo criterio de baja confianza, **para todos los modelos**. Decisión del usuario: "si ganamos, en todos".

## No objetivos

- Cambiar los valores de los umbrales (0,85 de catálogo y 0,6 de fallback), las filas por defecto o el prompt `prompt.gpt56-luna`.
- Persistir la baja confianza del camino de catálogo o unificar los dos caminos. Es el enfoque C, que queda como proyecto aparte.
- Tocar PRE o PRO.

## Antiobjetivos

- Perder trazabilidad: que las ejecuciones virtuales en baja confianza dejen de verse en BD, el Monitor, los costes o las mediciones.
- Cambiar el comportamiento del camino de catálogo o de la clasificación parcial con TDN1 conocido, que continúa el pipeline.

## Enfoques considerados

- **A. Marcar baja confianza en la rama virtual sin quitarle nada (elegido).** Se compara la confianza con el umbral y, si no lo alcanza, el estado pasa a `BAJA_CONFIANZA_CLASIFICACION`. Se mantiene todo lo demás: persistencia, resumen combinado, markdown, Postproceso y Seguimiento. Es la superficie de cambio mínima, consigue la ganancia medida y no pierde trazabilidad.
- B. Lo mismo que A, pero sin persistir, como el camino de catálogo. Es más coherente entre caminos, pero los documentos desaparecen de BD. Descartado por el antiobjetivo.
- C. Un único punto de decisión para los dos caminos, con la baja confianza persistida siempre. Cambia el camino de catálogo y las métricas históricas. Fuera de alcance.

## Decisiones

- **Umbral de la rama virtual:** `entrada.Instrucciones.Classification.Umbral ?? resultadoClasificacion.UmbralFallbackAplicado ?? 0.6`. No hay tipología de catálogo, así que se salta ese eslabón de la cadena.
- **Comparación** con `resultadoClasificacion.Confianza`, estrictamente menor (`<`), igual que en el camino de catálogo.
- **Qué cambia:** en baja confianza solo cambian `Resultado.Estado` y el mensaje final de `FinalizarSeguimiento`, que dirá que la clasificación está por debajo del umbral. `ConfianzaGlobal`, `EstadoCalidad`, el resto de la salida y la llamada a `PersistirActivity` no cambian.
- **Deduplicación:** ya trata `BAJA_CONFIANZA_CLASIFICACION` como reutilizable (`DocumentoEjecucionRepositoryClaveTests`), así que no necesita cambios.

## Verificación

1. Tests unitarios en `DocumentProcessOrchestratorTests`, en la línea de `RunOrchestrator_ClasificacionParcial_OmitePipelinePosteriorYPersiste`:
   - Rama virtual con confianza por debajo del umbral: `BAJA_CONFIANZA_CLASIFICACION`, y `PersistirActivity` se llama.
   - Rama virtual con confianza igual o superior: OK, como hoy.
   - Umbral de la petición por encima del umbral del proveedor: manda el de la petición.
   - Clasificación parcial con TDN1 conocido: el comportamiento no cambia.
2. Build sin warnings nuevos, `dotnet test src/backend/DocumentIA.Tests.Unit` y `dotnet format --verify-no-changes`.
3. **Opcional, con sí:** desplegar la rama en DEV con el 802 y repetir el golden de luna. Lo esperado es unos 66 en baja y unos 118 errores de TDN1 en OK. Recuento de virtuales en baja confianza con la consulta `medicion/02` adaptada.

## Queda al constructor

- Nombre y forma del helper de umbral, si conviene extraerlo para compartirlo con el camino de catálogo sin cambiar su cadena.
- Texto exacto del mensaje final y si se añade una línea a `Postproceso.Normalizaciones`.

## Impacto documental previsto

- Técnica: `docs/03_DISENO_TECNICO_DETALLADO.md`, apartado de estados y umbral de clasificación.
- Funcional: `docs/05_MANUAL_USO_CONFIGURACION.md`, sobre qué significa `BAJA_CONFIANZA_CLASIFICACION` en la tipología virtual.
- Explotación: `scripts/migrations/gpt56-luna/README.md`, recuento esperado de bajas confianzas.
- Arquitectura: ninguno. No hay disparador de ADR; el ADR va con el cutover (AB#99939).
