# pipelines/

Step templates y plantilla de variables que componen los pipelines de DocumentIA (AB#101570, hito 1 de la pipeline de release). Los YAML de la raíz (`azure-pipelines.yml`, `azure-pipelines-migrations.yml`, `azure-pipelines-ai-artifacts.yml`) siguen siendo los pipelines que se lanzan; esta carpeta solo contiene los bloques que reutilizan.

## Regla

Los pipelines 799, 807 y 832 componen estos templates sin cambiar parámetros, nombres de stage, artefactos ni comportamiento. La pipeline Release del hito 2 los reutilizará en jobs normales (no `deployment`), de modo que un cambio en un template se refleja en las dos formas de lanzar la misma operación.

## Estructura

- `templates/steps/`: once templates de pasos (`steps:`), uno por bloque de trabajo.
- `templates/variables/entorno.yml`: variables por entorno (`dev`, `pre`, `prod`) del despliegue. Parámetro `environment` (valores `dev`, `pre`, `prod`). En este hito solo la consume el 799; el 807 y el 832 conservan sus propias variables, y la pipeline Release del hito 2 la usará. Las variables `SQL_SERVER_NAME` y `TASKHUB_STORAGE_ACCOUNT` son nuevas respecto al bloque original del 799.

## Templates de pasos

Los parámetros son `string` salvo donde se indica `boolean`. «Checkout» y «download» indican si el template incluye `checkout: self` y los `download` del artefacto de entrada.

| Template | Procede de | Parámetros | Publica | Consume | Checkout / download |
|---|---|---|---|---|---|
| `migrations-generate.yml` | 807 (stage Generate) | ninguno | `MigrationsScript` (`migrations.sql`) | | checkout |
| `migrations-apply.yml` | 807 (stage Apply) | `serviceConnection`, `resourceGroup`, `sqlServerName`, `sqlDatabaseName`, `addTransientFirewallRule` (boolean), `backfillClaveIntencion` (boolean) | | `MigrationsScript` | checkout y download |
| `ai-export.yml` | 832 (job Export) | `serviceConnection`, `sqlServer`, `sqlDatabaseName`, `sourceEnvironment`, `releaseTag`, `artifactName` (por defecto `db-config`) | artefacto `artifactName` (`db-config`) | | checkout |
| `ai-promote.yml` | 832 (job Promote) | `targetServiceConnection`, `promotionServiceConnection`, `targetEnvironment`, `sourceEnvironment`, `routeArgs`, `dryRunArg`, `runValidation` (boolean), `cuSecondaryAnalyzers`, `artifactName` (obligatorio) | artefacto `artifactName` (manifiestos) | | checkout y `download: none` |
| `config-seed.yml` | 832 (job Diff) | `targetServiceConnection`, `targetSqlServer`, `sqlDatabaseName`, `targetResourceGroup`, `targetFunctionApp`, `targetEnvironment`, `sourceEnvironment`, `applyMissingConfig` (boolean), `releaseTag` | `config-diff` | `db-config` | checkout y download |
| `config-apply.yml` | 832 (job Apply) | `targetServiceConnection`, `targetSqlServer`, `sqlDatabaseName`, `targetEnvironment`, `sourceEnvironment`, `releaseTag` | `config-apply-<intento>` | `config-diff`, `db-config` | checkout y download |
| `build-solucion.yml` | 799 (BuildJob) | `solutionPath`, `buildConfiguration`, `testsProject`, `functionsProject`, `adminProject`, `assetResolverProject` | `drop-functions`, `drop-admin`, `drop-assetresolver` | | sin checkout (lo hace el job) |
| `deploy-functions.yml` | 799 (DeployFunctionsJob), vuelta atras de Release | `serviceConnection`, `functionAppName`, `artifactName` (por defecto `drop-functions`), `packagePath` (por defecto `$(Pipeline.Workspace)/drop-functions`) | | `drop-functions` | checkout y download; solo `AzureFunctionApp@2` con zipDeploy |
| `functions-settings.yml` | 799 (DeployFunctionsJob, a continuacion de `deploy-functions.yml`) | `serviceConnection`, `functionAppName`, `resourceGroup`, `keyVaultName`, `assetResolverBaseUrl`, `aiOpenaiPrimaryEndpoint`, `aiCuPrimaryEndpoint`, `aiCuSecondaryEndpoint`, `aiDiEndpoint`, `cuMaxConcurrentCalls`, `cuHardTimeoutSeconds`, `cuEnableCircuitBreaker`, `cuCircuitBreakerFailureThreshold`, `cuCircuitBreakerOpenSeconds`, `cuMaxRetries`, `cuInitialRetryDelayMs`, `promptTracingEnabled`, `promptTracingIncludePromptText`, `promptTracingMaxPromptTextChars`, `gdcEndpoint`, `diUseInlineContent`, `dedupModoEstricto`, `promptDefaultsModelKey`, `gcHeapHardLimitPercent`, `blobRetentionCleanupCron`, `blobRetentionDefaultDays`, `blobRetentionBatchSize`, `gdcTimeoutSeconds`, `environmentName` | | | ensure de settings, decisivos (AB#101570), retirada de heredados y validacion CU |
| `deploy-admin.yml` | 799 (DeployAdminJob) | `serviceConnection`, `adminWebAppName`, `functionAppName`, `resourceGroup`, `keyVaultName`, `manageKeyVaultRbac`, `targetEnvironment`, `artifactName` (por defecto `drop-admin`), `packagePath` | | `drop-admin` | checkout y download |
| `deploy-assetresolver.yml` | 799 (DeployAssetResolverJob) | `serviceConnection`, `assetResolverWebAppName`, `resourceGroup`, `keyVaultName`, `artifactName` (por defecto `drop-assetresolver`), `packagePath` | | `drop-assetresolver` | checkout y download |
| `validate-configuration.yml` | 799 (ValidateConfigurationJob) | `serviceConnection`, `resourceGroup`, `functionAppName`, `adminWebAppName`, `assetResolverWebAppName` | | | checkout |

Notas:

- `sqlDatabaseName` se declara en `ai-export`, `config-seed` y `config-apply` por contrato del template aunque esos pasos no lo usan (los scripts no reciben `-Database`); en `ai-promote`, `sourceEnvironment` también está sin uso porque el origen llega en `routeArgs`.
- Los valores por entorno de `deploy-functions` llegan desde `variables/entorno.yml` y las constantes del 799; los endpoints de IA salen de `infra/ai/pipeline-variables.yml`.
- La cabecera de cada template repite de qué pipeline procede y qué incluye; es la fuente si esta tabla se desfasa.

## Comparar el YAML expandido antes y después de un cambio

Para refactorizar un pipeline sobre templates sin cambiar su comportamiento se compara el YAML final que resuelve Azure DevOps, no el fichero de la raíz. `scripts/ado/Get-PipelinePreview.ps1` lo obtiene con la API de preview (no ejecuta nada); la rama debe estar subida al remoto porque la API resuelve los templates desde allí. Detalle y códigos de salida en `scripts/ado/README.md`.

```bash
pwsh -NoProfile -Command "& ./scripts/ado/Get-PipelinePreview.ps1 -PipelineId 807 -Branch develop -TemplateParameters @{ targetEnvironment = 'dev' } -OutFile docs/auxiliares/temps/<fecha>/preview-antes/807-dev.yml"
pwsh -NoProfile -Command "& ./scripts/ado/Get-PipelinePreview.ps1 -PipelineId 807 -Branch <rama-con-cambio> -TemplateParameters @{ targetEnvironment = 'dev' } -OutFile docs/auxiliares/temps/<fecha>/preview-despues/807-dev.yml"
```

Procedimiento:

1. Obtener el YAML expandido de la rama base («antes») y de la rama con el cambio («después») para cada pipeline y cada entorno o combinación de parámetros que importe (el 832 depende de `dryRun`, `fromProd` y `applyMissingConfig`).
2. Comparar con un diff textual. Habrá ruido que no cambia el comportamiento (comillas en `azureSubscription`, `|` frente a `>` en un `inlineScript`).
3. Para decidir si es ruido, comparar los dos YAML ya parseados (por ejemplo con PyYAML) y no por texto. Si la comparación es posicional, listas con elementos nuevos desplazan todo lo posterior y marcan diferencias falsas: en ese caso se comparan las variables por nombre.
4. Ensayar en DEV el pipeline refactorizado antes de darlo por bueno.

Los resultados de estas comparaciones van a `docs/auxiliares/temps/<fecha>/` (carpeta ignorada por git).

## Pipeline Release

`azure-pipelines-release.yml` (pipeline `AI DocClassExt (Release)`, id `842`; AB#101583, hito 2) es la release de un botón: una ejecución es la release de un entorno, con la versión que declara el manifiesto de `releases/` (`CURRENT`; en dev, `_ensayo`). Compone los templates de esta carpeta en jobs normales y la lógica propia de la release está en `scripts/release/`. Diseño en `docs/superpowers/specs/2026-11-26-pipeline-release-un-boton-design.md` (y su enmienda del 2026-11-27); decisiones en ADR-007 y ADR-008.

### Parámetros

| Parámetro | Valores | Notas |
|---|---|---|
| `targetEnvironment` | `dev` (por defecto), `pre`, `prod` | `dev` es solo para ensayos. Origen del salto como en el 832: `pre` desde `dev`, `prod` desde `pre`; `dev` recupera desde PRO con `-FromProd -DryRun` y sin export ni altas. |
| `simularFalloEn` | `ninguna` (por defecto, sin fallo; se pasa como vacío a Guard y a `simular-fallo.yml` porque Azure DevOps no admite la cadena vacía en `values`), `Migraciones`, `IA`, `Codigo`, `Configuracion`, `Altas`, `Verificacion` | Solo en dev (Guard lo rechaza fuera de dev). Fuerza un fallo al inicio de esa stage (`simular-fallo.yml`) para ensayar la vuelta atrás. |

Variables: `variables/entorno.yml`, `infra/ai/pipeline-variables.yml`, `variables/aprobadores.yml`, las constantes del 799 y del 832 y, por entorno, el origen del salto (`sourceEnvironment`, `sourceServiceConnection`, `sourceSqlServer`, `routeArgs`, `dryRunArg`). Pool por defecto `docia-mdp-private`; `Aprobacion` agentless; en `windows-latest` (agente alojado) van `Build/BuildJob`, `Guard/GuardKudu`, `Foto/FotoCodigoJob`, `Migraciones/Generate`, `Codigo/Deploy`, `Codigo/Validate`, `Configuracion/ReinicioJob`, `Altas/ReinicioJob`, `Verificacion/SmokeJob` y `VueltaAtras/RevertirCodigo` y `VueltaAtras/ReinicioVuelta`. Todo lo que habla con SQL, Key Vault o storage sigue en el pool privado. Ninguna stage usa environments de Azure DevOps ni jobs de tipo deployment (ADR-008).

### Qué corre en agente alojado y por qué

El Kudu (`scm`) de la Function App no resuelve desde el pool privado `docia-mdp-private`: la app tiene endpoint privado y la zona DNS no está enlazada a la VNet del pool (NODATA). Desde un agente alojado resuelve a IP pública y Kudu con Bearer de Entra responde. Por eso todo lo que habla con Kudu va en `windows-latest`, como el 799: la comprobación de Kudu de Guard (`GuardKudu`), la foto del código (`FotoCodigoJob`) y los despliegues de código de `Codigo/Deploy` y `VueltaAtras/RevertirCodigo`. Lo mismo vale para el host principal: `<app>.azurewebsites.net` es un CNAME a `<app>.privatelink.azurewebsites.net` y tampoco resuelve desde el pool privado, así que todo job que hace HTTP a la Function App (reinicio con health: `Configuracion/ReinicioJob`, `Altas/ReinicioJob`, `VueltaAtras/ReinicioVuelta`; smoke: `Verificacion/SmokeJob`) va en hosted y se separa de los pasos de SQL, Key Vault y storage con `dependsOn`. SQL, Key Vault y storage siguen en el pool privado. Las llamadas a `az` con paréntesis u otros caracteres de `cmd` en argumentos sin espacios las prepara `ConvertTo-ReleaseAzArgumento` (en Windows, `az.cmd` rompe con `( ) & | < > ^`).

### Stages

| Stage | Qué hace |
|---|---|
| `Guard` | Solo lectura: rama esperada por entorno, manifiesto, aprobadores, simulación, registro `ReleaseAplicada` y permisos. Emite `version`, `manifestPath`, `yaAplicada` e `ignorarRegistro` (`guard.<variable>` del job `GuardJob`, en el pool privado y con `-SinKudu`). La comprobación de Kudu de las tres apps va en el job `GuardKudu` (agente alojado, `Test-ReleasePermisos.ps1 -SoloKudu`); la stage falla si falla cualquiera de los dos. Si la versión ya está aplicada se saltan todas las demás. |
| `Build` | Build y tests de la solución (template del 799); en paralelo a la espera de aprobación. |
| `Aprobacion` | Dos `ManualValidation@1` encadenados (`Aprobacion1`, `Aprobacion2`), aprobadores de `aprobadores.yml`, 7 días de espera, rechazo al agotarse. En prod la segunda recuerda las cuatro puertas de PRE y el Smoke del Test Plan 100069. |
| `Foto` | Foto de app settings, ingesta pausada, drenado del task hub (exit 3 = tiempo agotado: se rehabilita la ingesta y falla), copia de las tablas de configuración (`FotoJob`, pool privado) y, después, foto del código de las tres apps (`FotoCodigoJob`, agente alojado). Espera a `Aprobacion` y `Build`. |
| `FotoFallida` | Solo si falla `Foto`: rehabilita la ingesta y publica el resumen en rojo. |
| `Migraciones` | `Generate` y `Apply` (templates del 807, sin backfill ni regla de firewall). |
| `IA` | `Export` de la config del origen (no en dev) y `Promote` de artefactos de IA (templates del 832). |
| `Codigo` | `Deploy` (Functions, settings, Admin y AssetResolver, en ese orden; agente alojado) y `Validate` (templates del 799). |
| `Configuracion` | Pasos del manifiesto, secretos heredados de Key Vault y ciclo de vida del storage (cada uno con `-WhatIf` antes del real), reinicio con health en un job aparte (`ReinicioJob`, agente alojado). |
| `Altas` | Solo pre y prod: `config-seed` y `config-apply` con `applyMissingConfig`; reinicio con health en `ReinicioJob` (agente alojado). |
| `Verificacion` | Export `<versión>-<env>` (solo `.hashes.json` a evidencias), diff frente al origen (no en dev), smoke E2E con function key temporal y publicación en el Test Plan 100069 (el smoke va en `SmokeJob`, agente alojado, tras `VerifJob`). |
| `Fin` | Solo con todo en verde: ingesta activa, cierre en `ReleaseAplicada`, `resumen.md` y artefacto `release-<env>-<build>`. |
| `VueltaAtras` | Solo si `Foto` pasó y falla una stage de `Migraciones` a `Verificacion`: restaura la BD y los settings, redespliega el código de la foto si `Codigo` llegó a empezar (`RevertirCodigo`, agente alojado, desde `evidencias-FotoCodigo`), marca el registro como `Revertido`, rehabilita la ingesta (`Cierre`), reinicia (`ReinicioVuelta`, agente alojado), publica el resumen en rojo y deja el run en rojo (`ResumenVuelta`). |

`simularFalloEn` coloca `simular-fallo.yml` como primer paso de `Migraciones`, `IA`, `Codigo`, `Configuracion`, `Altas` y `Verificacion`.

### Artefactos

- `evidencias-<stage>` (`Foto`, `FotoCodigo`, `Configuracion`, `Altas`, `Verificacion`, `VueltaAtras`, `VueltaAtrasCierre`): ficheros que dejan los scripts en `$(Build.ArtifactStagingDirectory)/evidencias`, sin secretos ni listado completo de app settings. `evidencias-FotoCodigo/foto-codigo/*.zip` (más `foto-codigo.json`) es el código desplegado antes de la release (`functions.zip`, `admin.zip`, `assetresolver.zip`).
- `release-<env>-<build>`: `resumen.md` y las evidencias (sin los zips del código). Se publica en verde (`Fin`) y en rojo (`VueltaAtras`, `FotoFallida`).
- Los propios de los templates: `drop-*` (build), `MigrationsScript`, `db-config`, `ai-artifacts-<env>-<intento>`, `config-diff` y `config-apply-<intento>`.

### Scripts de `scripts/release/`

| Script | Qué hace |
|---|---|
| `Get-ReleaseManifest.ps1`, `Test-ReleaseManifest.ps1` | Leen y validan el manifiesto de `releases/`. |
| `Invoke-ReleaseGuard.ps1` | Comprobaciones de `Guard` y variables de salida. |
| `Test-ReleasePermisos.ps1` | Lecturas que comprueban los permisos de la service connection (lo llama `Guard`). |
| `Save-ReleaseFotoSettings.ps1` / `Restore-ReleaseFotoSettings.ps1` | Foto y restauración de los app settings que toca la release. |
| `Set-ReleaseIngesta.ps1` | Pausa o reactiva la ingesta (`AzureWebJobs.IngestDocument.Disabled`). |
| `Wait-ReleaseTaskHubDrenado.ps1` | Espera a que el task hub no tenga instancias en curso (exit 3 al agotar el tiempo). |
| `New-ReleaseFotoBd.ps1` / `Restore-ReleaseFotoBd.ps1` | Copia `<Tabla>__rel_<BuildId>` de las tablas de configuración y restauración por `MERGE`. |
| `Save-ReleaseFotoCodigo.ps1` | Descarga por Kudu, con token de Entra (sin autenticación básica), el código desplegado de las tres apps. |
| `Invoke-ReleasePasos.ps1` | Aplica los pasos del manifiesto (con ensayo previo) y los registra por hash. |
| `Restart-ReleaseFunctionApp.ps1` | Reinicia la Function App y espera al health. |
| `Invoke-ReleaseSmoke.ps1` | Smoke E2E con function key temporal y publicación en el Test Plan 100069. |
| `New-ReleaseResumen.ps1` | Genera `resumen.md` a partir de las evidencias. |
| `ReleaseCommon.psm1`, `ReleaseRegistro.psm1` | Utilidades de `az` y SQL, y registro en `ReleaseAplicada` (`Register-ReleaseCierre`, `Set-ReleaseRevertida`). |

### Qué no se deshace

La vuelta atrás no revierte: las migraciones EF, los analyzers de CU copiados por el 832 (sobrescriben los del destino), los secretos heredados de Key Vault borrados (AB#101450), los deployments de IA (aditivos) ni el artefacto de embeddings copiado. Las copias `<Tabla>__rel_<BuildId>` se conservan. Si falla `Foto` no se ha tocado nada más.

### Ensayo

Antes de usarla en PRE (Tarea 13 del plan): una ejecución completa con `targetEnvironment=dev` sobre el estado actual (debe acabar en verde sin cambios y con smoke 6/6) y una con `simularFalloEn=Configuracion` (u otra), comprobando con el export de la config antes y después que la vuelta atrás deja DEV como estaba. Procedimiento paso a paso en el plan del hito 2.

Riesgo abierto: los aprobadores viven en `variables/aprobadores.yml`, es decir, en el repositorio; quien pueda integrar en `develop` o `master` puede cambiarlos. A 2026-11-27 el proyecto no tiene ninguna política de rama activa (`_apis/policy/configurations` devuelve 0): ni revisores mínimos en `develop` ni en `master`. Mientras no la haya, el único control es quién tiene permiso de push. Cambiar la política es una escritura en Azure DevOps fuera de este hito.
