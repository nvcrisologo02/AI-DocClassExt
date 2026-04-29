## Resumen Ejecutivo para Comité de Riesgos de IA
### Sistema de Clasificación y Extracción Documental con IA (DocumentIA MVP)

### Control documental
- Organización: Sareb
- Fecha: 28/04/2026
- Versión: 1.0
- Clasificación: Uso interno

## 1. Personas, grupos y sistemas con acceso
### 1.1 Perfiles de acceso
- Desarrollo, Operaciones/DevOps, Administración funcional, Analistas de negocio y Seguridad/Compliance.
- Sistemas corporativos integrados para archivado documental y enriquecimiento de datos.

### 1.2 Ámbito de publicación
- Desarrollo: pruebas técnicas y funcionales.
- Preproducción: validación de cambios y pruebas de despliegue.
- Producción: operación real con trazabilidad completa.

### 1.3 Control de permisos
- Entra ID con grupos por rol y RBAC por recurso.
- Managed Identity para acceso entre servicios.
- PIM para privilegios temporales.
- Pull requests, aprobaciones y pipeline como control de cambio.

## 2. Documentación funcional del sistema
### 2.1 Qué hace
- Clasifica y extrae datos de documentos con IA.
- Valida resultados por reglas configurables.
- Integra y archiva resultados en sistemas corporativos.

### 2.2 Cómo se usa
1. Se envía documento o referencia documental.
2. Se procesa de forma asíncrona con orquestación y validación.
3. Se devuelve resultado estructurado con estado, confianza y trazabilidad.

### 2.3 Limitaciones
- No sustituye decisiones jurídicas o de negocio final.
- No debe promover cambios críticos a producción sin aprobación humana.

### 2.4 Casos de uso representativos
- Nota simple: clasificación y extracción con validación final.
- Documento ya en gestor documental: procesamiento sin re-subida redundante.
- Baja confianza: derivación a revisión humana obligatoria.

## 3. Finalidad y beneficios esperados para Sareb
### 3.1 Finalidad
- Estandarizar y acelerar el tratamiento documental con gobierno y trazabilidad.

### 3.2 Beneficios
- Menor tiempo manual por documento.
- Mayor consistencia de resultados.
- Menor incidencia de errores en procesos críticos.
- Mejor capacidad de auditoría y cumplimiento.

## 4. Fuentes de información y clasificación de datos
### 4.1 Fuentes
- Repositorios corporativos, APIs internas, servicios IA Azure, base de datos de operación y configuración.

### 4.2 Clasificación de datos
- Datos de negocio y metadatos documentales.
- Datos potencialmente sensibles y personales según documento.
- Logs técnicos y trazabilidad operativa.
- Secretos técnicos protegidos en Key Vault.

## 5. Costes asociados
### 5.1 Estimación cualitativa
- Licencias y consumo IA: medio-alto.
- Infraestructura cloud (compute, storage, DB, observabilidad): medio.
- Desarrollo y mantenimiento: alto en implantación, medio en operación estable.

## 6. Tools / herramientas que utiliza el sistema
### 6.1 Herramientas clave y criticidad
- Alta: Azure Functions, Azure DevOps, Key Vault, Storage/SQL, servicios IA.
- Media-alta: frontends COMPLETAR_GDC_HTTP_BASIC_USERNAMEistrativo y operativo.
- Alta: plugins e integraciones con sistemas corporativos.

### 6.2 Tipo de acciones
- Lectura y escritura controlada según rol.
- Automatización en build, test, despliegue y ejecución operativa.

## 7. Instrucciones de uso
### 7.1 Manual breve (1-2-3)
1. Preparar entrada y metadatos.
2. Ejecutar procesamiento por canal autorizado.
3. Revisar resultado, validaciones y confianza antes de aceptar.

### 7.2 Controles mínimos de seguridad
- Revisión humana obligatoria en excepciones.
- Sin cambios automáticos en producción sin aprobación.
- Mínimo privilegio, segregación de funciones y trazabilidad completa.

### 7.3 Pendiente de validación
- Confirmar si habrá acceso operativo para terceros externos o solo equipos internos Sareb.
