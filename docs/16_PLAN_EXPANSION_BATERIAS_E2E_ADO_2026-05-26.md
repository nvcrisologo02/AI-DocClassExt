# Plan maestro - Expansion de baterias E2E con integracion ADO

Fecha: 2026-05-26
Estado: En curso (estructura ADO base creada)
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

## 16. Estado real en ADO (creado 2026-05-26)

Objetivo de esta seccion: dejar trazabilidad explicita de lo ya creado en Azure DevOps para no perder continuidad entre sesiones.

### 16.1 Backlog de gestion creado

Features:

- 99592 - [E2E ADO] Linea A - Extraccion y Normalizacion documental.
- 99593 - [E2E ADO] Linea B - Orquestacion durable y estado.
- 99594 - [E2E ADO] Linea C - Contrato HTTP y validaciones.

User Stories / Product Backlog Items:

- 99597 - Gobernanza de expansion E2E para Linea A.
- 99595 - Gobernanza de expansion E2E para Linea B.
- 99596 - Gobernanza de expansion E2E para Linea C.

Tasks de Linea A (Feature 99592):

- 99601 - Diseno de casos y corpus.
- 99602 - Script de ejecucion (planificacion).
- 99603 - Alta de Test Plan/Suites/Test Cases.
- 99606 - Ejecucion, evidencia y cierre.

Tasks de Linea B (Feature 99593):

- 99599 - Diseno de casos y corpus.
- 99600 - Script de ejecucion (planificacion).
- 99598 - Alta de Test Plan/Suites/Test Cases.
- 99607 - Ejecucion, evidencia y cierre.

Tasks de Linea C (Feature 99594):

- 99609 - Diseno de casos y corpus.
- 99605 - Script de ejecucion (planificacion).
- 99608 - Alta de Test Plan/Suites/Test Cases.
- 99604 - Ejecucion, evidencia y cierre.

### 16.2 Test Plans canonicos vigentes (modulo Test Plans)

- 99650 - Cobertura E2E - Extraccion y Normalizacion.
- 99639 - Cobertura E2E - Orquestacion y Estado.
- 99652 - Cobertura E2E - Contrato HTTP y Validaciones.

Plan historico de referencia:

- 99581 - Cobertura E2E - Proceso Clasificacion D1-D7.

URLs canonicas de navegacion:

- https://sareb.visualstudio.com/AI%20DocClassExt/_testPlans?planId=99650&suiteId=99651
- https://sareb.visualstudio.com/AI%20DocClassExt/_testPlans?planId=99639&suiteId=99640
- https://sareb.visualstudio.com/AI%20DocClassExt/_testPlans?planId=99652&suiteId=99653

Nota de consistencia:

- Los IDs 99610, 99611 y 99612 correspondian a Work Items tipo Test Plan (no a planes reales del modulo Test Plans) y fueron deprecados durante la limpieza.
- Los planes canonicos A/B/C anteriores se validaron por REST API de Test Plans y por conteo de Test Cases asociados a cada suite.

### 16.3 Suites y casos vigentes por plan

Plan 99650 - Extraccion y Normalizacion:

- 99654 - A1 Proveedor y fallback -> TC 99663.
- 99655 - A2 Markdown y preprocesado -> TC 99634.
- 99656 - A3 Campos minimos y normalizacion -> TC 99629.
- 99657 - A4 Paginas y metadatos -> TC 99637.
- 99658 - A5 Calidad del resultado -> TC 99627.

Plan 99639 - Orquestacion y Estado:

- 99641 - B1 Secuencia de actividades -> TC 99648.
- 99642 - B2 Estado y customStatus -> TC 99646.
- 99643 - B3 Timeline y duraciones -> TC 99647.
- 99644 - B4 Reintentos y errores parciales -> TC 99649.

Plan 99652 - Contrato HTTP y Validaciones:

- 99659 - C1 Payload invalido -> TC 99636.
- 99660 - C2 Reglas de negocio invalidas -> TC 99638.
- 99661 - C3 Errores semanticos esperados -> TC 99630.
- 99662 - C4 Respuesta de error -> TC 99635.

### 16.4 Limpieza aplicada en backlog y test artifacts

Elementos deprecados/cerrados para evitar duplicidad y confusion:

- Test Plan WI: 99610, 99611, 99612 -> Inactive.
- Test Suite WI: 99613-99625 -> Completed.
- Test Case WI duplicados/no canonicos: 99626, 99628, 99631, 99632, 99633 -> Closed.
- Test Case WI reutilizados como vigentes y normalizados a Design: 99627, 99629, 99630, 99634, 99635, 99636, 99637, 99638, 99646, 99647, 99648, 99649, 99663.

Elementos de gestion conservados (vigentes):

- Features, PBIs y Tasks de lineas A/B/C: 99592-99609.

### 16.5 Estado operativo y siguientes pasos

Estado actual:

- Backlog limpiado y consistente con planes canonicos reales.
- Existen tres planes reales separados A/B/C en el modulo Test Plans.
- Estructura de gestion vigente y trazable para continuar sin ambiguedad.
- No se ha iniciado implementacion de scripts ni automatizacion (por decision explicita).

Siguientes pasos cuando se retome esta linea:

1. Completar pasos detallados y expected result en los Test Case vigentes.
2. Vincular ejecuciones y evidencias a los Tasks de cierre por linea.
3. Implementar scripts solo cuando se retome la automatizacion funcional.