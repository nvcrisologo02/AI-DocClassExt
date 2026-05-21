# Plan de implementacion - Pipeline de clasificacion configurable

Fecha: 2026-05-21
Estado: En ejecucion
Rama: se mantiene la rama actual (correcta)

## 1. Objetivo

Implementar un pipeline de clasificacion configurable por flujo (secuencia fija de providers), donde:

- Se ejecutan providers en orden hasta obtener un resultado satisfactorio.
- Si ninguno devuelve resultado satisfactorio, se usa un provider de fallback global por defecto (si esta activo).
- Se mantiene compatibilidad con el comportamiento/configuracion existente durante la migracion.

## 2. Decisiones ya cerradas

- No se usaran condiciones dinamicas de salto de providers por input (de momento).
- El pipeline sera secuencial y fijo por flujo.
- El fallback es global y se ejecuta solo al final del camino, nunca como paso intermedio del flujo.

## 3. Criterio de resultado satisfactorio

Resultado satisfactorio (v1):

- Tipologia valida (distinta de Desconocido/RESTO segun reglas de negocio vigentes), y
- Confianza >= umbral efectivo del provider/flujo.

Si no cumple, el pipeline continua con el siguiente provider.

## 4. Modelo de configuracion propuesto

## 4.1 Seccion de routing/pipeline

Ejemplo de esquema:

```json
{
  "ClassificationRouting": {
    "DefaultFlow": "hybrid-rules-gpt-di",
    "UseGlobalFallback": true,
    "GlobalFallbackProvider": "gpt",
    "Flows": {
      "hybrid-rules-gpt-di": {
        "Providers": ["rules", "gpt", "di"]
      },
      "hybrid-rules-di-gpt": {
        "Providers": ["rules", "di", "gpt"]
      },
      "di-only": {
        "Providers": ["di"]
      },
      "gpt-only": {
        "Providers": ["gpt"]
      }
    }
  }
}
```

Notas:

- Si la instruccion llega con `Provider=auto`, se usa `DefaultFlow`.
- Si la instruccion llega con un flujo explicito, ese flujo tiene prioridad.
- `UseGlobalFallback=true` habilita ejecutar `GlobalFallbackProvider` al final del pipeline sin exito.

## 4.2 Compatibilidad hacia atras

Mientras dure la migracion:

- Valores antiguos de `Provider` (por ejemplo: `hybrid`, `di`, `gpt`) se mapearan internamente a flujos equivalentes.
- Si no existe `Flows`, se aplica el comportamiento legacy actual.

## 5. Cambios de codigo (alto nivel)

## 5.1 Orquestacion

- Centralizar la resolucion de flujo en el orquestador configurable.
- Ejecutar providers del flujo en orden.
- Evaluar satisfaccion despues de cada provider.
- Si nadie satisface y fallback global activo, ejecutar fallback global.
- Registrar detalle de providers evaluados y motivo de descarte en `DetalleProveedores`.

## 5.2 Reutilizacion de providers actuales

- Reglas: primer intento rapido y barato.
- GPT: segundo intento para cubrir tipologias nuevas sin entrenar DI.
- DI: tercer intento en el flujo acordado para soporte legacy/modelos entrenados.
- Fallback global: provider unico por defecto, solo final.

## 5.3 Observabilidad

Agregar trazas estandar:

- Flujo resuelto (por config o por instruccion).
- Orden de ejecucion real.
- Causa de descarte por provider.
- Activacion o no del fallback global.
- Provider final ganador.

## 6. Plan de implementacion por fases

## Fase 1 - Configuracion y resolucion de flujo

- Extender settings para soportar `DefaultFlow`, `Flows`, `UseGlobalFallback`, `GlobalFallbackProvider`.
- Implementar resolver de flujo con prioridad:
  1) Instruccion explicita
  2) DefaultFlow por configuracion
  3) Legacy mapping

## Fase 2 - Pipeline secuencial y criterio de satisfaccion

- Ejecutar providers de forma secuencial.
- Cortar en primer satisfactorio.
- Completar `DetalleProveedores` de forma consistente.

## Fase 3 - Fallback global final

- Si no hay satisfactorio, y fallback activo, ejecutar fallback.
- Si fallback tambien falla/no satisface, devolver resultado de negocio controlado (no error tecnico salvo excepcion real).

## Fase 4 - Compatibilidad, pruebas y hardening

- Mantener comportamiento legacy en ausencia de nueva config.
- Pruebas unitarias e integracion para rutas principales y regresion.
- Ajuste de logs y mensajes operativos.

## 7. Pruebas

## 7.1 Unitarias

- Resolver de flujo (auto vs explicito).
- Secuencia fija por flujo.
- Corte por resultado satisfactorio.
- Activacion de fallback global solo al final.
- Compatibilidad legacy.

## 7.2 Integracion

- Flujo `rules -> gpt -> di` con casos donde gana cada provider.
- Caso sin satisfactorio en pipeline y con fallback activo.
- Caso fallback desactivado.

## 7.3 No funcional

- Validar tiempos y tasa de acierto en lote de documentos de referencia.
- Revisar telemetria y trazabilidad en artifacts de pruebas.

## 8. Work Items Azure DevOps (estructura)

IDs reales creados:

- Epic: 99461
- Features: 99464, 99462, 99463, 99465
- Tasks: 99473, 99476, 99466, 99467, 99477, 99469, 99468, 99470, 99475, 99471, 99472, 99474

Estado ADO actualizado:

- Features Done: 99464, 99462, 99463
- Feature In Progress: 99465
- Epic In Progress: 99461

Epic:

- Pipeline de clasificacion configurable por flujo con fallback global

Features:

- Configuracion de flujos y compatibilidad legacy
- Orquestador secuencial y criterio de satisfaccion
- Fallback global final
- Pruebas y observabilidad
- Documentacion funcional y tecnica

Tasks sugeridas:

- Implementar nuevo schema de routing y resolver de flujo
- Implementar ejecucion secuencial de providers
- Implementar fallback global final
- Completar cobertura de tests unitarios
- Completar pruebas de integracion y evidencias
- Actualizar manual de configuracion
- Actualizar diseno tecnico y analisis funcional

## 9. Documentacion a actualizar

- docs/02_ANALISIS_FUNCIONAL.md
- docs/03_DISENO_TECNICO_DETALLADO.md
- docs/05_MANUAL_USO_CONFIGURACION.md
- docs/07_ROADMAP_PENDIENTES.md

## 10. Criterios de aceptacion

- Es posible definir y ejecutar flujos fijos de providers por configuracion.
- `auto` usa la configuracion por defecto sin ambiguedades.
- Instruccion explicita de flujo tiene prioridad sobre la configuracion.
- El fallback global se ejecuta solo al final del camino sin resultado satisfactorio.
- Se mantiene compatibilidad con configuracion legacy durante la transicion.
- Existen pruebas y documentacion actualizadas.

## 11. Seguimiento

Checklist rapido:

- [x] Config nueva cargada en entorno dev
- [x] Flujo por defecto activo y validado
- [x] Fallback global final validado
- [x] Tests unitarios en verde
- [x] Tests de integracion en verde (Task 99472)
- [x] Documentacion actualizada
- [x] Work items creados y enlazados
- [x] Todos los WI cerrados en ADO con evidencias

## 12. Estado final

Iniciativa completada y cerrada (2026-05-21):

- Epic 99461: Done
- Features 99464, 99462, 99463, 99465: Done
- Tasks 99473-99477, 99466-99475, 99471-99474: Done
- Documentacion operativa:
  - Plan: docs/13_PLAN_PIPELINE_CLASIFICACION_CONFIGURABLE_2026-05-21.md
  - Backlog: docs/14_ADO_BACKLOG_PIPELINE_CLASIFICACION_2026-05-21.md
- Evidencias:
  - Build backend Functions: OK (10,1s).
  - Cambios Git: 7 archivos modificados + 2 archivos nuevos de documentacion.
  - Configuracion activa: DefaultFlow, Flows, UseGlobalFallback, GlobalFallbackProvider en appsettings.
  - Codigo: Orquestacion secuencial, corte por satisfaccion, fallback global final implementado en ConfigurableClasificarDataProvider.
