# Reentreno v3 del clasificador por embeddings con el Lote2 del share DOCIA

- **Fecha**: 2026-11-05. **Línea**: core (ejecución en DocumentIA.Batch y core).
- **Work item**: PBI AB#101313 bajo la Feature AB#99948, con las tasks AB#101314 a AB#101317 (creadas el 2026-11-05). AB#100780 (v2) está Done y no se reabre.
- **Tamaño**: cambio pequeño. El flujo, los scripts y la puerta de calidad existen desde v2 (spec `2026-11-03-reentreno-embeddings-v2-design.md`, INFORME §13 del Batch).

## Intención

El equipo de GDC dejó el 2026-11-04 un segundo corte en `\\srbsfsp01\VARIOS\DOCIA\para embdeddings\Lote2` (692 PDF, 745 MB, 20 carpetas por código TDN2, mismo layout `CODIGO/CODIGO_n.pdf`). De esos 20 códigos, 10 no tienen ningún documento en el train de v2 y 6 tienen entre 1 y 5; solo 4 pasan de 5. Son tipologías en las que el modelo A no contesta y todo cae al GPT. Se entrena y publica en DEV, en sombra, una versión v3 que las incorpore.

## Objetivos

1. Artefacto `clasificador-embeddings/v3` publicado en el storage de DEV y referenciado desde la fila 1026 de `ModeloConfigs`, en modo `sombra`, con manifiesto de tres cortes (`corpus-v1`, `share-2026-11-03`, `share-2026-11-04-lote2`).
2. Puerta de calidad ejecutada y pasada: v3 no se publica si su TDN1 sobre cal baja más de 0,5 puntos frente al de v2 (87,4%) o si el TDN1 sobre el holdout nuevo del lote 2 no llega al 80%. (Criterio de cal corregido el 2026-11-05 tras la primera ejecución de la puerta; ver "Mis decisiones".)
3. INFORME §14 con inventario, exclusiones, coste y puerta; estado de cierre en esta spec.

## No objetivos

- Ingerir el lote 2 en DEV, calibrar, modo híbrido, PRE y PRO (AB#100781): fuera, como en v2.
- Esperar a que termine la medición de sombra de v2 (decisión del usuario, 2026-11-05): v3 la sustituye en cuanto pase la puerta; la sombra que importa es la del modelo vigente y la comparación v2/v3 se hace fuera de línea.
- Tocar el loader de core: el formato del artefacto no cambia.

## Anti objetivos

- Publicar sin puerta o con puerta fallida. Perder la traza por lote en el manifiesto. Dejar texto del share en ningún sitio fuera de la caché local ya existente.

## Restricciones

- Las decisiones de v2 se mantienen: etiquetas desde el nombre de la carpeta, mínimo 5 documentos por clase, holdout del 20% estratificado (semilla 42) solo en clases con 10 o más, `cal` y `golden` intactos, duplicados por hash fuera (dentro del lote y contra v1 y lote 1), DI Layout con tope de 5 páginas, `fuenteEtiquetas` con el mismo texto cambiando la fecha.
- DI Layout en DEV se lanza con sí explícito (coste estimado 20 USD si la proporción de escaneados es como en el lote 1; se confirma con `--estimar`). Publicación en blob y script SQL con sí explícito.
- Sin push, merge ni escritura en Azure DevOps sin sí explícito. Commits con el AB# del PBI nuevo.

## Enfoques considerados

1. **Corte incremental sobre v2** (elegido): `inventario_manifiesto.py --base inventario_v2.csv`, mismo flujo, `exportar_modelo.py --version v3`, puerta v2 contra v3. Conserva la traza por lote y reutiliza todo.
2. Reentrenar desde cero fundiendo los dos lotes en un corte: mismo resultado, pierde la traza. Descartado.
3. Ingerir el lote 2 en DEV y entrenar con el markdown de producción: descartado ya en v2 (coste GPT, contamina la sombra, depende del estado de DEV).

## Cómo se hace (lo decidido)

1. **Corpus local**: copiar el lote 2 a `C:/temp/MVP/corpus-embeddings-v3` (no se trabaja sobre el share).
2. **Inventario** (Batch): `inventario_manifiesto.py --corpus ... --base CACHE/inventario_v2.csv --salida CACHE/inventario_v3.csv --origen share-2026-11-04-lote2 --holdout hold3`. Cambio mínimo en el script: parámetro `--holdout` (por defecto `hold2`) para nombrar la partición del holdout nuevo. Las filas base conservan su partición y origen. Resultado esperado: unos 690 documentos nuevos repartidos en `train` y `hold3`.
3. **Texto y vectores**: `texto.py --inventario CACHE/inventario_v3.csv` (reutiliza markdown de BD por hash), `di_layout.py --estimar` y, con sí, `--lanzar` para los sin texto; `embeddings.py` sobre el inventario v3. Caché existente, reanudable.
4. **Exportación**: `exportar_modelo.py --version v3 --inventario CACHE/inventario_v3.csv --tipologias out/tipologias-dev.json --fuente-etiquetas "Documentos seleccionados y certificados por el equipo de GDC (share DOCIA, lotes 2026-11-03 y 2026-11-04)"`. `tipologias-dev.json` del 2026-11-03 sigue valiendo: los 20 códigos ya existen en DEV (comprobado el 2026-11-05).
5. **Puerta de calidad**: `puerta_calidad.py --anterior out/clasificador-embeddings-v2.json --nuevo out/clasificador-embeddings-v3.json --inventario CACHE/inventario_v3.csv --holdout hold3 --salida out/puerta-calidad-v3.json`. Cambio mínimo en el script: parámetro `--holdout` (por defecto `hold2`) y etiquetas de salida "anterior/nuevo" en vez de v1/v2 donde se impriman. Se añade `hold2` como información (v3 no debería bajar frente a v2), sin ser condición de paso.
6. **Publicación en DEV** (con sí): subir `v3/` a `srbstgdevdocai/documentai/modelos/clasificador-embeddings/v3/`, verificar MD5, script `04-modeloconfig-embeddings-v3.sql` (copia del 03 con la ruta v3, mismo guardarrail de servidor, backup e idempotencia), README de `scripts/migrations/clasificador-embeddings/` actualizado. v1 y v2 se conservan en el blob (rollback: repetir con la ruta v2).
7. **Verificación de cierre**: una ejecución en DEV con `VersionModelo = v3` y `Modo = sombra` en el contrato persistido; `pytest` del Batch en verde con los tests ampliados (`--holdout` en inventario y puerta); `dotnet test DocumentIA.Tests.Unit` sin cambios esperados.
8. **Documentación**: INFORME §14 (Batch), estado de cierre en esta spec, README de migraciones (core), memoria del corpus.

## Mis decisiones (del asistente, corregibles)

- Holdout del lote 2 en partición propia `hold3`, para que la condición "holdout nuevo al 80%" mida solo documentos que v2 no vio, y `hold2` quede como control de regresión. Alternativa descartada: mezclarlo en `hold2`, que diluiría la medida con documentos que v2 ya aprendió.
- Mismo texto de `fuenteEtiquetas` con las dos fechas, en vez de un texto por corte: el manifiesto ya lleva los cortes por origen.
- Reutilizar `tipologias-dev.json` en vez de reexportarlo.
- **Tolerancia de 0,5 puntos en cal (decisión del usuario, 2026-11-05).** La primera ejecución de la puerta falló por cal: v3 87,1% frente a v2 87,4% (1.372 frente a 1.377 aciertos de 1.576), con hold2 91,7% y hold3 95,5% TDN1 / 93,3% TDN2. El análisis documento a documento (`docs/auxiliares/temps/2026-11-05/analisis-cal-perdidos-v3.txt`) mostró 23 discordantes (14 a favor de v2, 9 a favor de v3; McNemar exacto p = 0,40), todos casos de frontera con confianza de v2 entre 0,19 y 0,61, y solo 5 atraídos por clases del lote 2. Se cambia el criterio de cal a "no bajar más de 0,5 puntos frente al anterior" (por debajo de la desviación muestral de cal, ±0,85 pp) en esta spec y en `puerta_calidad.py` (parámetro `--tolerancia-cal`, por defecto 0,005, con test). Riesgo asumido: la tolerancia puede acumularse versión a versión; se mitiga dejando la serie v1/v2/v3 de cal en el INFORME y vigilando la sombra. Alternativas descartadas: criterio estadístico (McNemar p ≥ 0,05), más riguroso pero más difícil de explicar; no publicar v3.

## Lo que queda al constructor

- Nombres de los parámetros nuevos y actualización de las cadenas de uso de los dos scripts.
- Lanzadores `-Detached` para DI y embeddings, como en v2.
- Formato de §14 del INFORME, calcado del §13.

## Verificación de cierre

- `out/puerta-calidad-v3.json` con `PASA` y cifras en INFORME §14.
- Fila 1026 apuntando a v3 en `sombra`; ejecución en DEV con `VersionModelo = v3`.
- Commits con el AB# del PBI en Batch y core; integración con sí.

## Estado de cierre (2026-11-05)

- Inventario: 692 PDF → 677 nuevos (7 duplicados MD5, 8 ya en v1 o lote 1), 543 `train` / 134 `hold3`, 20 TDN2. Texto: 2 desde la BD de PRO y 674 por DI Layout en DEV (1.813 páginas facturables estimadas, ~19 USD); `ESCR-06/ESCR-06_39.pdf` truncado en el propio share (HTTP 400 de DI), excluido. Embeddings: 676 documentos, 1,14 M de tokens.
- v3: 7.351 documentos de train (cortes 6.313 / 496 / 542), 41 familias y 117 tipologías. Puerta: cal 87,1% (v2 87,4%), hold2 91,7% (v2 90,9%), hold3 95,5% TDN1 y 93,3% TDN2 (v2 53,0% / 6,7%). `PUERTA: PASA` con la tolerancia de 0,5 pp decidida tras el análisis (ver "Mis decisiones"); copia de `puerta-calidad-v3.json` y del análisis en `docs/auxiliares/temps/2026-11-05/`.
- Publicado en DEV con autorización: blobs `v3/` en `srbstgdevdocai/documentai` (MD5 verificado), script 04 ejecutado (backup `ModeloConfigs__bak_20261105_094110`), fila 1026 en v3 y `sombra`. Ejecución 11134 en DEV (09:44 UTC) con `VersionModelo = v3`, confianza 0,979, decisión DerivarGpt.
- Código: Batch `--holdout` en `inventario_manifiesto.py` (1272e05), puerta parametrizada (16d46ac), tolerancia en cal (5215fc0), INFORME §14; core script 04 y README de migraciones (4b19fd3), esta spec. `pytest` del Batch: 108 passed.
- Queda: medir la sombra de v3 en DEV (bloque 1 de `scripts/analysis/sombra-embeddings.sql` con `VersionModelo = v3`, ~2 semanas o 500 ejecuciones); PRE y PRO por AB#100781; avisar a GDC del PDF truncado ESCR-06_39.
