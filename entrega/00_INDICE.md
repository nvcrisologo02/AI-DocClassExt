# Índice de Documentación — DocumentIA

DocumentIA es un sistema modular de clasificación y extracción automática de documentos, construido sobre Azure (Durable Functions en .NET, Azure AI Document Intelligence, Azure Content Understanding y Azure OpenAI como fallback). Procesa documentos (principalmente Notas Simples Registrales y tipologías afines), determina su tipología, extrae datos estructurados, valida los resultados y los integra con sistemas externos (GDC, AssetResolver).

Esta documentación está organizada por audiencia en cuatro bloques. Cada documento es autocontenido; los enlaces internos son relativos a esta carpeta de entrega.

---

## Guía de lectura por audiencia

| Audiencia | Qué necesita | Documentos recomendados |
|-----------|--------------|-------------------------|
| **Negocio** | Qué hace el sistema y con qué reglas | Análisis Funcional · Glosario · Casuísticas Nota Simple · Guía de Clasificación |
| **Arquitectura** | Diseño, decisiones y modelo de datos | Arquitectura del Sistema · Diseño Técnico Detallado · Modelo de Datos (ER) · Confianza Agregada · Sistema de Plugins |
| **Desarrollo** | Cómo está construido e integrado | Diseño Técnico Detallado · Contrato API HTTP · API de Administración · Especificaciones (plugins, GDC, prompts, límite de páginas) · Quickstart · Manuales de Plugins/Validaciones |
| **Operación** | Cómo desplegar, monitorizar y resolver incidencias | Checklists de Despliegue · Infraestructura Real Desplegada · CI/CD · Migraciones de BD · Runbook de Incidentes · Observabilidad (KQL, Monitoreo, CU) · Troubleshooting · Healthcheck |

---

## 1. Negocio y conceptos

Visión funcional del sistema y terminología común.

| Documento | Contenido |
|-----------|-----------|
| [Análisis Funcional](1_negocio/02_ANALISIS_FUNCIONAL.md) | Requisitos funcionales y no funcionales, actores, casos de uso y reglas de negocio del sistema. |
| [Glosario de Términos](1_negocio/GLOSSARIO_TERMINOS.md) | Términos técnicos, acrónimos y conceptos centrales del proyecto. |

---

## 2. Arquitectura y diseño

Diseño técnico del sistema: arquitectura, modelo de datos, contratos, especificaciones de componentes y reglas de confianza.

| Documento | Contenido |
|-----------|-----------|
| [Arquitectura del Sistema](2_arquitectura_y_diseno/01_ARQUITECTURA_SISTEMA.md) | Arquitectura general, componentes, servicios Azure, diagrama de despliegue y decisiones arquitectónicas. |
| [Diseño Técnico Detallado](2_arquitectura_y_diseno/03_DISENO_TECNICO_DETALLADO.md) | Flujos del pipeline, secuencias de Durable Functions, actividades, motor de validación y sistema de plugins. |
| [Modelo de Datos (ER)](2_arquitectura_y_diseno/DATA_MODELS_ER_DIAGRAM.md) | Entidades de base de datos, relaciones, índices y diagrama entidad-relación. |
| [Documentación de API (Admin de Tipologías)](2_arquitectura_y_diseno/15_API_DOCUMENTATION_V1_4.md) | Endpoints CRUD de administración de tipologías, modelos de datos, autenticación y manejo de errores. |
| [Contrato API HTTP](2_arquitectura_y_diseno/CONTRATO_API_HTTP.md) | Contrato de los endpoints de ingestión (`/api/IngestDocument`) y healthcheck: request/response, estados y validaciones. |
| [Confianza Agregada](2_arquitectura_y_diseno/CONFIANZA_AGREGADA.md) | Cálculo de la métrica de confianza (0–1) en clasificación, extracción y validación. |
| [Confianza y Umbral (diagrama)](2_arquitectura_y_diseno/CONFIANZA_UMBRAL.md) | Diagrama de flujo de la confianza agregada a lo largo del pipeline. |
| [Priorización de Umbrales (diagrama)](2_arquitectura_y_diseno/UMBRAL_PRIORIZACION.md) | Jerarquía de resolución de umbrales: petición → tipología → modelo → valor por defecto. |
| [Casuísticas Nota Simple](2_arquitectura_y_diseno/CAUSISTICAS_NOTA_SIMPLE_1_4.md) | Matriz de casos de uso válidos de clasificación y extracción para Nota Simple. |
| [Tipologías de Referencia](2_arquitectura_y_diseno/TIPOLOGIAS_REFERENCIA.md) | Tipologías configuradas: versiones, campos, validación y modelos asociados. |
| [Sistema de Plugins (Extensibilidad)](2_arquitectura_y_diseno/EXTENSIBILIDAD_PLUGIN_SYSTEM.md) | Arquitectura del sistema de plugins, interfaces, contratos y guía para crear un nuevo plugin. |
| [Especificación — Plugin AssetResolver](2_arquitectura_y_diseno/ESPECIFICACION_PLUGIN_ASSETRESOLVER.md) | Búsqueda de activos por IDUFIR, referencia catastral y dirección; contrato de precedencia de criterios. |
| [Especificación — Capa de Servicio GDC (SINTWS)](2_arquitectura_y_diseno/ESPECIFICACION_CAPA_SERVICIO_GDC_SINTWS.md) | Integración SOAP con el gestor documental GDC: operaciones, endpoints, configuración y resiliencia. |
| [Especificación — Límite de Páginas](2_arquitectura_y_diseno/ESPECIFICACION_LIMITE_PAGINAS_DOCUMENTO.md) | Límite configurable de páginas por documento e impacto en los componentes del pipeline. |
| [Especificación — Prompts Configurables](2_arquitectura_y_diseno/ESPECIFICACION_PROMPTS_CONFIGURABLES.md) | Arquitectura de prompts configurables: claves canónicas, placeholders, versionado y modelo de datos. |
| [Plantilla de Plugins (JSON)](2_arquitectura_y_diseno/PLANTILLA_PLUGINS_JSON.md) | Plantillas de configuración de plugins (REST, SOAP, Custom) por tipología. |

---

## 3. Operación y despliegue

Despliegue, infraestructura, observabilidad y procedimientos operativos.

| Documento | Contenido |
|-----------|-----------|
| [Checklists de Despliegue](3_operacion_y_despliegue/08_CHECKLISTS_DESPLIEGUE.md) | Checklists de verificación y control de cambios para el despliegue en Azure/DevOps. |
| [Infraestructura Real Desplegada](3_operacion_y_despliegue/INFRAESTRUCTURA_REAL_DESPLEGADA.md) | Topología real verificada de producción (SRBRGDOCSAIPROD): recursos, app settings y proceso de despliegue. |
| [Detalle de CI/CD](3_operacion_y_despliegue/CI_CD_DEPLOYMENT_DETAILS.md) | Pipelines de Azure DevOps, stages, artefactos, troubleshooting y gestión de secretos. |
| [Estrategia de Migraciones de BD](3_operacion_y_despliegue/DATABASE_MIGRATION_STRATEGY.md) | Procedimientos de migración EF Core en desarrollo, staging y producción. |
| [Gestión de Releases](3_operacion_y_despliegue/RELEASE_MANAGEMENT.md) | Versionado semántico, procedimiento de release y rollback. |
| [Runbook de Incidentes en Producción](3_operacion_y_despliegue/RUNBOOK_INCIDENTES_PRODUCCION.md) | Escenarios de incidentes con diagnóstico, soluciones y vía de escalado. |
| [Observabilidad — Queries KQL](3_operacion_y_despliegue/OBSERVABILIDAD_KQL.md) | Catálogo de queries KQL y alertas recomendadas para diagnóstico y monitoreo. |
| [Monitoreo y Alertas](3_operacion_y_despliegue/MONITOREO_ALERTAS_REAL.md) | Configuración de Application Insights: eventos de telemetría, circuit breaker, retry y métricas. |
| [Rendimiento de Content Understanding](3_operacion_y_despliegue/CU_RENDIMIENTO_INSIGHTS.md) | Interpretación de métricas de rendimiento de Azure Content Understanding. |
| [Optimización de Rendimiento](3_operacion_y_despliegue/PERFORMANCE_TUNING.md) | Puntos de tuning, escenarios de escalado y diagnóstico de rendimiento. |
| [Troubleshooting y Diagnóstico](3_operacion_y_despliegue/TROUBLESHOOTING_DIAGNOSTICO.md) | Árbol de decisión, casos prácticos, queries KQL y scripts de diagnóstico. |
| [Manual de Healthcheck](3_operacion_y_despliegue/MANUAL_HEALTHCHECK.md) | Probes de salud del sistema (componentes, GDC, AssetResolver, proveedores de modelos). |
| [Workbook de rendimiento CU](3_operacion_y_despliegue/workbooks/documentia-cu-performance.workbook.json) | Workbook de Azure Monitor para diagnóstico de rendimiento de Content Understanding. |

---

## 4. Uso y configuración

Uso del sistema, configuración funcional y guías operativas.

| Documento | Contenido |
|-----------|-----------|
| [Manual de Uso y Configuración](4_uso_y_configuracion/05_MANUAL_USO_CONFIGURACION.md) | Guía de uso del sistema, referencia de la API de ingestión y configuración de tipologías, plugins y validación. |
| [Manual de Configuración](4_uso_y_configuracion/MANUAL_CONFIGURACION.md) | Configuración de base de datos, storage, clasificación, extracción, GDC y modelos dinámicos. |
| [Fuente de Verdad de Configuración](4_uso_y_configuracion/FUENTE_VERDAD_CONFIGURACION.md) | Política de configuración: la base de datos como fuente de verdad para tipologías y modelos en runtime. |
| [Guía de Clasificación de Documentos](4_uso_y_configuracion/GUIA_CLASIFICACION_DOCUMENTOS.md) | Guía funcional de clasificación automática: modos, flujos, confianza y uso de la API. |
| [Guía de Extracción con Content Understanding](4_uso_y_configuracion/GUIA_EXTRACCION_AZURE_CONTENT_UNDERSTANDING.md) | Integración de extracción con Azure Content Understanding: configuración, modelos y mapeo de campos. |
| [Manual de Plugins](4_uso_y_configuracion/MANUAL_PLUGINS.md) | Arquitectura de plugins extensibles (REST, SOAP, Custom): configuración y despliegue por tipología. |
| [Manual de Validaciones](4_uso_y_configuracion/MANUAL_VALIDACIONES.md) | Motor de reglas de validación por tipología: severidades y tipos de validadores. |
| [Manual de Deduplicación](4_uso_y_configuracion/MANUAL_DEDUPLICACION.md) | Mecanismo de deduplicación por hash para evitar el reprocesamiento de documentos. |
| [Quickstart para Desarrolladores](4_uso_y_configuracion/QUICKSTART_DESARROLLADORES.md) | Puesta en marcha local del entorno de desarrollo y primera clasificación de extremo a extremo. |
