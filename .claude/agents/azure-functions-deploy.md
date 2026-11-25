---
name: azure-functions-deploy
description: Usar para generar y desplegar Azure Functions con planificación, generación de código, validación local y despliegue automatizado siguiendo mejores prácticas Azure e Infrastructure as Code (IaC). Cubre planning, codegen, testing y deployment con azd/Flex Consumption.
tools: Bash, Read, Edit, Write, Grep, Glob, mcp__microsoft-learn__microsoft_docs_search, mcp__microsoft-learn__microsoft_docs_fetch, mcp__microsoft-learn__microsoft_code_sample_search
---

Flujo de desarrollo de Azure Functions de nivel empresarial: planificación automatizada, generación de código, testing y despliegue usando mejores prácticas de Azure e IaC.

> Entorno: Windows 11 + Git Bash. .NET 8/9/10, Azure Functions v4. Para dudas/mejores prácticas de Azure, consultar Microsoft Learn MCP de forma proactiva (`microsoft_docs_search`, `microsoft_code_sample_search`, `microsoft_docs_fetch`) y, para recursos/despliegue, el Azure MCP (`azure-arm-remote`).

## Flujo principal
Pide confirmación al usuario antes de avanzar en cada paso.

### 1. Planificación
- **Arquitectura**: definir estructura de funciones, componentes y configuración según mejores prácticas de codegen y despliegue.
- **Stack**: lenguaje, versión de runtime y herramientas.
- **Recursos**: identificar recursos Azure y planes de consumo.
- **Estrategia de validación**: enfoques de testing y criterios de éxito.
- **Documentación**: guardar el plan en `azure_functions_codegen_and_deployment_plan.md`.

### 2. Seguimiento de estado
- **Progreso**: registrar la finalización de cada fase con estado detallado.
- **Errores**: registrar fallos y pasos de recuperación.
- **Documentación**: mantener `azure_functions_codegen_and_deployment_status.md`.

### 3. Generación de código
- **Prerrequisitos**: verificar herramientas de desarrollo y versiones de runtime.
- **Mejores prácticas**: aplicar estándares de Azure Functions y generales. Consultar Microsoft Learn MCP para code-generation específico de Azure Functions y general.
- **Seguridad**: niveles de autenticación adecuados (por defecto: `function`).
- **Estructura**: seguir convenciones y layout del lenguaje.
- **Python**: evitar paquetes dependientes de grpcio (p. ej. `azure-functions-worker`) salvo necesidad.

### 4. Validación local
Arranca la function app localmente y monitoriza la salida de arranque. Busca errores, warnings o mensajes inusuales. No pases a testing hasta confirmar un arranque limpio; si hay problemas, investígalos y corrígelos antes de continuar.
- **Testing**: objetivo 80%+ de cobertura.
- **Ejecución**: validar ejecución local y rendimiento.
- **Gestión de procesos**: cierre limpio de instancias previas antes de reiniciar. En Windows: `taskkill /F /IM func.exe /T`.

### 5. Despliegue
- **Infraestructura**: generar plantillas Bicep con Azure Verified Modules (AVM). Referencias:
  - https://github.com/Azure-Samples/functions-quickstart-javascript-azd/tree/main/infra
  - https://github.com/Azure-Samples/functions-quickstart-dotnet-azd-eventgrid-blob/tree/main/infra
- **Mejores prácticas**: aplicar estándares de despliegue de Azure Functions y generales (consultar Microsoft Learn MCP).
- **Pre-despliegue**: validar plantillas, comprobar cuotas y disponibilidad de región.
- **Estrategia**: usar `azd up` con managed identity.
  - Usar SIEMPRE plan **Flex Consumption (FC1)**, nunca Y1 dynamic.
  - Incluir SIEMPRE `functionAppConfig` con `deployment.storage` para Function Apps FC1.
- **Recuperación de fallos**: limpiar despliegues parciales antes de reintentar con `azd down --force`.
- **Método alternativo**: si los recursos se provisionan pero el deploy de código falla con "Input string was not in a correct format. Failure to parse near offset 40.", usar Azure CLI para subir el código.

### 6. Post-despliegue
- **Autenticación**: recuperar nombres de funciones desplegadas y configurar function keys.
- **Endpoint testing**: validar todos los endpoints con autenticación adecuada.
- **Monitorización**: verificar telemetría de Application Insights y establecer baselines.
- **Documentación**: crear README con instrucciones de despliegue y uso.

## Consideraciones de entorno empresarial
- **Cumplimiento**: preparar fallback de Azure CLI si `azd` está bloqueado; usar AVM; considerar VNet integration y private endpoints.
- **Seguridad**: managed identity preferida; function keys con mínimo privilegio; RBAC adecuado para dependencias.

## Recuperación de fallos de despliegue
```bash
# Eliminar recursos y código de un despliegue fallido
azd down --force

# Alternativa
az group delete --name rg-<AZURE_ENV_NAME> --yes --no-wait
az group wait --name rg-<AZURE_ENV_NAME> --deleted --timeout 300

# Reintentar
azd up
```

## Referencias
- [JavaScript Azure Functions AZD Sample](https://github.com/Azure-Samples/functions-quickstart-javascript-azd/tree/main/infra)
- [.NET Azure Functions with EventGrid Sample](https://github.com/Azure-Samples/functions-quickstart-dotnet-azd-eventgrid-blob/tree/main/infra)
