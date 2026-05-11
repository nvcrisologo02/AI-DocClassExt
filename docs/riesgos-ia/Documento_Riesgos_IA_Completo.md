## Documento para Comité de Riesgos de IA
### Sistema de Clasificación y Extracción Documental con IA (DocumentIA MVP)

### Control documental
- Organización: Sareb
- Sistema: DocumentIA MVP
- Fecha: 28/04/2026
- Versión: 1.0
- Clasificación: Uso interno - Gobierno TI, Compliance y Seguridad

### Nota de alcance
Este documento describe el sistema corporativo de clasificación y extracción documental con IA implementado por Sareb, su modelo de acceso, controles de seguridad y gobernanza operacional para evaluación por el Comité de Riesgos de IA.

## 1. Personas, grupos y sistemas con acceso (ámbito de publicación y control de permisos)
### 1.1 Tipos de usuarios con acceso
- Equipo de desarrollo: mantiene backend, frontend, plugins e integraciones técnicas.
- Equipo de operaciones y DevOps: gestiona despliegues, configuración de entornos y operación técnica.
- Administradores funcionales: gestionan tipologías, reglas y configuración de modelos desde componentes de COMPLETAR_GDC_HTTP_BASIC_USERNAMEistración.
- Analistas de negocio y operación: revisan resultados, validan casos de excepción y trazabilidad funcional.
- Seguridad, compliance y auditoría interna: supervisan cumplimiento, accesos, cambios y evidencias.
- Sistemas corporativos integrados: gestor documental y servicios de datos de activos.

### 1.2 Ámbito de publicación
- Entorno de desarrollo: pruebas técnicas, integración temprana y validación de cambios.
- Entorno de preproducción: validación integrada y pruebas de despliegue previas a producción.
- Entorno de producción: operación real con documentos de negocio y trazabilidad completa.

Ámbito de proyecto y repositorio:
- Repositorio corporativo del sistema (backend, frontend, plugins, scripts y documentación).
- Proyecto corporativo de Azure DevOps para gestión de cambios, pull requests, pipelines y evidencias.

Tipos de acciones por perfil:
- Lectura: analistas, negocio, compliance y auditoría (según necesidad de conocer).
- Escritura: equipos técnicos autorizados, bajo control de rama y revisión.
- Ejecución: pipelines y servicios de runtime en función de rol y entorno.

### 1.3 Control de permisos
- Identidad y acceso centralizados en Entra ID (Azure AD) con grupos por rol.
- RBAC por recurso en Azure (Key Vault, Storage, SQL, App Services, recursos IA).
- Uso de Managed Identity para acceso servicio a servicio, reduciendo credenciales fijas.
- Uso de PIM para elevación temporal de privilegios en operaciones sensibles.
- Segregación de funciones entre desarrollo, despliegue, operación y seguridad.
- Trazabilidad de cambios mediante pull request, aprobaciones y ejecución de pipeline.
- Gestión de secretos en Key Vault y referencias seguras en configuración de aplicaciones.

## 2. Documentación funcional del sistema
### 2.1 Qué hace el sistema y qué problemática resuelve
DocumentIA MVP resuelve la necesidad de automatizar y estandarizar el tratamiento documental en procesos inmobiliarios, reduciendo tiempos y errores de clasificación/extracción manual.

Funciones principales:
- Ingesta de documentos (contenido o referencia documental).
- Clasificación de tipología documental con IA.
- Extracción de campos estructurados con IA.
- Validación automática por motor de reglas configurable.
- Enriquecimiento mediante plugins de integración.
- Archivado en gestor documental corporativo.
- Persistencia de resultados y auditoría técnica/funcional.

### 2.2 Tareas automáticas y semiautomáticas
Automáticas:
- Cálculo de integridad y huellas del documento.
- Control de duplicidad.
- Clasificación y extracción con estrategias de fallback.
- Validación de campos por reglas.
- Integración técnica y persistencia final.

Semiautomáticas:
- Gestión de configuraciones (tipologías, modelos, reglas y umbrales).
- Revisión humana obligatoria de casos de baja confianza o incidencias.

### 2.3 Instrucciones de uso
Canales de uso:
- API HTTP de ingesta y seguimiento asíncrono.
- Frontend operativo para consumo de resultados.
- Frontend COMPLETAR_GDC_HTTP_BASIC_USERNAMEistrativo para configuración funcional y técnica.
- Pipeline CI/CD para despliegues controlados.

Flujo de trabajo típico (A-Z):
1. Entrada: recepción de documento o identificador documental y metadatos de trazabilidad.
2. Proceso: normalización, deduplicación, clasificación, resolución de tipología, extracción, validación, enriquecimiento, archivado y persistencia.
3. Resultado: salida estructurada con estado final, confianza, datos extraídos y trazabilidad.

### 2.4 Limitaciones
Casos no soportados o no objetivo:
- Tipologías no modeladas o no configuradas en el sistema.
- Documentos con calidad insuficiente fuera de umbrales de recuperación.
- Interpretaciones jurídicas complejas sin soporte de reglas explícitas.

Decisiones que no debe tomar de forma autónoma:
- Aprobación jurídica o de negocio final de expedientes críticos.
- Promoción de cambios a producción sin aprobación formal.
- Resolución de incidencias de cumplimiento sin intervención humana.

### 2.5 Ejemplos de casos de uso y resultado esperado
Caso 1: Procesar nota simple registral.
- Resultado esperado: tipología correcta, extracción de campos clave, validación de integridad y salida con trazabilidad.

Caso 2: Procesar documento ya archivado en GDC.
- Resultado esperado: recuperación por referencia, procesamiento sin duplicar archivado y persistencia consistente.

Caso 3: Documento con baja confianza de extracción.
- Resultado esperado: estado de revisión, detalle de errores/alertas y obligación de revisión humana antes del cierre.

## 3. Finalidad y beneficios esperados para Sareb
### 3.1 Finalidad estratégica
- Incrementar calidad y consistencia del procesamiento documental.
- Reducir carga manual en tareas repetitivas.
- Mejorar trazabilidad y capacidad de auditoría en procesos críticos.
- Acelerar ciclo de tratamiento documental en cadena operativa.

### 3.2 Beneficios esperados
Beneficios cualitativos:
- Menor dispersión de criterios de extracción.
- Mayor robustez ante variabilidad documental.
- Mayor control de cambios y de evidencias operativas.

Beneficios medibles esperados:
- Reducción del tiempo medio de procesamiento por documento.
- Menor porcentaje de reprocesos por errores de extracción.
- Menor tasa de incidencias operativas en integraciones documentales.
- Mayor porcentaje de expedientes con trazabilidad completa auditada.

## 4. Fuentes de información y clasificación de datos
### 4.1 Fuentes de información
- Repositorios corporativos de código y configuración.
- APIs internas (gestor documental, servicios de integración y COMPLETAR_GDC_HTTP_BASIC_USERNAMEistración).
- APIs de servicios IA corporativos en Azure.
- Base de datos de resultados, configuración, auditoría y operación.
- Archivos de configuración de tipologías, reglas, modelos y plugins.

### 4.2 Clasificación general de datos
- Datos de negocio: metadatos documentales, tipología, referencias de activo/expediente.
- Datos potencialmente sensibles: contenido documental y campos extraídos según tipología.
- Datos personales (cuando proceda): identificativos presentes en documentación de origen.
- Datos técnicos: logs, métricas, identificadores de ejecución y correlación.
- Datos de seguridad: secretos y credenciales técnicas protegidas en Key Vault.

## 5. Costes asociados
### 5.1 Licencias de herramientas de IA
- Herramientas de productividad/IA de desarrollo: coste estimado medio.
- Servicios de inferencia IA (clasificación/extracción/fallback): coste estimado medio-alto según volumen.

### 5.2 Infraestructura
- Cómputo y hosting (Functions, App Services): coste estimado medio.
- Almacenamiento y base de datos (Storage, SQL): coste estimado medio.
- Observabilidad y seguridad (App Insights, Log Analytics, Key Vault): coste estimado medio.

### 5.3 Desarrollo y mantenimiento
- Coste de implantación inicial: alto.
- Coste de operación evolutiva y soporte: medio.
- Coste de mantenimiento correctivo/preventivo: medio.

## 6. Tools / herramientas que utiliza el sistema
### 6.1 Inventario y uso
1. Azure Functions + Durable Functions.
- Lectura/escritura: sí.
- Ejecución automatizada: sí.
- Criticidad: alta.

2. Azure DevOps (repos, PR, pipelines).
- Lectura/escritura: sí.
- Ejecución automatizada: sí (build/test/deploy).
- Criticidad: alta.

3. Azure Key Vault.
- Lectura/escritura: lectura por servicios; escritura restringida a roles autorizados.
- Ejecución automatizada: sí, por referencias de secretos en runtime.
- Criticidad: alta.

4. Azure Storage y Azure SQL.
- Lectura/escritura: sí.
- Ejecución automatizada: sí.
- Criticidad: alta.

5. Servicios IA Azure (clasificación, extracción y fallback).
- Lectura/escritura: ejecución de inferencia y recepción de resultados.
- Ejecución automatizada: sí.
- Criticidad: alta.

6. Frontend COMPLETAR_GDC_HTTP_BASIC_USERNAMEistrativo y operativo.
- Lectura/escritura: sí, según rol.
- Ejecución automatizada: limitada.
- Criticidad: media-alta.

7. Integraciones y plugins corporativos.
- Lectura/escritura: sí, según tipo de integración.
- Ejecución automatizada: sí, en pipeline funcional.
- Criticidad: alta.

## 7. Instrucciones de uso
### 7.1 Manual breve para usuario típico
1. Preparar entrada documental y metadatos mínimos requeridos.
2. Lanzar procesamiento por canal autorizado y monitorizar estado.
3. Revisar resultado final antes de aceptación operativa.

### 7.2 Revisión obligatoria previa a aceptación
- Coherencia entre tipología detectada y documento.
- Completitud y calidad de campos críticos extraídos.
- Estado de validación y nivel de confianza.
- Evidencias de integración y archivado cuando aplique.

### 7.3 Buenas prácticas y controles de seguridad
- Revisión humana obligatoria en baja confianza o errores de validación.
- Prohibición de cambios automáticos en producción sin aprobación previa.
- Segregación de funciones y mínimo privilegio.
- Trazabilidad completa de cambios, ejecuciones y accesos.
- Rotación y protección de secretos según política corporativa.

### 7.4 Supuestos pendientes de validación por comité
- Confirmar si habrá terceros externos con acceso operativo o solo equipos internos Sareb.
- Confirmar umbrales objetivos de calidad/aceptación por tipología para su formalización en políticas de IA.
