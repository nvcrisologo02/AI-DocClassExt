# Plan De Configuracion Limpia

Fecha: 2026-05-01  
Estado: propuesta de solucion  
Alcance: repositorio, configuracion local, Azure App Settings, Key Vault, SQL dinamico, pipeline y documentacion.

## Objetivo

Definir una version limpia y gobernable de la configuracion del proyecto DocumentIA, eliminando deriva entre entornos, secretos versionados, configuraciones duplicadas y fuentes ambiguas.

Este documento no ejecuta cambios sobre Azure ni sobre datos productivos. Sirve como diseno de referencia para aprobar y secuenciar la remediacion.

## Principio Rector

La solucion no debe ser una limpieza puntual de claves. Debe ser un modelo de control de configuracion con una fuente de verdad clara para cada tipo de dato.

| Fuente | Debe gobernar | No debe gobernar |
|---|---|---|
| Repositorio | Esquema de configuracion, defaults seguros, plantillas, scripts de validacion, seeds bootstrap y documentacion | Secretos reales, valores productivos, publish/bin/obj como fuente |
| Azure Key Vault | Secretos: connection strings, API keys, credenciales GDC, claves Function/Admin, clave AssetResolver | Flags no secretos, configuracion funcional dinamica |
| Azure App Settings | Cableado runtime por app: endpoints, flags, referencias a Key Vault, modo de entorno | Secretos literales, configuracion dinamica de negocio |
| SQL dinamico | Configuracion funcional versionada: modelos, tipologias, prompts, validaciones, plugins y routing | Secretos, connection strings o credenciales |
| Azure DevOps Pipeline | Aplicar estado deseado, desplegar artefactos, validar presencia de settings sin imprimir valores | Verdad oculta no documentada o configuracion manual-only |

## Modelo Objetivo

### 1. Repositorio

- Mantener como activos `host.json`, `appsettings.json`, `appsettings.Development.json`, plantillas locales, seeds bootstrap y scripts de validacion.
- Usar [docs/CATALOGO_APP_SETTINGS.md](CATALOGO_APP_SETTINGS.md) como inventario vivo generado desde codigo.
- Ampliar el catalogo con propiedad de cada clave: owner, consumidor, entorno, secreto/no secreto, requerido/opcional y fuente objetivo.
- Tratar `publish/`, `bin/`, `obj/`, snapshots antiguos y copias generadas como artefactos no autoritativos.
- Sustituir valores reales en configuracion versionada por placeholders seguros o referencias logicas.

### 2. Azure Key Vault

- Centralizar todos los secretos productivos en `srbkvprodocai`.
- Referenciar secretos desde App Settings con `@Microsoft.KeyVault(...)`.
- Rotar cualquier secreto que haya estado versionado, publicado o duplicado en artefactos.
- Definir politica de caducidad y rotacion para secretos criticos.

### 3. Azure App Settings

- Mantener solo cableado runtime, flags y referencias a Key Vault.
- En Functions, el estado objetivo debe incluir como minimo:
  - `SecretsSource=AzureVault`
  - `KeyVaultName=srbkvprodocai`
  - `AzureWebJobsStorage` como referencia a Key Vault
  - `SqlConnectionString` o `ConnectionStrings__DocumentIA` como referencia a Key Vault, eligiendo una forma canonica
  - `AssetResolver__BaseUrl`
  - `AssetResolver__ApiKey` como referencia a Key Vault
  - `RunDatabaseMigrationsOnStartup` alineado con la estrategia aprobada
  - `GDC__Endpoint`, `GDC__TimeoutSeconds` y flags no secretos documentados
  - credenciales GDC como referencias a Key Vault si se mantienen Basic Auth
- En Admin, `FunctionsAdminApi__FunctionKey` no debe estar como literal. Debe ser referencia Key Vault o sustituirse por autenticacion Entra/App Service.
- En AssetResolver, `ApiKey` y `ConnectionStrings__AssetResolverDb` deben seguir como referencias Key Vault.

### 4. SQL Dinamico

- Declarar SQL como fuente activa para `ModeloConfigs`, `TipologiaConfigs` y `PluginTipologiaConfigs`.
- Considerar los seeds del repo como bootstrap-only, no como verdad productiva si SQL ya contiene datos.
- Prohibir secretos dentro de `ConfiguracionJson`.
- Permitir solo identificadores logicos, nombres de deployment, nombres de proveedor, parametros funcionales o referencias no secretas.
- Crear validacion read-only que compare estructura SQL contra esquemas esperados y detecte drift frente a seeds sin sobrescribir datos.

### 5. Pipeline

- Mantener `azure-pipelines.yml` como unico pipeline activo salvo decision explicita contraria.
- Marcar `.github/workflows/infrastructure.yml` como legacy o eliminarlo en una fase controlada.
- El pipeline debe aplicar settings no secretos y referencias Key Vault para Functions, Admin y AssetResolver.
- El pipeline debe validar despues del despliegue que existen las claves obligatorias, sin imprimir valores.
- La estrategia de migraciones debe ser una sola: startup, pipeline o operacion manual aprobada.

## Decisiones Necesarias

| Decision | Opcion recomendada | Motivo |
|---|---|---|
| Fuente de secretos | Key Vault | Ya existe y encaja con App Service/Functions |
| Fuente de negocio dinamico | SQL | Ya existe modelo versionado y Admin API |
| Azure App Configuration | Diferir | Anade superficie operativa; no resuelve primero el drift actual |
| Autenticacion Admin -> Functions | Corto plazo: Function Key en Key Vault. Medio plazo: Entra | Reduce riesgo inmediato sin redisenar auth completa |
| Migraciones EF | Elegir pipeline o manual; evitar startup en prod salvo ventana controlada | Evita cambios de esquema implicitos al arrancar Functions |
| Secrets en SQL config | No permitidos | Evita fuga por Admin/API/exportaciones |
| Artefactos generados | No autoritativos y fuera de auditorias | Evita falsos positivos y secretos duplicados |

## Plan De Remediacion

### Fase 0. Congelacion Y Evidencia

- Congelar cambios de configuracion no relacionados con este plan.
- Confirmar con lectura Azure el estado actual de App Settings, Key Vault references y slots si existieran.
- Completar auditoria read-only de SQL dinamico con acceso MFA aprobado.
- Guardar evidencias sin valores secretos.

### Fase 1. Contrato Canonico

- Extender [docs/CATALOGO_APP_SETTINGS.md](CATALOGO_APP_SETTINGS.md) o su generador para incluir owner, entorno, secreto/no secreto, fuente objetivo y obligatoriedad.
- Crear matriz canonica de settings por componente:
  - Functions
  - Admin Web App
  - AssetResolver
  - local dev
  - pipeline variables
  - Key Vault secrets
  - SQL dynamic config
- Decidir forma canonica para connection strings jerarquicas: `ConnectionStrings__DocumentIA` frente a `SqlConnectionString`.

### Fase 2. Limpieza De Secretos

- Sustituir secretos versionados por placeholders en configuraciones fuente.
- Sanear seeds de modelos y plugins para eliminar API keys reales.
- Sacar `FunctionsAdminApi__FunctionKey` literal de configuracion versionada y Azure App Settings.
- Rotar secretos expuestos o copiados en artefactos.
- Revisar exclusiones para que publish/bin/obj no participen en auditorias de configuracion.

### Fase 3. Alineacion De Runtime Azure

- Anadir a Function App el cableado faltante de AssetResolver mediante App Settings y Key Vault reference.
- Alinear `RunDatabaseMigrationsOnStartup` entre pipeline, scripts y estado productivo.
- Revisar `GDC__BypassSslValidation`; documentar excepcion temporal o retirarla cuando se arregle confianza TLS.
- Revisar `httpsOnly`, acceso publico y red privada en una fase de seguridad separada si requiere pruebas de conectividad.

### Fase 4. Normalizacion SQL

- Definir esquema permitido para `ConfiguracionJson` de modelos, tipologias y plugins.
- Eliminar secretos o valores no permitidos de configuracion dinamica.
- Marcar seeds como bootstrap-only.
- Crear export/audit read-only para comparar SQL contra esquemas y catalogo esperado.

### Fase 5. Desarrollo Local Limpio

- Crear plantilla local segura para Functions y AssetResolver con placeholders.
- Documentar dos modos soportados:
  - local autocontenido con Azurite/SQL local o recursos dev
  - local conectado a Azure mediante identidad aprobada y Key Vault
- Retirar o documentar claves `_dev` para evitar alias ad hoc.
- Actualizar `.env.example` como legacy o sustituirlo por plantillas reales de .NET.

### Fase 6. Pipeline Y Documentacion

- Actualizar scripts activos para aplicar todas las claves requeridas, incluyendo AssetResolver.
- Convertir validaciones postdeploy en gates del pipeline.
- Archivar o eliminar workflows obsoletos solo tras aprobacion.
- Actualizar README, manual de explotacion, despliegue y healthcheck para reflejar el modelo real.

### Fase 7. Validacion Final

- Ejecutar build y tests relevantes.
- Ejecutar smoke test de Functions.
- Validar health/config sin mostrar secretos.
- Validar integracion AssetResolver.
- Ejecutar escaneo de secretos sobre fuentes versionadas.
- Validar App Settings por nombre, tipo y presencia, nunca por valor en logs.

## Matriz Inicial De Remediacion

| Hallazgo | Estado objetivo | Artefacto a cambiar | Riesgo | Validacion |
|---|---|---|---|---|
| Function App sin `AssetResolver__*` | Settings presentes; API key por Key Vault reference | Pipeline/scripts/App Settings | Alto funcional | Healthcheck e integracion AssetResolver |
| Admin con FunctionKey literal | Key Vault reference o Entra auth | Admin App Settings/pipeline | Alto seguridad | App setting muestra referencia, no literal |
| Seeds con API keys | Placeholders o referencias logicas | `src/backend/DocumentIA.Functions/config/**` | Alto seguridad | Secret scan limpio |
| Pipeline y prod difieren en migraciones | Una estrategia aprobada | `azure-pipelines.yml`, scripts, docs | Medio/alto | Deploy dry-run/validacion setting |
| GitHub Actions infra obsoleto | Marcado legacy o eliminado | `.github/workflows/infrastructure.yml` | Medio operativo | Solo queda pipeline soportado documentado |
| `.env.example` legacy | Sustituido por plantillas .NET actuales | `.env.example`, docs local dev | Medio | Onboarding local reproducible |
| SQL dinamico no auditado | Auditoria read-only completada | SQL/report | Alto desconocido | Reporte de tablas dinamicas |
| Artefactos publish/bin con config copiada | No autoritativos, excluidos o limpiados | `.gitignore`, scripts, docs | Medio seguridad | Auditoria ignora generados; secret scan controlado |

## Orden Recomendado De Ejecucion

1. Aprobar decisiones abiertas.
2. Completar auditoria SQL dinamica.
3. Definir contrato canonico y matriz de settings.
4. Sanear secretos en repo y rotar los expuestos.
5. Alinear pipeline/scripts con el contrato.
6. Aplicar cambios Azure en ventana controlada.
7. Normalizar SQL dinamico.
8. Actualizar documentacion y cerrar workflows legacy.
9. Ejecutar validacion final.

## Cambios Que No Deben Hacerse Sin Aprobacion Explicita

- Modificar o borrar recursos Azure.
- Rotar secretos productivos.
- Cambiar estrategia de autenticacion Admin/Functions.
- Ejecutar migraciones contra SQL productivo.
- Eliminar workflows, scripts historicos o artefactos publicados.
- Sobrescribir configuracion dinamica SQL con seeds del repo.

## Resultado Esperado

Al finalizar, cada clave tendra un propietario, una fuente de verdad, una forma de despliegue y una validacion. El repositorio quedara libre de secretos reales, Azure usara referencias a Key Vault, SQL conservara la configuracion funcional dinamica sin credenciales, y el pipeline sera el mecanismo unico y verificable para mantener el runtime alineado.