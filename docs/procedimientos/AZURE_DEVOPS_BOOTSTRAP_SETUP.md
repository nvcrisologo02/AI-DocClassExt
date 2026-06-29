# Azure DevOps Bootstrap Setup

Fecha: 2026-06-24

Este documento define los objetos que deben existir en Azure DevOps para ejecutar el bootstrap multi-entorno sin depender de configuracion manual.

## 1. Environments a crear

Crear estos Azure DevOps Environments:

- dev
- pre
- prod

Recomendacion:

- Asignar approvals manuales en `pre` y `prod`.
- Dejar `dev` sin approvals o con checks ligeros si lo necesitais para validacion rapida.

## 2. Variable groups a crear

Crear un variable group por entorno para secretos del bootstrap:

- docia-bootstrap-dev-secrets
- docia-bootstrap-pre-secrets
- docia-bootstrap-prod-secrets

## 3. Variables seguras requeridas en cada group

Todas las variables siguientes deben existir como secret variables.

- DOCIA_SECRET_SQLCONNECTIONSTRING
- DOCIA_SECRET_AZUREWEBJOBSSTORAGE
- DOCIA_SECRET_AZURESTORAGECONNECTIONSTRING
- DOCIA_SECRET_EXTRACTION_AZURECONTENTUNDERSTANDING_APIKEY
- DOCIA_SECRET_EXTRACTION_GPTFALLBACK_APIKEY
- DOCIA_SECRET_CLASSIFICATION_AZUREDOCUMENTINTELLIGENCE_APIKEY
- DOCIA_SECRET_CLASSIFICATION_GPTFALLBACK_APIKEY
- DOCIA_SECRET_ASSETRESOLVERAPIKEY
- DOCIA_SECRET_FUNCTIONSADMINAPIFUNCTIONKEY
- DOCIA_SECRET_GDC_USERNAME
- DOCIA_SECRET_GDC_PASSWORD
- DOCIA_SECRET_GDC_HTTPBASICUSERNAME
- DOCIA_SECRET_GDC_HTTPBASICPASSWORD

## 4. Mapeo recomendado por entorno

### 4.1 DEV

- Environment: dev
- Variable group: docia-bootstrap-dev-secrets
- Service connection: AI DocClassExt DEV

### 4.2 PRE

- Environment: pre
- Variable group: docia-bootstrap-pre-secrets
- Service connection: AI DocClassExt PRE

### 4.3 PROD

- Environment: prod
- Variable group: docia-bootstrap-prod-secrets
- Service connection: AI DocClassExt PRO

## 5. Permisos necesarios

Conceder al pipeline:

- Uso de cada service connection correspondiente.
- Lectura de cada variable group de secretos.
- Permiso de despliegue sobre cada environment si se usan approvals/checks.

## 6. Flujo de uso

1. Crear environments y variable groups en Azure DevOps.
2. Cargar los secretos por entorno.
3. Ejecutar [azure-pipelines-bootstrap.yml](../../azure-pipelines-bootstrap.yml) con `targetEnvironment`.
4. Ejecutar [azure-pipelines.yml](../../azure-pipelines.yml) con `targetEnvironment`.

## 7. Nota importante

El repositorio no puede crear por si mismo estos objetos de Azure DevOps sin una integracion adicional con REST API o credenciales de acceso a la organizacion.
Este documento deja la definicion exacta para crearlos de forma manual o automatizada despues.

## 8. Obtencion de valores de secretos en DEV

Esta seccion resume como obtener los valores de los secretos principales para el entorno DEV antes de ejecutar el bootstrap.

Datos de entorno DEV:

- SubscriptionId: 8764f9ff-fe37-4c03-bde9-6294622bef6d
- ResourceGroup: SRBRGDEVDOCSAI
- FunctionApp: srbappdevdocai
- Storage documents: srbstgdevdocai
- Storage durable: srbstgdevappdocai
- SQL server: srbsqldevdocai
- Database: DocumentIA

### 8.1 DOCIA_SECRET_AZURESTORAGECONNECTIONSTRING (documents)

		az account set --subscription 8764f9ff-fe37-4c03-bde9-6294622bef6d
		az storage account show-connection-string `
			--name srbstgdevdocai `
			--resource-group SRBRGDEVDOCSAI `
			--query connectionString -o tsv

### 8.2 DOCIA_SECRET_AZUREWEBJOBSSTORAGE (durable)

		az account set --subscription 8764f9ff-fe37-4c03-bde9-6294622bef6d
		az storage account show-connection-string `
			--name srbstgdevappdocai `
			--resource-group SRBRGDEVDOCSAI `
			--query connectionString -o tsv

### 8.3 DOCIA_SECRET_SQLCONNECTIONSTRING (patron Managed Identity)

Valor esperado en DEV (alineado con PROD):

		Server=tcp:srbsqldevdocai.database.windows.net,1433;Database=DocumentIA;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

### 8.4 DOCIA_SECRET_FUNCTIONSADMINAPIFUNCTIONKEY

Obtener function key de la Function App DEV:

		az account set --subscription 8764f9ff-fe37-4c03-bde9-6294622bef6d
		az functionapp keys list `
			--resource-group SRBRGDEVDOCSAI `
			--name srbappdevdocai `
			--query "functionKeys.default" -o tsv

### 8.5 Validacion de identidad administrada para SQL

Verificar que la Function App tiene SystemAssigned identity:

		az functionapp identity show `
			--resource-group SRBRGDEVDOCSAI `
			--name srbappdevdocai `
			--query "{type:type,principalId:principalId,tenantId:tenantId}" -o json

Comprobar en SQL (con usuario admin Entra) que existe el usuario y sus roles:

		SELECT name, type_desc, authentication_type_desc
		FROM sys.database_principals
		WHERE name = 'srbappdevdocai';

		SELECT r.name AS role_name, m.name AS member_name
		FROM sys.database_role_members drm
		JOIN sys.database_principals r ON drm.role_principal_id = r.principal_id
		JOIN sys.database_principals m ON drm.member_principal_id = m.principal_id
		WHERE m.name = 'srbappdevdocai';

Si faltan permisos: asignar al menos db_datareader y db_datawriter. Agregar db_ddladmin solo si se ejecutaran migraciones desde app/pipeline.