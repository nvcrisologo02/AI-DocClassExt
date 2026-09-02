# Optimización de almacenamiento BD PRO (AB#100165)

> **Estado (02/09/2026): implementado, en `develop` (merge `5c61309`) y validado en DEV.**
> Tasks AB#100166-100172 en Done. E2E full 31 PASS / 0 FAIL; ahorro medido por ejecución
> (mediana): 20,23 KB → 11,51 KB (−43%). Cambios sobre el plan durante la ejecución: los
> backfills van en scripts por lotes fuera de las migraciones (el de IdActivo agotaba el
> timeout ya con 8k filas) y el markdown usa **escritura dual** (ambas columnas) para que la
> vuelta atrás no pierda datos — ensayada de verdad en DEV (aplicar→revertir→reaplicar).
> **Pendiente PRO**: script de AB#100168 en la misma ventana que el índice de AB#100185,
> después los dos backfills (`backfill-idactivo.ps1`, `migrar-markdown-a-binario.ps1`) y la
> alerta de capacidad (`create-db-capacity-alert.ps1`). La fase de *contract* del markdown
> (dejar de escribir la Base64 y borrarla) queda fuera de alcance hasta verificar PRO.

Plan de la iniciativa para reducir el consumo de almacenamiento de la BD `DocumentIA` en PRO
(Azure SQL, `srbsqlprodocai`), tras alcanzar el límite configurado de 2 GB (ampliado a 20 GB
el 31/08/2026) con 2.880 MB de datos y 58.624 ejecuciones en agosto de 2026.

## Análisis (31/08/2026)

Medición directa sobre PRO (scripts de apoyo en `docs/auxiliares/temps/2026-08-31/`):

| Tabla / columna | Tamaño | Observación |
|---|---|---|
| `DocumentoEjecuciones` (70.102 filas) | 1.758 MB | 1.212 MB en LOB |
| — `ContratoSalidaCompletoJson` | 949 MB | media 13,9 KB; máx 7,9 MB (AssetResolver completo en filas antiguas) |
| — `ActivityTimelineJson` | 207 MB | duplicado de `$.DetalleEjecucion.Seguimiento.Actividades` del contrato |
| — `DatosFinalesJson` | 118 MB | duplicado de `$.DatosExtraidos` del contrato |
| — `DatosOriginalesJson` | 40 MB | duplicado de `$.DetalleEjecucion.Integracion.DatosOriginales` |
| `Documentos` (66.169 filas) | 846 MB | — |
| — `NormalizacionMarkdownCompressed` | 640 MB | GZip→Base64 en `nvarchar(max)`: infla ×2,67 frente a binario |
| `ResultadosProcesamiento` | 158 MB | — |
| Resto (`Auditoria`, catálogos, backups) | ~35 MB | — |

Causas principales:

1. **Duplicidad de persistencia**: `PersistirActivity` graba el contrato completo y, además, tres
   columnas cuyo contenido ya viaja dentro del contrato (~365 MB de copias).
2. **Codificación del markdown**: comprimido con GZip pero codificado en Base64 dentro de
   `nvarchar(max)` (UTF-16): 2 bytes por carácter Base64, ×2,67 el tamaño binario real.
3. **Volumen**: campaña masiva de agosto (58.624 ejecuciones, `DocumentIA.Batch.ClassificationLite`),
   con un footprint neto de ~42 KB por ejecución.
4. **Sin retención en BD**: existe limpieza de blobs (`BlobCleanupTimerTrigger`) pero las filas
   (contratos, timelines, markdown) no expiran nunca.

## Alcance de esta iniciativa (AB#100165)

Decisiones acordadas: dejar de grabar las columnas duplicadas (el histórico no se toca),
markdown a binario, alerta de capacidad. La retención/archivado (opción "conservar el contrato
vigente por documento" o archivado a blob) queda pospuesta: se medirá el crecimiento un mes
después de desplegar estos cambios y se decidirá con datos.

### Decisión de diseño del timeline (actualizada 01/09, coordinada con AB#100182)

El plan de rendimiento del Monitor (AB#100182, spec
`docs/superpowers/specs/2026-09-01-monitor-admin-rendimiento-design.md`) proyecta un DTO ligero
del listado que incluye `ActivityTimelineJson`. Para no colisionar, la deduplicación del timeline
se **invierte**: se mantiene la columna `ActivityTimelineJson` (copia compacta que consume el
listado) y se **poda `$.DetalleEjecucion.Seguimiento.Actividades` del contrato antes de
persistirlo** (mismo patrón que ya se aplica a `Postproceso.Markdown`: retirar antes de
serializar, restaurar después en el objeto en memoria). El ahorro es equivalente (~245 MB del
lado del contrato frente a ~207 MB del lado de la columna) y ambos planes quedan independientes.

Los lectores que deserializan el contrato (detalle de Admin y flujo de duplicados) re-mergean el
timeline desde la columna cuando el contrato venga podado, de modo que sus salidas son idénticas
a las actuales.

### Garantía de no impacto en consumidores (verificada)

- La respuesta HTTP de la API se construye en memoria; nunca lee las columnas eliminadas y el
  objeto en memoria conserva el timeline completo.
- El flujo de duplicados (`ObtenerUltimaEjecucionDuplicadoActivity`) usa
  `ContratoSalidaCompletoJson` (que se mantiene) y re-mergea el timeline desde
  `ActivityTimelineJson` para devolver el contrato completo.
- `sp_ObtenerDocumentoEjecucionesPorIdActivo`: sin ejecuciones en caché de procedimientos ni en
  Query Store (~30 días) en PRO. Se reescribe de forma defensiva conservando la forma del resultset.
- El detalle de ejecución de Admin se monta desde el contrato con el mismo re-merge; el listado
  no cambia (sigue leyendo `ActivityTimelineJson`). La webapp Blazor no se modifica.
- El histórico conserva sus columnas rellenas: solo se deja de grabar, no se borra nada.

### Tasks

| WI | Título | Resumen |
|---|---|---|
| AB#100166 | Persistencia | `PersistirActivity` deja a NULL `DatosFinalesJson` y `DatosOriginalesJson` en filas nuevas y poda `Seguimiento.Actividades` del contrato persistido (`ActivityTimelineJson` se mantiene) |
| AB#100167 | Lectores del contrato | Detalle de Admin y flujo de duplicados re-mergean el timeline desde `ActivityTimelineJson` cuando el contrato viene podado |
| AB#100168 | BD / SP | Columna escalar `IdActivo` indexada en `DocumentoEjecuciones`; SP por IdActivo reescrito con mismos alias vía `JSON_QUERY`; retirada de la columna calculada `IdActivoNormalizado` |
| AB#100169 | Markdown binario | Columna `varbinary(max)` nueva (GZip sin Base64), lectura con fallback, migración del histórico por lotes apta para S0 |
| AB#100170 | Scripts internos | `generate-real-cost-report-from-csv.ps1` y `eval/audit_notext_db.py` (repo Batch) compatibles con ambos formatos |
| AB#100171 | Observabilidad | Alerta Azure Monitor `storage_percent` ≥ 80% en BD PRO + dato en dashboard de seguimiento |
| AB#100172 | Validación | Suite `tests/e2e-postdeploy` en DEV, comparativa byte a byte de respuestas Admin, verificación del flujo de duplicados |

### Orden de ejecución propuesto

1. AB#100166 + AB#100167 + AB#100168 (mismo despliegue: persistencia + lectores adaptados).
2. AB#100170 (scripts) y AB#100172 (validación en DEV) antes de promocionar a PRO.
3. AB#100169 (markdown binario) como segunda fase, con su migración por lotes.
4. AB#100171 (alerta) independiente, puede ir en cualquier momento.

### Coordinación con las otras iniciativas en curso

Orden global acordado entre los tres planes activos (todos bajo el Epic 100005):

1. **AB#100176 — Fiabilidad de resumen y persistencia** (primero): bugs de producción con
   incidencia abierta (INC1338832); es el que más toca el orquestador y conviene estabilizarlo
   antes. Su task de alertas (AB#100181) modifica `create-monitor-alerts.ps1`, el mismo script
   donde después se añade la alerta de AB#100171 — no desarrollarlas en paralelo.
2. **AB#100182 — Rendimiento del Monitor** (segundo): con la decisión del timeline ya no depende
   de este plan. Su índice cubriente (AB#100185) se deja preparado y se aplica en PRO **en la
   misma ventana manual** que la migración de AB#100168 (Apply del pipeline Migrations-BD
   bloqueado; `ONLINE=ON` en ambos scripts).
3. **AB#100165 — este plan** (tercero): sin urgencia real tras la ampliación a 20 GB; la task
   AB#100178 (persistir salidas tempranas) del plan de fiabilidad heredará automáticamente la
   regla de no grabar columnas duplicadas si AB#100166 ya está mergeado, y en el orden elegido
   ocurre al revés sin conflicto (los cambios están en ficheros distintos: orquestador vs
   `PersistirActivity`).

Transversal: ramas por PBI desde `develop`, merges secuenciales (sin trabajo en paralelo sobre
orquestador/repositorio), suite e2e completa en DEV tras cada merge y smoke pre-release PRO
(Test Plan 100069) antes de cada subida.

### Impacto esperado

- Crecimiento mensual en campañas tipo agosto: de ~2,3 GB a ~1,5 GB con las columnas eliminadas,
  y ~1,1 GB con el markdown binario (footprint por ejecución de ~42 KB a ~25 KB).
- Ahorro sobre el crecimiento futuro; el histórico existente (2,88 GB) no varía en esta fase.
- Los 20 GB actuales pasan de ~8 meses a ~18 meses de margen al ritmo de agosto, además de la
  alerta preventiva al 80%.

## Fuera de alcance (decisiones futuras)

- Retención/archivado de contratos y timelines históricos (opciones A "contrato vigente por
  documento" y B "archivado a blob con rehidratación" descritas en el análisis).
- Compresión del `ContratoSalidaCompletoJson` retenido.
- Duplicados por referencia (`ReferenciaEjecucionId`) en reejecuciones de documentos existentes.
- Conversión/limpieza del histórico de columnas duplicadas ya grabadas.
