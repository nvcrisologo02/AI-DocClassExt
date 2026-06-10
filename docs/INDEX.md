# 📚 Índice Maestro de Documentación — DocumentIA

> **Última actualización:** 2026-06-10  
> **Versión:** 1.5.1
> **Rama:** develop
> **Nuevos:** 6 documentos operacionales críticos agregados

Bienvenido a la documentación centralizada de **DocumentIA**, un sistema de clasificación inteligente de documentos basado en Azure cloud-native. Esta página es tu punto de partida para navegar toda la documentación técnica del proyecto.

---

## 🚀 **Inicio Rápido**

### Para Nuevos Desarrolladores
1. **[QUICKSTART_DESARROLLADORES.md](guias/QUICKSTART_DESARROLLADORES.md)** — Setup en 15 minutos
2. **[GLOSSARIO_TERMINOS.md](referencias/GLOSSARIO_TERMINOS.md)** — Aprende la terminología
3. **[01_ARQUITECTURA_SISTEMA.md](01_ARQUITECTURA_SISTEMA.md)** — Visión general del sistema

### Para Operadores
1. **[08_CHECKLISTS_DESPLIEGUE.md](08_CHECKLISTS_DESPLIEGUE.md)** — Procedimientos de despliegue
2. **[TROUBLESHOOTING_DIAGNOSTICO.md](guias/TROUBLESHOOTING_DIAGNOSTICO.md)** — Resolver problemas rápidamente
3. **[OBSERVABILIDAD_KQL.md](observabilidad/OBSERVABILIDAD_KQL.md)** — Monitoreo y diagnóstico

### Para Arquitectos
1. **[03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md)** — Diseño técnico profundo
2. **[DATA_MODELS_ER_DIAGRAM.md](especificaciones/DATA_MODELS_ER_DIAGRAM.md)** — Schema de BD
3. **[EXTENSIBILIDAD_PLUGIN_SYSTEM.md](guias/EXTENSIBILIDAD_PLUGIN_SYSTEM.md)** — Sistema de plugins

---

## 📖 **Documentación Oficial (Raíz)**

### Documentos Principales

| Documento | Propósito | Audiencia |
|-----------|-----------|-----------|
| **[01_ARQUITECTURA_SISTEMA.md](01_ARQUITECTURA_SISTEMA.md)** | Arquitectura general, componentes, diagrama de despliegue | Todos |
| **[02_ANALISIS_FUNCIONAL.md](02_ANALISIS_FUNCIONAL.md)** | Análisis de requisitos, casos de uso, flujos | PO, Architects |
| **[03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md)** | Diseño técnico profundo, contratos, patterns | Devs, Architects |
| **[05_MANUAL_USO_CONFIGURACION.md](05_MANUAL_USO_CONFIGURACION.md)** | Guía de uso del sistema, configuración | End-users, Devs |
| **[08_CHECKLISTS_DESPLIEGUE.md](08_CHECKLISTS_DESPLIEGUE.md)** | Checklists para despliegue en todos los ambientes | Operators |
| **[15_API_DOCUMENTATION_V1_4.md](15_API_DOCUMENTATION_V1_4.md)** | Documentación completa de endpoints REST | API consumers |

---

## 🎓 **Guías Técnicas (docs/guias/)**

Guías detalladas para tareas específicas:

### Para Nuevos Desarrolladores
- **[QUICKSTART_DESARROLLADORES.md](guias/QUICKSTART_DESARROLLADORES.md)** ⭐ **COMIENZA AQUÍ**
  - Setup local en 15 minutos
  - Primer classification end-to-end
  - Debugging y tips de productividad
  - *Líneas: ~400 | Tiempo: 15 min*

### Para Extensibilidad & Desarrollo
- **[EXTENSIBILIDAD_PLUGIN_SYSTEM.md](guias/EXTENSIBILIDAD_PLUGIN_SYSTEM.md)**
  - Architecture de plugin system
  - Interfaces y contratos reales
  - 6 providers documentados
  - Step-by-step: crear nuevo plugin en 2-3 horas
  - Testing patterns e integración
  - *Líneas: ~1,200 | Tiempo: 1h estudio*

### Para Resolución de Problemas
- **[TROUBLESHOOTING_DIAGNOSTICO.md](guias/TROUBLESHOOTING_DIAGNOSTICO.md)**
  - Decision tree para diagnóstico rápido
  - 6 casos prácticos documentados
  - KQL queries para diagnosis
  - PowerShell troubleshooting scripts
  - Escalation path y SLAs
  - *Líneas: ~860 | Tiempo: 30 min lookup*

### Para Optimización
- **[PERFORMANCE_TUNING.md](guias/PERFORMANCE_TUNING.md)**
  - Benchmarks para 3 ambientes (dev, staging, prod)
  - Identificar cuellos de botella
  - Configuraciones recomendadas por escenario
  - Scaling strategies (vertical, horizontal, queue-based)
  - Troubleshooting de performance
  - *Líneas: ~1,210 | Tiempo: 1h estudio*

---

## 🔍 **Especificaciones & Modelos (docs/especificaciones/)**

### Data Models & Architecture
- **[DATA_MODELS_ER_DIAGRAM.md](especificaciones/DATA_MODELS_ER_DIAGRAM.md)**
  - 12 entidades documentadas con detalles
  - ER Diagram (Mermaid)
  - Relaciones y constraints
  - Versionado y evolución de schema
  - 6 queries SQL comunes
  - *Líneas: ~650 | Tiempo: 30 min study*

### Especificaciones de Componentes
- **[ESPECIFICACION_PLUGIN_ASSETRESOLVER.md](ESPECIFICACION_PLUGIN_ASSETRESOLVER.md)**
  - Plugin específico: AssetResolver
  - Requisitos y comportamiento esperado

---

## 📊 **Observabilidad & Monitoreo (docs/observabilidad/)**

### Guía de Observabilidad
- **[OBSERVABILIDAD_KQL.md](observabilidad/OBSERVABILIDAD_KQL.md)**
  - 20 KQL queries listas para copiar-pegar
  - 8 alertas recomendadas con thresholds
  - Dashboards y Workbooks existentes
  - Debugging avanzado con distributed tracing
  - Casos de estudio: investigar incidentes
  - *Líneas: ~1,000 | Tiempo: 30 min lookup*

### Workbooks & Dashboards
- Documentos en formato `.workbook.json` para Azure Monitor

---

## 📚 **Referencias & Conceptos (docs/referencias/)**

### Glosario
- **[GLOSSARIO_TERMINOS.md](referencias/GLOSSARIO_TERMINOS.md)** ⭐ **LEE PRIMERO si es nuevo**
  - 20 términos técnicos centrales
  - 23 acrónimos del proyecto
  - Conceptos relacionados y relaciones
  - Patrones y antipatrones
  - *Líneas: ~400 | Tiempo: 20 min*

### Otras Referencias
- **[FUENTE_VERDAD_CONFIGURACION.md](referencias/FUENTE_VERDAD_CONFIGURACION.md)**
  - Configuración centralizada
  - Tipologías y modelos
  - Settings por ambiente

---

## 📋 **Contratos & Interfaces (docs/contratos/)**

### API & Integración
- **[CONTRATO_API_HTTP.md](contratos/CONTRATO_API_HTTP.md)**
  - Endpoints REST completos
  - Request/Response schemas
  - Error codes y manejo
  - Ejemplos de uso

- **[PLANTILLA_PLUGINS_JSON.md](contratos/PLANTILLA_PLUGINS_JSON.md)**
  - Template para crear plugins
  - Estructura esperada de configuración

### Manuales
- **[MANUAL_PLUGINS.md](manuales/MANUAL_PLUGINS.md)** — Cómo usar y configurar plugins
- **[MANUAL_VALIDACIONES.md](manuales/MANUAL_VALIDACIONES.md)** — Rules engine de validación

---

## 🚀 **Procedimientos Operacionales Críticos (docs/procedimientos/ — 2026-06-10)**

⭐ **NUEVOS DOCUMENTOS** para control real del sistema en producción:

### Gestión de Incidentes
- **[RUNBOOK_INCIDENTES_PRODUCCION.md](procedimientos/RUNBOOK_INCIDENTES_PRODUCCION.md)** 
  - 7 escenarios de incidentes (latency, errores, circuit breaker, stuck docs, memory leak, access denied)
  - KQL queries para diagnóstico rápido
  - Soluciones inmediatas y permanentes
  - Escalation path y contactos
  - *Líneas: ~337 | Tiempo: 15 min diagnosis*

### Release & Versioning
- **[RELEASE_MANAGEMENT.md](procedimientos/RELEASE_MANAGEMENT.md)**
  - Semantic versioning strategy
  - Pre-release timeline (1 semana)
  - Rollback procedures
  - Hotfix process
  - Pre-release checklist
  - *Líneas: ~263 | Tiempo: 30 min study*

### Migraciones de Base de Datos
- **[DATABASE_MIGRATION_STRATEGY.md](procedimientos/DATABASE_MIGRATION_STRATEGY.md)**
  - EF Core migration workflows
  - Testing local vs staging vs production
  - Blue-green deployment para 0-downtime
  - Rollback procedures
  - Known issues & solutions
  - *Líneas: ~332 | Tiempo: 20 min lookup*

### CI/CD & Deployments
- **[CI_CD_DEPLOYMENT_DETAILS.md](procedimientos/CI_CD_DEPLOYMENT_DETAILS.md)**
  - Pipeline overview (3 pipelines)
  - Build, test, deploy stages
  - Staging vs production deployments
  - Troubleshooting pipeline failures
  - Secrets management
  - *Líneas: ~325 | Tiempo: 25 min lookup*

---

## 🏗️ **Infraestructura & Despliegue (docs/infraestructura/ — 2026-06-10)**

- **[INFRAESTRUCTURA_DESPLIEGUE.md](infraestructura/INFRAESTRUCTURA_DESPLIEGUE.md)** ⭐ **LEER PRIMERO**
  - Topología Azure (staging vs production)
  - Resource groups y servicios por ambiente
  - Capacidad & escalado (límites actuales)
  - Cost breakdown ($926/month) + optimizaciones
  - Networking, Disaster recovery, Backup strategy
  - Monitoring, alertas, compliance
  - *Líneas: ~245 | Tiempo: 30 min overview*

---

## 👥 **Team & Responsabilidades (docs/referencias/ — 2026-06-10)**

- **[TEAM_MATRIX_RESPONSABILIDADES.md](referencias/TEAM_MATRIX_RESPONSABILIDADES.md)**
  - RACI matrix por componente
  - On-call rotation schedule
  - SLA response times (P1/P2/P3)
  - Change approval process
  - Training & onboarding workflows
  - Performance metrics por rol
  - *Líneas: ~188 | Tiempo: 15 min reference*

---

## 🚀 **Procedimientos & Despliegue Legacy (docs/auxiliares/migracion-deployment/)**

### Manuales Operacionales
- **[04_MANUAL_EXPLOTACION.md](auxiliares/migracion-deployment/04_MANUAL_EXPLOTACION.md)**
  - Procedimientos day-to-day
  - Scripts de operación
  - Troubleshooting operacional

### Migración & Deployment
- **[12_MIGRACION_PROMPTGPT_V1_4.md](auxiliares/migracion-deployment/12_MIGRACION_PROMPTGPT_V1_4.md)**
  - Migration guide para upgrading
  - Changelog de versiones

---

## 📈 **Reportes & Auditorías (docs/auxiliares/auditorias/)**

- **09_AUDITORIA_CONFIGURACION_2026-04-30.md** — Auditoría de configuración
- **10_AUDITORIA_DOCUMENTAL_2026-04-30.md** — Auditoría documental
- Otros reportes de auditoría y compliance

---

## 🎯 **Planes & Roadmap (docs/auxiliares/planes/)**

- **11_PLAN_CONFIGURACION_LIMPIA.md** — Plan de limpieza de configuración
- **14_ADO_BACKLOG_PIPELINE_CLASIFICACION_2026-05-21.md** — Backlog del proyecto
- Otros planes de trabajo

---

## 📊 **Visualizaciones & Diagramas (docs/diagrams/)**

- Diagramas de arquitectura (Mermaid, Visio, etc.)
- Flujos de proceso
- Diagramas de despliegue

---

## 🎬 **Presentaciones (docs/presentaciones/)**

- **13_GUION_PRESENTACION_CLASIFICACION_V1_4_SLIDES.md** — Guión de presentación
- Slides y materiales de presentación

---

## 📝 **Ejemplos & Sandbox (docs/auxiliares/ejemplos/)**

- Ejemplos de uso
- JSON samples
- Respuestas esperadas
- Datasets de prueba

---

## 📖 **Reportes de Progreso (docs/auxiliares/phase-reports/)**

- Reportes de fases completadas
- Progress tracking histórico
- Cambios y evolución del proyecto

---

## 🔗 **Mapa Rápido de Referencias Cruzadas**

### Si Necesitas...

| Necesidad | Documento | Ubicación |
|-----------|-----------|-----------|
| **Empezar desde cero** | QUICKSTART | `guias/` |
| **Entender terminología** | GLOSSARIO | `referencias/` |
| **Resolver error rápido** | TROUBLESHOOTING | `guias/` |
| **Ver arquitectura** | ARQUITECTURA_SISTEMA | `docs/` (raíz) |
| **Crear un plugin** | EXTENSIBILIDAD | `guias/` |
| **Ver schema BD** | DATA_MODELS | `especificaciones/` |
| **Monitorear sistema** | OBSERVABILIDAD + KQL | `observabilidad/` |
| **Optimizar performance** | PERFORMANCE_TUNING | `guias/` |
| **API reference** | CONTRATO_API_HTTP | `contratos/` |
| **Desplegar a prod** | CHECKLISTS_DESPLIEGUE | `docs/` (raíz) |
| **🆕 Incidente en producción** | RUNBOOK_INCIDENTES_PRODUCCION | `procedimientos/` |
| **🆕 Infraestructura Azure** | INFRAESTRUCTURA_DESPLIEGUE | `infraestructura/` |
| **🆕 Release & hotfix** | RELEASE_MANAGEMENT | `procedimientos/` |
| **🆕 Migración BD** | DATABASE_MIGRATION_STRATEGY | `procedimientos/` |
| **🆕 CI/CD y pipelines** | CI_CD_DEPLOYMENT_DETAILS | `procedimientos/` |
| **🆕 Responsabilidades equipo** | TEAM_MATRIX_RESPONSABILIDADES | `referencias/` |

---

## 🗂️ **Estructura de Carpetas Completa**

```
docs/
├── 📄 INDEX.md (TÚ ESTÁS AQUÍ)
├── 01_ARQUITECTURA_SISTEMA.md
├── 02_ANALISIS_FUNCIONAL.md
├── 03_DISENO_TECNICO_DETALLADO.md
├── 05_MANUAL_USO_CONFIGURACION.md
├── 08_CHECKLISTS_DESPLIEGUE.md
├── 15_API_DOCUMENTATION_V1_4.md
│
├── 📁 guias/
│   ├── QUICKSTART_DESARROLLADORES.md   ⭐
│   ├── EXTENSIBILIDAD_PLUGIN_SYSTEM.md
│   ├── TROUBLESHOOTING_DIAGNOSTICO.md
│   └── PERFORMANCE_TUNING.md
│
├── 📁 especificaciones/
│   ├── DATA_MODELS_ER_DIAGRAM.md       ⭐
│   └── ESPECIFICACION_PLUGIN_ASSETRESOLVER.md
│
├── 📁 referencias/
│   ├── GLOSSARIO_TERMINOS.md           ⭐
│   ├── FUENTE_VERDAD_CONFIGURACION.md
│   └── TEAM_MATRIX_RESPONSABILIDADES.md (🆕 2026-06-10)
│
├── 📁 observabilidad/
│   ├── OBSERVABILIDAD_KQL.md           ⭐
│   └── CU_RENDIMIENTO_INSIGHTS.md
│
├── 📁 procedimientos/ (🆕 2026-06-10)
│   ├── RUNBOOK_INCIDENTES_PRODUCCION.md
│   ├── RELEASE_MANAGEMENT.md
│   ├── DATABASE_MIGRATION_STRATEGY.md
│   └── CI_CD_DEPLOYMENT_DETAILS.md
│
├── 📁 infraestructura/ (🆕 2026-06-10)
│   └── INFRAESTRUCTURA_DESPLIEGUE.md
│
├── 📁 contratos/
│   ├── CONTRATO_API_HTTP.md
│   └── PLANTILLA_PLUGINS_JSON.md
│
├── 📁 manuales/
│   ├── MANUAL_PLUGINS.md
│   └── MANUAL_VALIDACIONES.md
│
├── 📁 diagrams/
│   └── (Mermaid, Visio, etc.)
│
├── 📁 presentaciones/
│   └── 13_GUION_PRESENTACION_CLASIFICACION_V1_4_SLIDES.md
│
└── 📁 auxiliares/
    ├── migracion-deployment/
    ├── auditorias/
    ├── planes/
    ├── phase-reports/
    ├── ejemplos/
    └── temps/                          ← Archivos temporales (gitignored)
```

---

## 📌 **Documentos Esenciales (Top 5)**

Para estar productivo rápidamente:

1. ✅ **[GLOSSARIO_TERMINOS.md](referencias/GLOSSARIO_TERMINOS.md)** — Aprende qué es qué (20 min)
2. ✅ **[QUICKSTART_DESARROLLADORES.md](guias/QUICKSTART_DESARROLLADORES.md)** — Setup + primer test (15 min)
3. ✅ **[EXTENSIBILIDAD_PLUGIN_SYSTEM.md](guias/EXTENSIBILIDAD_PLUGIN_SYSTEM.md)** — Entiende la arquitectura (1 h)
4. ✅ **[OBSERVABILIDAD_KQL.md](observabilidad/OBSERVABILIDAD_KQL.md)** — Monitorea el sistema (30 min lookup)
5. ✅ **[TROUBLESHOOTING_DIAGNOSTICO.md](guias/TROUBLESHOOTING_DIAGNOSTICO.md)** — Resuelve problemas (30 min lookup)

**Total: ~2.5 horas** para ser productivo = Onboarding acelerado ✅

---

## 🆕 **Documentos Recientemente Agregados (v1.5 — 2026-06-10)**

### Operacionales Críticos (6 documentos nuevos — 2026-06-10) ⭐

| # | Documento | Propósito | Líneas | Commit |
|----|-----------|-----------|--------|--------|
| 1 | RUNBOOK_INCIDENTES_PRODUCCION.md | 7 escenarios de incidentes + diagnostic queries | 337 | `2d3543e` |
| 2 | INFRAESTRUCTURA_DESPLIEGUE.md | Topología Azure, capacidad, DR, compliance | 245 | `6b78895` |
| 3 | RELEASE_MANAGEMENT.md | Versioning, pre-release, rollback, hotfix | 263 | `65907d4` |
| 4 | TEAM_MATRIX_RESPONSABILIDADES.md | RACI, on-call, SLAs, change approval, training | 188 | `058b6c8` |
| 5 | DATABASE_MIGRATION_STRATEGY.md | EF Core, testing, blue-green, rollback | 332 | `5abdfb2` |
| 6 | CI_CD_DEPLOYMENT_DETAILS.md | Pipelines, build, test, deploy, troubleshooting | 325 | `e49009e` |

**Subtotal Operacionales:** 1,690 líneas de documentación crítica ✅

### Técnicas Previas (7 documentos)

| # | Documento | Secciones | Líneas |
|----|-----------|-----------|--------|
| 1 | GLOSSARIO_TERMINOS.md | Términos, Acrónimos, Conceptos, Patrones | ~400 |
| 2 | DATA_MODELS_ER_DIAGRAM.md | ER, Tablas, Relaciones, Queries | ~650 |
| 3 | QUICKSTART_DESARROLLADORES.md | Setup, Estructura, First Run, Debugging | ~400 |
| 4 | EXTENSIBILIDAD_PLUGIN_SYSTEM.md | Architecture, Interfaces, 6 Providers, Testing | ~1,200 |
| 5 | TROUBLESHOOTING_DIAGNOSTICO.md | Decision Tree, 6 Casos, KQL, Escalation | ~860 |
| 6 | OBSERVABILIDAD_KQL.md | 20 Queries, 8 Alerts, Debugging | ~1,000 |
| 7 | PERFORMANCE_TUNING.md | Benchmarks, Config, Scaling | ~1,210 |

**Subtotal Técnicas:** ~5,720 líneas

**TOTAL v1.5:** ~7,410 líneas de documentación nueva ✅

---

## 📞 **Soporte & Preguntas**

- **¿Nuevo en el proyecto?** → Lee [GLOSSARIO](referencias/GLOSSARIO_TERMINOS.md) + [QUICKSTART](guias/QUICKSTART_DESARROLLADORES.md)
- **¿Error en producción?** → Revisa [TROUBLESHOOTING](guias/TROUBLESHOOTING_DIAGNOSTICO.md)
- **¿Cómo monitorear?** → Ve a [OBSERVABILIDAD](observabilidad/OBSERVABILIDAD_KQL.md)
- **¿Crear nuevo plugin?** → Sigue [EXTENSIBILIDAD](guias/EXTENSIBILIDAD_PLUGIN_SYSTEM.md)
- **¿Optimizar performance?** → Lee [PERFORMANCE_TUNING](guias/PERFORMANCE_TUNING.md)

---

## 📅 **Histórico de Versiones**

| Versión | Fecha | Cambios |
|---------|-------|---------|
| **v1.5** | 2026-06-10 | Agregados 7 guías técnicas: Glossario, Data Models, Quickstart, Plugins, Troubleshooting, Observabilidad, Performance |
| v1.4 | 2026-05-21 | ADO Backlog, Pipeline clasificación |
| v1.3 | 2026-04-30 | Auditorías, Checklist despliegue |
| v1.0-1.2 | 2025-2026 | Arquitectura, Diseño, API docs |

---

## ✅ **Checklist: ¿Por Dónde Empiezo?**

- [ ] Soy **nuevo dev** → Ve a [QUICKSTART](guias/QUICKSTART_DESARROLLADORES.md)
- [ ] Soy **operator** → Ve a [TROUBLESHOOTING](guias/TROUBLESHOOTING_DIAGNOSTICO.md)
- [ ] Soy **architect** → Ve a [ARQUITECTURA](01_ARQUITECTURA_SISTEMA.md) + [DATA_MODELS](especificaciones/DATA_MODELS_ER_DIAGRAM.md)
- [ ] Necesito **crear plugin** → Ve a [EXTENSIBILIDAD](guias/EXTENSIBILIDAD_PLUGIN_SYSTEM.md)
- [ ] Necesito **monitorear** → Ve a [OBSERVABILIDAD](observabilidad/OBSERVABILIDAD_KQL.md)
- [ ] Necesito **optimizar** → Ve a [PERFORMANCE_TUNING](guias/PERFORMANCE_TUNING.md)
- [ ] Necesito **resolver error** → Ve a [TROUBLESHOOTING](guias/TROUBLESHOOTING_DIAGNOSTICO.md)

---

**¡Bienvenido a DocumentIA! 🚀**

*Última actualización: 2026-06-10 | Próxima revisión: 2026-07-10*
