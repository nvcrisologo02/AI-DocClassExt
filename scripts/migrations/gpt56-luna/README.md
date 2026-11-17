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
`max_completion_tokens`.

## Orden de ejecución

| Paso | Qué | Riesgo |
|---|---|---|
| 1 | `ENV=dev ./create-gpt56-luna-deployment.sh` — deployment `gpt-5.6-luna` (modelo `gpt-5.6-luna` 2026-07-09, `DataZoneStandard`, 50K TPM por defecto; `CAPACITY=nn` para otro valor). `ENV` es obligatoria: `pro` lleva los valores de PRO (`--with-secondary` para West Europe) y `pre` falla hasta rellenar su bloque. Comprueba antes que el modelo y el SKU existen en la región; si falta cuota Data Zone, falla y se para (no cambiar a Global). Idempotente | Ninguno: no toca el flujo |
| 2 | `./smoke-chat.sh` — una llamada de chat directa al deployment con `reasoning_effort: none` y `max_completion_tokens: 20`, api-version `2025-04-01-preview` (la que usa el SDK beta). Confirma que el servicio acepta ambos campos con este modelo; el test unitario solo prueba el cuerpo que genera el SDK | Ninguno: solo lectura, unos 30 tokens |
| 3 | `01-dev-insert-gpt56-luna-rows.sql` — **solo DEV**: filas `classification.gpt56-luna` (Tipo 0) y `prompt.gpt56-luna` (Tipo 2) clonadas de `classification.gpt5-mini` y `prompt.gpt5-mini`, con `DeploymentName` gpt-5.6-luna, `ReasoningEffort` none, `Activo = 1`, sin IsDefault ni UseAsFallback (solo se resuelven pidiendo la clave). Idempotente, backup `ModeloConfigs__bak_<ts>` solo si inserta | Ninguno: el flujo por defecto no cambia |
| 4 | `02-dev-tarifa-gpt56-luna.sql` — **solo DEV**: línea `gpt-5.6-luna` en el catálogo `tarifas.ia` (VigenteDesde 2026-11-17; 0,1975 / 0,0197 / 1,185 € por millón de entrada, entrada en caché y salida: medidores Data Zone de West Europe en EUR, Retail Prices API 2026-11-17, precios de lista). Idempotente, backup solo si añade, valida el JSON antes de guardar | Ninguno: solo añade una línea |

Ejecución SQL en DEV (sesión `az login` con permisos de escritura en la BD; `sqlcmd -G`
falla por MFA en este entorno):

```bash
pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/<script>.sql
```

## Resultado en DEV (2026-11-17)

- Deployment creado en `srbaisrv01devdocai` (West Europe): `gpt-5.6-luna` / `2026-07-09` /
  `DataZoneStandard` / 50, `provisioningState` Succeeded. Cuota Data Zone suficiente para 50K TPM.
- Smoke: HTTP 200, `finish_reason` `stop`, contenido `OK`, `usage.completion_tokens_details.reasoning_tokens` 0,
  `prompt_tokens` 10, `completion_tokens` 4. El servicio acepta `reasoning_effort` y
  `max_completion_tokens` con api-version `2025-04-01-preview`.
- Scripts 01 y 02 aplicados con `apply-sql-dev.ps1`: filas Id 1027 (`classification.gpt56-luna`,
  MaxTokens 2000) e Id 1028 (`prompt.gpt56-luna`), ambas gpt-5.6-luna / none / sin flags; línea
  `gpt-5.6-luna` añadida al catálogo (23 líneas, JSON válido). Backups
  `ModeloConfigs__bak_20261117_134130` y `..._134138`. Reejecución de ambos sin cambios ni backup.

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
