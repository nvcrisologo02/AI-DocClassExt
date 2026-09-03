# Separación de juegos de modelos gpt-4o-mini y gpt-5-mini en DEV

**Fecha:** 2026-08-14 · **Ámbito:** BD DEV (`srbsqldevdocai` / `DocumentIA`, tabla `ModeloConfigs`)

## Problema

Tras las pruebas de `gpt-5-mini`, DEV quedó en modo mixto: las 4 filas con keys
`gpt4o-mini` (que son el set activo del pipeline: `IsDefault`/`UseAsFallback`)
apuntan al deployment `gpt-5-mini`, y las 3 filas de prueba `*-gpt5-mini-test`
duplican ese mismo deployment con parámetros incompletos. El nombre de la key
no se corresponde con el modelo real y no existe un juego GPT-4 operativo.

## Objetivo

Dos juegos separados y totalmente operativos, seleccionables sin ambigüedad:

- **Juego GPT-4 (activo del pipeline).** Las 4 filas `gpt4o-mini` vuelven al
  deployment `gpt-4o-mini` conservando sus flags:
  - `classification.gpt4o-mini-fallback`: deployment `gpt-4o-mini`, `MaxTokens` 150.
  - `default.gpt4o-mini_ex`: deployment `gpt-4o-mini`.
  - `extraction.gpt4o-mini-fallback`: deployment `gpt-4o-mini`.
  - `default.gpt4o-mini` (prompt): deployment `gpt-4o-mini`.
  - Misma semántica que `03-rollback-gpt5-models.sql`: solo se tocan
    `DeploymentName` y `MaxTokens`; el resto de parámetros se conserva.
- **Juego GPT-5 (registrado, seleccionable por `Key`, sin flags).** Renombrado
  quitando el sufijo `-test` (columna `Key` y `$.Key` del JSON) y parámetros
  corregidos para la familia gpt-5:
  - `classification.gpt5-mini`: `MaxTokens` 150 → 2000 (los reasoning tokens
    cuentan contra el límite de salida).
  - `extraction.gpt5-mini`: `MaxTokens` 2000 → 16000 (paridad con el juego de
    extracción activo).
  - `prompt.gpt5-mini`: solo renombrado.

## Decisiones validadas

- Set activo en DEV: **GPT-4** (default/fallback). GPT-5 queda operativo y
  seleccionable por `Key` (p. ej. evaluación A/B), sin `IsDefault` ni
  `UseAsFallback` — el loader exige exactamente un fallback por tipo.
- Renombrado `-test` → definitivo: verificado que ninguna tipología en DEV ni
  ningún código/config del repo (ni DocumentIA.Batch) referencia las keys
  antiguas fuera de los propios scripts de migración.
- Los deployments `gpt-4o-mini` (modelo gpt-4.1-mini) y `gpt-5-mini` ya existen
  en el recurso compartido `upe48-mm2avmdm-swedencentral` (RG `srbrgdocsaiprod`);
  no se toca Azure OpenAI.

## Implementación

Script versionado `scripts/migrations/gpt5-mini/04-dev-split-gpt4-gpt5.sql`,
convención de la carpeta: backup `ModeloConfigs__bak_<timestamp>`, transacción
con guardas de recuento (4 reversiones + 3 renombrados; si no cuadra, rollback)
y SELECT de verificación de las 7 filas `azure-openai`.

No idempotente por diseño (igual que el script 02): una segunda ejecución no
encuentra filas que cumplan los filtros y aborta con la guarda de recuento.

## Verificación

1. SELECT final del script: deployments, keys y MaxTokens esperados.
2. Tras el refresco de caché del registro de modelos (≤ 5 min), health de
   Functions DEV sano (valida los providers registrados).
