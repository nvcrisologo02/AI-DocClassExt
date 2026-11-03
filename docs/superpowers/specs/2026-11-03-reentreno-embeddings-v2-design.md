# Reentreno v2 del clasificador por embeddings con el corpus del share DOCIA

- **Fecha**: 2026-11-03. **Tamaño**: cambio pequeño (diseño corto).
- **Work item**: PBI AB#100780 "Crecimiento y reentreno versionado del clasificador por embeddings" (New). Tasks hijas y un Bug para los HTTP 400 de la sombra se crean en Azure DevOps con sí explícito antes de ejecutar el plan.
- **Antecedentes**: spec `2026-10-01-clasificador-embeddings-hibrido-design.md`, ADR-002, INFORME.md del repo DocumentIA.Batch (`eval/clasificador-embeddings/`). Análisis del corpus en `docs/auxiliares/temps/2026-11-03/analisis-corpus-para-embeddings.md` (fuera de git).

## Intención

El modelo v1 (entrenado el 2026-10-02 con 6.313 documentos, 37 familias, 85 tipologías) no cubre 18 de las 28 tipologías del corpus que el usuario ha dejado en `\\srbsfsp01\VARIOS\DOCIA\para embeddings` (638 ficheros, 29 carpetas por código TDN2). En la sombra de DEV (255 ejecuciones del 02/10 al 22/10) A coincide con el GPT en TDN1 el 83 % cuando la tipología está cubierta y el 30 % cuando no. Se entrena y publica en DEV, en sombra, una versión v2 que incorpore ese corpus.

## Objetivos

1. Artefacto `clasificador-embeddings/v2` con manifiesto (corte de datos, fecha, nDocumentos, tipologías cubiertas) publicado en el storage de DEV y referenciado desde la fila 1026 de `ModeloConfigs`, en modo `sombra`.
2. Tipología `fiav.06` dada de alta en DEV con alcance solo clasificación, con las descripciones de `CatalogoTdn1` (FIAV) y `CatalogoTdn2` (FIAV-06), siguiendo el patrón del seed de AB#101074.
3. Puerta de calidad escrita y ejecutada: v2 no se publica si su TDN1 sobre cal (1.576) es inferior al de v1 (87,2 %) o si el TDN1 medio sobre el holdout de documentos nuevos no llega al 80 %.
4. Proceso repetible: inventario por manifiesto (ruta, código) que no dependa de la convención de nombres del corpus, para que el siguiente crecimiento reutilice los mismos scripts.

## No objetivos

- Calibrar la confianza de A ni activar el modo `hibrido` (restringido o no). v2 declara `calibrado = false`.
- Promocionar v2 a PRE o PRO (cauce de ADR-001 y AB#100781).
- Ingerir el corpus en DEV. La ingestión del holdout renombrado para medir v2 con texto de producción se decide después de la puerta de calidad, como petición nueva.
- Tocar el orquestador, el decisor o el loader del artefacto en core: el formato del artefacto no cambia.
- Corregir el HTTP 400 del endpoint de embeddings: se abre Bug y se trata aparte.

## Anti objetivos

- Publicar un v2 que baje el acierto en cal aunque suba en las clases nuevas.
- Entrenar con documentos cuya etiqueta sea contradictoria o cuya clase no llegue al mínimo.

## Restricciones

- Mínimo del entrenador: 5 documentos por clase (`ClasificadorLR`, `min_ejemplos = 5`, C = 10, `text-embedding-3-large`, 3072 dimensiones, recorte a 24.000 caracteres). No se cambia.
- El exportador cruza las clases con el catálogo de tipologías publicadas de DEV (`sql/tipologias-pares-tdn.sql`): `tipologias-dev.json` se regenera después del alta de `fiav.06`.
- DI Layout y embeddings se pagan en los recursos de DEV: unas 2.560 páginas escaneadas (estimación 15-25 USD) y unos 4 M de tokens de embeddings (menos de 1 USD). Se reutiliza la caché `C:/temp/MVP/spike-embeddings-cache` (contiene texto sensible; no se borra).
- El nombre de fichero lleva la etiqueta: cualquier ingestión futura en DEV renombra antes.
- Escrituras en Azure DevOps, en la BD de DEV (alta de tipología, `ModeloConfigs`) y en el storage de DEV solo con sí explícito.

## Decisiones del usuario (2026-11-03)

- FIAV-06 (18 docs): se da de alta `fiav.06`, solo clasificación, descripciones de los catálogos.
- ESIN-AI (2 docs): fuera, no llega al mínimo. Regla general: una clase con menos de 5 documentos útiles no entra.
- CERT-05-Etiqueta (9 docs): se trata como CERT-05.
- Duplicado entre carpetas (LIPR-06_3.PDF = LIPR-13_31.pdf): se excluyen los dos ficheros. Los 11 duplicados dentro de su carpeta se dejan en una copia.
- Puerta de calidad: TDN1 en cal ≥ v1 y TDN1 medio ≥ 80 % en el holdout nuevo. Sin listón por clase.
- Encaje en AB#100780 y Bug aparte para los HTTP 400.

## Enfoques considerados

1. **Ampliar la cadena del spike en DocumentIA.Batch** (elegido): inventario por manifiesto, DI Layout y embeddings con caché, entrenamiento con train de v1 más los nuevos, exportación v2, script de puerta de calidad. Fuera de línea, barato, código probado. Cede paridad de texto con la pipeline (igual que v1).
2. Ingerir el corpus en DEV y entrenar con el markdown persistido: paridad de texto y alimenta la sombra, pero paga GPT, mete 626 documentos de prueba en DEV, depende de su estado y contamina la sombra con documentos de entrenamiento.
3. Enfoque 1 más ingestión posterior del holdout en DEV: queda como decisión posterior, fuera de este diseño.

## Cómo se hace (lo decidido)

1. **Saneado e inventario** (Batch, `eval/clasificador-embeddings/`): script nuevo `inventario_manifiesto.py` que recorre el share, aplica las exclusiones (ESIN-AI, duplicados MD5 dentro de carpeta, los dos LIPR cruzados), funde CERT-05-Etiqueta en CERT-05, deriva TDN1 del código, calcula SHA256 y páginas, y escribe `CACHE/inventario_v2.csv` con la misma estructura que `inventario.csv` (`sha256;rel_path;tdn1;tdn2;particion;paginas`) más la columna `origen = share-2026-11-03`. Los ficheros se copian a un corpus local (`C:/temp/MVP/corpus-embeddings-v2`) para no trabajar sobre el share.
2. **Particiones**: holdout estratificado del 20 % (semilla fija) solo en clases con 10 o más documentos; las clases con 5-9 van enteras a `train`. El holdout se llama `hold2` y no se mezcla con `cal` ni con `golden`, que no cambian.
3. **Texto y vectores**: `di_layout.py` para los PDF sin texto extraíble, los cifrados que pypdf no abra y las 7 imágenes; `embeddings.py` sobre todo el inventario v2. Ambos con la caché existente (reanudables).
4. **Entrenamiento y exportación**: `exportar_modelo.py --version v2` entrena con `train` de v1 más `train` de v2 y escribe `out/clasificador-embeddings-v2.json` y `paridad-v2.json`. El manifiesto añade `cortes` (lista de orígenes con fecha y número de documentos) y `fuenteEtiquetas`.
5. **Puerta de calidad**: script `puerta_calidad.py` que evalúa v1 y v2 sobre cal y v2 sobre hold2, imprime TDN1, TDN2, ECE y cobertura por umbral, y devuelve código de salida distinto de cero si falla cualquiera de las dos condiciones. El resultado se anexa al INFORME.md (§13).
6. **Alta de `fiav.06`** (core, `scripts/database/`): script idempotente con `@SoloSimular = 1` por defecto, mismo patrón que `seed-tipologias-ab101074.sql`, descripciones leídas de los catálogos en la propia consulta. Se ejecuta en DEV con sí. PRE y PRO llegan por el alta por clave natural (AB#101075), como las 19 anteriores.
7. **Publicación en DEV** (con sí): subir `v2/` al contenedor `documentai` de `srbstgdevdocai` (`modelos/clasificador-embeddings/v2/`), verificar MD5, actualizar la fila 1026 de `ModeloConfigs` para apuntar a v2 manteniendo `sombra`. v1 se conserva en el blob.
8. **Tests**: test de `inventario_manifiesto.py` sobre un árbol sintético (exclusiones, fusión, particiones), test de `exportar_modelo.py` ampliado al manifiesto nuevo, test de `puerta_calidad.py` con métricas sintéticas por encima y por debajo del listón. Los tests de core no cambian (formato del artefacto intacto); se ejecuta `dotnet test DocumentIA.Tests.Unit` para confirmar.
9. **Bug HTTP 400**: se crea con los 15 casos (documentos, fechas, mensaje) y la hipótesis del tope de 8.192 tokens frente al recorte por caracteres. No se resuelve en este trabajo.

## Mis decisiones (del asistente, corregibles)

- Holdout solo en clases con 10 o más documentos, para no dejar ninguna clase por debajo del mínimo de 5 en train.
- Umbral de 200 caracteres en las tres primeras páginas para considerar un PDF escaneado y mandarlo a DI Layout, el mismo criterio del análisis.
- Corpus local copiado desde el share en vez de leer por SMB en cada paso.
- El origen de las etiquetas se registra en el manifiesto como `fuenteEtiquetas`; el valor lo da el usuario antes de exportar (pendiente de respuesta).

## Lo que queda al constructor

- Nombre exacto de los campos nuevos del manifiesto y cómo los ignora el loader de core sin fallar (comprobar que el deserializador tolera campos desconocidos antes de publicar).
- Orden de los scripts y lanzadores `-Detached` para DI y embeddings, como en el spike.
- Formato del anexo §13 del INFORME.md.

## Verificación de cierre

- `python -m pytest eval/clasificador-embeddings/tests` en Batch en verde.
- `puerta_calidad.py` con salida 0 y cifras copiadas en INFORME.md §13.
- Fila 1026 de `ModeloConfigs` apuntando a v2 en `sombra`; una ejecución en DEV con `VersionModelo = v2` en el contrato persistido.
- Session Log y memoria actualizados; commits con `AB#100780` (y el AB# del Bug donde toque).

## Estado (2026-11-03, tras la ejecución del plan)

- Objetivo 1 cumplido: v2 (6.809 documentos, 39 familias, 102 tipologías, `cortes` y `fuenteEtiquetas` en el manifiesto) publicado en `srbstgdevdocai/documentai/modelos/clasificador-embeddings/v2/` y fila 1026 de `ModeloConfigs` apuntada con el script `scripts/migrations/clasificador-embeddings/03-modeloconfig-embeddings-v2.sql`, modo `sombra`.
- Verificación de cierre en DEV: ejecución 10892 (InstanceId 0f208f8ad8e34e10a406204bd0691f8f, 2026-11-03 13:02 UTC, documento sintético enviado para la prueba) con `VersionModelo = v2`, `Modo = sombra`, A = escr.10 con confianza 0,976, misma tipología que el GPT (ESCR-10), sin error.
- Objetivo 2 cumplido: `fiav.06` dada de alta en DEV con `scripts/database/seed-tipologia-fiav06-ab100780.sql`.
- Objetivo 3 cumplido: `puerta_calidad.py` → PASA. TDN1 en cal 87,4 % (v1 87,2 %); TDN1 en hold2 (121 docs nuevos) 90,9 % (v1 67,8 %). Cifras en INFORME.md §13 del repo DocumentIA.Batch.
- Objetivo 4 cumplido: `inventario_manifiesto.py` (manifiesto por carpeta, exclusiones, particiones) y `--inventario` en `texto.py`, `di_layout.py`, `exportar_modelo.py` y `puerta_calidad.py`.
- Fuera de este trabajo, como estaba previsto: ingestión del holdout en DEV, calibración, modo híbrido, PRE y PRO (AB#100781). El Bug de los HTTP 400 es AB#101245 (creado el 2026-11-03 con los 15 casos y la hipótesis sin verificar).
- Desvíos respecto al diseño: 2 ficheros ESIN-34 de v1 sin texto ya en v1 siguen sin él (no están en el corpus nuevo); los artefactos `out/*-v2.json` no se versionan (carpeta ignorada por git, como v1): viven en el blob y sus métricas en INFORME §13.
