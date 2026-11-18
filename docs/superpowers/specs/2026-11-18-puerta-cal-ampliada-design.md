# Cal ampliada en la puerta de calidad del clasificador por embeddings

- **Fecha**: 2026-11-18. **Línea**: core (ejecución en DocumentIA.Batch; en core solo esta spec).
- **Work item**: PBI AB#101418 bajo la Feature AB#99948 (creado el 2026-11-18 con sí). Continúa AB#101405 (v4, Done).
- **Tamaño**: cambio pequeño. Un parámetro en `puerta_calidad.py` con test, una pasada de la puerta v3→v4 con el parámetro para dejar la referencia, e INFORME.

## Intención

La métrica de regresión de la puerta se mide sobre `cal` (1.576 documentos de la distribución original, 2026-09). Las tipologías que GDC añade lote a lote no están en cal, así que cada clase nueva solo puede restar aciertos ahí y la serie baja por construcción (v1 87,25 % → v4 86,68 % con 37 → 44 clases; INFORME §15). El usuario decidió el 2026-11-18 enriquecer cal con documentos de las clases nuevas (opción a de §15) antes del siguiente lote.

## Objetivos

1. La condición de regresión de la puerta se evalúa sobre una **cal ampliada** = `cal` ∪ holdouts de los cortes **anteriores** al que se evalúa (hoy `hold2` y `hold3` para la puerta v3→v4; `hold2`, `hold3` y `hold4` para la de v4→v5), con la misma tolerancia de 0,5 pp. El holdout del corte nuevo sigue aparte con su condición de 80 % y **no** entra en la cal ampliada, porque el modelo anterior no vio esas clases y meterlo haría la comparación trivial (el nuevo ganaría siempre) y enmascararía una regresión real en cal.
2. `cal` original se sigue evaluando e imprimiendo en cada puerta, como serie histórica informativa (v1 87,25 / v2 87,37 / v3 87,06 / v4 86,68), sin ser condición.
3. Referencia inicial: la puerta v3→v4 repetida con cal ampliada (`cal` ∪ `hold2` ∪ `hold3`, 1.831 documentos), recogida en el INFORME junto con la decisión.

## No objetivos

- Reentrenar nada ni tocar los artefactos v1-v4 publicados. Ingerir o etiquetar tráfico real de PRO (enfoque 3, descartado por coste y plazo). Reservar parte de cada lote para cal en vez de holdout (enfoque 2, descartado: pierde el holdout como medida separada). Cambiar la tolerancia (0,5 pp) ni el mínimo del holdout (80 %).

## Anti objetivos

- Que la cal ampliada incluya el holdout del corte evaluado. Que deje de publicarse la serie de cal original. Que la puerta de v2 o v3 deje de poder reproducirse tal como está documentada en §13 y §14.

## Restricciones

- Sin cambios en el entrenador, el exportador ni el formato del artefacto. Tests con `.venv/Scripts/python.exe -m pytest tests -q` (hoy 109). Commits con el AB# del PBI nuevo. Sin push ni merge sin sí.

## Enfoques considerados

1. **Cal ampliada con los holdouts anteriores** (elegido por el usuario, 2026-11-18): coste cero (texto y vectores ya en caché), disponible antes del Lote4, mantiene la serie original. Trade-off: mezcla tráfico real con documentos limpios seleccionados por GDC; la tolerancia se aplica sobre un conjunto algo mayor y más fácil.
2. Reservar en cada lote una parte para cal en vez de holdout: misma idea hacia delante, pierde la medida separada del lote. Descartado.
3. Muestrear tráfico real de PRO de las clases nuevas y etiquetar a mano: la única que mide la distribución real, pero semanas de validación humana. Descartado para este corte; queda como idea si la serie sigue bajando.

## Cómo se hace (lo decidido)

1. **`puerta_calidad.py`**: nuevo argumento `--cal-ampliada` (`store_true`). Nueva función `particiones_cal_ampliada(inv: dict, holdout: str) -> list[str]` que devuelve `["cal"]` más todas las particiones del inventario que empiezan por `hold`, ordenadas, excepto `holdout`. Con el flag, la puerta evalúa una partición virtual `cal_ampliada` (concatenación de esas particiones) para anterior y nuevo, la imprime en la tabla junto a `cal`, `hold2`, el holdout y el resto de holds, y pasa a `veredicto` el TDN1 de `cal_ampliada` en vez del de `cal`. En el JSON de salida: clave `cal_ampliada` con `anterior`/`nuevo` y `particiones`, y clave `condicion_cal` = `"cal_ampliada"` o `"cal"`. Sin el flag, comportamiento idéntico al actual (los comandos de §13 y §14 siguen valiendo).
2. **Tests** (`tests/test_puerta_calidad.py`): `particiones_cal_ampliada` con un inventario sintético con `cal`, `hold2`, `hold3`, `hold4`, `train`, `golden` y holdout `hold4` → `["cal", "hold2", "hold3"]`; con holdout `hold2` → `["cal"]`. El veredicto no cambia (tests existentes).
3. **Referencia v3→v4**: `puerta_calidad.py --anterior out/clasificador-embeddings-v3.json --nuevo out/clasificador-embeddings-v4.json --inventario CACHE/inventario_v4.csv --holdout hold4 --cal-ampliada --salida out/puerta-calidad-v4-cal-ampliada.json`. Se anotan cal original y cal ampliada para v3 y v4. No cambia la decisión de publicar v4 (ya tomada con la puerta vigente).
4. **INFORME**: apartado nuevo §16 (corto) con la decisión, la definición, la tabla de referencia y el comando que usará el siguiente corte (`--cal-ampliada` pasa a ser obligatorio en el runbook de cortes). Docstring de la puerta actualizado.
5. **Spec**: estado de cierre aquí.

## Mis decisiones (del asistente, corregibles)

- Excluir de la cal ampliada el holdout del corte evaluado (motivo en el objetivo 1). Alternativa descartada: incluirlo, que haría pasar cualquier versión.
- Flag opt-in en vez de comportamiento por defecto, para no alterar la reproducibilidad de §13 y §14; el runbook lo hará obligatorio.
- Tolerancia de 0,5 pp sin cambios sobre la cal ampliada; si la serie ampliada muestra otra desviación muestral, se revisa con datos en un corte posterior.

## Lo que queda al constructor

- Nombres internos y orden de columnas de la tabla impresa; texto exacto del §16.

## Verificación de cierre

- pytest en verde con los tests nuevos; `out/puerta-calidad-v4-cal-ampliada.json` con `condicion_cal = cal_ampliada` y cifras en §16; commits con el AB# del PBI; integración con sí.

## Estado de cierre (2026-11-18)

- Work items: PBI AB#101418 y task AB#101419 (creados con sí). Ejecución nativa en la misma sesión que v4.
- Código (Batch f7a12b3): `particiones_cal_ampliada(inv, holdout)` y flag `--cal-ampliada` en `puerta_calidad.py`; `condicion_cal` siempre en el JSON y `cal_ampliada` con el flag; docstring actualizado. Test nuevo (holdout actual excluido; inventario sin holds anteriores → solo `cal`). `pytest`: 110 passed. Regresión sin flag comprobada contra `out/puerta-calidad-v4.json`: única diferencia, `condicion_cal = cal`.
- Referencia v3→v4 con el flag (Batch 36f3da8, INFORME §16): cal ampliada = cal ∪ hold2 ∪ hold3 = 1.831 documentos; v3 88,0 % / v4 87,7 % TDN1 (−0,3 pp), 65,2 % / 64,9 % TDN2; `PUERTA: PASA`. `out/puerta-calidad-v4-cal-ampliada.json` SHA256 `1c737f4930944ca828b7e91c0d72eb2a1ac2de82e4da4d67a5ef7e2e3f3af049` (copia en `docs/auxiliares/temps/2026-11-18/`). La decisión de publicar v4 no cambia.
- Para el siguiente corte (v5): la puerta se lanza siempre con `--cal-ampliada`; la ampliada pasará a incluir `hold4`.
- Queda: runbook de cortes recurrentes con el flag obligatorio y la revisión de `EXCLUIR_CARPETAS`; si la serie ampliada también baja de forma sostenida, valorar el enfoque 3 (tráfico real etiquetado).
