# Plan de Despliegue Inicial DEV y PRE

Fecha: 2026-06-24
Estado: Base de infraestructura entregada. Pendiente configuracion de aplicacion y primer despliegue.

## 1. Objetivo

Definir y ejecutar el despliegue inicial de la solucion en DEV y PRE desde el estado actual.

Alcance:
- Plan por fases.
- Checklist de comprobaciones.
- Plantilla de variables por entorno.
- Comandos operativos para ejecutar en orden.

Restriccion de diseno:
- BBDD igual que en PROD: SqlConnectionString en Key Vault y consumo por app settings en Functions.

## 2. Contexto confirmado

Se confirma que no se ha configurado ni desplegado la aplicacion todavia en DEV/PRE.

Implicacion:
- La infraestructura base puede estar completa.
- La capa de configuracion (secrets, app settings, validaciones de contrato, smoke) es el trabajo pendiente antes del primer despliegue funcional.

## 3. Plan por fases

### Fase 0 - Preparacion

1. Confirmar baseline funcional final a aplicar (contrato de settings y estrategia DB).
2. Confirmar politica de AI compartida (sin crear AI nuevo en DEV/PRE).
3. Preparar hoja de variables de entorno (DEV y PRE) para no mezclar nombres/suscripciones.

### Fase 1 - Prerrequisitos bloqueantes

1. Verificar permisos de operador y visibilidad de recursos por entorno.
2. Cargar secretos en Key Vault por entorno.
3. Verificar prerequisitos tecnicos tras carga de secretos.

### Fase 2 - Configuracion de aplicacion

1. Configurar Function App (settings no secretos + referencias Key Vault).
2. Configurar Admin y AssetResolver.
3. Validar contrato de app settings hasta cero errores criticos.

### Fase 3 - BBDD y despliegue

1. Aplicar estrategia de migracion inicial (recomendada: script idempotente controlado).
2. Mantener RunDatabaseMigrationsOnStartup en false al cierre.
3. Ejecutar despliegue de codigo en DEV.
4. Validar smoke funcional y telemetria.

### Fase 4 - Promocion a PRE

1. Repetir exactamente fases 1 a 3 en PRE.
2. No introducir cambios de codigo entre DEV validado y PRE.

## 4. Plantilla de variables por entorno

## 4.1 DEV

- SubscriptionId: 8764f9ff-fe37-4c03-bde9-6294622bef6d
- ResourceGroup: SRBRGDEVDOCSAI
- FunctionAppName: srbappdevdocai
- AdminWebAppName: srbwebadmindevdocai
- AssetResolverWebAppName: srbwebpluginassetresolverdev
- KeyVaultName: srbkvdevdocai
- SqlServerName: srbsqldevdocai
- SqlDatabaseName: DocumentIA
- StorageDocuments: srbstgdevdocai
- StorageDurable: srbstgdevappdocai
- AppInsightsName: srbappidevdocai

## 4.2 PRE

- SubscriptionId: a4f6b357-8f13-4488-9ee8-b9f635426f91
- ResourceGroup: SRBRGPREDOCSAI
- FunctionAppName: srbapppredocai
- AdminWebAppName: srbwebadminpredocai
- AssetResolverWebAppName: srbwebpluginassetresolverpre
- KeyVaultName: srbkvpredocai
- SqlServerName: srbsqlpredocai
- SqlDatabaseName: DocumentIA
- StorageDocuments: srbstgpredocai
- StorageDurable: srbstgpreappdocai
- AppInsightsName: srbappipredocai

## 4.3 Variables funcionales a confirmar

- FunctionsAdminApi base URL por entorno.
- AssetResolver base URL por entorno.
- Endpoints AI compartidos definitivos (si no se usan los recursos AI locales).
- Estrategia de migracion DB inicial.

## 5. Checklist de comprobaciones

### 5.1 Antes de configurar

- Suscripcion correcta seleccionada.
- RG y aplicaciones objetivo correctas.
- RBAC de operador suficiente.
- Managed identities de apps presentes.
- SQL Server y DB visibles.
- Storage documental y durable visibles.
- Key Vault visible y con permisos de escritura de secretos.

### 5.2 Antes de desplegar codigo

- Secretos obligatorios cargados en Key Vault.
- App settings de Functions, Admin y AssetResolver aplicados.
- Contrato de settings validado sin faltantes criticos.
- Referencias Key Vault en estado Resolved.
- Estrategia de migraciones DB ejecutada o aprobada.

### 5.3 Post deploy

- Pipeline/ejecucion en estado Succeeded.
- Functions arranca sin errores de secretos/config.
- Smoke endpoint OK.
- App Insights sin excepciones criticas.
- Conectividad SQL y storage validada.

### 5.4 Criterios de salida por entorno

- Cero faltantes criticos en validacion de contrato.
- Key Vault references resueltas.
- SQL operativo con patron PROD (SqlConnectionString via Key Vault).
- Smoke funcional OK.

## 6. Comandos operativos

Nota:
- El flujo principal es por Azure DevOps Pipelines.
- Los comandos manuales quedan como plan de respaldo para incidencias o ejecuciones puntuales.
- Ejecutar primero todo en DEV y despues repetir en PRE cambiando variables.
- Comandos en PowerShell.
- En los pasos que requieren datos sensibles, usar variables DOCIA_SECRET_* o local.settings no versionado.

### 6.1 Preparacion de variables DEV

    $SubscriptionId = "8764f9ff-fe37-4c03-bde9-6294622bef6d"
    $ResourceGroup = "SRBRGDEVDOCSAI"
    $FunctionAppName = "srbappdevdocai"
    $AdminWebAppName = "srbwebadmindevdocai"
    $AssetResolverWebAppName = "srbwebpluginassetresolverdev"
    $KeyVaultName = "srbkvdevdocai"
    $SqlServerName = "srbsqldevdocai"
    $SqlDatabaseName = "DocumentIA"
    $StorageDocuments = "srbstgdevdocai"
    $StorageDurable = "srbstgdevappdocai"

### 6.2 Seleccion de suscripcion y pre-check de permisos

    az account set --subscription $SubscriptionId
    pwsh -File .\scripts\configuration\check-azure-permissions.ps1 `
      -SubscriptionId $SubscriptionId `
      -ResourceGroup $ResourceGroup `
      -KeyVaultName $KeyVaultName `
      -SqlServerName $SqlServerName `
      -FunctionAppName $FunctionAppName `
      -StorageDocuments $StorageDocuments `
      -StorageDurable $StorageDurable

### 6.3 Carga de secretos en Key Vault

Opcion A: variables de entorno DOCIA_SECRET_*

    $env:DOCIA_SECRET_SQLCONNECTIONSTRING = "<valor>"
    $env:DOCIA_SECRET_AZUREWEBJOBSSTORAGE = "<valor>"
    $env:DOCIA_SECRET_AZURESTORAGECONNECTIONSTRING = "<valor>"
    # Repetir para el resto de secretos obligatorios

    pwsh -File .\scripts\configuration\set-keyvault-secrets.ps1 `
      -SubscriptionId $SubscriptionId `
      -ResourceGroup $ResourceGroup `
      -KeyVaultName $KeyVaultName

Obtencion rapida de valores en DEV para storage:

    $docsConn = az storage account show-connection-string `
      --name $StorageDocuments `
      --resource-group $ResourceGroup `
      --query connectionString -o tsv

    $jobsConn = az storage account show-connection-string `
      --name $StorageDurable `
      --resource-group $ResourceGroup `
      --query connectionString -o tsv

    # Asignar a variables secretas de sesion (no imprimir en consola)
    $env:DOCIA_SECRET_AZURESTORAGECONNECTIONSTRING = $docsConn
    $env:DOCIA_SECRET_AZUREWEBJOBSSTORAGE = $jobsConn

SqlConnectionString recomendada para DEV (alineada con PROD mediante Managed Identity):

    $env:DOCIA_SECRET_SQLCONNECTIONSTRING = "Server=tcp:srbsqldevdocai.database.windows.net,1433;Database=DocumentIA;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Function key para Admin API (si no existe una dedicada, usar la default):

    $env:DOCIA_SECRET_FUNCTIONSADMINAPIFUNCTIONKEY = az functionapp keys list `
      --resource-group $ResourceGroup `
      --name $FunctionAppName `
      --query "functionKeys.default" -o tsv

Opcion B: local.settings no versionado

    pwsh -File .\scripts\configuration\set-keyvault-secrets.ps1 `
      -SubscriptionId $SubscriptionId `
      -ResourceGroup $ResourceGroup `
      -KeyVaultName $KeyVaultName `
      -PreferLocalSettings `
      -LocalSettingsPath .\src\backend\DocumentIA.Functions\local.settings.json

### 6.4 Verificacion de prerequisitos tras carga de secretos

    pwsh -File .\scripts\deployment\verify-prod-prereqs.ps1 `
      -SubscriptionId $SubscriptionId `
      -ResourceGroup $ResourceGroup `
      -FunctionAppName $FunctionAppName `
      -KeyVaultName $KeyVaultName `
      -StorageAccountName $StorageDurable

Si aparece "[SIN PERMISO]" en todos los secretos de Key Vault:

1. Confirmar que Key Vault usa RBAC:

    az keyvault show --name $KeyVaultName --resource-group $ResourceGroup --query properties.enableRbacAuthorization -o tsv

2. Asignar rol al principal de la Service Connection (WIF) del entorno:

    az role assignment create `
      --assignee <APP_ID_SERVICE_CONNECTION> `
      --role "Key Vault Secrets Officer" `
      --scope "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$KeyVaultName"

3. Verificar asignacion:

    az role assignment list `
      --assignee <APP_ID_SERVICE_CONNECTION> `
      --scope "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$KeyVaultName" `
      --query "[].roleDefinitionName" -o table

4. Esperar 2-5 minutos por propagacion RBAC y relanzar bootstrap.

Caso real DEV validado:

- AppId service connection DEV: efff077f-57d1-41c5-b66f-7b0d2e00fc0e
- Rol verificado: Key Vault Secrets Officer

### 6.5 Configuracion de Function App

Importante:
- El script set-app-settings actual esta hardcodeado a PROD.
- Antes de usarlo en DEV/PRE, crear variante parametrizada o actualizar script para recibir parametros de entorno.

Ejemplo de aplicacion minima de referencias Key Vault en Functions:

    pwsh -File .\scripts\configuration\set-functionapp-keyvault-references.ps1 `
      -SubscriptionId $SubscriptionId `
      -ResourceGroup $ResourceGroup `
      -FunctionAppName $FunctionAppName `
      -KeyVaultName $KeyVaultName

### 6.6 Configuracion Admin y AssetResolver

Aplicar settings requeridos (por CLI o script parametrizado):

Admin:

    az webapp config appsettings set `
      --resource-group $ResourceGroup `
      --name $AdminWebAppName `
      --settings `
        "FunctionsAdminApi__BaseUrl=https://$FunctionAppName.azurewebsites.net/api/" `
        "FunctionsAdminApi__FunctionKey=@Microsoft.KeyVault(VaultName=$KeyVaultName;SecretName=FunctionsAdminApiFunctionKey)"

AssetResolver:

    az webapp config appsettings set `
      --resource-group $ResourceGroup `
      --name $AssetResolverWebAppName `
      --settings `
        "ConnectionStrings__AssetResolverDb=@Microsoft.KeyVault(VaultName=$KeyVaultName;SecretName=user-ods-dwh)" `
        "ApiKey=@Microsoft.KeyVault(VaultName=$KeyVaultName;SecretName=AssetResolverApiKey)"

### 6.7 Validacion de contrato de app settings

    pwsh -File .\scripts\testing\validate-azure-appsettings-contract.ps1 `
      -SubscriptionId $SubscriptionId `
      -ResourceGroup $ResourceGroup `
      -FunctionsAppName $FunctionAppName `
      -AdminWebAppName $AdminWebAppName `
      -AssetResolverWebAppName $AssetResolverWebAppName

### 6.8 BBDD (mismo patron que PROD)

Comprobacion previa recomendada de identidad administrada en DEV:

  az functionapp identity show `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "{type:type,principalId:principalId,tenantId:tenantId}" -o json

Validacion en SQL (ejecutar con usuario administrador Entra en la base DocumentIA):

  SELECT name, type_desc, authentication_type_desc
  FROM sys.database_principals
  WHERE name = 'srbappdevdocai';

  SELECT r.name AS role_name, m.name AS member_name
  FROM sys.database_role_members drm
  JOIN sys.database_principals r ON drm.role_principal_id = r.principal_id
  JOIN sys.database_principals m ON drm.member_principal_id = m.principal_id
  WHERE m.name = 'srbappdevdocai';

Si falta usuario o roles: crear usuario desde EXTERNAL PROVIDER y asignar al menos db_datareader + db_datawriter. Agregar db_ddladmin solo si se ejecutaran migraciones desde app/pipeline.

Generar script idempotente de migraciones:

    dotnet ef migrations script --idempotent `
      -p .\src\backend\DocumentIA.Data `
      -s .\src\backend\DocumentIA.Functions `
      -o .\artifacts\migrations-inicial.sql

Aplicar script en SQL:

    sqlcmd -S "$SqlServerName.database.windows.net" -d $SqlDatabaseName -i .\artifacts\migrations-inicial.sql

Mantener setting final:

    az functionapp config appsettings set `
      --resource-group $ResourceGroup `
      --name $FunctionAppName `
      --settings "RunDatabaseMigrationsOnStartup=false"

### 6.9 Smoke y telemetria post despliegue

Comprobar que referencias Key Vault estan resueltas y que no hay faltantes:

    pwsh -File .\scripts\deployment\verify-prod-prereqs.ps1 `
      -SubscriptionId $SubscriptionId `
      -ResourceGroup $ResourceGroup `
      -FunctionAppName $FunctionAppName `
      -KeyVaultName $KeyVaultName `
      -StorageAccountName $StorageDurable

### 6.10 Repeticion en PRE

Repetir pasos 6.1 a 6.9 sustituyendo por las variables PRE del apartado 4.2.

### 6.11 Ejecucion desde Azure DevOps Pipelines

Orden recomendado:

1. Ejecutar primero el pipeline de bootstrap para dejar entorno configurado.
2. Ejecutar despues el pipeline principal de despliegue de codigo.

Pipelines:

- Bootstrap por entorno: [azure-pipelines-bootstrap.yml](azure-pipelines-bootstrap.yml)
- Despliegue multi-entorno: [azure-pipelines.yml](azure-pipelines.yml)

Uso esperado:

- Bootstrap: primera alta de entorno, remediacion de prerequisitos o reconfiguracion.
- Pipeline principal: despliegue repetible de codigo una vez que el entorno ya esta preparado.
- Si el entorno falla en validacion, volver al bootstrap; no saltar directamente al deploy.

Parametros minimos para bootstrap:

- targetEnvironment: dev | pre | prod

Nota de secretos en bootstrap:

- El pipeline consume secretos desde variables seguras `DOCIA_SECRET_*`.
- Si faltan secretos, el paso de prerequisitos fallara y no avanzara.

Parametros para despliegue multi-entorno:

- targetEnvironment: dev | pre | prod

Comportamiento:

- El pipeline resuelve service connection, nombres de apps/RG/KV y suscripcion segun targetEnvironment.
- Mantiene endpoints AI compartidos y valida contrato de app settings al final.

### 6.12 Que necesitas crear o revisar en Azure DevOps

Ya no hace falta crear mas YAML para separar DEV/PRE/PRO si vas a usar los pipelines actuales.

Lo que si debes tener creado o revisado en Azure DevOps es esto:

1. Service connections existentes y autorizadas:
  1. AI DocClassExt DEV
  2. AI DocClassExt PRE
  3. AI DocClassExt PRO
2. Variables seguras o variable groups para los secretos DOCIA_SECRET_* que consume el bootstrap.
3. Environments de Azure DevOps para `dev`, `pre` y `prod` si quieres aprobaciones, checks o trazabilidad por entorno.
4. Permisos de pipeline sobre cada service connection y sobre cada environment, si aplicas approvals.
5. Si quieres gobernanza adicional, reglas de branch policy y/o approvals manuales antes de desplegar a PRE y PROD.

No necesitas crear pipeline nuevo adicional para cada entorno salvo que quieras una separacion organizativa distinta a la que ya deja el YAML.

## 7. Riesgos y mitigaciones

1. Riesgo: scripts hardcodeados a PROD.
   Mitigacion: parametrizar scripts antes de ejecutar en DEV/PRE.

2. Riesgo: secretos incompletos o mal nombrados.
   Mitigacion: validar con verify-prod-prereqs y validate-azure-appsettings-contract tras cada cambio.

3. Riesgo: estrategia DB inconsistente.
   Mitigacion: fijar una sola estrategia para el primer despliegue y documentarla.

4. Riesgo: desalineacion DEV/PRE.
   Mitigacion: bloquear cambios de codigo entre DEV aprobado y PRE.

## 8. Referencias del repositorio

- [docs/08_CHECKLISTS_DESPLIEGUE.md](docs/08_CHECKLISTS_DESPLIEGUE.md)
- [docs/procedimientos/AZURE_DEVOPS_BOOTSTRAP_SETUP.md](docs/procedimientos/AZURE_DEVOPS_BOOTSTRAP_SETUP.md)
- [scripts/configuration/check-azure-permissions.ps1](scripts/configuration/check-azure-permissions.ps1)
- [scripts/configuration/set-keyvault-secrets.ps1](scripts/configuration/set-keyvault-secrets.ps1)
- [scripts/configuration/set-app-settings.ps1](scripts/configuration/set-app-settings.ps1)
- [scripts/configuration/set-functionapp-keyvault-references.ps1](scripts/configuration/set-functionapp-keyvault-references.ps1)
- [scripts/deployment/verify-prod-prereqs.ps1](scripts/deployment/verify-prod-prereqs.ps1)
- [scripts/testing/validate-azure-appsettings-contract.ps1](scripts/testing/validate-azure-appsettings-contract.ps1)
- [scripts/config/azure-appsettings-contract.json](scripts/config/azure-appsettings-contract.json)
- [azure-pipelines.yml](azure-pipelines.yml)