# Guía del operador: despliegue en PRE de la release v1.2.0

Fecha de la guía: 2026-11-26 · Candidato: HEAD de `develop` en origin al abrir la ventana (`4c539936` a esta fecha; el código de `src/` no cambia desde `bc41f289`) · Registro: [release.md](release.md) · Runbook: [runbook.md](runbook.md) · Procedimiento oficial: [RELEASE_MANAGEMENT.md](../../procedimientos/RELEASE_MANAGEMENT.md)

## Cómo usar esta guía

Esta guía es el detalle ejecutable de las Fases 2 y 3 de esta release en PRE. El runbook y RELEASE_MANAGEMENT siguen siendo el checklist oficial: cada paso dice qué casilla del runbook cubre. La secuencia es la que decidió el usuario el 2026-11-26 y sustituye al orden de los apartados 2.1 a 2.4 del procedimiento general, porque esta release mezcla un despliegue de código, un deployment de IA nuevo y tres scripts de base de datos que dependen unos de otros.

Qué consigue la release en PRE: la configuración que ya corre en DEV (modelo `gpt-5.6-luna` por defecto, clasificador por embeddings v5 en modo híbrido, modelo de prompt heredado, validación TLS del GDC, app settings heredados retirados, altas de tipologías y catálogo por clave natural).

Convenciones:

- Todos los comandos se lanzan desde la raíz del clon, en Git Bash (barras `/`, sintaxis Unix). Los `pwsh ./...` lanzan scripts de PowerShell 7.
- «Operador» es quien ejecuta la ventana. «Aprobador» es quien aprueba el check 64 del environment `pre` en Azure DevOps (único aprobador, puede ser la misma persona).
- Los pipelines se lanzan desde Azure DevOps, proyecto `AI DocClassExt`, **Pipelines > Run pipeline**, rama `develop`.
- Cada paso lleva: quién, dónde, comando o parámetros, resultado esperado, qué hacer si falla y evidencia. Las evidencias van a `docs/releases/v1.2.0/evidencias/` (texto corto, sin secretos ni function keys).
- Ningún paso de esta guía se salta si el anterior falló: se corrige y se repite desde donde falló, o se aborta la ventana. No se parchea PRE a mano (regla de RELEASE_MANAGEMENT, Fase 2).
- Servidor SQL de PRE: `srbsqlpredocai.database.windows.net`, base `DocumentIA`. Function App: `srbapppredocai`. Grupo de recursos: `SRBRGPREDOCSAI`. Suscripción: `a4f6b357-8f13-4488-9ee8-b9f635426f91`. Storage de documentos: `srbstgpredocai`.

## Glosario y mapa

Fases del procedimiento general (RELEASE_MANAGEMENT): **0** preparación y versión; **1** DEV (build, tests, smoke y export de referencia); **2** PRE, puerta técnica (esta guía, pasos 1 a 19); **3** aprobación (Test Plan, revisión del diff, «Go»); **4** PRO; **5** cierre y registro (tag, `master`, work items). Esta guía cubre la Fase 2 y la Fase 3 de esta release.

Las cuatro puertas de PRE (todas obligatorias; la 3 suele ser condicional, pero aquí lo es porque cambia el modelo):

- **Puerta 1, smoke E2E:** el runner `run-e2e-postdeploy.ps1 -Profile smoke` contra PRE debe dar 6/6 PASS y la telemetría no debe mostrar dependencias a IA de PRO (paso 15).
- **Puerta 2, golden:** 467 documentos clasificados en PRE comparados con la línea base de DEV (paso 16).
- **Puerta 3, coste:** la factura de IA de PRE del día de las puertas 1 y 2, comparada con DEV, al día siguiente (paso 19).
- **Puerta 4, deriva:** la configuración de PRE no ha cambiado entre la referencia y el final de las puertas (paso 17).

Piezas de Azure DevOps que aparecen en la guía:

- **Check 64:** la aprobación manual del environment `pre`. Se concede en Azure DevOps, en el propio run (botón *Review* / *Approve* de la etapa que espera) o en **Pipelines > Environments > pre > Approvals and checks**. Afecta a todo job de despliegue sobre `pre` (807 Apply, 832, 799). El aprobador es único.
- **Pipeline 807 `Migrations-BD`** (`azure-pipelines-migrations.yml`): aplica las migraciones EF a la base de datos. Etapas Generate y Apply.
- **Pipeline 832 `AI DocClassExt (AiArtifacts)`** (`azure-pipelines-ai-artifacts.yml`): promociona de DEV a PRE los deployments de IA (`apply-deployments.ps1`), los analyzers y los clasificadores, y da de alta configuración. Etapas: **Guard** (comprueba parámetros), **Export** (exporta la configuración de DEV, solo lectura), **AiArtifacts** (despliegue de IA, pide aprobación), **ConfigSeed** (diff de configuración DEV frente a PRE y genera `altas-config.sql`; no escribe) y **ConfigApply** (aplica las altas; solo existe con `applyMissingConfig=true` y `dryRun=false`, pide aprobación).
- **Pipeline 799 `AI DocClassExt`** (`azure-pipelines.yml`): despliega el código (Functions, Admin y AssetResolver) y fija los app settings.
- **6/6 frente a 5 Passed + 3 NotApplicable:** el runner con `-Profile smoke` ejecuta los 6 casos de perfil smoke de `tests/e2e-postdeploy/cases/` y el criterio de la puerta 1 es que los 6 pasen. El Test Plan 100069 tiene 8 casos SMK-1 a SMK-8 y se rellena a partir de dos pasadas del runner: 5 se publican como `Passed` y 3 como `NotApplicable` (SMK-2 y SMK-7, sin caso automatizado, y SMK-8, que solo aplica a PRO). Son dos medidas distintas y las dos son correctas.
- **Carpeta temporal:** en esta guía, una carpeta fuera de lo versionado, por ejemplo `docs/auxiliares/temps/<AAAA-MM-DD>/` (está en `.gitignore`).

Mapa de pasos:

| Paso | Qué | Casilla del runbook |
|---|---|---|
| 0 | Prerrequisitos | 0.1 |
| 1 | Evidencias previas de PRE | 2.2.4 |
| 2 | Sin orquestaciones en vuelo y pausa de la ingesta | 2.2.6 |
| 3 | 807 Migrations-BD | 2.1.1, 2.1.2 |
| 4 | 832 sin altas (deployment luna) | 2.4.1 a 2.4.4 |
| 5 | 799 código y settings decisivos | 2.2.1 a 2.2.3 |
| 6 | Secretos heredados de Key Vault | 2.2.2a |
| 7 | Ciclo de vida de blobs | 2.2.5 |
| 8 | `aplicar-luna.ps1` | 2.1.3, 2.3.3 |
| 9 | Reinicio y health del GDC | 2.4.6 |
| 10 | Copia del artefacto v5 | 2.1.3 |
| 11 | Script 07 de embeddings | 2.1.3, 2.3.3 |
| 12 | 832 con altas | 2.3.3 |
| 13 | Reinicio y reanudar la ingesta | |
| 14 | Export `v1.2.0-pre` y diff frente a DEV | 2.3.2, 2.3.4 |
| 15 | Puerta 1: smoke y KQL | 2.5.1 |
| 16 | Puerta 2: golden | 2.5.2 |
| 17 | Puerta 4: deriva | 2.5.3 |
| 18 | Test Plan 100069 | 3.1 |
| 19 | Puerta 3: coste (día siguiente) | 2.5.4, 3.3 |
| 20 | Go | 3.4 |

Por qué este orden (para no «arreglarlo» sin querer):

- El 832 sin altas va primero porque crea el deployment `gpt-5.6-luna` en PRE, del que dependen las filas de luna, y no toca configuración.
- La ingesta se pausa antes del 799 porque el 799 fija `PromptDefaults__ModelKey=prompt.gpt56-luna`, que apunta a una fila de `ModeloConfigs` que **todavía no existe** hasta que corre `aplicar-luna.ps1` (paso 8): entre el 799 y el paso 8, prompt y resumen fallan en cada llamada.
- `aplicar-luna.ps1` va antes del 832 con altas: si ConfigSeed diera de alta las filas luna de DEV, entrarían con `UseAsFallback = true` y quedarían dos fallbacks por tipo (ADR-005).
- La copia del artefacto v5 va antes del script 07: el 07 exige que el blob exista, o el cargador conserva el último modelo bueno y cachea el fallo 5 minutos.

---

## Paso 0. Prerrequisitos verificables

- **Quién:** operador.
- **Dónde:** Git Bash en el clon `c:/temp/MVP/documento-ia-clasificacion-mvp`.
- **Comando:**

      git fetch origin
      git status --short
      git log --oneline -1 origin/develop
      git diff --stat bc41f289..origin/develop -- src/
      az account show --query "{usuario:user.name, suscripcion:id}" -o tsv

  Comprobar además, a mano:
  1. El fichero `tests/e2e-postdeploy/config/environments.json` existe (está en `.gitignore`; se crea copiando `environments.sample.json`, de la misma carpeta) y tiene la function key de PRE.
  2. La cuenta de `az login` tiene: usuario AAD con escritura en la BD `DocumentIA` de `srbsqlpredocai.database.windows.net`; `Storage Blob Data Reader` en `srbstgdevdocai`; `Storage Blob Data Contributor` en `srbstgpredocai`; acceso al proyecto de Azure DevOps (el script del Test Plan resuelve su token con `az account get-access-token`).
  3. Quien aprueba el check 64 del environment `pre` está localizable durante toda la ventana (el check 64 afecta a todo job de despliegue sobre `pre`: 807 Apply, 832 sin altas, cada job de despliegue del 799 y las dos del 832 con altas, ver pasos 3, 4, 5 y 12).
  4. `pwsh --version` da 7.x.
- **Resultado esperado:** el árbol local está limpio; `origin/develop` es el commit candidato y se anota su hash corto, de 8 caracteres (ese valor único, el HEAD de `origin/develop` al abrir la ventana, es el `<hash>` de todos los pasos que lo piden); `git diff --stat ... -- src/` vacío (sin cambios de código desde el candidato validado en DEV; si no está vacío, parar y avisar: el smoke de DEV no cubre ese código).
- **Si falla:** sin function key, pedirla al responsable de PRE y no seguir; sin rol de datos en un storage, el paso 10 fallará: pedir el rol antes de empezar. No se abre la ventana con un prerrequisito sin cumplir.
- **Evidencia:** el hash candidato en `release.md` (cabecera «Commit») y en la casilla 0.1 del runbook.

## Paso 1. Evidencias previas de PRE (2.2.4)

- **Quién:** operador.
- **Dónde:** Git Bash, raíz del clon. Antes de cualquier cambio en PRE.
- **Comando:**

      az functionapp config appsettings list --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91 --resource-group SRBRGPREDOCSAI --name srbapppredocai --query "[?ends_with(name, '__Endpoint') && name != 'GDC__Endpoint'].[name, value]" -o table > docs/releases/v1.2.0/evidencias/appsettings-ia-pre-antes.txt
      az functionapp config appsettings list --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91 --resource-group SRBRGPREDOCSAI --name srbapppredocai --query "[].name" -o tsv > docs/releases/v1.2.0/evidencias/appsettings-nombres-pre-antes.txt

- **Resultado esperado:** dos ficheros no vacíos. Solo endpoints y nombres: el listado completo lleva cadenas de conexión y nunca va a `evidencias/`.
- **Si falla:** `az login` caducado: repetir el login. Fichero vacío: no seguir; es de donde restaura el Anexo B (capas 2 y 3).
- **Evidencia:** los dos ficheros anteriores.

## Paso 2. Sin orquestaciones en vuelo y pausa de la ingesta

- **Quién:** operador, con el equipo que alimenta la ingesta de PRE (la pausa la hace quien envía documentos, por el canal que use; en el repo no hay script para ello).
- **Dónde:** canal acordado con quien envía documentos a PRE; comprobación desde Git Bash en la raíz del clon.
- **Para qué:** esta release cambia el modelo y los prompts. Una orquestación de Durable Functions iniciada con la versión anterior y reanudada tras el despliegue puede romper el replay o dar un resultado vacío (precedente AB#100176).
- **Comando:** la pausa no tiene comando; la hace quien alimenta la ingesta. Si no se puede pausar, esperar a una ventana sin carga. Después se comprueba que no queda ninguna instancia `Running` ni `Pending`. El task hub de la Function App se llama `DocumentIAHub` (`src/backend/DocumentIA.Functions/host.json`) y vive en la cuenta de almacenamiento del Durable hub de PRE, `srbstgpreappdocai` (`docs/infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md`). Dos formas equivalentes:

  1. Tabla de instancias del task hub (requiere un rol de datos de tablas sobre esa cuenta, por ejemplo `Storage Table Data Reader`):

         az storage entity query --account-name srbstgpreappdocai --table-name DocumentIAHubInstances --filter "RuntimeStatus eq 'Running' or RuntimeStatus eq 'Pending'" --select PartitionKey RuntimeStatus CreatedTime --auth-mode login -o table

  2. Endpoint de estado de Durable de la Function App (requiere una clave del host con permiso sobre la extensión Durable, por ejemplo la clave maestra; es el mismo endpoint que usa `docs/procedimientos/RUNBOOK_INCIDENTES_PRODUCCION.md`). Sustituir `<clave>`:

         curl -s --ssl-no-revoke -H "x-functions-key: <clave>" "https://srbapppredocai.azurewebsites.net/runtime/webhooks/durableTask/instances?runtimeStatus=Running"
         curl -s --ssl-no-revoke -H "x-functions-key: <clave>" "https://srbapppredocai.azurewebsites.net/runtime/webhooks/durableTask/instances?runtimeStatus=Pending"

  Nota: estos comandos salen de la configuración del proyecto y del runbook de incidentes; no se han ejecutado contra PRE al escribir la guía. Si la tabla no existe, listar las de la cuenta con `az storage table list --account-name srbstgpreappdocai --auth-mode login -o table` y usar la que acaba en `Instances`.
- **Resultado esperado:** la consulta de tabla devuelve 0 filas y los dos `curl` devuelven una lista vacía `[]`. La pausa dura **desde antes del paso 5 hasta después del paso 9** (reinicio posterior a `aplicar-luna.ps1`); lo normal es reanudar tras el paso 13.
- **Si falla:** si quedan instancias, esperar a que terminen y repetir la consulta; no se despliega con orquestaciones en vuelo. Si no hay forma de leer la tabla ni el endpoint (falta de rol o de clave), no seguir: pedir el rol o la clave al responsable de PRE.
- **Evidencia:** una línea en el runbook (casilla 2.2.6) con la hora de la pausa y el resultado de la consulta (0 filas).

## Paso 3. 807 Migrations-BD `pre` (2.1)

- **Quién:** operador lanza; aprobador aprueba el check 64.
- **Dónde:** Azure DevOps, pipeline 807 `Migrations-BD` (`azure-pipelines-migrations.yml`).
- **Parámetros:** `targetEnvironment=pre`, `addTransientFirewallRule=false`, `backfillClaveIntencion=false`.
- **Resultado esperado:** stages Generate y Apply en verde. En el log del stage Apply, `Pendientes: 0` y el cierre `Migrations aplicadas y verificadas correctamente`. Esta release no trae migraciones EF; el 807 solo confirma que PRE está al día.
- **Si falla:** `Pendientes` distinto de 0: leer qué migración falta y parar; PRE no está en la última migración y no se despliega código sobre un esquema atrasado. Fallo de red del agente: la Apply corre en el pool privado `docia-mdp-private`; avisar al responsable del pool.
- **Evidencia:** ID del run en la tabla «Runs de pipeline» de `release.md` (fila 807 `pre`).

## Paso 4. 832 `pre` con `applyMissingConfig=false` (deployment de luna)

- **Quién:** operador lanza; aprobador aprueba el check 64.
- **Dónde:** Azure DevOps, pipeline 832 `AI DocClassExt (AiArtifacts)` (`azure-pipelines-ai-artifacts.yml`).
- **Parámetros:** `targetEnvironment=pre`, `fromProd=false`, `preGatesPassed=false`, `releaseTag=v1.2.0-pre-artefactos` (o `auto`), `runValidation=true`, `dryRun`, `applyMissingConfig=false`. Se lanza **dos veces**: primero con `dryRun=true`, después con `dryRun=false`.
- **Resultado esperado:**
  - Con `dryRun=true`: en el log de `apply-deployments.ps1` aparece `create  gpt-5.6-luna  (...)` y los demás deployments `ok`; solo lee.
  - Con `dryRun=false`: una aprobación del check 64 (stage AiArtifacts); el log de `apply-deployments.ps1` muestra `created   gpt-5.6-luna  en N s` y en la tabla final `gpt-5.6-luna` en `Succeeded`; después `copy-cu-analyzers`, `copy-di-artifacts` y `validate-analyzer` sin `conflict` ni 401. El stage ConfigSeed publica el diff pero **no escribe**: sin `applyMissingConfig` el stage ConfigApply no existe, así que no hay segunda aprobación ni toca configuración.
- **Si falla:**
  - Por cuota de Data Zone al crear el deployment: parar la ventana. Plataforma amplía la cuota; **no se cambia a Global** (el deployment declarado es `DataZoneStandard`, 150K, en `infra/ai/deployments.pre.json`).
  - `differs` en un deployment existente: se informa, no se modifica; anotarlo y seguir solo si es un deployment ajeno a esta release.
  - `conflict` en analyzers: parar y decidir con el responsable (el pipeline no pasa `-Force`).
- **Evidencia:** el log del job de `apply-deployments.ps1` con la fila `gpt-5.6-luna` en `Succeeded` (guardar como `evidencias/832-pre-sin-altas-apply-deployments.txt`); la identidad del usuario no puede leer la cuenta de OpenAI de PRE por ARM, así que este log es la prueba. El informe `validacion-analyzers-pre.txt` (artefacto `ai-artifacts-pre-*`) y el diff de ConfigSeed a `evidencias/`. ID de los dos runs en `release.md`.

## Paso 5. 799 `pre`: código y settings decisivos (2.2.1 a 2.2.3)

- **Quién:** operador lanza; aprobador aprueba el check 64 (cada job de despliegue sobre `pre`: Functions, Admin y AssetResolver, hasta tres aprobaciones).
- **Dónde:** Azure DevOps, pipeline 799 `AI DocClassExt` (`azure-pipelines.yml`).
- **Parámetros:** `targetEnvironment=pre` (el valor por defecto del pipeline es `prod`: cambiarlo antes de lanzar), rama `develop`. La ingesta de PRE debe seguir pausada (paso 2).
- **Resultado esperado:** Build → DeployFunctions (jobs Functions y Admin) → DeployAssetResolver → ValidateConfiguration, los cuatro en verde; `RunMigrations` aparece omitido (deshabilitado a propósito). En el log del job Functions, el paso «Fijando settings decisivos (AB#101570)» termina con la línea `Settings decisivos verificados: PromptDefaults__ModelKey=prompt.gpt56-luna GDC__BypassSslValidation=false`. Leer esa línea en el log: es la comprobación de que los dos settings valen lo esperado. ValidateConfiguration confirma el contrato de app settings (`scripts/config/azure-appsettings-contract.json`).
- **Avisos:**
  - A partir de aquí y hasta el paso 8, prompt y resumen fallan en cada llamada porque la fila `prompt.gpt56-luna` aún no existe. Es esperado; por eso la ingesta está pausada.
  - El `appsettings set` del pipeline reinicia la Function App una vez más.
  - Primer run del paso «Fijando settings decisivos» en PRE: el service connection de PRE necesita permiso de escritura sobre los app settings. Si el paso falla por permisos, parar y pedirlo a plataforma.
- **Si falla:** Si falla el paso de settings decisivos o ValidateConfiguration, no seguir con el paso 6. Si falla Build o el despliegue, se corrige en `develop` y se repite el paso. Para volver atrás: Anexo B, capa 1 (Rerun from stage `DeployFunctions` del último run correcto de PRE).
- **Evidencia:** ID del run y hash desplegado en la tabla «Runs de pipeline» de `release.md`; copiar a `evidencias/799-pre-settings-decisivos.txt` la línea `Settings decisivos verificados` del log.

## Paso 6. Secretos heredados de Key Vault (2.2.2a)

- **Quién:** operador.
- **Dónde:** Git Bash, raíz del clon. Solo con ValidateConfiguration en verde (paso 5): los app settings que referenciaban los secretos ya no existen.
- **Comando** (el script admite `-WhatIf` y su único parámetro es `-TargetEnvironment`):

      pwsh ./scripts/configuration/remove-legacy-keyvault-secrets.ps1 -TargetEnvironment pre -WhatIf
      pwsh ./scripts/configuration/remove-legacy-keyvault-secrets.ps1 -TargetEnvironment pre

- **Resultado esperado:** actúa sobre el vault `srbkvpredocai` y los 4 secretos heredados (`Extraction--AzureContentUnderstanding--ApiKey`, `Extraction--GptFallback--ApiKey`, `Classification--AzureDocumentIntelligence--ApiKey`, `Classification--GptFallback--ApiKey`). Con `-WhatIf` muestra qué borraría; sin él, borra (soft delete, recuperable) y un secreto ausente se informa sin tocarlo. Una segunda ejecución no cambia nada.
- **Si falla:** error de permisos sobre el vault: pedir el rol a plataforma. Si borrar un secreto rompe algo, se recupera del soft delete (`az keyvault secret recover`), no se recrea a mano.
- **Evidencia:** la salida de las dos ejecuciones en el runbook (casilla 2.2.2a).

## Paso 7. Ciclo de vida de los recortes de clasificación (2.2.5)

- **Quién:** operador.
- **Dónde:** Git Bash, raíz del clon.
- **Comando:**

      pwsh ./scripts/storage/set-lifecycle-documents-clasif.ps1 -Environment pre -WhatIf
      pwsh ./scripts/storage/set-lifecycle-documents-clasif.ps1 -Environment pre

- **Resultado esperado:** con `-WhatIf` muestra la política resultante sin escribirla: conserva las reglas existentes de `srbstgpredocai` y añade `documents-clasif-7d`. Sin `-WhatIf` la escribe; repetirlo deja la misma política.
- **Si falla:** si el GET de la política falla con un error distinto de `ManagementPolicyNotFound`, el script aborta sin escribir: leer el error de `az rest` y no forzar nada.
- **Evidencia:** la política resultante en el runbook (casilla 2.2.5).

## Paso 8. `aplicar-luna.ps1`: filas de luna, tarifa, caché, cutover y herencia del modelo de prompt

- **Quién:** operador. Requiere usuario AAD con lectura y escritura en la BD de PRE.
- **Dónde:** Git Bash, raíz del clon. **Solo tras el 832 del paso 4 en verde** (el script no puede comprobar que el deployment `gpt-5.6-luna` existe). La ingesta sigue pausada.
- **Comando:**

      pwsh -NoProfile -File ./scripts/migrations/gpt56-luna/aplicar-luna.ps1 -Environment pre -DryRun
      pwsh -NoProfile -File ./scripts/migrations/gpt56-luna/aplicar-luna.ps1 -Environment pre

- **Aviso:** primera ejecución real en PRE, y los caminos de fallo (código 2 y 1) nunca se han ejercitado contra un entorno real. Ejecutar siempre primero con `-DryRun` y leer la tabla de la comprobación previa entera antes de lanzar sin él.
- **Resultado esperado:**
  - `-DryRun` (solo lectura): comprobación previa en verde, servidor `srbsqlpredocai.database.windows.net` y el orden de scripts 01, 02, 05, 03, 09, 08. Exigencias: filas activas `classification.gpt5-mini` (Tipo 0), `prompt.gpt5-mini` (Tipo 2), `classification.gpt4o-mini-fallback` (Tipo 0), `extraction.gpt4o-mini-fallback` (Tipo 1), `default.gpt4o-mini_ex` (Tipo 1) y la tipología activa `IBI_1.1`; ninguna tipología con los objetos `PromptConfig` y `promptConfig` a la vez; y las únicas filas con `UseAsFallback = true` de Tipo 0 y 1 son las `gpt4o-mini-fallback` (o ya las de luna).
  - Real: se aplican los seis scripts (cada uno hace su copia `ModeloConfigs__bak_<ts>` o `Tipologias__bak_<ts>` solo si cambia algo). Verificación final impresa: exactamente un `UseAsFallback = true` activo por Tipo 0 y por Tipo 1, que son `classification.gpt56-luna` y `extraction.gpt56-luna`, y **cero tipologías** con `default.gpt4o-mini` como clave de prompt. Código de salida 0. Una segunda ejecución es idempotente.
- **Si falla:** código de salida 2: la comprobación previa falló y no se tocó nada; el script lista lo que falta (por ejemplo, otra fila con `UseAsFallback = true` haría fallar el 09 con `THROW 50038`; casing mixto, el 08 con `THROW 50053`). Corregir la causa y repetir desde el `-DryRun`. Código 1: error de parámetros, de un script o de la verificación final; se detiene en el primer script que falla y los anteriores quedan aplicados (cada uno es idempotente): guardar la salida completa y no improvisar; si hace falta deshacer, ver «Vuelta atrás por paso».
- **Evidencia:** la salida completa del `-DryRun` y de la ejecución real y la verificación final (`evidencias/aplicar-luna-pre.txt`); nombres de las copias `__bak_*` creadas.

## Paso 9. Reinicio de la Function App y health del GDC

- **Quién:** operador.
- **Dónde:** Git Bash.
- **Comando:**

      az functionapp restart --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91 --resource-group SRBRGPREDOCSAI --name srbapppredocai

  Esperar 1 a 2 minutos a que arranque (o los 5 minutos de la caché de configuración, si no se reinicia), y después:

      curl -s --ssl-no-revoke -X POST https://srbapppredocai.azurewebsites.net/api/healthcheck

  (`POST` anónimo, sin function key; el resultado se cachea 45 s en el servidor. Detalle de los probes en `docs/manuales/MANUAL_HEALTHCHECK.md`.)
- **Resultado esperado:** HTTP 200 y el componente del GDC en `healthy` con `Reachable (document not found)` o `Reachable (document found)`, con `GDC__BypassSslValidation=false` (validación real del certificado contra la CA de SAREB, AB#101553). Los demás componentes en `healthy`; el agregado no puede ser `unhealthy`.
- **Aviso:** las hojas TLS del GDC de PRE caducan el 2027-01-20 (AB#101556); anotarlo, no afecta a esta ventana.
- **Si falla:** si el GDC sale `unhealthy` con un error de certificado (`UntrustedRoot` u otro fallo de cadena), la CA de SAREB no valida la hoja de PRE: **no se pone `GDC__BypassSslValidation=true`** (es solo un interruptor de emergencia que acepta cualquier certificado) sin decisión explícita del usuario; parar y consultar. Si el GDC da `degraded` por timeout (más de 30 s), repetir una vez; si persiste, anotarlo.
- **Evidencia:** la salida del health en `evidencias/health-pre-gdc.txt` (sin secretos) y la hora del reinicio en el runbook.

## Paso 10. Copia del artefacto v5 de embeddings de DEV a PRE

- **Quién:** operador. Requiere `Storage Blob Data Reader` en `srbstgdevdocai` y `Storage Blob Data Contributor` en `srbstgpredocai`.
- **Dónde:** Git Bash, raíz del clon.
- **Comando** (origen `dev` por defecto):

      pwsh -NoProfile -File ./scripts/storage/copy-embeddings-artifacts.ps1 -Version v5 -TargetEnvironment pre -DryRun
      pwsh -NoProfile -File ./scripts/storage/copy-embeddings-artifacts.ps1 -Version v5 -TargetEnvironment pre

- **Aviso:** primera ejecución real del script: la ruta de escritura (`az storage blob upload` sin `--overwrite`, con comprobación de `contentMd5`) no se ha ejercitado. Hacer siempre el `-DryRun` y comprobar la tabla antes de la ejecución real.
- **Resultado esperado:**
  - `-DryRun`: tabla por blob del prefijo `modelos/clasificador-embeddings/v5/` en el contenedor `documentai`, con estado `copy` (no existe en PRE), `present` (mismo MD5) o `conflict` (otro MD5). 0 escrituras. Código de salida 0.
  - Real: cada blob termina en `copiado` (MD5 del destino igual al del origen) o `present`. Código 0.
- **Si falla:** `conflict` sin `-Force`: el script aborta antes de escribir nada y sale con código 1; **parar y decidir con el responsable** (no se añade `-Force` por costumbre: sobrescribiría un artefacto distinto en PRE). `md5-distinto` tras copiar: error, código 1; repetir la copia una vez y, si persiste, parar. Origen sin blobs o `az` falla: revisar roles y `az login`.
- **Evidencia:** la tabla del `-DryRun` y la de la ejecución real (`evidencias/copy-embeddings-v5-pre.txt`).

## Paso 11. Script 07 de embeddings en PRE

- **Quién:** operador. Usuario AAD con escritura en la BD de PRE.
- **Dónde:** Git Bash, raíz del clon. Solo con el blob v5 ya en `srbstgpredocai` (paso 10).
- **Comando** (el fichero versionado ya lleva `@Modo = hibrido` y `@Version = v5`; fuera de DEV el switch `-PermitirNoDev` es obligatorio):

      pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/clasificador-embeddings/07-modeloconfig-embeddings-modo-version.sql -Server srbsqlpredocai.database.windows.net -PermitirNoDev

- **Resultado esperado:** crea la copia `ModeloConfigs__bak_<ts>` y un único `UPDATE`; el `SELECT` final muestra la fila `clasificador.embeddings` con `Modo = hibrido`, `ModoRestringido = hibrido` y `BlobPath = modelos/clasificador-embeddings/v5/clasificador-embeddings-v5.json`. La segunda ejecución imprime `0 cambios` y no crea copia.
- **Si falla:** `THROW 50052` (no existe la fila `clasificador.embeddings` activa): falta aplicar antes el script 01 (`scripts/migrations/clasificador-embeddings/01-modeloconfig-clasificador-embeddings.sql`); parar y consultar, porque la fila ya existe en PRE y su ausencia no es esperada. Cualquier error de validación sale antes de crear la copia. La app refresca la configuración en 5 minutos como máximo (el reinicio del paso 13 lo fuerza).
- **Evidencia:** la salida completa y el nombre de la copia `__bak` en `evidencias/embeddings-07-pre.txt`.

## Paso 12. 832 `pre` con `applyMissingConfig=true` (altas por clave natural)

- **Quién:** operador lanza; aprobador aprueba el check 64 (dos veces en el run real: stages AiArtifacts y ConfigApply).
- **Dónde:** Azure DevOps, pipeline 832.
- **Parámetros:** `targetEnvironment=pre`, `fromProd=false`, `preGatesPassed=false`, `releaseTag=v1.2.0-pre-altas` (o `auto`), `runValidation=false` (los analyzers ya se validaron en el paso 4), `applyMissingConfig=true`, y `dryRun` primero `true` y después `false`.
- **Resultado esperado:**
  - `dryRun=true`: ConfigSeed publica `config-diff` con `altas-config.sql`. Esperadas **26 altas**: 24 tipologías y las dos de `CatalogoTdn2` (`CERJ-71` y `LIPR-13`). **Si el SQL de altas incluye filas de `ModeloConfigs` de luna (por ejemplo `classification.gpt56-luna`), PARAR:** significa que `aplicar-luna.ps1` no se aplicó (el ensayo previo del 2026-11-26 daba 30 con las 4 filas de luna) y dos fallbacks por tipo romperían el registro (ADR-005). Volver al paso 8.
  - `dryRun=false`: aprobar el check 64 en AiArtifacts y, tras revisar `config-diff` y `altas-config.sql`, en ConfigApply. `apply-config-altas.ps1` copia cada tabla que recibe altas en `<Tabla>__bak_configseed_build<id>`, ejecuta las altas en una transacción y el diff posterior debe salir con **0 altas pendientes** (artefacto `config-apply-<intento>`).
- **Si falla:** el SQL de altas rechaza un `ModeloConfigs` con `Endpoint` o `ApiKey` con valor: parar, no se edita a mano. Si ConfigApply falla, la transacción deshace las altas de ese run; leer el error y repetir. Las altas ya aplicadas no se deshacen solas: ver «Vuelta atrás por paso».
- **Evidencia:** `config-diff` y `config-apply-<intento>` descargados a `evidencias/`; ID de los dos runs en `release.md`; nombres de las tablas `__bak_configseed_build<id>` para la limpieza diferida del runbook (5.6).

## Paso 13. Reinicio de la Function App y reanudar la ingesta

- **Quién:** operador, con quien alimenta la ingesta.
- **Dónde:** Git Bash.
- **Comando:**

      az functionapp restart --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91 --resource-group SRBRGPREDOCSAI --name srbapppredocai

  (carga las altas de tipologías, la fila de embeddings del paso 11 y fuerza la lectura de la configuración, sin esperar la caché de 5 minutos.) Después, repetir el `curl` del paso 9 y comprobar HTTP 200.
- **Resultado esperado:** Function App arrancada y health sin `unhealthy`. Se avisa a quien alimenta la ingesta de PRE para reanudarla.
- **Si falla:** si no arranca, revisar App Insights `srbappipredocai` (errores de arranque) y volver a la capa 1 del Anexo B. No reanudar la ingesta con un health en `unhealthy`.
- **Evidencia:** hora del reinicio y de la reanudación en el runbook.

## Paso 14. Export `v1.2.0-pre` y diff frente a DEV (2.3.2 y 2.3.4)

- **Quién:** operador.
- **Dónde:** Git Bash, raíz del clon.
- **Comando:**

      pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqldevdocai.database.windows.net -ReleaseTag v1.2.0
      pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqlpredocai.database.windows.net -ReleaseTag v1.2.0-pre
      python scripts/database/diff-config-exports.py artifacts/db-config/config-v1.2.0-pre.sql artifacts/db-config/config-v1.2.0.sql ModeloConfigs=Key PromptTemplates=PromptKey+Version Tipologias=Codigo CatalogoTdn1=Codigo CatalogoTdn2=Codigo PluginTipologiaConfigs=TipologiaCodigo

  El primer export (DEV, solo lectura) es la referencia de la casilla 1.3 del runbook. Debe haberse generado después de que DEV pasase a híbrido (2026-11-26 12:57:42Z). Si `artifacts/db-config/config-v1.2.0.sql` ya existía, ver su fecha con `stat -c '%y' artifacts/db-config/config-v1.2.0.sql` (hora local; España es UTC+1 en noviembre, así que debe ser posterior a las 13:57:42 del 2026-11-26). Si es anterior o hay duda, ejecutar el primer comando, que lo sobrescribe. Ambos exports son solo lectura y salen en `artifacts/db-config/` (en `.gitignore`).

  **Dependencia:** este paso solo vale con los pasos 8 a 13 aplicados (luna, copia v5, script 07 y altas del 832) y la Function App reiniciada en el paso 13; si falta alguno, el diff mostrará diferencias que no son reales.
- **Resultado esperado:** `config-v1.2.0-pre.hashes.json` generado (es la referencia de la puerta 4). El diff termina sin error y solo deja diferencias cosméticas: `Id`, fechas, `ApiKey` vacía y `Endpoint` nulo. **Diferencia conocida y aceptada:** la tipología `IBI` (Id 1006 en DEV) extrae con `nota.simple.1_4.azure-cu` en DEV y con `default.gpt4o-mini_ex` en PRE; ninguna parte de la release la iguala (decisión pendiente del usuario, no bloquea). Si aparece la diferencia de `CatalogoTdn1.Descripcion` por CRLF, es el fleco conocido de RELEASE_MANAGEMENT (2.3). El ensayo del 832 del 2026-11-26, antes de aplicar nada, dio 16 `ModeloConfigs`, 199 `Tipologias` y 2 `PromptTemplates` distintas: tras los pasos 8 a 12 solo pueden quedar las cosméticas; cualquier otra se explica por escrito en `runbook.md` antes de seguir.
- **Si falla:** error del diff por clave inexistente o repetida: es un fallo de datos, no de formato; parar. Diferencias no explicadas: no se pasa a las puertas hasta documentarlas o corregirlas con un seed de la release (nunca con el export completo; RELEASE_MANAGEMENT 2.3).
- **Evidencia:** `config-v1.2.0-pre.hashes.json` y la salida del diff a `evidencias/`.

## Paso 15. Puerta 1: smoke E2E y KQL de hosts de IA (2.5.1)

- **Quién:** operador.
- **Dónde:** Git Bash para el runner; portal de Azure, App Insights de PRE `srbappipredocai` > **Logs**, para las consultas.
- **Comando:**

      pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pre -Profile smoke

  Anotar la hora de inicio y de fin (UTC) del run. El informe queda en `tests/e2e-postdeploy/artifacts/<timestamp>-pre-smoke`: **conservar ese directorio**, el paso 18 lo usa como primera pasada del Test Plan.

  Después, en App Insights de PRE, ejecutar las tres consultas siguientes sustituyendo `<inicio>` y `<fin>` por la ventana del smoke en UTC, con formato `2026-11-26T14:05:30Z`.

  1. Hosts de IA usados en la ventana:

         dependencies
         | where timestamp between (datetime(<inicio>) .. datetime(<fin>))
         | where type == "Http"
         | extend host = tostring(parse_url(data).Host)
         | where host has_any ("openai.azure.com", "services.ai.azure.com", "cognitiveservices.azure.com")
         | summarize llamadas = count(), no_ok = countif(resultCode !in ("200", "201", "202")) by host, resultCode
         | order by host asc

     Esperado: solo hosts de PRE (`srbaisrv01predocai`, `srbaisrv02predocai`, `srbdipredocai`) y, en `resultCode`, solo 200, 201 o 202 (ningún 401 ni 403). Si no devuelve filas, es que en PRE el campo `data` llega vacío: usar la consulta 2.

  2. Variante por `target` (la que suele hacer falta en PRE):

         dependencies
         | where timestamp between (datetime(<inicio>) .. datetime(<fin>))
         | where target has_any ("openai.azure.com", "services.ai.azure.com", "cognitiveservices.azure.com")
         | summarize llamadas = count(), no_ok = countif(resultCode !in ("200", "201", "202")) by target, resultCode
         | order by target asc

     Esperado: igual que la 1: solo hosts de PRE y ningún 401 ni 403.

  3. Hosts de PRO en la ventana:

         union dependencies, traces
         | where timestamp between (datetime(<inicio>) .. datetime(<fin>))
         | where target has "prodocai" or target has "upe48" or name has "prodocai" or message has "prodocai"
         | summarize n = count()

     Esperado: `n = 0`.
- **Resultado esperado:** 6/6 PASS del runner; consulta 1 o 2 con solo hosts de PRE y cero 401/403; consulta 3 con `n = 0`.
- **Si falla:** un FAIL del runner: leer `report.md` del directorio, corregir en `develop` y repetir la Fase 2 entera con un commit nuevo (no se parchea PRE). Cualquier host de PRO o `n > 0` en la consulta 3: parar; hay una dependencia a IA de PRO que no debería existir.
- **Evidencia:** el resumen del run y las tres consultas con su salida en `evidencias/puerta1-kql-smoke-pre-<AAAA-MM-DD>.txt`; fila 1 de la tabla de aprobación de `release.md`.

## Paso 16. Puerta 2: golden contra la línea base de luna en DEV (2.5.2)

- **Quién:** operador.
- **Dónde:** repositorio `c:/temp/MVP/DocumentIA.Batch` (el comando no se lanza desde este clon). **Dependencia:** solo con los pasos 8 a 13 aplicados y la Function App de PRE reiniciada; la golden mide la configuración final.
- **Datos que necesita:**
  - `<corpus>`: la carpeta local con los documentos del golden. El valor por defecto del harness es `H:\Documentia\ParaNacho\Class` (`src/DocumentIA.Batch.Evaluation/Program.cs`); si en esa máquina el corpus está en otra ruta, se pasa con `--corpus-root`.
  - `<eval-config-pre.json>`: copia local, fuera de git y fuera de `evidencias/`, de `src/DocumentIA.Batch.Evaluation/config.sample.json` con el entorno `PRE` (`BackendUrl` `https://srbapppredocai.azurewebsites.net`, `FunctionKey` de `environments.json`) y `SelectedEnvironment=PRE`. Lleva la function key: no se versiona. De modelo sirve `eval/runs/config-dev-luna.json`, que es el equivalente para DEV.
- **Comando:**

      cd c:/temp/MVP/DocumentIA.Batch
      dotnet run --project src/DocumentIA.Batch.Evaluation -c Debug -- run --set golden --env PRE --parallel 2 --label v1.2.0-pre --corpus-root <corpus> --config <eval-config-pre.json>
      dotnet run --project src/DocumentIA.Batch.Evaluation -c Debug -- compare --a eval/runs/20261119-155707-gpt56-luna-golden --b eval/runs/<run-PRE>

  `dotnet run` compila antes de ejecutar. `<run-PRE>` es el directorio nuevo `eval/runs/<AAAAMMDD-HHMMSS>-v1.2.0-pre` que crea el primer comando. La línea base de DEV con luna es el golden completo AB#101433 (`20261119-155707-gpt56-luna-golden`). El `compare` escribe `compare.md`; el comando muestra su ruta.
- **Cómo se lee `compare.md`:** para TDN1 y para TDN2 hay una sección con la línea `McNemar exacto, p-valor de dos colas: <valor>` y la línea `Veredicto: ...`; al final, `Veredicto global`. **PASS** = el p-valor es **mayor o igual que 0,05** en las dos secciones (veredicto «diferencia dentro del ruido»). Ejemplo de v1.1.0: `McNemar exacto, p-valor de dos colas: 0.0654` en TDN1 y `0.7539` en TDN2, con `Veredicto: **diferencia dentro del ruido**`. Un p-valor menor de 0,05 es una diferencia real y no es PASS. Además, el encabezado debe decir `Documentos evaluados en comun (interseccion por rel_path): 467` y el run no debe tener timeouts.
- **Resultado esperado:** PASS según lo anterior. Anotar en `release.md` el commit del repo DocumentIA.Batch usado (`git -C c:/temp/MVP/DocumentIA.Batch rev-parse --short HEAD`).
- **Si falla:** la línea base de luna es anterior al modo híbrido v5 de DEV. Si el p-valor baja de 0,05 por el híbrido y no por el modelo, regenerar la línea base en DEV (RELEASE_MANAGEMENT: «si la release cambia el modelo, regenerar la línea base en DEV antes») y repetir el `compare`. No se declara PASS sin un p-valor medido. Un p-valor menor sin explicación: parar y consultar.
- **Evidencia:** copiar `compare.md` a `evidencias/puerta2-compare-golden-pre.md`; fila 2 de la tabla de aprobación. Anotar la hora de inicio y de fin (UTC) de la golden, `StartedAtUtc` y `CompletedAtUtc` de `<run-PRE>/run-info.json`: el paso 19 las necesita.

## Paso 17. Puerta 4: deriva (2.5.3)

- **Quién:** operador.
- **Dónde:** Git Bash, raíz del clon. Tras terminar las puertas 1 y 2.
- **Comando:**

      pwsh ./scripts/ai/export-config-release.ps1 -SourceServer srbsqlpredocai.database.windows.net -ReleaseTag v1.2.0-pre-puerta4

  Comparar `artifacts/db-config/config-v1.2.0-pre-puerta4.hashes.json` con `config-v1.2.0-pre.hashes.json` del paso 14. Los ficheros llevan una marca de hora (`generatedAtUtc`) que siempre difiere, así que se comparan solo los hashes:

      python -c "import json,sys; a=json.load(open(sys.argv[1]))['hashes']; b=json.load(open(sys.argv[2]))['hashes']; print({k:('IGUAL' if a[k]==b[k] else 'DISTINTO') for k in a}); sys.exit(0 if a==b else 1)" artifacts/db-config/config-v1.2.0-pre.hashes.json artifacts/db-config/config-v1.2.0-pre-puerta4.hashes.json

  Código de salida 0 e `IGUAL` en las cinco tablas = PASS.
- **Resultado esperado:** hash idéntico en `ModeloConfigs`, `PromptTemplates`, `Tipologias`, `CatalogoTdn1` y `CatalogoTdn2` (compara PRE consigo mismo, no con DEV).
- **Si falla:** cualquier hash distinto es deriva desde la referencia: explicarla por escrito en `runbook.md` o corregirla antes de la Fase 3. Las puertas 1 y 2 pueden mover `ModeloConfigs` si algo escribe en la tabla; investigar qué.
- **Evidencia:** los dos `.hashes.json` en `evidencias/`; fila 4 de la tabla de aprobación.

## Paso 18. Test Plan 100069 (3.1)

- **Quién:** operador. Token de Azure DevOps vía `az login` (el script lo resuelve con `az account get-access-token`).
- **Dónde:** Git Bash, raíz del clon.
- **Comando:** la primera pasada es la del paso 15: su directorio `tests/e2e-postdeploy/artifacts/<timestamp>-pre-smoke` es `<dir1>`. La segunda pasada es el juego extra (SMK-3 y SMK-4) y su directorio es `<dir2>`:

      pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pre -Profile smoke -CasesDir tests/e2e-postdeploy/cases-smk
      pwsh ./tests/e2e-postdeploy/ado/publish-smk-run.ps1 -Environment pre -Candidate <hash> -ReportDirs <dir1>,<dir2> -WhatIf
      pwsh ./tests/e2e-postdeploy/ado/publish-smk-run.ps1 -Environment pre -Candidate <hash> -ReportDirs <dir1>,<dir2>

  `<hash>` es el del paso 0. `<dir1>` y `<dir2>` se escriben con la ruta relativa al clon, separados por una coma sin espacios.
- **Resultado esperado:** la vista previa (`-WhatIf`) muestra 5 `Passed` (SMK-1, 3, 4, 5 y 6) y 3 `NotApplicable` (SMK-2 y SMK-7 sin caso automatizado, SMK-8 solo PRO). La publicación crea un run llamado `Smoke pre-release v1.2.0 (PRE, <hash>)` en el plan 100069 y escribe `ado-run-<id>.json` en `<dir2>`.
- **Si falla:** código 2 del script: un directorio no existe, un `report.md` declara otro entorno distinto de `pre` o falta credencial. Si falla la validación de los puntos de prueba (primera publicación real con `-IncludeRootSuite`), guardar el error y publicar el run a mano con la API REST de Azure DevOps (organización `https://sareb.visualstudio.com`, proyecto `AI DocClassExt`, cabecera `Authorization: Bearer <token de az>`; es lo que hace `tests/api-tests/ado-testplans-common.ps1`). Base: `https://sareb.visualstudio.com/AI%20DocClassExt/_apis/test`.
  1. `GET <base>/Plans/100069/Suites/100070/points?api-version=7.1`: lista los 8 puntos de prueba; anotar el `id` de cada uno.
  2. `POST <base>/runs?api-version=7.1` con cuerpo `{"name":"Smoke pre-release v1.2.0 (PRE, <hash>)","plan":{"id":100069},"pointIds":[<ids de los 8 puntos>],"isAutomated":true}`: devuelve el `id` del run (`<runId>`).
  3. `GET <base>/runs/<runId>/results?api-version=7.1`: devuelve un resultado por punto, con su `id` (`<resultId>`).
  4. `PATCH <base>/runs/<runId>/results?api-version=7.1` con una lista de `{"id":<resultId>,"outcome":"Passed" o "NotApplicable","state":"Completed","comment":"<caso del runner>"}` según el mapeo que mostró el `-WhatIf`; después `PATCH <base>/runs/<runId>?api-version=7.1` con `{"state":"Completed"}`.

  Precedente: en v1.1.1 el run se creó primero sin resultados y se completó después con estos mismos pasos (`docs/releases/v1.1.1/evidencias/testplan-100069-smoke-pre-2026-10-16.txt`). No se declara el SMK como pasado sin run publicado.
- **Evidencia:** `evidencias/testplan-100069-smoke-pre-<AAAA-MM-DD>.txt` con la tabla SMK, el ID del run y la URL; el ID del run en `release.md` (línea «Test Plan 100069»).

## Paso 19. Puerta 3: coste, al día siguiente (2.5.4 y 3.3)

- **Quién:** operador; el día después del smoke y la golden, porque Cost Management ingiere con un día de retraso. Obligatoria en esta release porque cambia el modelo. La Fase 3 puede empezar con esta puerta pendiente.
- **Dónde:** Git Bash.
- **Dato previo:** la ventana UTC de las puertas 1 y 2, anotada en los pasos 15 y 16 (inicio del smoke y `CompletedAtUtc` de la golden, en `<run-PRE>/run-info.json`).
- **Comando:**
  1. Factura de IA de PRE por recurso y medidor para el día del smoke y la golden (lectura de Cost Management; con `curl` y el token de `az` devuelve 401, con `az rest` funciona). Crear en la carpeta temporal un `query.json`, con la fecha del día de las puertas:

         {"type":"Usage","timeframe":"Custom","timePeriod":{"from":"<AAAA-MM-DD>T00:00:00Z","to":"<AAAA-MM-DD>T23:59:59Z"},"dataset":{"granularity":"Daily","aggregation":{"totalCost":{"name":"Cost","function":"Sum"},"usage":{"name":"UsageQuantity","function":"Sum"}},"grouping":[{"type":"Dimension","name":"ResourceId"},{"type":"Dimension","name":"Meter"}]}}

     y lanzar (endpoint y versión de `scripts/migrations/costes-ia/README.md`; la API tiene un límite de peticiones agresivo: espaciar los reintentos):

         az rest --method post --url "https://management.azure.com/subscriptions/a4f6b357-8f13-4488-9ee8-b9f635426f91/resourceGroups/SRBRGPREDOCSAI/providers/Microsoft.CostManagement/query?api-version=2023-03-01" --body @<carpeta-temporal>/query.json

     Leer las filas `[coste, cantidad, fecha, ResourceId, Meter, moneda]` de los recursos `srbaisrv01predocai`, `srbaisrv02predocai` y `srbdipredocai`. Equivale a **Cost Management > Cost analysis** en el portal, ámbito el grupo `SRBRGPREDOCSAI`, agrupando por *Resource* y *Meter*. Para comprobar que PRO no sube, repetir con la suscripción `647c7246-54bc-4d31-b909-431cacf03272` y el grupo `SRBRGDOCSAIPROD`.
  2. Coste y tokens por ejecución en PRE. El fichero `scripts/migrations/gpt56-luna/medicion/01-coste-tokens-por-pasada.sql` lleva tres variables fijas en su cabecera (`@From`, `@To`, `@SubmittedByPrefix`). Copiarlo a la carpeta temporal como `01-coste-pre.sql` y ajustarlas **en la copia**: `@From` = inicio de la golden de PRE (UTC, formato `2026-11-27 08:00:00`), `@To` = `CompletedAtUtc` de la golden más unos minutos (el límite es exclusivo) y `@SubmittedByPrefix` = `N'DocumentIA.Batch.Evaluation/'` (el prefijo con el que el harness firma sus ejecuciones; es el valor que ya trae el fichero). Ejecutar con la copia:

         pwsh -NoProfile -Command "& ./scripts/database/query-sql.ps1 -Server srbsqlpredocai.database.windows.net -Query (Get-Content -Raw <carpeta-temporal>/01-coste-pre.sql)"

     (El fichero se lee desde PowerShell; desde Bash, `-Query "$(cat ...)"` rompe el análisis del argumento.) Repetir contra `srbsqldevdocai.database.windows.net` con la ventana de la golden de DEV de la línea base (`StartedAtUtc` y `CompletedAtUtc` en `eval/runs/20261119-155707-gpt56-luna-golden/run-info.json` del repo `c:/temp/MVP/DocumentIA.Batch`) para tener los dos lados.
- **Resultado esperado:** meters de IA (`gpt … Tokens`, `S0 Pre-built Pages`) facturados en `SRBRGPREDOCSAI`; PRO sin rampa de tokens ni de páginas atribuible a la release; en la consulta SQL, coste medio por ejecución y porcentaje de caché de PRE en el mismo orden de magnitud que los de DEV con luna.
- **Si falla:** si el coste de PRE se aparta claramente del de DEV, anotar la diferencia y consultar antes del Go. Si la factura no cuadra con las ejecuciones, revisar que la ventana y el prefijo son los correctos.
- **Evidencia:** las filas de la factura y la salida de la consulta en `evidencias/puerta3-coste-pre.txt`; fila 3 de la tabla de aprobación.

## Paso 20. Go (3.4)

- **Quién:** quien aprueba la release (el responsable del repo).
- **Dónde:** `docs/releases/v1.2.0/release.md`, apartado «Aprobación».
- **Qué se escribe:** la tabla con las cuatro puertas, fecha y resultado (la 3 puede figurar pendiente o N/A con motivo), el run del Test Plan 100069, la línea `Go: <fecha> · <nombre>` y la revisión 3.2 del diff (cada `AB#` en To Validate o superior si es PBI o Bug, y en Done si es Task).
- **Resultado esperado:** todas las casillas 2.x del runbook marcadas o tachadas con motivo; ninguna puerta en FAIL.
- **Si falla:** una puerta en FAIL: se corrige en `develop`, se vuelve a 0.1 con un commit nuevo y se repite la Fase 2 entera.
- **Evidencia:** el propio `release.md`. Después se sigue con la Fase 4 (PRO) del runbook; esta guía no la cubre.

---

## Vuelta atrás por paso

Regla general: se retrocede solo la capa que falló, de menos a más invasiva, y nunca se empieza por la base de datos. Las cuatro capas del Anexo B de RELEASE_MANAGEMENT, resumidas:

1. **Código.** Azure DevOps > pipeline 799 > último run correcto en `pre` > **Rerun** > **Rerun from stage** > `DeployFunctions`; después `ValidateConfiguration` y el smoke del paso 15. No hay deployment slots ni swap.
2. **Configuración.** En la BD, restaurar `ConfiguracionJson` desde la copia `ModeloConfigs__bak_<fecha>` que creó el script SQL, con un `UPDATE ... FROM` sobre la columna cambiada y dentro del mismo entorno (el patrón completo está en la fila del 08 de más abajo); las demás tablas, desde su propia `__bak`. App settings: relanzar el 799 no restaura nada, porque `ensure-app-settings.ps1` no sobrescribe claves existentes; se devuelven a mano con `az functionapp config appsettings set --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91 --resource-group SRBRGPREDOCSAI --name srbapppredocai --settings "CLAVE=VALOR"`, con los valores de las evidencias del paso 1, y se reinicia la Function App.
3. **Alias de IA.** Solo si cambiaron alias o endpoints de IA (esta release no los cambia): devolver el bloque de `pre` de `infra/ai/pipeline-variables.yml` a los hosts anteriores, forzar con `appsettings set` las claves `AI__Resources__*__Endpoint` a los valores de las evidencias del paso 1, restaurar `ConfiguracionJson` desde la copia `__bak` anterior al script de alias, reiniciar y repetir el smoke.
4. **Esquema.** Solo si una migración corrompió datos (esta release no trae migraciones): restaurar la copia `DocumentIA-prerel-<fecha>` con `az sql db copy` con un nombre nuevo, parar la Function App, renombrar la BD actual a `DocumentIA-failed-<fecha>` y la copia a `DocumentIA`, desplegar el commit anterior (capa 1) y arrancar. Se pierden las ejecuciones entre la copia y el fallo: anotarlas en `runbook.md`.

Complementos de esta release, por paso. Todos los scripts SQL se aplican contra PRE con `apply-sql-dev.ps1 ... -Server srbsqlpredocai.database.windows.net -PermitirNoDev`.

| Qué se deshace | Cómo |
|---|---|
| Código y settings (paso 5) | Capas 1 y 2 de arriba. |
| Luna entera (paso 8) | `pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/10-rollback-cutover-gpt56-luna-por-codigo.sql -Server srbsqlpredocai.database.windows.net -PermitirNoDev`. Devuelve `UseAsFallback = true` a las filas `gpt4o-mini-fallback`, lo quita de las de luna y devuelve la tipología `IBI_1.1` a `default.gpt4o-mini_ex`; hace su propia copia `ModeloConfigs__bak_<ts>` y `Tipologias__bak_<ts>` solo si hay algo que cambiar. **No borra filas:** las filas de luna que insertaron los scripts 01, 02 y 05 quedan inertes sin el indicador de fallback y no hace falta eliminarlas. Además hay que cambiar la variable `PROMPT_DEFAULTS_MODEL_KEY` del bloque `pre` de `azure-pipelines.yml` a `default.gpt4o-mini` y redesplegar con el 799: el pipeline fija el setting en cada despliegue, así que un `appsettings set` a mano no sobrevive al siguiente. Los cambios llegan a la app en 5 minutos como máximo (o con el reinicio). |
| Caché de prompt de luna (paso 8, script 03) | `pwsh ./scripts/database/apply-sql-dev.ps1 -ScriptPath scripts/migrations/gpt56-luna/04-dev-promptcache-off-gpt56-luna.sql -Server srbsqlpredocai.database.windows.net -PermitirNoDev` |
| Tipologías del 08 (paso 8) | Usar la copia `Tipologias__bak_<ts>` que creó el 08 (su nombre sale en la salida de `aplicar-luna.ps1`, mensaje `Backup creado: dbo.Tipologias__bak_<ts>`; para deshacer también el 09, la copia del 09, anterior). Guardar en la carpeta temporal un fichero `restaurar-tipologias.sql` con la sentencia siguiente (sustituyendo `<ts>`) y aplicarlo con `apply-sql-dev.ps1 -ScriptPath <carpeta-temporal>/restaurar-tipologias.sql -Server srbsqlpredocai.database.windows.net -PermitirNoDev`: `UPDATE t SET t.ConfiguracionJson = b.ConfiguracionJson FROM dbo.Tipologias t JOIN dbo.Tipologias__bak_<ts> b ON b.Id = t.Id WHERE t.ConfiguracionJson <> b.ConfiguracionJson;` (el 08 solo cambia esa columna). |
| Embeddings (paso 11) | Copiar el script 07 a la carpeta temporal, cambiar en la copia `@Modo = N'sombra'` (o `N'off'`) y/o `@Version = N'v1'` y aplicar la copia con `apply-sql-dev.ps1 -ScriptPath <copia> -Server srbsqlpredocai.database.windows.net -PermitirNoDev` (nunca se edita el fichero versionado); o restaurar `ConfiguracionJson` de la fila `clasificador.embeddings` desde la copia `ModeloConfigs__bak_<ts>` del 07. |
| Altas del 832 (paso 12) | No se deshacen automáticamente. `apply-config-altas.ps1` deja antes de insertar la copia `<Tabla>__bak_configseed_build<id>` de cada tabla que recibe altas, y las altas son solo `INSERT` por clave natural: se revierten borrando por clave natural las filas que no estén en esa copia, con sí explícito del responsable. No hay script para ello. |
| Secretos de Key Vault (paso 6) | Soft delete, recuperables mientras dure la retención: `az keyvault secret recover --vault-name srbkvpredocai --name <secreto>` (los cuatro nombres están en el paso 6). Los app settings que los referenciaban ya no existen y el código no los lee (AB#101450). |
| Ciclo de vida (paso 7) | Escribir de nuevo la política con las reglas anteriores (la salida del `-WhatIf` previo las muestra). |

Si se aborta la ventana con la configuración a medias, la ingesta de PRE no se reanuda hasta que el health esté sano y prompt y resumen funcionen (fila `prompt.gpt56-luna` presente, o el setting de vuelta a `default.gpt4o-mini` junto con su fila).
