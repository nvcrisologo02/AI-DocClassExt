# Clasificador por embeddings: configuracion por entorno (AB#100779)

Scripts de datos de la Fase A (sombra en DEV). No hay cambios de esquema.

| Script | Que hace | Entornos |
|---|---|---|
| `01-modeloconfig-clasificador-embeddings.sql` | Fila `ModeloConfigs` Tipo=5 `clasificador.embeddings` con deployment, artefacto, modo y umbrales. Copia la credencial de `classification.gpt4o-mini-fallback`. | DEV con `@Modo='sombra'`; PRE y PRO con `@Modo='off'` |
| `02-tarifas-embeddings-deployments.sql` | Lineas `text-embedding-3-large-030358` y `-010650` en el catalogo `tarifas.ia`. | DEV, PRE y PRO |
| `03-modeloconfig-embeddings-v2.sql` | Apunta la fila `clasificador.embeddings` al artefacto v2 (solo `Artefacto.BlobPath`; modo y umbrales intactos). | DEV (solo DEV por guardarrail; PRE y PRO: usar el 07) |
| `04-modeloconfig-embeddings-v3.sql` | Apunta la fila `clasificador.embeddings` al artefacto v3 (misma mecanica que el 03). | DEV (solo DEV por guardarrail; PRE y PRO: usar el 07) |
| `05-modeloconfig-embeddings-v4.sql` | Apunta la fila `clasificador.embeddings` al artefacto v4 (misma mecanica que el 03). | DEV (solo DEV por guardarrail; PRE y PRO: usar el 07) |
| `06-modeloconfig-embeddings-v5.sql` | Apunta la fila `clasificador.embeddings` al artefacto v5 (misma mecanica que el 03). | DEV (solo DEV por guardarrail; PRE y PRO: usar el 07) |
| `07-modeloconfig-embeddings-modo-version.sql` | Fija version del artefacto y modo en un solo paso (`@Version`, `@Modo`; actualiza `Artefacto.BlobPath`, `Modo` y `Restringido.Modo`). Sin guardarrail de servidor; idempotente (`0 cambios`). Con `apply-sql-dev.ps1`, y `-Server ... -PermitirNoDev` fuera de DEV. | DEV, PRE y PRO con la release v1.2.0 (ADR-006) |

Aviso del 07: el blob de la version (`modelos/clasificador-embeddings/<vN>/clasificador-embeddings-<vN>.json`) debe existir en el storage del entorno **antes** de ejecutarlo; el loader conserva el ultimo modelo bueno y cachea el fallo 5 min (cache negativa).

Orden: 01 y despues 02. Ambos hacen backup `ModeloConfigs__bak_<fecha>` y son idempotentes.

Para una version nueva del artefacto: se sube primero al blob (`modelos/clasificador-embeddings/vN/`) y despues se ejecuta el script de esa version (03 para v2, 04 para v3, 05 para v4); la app lo recoge en <= 5 min y las versiones anteriores se conservan en el blob (rollback: ejecutar el script de la version anterior). Si el script se ejecuta antes de subir el blob, el loader conserva el ultimo modelo bueno y cachea el fallo 5 min.
El flujo completo de un corte nuevo (parametros, numeracion del script `0<V+1>` para la version `v<V>`, comandos, comprobaciones y cierre) esta en `eval/clasificador-embeddings/RUNBOOK_CORTES.md` de DocumentIA.Batch (AB#101421).

En PRE y PRO el 01 (con `@Modo='off'`) va **antes de desplegar el codigo** que incluye el
paso 3.0 del orquestador (develop 2aa9f02). El orquestador llama a la activity en todas las
ejecuciones; si la fila no existe, el proveedor deriva con `fila_ausente` en `Error` y emite
el evento y la metrica `Classification.Embeddings` en cada ejecucion. Con la fila en `off`
no hay llamada al endpoint ni telemetria. El 02 puede ir antes o despues del codigo.

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

### Consulta de medición (`medicion/`, solo lectura)

`medicion/01-rama-clasificacion-por-dia.sql`: por día UTC, ejecuciones no reutilizadas por rama (`embeddings`, `gpt`, `expectedtype`), confianza media de embeddings y acuerdo de TDN1 con GPT cuando ambos se persisten. Parámetros `@From`, `@To` y `@SubmittedByPrefix` al inicio; se ejecuta con `query-sql.ps1` como las de `gpt56-luna/medicion/`.
