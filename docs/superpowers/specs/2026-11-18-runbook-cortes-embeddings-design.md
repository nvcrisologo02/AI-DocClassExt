# Runbook de cortes recurrentes del clasificador por embeddings — diseño corto

Fecha: 2026-11-18 · Línea: core · Feature AB#99948 · PBI AB#101421 · Task AB#101422 · Estado: aprobado por el usuario 2026-11-18

## Intención

GDC deja un lote nuevo en el share DOCIA cada 7-10 días. Hasta v4, cada corte (v2, v3, v4) llevó spec, plan y PBI propios aunque el flujo es casi el mismo; la cadena solo está en los apartados «Reproducir» de INFORME §13-§15. Se fija un runbook único para que cada corte sea un PBI más su sección del INFORME, sin spec ni plan, con `--cal-ampliada` obligatorio en la puerta y la revisión de `EXCLUIR_CARPETAS` en cada corte.

## Objetivos

- Un documento que una sesión siga de principio a fin para el lote N sin decidir parámetros a mano.
- Las decisiones que en v2-v4 se tomaron sobre la marcha quedan como reglas con su criterio (exclusión de carpetas, ficheros truncados o duplicados, clases por debajo del mínimo, clases nuevas que falten en DEV).
- Plantillas mínimas de lo que se escribe en cada corte: PBI (título, criterios de aceptación) y sección del INFORME (estructura de §15).

## No objetivos

- Script orquestador (enfoque 2, descartado por ahora: todavía aparecen casos de criterio).
- Cambiar la puerta, la tolerancia (0,5 pp), el mínimo del holdout (80 %), la definición de la cal ampliada ni la exclusión de FOTO-05.
- Promoción a PRE/PRO (sigue por AB#100781) y medición de la sombra (fuera del corte).

## Antiobjetivos

- Duplicar la mecánica de publicación del README de migraciones de core: el runbook remite a él.
- Que el runbook sustituya los «sí» de gobernanza: publicar en DEV, escribir en Azure DevOps y merge siguen pidiendo confirmación.

## Enfoques considerados

1. **Runbook en Batch junto a los scripts** (elegido por el usuario): `eval/clasificador-embeddings/RUNBOOK_CORTES.md`. Barato, vive con el código que describe; cada caso raro nuevo se convierte en regla.
2. Runbook más script orquestador (`corte.py --lote N`): quita errores de parámetros pero exige código y tests, y no resuelve los casos de criterio.
3. Runbook en core `docs/procedimientos/`: más visible, pero lejos del código y obliga a tocar dos repos en cada cambio de script.

## Contenido del runbook (decidido)

1. **Disparador y preparación**: lote nuevo en `\\srbsfsp01\VARIOS\DOCIA\para embdeddings\LoteN`; copia a `C:/temp/MVP/corpus-embeddings-v<N+1>`; PBI hijo de AB#99948 creado antes de empezar (con sí).
2. **Tabla de parámetros derivados del lote N** (versión nueva `v<N+1>`, holdout `hold<N+1>`, origen `share-<fecha>-loteN`, inventario base `inventario_v<N>.csv` y salida `inventario_v<N+1>.csv`, script SQL `0<N+2>-modeloconfig-embeddings-v<N+1>.sql`, script de rollback el anterior, `--fuente-etiquetas` acumulando la fecha del lote). Con el Lote4: v5, hold5, script 06.
3. **Cadena de comandos** de §15 parametrizada, con `--cal-ampliada` obligatorio en `puerta_calidad.py` y el control del holdout anterior aparte.
4. **Comprobaciones por corte** con criterio y acción: revisión de `EXCLUIR_CARPETAS` (carpetas de imagen o sin texto útil: el criterio de FOTO-05), duplicados y ficheros ya presentes, ficheros truncados o con error de DI, clases por debajo del mínimo de 5, códigos TDN2 del lote que no existen en DEV (`out/tipologias-dev.json`), ausencia del `AVISO ... holdouts posteriores` en la puerta.
5. **Lectura de la puerta**: condición sobre cal ampliada; serie de `cal` original vigilada a mano con el acumulado desde v2 anotado en cada corte (riesgo de dilución de §16); clases flojas del holdout nuevo anotadas.
6. **Publicación en DEV** (con sí): remite al README de migraciones (blob → script SQL nuevo con backup → ejecución de verificación con `VersionModelo`) y lo que añade el corte: nuevo script `0X` siguiendo el 05 y fila en la tabla del README.
7. **Cierre del corte**: sección nueva del INFORME con la estructura de §15 (corpus, inventario, texto, entrenamiento, puerta, publicación, reproducir con SHA256 de lo que queda fuera de git), evidencias en `docs/auxiliares/temps/<fecha>/` de core, criterios de aceptación tipo del PBI.

## Impacto en otros documentos

- INFORME §16 («Para el siguiente corte»): una línea que remite al runbook.
- README de migraciones de core: una línea que remite al runbook para el flujo completo.
- Sin ADR (sin disparador: procedimiento, no decisión técnica).

## A criterio de quien lo construya

Redacción, orden interno de los apartados, formato de la tabla de parámetros y nivel de detalle de los comandos, siempre que sean los de §15 con los argumentos reales de los scripts.
