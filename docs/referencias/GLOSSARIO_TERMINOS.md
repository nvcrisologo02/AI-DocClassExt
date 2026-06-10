# Glosario de Términos — DocumentIA

> **Última actualización:** 2026-06-10  
> **Proyecto:** AI DocClassExt — SAREB  
> **Propósito:** Referencia centralizada de conceptos, términos técnicos y acrónimos del sistema DocumentIA.

---

## A. Términos Técnicos Centrales

### Tipología

Clasificación canónica de un tipo de documento dentro del negocio de SAREB. Cada tipología posee un **código único** (ej: `NOTA_SIMPLE_REGISTRAL`), **nombre legible**, **versión**, **umbrales de confianza**, **modelos asociados** y **configuración JSON**.

**Ejemplo:** "Notas Simples Registrales v1.2", "Tasaciones Inmobiliarias v2.0", "Escrituras Públicas v1.0".

**Ciclo de vida:** Draft → Published → Retired. Solo tipologías Published están activas en pipeline de clasificación. Gestionadas vía API `/management/tipologias`.

**Referencia:** [docs/03_DISENO_TECNICO_DETALLADO.md - Gestionar Tipologias](../03_DISENO_TECNICO_DETALLADO.md)

---

### Clasificación

Proceso automatizado de identificación y etiquetado del tipo de documento (tipología) basado en contenido extraído del PDF. El orquestador ejecuta `ClasificarActivity` contra **proveedores configurables** en orden (flujo configurable + fallback global).

**Retorna:** `TipologiaDetectada` (código) + `Confianza` (float 0-1).

**Referencia:** [docs/03_DISENO_TECNICO_DETALLADO.md - ClasificarActivity](../03_DISENO_TECNICO_DETALLADO.md)

---

### Confianza

Métrica numérica (0.0 - 1.0) que cuantifica el grado de certeza de una decisión (clasificación, extracción, validación). Se agregan confianzas de múltiples proveedores y se comparan contra umbrales configurables.

**Tipos:**
- **Confianza de Clasificación:** Proporcionada por el modelo de clasificación (provider-specific).
- **Confianza Agregada:** Promedio ponderado de confianzas de múltiples providers según prioridad/weight.

**Umbrales:** Configurables por tipología. Si confianza < umbral, el estado resultante es `BAJA_CONFIANZA_CLASIFICACION` y la extracción se omite.

---

### Provider / Plugin

Componente modular que proporciona capacidades especializadas (clasificación, extracción, validación, enriquecimiento).

**Tipos de Provider:**
- **Clasificación:** Azure Content Understanding, Document Intelligence, modelo custom.
- **Extracción:** Document Intelligence, Content Understanding, GPT-4o-mini fallback.
- **Validación:** Reglas JSON, patrones regex, lógica personalizada.
- **Enriquecimiento (Plugins):** AssetResolver, GDC, servicios REST/SOAP externos.

**Configuración:** Almacenada en `ConfiguracionJson` de la BD; activación por tipología.

---

### Orquestación / Orchestrator

Durable Function (Microsoft.DurableTask) que coordina la ejecución secuencial de **14 actividades** del pipeline de procesamiento, con puntos de decisión, reintentos y manejo de fallos.

**Clase principal:** `DocumentProcessOrchestrator` (Durable Orchestration Function).

**Entrada:** `IngestDocumentRequest` (documento en Base64 o `objectIdGDC`).

**Salida:** `ContratoSalida` con resultado final + trazabilidad.

**Timeline de actividades:** Registrado en `customStatus` durante Running para visibilidad en tiempo real.

---

### Activity Function

Función discreta en Durable Functions que ejecuta una unidad de trabajo (ej: normalizar PDF, verificar duplicado, clasificar, extraer datos, validar, integrar plugins).

**Total en pipeline:** 14 actividades.

**Características:** Determinística, idempotente (idealmente), con reintentos configurables.

**Ejemplos:** `NormalizarActivity`, `ClasificarActivity`, `ExtraerActivity`, `ValidarActivity`, `PersistirActivity`.

---

### Extracción

Proceso de obtención de datos estructurados desde un documento procesado. Ejecuta `ExtraerActivity` contra proveedores configurables (Document Intelligence → Content Understanding → GPT-4o fallback).

**Retorna:** `DatosExtraidos` (Dictionary<string, object>) con campos tipología-específicos.

**Diferencia con Clasificación:** Clasificación = tipo de documento; Extracción = contenido del documento.

---

### Hub / Orchestrator Hub

Punto de entrada HTTP (Function) que recibe requests de ingestión de documentos. Se comunica con Durable Functions runtime para iniciar instancias del orquestador.

**Endpoint:** `POST /api/IngestDocument`

**Clase:** `OrquestarDocumentoHttpFunction` (HTTP Trigger Function).

**Retorna:** `statusQueryUri` (URL para polling del estado).

---

### Trazabilidad / Auditoría

Registro completo de decisiones, transformaciones y eventos durante el procesamiento de un documento. Incluye:
- Timeline de actividades ejecutadas con duración.
- Confianzas y umbrales en cada paso.
- Proveedores utilizados y resultados intermedios.
- Errores y fallbacks activados.

**Persistencia:** En tabla `EjecucionesDocumentos` (SQL Server) + logs en Application Insights.

---

### ExecutionLog

Entidad de BD que almacena el historial de todas las ejecuciones de un documento, incluyendo estado final, datos extraídos, metadatos de ejecución y timestamps.

**Tabla:** `EjecucionesDocumentos` en BD central.

**Propósito:** Auditoría, deduplicación, análisis histórico.

---

### Normalización

Primera actividad del pipeline (`NormalizarActivity`). Calcula propiedades canónicas del documento:
- **SHA256, MD5, CRC32:** Hashes para deduplicación.
- **Página count:** Número de páginas del PDF.
- **Metadatos:** Nombre, tamaño, timestamps.

**Propósito:** Preparar documento para verificación de duplicados y subsecuentes procesamiento.

---

### Duplicado

Documento que ya ha sido procesado anteriormente (identificado por SHA256). Si `forceReprocess=false`, se retorna resultado cacheado con flag `ReutilizadaPorDuplicado=true`.

**Verificación:** Realizada por `VerificarDuplicadoActivity` consultando BD por SHA256 (índice único).

---

### Resolver Tipología

Actividad (`ResolverTipologiaActivity`) que traduce un código de tipología detectada a su configuración completa desde BD. Resuelve familia@versión → `TipologiaConfig` (modelos, umbrales, plugins, prompt, reglas validación).

**Entrada:** Código tipología (ej: `NOTA_SIMPLE_REGISTRAL`).

**Salida:** `TipologiaConfig` object con todos los parámetros necesarios para extracción y validación.

---

### Markdown Layout

Extracción de estructura y layout de documento en formato Markdown, preservando jerarquía visual (encabezados, listas, tablas).

**Actividad:** `ExtraerMarkdownLayoutActivity` (opcional, configurable por tipología).

**Provider:** Document Intelligence (Layout model).

**Uso:** Enriquecimiento de datos extraídos, comprensión de estructura documental.

**Nota:** Si `instrucciones.classification.markdown` se proporciona en input (D4), se inyecta directamente sin ejecutar activity.

---

### Validación

Proceso de verificación de datos extraídos contra reglas tipología-específicas (formato, rango, patrones, lógica de negocio).

**Motor:** `ValidationEngine` + JSON rules (11 tipos de validadores).

**Actividad:** `ValidarActivity`.

**Retorna:** `ValidationReport` con campos válidos/inválidos, errores y warnings.

**Referencia:** [docs/manuales/MANUAL_VALIDACIONES.md](../../manuales/MANUAL_VALIDACIONES.md)

---

### AssetResolver

Plugin especializado que enriquece datos extraídos con información de activos desde tabla `DM_POSICION_AAII_TB` (o `DM_POSICION_AACC_TB`).

**Actividad:** `ObtenerActivoActivity`.

**Búsqueda:** Por IDUFIR, referencia catastral o dirección (configurable con AND/OR).

**Retorna:** `IdActivo`, `FCH_ALTA`, `FCH_BAJA`, `DES_SERVICER`, `IND_STATUS` y campos adicionales.

**Referencia:** [docs/especificaciones/ESPECIFICACION_PLUGIN_ASSETRESOLVER.md](../especificaciones/ESPECIFICACION_PLUGIN_ASSETRESOLVER.md)

---

### GDC / Gestor Documental Corporativo

Sistema externo de SAREB (`SINTWS`) que almacena y gestiona documentos clasificados. DocumentIA envía documentos procesados a GDC vía SOAP después de persistir resultados.

**Actividad:** `SubirGDCActivity`.

**Protocolos:** SOAP (`searchEntities`, `create`).

**Timeout:** 120 segundos; si expira, marca `Timeout` pero no bloquea persistencia.

---

### PluginManager / Plugin Integration

Componente que ejecuta plugins de enriquecimiento en orden de prioridad. Cada plugin recibe datos enriquecidos + configuración, retorna resultado enriquecido.

**Actividad:** `IntegrarActivity`.

**Orden:** Configurado por `priority` (menor = más prioritario).

**Fallo critico:** Si plugin con `Priority=1` falla, detiene cadena. Datos parciales se preservan.

---

### Confianza Baja

Estado resultante cuando clasificación retorna confianza < umbral tipología. Marca documento como `BAJA_CONFIANZA_CLASIFICACION`, omite extracción y validación, persiste estado parcial para auditoría.

---

### Blob / Azure Blob Storage

Almacenamiento en la nube (Azure Storage Account) donde se persisten documentos PDF originales después de normalización.

**Container:** `documents/`.

**Actividad:** `SubirBlobActivity`.

**Propósito:** Preservar documento original para auditoría y reprocesamiento.

---

### Fallback

Mecanismo de recuperación cuando un proveedor falla o retorna resultados insatisfactorios. Clasificación: flujo configurable + fallback global. Extracción: CU → DI → GPT-4o-mini. Prompt: GPT-4o-mini por defecto.

**Ejemplo:** Si Document Intelligence no extrae campo crítico, GPT-4o-mini intenta extracción mediante prompt.

---

### ConfiguracionJson

Campo JSON en tablas de BD (Tipologias, ModeloConfigs, TipologiasPlugins) que almacena configuración serializada de tipologías, modelos y plugins.

**Propósito:** Flexibilidad sin cambios de schema; fuente operativa de configuración.

**Nota v1.4+:** Deprecation de columnas legacy; migraciones aplicadas en Fase 3.1.

---

### expectedType

Parámetro opcional en `IngestDocumentRequest` que fuerza una tipología específica, omitiendo clasificación. Si informado, confianza se fija a 1.0.

**Uso:** Testing, casos especiales, pre-clasificación externa.

---

### instanceId

Identificador único (UUID) generado por Durable Functions para cada ejecución del orquestador. Usado para polling de estado via `statusQueryUri`.

---

### customStatus

Campo de la respuesta de Durable Functions que durante Running contiene timeline de actividades con duraciones, estado parcial, actividad actual y cualquier fallback activado. Permite visibilidad en tiempo real del procesamiento.

**Formato:** JSON estructura con `actividadesCompletadas[]`, `duracionTotalMs`, `actividades[]` detalle.

---

### runtimeStatus

Estado global de la ejecución Durable Functions: `Pending`, `Running`, `Completed`, `Failed`, `Terminated`.

---

## B. Acrónimos Comunes

| Acrónimo | Significado | Contexto |
|----------|-------------|---------|
| **CU** | Azure Content Understanding | Provider extracción/clasificación |
| **DI** | Azure Document Intelligence | Provider extracción layout/OCR |
| **GPT** | Generative Pre-trained Transformer | Modelo OpenAI (fallback extracción/prompt) |
| **DF** | Durable Functions | Orquestador Microsoft Azure |
| **GDC** | Gestor Documental Corporativo | Sistema externo SAREB (SINTWS) |
| **SAREB** | Sociedad de Gestión de Activos (cliente principal) | Negocio/dominio |
| **API** | Application Programming Interface | Contratos HTTP REST |
| **REST** | Representational State Transfer | Protocolo comunicación |
| **SOAP** | Simple Object Access Protocol | Protocolo GDC/plugins legacy |
| **JSON** | JavaScript Object Notation | Formato configuración/datos |
| **KQL** | Kusto Query Language | Queries Application Insights |
| **SQL** | Structured Query Language | BD SQL Server |
| **EF** | Entity Framework Core | ORM .NET |
| **ORM** | Object-Relational Mapping | Abstracción BD |
| **ER** | Entity Resolution | Búsqueda/matching datos externos |
| **PDF** | Portable Document Format | Formato entrada documentos |
| **SHA256** | Secure Hash Algorithm 256-bit | Hash criptográfico deduplicación |
| **MD5** | Message Digest 5 | Hash (complementario deduplicación GDC) |
| **CRC32** | Cyclic Redundancy Check 32-bit | Hash checksums |
| **IDUFIR** | ID Unico Finca Registral | Identificador catastral |
| **NIF** | Número de Identidad Fiscal | Identificador persona/empresa |
| **ASGI** | Asynchronous Server Gateway Interface | Web framework Python |
| **HTTP** | Hypertext Transfer Protocol | Protocolo comunicación |
| **BD / DB** | Base de Datos / Database | SQL Server central |
| **MVP** | Minimum Viable Product | Etapa proyecto |
| **v1.4** | Versión 1.4 | Release/schema versión |

---

## C. Conceptos Relacionados & Relaciones

### Jerarquía de Configuración

```
Tipología (familia@version)
  ├─ Modelos AI (clasificación, extracción, prompt, layout)
  ├─ Umbrales Confianza
  ├─ Reglas Validación (JSON)
  ├─ Plugins por Prioridad
  │  ├─ Plugin 1: AssetResolver
  │  ├─ Plugin 2: Enriquecimiento REST
  │  └─ Plugin N: Validación custom
  └─ ConfiguracionJson (flexible)
```

### Flujo de Decisión en Clasificación

```
Documento recibido
  ├─ ¿Duplicado (SHA256) + !forceReprocess? → Retorna cacheado
  ├─ ¿ExpectedType informado? → Omite clasificación (confianza=1.0)
  └─ Clasificar: DefaultFlow + Global Fallback
       └─ ¿Confianza < umbral? → BAJA_CONFIANZA_CLASIFICACION (parar)
       └─ ¿Tipología resoluble? → Continúa extracción
```

### Pipeline de Extracción (Fallback)

```
Documento normalizado
  → Provider 1 (CU/DI primario)
     ├─ Éxito: Retorna datos
     └─ Fallo/insuficiente: Continúa
  → Provider 2 (CU/DI alternativo)
     ├─ Éxito: Retorna datos
     └─ Fallo: Continúa
  → GPT-4o-mini Fallback
     └─ Último recurso (basado en Markdown + prompt)
```

### Actividades Criticas vs Opcionales

**Críticas (siempre ejecutadas):**
1. Normalizar
2. Verificar Duplicado
3. Subir Blob
4. Clasificar
5. Resolver Tipología
6. Persistir

**Condicionales (según configuración tipología):**
- Extraer (si no baja confianza)
- Extraer Markdown Layout
- Prompt
- Validar
- ObtenerActivo (AssetResolver)
- IntegrarPlugins
- SubirGDC

---

## D. Patrones & Buenas Prácticas

### ✅ Patrones Recomendados

| Patrón | Descripción | Beneficio |
|--------|-------------|----------|
| **Cache Duplicados** | Detectar SHA256 duplicados tempranamente, retornar resultado previo. | Ahorro CPU/latencia en reingestas. |
| **Fallback Graceful** | Clasificación con flujos + fallback; extracción con CU→DI→GPT. | Resiliencia ante fallos de providers. |
| **Validación Temprana** | Verificar tipología resoluble antes de extracción. | Evita procesamiento innecesario. |
| **Trazabilidad Completa** | Registrar actividad, duración, confianza, fallbacks en timeline. | Auditoría + debugging facilitado. |
| **Plugins por Prioridad** | Ejecutar plugins críticos primero, opcionales después. | Control de fallo, orden predecible. |
| **ConfiguracionJson Flexible** | Almacenar config en JSON BD en lugar de código. | Cambios sin redeploy. |

### ❌ Antipatrones a Evitar

| Antipatrón | Problema | Solución |
|-----------|---------|----------|
| **Reintentar Clasificación < Umbral** | Desperdicia recursos sin incrementar confianza. | Marcar `BAJA_CONFIANZA_CLASIFICACION` y pausar. |
| **Ignorar Fallback** | Falla total si provider primario está down. | Implementar cascada de providers. |
| **Omitir Deduplicación** | Reprocesar documentos idénticos múltiples veces. | Verificar SHA256 tempranamente. |
| **Config en Código Hardcoded** | Cambios requieren redeploy, versioning complejo. | Usar ConfiguracionJson + admin API. |
| **Plugins Sin Timeout** | Bloquea pipeline indefinidamente. | Establecer timeouts generosos (120s GDC). |
| **Omitir Trazabilidad** | No hay evidencia de decisiones. | Registrar timeline completa. |

---

## E. Referencias Cruzadas

### Documentación Relacionada

| Documento | Propósito | Enlace |
|-----------|----------|--------|
| **Diseño Técnico Detallado** | Arquitectura pipeline, diagrama flujo, actividades. | [03_DISENO_TECNICO_DETALLADO.md](../03_DISENO_TECNICO_DETALLADO.md) |
| **Análisis Funcional** | Casos de uso, actores, CU1-CU6. | [02_ANALISIS_FUNCIONAL.md](../02_ANALISIS_FUNCIONAL.md) |
| **Manual Validaciones** | Especificación motor validación, 11 tipos. | [docs/manuales/MANUAL_VALIDACIONES.md](../../manuales/MANUAL_VALIDACIONES.md) |
| **Manual Plugins** | Arquitectura plugins, integración REST/SOAP. | [docs/manuales/MANUAL_PLUGINS.md](../../manuales/MANUAL_PLUGINS.md) |
| **Especificación AssetResolver** | Detalles búsqueda activos, criterios AND/OR. | [docs/especificaciones/ESPECIFICACION_PLUGIN_ASSETRESOLVER.md](../especificaciones/ESPECIFICACION_PLUGIN_ASSETRESOLVER.md) |
| **Fuente Verdad Configuración** | Autoridad canónica BD config. | [docs/referencias/FUENTE_VERDAD_CONFIGURACION.md](../FUENTE_VERDAD_CONFIGURACION.md) |
| **Plantilla Plugins JSON** | Formato config plugins. | [docs/contratos/PLANTILLA_PLUGINS_JSON.md](../contratos/PLANTILLA_PLUGINS_JSON.md) |
| **Checklists Despliegue** | Validación pre-prod + prod. | [docs/08_CHECKLISTS_DESPLIEGUE.md](../08_CHECKLISTS_DESPLIEGUE.md) |
| **API Documentation v1.4** | Contratos REST endpoints. | [docs/15_API_DOCUMENTATION_V1_4.md](../15_API_DOCUMENTATION_V1_4.md) |

### Endpoints REST Principales

| Caso de Uso | Endpoint | Método | Documentación |
|------------|----------|--------|---------------|
| **CU1: Ingestar Documento** | `/api/IngestDocument` | POST | [02_ANALISIS_FUNCIONAL.md - CU1](../02_ANALISIS_FUNCIONAL.md#cu1-ingestar-documento-para-procesamiento) |
| **CU2: Consultar Estado** | `statusQueryUri` (Durable) | GET | [02_ANALISIS_FUNCIONAL.md - CU2](../02_ANALISIS_FUNCIONAL.md#cu2-consultar-estado-de-procesamiento) |
| **CU3: Gestionar Tipologias** | `/management/tipologias/*` | GET/POST/PUT | [02_ANALISIS_FUNCIONAL.md - CU3](../02_ANALISIS_FUNCIONAL.md#cu3-gestionar-tipologias) |
| **CU4: Gestionar Modelos IA** | `/management/modelos/*` | GET/POST/PUT/DELETE | [02_ANALISIS_FUNCIONAL.md - CU4](../02_ANALISIS_FUNCIONAL.md#cu4-gestionar-modelos-ai) |
| **CU5: Configurar Plugins** | `/management/plugins-tipologias/*` | GET/PUT | [02_ANALISIS_FUNCIONAL.md - CU5](../02_ANALISIS_FUNCIONAL.md#cu5-configurar-plugins-por-tipologia) |

### Componentes Clave en Codebase

| Componente | Ruta | Rol |
|-----------|------|-----|
| **Orchestrator** | `src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs` | Orquestación principal |
| **Activities** | `src/backend/DocumentIA.Functions/Activities/` | 14 activity functions |
| **Tipología Config** | `src/backend/DocumentIA.Core/Models/TipologiaConfig.cs` | Modelo configuración |
| **Validation Engine** | `src/backend/DocumentIA.Core/Validation/ValidationEngine.cs` | Motor validaciones |
| **Plugin Manager** | `src/backend/DocumentIA.Core/Plugins/PluginManager.cs` | Integración plugins |
| **Asset Resolver Plugin** | `src/plugins/DocumentIA.AssetResolver/` | Plugin búsqueda activos |
| **Admin Portal** | `src/frontend/DocumentIA.Admin/` | Gestión tipologias/modelos |

---

## Notas Finales

- Este glosario se mantiene como referencia viva. Cambios en conceptos o introducción de nuevos términos deben documentarse aquí.
- Para detalles profundos de implementación, consultar documentos especializados en la carpeta `docs/` y comentarios en código.
- Las definiciones priorizan claridad sobre exhaustividad; para casos edge o detalles arquitectónicos, remitir a documentación técnica específica.
- Las URLs de referencias asumen estructura de carpetas de DocumentIA MVP; validar rutas antes de usar en otros contextos.

---

**Glosario generado:** 2026-06-10  
**Versión:** 1.0
