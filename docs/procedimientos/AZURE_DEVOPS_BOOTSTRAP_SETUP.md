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