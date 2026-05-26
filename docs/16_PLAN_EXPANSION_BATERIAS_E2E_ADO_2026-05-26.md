# Plan maestro - Expansion de baterias E2E con integracion ADO

Fecha: 2026-05-26
Estado: Pendiente de inicio
Alcance: definicion y secuenciacion de nuevas baterias E2E con publicacion vinculada en Azure DevOps Test Plans.
Referencia base validada: plan ADO 99581 + run 1076772 + script `tests/api-tests/test-classification-process.ps1`

## 1. Objetivo

Extender el patron ya validado para clasificacion documental a otras tres areas funcionales del sistema:

1. Extraccion y normalizacion documental.
2. Orquestacion durable y seguimiento de estado.
3. Contrato HTTP y validaciones de entrada.

El objetivo no es solo aumentar cobertura tecnica. El objetivo real es disponer de baterias ejecutables con estas propiedades:

- Casos agrupados por suites funcionales.
- Evidencias JSON/CSV por caso.
- Publicacion automatica en Azure DevOps Test Plans.
- Resultados vinculados a `testCaseId` y `testPointId` reales.
- Trazabilidad suficiente para regresion, hardening y auditoria.

## 2. Situacion de partida

Patron base ya operativo en clasificacion:

- Script E2E: `tests/api-tests/test-classification-process.ps1`.
- Casos: `tests/api-tests/classification-process-cases.json`.
- Test Plan ADO operativo: 99581.
- Suites reales: 99583-99590.
- Run valido con vinculacion completa: 1076772.
- Verificacion cerrada: 23 resultados, 23 `testCaseId`, 23 `testPointId`, 23 `Passed`.

Conclusiones reutilizables del patron actual:

- La estructura JSON + script PowerShell es suficiente para una bateria funcional completa.
- La publicacion correcta a ADO debe crear un run con `pointIds` y despues hacer `PATCH` de resultados precreados.
- El valor no esta solo en ejecutar tests, sino en dejar evidencia navegable en ADO por suite/case/run.

## 3. Alcance funcional priorizado

Se priorizan estas tres lineas por retorno funcional y operativo:

### 3.1 Linea A - Extraccion y normalizacion documental

Cobertura objetivo:

- Seleccion de proveedor y fallback.
- Uso de markdown inyectado frente a markdown generado.
- Campos minimos obligatorios y campos opcionales.
- Normalizacion de fechas, importes, identificadores y paginas.
- Reglas de calidad del resultado extraido.

Valor de negocio:

- Detecta degradaciones silenciosas de calidad de salida.
- Reduce regresiones en cambios de prompts, provider routing o parser.
- Protege el contrato funcional que consumen validacion, persistencia y plugins.

### 3.2 Linea B - Orquestacion durable y estado

Cobertura objetivo:

- Secuencia de actividades segun camino funcional.
- Estados `Pending`, `Running`, `Completed` y salidas de negocio controladas.
- `customStatus` y timeline observable.
- Duraciones por actividad.
- Reintentos, errores parciales y early exits.

Valor de negocio:

- Detecta roturas de flujo que no siempre se ven en el output final.
- Protege la observabilidad y la capacidad de soporte operativo.
- Da cobertura a la evolucion del orquestador sin perder trazabilidad.

### 3.3 Linea C - Contrato HTTP y validaciones

Cobertura objetivo:

- Validacion de payloads invalidos.
- Combinaciones de parametros no permitidas.
- Mensajes y codigos HTTP esperados.
- Ausencia de errores tecnicos expuestos indebidamente.

Valor de negocio:

- Endurece el endpoint frente a integradores y consumidores externos.
- Hace visibles las regresiones de contrato en cambios pequenos.
- Es una bateria barata de ejecutar y muy estable en el tiempo.

## 4. Principios del patron reutilizable

Cada nueva bateria debe mantener estas reglas:

1. Un unico script `.ps1` por dominio funcional.
2. Un unico JSON de casos por dominio funcional.
3. Suites ADO alineadas con grupos funcionales del JSON.
4. Mapeo explicito caso -> Work Item `Test Case`.
5. Publicacion vinculada por `testPointId`, no por resultados sueltos.
6. Artefactos por caso y resumen CSV por ejecucion.
7. Criterios de aceptacion escritos antes de crear los casos.

## 5. Diseno propuesto - Linea A

Nombre de bateria sugerido:

- `test-extraction-normalization-process.ps1`

Nombre de fichero de casos sugerido:

- `extraction-normalization-cases.json`

Plan ADO sugerido:

- `Cobertura E2E - Extraccion y Normalizacion`

Suites candidatas:

### Suite A1 - Proveedor y fallback

Casos iniciales sugeridos:

- Auto usa proveedor primario esperado.
- Proveedor forzado DI.
- Proveedor forzado GPT.
- Fallback al proveedor secundario cuando falla el primario.
- Salida controlada cuando no responde ningun proveedor.

### Suite A2 - Markdown y preprocesado

Casos iniciales sugeridos:

- Markdown inyectado y no regenerado.
- Markdown ausente y generado por layout.
- Markdown vacio y regenerado.
- Markdown invalido y degradacion segura.

### Suite A3 - Campos minimos y normalizacion

Casos iniciales sugeridos:

- Extraccion completa con todos los campos.
- Falta campo opcional y el resultado sigue siendo valido.
- Falta campo obligatorio y se marca estado esperado.
- Normalizacion de fecha.
- Normalizacion de importe.
- Normalizacion de identificadores.

### Suite A4 - Paginas y metadatos

Casos iniciales sugeridos:

- Paginas derivadas del proveedor.
- Paginas derivadas por heuristica.
- Conflicto entre fuentes y prioridad correcta.
- Documento de una pagina.
- Documento multipagina con recorte aplicado.

### Suite A5 - Calidad del resultado

Casos iniciales sugeridos:

- Resultado completo y consumible.
- Resultado parcial pero usable.
- Resultado ambiguo.
- Error funcional controlado.

Pendientes tecnicos especificos:

- Identificar endpoint o punto de observacion comun para validar salida de extraccion/normalizacion.
- Confirmar campos minimos que forman parte del contrato funcional.
- Seleccionar corpus pequeno pero representativo de documentos.

## 6. Diseno propuesto - Linea B

Nombre de bateria sugerido:

- `test-orchestration-state-process.ps1`

Nombre de fichero de casos sugerido:

- `orchestration-state-cases.json`

Plan ADO sugerido:

- `Cobertura E2E - Orquestacion y Estado`

Suites candidatas:

### Suite B1 - Secuencia de actividades

Casos iniciales sugeridos:

- Camino feliz completo.
- Camino `classificationOnly`.
- Camino con markdown preexistente.
- Camino con duplicado y early exit.

### Suite B2 - Estado y customStatus

Casos iniciales sugeridos:

- Transicion `Pending -> Running -> Completed`.
- `customStatus` muestra actividad actual.
- `customStatus` refleja progreso parcial.
- Estado final de negocio no clasificado sin error tecnico.

### Suite B3 - Timeline y duraciones

Casos iniciales sugeridos:

- Timeline completo persistido.
- Duraciones mayores que cero en actividades ejecutadas.
- Actividades omitidas correctamente ausentes o marcadas como skipped.
- Consistencia entre timeline y output final.

### Suite B4 - Reintentos y errores parciales

Casos iniciales sugeridos:

- Error recuperable con reintento.
- Error no recuperable con estado final esperado.
- Fallo intermedio con trazabilidad util.
- Polling agotado o timeout funcional.

Pendientes tecnicos especificos:

- Confirmar puntos de inspeccion del timeline persistido.
- Acordar si ciertas actividades omitidas deben no aparecer o marcarse explicitamente.
- Determinar corpus minimo para forzar rutas de reintento y early exit.

## 7. Diseno propuesto - Linea C

Nombre de bateria sugerido:

- `test-http-contract-validation.ps1`

Nombre de fichero de casos sugerido:

- `http-contract-validation-cases.json`

Plan ADO sugerido:

- `Cobertura E2E - Contrato HTTP y Validaciones`

Suites candidatas:

### Suite C1 - Payload invalido

Casos iniciales sugeridos:

- `documento` ausente.
- `content.base64` ausente.
- base64 invalido.
- JSON mal formado.
- tipo incorrecto en flags o parametros.

### Suite C2 - Reglas de negocio invalidas

Casos iniciales sugeridos:

- `classificationOnly=true` con parametros incompatibles.
- `nivelClasificacion` fuera del enum.
- combinacion invalida de provider/model.
- `maxPagesForClassificationOnly <= 0`.

### Suite C3 - Errores semanticos esperados

Casos iniciales sugeridos:

- Documento vacio.
- Documento no soportado.
- `expectedType` invalido.
- parametros inconsistentes entre clasificacion y extraccion.

### Suite C4 - Respuesta de error

Casos iniciales sugeridos:

- Codigo HTTP correcto.
- Mensaje funcional util.
- Estructura de error consistente.
- Sin fuga de error tecnico interno.

Pendientes tecnicos especificos:

- Confirmar codigos HTTP objetivo por cada validacion.
- Confirmar contrato minimo del cuerpo de error.
- Revisar si existen otros endpoints candidatos para la misma bateria.

## 8. Secuencia recomendada de ejecucion

Orden recomendado:

1. Linea A - Extraccion y normalizacion.
2. Linea B - Orquestacion y estado.
3. Linea C - Contrato HTTP y validaciones.

Motivo:

- La linea A tiene el mejor retorno funcional inmediato.
- La linea B protege estabilidad operativa y observabilidad.
- La linea C es mas barata y puede cerrarse despues con rapidez.

## 9. Fases de trabajo

### Fase 0 - Preparacion comun

- Confirmar corpus reutilizable o crear corpus minimo dedicado.
- Confirmar endpoints y artefactos observables por dominio.
- Definir convencion de nombres de suites, casos y planes ADO.
- Crear plantilla reusable del mapeo `caso -> Test Case WI`.

### Fase 1 - Bateria Linea A

- Crear plan ADO y suites.
- Crear Test Cases WI.
- Implementar JSON de casos.
- Implementar script de ejecucion.
- Ejecutar, depurar y publicar run vinculado.

### Fase 2 - Bateria Linea B

- Repetir patron con foco en estado, timeline y observabilidad.
- Anadir asserts sobre secuencia de actividades y `customStatus`.

### Fase 3 - Bateria Linea C

- Repetir patron con foco en 4xx y contrato de error.
- Mantener los casos pequenos, rapidos y estables.

### Fase 4 - Consolidacion

- Extraer helpers comunes PowerShell si aparece duplicidad real.
- Unificar documentacion de ejecucion local y ADO.
- Anadir referencias en roadmap y plan de pruebas si procede.

## 10. Dependencias y prerrequisitos

Dependencias comunes:

- Azure Functions host operativo en `http://localhost:7071` para validacion local.
- Datos de prueba disponibles y versionados donde sea razonable.
- Acceso a ADO con PAT valido para crear planes, suites, casos y runs.
- Estabilidad minima de endpoints y contratos antes de masificar casos.

Dependencias funcionales por linea:

- Linea A: salida de extraccion/normalizacion claramente observable.
- Linea B: timeline y `customStatus` accesibles y consistentes.
- Linea C: contrato HTTP de error estabilizado o al menos conocido.

## 11. Riesgos y mitigaciones

| Riesgo | Impacto | Mitigacion |
|---|---|---|
| Los casos dependen de entorno local inestable | Alto | Mantener corpus pequeno, host controlado y artefactos de evidencia |
| Creacion manual de WI/suites deriva del plan | Alto | Crear nomenclatura y mapping explicito antes de publicar resultados |
| Duplicidad excesiva entre scripts | Medio | Extraer helpers solo tras cerrar la primera bateria adicional |
| Casos demasiado fragiles por telemetria cambiante | Medio | Validar invariantes funcionales, no mensajes accidentales |
| Contrato HTTP aun en movimiento | Medio | Posponer casos mas finos hasta estabilizar errores y payloads |

## 12. Criterio de done por bateria

Cada bateria se considera cerrada cuando cumple todo lo siguiente:

1. Existe script `.ps1` operativo.
2. Existe JSON de casos versionado.
3. Existe plan ADO real con suites y Test Cases WI enlazados.
4. Existe al menos un run exitoso con resultados vinculados a `testCaseId` y `testPointId` reales.
5. Se guardan artefactos JSON/CSV de la ejecucion.
6. La documentacion minima de uso queda actualizada.

## 13. Backlog minimo a crear en ADO

Propuesta de estructura por cada linea:

- 1 Feature por linea funcional.
- 4 Tasks tecnicas por linea:
  - Diseno de casos y corpus.
  - Script de ejecucion.
  - Alta de Test Plan/Suites/Test Cases.
  - Ejecucion, evidencia y cierre.

Total recomendado inicial:

- 3 Features.
- 12 Tasks.

## 14. Checklist de reanudacion

Antes de retomar este plan en otra sesion:

- Verificar que el run base 1076772 sigue visible en ADO como referencia valida.
- Revisar el estado actual del script `test-classification-process.ps1` para no perder el patron correcto de publicacion.
- Confirmar si ya existen nuevos WI o planes creados para alguna de las tres lineas.
- Elegir una sola linea de trabajo activa para evitar mezclar ADO, corpus y scripts a la vez.

Primer siguiente paso recomendado al retomar:

1. Arrancar por la Linea A.
2. Crear el documento espejo de backlog ADO para esa linea.
3. Dar de alta plan, suites y casos antes de escribir toda la automatizacion.

## 15. Nota operativa

Este documento actua como plan maestro de continuidad.

No implica que todas las baterias deban implementarse en una sola iteracion. La recomendacion es avanzar de forma incremental, cerrando por completo una linea antes de abrir la siguiente, para no repetir el problema inicial de resultados E2E sin vinculacion correcta en ADO.