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

Ejecución SQL (sesión `az login` con permisos de escritura en la BBDD):

```bash
sqlcmd -S srbsqlprodocai.database.windows.net -d DocumentIA -G -i 01-insert-gpt5-test-rows.sql
```

Para dev: mismo comando contra `srbsqldevdocai.database.windows.net` (si `sqlcmd -G` falla
por MFA, usar el método pyodbc+token documentado en la guía de BBDD).

## Fix de código obligatorio antes de activar (paso 3)

La familia gpt-5 **rechaza `temperature` distinta de la por defecto** y genera reasoning
tokens que cuentan contra el límite de salida:

1. `GptClasificarDataProvider`, `GptFallbackExtraerDataProvider` y `OpenAIPromptDataProvider`
   fijan `Temperature` incondicionalmente en `ChatCompletionOptions` — hacerla condicional
   (omitir si el deployment es familia gpt-5, o si `Temperature == 0` tratarla como "no enviar").
2. Valorar exponer `reasoning_effort` (minimal/low recomendado para clasificación).
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
