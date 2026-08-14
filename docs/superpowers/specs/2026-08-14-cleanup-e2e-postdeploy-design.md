# Spec — Limpieza de registros generados por el E2E post-despliegue

Fecha: 2026-08-14
Estado: aprobado (pendiente de plan de implementación; ejecución diferida a decisión del usuario)
Work item: AB#100134 (hijo del PBI AB#100080)

## 1. Objetivo

Las ejecuciones del runner E2E post-despliegue (`tests/e2e-postdeploy/`) ingieren
documentos que quedan registrados en la base de datos y en blob storage de DEV y PRO,
marcados con `SubmittedBy = "e2e-postdeploy"`. Este diseño define el mecanismo para
eliminarlos de forma segura, con dry-run, sin posibilidad de tocar registros ajenos.

## 2. Decisiones (validadas con el usuario, 2026-08-14)

| Decisión | Valor |
|---|---|
| Mecanismo | Ambos: script cliente independiente + flag `-Cleanup` del runner |
| Vía de borrado | Endpoint de administración nuevo en Functions (server-side); nada de SQL directo |
| Alcance | BD (`DocumentoEjecuciones` + `Documentos`) **y** blobs (`RutaBlobStorage`) |
| Ejecución de la implementación | Diferida: spec + plan + WIs ahora; implementación cuando el usuario lo indique |

## 3. Contexto de persistencia (verificado en código)

- `Documentos` (`DocumentoEntity`): fila por documento deduplicado; campos relevantes
  `SubmittedBy`, `RutaBlobStorage`, `Guid`, `CorrelationId`.
- `DocumentoEjecuciones` (`DocumentoEjecucionEntity`): fila por ejecución, FK
  `DocumentoId` → Documentos; `SubmittedBy` propio por ejecución (puede diferir del
  del documento cuando un mismo documento deduplicado se reprocesa desde otro origen).
- Blobs: ruta en `Documentos.RutaBlobStorage`; existe un `BlobCleanupTimerTrigger`
  genérico que borra blobs expirados por retención (si borramos la fila de Documentos
  sin borrar su blob, el blob queda huérfano — por eso el alcance incluye blobs).
- Auditoría: `AuditoriaRepository` ya se usa para registrar la limpieza por retención;
  la limpieza E2E registrará sus operaciones por la misma vía.

## 4. Componentes

### 4.1 Endpoint `POST /api/management/cleanup/ejecuciones` (Functions)

Sigue el patrón de las funciones `management/*` existentes (misma autenticación por
function key, mismo estilo de respuesta JSON).

Request body:

```json
{
  "submittedBy": "e2e-postdeploy",
  "desdeUtc": "2026-08-14T10:00:00Z",
  "hastaUtc": null,
  "dryRun": true
}
```

Reglas:

- `submittedBy` **obligatorio y no vacío**; sin comodines. Si falta o está en blanco →
  HTTP 400. No existe modo "borrar todo".
- `desdeUtc`/`hastaUtc` opcionales; acotan por fecha de la ejecución.
- `dryRun` por defecto `true` (si el campo falta, se comporta como dry-run).

Selección:

1. Ejecuciones: `DocumentoEjecuciones` con `SubmittedBy == submittedBy` (y dentro del
   rango de fechas si se indica).
2. Documentos: `Documentos` con `SubmittedBy == submittedBy` que, tras eliminar las
   ejecuciones del paso 1, **no conserven ninguna ejecución de otro solicitante**.
   Un documento compartido con al menos una ejecución ajena nunca se borra (ni su blob).

Acción (solo con `dryRun=false`):

1. Borrar las ejecuciones seleccionadas.
2. Para cada documento seleccionado: borrar su blob si `RutaBlobStorage` informada
   (ignorar blob inexistente sin error), después borrar la fila de `Documentos`.
3. Registrar en Auditoría una entrada por operación de limpieza (solicitante, filtros,
   conteos), siguiendo el patrón del `BlobCleanupTimerTrigger`.

Response (200):

```json
{
  "dryRun": true,
  "submittedBy": "e2e-postdeploy",
  "ejecuciones": 42,
  "documentos": 17,
  "blobs": 15,
  "bytesLiberados": 12345678,
  "documentosConservadosPorEjecucionesAjenas": 1,
  "muestra": [ { "guid": "...", "nombreArchivo": "...", "ejecuciones": 3 } ]
}
```

`muestra` (máx. 20 elementos) solo en dry-run. Los conteos en dry-run son los que se
borrarían; en ejecución real, los efectivamente borrados.

### 4.2 Script cliente `tests/e2e-postdeploy/tools/cleanup-e2e.ps1`

- Parámetros: `-Environment dev|pro` (obligatorio), `-SubmittedBy` (default
  `e2e-postdeploy`), `-DesdeUtc`, `-HastaUtc`, `-Confirm` (switch).
- Lee `baseUrl` y `functionKey` de `tests/e2e-postdeploy/config/environments.json`
  (mismo loader `Get-E2EEnvironment`).
- Sin `-Confirm`: llama con `dryRun=true` y muestra el resumen y la muestra.
- Con `-Confirm`: primero dry-run y muestra el resumen; después pide confirmación
  interactiva escribiendo el nombre del entorno (`dev`/`pro`) y ejecuta el borrado real.
- Nunca imprime la function key.

### 4.3 Flag `-Cleanup` del runner `run-e2e-postdeploy.ps1`

- Al terminar la batería (tras generar el reporte), si `-Cleanup` está presente:
  llama al endpoint con `submittedBy = "e2e-postdeploy"`, `desdeUtc =` inicio de la
  pasada (el `StartedAtUtc` que el runner ya registra) y `dryRun=false`, sin
  confirmación interactiva (la pasada es suya y el rango la acota).
- El resultado (conteos o error) se añade al `report.md` y a la salida por consola.
- Si el endpoint no existe aún en el entorno (404), se informa con aviso y no falla
  la batería.

## 5. Seguridad

- Todo el borrado pasa por la API server-side (function key); no hay SQL directo.
- `submittedBy` explícito y obligatorio; la selección de documentos exige ausencia de
  ejecuciones ajenas; los blobs solo se borran junto con su documento.
- Dry-run por defecto en endpoint y script; el borrado real requiere `dryRun=false`
  explícito (endpoint), `-Confirm` + confirmación interactiva (script) o `-Cleanup`
  (runner, acotado a su propia pasada).
- Auditoría persistente de cada limpieza real.

## 6. Pruebas

- Unit tests del endpoint/servicio: validación de `submittedBy` (400), selección con
  rango de fechas, guarda de documentos compartidos (ejecución ajena → documento y
  blob se conservan), dry-run sin efectos, borrado real con blobs (mock de
  `IBlobStorageService`), registro de auditoría.
- Verificación real en DEV tras el despliegue: pasada smoke + `cleanup-e2e.ps1 -Environment dev`
  (dry-run → confirmación) comprobando conteos coherentes y que una segunda llamada
  devuelve 0.

## 7. Criterios de aceptación

1. `POST /api/management/cleanup/ejecuciones` con `submittedBy` vacío → 400.
2. Dry-run devuelve conteos y muestra sin modificar BD ni blobs.
3. Borrado real elimina ejecuciones, documentos y blobs del marcador, conserva
   documentos con ejecuciones ajenas y registra auditoría.
4. `cleanup-e2e.ps1` opera contra DEV y PRO con la config existente; sin `-Confirm`
   nunca borra.
5. `run-e2e-postdeploy.ps1 -Cleanup` limpia solo lo generado desde el inicio de su
   pasada y lo refleja en el reporte; con endpoint ausente (404) avisa y no falla.
6. Unit tests en verde; suite Pester del harness en verde.

## 8. Fuera de alcance

- Limpieza de registros históricos con otros marcadores (`migracion-dev`, etc.):
  el endpoint lo permitiría (parámetro `submittedBy`), pero su uso para otros
  marcadores queda a decisión operativa, no automatizado.
- Retención/borrado programado (el `BlobCleanupTimerTrigger` existente sigue igual).
- Limpieza de Test Runs en ADO.
