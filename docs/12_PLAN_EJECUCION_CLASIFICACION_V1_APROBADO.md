# Plan de Ejecucion Clasificacion Documental v1 (Aprobado)

Fecha de cierre de decisiones: 2026-05-12
Estado: GO condicionado aprobado

## 1) Resumen ejecutivo

Se aprueba iniciar la implementacion de la Clasificacion Documental v1 sobre la arquitectura existente, sin crear un sistema paralelo.

Enfoque funcional:
- Clasificacion jerarquica TDN1 (familia) -> TDN2 (subtipo) -> Matricula
- Entrada por modo classificationOnly
- Baseline con reglas/OCR + DI Classification
- Fallback controlado con GPT-4o mini en casos ambiguos o de baja confianza

Guardrails aprobados:
- Umbral DI: 0,85
- Delta top1-top2: 0,15
- Timeout fallback: 8s + 1 retry + backoff 500ms
- Coste maximo: 0,10 EUR por documento
- Fallback rate objetivo: <= 25%
- Latencia P95 objetivo: <= 90s

## 2) Decisiones de negocio y operacion

Semantica de estados:
- UNKNOWN: resultado valido no clasificable
- OUT_OF_SCOPE: resultado valido fuera de catalogo
- NEEDS_REVIEW: resultado valido ambiguo, gestion externa por cliente
- ERROR: solo fallos tecnicos

Gobierno de catalogo:
- Fuente de verdad: BBDD/Admin
- Flujo: Draft -> Published
- Owner funcional: Admin
- Versionado por familia: tdn-clasificacion v1.0, v1.1, ...

Deduplicacion:
- No reprocesar por cambio de classifierVersion si SHA256 coincide

Politica de paginas:
- Clasificar 3 paginas por defecto
- Escalar a 5 paginas en escenarios necesarios

## 3) Alcance funcional v1

Tipologias propuestas (10):
- docn.cambio-titularidad.sareb
- escr.compraventa
- escr.titularidad.otro
- escr.titularidad-anterior.sareb
- escr.dacion
- escr.cancelacion-hipotecaria
- escr.prestamo-originario
- sere.subasta-adjudicacion.auto
- sere.subasta-testimonio.adjudicacion
- sere.subasta-cancelacion.cargas

## 4) Estructura ADO aprobada

- Epica: 99361
- Feature F1 Catalogo y gobierno: 99363
- Feature F2 Motor clasificacion jerarquica: 99366
- Feature F3 Fallback GPT controlado: 99364
- Feature F4 Persistencia y observabilidad: 99362
- Feature F5 Piloto y criterios Go/No-Go: 99365

Total planificado: 32 historias/tasks

## 5) Camino critico y secuencia

Dependencias:
- F1 es bloqueante para F2
- F2 habilita F3
- F4 avanza en paralelo tras base tecnica
- F5 valida transversalmente al final con criterio por tipologia

Ruta recomendada:
1. Semana 1: F1 (catalogo) + preparacion tecnica
2. Semana 1-2: F2 (motor)
3. Semana 2: F3 (fallback)
4. Semana 2-3: F4 (persistencia/observabilidad)
5. Semana 3-4: F5 (piloto y decision)

## 6) Bloqueos criticos previos a Dia 1

1. Confirmar catalogo final v1 (que tipologias entran efectivamente)
2. Verificar dataset piloto (60-100 muestras por tipologia)
3. Confirmar credenciales Azure OpenAI para GPT-4o mini
4. Cerrar RBAC Admin + operativa NEEDS_REVIEW externa
5. Validar capacidad de Custom DI Classification

## 7) Criterios de exito de piloto

Por tipologia (objetivo de aceptacion):
- Accuracy TDN1 >= 95%
- Accuracy TDN2 >= 85%
- Precision >= 95%

Global operativo:
- Coste <= 0,10 EUR/doc
- Fallback <= 25%
- P95 <= 90s

## 8) Checklist operativo inmediato

- Resolver los 5 bloqueos antes de kickoff
- Sprint planning con Backend + DevOps y asignaciones por feature
- Activar observabilidad desde Dia 1 (coste/fallback/latencia)
- Ejecutar implementacion por fases segun dependencias

## 9) Nota de gestion

Este documento consolida decisiones cerradas para evitar deriva funcional durante la implementacion.
Todo cambio posterior debe trazarse en ADO y reflejarse en version de catalogo o politica operativa.

## 10) Registro de arranque de implementacion (2026-05-13)

Rama de trabajo:
- feature/99361-clasificacion-v1-kickoff (creada desde origin/develop)

Estado ADO actualizado:
- Epica 99361: In Progress
- Features en ejecucion: 99363 (F1), 99366 (F2)
- Features en cola: 99364 (F3), 99365 (F4), 99362 (F5)
- Tasks Dia 1 activadas (Committed): 99370, 99368, 99367, 99377, 99376

Alcance operativo Dia 1:
- F1: congelacion de catalogo v1, diseno de estructura TDN y API base
- F2: definicion de umbrales y puesta en marcha del motor jerarquico base

Trazabilidad:
- La nota de kickoff se dejo registrada en la descripcion de la epica, features y tasks de Dia 1.
- Referencias base: PRD de clasificacion y este plan aprobado.

## 11) Actualizacion de avance (2026-05-13)

Resumen tecnico aplicado:
- Se consolida el enfoque markdown-first previo a clasificacion para reducir reextracciones posteriores.
- El proveedor inicial de markdown queda restringido a `di-layout` (Azure Document Intelligence Layout).
- Se retira la alternativa `local-pdf` y fallback OCR local por `tesseract` del runtime de Azure Functions.

Estado de work items relacionados:
- 99403: Done.
- 99404: Done.
- 99405: Done.

Pendiente operativo:
- Ejecutar smoke de ingestion HybridTDN en entorno con infraestructura local completa (SQL + Storage + host Functions) y adjuntar evidencia de trazabilidad en ADO.
