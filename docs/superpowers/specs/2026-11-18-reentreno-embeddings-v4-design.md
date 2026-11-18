# Reentreno v4 del clasificador por embeddings con el Lote3 del share DOCIA

- **Fecha**: 2026-11-18. **Línea**: core (ejecución en DocumentIA.Batch y core).
- **Work item**: PBI AB#101405 bajo la Feature AB#99948, con las tasks AB#101406 a AB#101408 (creadas el 2026-11-18). AB#101313 (v3) está Done y no se reabre.
- **Tamaño**: cambio pequeño. Tercer corte incremental con el mismo flujo, scripts y puerta de v2 y v3 (specs `2026-11-03-reentreno-embeddings-v2-design.md` y `2026-11-05-reentreno-embeddings-v3-design.md`, INFORME §13 y §14 del Batch).
- **Cadencia**: el usuario fija el 2026-11-18 que GDC irá dejando lotes cada 7-10 días para ampliar las tipologías cubiertas. Esta spec sirve de plantilla para los cortes siguientes; si el flujo se repite sin cambios, el siguiente corte puede documentarse en un runbook en vez de en una spec nueva (decisión para la siguiente sesión, fuera de este corte).

## Intención

El equipo de GDC dejó el 2026-11-18 un tercer corte en `\\srbsfsp01\VARIOS\DOCIA\para embdeddings\Lote3`: 561 ficheros (533 PDF, 28 JPG en FOTO-05), unos 630 MB, en 20 carpetas por código TDN2. Los nombres cambian de formato (`CODIGO_ (n).pdf`, con extensión `.PDF` a veces en mayúsculas); el inventario los admite. Los 20 códigos existen en DEV. De ellos, 6 no tienen ningún documento en el train de v3 (CONV-01, FICH-04, FOTO-05, LICM-15, PRPE-07, SEGU-11), 8 tienen entre 1 y 5 y 6 pasan de 5. Se entrena y publica en DEV, en sombra, una versión v4 que los incorpore.

## Objetivos

1. Artefacto `clasificador-embeddings/v4` publicado en el storage de DEV y referenciado desde la fila 1026 de `ModeloConfigs`, en modo `sombra`, con manifiesto de cuatro cortes (`corpus-v1`, `share-2026-11-03`, `share-2026-11-04-lote2`, `share-2026-11-18-lote3`).
2. Puerta de calidad ejecutada y pasada con el criterio vigente: TDN1 sobre cal no baja más de 0,5 puntos frente a v3 (87,1%) y TDN1 sobre el holdout nuevo `hold4` llega al 80%. `hold2` y `hold3` se informan como control, sin ser condición.
3. Última foto de la sombra de v3 (bloque 1 de `scripts/analysis/sombra-embeddings.sql` con `VersionModelo = v3`) tomada antes de publicar v4 y recogida en el INFORME, aunque tenga pocas ejecuciones.
4. INFORME §15 con inventario, exclusiones, coste, sombra v3 y puerta; estado de cierre en esta spec.

## No objetivos

- Esperar a medir la sombra de v3 (decisión del usuario, 2026-11-18): el objetivo es ampliar tipologías, v4 sustituye a v3 en cuanto pase la puerta.
- Ingerir el lote 3 en DEV, calibrar, modo híbrido, PRE y PRO (AB#100781). Tocar el loader de core (el formato del artefacto no cambia). Crear el runbook de cortes recurrentes.

## Anti objetivos

- Publicar sin puerta o con puerta fallida. Perder la traza por lote en el manifiesto. Dejar texto del share fuera de la caché local ya existente.

## Restricciones

- Decisiones de v2 y v3 que se mantienen: etiquetas desde el nombre de la carpeta, mínimo 5 documentos por clase, holdout del 20% estratificado (semilla 42) en clases con 10 o más, en partición propia; `cal` y `golden` intactos; duplicados por hash fuera (dentro del lote y contra v1-v3); DI Layout con tope de 5 páginas; tolerancia de 0,5 pp en cal (no se rediscute; suelo absoluto descartado); `fuenteEtiquetas` con el mismo texto añadiendo la fecha.
- DI Layout en DEV se lanza con sí explícito (estimación con `--estimar`; orden de magnitud 15 USD). Publicación en blob y script SQL con sí explícito. Sin push, merge ni escritura en Azure DevOps sin sí explícito. Commits con el AB# del PBI nuevo.
- La rama activa de core es `feature/99934-gpt56-luna-dev` (otro tema, desplegada en DEV): el trabajo de core va en rama propia desde `develop` (c5978ab, 4 commits sin push) y no toca la de luna.

## Enfoques considerados

1. **Corte incremental sobre v3** (elegido): `inventario_manifiesto.py --base inventario_v3.csv`, mismo flujo, `exportar_modelo.py --version v4`, puerta v3 contra v4. Conserva la traza por lote y reutiliza todo; ningún script necesita cambios funcionales.
2. Medir la sombra de v3 hasta las 500 ejecuciones y publicar v4 después: descartado por el usuario (prioridad a ampliar tipologías; la sombra se fotografía y se sigue).
3. Reentrenar desde cero o cambiar el entrenador: no es incremental. Descartado.

## Cómo se hace (lo decidido)

1. **Corpus local**: copiar el Lote3 a `C:/temp/MVP/corpus-embeddings-v4`. Hallazgos ya conocidos (2026-11-18, SHA256): 4 duplicados internos (CERT-22_ (6)=(7); PRPE-07_ (4)=(19), (6)=(20), (8)=(18)) y 5 ya en cortes anteriores (CERJ-25_ (1) y (21) en v1; CERT-22_ (6), (7) y (9) en el lote 1). Quedan unos 552 nuevos.
2. **Inventario** (Batch): `inventario_manifiesto.py --corpus C:/temp/MVP/corpus-embeddings-v4 --base CACHE/inventario_v3.csv --salida CACHE/inventario_v4.csv --origen share-2026-11-18-lote3 --holdout hold4`.
3. **Texto y vectores**: `texto.py --inventario CACHE/inventario_v4.csv` (markdown de BD por hash), `di_layout.py --estimar` y con sí `--lanzar --max-paginas 5`; `embeddings.py --inventario ...`. Caché reanudable; no se repiten DI ni embeddings ya cacheados.
4. **FOTO-05 (opción A, decisión del usuario)**: los 28 JPG entran en el inventario y pasan por DI Layout. Tras DI, si la mayoría vuelve sin texto útil, la tipología se excluye del train (vía `EXCLUIR_FICHEROS` o exclusión por `tdn2` en el inventario, lo que resulte menos invasivo) y se anota en §15 con el recuento; si DI saca texto, se queda. Lo mismo para cualquier fichero que DI rechace (como ESCR-06_39 en v3): excluido y anotado.
5. **Sombra v3**: antes del paso 7, ejecutar el bloque 1 de `sombra-embeddings.sql` contra DEV y guardar la salida en `docs/auxiliares/temps/2026-11-18/` para §15.
6. **Exportación y puerta**: `exportar_modelo.py --version v4 --inventario CACHE/inventario_v4.csv --tipologias out/tipologias-dev.json --fuente-etiquetas "Documentos seleccionados y certificados por el equipo de GDC (share DOCIA, lotes 2026-11-03, 2026-11-04 y 2026-11-18)"`; `puerta_calidad.py --anterior out/clasificador-embeddings-v3.json --nuevo out/clasificador-embeddings-v4.json --inventario CACHE/inventario_v4.csv --holdout hold4 --salida out/puerta-calidad-v4.json`. `tipologias-dev.json` del 2026-11-03 sigue valiendo (los 20 códigos existen en DEV, comprobado el 2026-11-18).
7. **Publicación en DEV** (con sí): subir `v4/` a `srbstgdevdocai/documentai/modelos/clasificador-embeddings/v4/`, verificar MD5, script `05-modeloconfig-embeddings-v4.sql` (copia del 04 con la ruta v4, mismo guardarrail, backup e idempotencia), README de `scripts/migrations/clasificador-embeddings/`. v1-v3 se conservan en el blob (rollback: repetir con la ruta v3).
8. **Verificación de cierre**: una ejecución en DEV con `VersionModelo = v4` y `Modo = sombra` (ingesta con `tests/api-tests/documentia-e2e-common.ps1` y el sintético de `tests/e2e-postdeploy/corpus/generico/`); `pytest` del Batch en verde; `dotnet test DocumentIA.Tests.Unit` sin cambios esperados.
9. **Documentación**: INFORME §15 (Batch), estado de cierre en esta spec, README de migraciones (core), memoria del corpus.

## Mis decisiones (del asistente, corregibles)

- Holdout del lote 3 en partición propia `hold4`, por la misma razón que `hold3` en v3.
- `--origen share-2026-11-18-lote3` con la fecha de hoy: las carpetas del share llevan fecha de hoy aunque los ficheros conserven la del 2026-11-04.
- Exclusión de FOTO-05 decidida por dato tras DI y no de antemano (opción A del usuario). El umbral de "sin texto útil" lo fija el constructor con el mismo criterio que usa `texto.py` para marcar pendientes de DI.
- Sin cambios funcionales en los scripts; si alguno aparece (por ejemplo los nombres con espacio y paréntesis dan problemas en algún paso), se corrige con test y se anota aquí.

## Lo que queda al constructor

- Lanzadores `-Detached` para DI y embeddings, como en v2 y v3.
- Formato de §15 del INFORME, calcado del §14, más el apartado de la sombra v3.

## Verificación de cierre

- `out/puerta-calidad-v4.json` con `PASA` y cifras en INFORME §15.
- Fila 1026 apuntando a v4 en `sombra`; ejecución en DEV con `VersionModelo = v4`.
- Commits con el AB# del PBI en Batch y core; integración con sí.

## Estado de cierre (2026-11-18)

- Inventario: 561 ficheros → 525 nuevos (4 duplicados SHA256, 4 ya en v1 o lote 1, `Thumbs.db` y la carpeta FOTO-05 excluida), 421 `train` / 104 `hold4`, 19 TDN2. Texto: 2 desde la BD de PRO y 551 por DI Layout en DEV (1.939 páginas facturables estimadas, ~20 USD; 18 vacíos, todos JPG de FOTO-05; 3 errores por restos antiguos sin fichero). Embeddings: 525 documentos, 1,28 M de tokens.
- FOTO-05 (opción A): 1 de 28 fotografías con texto útil (≥ 200 caracteres); como los 10 JPG con restos sí habrían dado vector, la carpeta se excluye en `EXCLUIR_CARPETAS` de `inventario_manifiesto.py` con test (Batch 4bffc38). Decisión del asistente dentro de la opción A; se puede revertir quitando la constante.
- v4: 7.772 documentos de train (cortes 6.313 / 496 / 542 / 421), 44 familias y 129 tipologías. Puerta: cal 86,7% (v3 87,1%; −0,4 pp, dentro de la tolerancia), hold2 92,6%, hold3 94,8% (control), hold4 82,7% TDN1 y TDN2 (v3 44,2% / 4,8%). `PUERTA: PASA`. Flojas en hold4: CULC-03 0/6, PRYT-11 2/6, PRPE-07 3/7, CERJ-25 2/4.
- Por qué baja cal (pregunta del usuario al autorizar la publicación): 10 discordantes v3/v4 (8 a favor de v3, McNemar p = 0,11), casos de frontera salvo ESIN-40 atraído por CULC-03. Causa estructural: cal solo tiene la distribución original y cada clase nueva solo puede restarle aciertos; serie v1 87,25 / v2 87,37 / v3 87,06 / v4 86,68 con 37 / 39 / 41 / 44 clases TDN1. Opciones anotadas en INFORME §15 (enriquecer cal, tope acumulado, seguir la sombra), sin decidir.
- Foto de la sombra v3: 970 ejecuciones sin errores, de las que ~890 son el golden de la prueba luna del 17-18/11 (AB#99934) y ~26 tráfico real; no sirve para comparar v2/v3. Registrada en §15.
- Publicado en DEV con autorización: blobs `v4/` (MD5 `D4pAmIk65hutLezcu/9BFg==` verificado), script 05 ejecutado (backup `ModeloConfigs__bak_20261118_110058`), fila 1026 en v4 y `sombra`. Ejecución 12130 en DEV (11:04 UTC, caso E2E único con el sintético) con `VersionModelo = v4`, confianza 0,974, decisión DerivarGpt, sin error.
- Código: Batch `EXCLUIR_CARPETAS` + test (4bffc38) e INFORME §15; core script 05 + README (1e14638) y esta spec. `pytest` del Batch: 109 passed.
- Queda: medir la sombra de v4 (~2 semanas o 500 ejecuciones de tráfico real, excluyendo golden); PRE y PRO por AB#100781; decidir qué hacer con la serie de cal antes del siguiente corte (opciones en §15); runbook para cortes recurrentes (cadencia 7-10 días); avisar a GDC de ESCR-06_39 (pendiente desde v3).
