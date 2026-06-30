# Entrega de Documentación — DocumentIA

**Sistema:** DocumentIA — Clasificación y Extracción Automática de Documentos (MVP)
**Fecha de entrega:** 2026-06-30

---

## Resumen ejecutivo

| | |
|---|---|
| **Objetivo del sistema** | Automatizar la clasificación y la extracción de datos estructurados de documentos (principalmente Notas Simples Registrales y tipologías afines), validando los resultados e integrándolos con los sistemas corporativos (GDC, AssetResolver). Reduce el procesamiento manual y normaliza la información documental. |
| **Alcance** | Pipeline de procesamiento de extremo a extremo: ingestión, normalización, deduplicación, clasificación, extracción, validación e integración. Construido sobre Azure (Durable Functions en .NET, Azure AI Document Intelligence, Azure Content Understanding y Azure OpenAI como fallback). La configuración de tipologías, modelos y plugins es dinámica (base de datos). Corresponde al MVP del sistema, desplegado en producción. |
| **Documentos de lectura obligatoria** | 1) [Arquitectura del Sistema](2_arquitectura_y_diseno/01_ARQUITECTURA_SISTEMA.md) · 2) [Análisis Funcional](1_negocio/02_ANALISIS_FUNCIONAL.md) · 3) [Manual de Uso y Configuración](4_uso_y_configuracion/05_MANUAL_USO_CONFIGURACION.md) · 4) [Infraestructura Real Desplegada](3_operacion_y_despliegue/INFRAESTRUCTURA_REAL_DESPLEGADA.md) · 5) [Runbook de Incidentes](3_operacion_y_despliegue/RUNBOOK_INCIDENTES_PRODUCCION.md). |
| **Ruta de navegación recomendada** | (1) Negocio → (2) Arquitectura y diseño → (3) Uso y configuración → (4) Operación y despliegue. Comenzar por el [Índice](00_INDICE.md), que selecciona los documentos por perfil. |

La documentación describe el sistema **tal como está construido y desplegado**. No incluye propuestas, tareas pendientes ni planes de trabajo.

## Cómo navegar

Comience por el **[Índice de Documentación](00_INDICE.md)**, que cataloga todos los documentos organizados por audiencia.

La documentación está estructurada en cuatro bloques:

| Carpeta | Audiencia | Contenido |
|---------|-----------|-----------|
| [`1_negocio/`](1_negocio/) | Negocio, dirección | Análisis funcional y glosario de términos. |
| [`2_arquitectura_y_diseno/`](2_arquitectura_y_diseno/) | Arquitectos, desarrolladores | Arquitectura, diseño técnico, modelo de datos, contratos y especificaciones. |
| [`3_operacion_y_despliegue/`](3_operacion_y_despliegue/) | Operación, DevOps | Despliegue, infraestructura, observabilidad y procedimientos operativos. |
| [`4_uso_y_configuracion/`](4_uso_y_configuracion/) | Usuarios, integradores | Manuales de uso, configuración y guías operativas. |

## Notas de uso

- Todos los enlaces entre documentos son relativos a esta carpeta y se resuelven dentro del propio paquete.
- Las credenciales, cadenas de conexión y claves de los ejemplos están sustituidas por marcadores de posición (`<...>`); los valores reales se gestionan en Azure Key Vault.
- La fuente de verdad de tipologías, modelos y plugins en ejecución es la base de datos; los ficheros de configuración del paquete son referencia y plantilla.
