# Retirada de los app settings heredados sin efecto (AB#101450) · diseño corto

Fecha: 2026-11-25 · Línea: core · Work item: AB#101450 (hija de AB#101445) · Rama: `chore/101450-retirar-app-settings-heredados`

## Intención

El repaso documental del 2026-11-22 (AB#101447) comprobó que 27 app settings que `azure-pipelines.yml` fija en las tres Function Apps no se leen en el código: la conexión, el modelo y los umbrales de IA salen de `ModeloConfigs` por alias e identidad administrada. Siguen en el pipeline, en el contrato de settings, en `set-app-settings.ps1` y en la documentación, con cuatro secretos `*--ApiKey` en los Key Vault de DEV, PRE y PRO. El objetivo es que los tres entornos queden limpios de verdad, settings y secretos, y que el contrato impida que vuelvan.

Verificado el 2026-11-25 sobre `develop` 81793bb0: `AzureContentUnderstandingOptions` solo tiene las siete propiedades de resiliencia; `GptFallbackExtraerSettings` no se enlaza en ningún sitio; `ClassificationRoutingSettings.DefaultModelKey` se enlaza pero nadie lo lee; no hay lecturas por indexador de esas claves. Ni `DocumentIA.Batch` ni `DocumentIA.DocsAgent` usan los secretos.

## Qué se retira

- `Extraction__AzureContentUnderstanding__Endpoint`, `AuthMode`, `ApiKey`, `DefaultProcessingLocation` (4). Se conservan los siete de resiliencia (`MaxConcurrentCalls`, `HardTimeoutSeconds`, `EnableCircuitBreaker`, `CircuitBreakerFailureThreshold`, `CircuitBreakerOpenSeconds`, `MaxRetries`, `InitialRetryDelayMs`).
- `Extraction__GptFallback__Enabled`, `Endpoint`, `AuthMode`, `ApiKey`, `DeploymentName`, `MinFieldsRatio`, `Temperature`, `MaxTokens`, `TimeoutSeconds` (9).
- `Classification__DefaultModelKey` (1).
- `Classification__AzureDocumentIntelligence__Endpoint`, `AuthMode`, `ApiKey`, `ApiVersion` (4).
- `Classification__GptFallback__Enabled`, `Endpoint`, `AuthMode`, `ApiKey`, `DeploymentName`, `FallbackThreshold`, `Temperature`, `MaxTokens`, `TimeoutSeconds` (9).
- Secretos de Key Vault: `Extraction--AzureContentUnderstanding--ApiKey`, `Extraction--GptFallback--ApiKey`, `Classification--AzureDocumentIntelligence--ApiKey`, `Classification--GptFallback--ApiKey`.

Se conservan `Extraction__DefaultProvider`, `Classification__DefaultProvider` y `AI__Resources__*__Endpoint`, que sí se leen.

## Objetivos, no objetivos y anti-objetivos

- Objetivos: pipeline y contrato sin los 27 settings; los tres entornos sin ellos y sin los cuatro secretos; ValidateConfiguration en verde en DEV; documentación alineada; restos locales (ficheros de configuración y código muerto) retirados.
- No objetivos: tocar `ModeloConfigs`, los alias de IA ni los siete settings de resiliencia de CU; cambiar `ensure-app-settings.ps1`.
- Anti-objetivos: que un entorno pierda conectividad con IA (imposible por diseño: nada lee lo retirado, y build y tests lo confirman); borrar un secreto que todavía referencie un app setting vivo (por eso el orden es despliegue primero, secretos después).

## Restricciones

- `ensure-app-settings.ps1` solo crea lo que falta: nunca borra ni modifica. Retirar del YAML no retira de las apps.
- Los secretos no los borra el pipeline: el service connection no debe tener permiso de borrado sobre Key Vault y un borrado automático es difícil de deshacer. Se borran a mano, con soft delete (recuperables 90 días) y con sí explícito por entorno.
- PRE y PRO se limpian como parte de sus despliegues de la siguiente release; el borrado de secretos en PRE y PRO entra como paso del runbook de release, después del despliegue.
- Scripts `.ps1` con BOM UTF-8 y solo ASCII. Documentos en CRLF.

## Enfoques considerados

1. **El pipeline borra los settings, los secretos a mano** (elegido). Paso idempotente en `DeployFunctions`, tras `ensure-app-settings.ps1`, con `az functionapp config appsettings delete` sobre la lista de 27 nombres; contrato con los 27 en `forbiddenSettings`; script de borrado de secretos ejecutado por entorno. Ventaja: PRE y PRO quedan limpios de settings con el mismo despliegue, sin PIM ni paso manual que olvidar. Coste: un paso más en el pipeline, retirable en una release posterior.
2. **Todo a mano, el contrato vigila.** El pipeline solo deja de fijarlos; un script borra settings y secretos por entorno como paso del runbook. Descartado por el usuario: en PRO cambiar app settings exige elevación PIM y una ventana con alguien delante.
3. **El pipeline borra también los secretos.** Descartado: permisos del service connection y reversibilidad.

Decisión del usuario (2026-11-25): enfoque 1, empezando por DEV; PRE y PRO como parte de sus despliegues. Alcance ampliado por el usuario: también `appsettings.json`, `local.settings.template.json`, la clase `GptFallbackExtraerSettings` y la propiedad `DefaultModelKey`.

## Cómo se construye

1. **Pipelines** (`azure-pipelines.yml`, pipeline 799 de release, y `azure-pipelines-functions.yml`, pipeline 802 de DEV; los dos aplican app settings): quitar las 27 líneas del array `$settings` del primero; en ambos, tras `ensure-app-settings.ps1`, llamar a un script nuevo `scripts/configuration/remove-legacy-app-settings.ps1 -ResourceGroup -Name [-SubscriptionId] [-WhatIf]` que lleva la lista de los 27 nombres, consulta cuáles existen con `appsettings list` y borra solo esos con `az functionapp config appsettings delete --setting-names`, diciendo cuántos ha borrado. Idempotente: con cero presentes no llama a `delete`. Un script compartido evita duplicar la lista en dos YAML.
2. **Contrato** (`scripts/config/azure-appsettings-contract.json`): quitar las 8 entradas de `requiredSettings`; añadir los 27 nombres a `forbiddenSettings` con `reason` «Setting heredado sin efecto, retirado en AB#101450». `validate-azure-appsettings-contract.ps1` ya evalúa `forbiddenSettings`; no se modifica.
3. **Scripts**: `set-app-settings.ps1` sin las 31 líneas, con BOM UTF-8 y sin el carácter no ASCII de la l.13 (pendiente (au) absorbido aquí por decisión del usuario); `verify-prod-prereqs.ps1` sin los cuatro secretos en `$requiredSecrets`; nuevo `scripts/configuration/remove-legacy-keyvault-secrets.ps1 -TargetEnvironment dev|pre|prod [-WhatIf]` que resuelve el Key Vault del entorno, lista cuáles de los cuatro secretos existen y los borra con `az keyvault secret delete` (soft delete), uno a uno, con resumen final.
4. **Ficheros de configuración y código**: `appsettings.json` sin `Extraction:AzureContentUnderstanding` (solo las cuatro claves de conexión), `Extraction:GptFallback`, `Classification:DefaultModelKey`, `Classification:AzureDocumentIntelligence` y `Classification:GptFallback`; `local.settings.template.json` sin las claves equivalentes; borrar `GptFallbackExtraerSettings.cs`; quitar `DefaultModelKey` de `ClassificationRoutingSettings`; corregir el texto de la excepción de `OpenAiClientFactory` que cita `Extraction:GptFallback:ApiKey`. Build sin advertencias nuevas, suite Unit completa, `dotnet format` limpio.
5. **Documentación**: MANUAL_CONFIGURACION (hallazgo → retirado), INFRAESTRUCTURA_REAL_DESPLEGADA (tablas de opciones directas, nota de settings heredados, tabla de secretos), 08_CHECKLISTS_DESPLIEGUE (secretos y settings), TROUBLESHOOTING_DIAGNOSTICO, RUNBOOK_INCIDENTES_PRODUCCION, RELEASE_MANAGEMENT (paso post-despliegue «borrar secretos heredados en PRE y PRO» y plantilla de runbook), 01_ARQUITECTURA_SISTEMA, 03_DISENO_TECNICO_DETALLADO, GUIA_EXTRACCION_AZURE_CONTENT_UNDERSTANDING, GUIA_CLASIFICACION_DOCUMENTOS, PERFORMANCE_TUNING, GLOSSARIO_TERMINOS, `infra/ai/README.md` (pasos 1 y 2b del cutover), READMEs de `scripts/migrations/gpt5-mini` y `gpt56-luna` (nota de retirada, sin reescribir el histórico). Las evidencias de `docs/releases/v1.x` no se tocan.
6. **Verificación en DEV** (misma sesión si cabe, con sí explícito para cada escritura): merge a `develop`, despliegue del pipeline 802 con `targetEnvironment=dev` (sirve también para verificar AB#101553), ValidateConfiguration en verde, lectura de los app settings de `srbappdevdocai` sin los 27 nombres, y después `remove-legacy-keyvault-secrets.ps1 -TargetEnvironment dev`. PRE y PRO: con la siguiente release, settings por el pipeline y secretos por el runbook.

## Lo que queda al constructor

Forma exacta del bloque PowerShell de borrado en el YAML (reutilizar el patrón de `appsettings list --query` que ya usa la comprobación de resiliencia), redacción de las notas de retirada en cada documento y orden de los commits (propuesta: pipeline y contrato; scripts; configuración y código; documentación).
