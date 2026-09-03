# Migración gpt-4o-mini → gpt-5-mini

La familia gpt-4.1 (que sirve el deployment `gpt-4o-mini`) **se retira el 14-oct-2026**
([model retirement schedule](https://learn.microsoft.com/azure/foundry/openai/concepts/model-retirement-schedule)).
Estos scripts crean los deployments nuevos y migran el registro de modelos en BBDD
(`dbo.ModeloConfigs`) sin renombrar claves (las tipologías referencian los modelos por `ModelKey`).

Contexto y proyección de costes: `docs/auxiliares/temps/2026-07-21/INFORME-COSTES-IA.md`
(gpt-5-mini Data Zone EU ≈ −41 % sobre el gasto GPT actual de la app).

## Orden de ejecución

| Paso | Qué | Riesgo |
|---|---|---|
| 1 | `./create-gpt5-deployments.sh` (añadir `--with-secondary` para West Europe). Crea el deployment `gpt-5-mini` (2025-08-07, DataZoneStandard, 50K TPM por defecto — `CAPACITY=nn` para otro valor) | Ninguno: no toca el flujo |
| 2 | `01-insert-gpt5-test-rows.sql` — filas `*-gpt5-mini-test` en `ModeloConfigs` (Activo=1, sin IsDefault/UseAsFallback): solo se resuelven pidiendo el modelKey explícito → A/B de calidad TDN1/TDN2 | Ninguno: no entran en la resolución por defecto |
| 3 | **Fix de código y despliegue** (ver abajo) | — |
| 4 | `02-activate-gpt5-models.sql` — cutover in-place de las 4 filas GPT (backup automático `ModeloConfigs__bak_<ts>`); la app lo recoge en ≤ 5 min (caché del registro) | Reversible con paso 5 |
| 5 | (si hace falta) `03-rollback-gpt5-models.sql` | — |
| — | `04-dev-split-gpt4-gpt5.sql` — **solo DEV** (AB#100131, ejecutado 2026-08-14): deshace el modo mixto que dejó el A/B — revierte las 4 filas `gpt4o-mini` al deployment `gpt-4o-mini` (clasificación MaxTokens 150) y renombra las filas `*-gpt5-mini-test` a `classification.gpt5-mini` / `extraction.gpt5-mini` / `prompt.gpt5-mini` (MaxTokens 2000 / 16000) como juego GPT-5 operativo seleccionable por modelKey | Ninguno: el set activo queda en gpt-4o-mini |
| — | `05-dev-e2e-classification-aliases.sql` — **solo DEV** (AB#100131, ejecutado 2026-08-14): alta de las filas alias de clasificación `gpt-4o-mini` y `gpt-5-mini` (clonan la config real, sin IsDefault/UseAsFallback) que los casos E2E FC-FC4/FC-FC5 piden por modelKey literal. Idempotente | Ninguno: solo se resuelven por modelKey |
| — | `06-pre-create-gpt5-set.sql` — **solo PRE** (AB#100218, ejecutado 2026-08-14): tras el mirror PRO→PRE, alta del juego gpt-5-mini (`classification.gpt5-mini` 2000 tok, `extraction.gpt5-mini` 16000 tok y timeout 180 s, `prompt.gpt5-mini`) clonando las filas gpt4 del propio PRE, sin flags. Idempotente | Ninguno: solo se resuelven por modelKey |

Ejecución SQL (sesión `az login` con permisos de escritura en la BBDD):

```bash
sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i 01-insert-gpt5-test-rows.sql
```

Para dev: mismo comando contra `srbsqldevdocai.database.windows.net` (si `sqlcmd -G` falla
por MFA, usar el método pyodbc+token documentado en la guía de BBDD).

## Fix de código obligatorio antes de activar (paso 3)

La familia gpt-5 rechaza con HTTP 400 dos parámetros que el flujo enviaba siempre:
`temperature` distinta de la por defecto, y `max_tokens` (el SDK estable
Azure.AI.OpenAI 2.1.0 serializa `MaxOutputTokenCount` como `max_tokens`; estos
modelos exigen `max_completion_tokens`, que ese SDK no sabe emitir).

1. Implementado en `OpenAiModelCapabilities.ConfigureChatOptions` (detección de familia
   por prefijo del deployment): para gpt-5/o-series no se envían ni `temperature` ni el
   límite de salida (queda acotado por el propio modelo); para modelos clásicos nada cambia.
   Nota: mientras no se suba el SDK, el `MaxTokens` de BBDD no aplica a deployments gpt-5.
2. Valorar subir `Azure.AI.OpenAI` (>=2.9.0-beta): recuperaría el cap de salida vía
   `max_completion_tokens` y permitiría `reasoning_effort` (minimal/low recomendado
   para clasificación).
3. `MaxTokens` de clasificación: el script 02 ya lo sube 150 → 2000 en la fila de BBDD.

Sin el fix, activar el paso 4 provoca 400 en todas las llamadas GPT.

## Qué NO cubren estos scripts

- **App settings del pipeline**: `azure-pipelines.yml` cablea
  `Classification__GptFallback__DeploymentName=gpt-4o-mini` y
  `Extraction__GptFallback__DeploymentName=gpt-4o-mini` (defaults/legacy; la resolución
  real sale de `ModeloConfigs`). Actualizarlos a `gpt-5-mini` en el mismo PR del fix
  de código para no dejar config engañosa.
- **El gpt-4.1 interno de Content Understanding** (47 % del coste IA): lo migra el propio
  servicio CU con las api-version de los analyzers; no depende de estos scripts.
- **Borrar el deployment `gpt-4o-mini`**: hacerlo manualmente tras un periodo de
  convivencia (sirve de rollback instantáneo vía script 03 mientras exista).
- Las filas `*-gpt5-mini-test` pueden desactivarse (`Activo=0`) tras el cutover.
