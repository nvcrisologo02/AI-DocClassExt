# ADR-006: Activación del clasificador híbrido por embeddings (modo hibrido, umbral 0,6) en DEV, PRE y PRO
Fecha: 2026-11-26 · Estado: aceptada · Work Item: AB#101570 (release v1.2.0; complementa ADR-002, AB#100779) · Session ID: sin Session ID (proyecto sin `Gobernanza: activa`; traspaso en `docs/auxiliares/temps/2026-11-26/`)

## Contexto y problema

ADR-002 integró el clasificador A (regresión logística sobre `text-embedding-3-large`) como activity previa al GPT con tres modos en la fila `clasificador.embeddings` de `ModeloConfigs`: `off`, `sombra` (predice y persiste sin decidir) e `hibrido` (contesta cuando su confianza TDN1 alcanza `UmbralConfianza`; si no, deriva al GPT). Desde el 2026-10-02 DEV está en sombra; PRE y PRO llevan la fila en `off` con el artefacto v1. ADR-002 dejó la activación para cuando la sombra fijara el umbral con tráfico real.

La release v1.2.0 quiere PRE y PRO como espejo de DEV y el usuario ha pedido que el clasificador por embeddings pase a decidir. Hay que fijar en qué modo, con qué umbral, en qué orden de entornos y cómo se deshace.

## Opciones consideradas

- A. DEV pasa a `hibrido` primero (cambio de configuración), se observa con tráfico real y PRE y PRO reciben la misma fila en la release v1.2.0.
- B. PRE pasa a `hibrido` en la release y DEV sigue en sombra.
- C. PRE y PRO en sombra con el artefacto v5 y la activación se decide más adelante.

## Decisión

El clasificador por embeddings pasa a modo `hibrido` con `UmbralConfianza` 0,6 y, en peticiones restringidas, `Restringido.Modo` = `hibrido` con `UmbralMasa` 0,5 y `UmbralConfianzaCondicionada` 0,8 (los valores que la fila de DEV ya lleva). Primero en DEV, por script versionado en `scripts/migrations/clasificador-embeddings/` con copia `__bak`; PRE y PRO reciben la misma fila, con el artefacto v5, en la release v1.2.0, en el orden DEV → PRE → PRO de ADR-001. El GPT sigue clasificando todo lo que A deriva.

## Motivos

- Medición fuera de línea del híbrido sobre cal (1.576 documentos, INFORME §12 de DocumentIA.Batch): con umbral 0,6 el híbrido supera a sus dos componentes en TDN1, +5,7 pp frente al GPT de producción (McNemar 111/23, p = 5·10⁻¹⁵) y +2,4 pp frente a A solo (63/28, p = 3·10⁻⁴); en TDN2, 0,642 frente a 0,600 del GPT. El óptimo está en 0,5-0,6; por encima de 0,9 el híbrido no mejora a A.
- A separa bien sus aciertos: ECE 0,029 en cal frente a 0,067 del GPT; el GPT solo aporta donde A duda.
- El corte v5 (AB#101564, 2026-11-25) pasa la puerta de calidad: cal ampliada 87,5 % TDN1 (1.935 documentos), hold5 88,7 %, y es el primer corte en que la serie de cal original no baja (86,74 %).
- Activar primero en DEV mantiene la regla de promoción por saltos (ADR-001) y deja la puerta 2 de la release (golden de PRE frente a la línea base de DEV) comparable: los dos entornos deciden igual.
- El cambio es solo configuración: la vuelta atrás es poner `Modo` y `Restringido.Modo` en `sombra` (o `off`) en la misma fila; la app lo recoge en menos de 5 minutos.

## Consecuencias

- A partir del cambio en DEV, las ejecuciones con confianza TDN1 ≥ 0,6 llevan la tipología de A (`RamaClasificacion` = embeddings) y no llaman al GPT de clasificación: baja el coste de clasificación y cambia la distribución de tipologías respecto a la sombra. La reutilización por duplicado no se ve afectada (misma clave de intención).
- Lo que no se ha medido: la sombra en DEV con tráfico real sigue siendo escasa (224 ejecuciones reales de v4 en una semana; v5 en sombra desde el 2026-11-25). La observación en DEV antes de la release (comparar `RamaClasificacion`, confianza y acuerdo con el GPT en los documentos derivados) es la medición pendiente y se anota en `docs/releases/v1.2.0/release.md`.
- En PRE y PRO hay que copiar los artefactos `modelos/clasificador-embeddings/v5/` al storage del entorno antes de cambiar la fila (hoy no tienen ninguno) y confirmar que el alias `openai_primary` resuelve en `AI__Resources__openai_primary__Endpoint`. AB#100781 (promoción por pipeline) sigue abierto; esta release lo hace con script y evidencia.
- La línea `text-embedding-3-large-030358` ya está en `tarifas.ia` de los tres entornos (v1.0.0); el coste de embeddings suma en `ClasificacionEur`.
- ADR-002 queda complementada, no sustituida: su bloque sigue en «propuesta» por la regla de no reescribir registros; esta ADR recoge la activación que aquel anticipaba.

## Alternativas descartadas y por qué

- B: PRE por delante de DEV rompe el espejo y la puerta 2 deja de ser comparable; además invierte el orden de promoción de ADR-001.
- C: pospone otra vez una decisión que la medición fuera de línea ya sostiene y deja la sombra de PRE y PRO como única fuente nueva, con el mismo problema de tráfico escaso que en DEV.
