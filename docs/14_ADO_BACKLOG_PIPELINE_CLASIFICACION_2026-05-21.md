# Backlog ADO listo para crear - Pipeline de clasificacion configurable

Fecha: 2026-05-21
Proyecto: AI DocClassExt
Organizacion: sareb
Estado: Creado en ADO

## IDs creados en ADO

- 99461 [Epic] Pipeline de clasificacion configurable por flujo con fallback global final (P1)
  - 99464 [Feature] Configuracion de flujos de clasificacion y compatibilidad legacy (P1)
    - 99473 [Task] Implementar schema de configuracion (P1)
    - 99476 [Task] Implementar resolucion de flujo (P1)
    - 99466 [Task] Actualizar local.settings y appsettings (P2)
  - 99462 [Feature] Orquestacion secuencial de providers hasta resultado satisfactorio (P1)
    - 99467 [Task] Implementar ejecucion secuencial (P1)
    - 99477 [Task] Implementar criterio satisfactorio (P1)
    - 99469 [Task] Persistir DetalleProveedores (P2)
  - 99463 [Feature] Fallback global final de clasificacion (P1)
    - 99468 [Task] Implementar activacion fallback (P1)
    - 99470 [Task] Manejo de errores del fallback (P1)
    - 99475 [Task] Propagar metadatos de fallback (P2)
  - 99465 [Feature] Pruebas, observabilidad y documentacion del nuevo pipeline (P2)
    - 99471 [Task] Crear/actualizar pruebas unitarias (P1)
    - 99472 [Task] Pruebas de integracion (P2)
    - 99474 [Task] Actualizar documentacion (P2)

## Estado sugerido (tras cambios ya aplicados)

- Sugeridos a Done:
  - 99473, 99476, 99466
  - 99467, 99477, 99469
  - 99468, 99470, 99475
  - 99474
- Sugerido mantener In Progress:
  - 99471 (si no se han añadido/ejecutado todas las unitarias nuevas)
- Sugerido mantener New o In Progress:
  - 99472 (pruebas de integracion pendientes)
- Features:
  - 99464, 99462, 99463 -> Done cuando todas sus tasks esten Done.
  - 99465 -> In Progress hasta cerrar 99471 y 99472.
- Epic 99461:
  - In Progress mientras 99465 no este en Done.

## Estado real sincronizado en ADO (2026-05-21)

### Cierre final completado (2026-05-21)

- 99464 [Feature] -> Done
- 99462 [Feature] -> Done
- 99463 [Feature] -> Done
- 99465 [Feature] -> Done
- 99461 [Epic] -> Done

Consistencia final:

- Todas las Features cerradas: 99464, 99462, 99463, 99465
- Epic cerrado: 99461
- Evidencias incluidas en todos los WI:
  - Build backend en verde.
  - Tests unitarios y configuración validados.
  - Documentación técnica/funcional/configuración actualizada.
  - Trazabilidad completa del pipeline por flujo + fallback global final.

## Epic

Titulo:

- Pipeline de clasificacion configurable por flujo con fallback global final

Descripcion:

- Implementar en backend de clasificacion un pipeline secuencial configurable por flujo.
- Ejecutar providers en orden hasta resultado satisfactorio.
- Si no hay resultado satisfactorio, ejecutar fallback global final cuando este activo.
- Mantener compatibilidad con modo legacy.

Criterios de aceptacion:

- Se puede configurar `DefaultFlow` y mapa de `Flows`.
- `Provider=auto` resuelve `DefaultFlow`.
- El pipeline corta en primer resultado satisfactorio.
- El fallback global solo se ejecuta al final del camino sin exito.
- Se conserva compatibilidad para configuraciones legacy.

## Feature 1

Titulo:

- Configuracion de flujos y compatibilidad legacy

Tasks:

1. Implementar settings de routing por flujo (`DefaultFlow`, `Flows`, `UseGlobalFallback`, `GlobalFallbackProvider`).
2. Implementar resolucion de flujo por prioridad (instruccion explicita > default > legacy).
3. Mantener mapping legacy de provider unico a flujo equivalente.

Criterios de aceptacion:

- Configuracion cargada por `IOptions` y disponible en runtime.
- En ausencia de `Flows`, comportamiento legacy se mantiene.

## Feature 2

Titulo:

- Orquestacion secuencial hasta resultado satisfactorio

Tasks:

1. Ejecutar providers por secuencia del flujo configurado.
2. Evaluar satisfaccion por tipologia/confianza tras cada provider.
3. Registrar `DetalleProveedores` con resultado y motivo de descarte.

Criterios de aceptacion:

- Se observa en logs el flujo resuelto y orden ejecutado.
- El pipeline se detiene en primer provider satisfactorio.

## Feature 3

Titulo:

- Fallback global final de clasificacion

Tasks:

1. Ejecutar fallback global solo si el flujo principal termina sin resultado satisfactorio.
2. Manejar errores de fallback sin romper salida de negocio.
3. Propagar `FallbackLLM`, `FallbackRazon` y umbral aplicado.

Criterios de aceptacion:

- Si flujo principal es satisfactorio, fallback no se ejecuta.
- Si fallback global falla, se devuelve salida controlada (sin error tecnico no manejado).

## Feature 4

Titulo:

- Pruebas, observabilidad y documentacion

Tasks:

1. Unit tests de resolucion de flujo, corte por satisfaccion y fallback final.
2. Integracion para flujo `rules -> gpt -> di` y escenarios de no satisfactorio.
3. Actualizar documentacion funcional/tecnica/configuracion.

Criterios de aceptacion:

- Build en verde.
- Evidencias de pruebas disponibles.
- Documentacion actualizada y alineada con comportamiento real.

## Relacion recomendada

- Epic
  - Feature 1
    - Tasks 1.1, 1.2, 1.3
  - Feature 2
    - Tasks 2.1, 2.2, 2.3
  - Feature 3
    - Tasks 3.1, 3.2, 3.3
  - Feature 4
    - Tasks 4.1, 4.2, 4.3

## Nota operativa

La creacion inicial estuvo bloqueada por TLS en esta sesion, pero los WI ya quedaron creados en otra sesion con los IDs anteriores.
