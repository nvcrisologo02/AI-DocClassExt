# Prueba en DEV de gpt-5.6-luna (AB#99934)

Candidata a sustituir `gpt-4.1-mini` (deployment `gpt-4o-mini`, retirada el 2027-04-14) en
clasificación, prompt libre y resumen. Estos scripts crean el deployment Data Zone en el
entorno indicado y dan de alta en `dbo.ModeloConfigs` las filas y la tarifa que permiten
seleccionar el modelo por clave sin tocar el flujo por defecto. La medición (golden
frente a control) y el cutover quedan fuera: ver la spec
`docs/superpowers/specs/2026-11-17-migracion-clasificacion-gpt56-luna-design.md`.

Requisitos de código: `Azure.AI.OpenAI` 2.9.0-beta.1 y la propiedad `ReasoningEffort`
en las configuraciones de modelo (rama `feature/99934-gpt56-luna-dev`, AB#101371). Sin
ese código la fila resuelve pero el helper no envía `reasoning_effort` ni
`max_completion_tokens`. La caché de prompt (scripts 03-05) necesita además el código de
`feature/101411-prerrequisitos-cutover` (AB#101412 y AB#101428). Con `develop`, pedir la
clave luna ignora el bloque `promptCache`: la llamada sale sin campos de caché, el servicio
aplica su modo por defecto (`implicit`), que escribe y cobra el prompt entero en cada llamada,
y `develop` tampoco registra esas escrituras en el coste.

## Orden de ejecución

| Paso | Qué | Riesgo |
|---|---|---|
| 0 (caché) | `./smoke-cache.sh` — puerta de la caché de prompt: dos llamadas al deployment con `prompt_cache_key`, `prompt_cache_options` (implicit, 30m) y `prompt_cache_breakpoint` explícito en el system (~5.800 caracteres, 1.332 tokens). Resultado en DEV (2026-11-18, URL del deployment con api-version `2025-04-01-preview`, sin hacer falta `URL_V1=1`): llamada 1 HTTP 200, `cached_tokens` 0, `cache_write_tokens` 1329; llamada 2 HTTP 200, `cached_tokens` 1329, `cache_write_tokens` 0. El servicio acepta los tres campos y la caché se recupera | Ninguno: solo lectura, dos llamadas |
| 1 | `ENV=dev ./create-gpt56-luna-deployment.sh` — deployment `gpt-5.6-luna` (modelo `gpt-5.6-luna` 2026-07-09, `DataZoneStandard`, 50K TPM por defecto; `CAPACITY=nn` para otro valor). `ENV` es obligatoria: `pro` lleva los valores de PRO (`--with-secondary` para West Europe) y `pre` falla hasta rellenar su bloque. Comprueba antes que el modelo y el SKU existen en la región; si falta cuota Data Zone, falla y se para (no cambiar a Global). Idempotente | Ninguno: no toca el flujo |
| 2 | `./smoke-chat.sh` — una llamada de chat directa al deployment con `reasoning_effort: none` y `max_completion_tokens: 20`, api-version `2025-04-01-preview` (la que usa el SDK beta). Confirma que el servicio acepta ambos campos con este modelo; el test unitario solo prueba el cuerpo que genera el SDK | Ninguno: solo lectura, unos 30 tokens |
| 3 | `01-dev-insert-gpt56-luna-rows.sql` — **solo DEV**: filas `classification.gpt56-luna` (Tipo 0) y `prompt.gpt56-luna` (Tipo 2) clonadas de `classification.gpt5-mini` y `prompt.gpt5-mini`, con `DeploymentName` gpt-5.6-luna, `ReasoningEffort` none, `Activo = 1`, sin IsDefault ni UseAsFallback (solo se resuelven pidiendo la clave). Idempotente, backup `ModeloConfigs__bak_<ts>` solo si inserta | Ninguno: el flujo por defecto no cambia |
| 4 | `02-dev-tarifa-gpt56-luna.sql` — **solo DEV**: línea `gpt-5.6-luna` en el catálogo `tarifas.ia` (VigenteDesde 2026-11-17; 0,1975 / 0,0197 / 1,185 € por millón de entrada, entrada en caché y salida: medidores Data Zone de West Europe en EUR, Retail Prices API 2026-11-17, precios de lista). Idempotente, backup solo si añade, valida el JSON antes de guardar | Ninguno: solo añade una línea |

Prerrequisitos del cutover (AB#101412). Los scripts 03 y 04 **solo cambian la fila
`classification.gpt56-luna`** (Tipo 0): no tocan las filas por defecto de DEV ni `prompt.gpt56-luna`.

| Paso | Qué | Riesgo |
|---|---|---|
| 5 | `03-dev-promptcache-on-gpt56-luna.sql` — **solo DEV**: escribe `promptCache` = `{"enabled":true,"keyPrefix":"documentia-clasif","breakpoint":true,"ttl":"30m","mode":"explicit"}` en la fila luna (`explicit` porque con `implicit` GPT-5.6 cobra la escritura del prompt entero, documento incluido, en cada llamada). Idempotente, backup solo si cambia, `ISJSON` antes de guardar | Solo afecta a las peticiones que piden la clave luna |
| 6 | `04-dev-promptcache-off-gpt56-luna.sql` — **solo DEV**: `promptCache.enabled = false` (conserva el resto del bloque para volver a encender con el 03). Idempotente: sin bloque o ya apagado, no hace nada | Ninguno: devuelve el comportamiento sin caché |
| 7 | `05-dev-tarifa-escritura-cache-gpt56-luna.sql` — **solo DEV**: `EurEscrituraCachePor1M` en la línea `gpt-5.6-luna` de `tarifas.ia`. Precio confirmado el 2026-11-18: 0,2469 € por millón de tokens escritos en caché (medidor Data Zone de West Europe «5.6 luna ShortCo Cd Wr Std DZ», Retail Prices API, vigente desde 2026-08-01); el medidor de retención 12 h (0,5386) no aplica porque la petición usa ttl 30m. Si la variable queda a `NULL` el script falla con `THROW` y no toca nada. Falla si la línea no existe (ejecutar antes el 02) | Ninguno: solo añade una propiedad a una línea |

### Consultas de medición (`medicion/`, solo lectura)

Consultas para comparar pasadas de la golden (luna frente a control). Parámetros al principio de cada
fichero: `@From` (inclusive) y `@To` (exclusivo) en UTC y `@SubmittedByPrefix`
(`DocumentIA.Batch.Evaluation/`, el que pone el harness). Agrupan por `ModeloClasificacion`. Sin `GO`; un
solo resultado por fichero (el runner solo muestra el primero).

| Consulta | Qué devuelve |
|---|---|
| `01-coste-tokens-por-pasada.sql` | Por modelo y fase (`classification.phase1`/`phase2`): N, documentos distintos, coste medio y total de clasificación, tokens medios de entrada, en caché y escritos en caché, porcentaje de caché y ejecuciones con la marca `\|sin-precio-escritura`. Referencia de la pasada de luna en la ventana limpia de 150K TPM (`2026-11-17 16:34:00` a `17:41:53`): fase 1 con 18.223 tokens de entrada y 1.229 en caché de media |
| `02-virtuales-tdn1-igual-tdn2.sql` | Ejecuciones virtuales (`ClasificacionParcial`), con `Tdn2` informado y con `Tdn1 = Tdn2`, por familia (`Tdn1`) con total. Pasada de luna completa (`16:10:42` a `17:41:53`): 159 virtuales, 136 con `Tdn1 = Tdn2`; en el control (`14:50:47` a `16:10:43`), 0 |
| `03-latencia-por-pasada.sql` | N, p50, p95 y media de `DuracionTotalMs` (sin reutilizadas por duplicado). Ventana limpia de luna: 10.872 / 18.737 ms |

`query-sql.ps1` recibe el SQL en `-Query`; como los ficheros empiezan por un comentario `--`, se pasa el texto
leído del fichero desde PowerShell (desde Bash, `-Query "$(cat ...)"` rompe el análisis del argumento):

```bash
pwsh -NoProfile -Command "& ./scripts/database/query-sql.ps1 -Server srbsqldevdocai.database.windows.net -Query (Get-Content -Raw scripts/migrations/gpt56-luna/medicion/01-coste-tokens-por-pasada.sql) -OutJson <ruta>/01.json"
```

Para otra ventana se edita `@From`/`@To` en una copia del fichero. `TokensEscrituraCache` sale `NULL` en las
ejecuciones persistidas antes de desplegar AB#101412.

Ejecución SQL en DEV (sesión `az login` con permisos de escritura en la BD; `sqlcmd -G`
falla por MFA en este entorno):

```bash
pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/<script>.sql
```

## Resultado en DEV (2026-11-17)

- Deployment creado en `srbaisrv01devdocai` (West Europe): `gpt-5.6-luna` / `2026-07-09` /
  `DataZoneStandard` / 50, `provisioningState` Succeeded. Cuota Data Zone suficiente para 50K TPM.
- **Capacidad subida a 150K TPM el 2026-11-17 a las 16:22 UTC** (con sí explícito, `az cognitiveservices account
  deployment create ... --sku-capacity 150`): con 50K TPM y `ParallelQueries` 2 la golden daba 429 con
  `Retry-After` 60 s (13 de 48 llamadas en 30 min) y la latencia medida no era del modelo. Igual que la
  capacidad del `gpt-4o-mini` de DEV (150). El script sigue creando con 50 por defecto (`CAPACITY=150` para
  replicarlo); el dimensionado de PRE/PRO es decisión del cutover.
- Smoke: HTTP 200, `finish_reason` `stop`, contenido `OK`, `usage.completion_tokens_details.reasoning_tokens` 0,
  `prompt_tokens` 10, `completion_tokens` 4. El servicio acepta `reasoning_effort` y
  `max_completion_tokens` con api-version `2025-04-01-preview`.
- Scripts 01 y 02 aplicados con `apply-sql-dev.ps1`: filas Id 1027 (`classification.gpt56-luna`,
  MaxTokens 2000) e Id 1028 (`prompt.gpt56-luna`), ambas gpt-5.6-luna / none / sin flags; línea
  `gpt-5.6-luna` añadida al catálogo (23 líneas, JSON válido). Backups
  `ModeloConfigs__bak_20261117_134130` y `..._134138`. Reejecución de ambos sin cambios ni backup.
- Rama desplegada en DEV con el pipeline 802 "AI DocClassExt (Functions)" (run 80138, `targetEnvironment=dev`,
  rama `feature/99934-gpt56-luna-dev`): DEV ejecutó esta rama durante la medición (17/11) y **volvió a `develop`
  el 2026-11-18 con el run 80153 del 802** (`develop` 83ca620, `targetEnvironment=dev`, succeeded 09:02 UTC).
  Las filas `*.gpt56-luna` y el deployment se quedan: con el código de `develop` la propiedad
  `ReasoningEffort` se ignora al deserializar y las filas no entran en la resolución por defecto. Humo de clasificación con
  `classification.gpt56-luna` (3 documentos): 3/3 OK, `ModeloClasificacion` gpt-5.6-luna, coste y
  tokens informados, `TokensRazonamiento` 0. Detalle en la spec, sección 9.

## Estado en DEV tras los prerrequisitos del cutover (leído el 2026-11-19)

- Script 03 aplicado: la fila Id 1027 (`classification.gpt56-luna`) tiene
  `promptCache` = `{"enabled":true,"keyPrefix":"documentia-clasif","breakpoint":true,"ttl":"30m","mode":"explicit"}`
  (`FechaActualizacion` 2026-11-18 13:04:59) y sigue sin `IsDefault`. El 04 no se aplicó después, así que la
  caché queda encendida. `prompt.gpt56-luna` y las filas por defecto no se tocaron.
- Script 05 aplicado: la línea `gpt-5.6-luna` de `tarifas.ia` (VigenteDesde 2026-11-17) tiene
  `EurEscrituraCachePor1M` 0,2469.
- Backups de `ModeloConfigs` de estos pasos: `ModeloConfigs__bak_20261118_123946` y `..._130459`.
- Runs del 802 sobre DEV: 80158 (3d51225), 80160 (1af617c), 80161 (`develop`, vuelta temporal) y **80164
  (72d4626, rama `feature/101411-prerrequisitos-cutover`)**. Por decisión del 2026-11-19, DEV se quedó en
  72d4626 para el golden completo (AB#101433, terminado el mismo día) y sigue ahí. Volver a `develop` se pregunta,
  porque `develop` no tiene el código de caché (ver «Requisitos de código»).
- Medición con la muestra de 50 (spec `2026-11-18-breakpoint-tramo-estable-usuario-design.md`, «Resultado en
  DEV»): fase 1 con el 74,1% en caché, ratio de coste 0,49 frente al control y 0 HTTP 400. Golden completo de
  467 (misma spec, «Golden completo en DEV»): TDN1 66,0% y TDN2 41,8% frente a 56,8% y 34,3% del control,
  76,0% en caché en la fase 1, ratio 0,45 y 48 contratos de baja confianza frente a 8.
- AB#101435 (commit 7d325f3, **no desplegado en DEV**): la tipología virtual pasa a aplicar el umbral de baja
  confianza. Estimación sobre el `results.csv` de ese golden, no medida: luna pasaría de 48 a unos 66 documentos en baja
  confianza y los errores de TDN1 que salen como OK bajarían de 131 a 118. En el control, de 8 a 16.

## Qué NO cubren estos scripts

- **El cutover** (cambiar `DeploymentName` de las filas por defecto `gpt4o-mini*`): es AB#99939,
  con su propio script y ADR, tras el resultado de la medición.
- **PRE y PRO**: el bloque `pre` del script de deployment está en TODO a propósito; `pro` tiene
  valores pero no se ejecuta en esta unidad. Las filas y la tarifa son solo DEV.
- **App settings legacy del pipeline** (`Classification__GptFallback__DeploymentName`,
  `Extraction__GptFallback__DeploymentName`): siguen en `gpt-4o-mini`; la resolución real
  sale de `ModeloConfigs`.
- **Extracción con fallback GPT**: no cambia de modelo en esta prueba (spec 2).
- **Borrar el deployment `gpt-4o-mini`**: decisión del cutover, tras un periodo de convivencia.
