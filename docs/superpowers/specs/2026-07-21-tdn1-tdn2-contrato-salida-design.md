# Spec: Poblar Tdn1/Tdn2 en el contrato de salida (camino feliz y reutilización por duplicado)

**Fecha:** 2026-07-21
**Estado:** Aprobado (diseño validado en sesión)

## Problema

Cuando la clasificación jerárquica resuelve directamente una tipología publicada del catálogo
(p. ej. `comu.48`), el contrato de salida no informa `Identificacion.Tdn1` ni `Identificacion.Tdn2`.
Los únicos caminos que hoy asignan esos campos son los de tipología virtual/parcial
(`DocumentProcessOrchestrator.cs` líneas ~946, ~1064, ~1083) y la propagación de `Tdn2Detectado`
de la Fase 2 (~898-901). El camino feliz (~1178-1182) copia `Tipologia`, `TipologiaFamilia`,
`TipologiaVersion` y `TipologiaNombre` desde `tipologiaResuelta`, pero omite `Tdn1`/`Tdn2`,
que ya vienen cargados en el record `ResolvedTipologia` desde la configuración de la tipología
(`TipologiaVersionResolver` líneas ~219-220).

Consecuencia adicional: al reutilizar una ejecución por duplicado
(`ObtenerUltimaEjecucionDuplicadoActivity`), se devuelve el `ContratoSalidaCompletoJson`
guardado tal cual, por lo que las ejecuciones históricas afectadas por este hueco seguirán
devolviendo la salida sin TDN aunque se corrija el origen.

Ejemplo real: documento `OP-99-SCXX-00_ATINF00001608_106679764.pdf`, tipología `comu.48`,
`ReutilizadaPorDuplicado=true`, sin `Tdn1` ni `Tdn2` en `Identificacion`.

## Alcance

1. **Camino feliz del orquestador**: las nuevas ejecuciones que resuelven tipología de catálogo
   pueblan `Identificacion.Tdn1`/`Tdn2` desde `tipologiaResuelta`.
2. **Rehidratación en reutilización por duplicado**: si la salida guardada no trae TDN,
   se completan en cascada sin reprocesar el documento.

Fuera de alcance: backfill masivo de BD (la rehidratación en lectura cubre el caso),
cambios de esquema o de contrato (los campos ya existen en `ContratoSalida`).

## Diseño

### 1. DocumentProcessOrchestrator (camino feliz, ~línea 1178)

Al asignar la tipología resuelta:

- `Identificacion.Tdn1 = tipologiaResuelta.Tdn1` solo si `Identificacion.Tdn1` está vacío
  (los caminos parciales que ya lo asignaron tienen precedencia).
- `Identificacion.Tdn2 = tipologiaResuelta.Tdn2` solo si `Identificacion.Tdn2` está vacío
  (el `Tdn2Detectado` de Fase 2, asignado antes en ~898-901, tiene precedencia; normalmente coinciden).
- No asignar valores vacíos (no pisar con `string.Empty`).

Efecto colateral deseado: `PersistirActivity` (líneas ~131-134) empezará a guardar
`Documentos.Tdn1`/`Tdn2` también en el camino feliz.

### 2. ObtenerUltimaEjecucionDuplicadoActivity (rehidratación)

Nuevo método `RehidratarTdnSiIncompletoAsync(salida, documento)` invocado tras
`RehidratarResultadoSiIncompleto`, con cascada por campo (`Tdn1` y `Tdn2` de forma independiente):

1. **JSON guardado**: si `Identificacion.Tdn1`/`Tdn2` ya vienen informados, no tocar.
2. **Entidad `Documentos`**: si falta alguno, tomar `documento.Tdn1`/`documento.Tdn2`.
3. **Configuración de tipología**: si aún falta alguno y `Identificacion.Tipologia` es un
   código de catálogo (no vacío, distinto de `Desconocido`), cargar la tipología con
   `ITipologiaRepository.GetByCodigoAsync(codigo)` y usar las extensiones existentes
   `GetTdn1()`/`GetTdn2()` (`TipologiaEntityExtensions`, leen `ConfiguracionJson.ResolvedTdn1/2`).

Tolerancia a fallos: el lookup de tipología va en try/catch; si la tipología no existe,
no está publicada o su JSON es inválido, se loguea warning y los campos quedan como estaban.
La rehidratación nunca rompe la reutilización.

Dependencia nueva de la activity: `ITipologiaRepository` (inyección por constructor,
mismo patrón que los repositorios ya inyectados).

### 3. Tests unitarios

- `DocumentProcessOrchestratorTests`:
  - Camino feliz con tipología de catálogo → `Identificacion.Tdn1`/`Tdn2` poblados desde
    la tipología resuelta.
  - Con `Tdn2Detectado` informado por Fase 2 → `Tdn2` conserva el detectado (precedencia).
- `ObtenerUltimaEjecucionDuplicadoActivityTests`:
  - Salida guardada con TDN → se respeta (no se pisa).
  - Salida sin TDN + `Documentos.Tdn1/Tdn2` informados → se rellenan desde la entidad.
  - Salida sin TDN + entidad sin TDN + tipología publicada con config → se rellenan desde config.
  - Tipología inexistente/`Desconocido` → campos quedan null y la reutilización funciona.

## Criterio de aceptación

- Una ejecución nueva con tipología de catálogo devuelve `Identificacion.Tdn1` y `Tdn2`.
- Una reutilización por duplicado de una ejecución histórica sin TDN devuelve ambos campos
  (derivados de entidad o config) sin reprocesar el documento.
- `dotnet build` y `dotnet test` en verde.
