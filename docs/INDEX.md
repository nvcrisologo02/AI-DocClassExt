# Índice de documentación — DocumentIA

> Última revisión estructural: 2026-11-25 (AB#101452, retirada de `entrega/`). Revisión estructural previa: 2026-11-22 (AB#101445). Rama de referencia: `develop`.
> Verificación de contenido por familia cerrada el 2026-11-22 (AB#101447) e integrada en `develop` el 2026-11-23 (AB#101448). El estado de cada fichero se actualiza aquí con cada cambio que lo afecte; las decisiones derivadas están en las tasks AB#101449 a AB#101452. Las marcas «no comprobado» del repaso se comprobaron en vivo contra Azure, Azure DevOps, App Insights y las BD de DEV, PRE y PRO el 2026-11-23 (AB#101449) y se sustituyeron por el estado real.

DocumentIA es el sistema de clasificación y extracción documental de SAREB, en operación desde julio de 2026. Esta página cataloga la documentación permanente del repositorio, organizada en las cuatro familias que usa el impacto documental de cada cambio: técnica, funcional, arquitectura y explotación. El mapa por familia también está declarado en el `CLAUDE.md` del proyecto.

## Cómo se lee este índice

- **Estado** `vigente`: describe el sistema actual y se mantiene con cada cambio que lo afecte. `histórico`: registro de algo ya ocurrido (releases, runbooks de una fecha, planes iniciales, presentaciones); se conserva sin reescribir. `sustituido`: lo cubre otro documento; se conserva por referencia con un aviso en cabecera y no se mantiene.
- **Fuente de verdad**: cuando dos documentos se solapan, la tabla dice cuál manda.
- Las rutas son relativas a `docs/`.

## Carpetas que no forman parte de la documentación permanente

| Carpeta | Qué contiene | Versionada |
|---|---|---|
| `docs/auxiliares/` | Material de trabajo: análisis, planes, auditorías, manuales retirados (`migracion-deployment/`, `sql-schemas/`, `plans/`) y los temporales de sesión en `temps/<fecha>/` | No (`.gitignore`). Los documentos permanentes no deben enlazar aquí |
| `docs/superpowers/specs/` y `plans/` | Especificaciones de diseño y planes de implementación de cada unidad de trabajo | Specs sí; planes no |
| `docs/presentaciones/` | Guiones y apoyo visual de presentaciones (abril y mayo de 2026) | Solo los dos ficheros antiguos ya versionados; los nuevos se ignoran |
| `entrega/` (raíz) | Retirada del árbol el 2026-11-25 (AB#101452): era la copia congelada de la documentación entregada el 2026-06-30 (87 ficheros, 15 MB) y solo la citaba este índice; sigue disponible en el historial de git hasta el commit 5b270177 | No (retirada) |

## Por dónde empezar

| Perfil | Lectura |
|---|---|
| Desarrollador nuevo | [QUICKSTART_DESARROLLADORES.md](guias/QUICKSTART_DESARROLLADORES.md), [GLOSSARIO_TERMINOS.md](referencias/GLOSSARIO_TERMINOS.md), [01_ARQUITECTURA_SISTEMA.md](01_ARQUITECTURA_SISTEMA.md), [03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md) |
| Integrador de la API | [CONTRATO_API_HTTP.md](contratos/CONTRATO_API_HTTP.md), [05_MANUAL_USO_CONFIGURACION.md](05_MANUAL_USO_CONFIGURACION.md) Parte B |
| Negocio y configuración de tipologías | [02_ANALISIS_FUNCIONAL.md](02_ANALISIS_FUNCIONAL.md), [GUIA_CLASIFICACION_DOCUMENTOS.md](guias/GUIA_CLASIFICACION_DOCUMENTOS.md), [05_MANUAL_USO_CONFIGURACION.md](05_MANUAL_USO_CONFIGURACION.md) Parte A |
| Operación y despliegue | [RELEASE_MANAGEMENT.md](procedimientos/RELEASE_MANAGEMENT.md), [INFRAESTRUCTURA_REAL_DESPLEGADA.md](infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md), [RUNBOOK_INCIDENTES_PRODUCCION.md](procedimientos/RUNBOOK_INCIDENTES_PRODUCCION.md), [TROUBLESHOOTING_DIAGNOSTICO.md](guias/TROUBLESHOOTING_DIAGNOSTICO.md) |
| Arquitectura y decisiones | [01_ARQUITECTURA_SISTEMA.md](01_ARQUITECTURA_SISTEMA.md), [DATA_MODELS_ER_DIAGRAM.md](especificaciones/DATA_MODELS_ER_DIAGRAM.md), carpeta [decisiones/](decisiones/) |

## Familia técnica

Diseño técnico, API y contratos, configuración, desarrollo y plugins.

| Documento | Contenido | Estado |
|---|---|---|
| [README.md](../README.md) (raíz) | Visión general, requisitos (.NET 10 para Functions, .NET 8 para el resto), setup local y comandos | vigente |
| [03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md) | Flujo del pipeline, orquestación Durable, clasificación jerárquica GPT, modelo ER, contratos internos | vigente |
| [05_MANUAL_USO_CONFIGURACION.md](05_MANUAL_USO_CONFIGURACION.md) | Parte A: uso diario. Parte B: APIs, contratos, `ConfiguracionJson`, claves de prompt y modelo | vigente |
| [contratos/CONTRATO_API_HTTP.md](contratos/CONTRATO_API_HTTP.md) | Contrato REST de ingest, consulta, management y Admin de tipologías. Fuente de verdad de la API | vigente |
| [contratos/PLANTILLA_PLUGINS_JSON.md](contratos/PLANTILLA_PLUGINS_JSON.md) | Plantillas JSON de configuración de plugins para copiar | vigente |
| [manuales/MANUAL_CONFIGURACION.md](manuales/MANUAL_CONFIGURACION.md) | App settings por entorno y precedencia; manda en ese tema (05 Parte B manda en contrato, `ConfiguracionJson` y modelos; FUENTE_VERDAD en BD frente a seeds). Revisado 2026-11-26 (AB#101570: `PromptDefaults__ModelKey` y `GDC__BypassSslValidation` fijados por el pipeline) | vigente |
| [manuales/MANUAL_PLUGINS.md](manuales/MANUAL_PLUGINS.md) | Runtime de plugins: carga, configuración por tipología, integración REST y SOAP | vigente |
| [manuales/MANUAL_COSTES_IA.md](manuales/MANUAL_COSTES_IA.md) | Coste de IA por ejecución: medición, tarifas por modelo físico y consulta | vigente |
| [manuales/MANUAL_DEDUPLICACION.md](manuales/MANUAL_DEDUPLICACION.md) | Deduplicación por SHA256 y clave de intención, reutilización de ejecuciones y su traza (ADR-003) | vigente |
| [guias/QUICKSTART_DESARROLLADORES.md](guias/QUICKSTART_DESARROLLADORES.md) | Setup local y primera clasificación de extremo a extremo | vigente |
| [guias/GUIA_HOST_LOCAL_CONTRA_DEV.md](guias/GUIA_HOST_LOCAL_CONTRA_DEV.md) | Ejecutar el host de Functions en local contra los recursos de DEV | vigente |
| [guias/EXTENSIBILIDAD_PLUGIN_SYSTEM.md](guias/EXTENSIBILIDAD_PLUGIN_SYSTEM.md) | Arquitectura del sistema de plugins y providers, cómo crear uno nuevo y probarlo | vigente |
| [guias/GUIA_EXTRACCION_AZURE_CONTENT_UNDERSTANDING.md](guias/GUIA_EXTRACCION_AZURE_CONTENT_UNDERSTANDING.md) | Extracción con Azure Content Understanding: analyzers, proyectos y modelos requeridos | vigente |
| [especificaciones/ESPECIFICACION_PLUGIN_ASSETRESOLVER.md](especificaciones/ESPECIFICACION_PLUGIN_ASSETRESOLVER.md) | Plugin AssetResolver: resolución de `IdActivo`, criterios y grupos | vigente |
| [especificaciones/ESPECIFICACION_CAPA_SERVICIO_GDC_SINTWS.md](especificaciones/ESPECIFICACION_CAPA_SERVICIO_GDC_SINTWS.md) | Integración con GDC (SINTWS): contrato, errores y configuración | vigente |
| [especificaciones/ESPECIFICACION_PROMPTS_CONFIGURABLES.md](especificaciones/ESPECIFICACION_PROMPTS_CONFIGURABLES.md) | Política de prompts configurables en BD (`PromptTemplates`). Cabecera aún en «propuesta»; la implementación está hecha | vigente |
| [especificaciones/PROMPT_CLASIFICACION_COMPLETO.md](especificaciones/PROMPT_CLASIFICACION_COMPLETO.md) | Prompt de clasificación renderizado a partir del seed del repositorio. La verdad operativa está en la BD de cada entorno | vigente |
| [referencias/FUENTE_VERDAD_CONFIGURACION.md](referencias/FUENTE_VERDAD_CONFIGURACION.md) | Qué configuración manda (BD frente a JSON del repo frente a app settings) | vigente |
| [procedimientos/DATABASE_MIGRATION_STRATEGY.md](procedimientos/DATABASE_MIGRATION_STRATEGY.md) | Crear y probar migraciones EF Core en local; el despliegue está en el runbook de release | vigente |
| [15_API_DOCUMENTATION_V1_4.md](15_API_DOCUMENTATION_V1_4.md) | Documentación en inglés de la API de administración de tipologías v1.4 | sustituido por CONTRATO_API_HTTP §9 |

## Familia funcional

Análisis funcional, reglas de negocio, tipologías, confianza y validaciones.

| Documento | Contenido | Estado |
|---|---|---|
| [02_ANALISIS_FUNCIONAL.md](02_ANALISIS_FUNCIONAL.md) | Problema de negocio, actores, casos de uso, historias y requisitos. Revisado 2026-11-22 | vigente |
| [guias/GUIA_CLASIFICACION_DOCUMENTOS.md](guias/GUIA_CLASIFICACION_DOCUMENTOS.md) | Cómo clasifica el sistema: flujo por defecto `rules → gpt → di` con fallback global, `hybrid-tdn`, niveles TDN1/TDN2, modos de petición. Revisado 2026-11-22; el §4.1 conserva el flujo `di` histórico con aviso | vigente |
| [guias/GUIA_CLASIFICACION_RESTRINGIDA.md](guias/GUIA_CLASIFICACION_RESTRINGIDA.md) | Clasificación restringida a un subconjunto de tipologías (`restriccionTipologias`, catálogo filtrado). Revisado 2026-11-22 | vigente |
| [referencias/GLOSSARIO_TERMINOS.md](referencias/GLOSSARIO_TERMINOS.md) | Términos, acrónimos y conceptos del proyecto. Revisado 2026-11-22 | vigente |
| [referencias/TIPOLOGIAS_REFERENCIA.md](referencias/TIPOLOGIAS_REFERENCIA.md) | Subconjunto del catálogo de tipologías (el catálogo real está en BD: 233 en DEV y 213 en PRE y PRO a 2026-11-23). Revisado 2026-11-22 | vigente |
| [referencias/CONFIANZA_AGREGADA.md](referencias/CONFIANZA_AGREGADA.md) | Cálculo de la confianza agregada y umbrales. Fuente de verdad frente al diagrama. Revisado 2026-11-22 | vigente |
| [referencias/CAUSISTICAS_NOTA_SIMPLE_1_4.md](referencias/CAUSISTICAS_NOTA_SIMPLE_1_4.md) | Casuísticas de Nota Simple para el modelo 1.4 (analyzer `CU_NS_1.4_3`; la vigente es 1.5 con `CU_NS_1.5_0`). Revisado 2026-11-22 | vigente |
| [manuales/MANUAL_VALIDACIONES.md](manuales/MANUAL_VALIDACIONES.md) | Motor de validaciones: tipos de regla, configuración, persistencia y resultados. Revisado 2026-11-22 | vigente |
| [especificaciones/ESPECIFICACION_LIMITE_PAGINAS_DOCUMENTO.md](especificaciones/ESPECIFICACION_LIMITE_PAGINAS_DOCUMENTO.md) | Límite de páginas por documento y su tratamiento. Implementada (fbf33519). Revisado 2026-11-22 | vigente |
| [diagrams/UMBRAL_PRIORIZACION.md](diagrams/UMBRAL_PRIORIZACION.md) | Diagrama de priorización por umbral. Revisado 2026-11-22 | vigente |
| [diagrams/CONFIANZA_UMBRAL.md](diagrams/CONFIANZA_UMBRAL.md) | Diagrama del umbral de confianza | sustituido por CONFIANZA_AGREGADA |
| [presentaciones/APOYO_VISUAL_PRESENTACION.md](presentaciones/APOYO_VISUAL_PRESENTACION.md), [presentaciones/GUION_PRESENTACION_30MIN.md](presentaciones/GUION_PRESENTACION_30MIN.md) | Presentación del sistema (abril y mayo de 2026) | histórico |

## Familia arquitectura

Arquitectura, infraestructura, modelo de datos y decisiones.

| Documento | Contenido | Estado |
|---|---|---|
| [01_ARQUITECTURA_SISTEMA.md](01_ARQUITECTURA_SISTEMA.md) | Componentes, flujo de extremo a extremo, proveedores de IA, despliegue lógico | vigente |
| [infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md](infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md) | Recursos, app settings, secretos, identidades y pipelines reales de DEV, PRE y PRO. Fuente de verdad de la infraestructura | vigente |
| [especificaciones/DATA_MODELS_ER_DIAGRAM.md](especificaciones/DATA_MODELS_ER_DIAGRAM.md) | Modelo de datos: entidades, relaciones, versionado del esquema y consultas habituales | vigente |
| [especificaciones/OPTIMIZACION_ALMACENAMIENTO_BD_PRO.md](especificaciones/OPTIMIZACION_ALMACENAMIENTO_BD_PRO.md) | Diseño de la reducción de almacenamiento de la BD de PRO, aplicado en las releases de septiembre de 2026 | histórico |
| [decisiones/ADR-001-promocion-artefactos-ia-dev-pre-pro.md](decisiones/ADR-001-promocion-artefactos-ia-dev-pre-pro.md) | IA propia por entorno y promoción de artefactos DEV → PRE → PRO | vigente (aceptada) |
| [decisiones/ADR-002-clasificador-embeddings-hibrido.md](decisiones/ADR-002-clasificador-embeddings-hibrido.md) | Clasificador híbrido por embeddings en sombra. El bloque sigue en «propuesta» | vigente (propuesta) |
| [decisiones/ADR-003-reutilizacion-duplicado-clave-intencion.md](decisiones/ADR-003-reutilizacion-duplicado-clave-intencion.md) | Reutilización de ejecuciones duplicadas por clave de intención | vigente (aceptada) |
| [decisiones/ADR-004-cambio-modelo-ia-generativa-gpt56-luna.md](decisiones/ADR-004-cambio-modelo-ia-generativa-gpt56-luna.md) | Cambio del modelo generativo a gpt-5.6-luna (DEV desde 2026-11-20; PRE y PRO con la siguiente release) | vigente (aceptada) |
| [decisiones/ADR-005-altas-config-clave-natural-configseed.md](decisiones/ADR-005-altas-config-clave-natural-configseed.md) | Altas de configuración por clave natural en la etapa ConfigSeed del pipeline 832 (`applyMissingConfig`) | vigente (aceptada) |
| [infraestructura/INFRAESTRUCTURA_DESPLIEGUE.md](infraestructura/INFRAESTRUCTURA_DESPLIEGUE.md) | Plantilla genérica Staging/Production que no refleja lo desplegado | sustituido por INFRAESTRUCTURA_REAL_DESPLEGADA |

## Familia explotación

Release y despliegue, operación, observabilidad, incidentes y acceso.

| Documento | Contenido | Estado |
|---|---|---|
| [procedimientos/RELEASE_MANAGEMENT.md](procedimientos/RELEASE_MANAGEMENT.md) | Runbook de release DEV → PRE → PRO: fases 0 a 5, puertas de PRE, catálogo de pipelines, vuelta atrás, secretos y app settings | vigente |
| [releases/README.md](releases/README.md) | Registro de releases por versión (`releases/vX.Y.Z/release.md`, `runbook.md`, `evidencias/`) y plantillas en `releases/_plantilla/` | vigente; cada versión es histórico |
| [procedimientos/DESPLIEGUE_IA.md](procedimientos/DESPLIEGUE_IA.md) | Promoción de artefactos de IA (analyzers, proyectos, deployments) entre entornos, según ADR-001 | vigente |
| [08_CHECKLISTS_DESPLIEGUE.md](08_CHECKLISTS_DESPLIEGUE.md) | Checklists de comprobación de configuración, pipelines y observabilidad al desplegar | vigente |
| [infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md](infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md) | Inventario operativo de recursos y configuración por entorno (también en arquitectura) | vigente |
| [procedimientos/RUNBOOK_INCIDENTES_PRODUCCION.md](procedimientos/RUNBOOK_INCIDENTES_PRODUCCION.md) | Escenarios de incidente, diagnóstico KQL y acciones. Los contactos de escalado siguen por rellenar | vigente |
| [guias/TROUBLESHOOTING_DIAGNOSTICO.md](guias/TROUBLESHOOTING_DIAGNOSTICO.md) | Árbol de diagnóstico, casos resueltos y consultas | vigente |
| [observabilidad/MONITOREO_ALERTAS_REAL.md](observabilidad/MONITOREO_ALERTAS_REAL.md) | Telemetría, alertas y resiliencia verificadas en Azure. Fuente de verdad de las alertas | vigente |
| [observabilidad/OBSERVABILIDAD_KQL.md](observabilidad/OBSERVABILIDAD_KQL.md) | Recetario de consultas KQL, workbooks y depuración | vigente |
| [observabilidad/CU_RENDIMIENTO_INSIGHTS.md](observabilidad/CU_RENDIMIENTO_INSIGHTS.md) | Métricas por subfase de Content Understanding y su workbook | vigente |
| [manuales/MANUAL_HEALTHCHECK.md](manuales/MANUAL_HEALTHCHECK.md) | Endpoint de health check: comprobaciones y lectura del resultado | vigente |
| [guias/GUIA_EASYAUTH_ADMIN.md](guias/GUIA_EASYAUTH_ADMIN.md) | Autenticación del Admin con App Service Authentication. Fuente de verdad del acceso al Admin | vigente |
| [guias/GUIA_RESTRICCION_ACCESO_ADMIN.md](guias/GUIA_RESTRICCION_ACCESO_ADMIN.md) | Restricción de red al Admin, medida complementaria opcional (EasyAuth ya está activo en los tres entornos). Revisado 2026-11-22 | vigente |
| [guias/PERFORMANCE_TUNING.md](guias/PERFORMANCE_TUNING.md) | Ajustes de concurrencia, resiliencia de CU y Azure OpenAI, índices y escalado. Las cifras de benchmark son plantilla sin medición y los ajustes `GptFallback` no se leen (avisos en cabecera). Revisado 2026-11-22 | vigente con avisos |
| [guias/SOLICITUD_APP_REGISTRATION_ADMIN.md](guias/SOLICITUD_APP_REGISTRATION_ADMIN.md) | Texto de la solicitud de app registration y grupo de seguridad para el Admin | histórico |
| [procedimientos/RUNBOOK_RELEASE_PRO_2026-09.md](procedimientos/RUNBOOK_RELEASE_PRO_2026-09.md) | Releases a PRO del 3 y el 20 de septiembre de 2026 | histórico |
| [procedimientos/PLAN_DESPLIEGUE_INICIAL_DEV_PRE.md](procedimientos/PLAN_DESPLIEGUE_INICIAL_DEV_PRE.md) | Plan del primer despliegue de DEV y PRE (junio de 2026) | histórico |
| [procedimientos/AZURE_DEVOPS_BOOTSTRAP_SETUP.md](procedimientos/AZURE_DEVOPS_BOOTSTRAP_SETUP.md) | Montaje inicial de pipelines y service connections en Azure DevOps | histórico |
| [referencias/TEAM_MATRIX_RESPONSABILIDADES.md](referencias/TEAM_MATRIX_RESPONSABILIDADES.md) | RACI, guardias y SLA genéricos sin datos reales | sustituido; sin sustituto hasta que RUNBOOK_INCIDENTES recoja los contactos reales |

## Documentos sustituidos

Se conservan por referencia, con un aviso en su cabecera, y no se mantienen. Si vuelven a hacer falta, se parte del documento vigente.

| Sustituido | Vigente |
|---|---|
| [15_API_DOCUMENTATION_V1_4.md](15_API_DOCUMENTATION_V1_4.md) | [contratos/CONTRATO_API_HTTP.md](contratos/CONTRATO_API_HTTP.md) §9 |
| [infraestructura/INFRAESTRUCTURA_DESPLIEGUE.md](infraestructura/INFRAESTRUCTURA_DESPLIEGUE.md) | [infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md](infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md) |
| [diagrams/CONFIANZA_UMBRAL.md](diagrams/CONFIANZA_UMBRAL.md) | [referencias/CONFIANZA_AGREGADA.md](referencias/CONFIANZA_AGREGADA.md) |
| [referencias/TEAM_MATRIX_RESPONSABILIDADES.md](referencias/TEAM_MATRIX_RESPONSABILIDADES.md) | Ninguno; contactos y escalado pendientes en [RUNBOOK_INCIDENTES_PRODUCCION.md](procedimientos/RUNBOOK_INCIDENTES_PRODUCCION.md) |

## Estructura de carpetas

```
docs/
├── INDEX.md
├── 01_ARQUITECTURA_SISTEMA.md
├── 02_ANALISIS_FUNCIONAL.md
├── 03_DISENO_TECNICO_DETALLADO.md
├── 05_MANUAL_USO_CONFIGURACION.md
├── 08_CHECKLISTS_DESPLIEGUE.md
├── 15_API_DOCUMENTATION_V1_4.md        (sustituido)
├── contratos/          CONTRATO_API_HTTP, PLANTILLA_PLUGINS_JSON
├── decisiones/         ADR-001 … ADR-005
├── diagrams/           UMBRAL_PRIORIZACION, CONFIANZA_UMBRAL (sustituido), fuentes .mmd
├── especificaciones/   DATA_MODELS_ER_DIAGRAM, ASSETRESOLVER, GDC_SINTWS, PROMPTS_CONFIGURABLES, PROMPT_CLASIFICACION_COMPLETO, LIMITE_PAGINAS, OPTIMIZACION_ALMACENAMIENTO_BD_PRO
├── guias/              QUICKSTART, HOST_LOCAL_CONTRA_DEV, EXTENSIBILIDAD_PLUGIN_SYSTEM, EXTRACCION_CU, CLASIFICACION_DOCUMENTOS, CLASIFICACION_RESTRINGIDA, TROUBLESHOOTING, EASYAUTH_ADMIN, RESTRICCION_ACCESO_ADMIN, SOLICITUD_APP_REGISTRATION_ADMIN
├── infraestructura/    INFRAESTRUCTURA_REAL_DESPLEGADA, INFRAESTRUCTURA_DESPLIEGUE (sustituido)
├── manuales/           CONFIGURACION, PLUGINS, COSTES_IA, DEDUPLICACION, VALIDACIONES, HEALTHCHECK
├── observabilidad/     MONITOREO_ALERTAS_REAL, OBSERVABILIDAD_KQL, CU_RENDIMIENTO_INSIGHTS, workbooks
├── presentaciones/     (histórico; nuevos ficheros ignorados)
├── procedimientos/     RELEASE_MANAGEMENT, DESPLIEGUE_IA, DATABASE_MIGRATION_STRATEGY, RUNBOOK_INCIDENTES_PRODUCCION, RUNBOOK_RELEASE_PRO_2026-09, PLAN_DESPLIEGUE_INICIAL_DEV_PRE, AZURE_DEVOPS_BOOTSTRAP_SETUP
├── referencias/        GLOSSARIO, TIPOLOGIAS_REFERENCIA, CONFIANZA_AGREGADA, CAUSISTICAS_NOTA_SIMPLE_1_4, FUENTE_VERDAD_CONFIGURACION, TEAM_MATRIX (sustituido)
├── releases/           README, _plantilla/, v1.0.0/, v1.1.0/, v1.1.1/
├── superpowers/        specs/ (versionadas) y plans/ (no versionados)
└── auxiliares/         (no versionado)
```

## Mantenimiento

- Cada unidad de trabajo evalúa su impacto documental sobre las cuatro familias y actualiza en el mismo commit lo que deje de ser cierto; al cerrar sesión se repasan las cuatro familias contra todo lo cambiado.
- Un documento nuevo entra en este índice con familia y estado. Un documento que deja de ser cierto se corrige o pasa a `sustituido` con su aviso de cabecera; no se deja desactualizado en silencio.
- Los enlaces entre documentos permanentes son relativos y apuntan solo a ficheros versionados.
