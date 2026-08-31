# Optimización de almacenamiento BD PRO (AB#100165)

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

### Garantía de no impacto en consumidores (verificada)

- La respuesta HTTP de la API se construye en memoria; nunca lee las columnas eliminadas.
- El flujo de duplicados (`ObtenerUltimaEjecucionDuplicadoActivity`) usa exclusivamente
  `ContratoSalidaCompletoJson`, que se mantiene.
- `sp_ObtenerDocumentoEjecucionesPorIdActivo`: sin ejecuciones en caché de procedimientos ni en
  Query Store (~30 días) en PRO. Se reescribe de forma defensiva conservando la forma del resultset.
- El detalle de ejecución de Admin ya se monta desde el contrato; la lista se adapta en servidor
  devolviendo JSON idéntico (la webapp Blazor no se modifica).
- El histórico conserva sus columnas rellenas: solo se deja de grabar, no se borra nada.

### Tasks

| WI | Título | Resumen |
|---|---|---|
| AB#100166 | Persistencia | `PersistirActivity` deja a NULL `ActivityTimelineJson`, `DatosFinalesJson`, `DatosOriginalesJson` en filas nuevas |
| AB#100167 | Admin API | Lista de ejecuciones lee el timeline del contrato con fallback a la columna antigua |
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
