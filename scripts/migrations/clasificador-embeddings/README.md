# Clasificador por embeddings: configuracion por entorno (AB#100779)

Scripts de datos de la Fase A (sombra en DEV). No hay cambios de esquema.

| Script | Que hace | Entornos |
|---|---|---|
| `01-modeloconfig-clasificador-embeddings.sql` | Fila `ModeloConfigs` Tipo=5 `clasificador.embeddings` con deployment, artefacto, modo y umbrales. Copia la credencial de `classification.gpt4o-mini-fallback`. | DEV con `@Modo='sombra'`; PRE y PRO con `@Modo='off'` |
| `02-tarifas-embeddings-deployments.sql` | Lineas `text-embedding-3-large-030358` y `-010650` en el catalogo `tarifas.ia`. | DEV, PRE y PRO |

Orden: 01 y despues 02. Ambos hacen backup `ModeloConfigs__bak_<fecha>` y son idempotentes.

Antes del 01 en DEV, el artefacto `clasificador-embeddings-v1.json` debe estar en
`srbstgdevdocai/documentai/modelos/clasificador-embeddings/v1/` (lo genera
`exportar_modelo.py` en DocumentIA.Batch). En PRE y PRO la fila entra en `off` y no
necesita artefacto hasta AB#100781. `openai_primary` esta verificado para DEV; antes
de activar la fila en PRE o PRO (AB#100781) hay que confirmar que ese alias existe en
el mapa `AI__Resources__*` del entorno.

Avisos:

- Una fila con `Modo = off` debe llevar tambien `Restringido.Modo = off`:
  `Restringido.Modo` tiene su propio valor por defecto (`sombra`) y gobierna las
  peticiones con `restriccionTipologias`. El script 01 lo hace (`@Modo` rellena ambos);
  una edicion manual desde el Admin que solo cambie `Modo` deja activo el modo restringido.
- Un endpoint de embeddings colgado cuesta `TimeoutSeconds` en cada ejecucion y no abre
  el circuito de resiliencia (solo lo abren los 429 y los fallos HTTP); si ocurre, bajar
  `TimeoutSeconds` o poner la fila en `off` hasta que el endpoint responda.
- El orquestador colapsa los espacios y recorta el texto a 24.000 caracteres
  (`EmbeddingsClasificadorConfig.MaxCharsPorDefecto`) antes de llamar a la activity, para
  no duplicar el markdown completo en el historial Durable; `MaxChars` solo puede bajar
  ese tope, un valor mayor no tiene efecto.

Cambiar modo, umbrales o version del modelo es editar la fila (Admin, pagina Modelos,
seccion Embeddings) o `UPDATE ... JSON_MODIFY(ConfiguracionJson, '$.Modo', 'hibrido')`;
entra en vigor en 5 minutos. El analisis de la sombra esta en
`scripts/analysis/sombra-embeddings.sql`.

Documentacion: `docs/05_MANUAL_USO_CONFIGURACION.md`, seccion 5.6.1c; diseno en
`docs/superpowers/specs/2026-10-01-clasificador-embeddings-hibrido-design.md` y ADR-002.
