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
