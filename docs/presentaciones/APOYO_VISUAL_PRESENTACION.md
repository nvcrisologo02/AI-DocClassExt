# DocumentIA — Apoyo Visual para Presentación
> Documento de soporte: diapositivas / pantallas de apoyo  
> Fecha: abril 2026 | Proyecto: AI DocClassExt — SAREB

Cada sección corresponde a un bloque del guion. Los diagramas están en formato Mermaid (renderizable en VS Code, GitHub, Confluence y herramientas similares).

---

## DIAPOSITIVA 1 — Portada

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│              DocumentIA                                     │
│   Clasificación y extracción automática de documentos       │
│                                                             │
│                      SAREB · Abril 2026                     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## DIAPOSITIVA 2 — El problema: antes del sistema *(Bloque 1)*

```mermaid
flowchart LR
    DOC["📄 Documento\nlega a SAREB"]

    subgraph ANTES["❌  Proceso MANUAL"]
        direction TB
        P1["👤 Operador abre\nel documento"]
        P2["🔍 Identifica el tipo\n'¿nota simple? ¿tasación?'"]
        P3["✍️ Copia datos\na mano"]
        P4["📁 Archiva en GDC\n(si procede)"]
        P1 --> P2 --> P3 --> P4
    end

    subgraph PROBLEMAS["⚠️  Consecuencias"]
        direction TB
        E1["⏱️ Lento\n(minutos por documento)"]
        E2["❌ Errores\n(transcripción manual)"]
        E3["🔇 Sin trazabilidad\n(¿quién hizo qué?)"]
    end

    DOC --> ANTES
    ANTES --> PROBLEMAS
```

---

## DIAPOSITIVA 3 — La solución: el pipeline automático *(Bloque 2)*

```mermaid
flowchart LR
    IN["📄 PDF de entrada\n(cualquier canal)"]

    P1["🔑\n1. Normalización\ny deduplicación"]
    P2["🏷️\n2. Clasificación\nautomática"]
    P3["📋\n3. Extracción\nde datos"]
    P4["✅\n4. Validación\nde reglas"]
    P5["🔗\n5. Enriquecimiento\n(datos externos)"]
    P6["📁\n6. Archivo en GDC"]
    P7["📊\n7. Puntuación\nde confianza"]

    OUT["✔️ Resultado\nestructurado\n+ auditado"]

    IN --> P1 --> P2 --> P3 --> P4 --> P5 --> P6 --> P7 --> OUT

    style P1 fill:#e8f4f8,stroke:#4a90d9
    style P2 fill:#e8f4f8,stroke:#4a90d9
    style P3 fill:#e8f4f8,stroke:#4a90d9
    style P4 fill:#e8f4f8,stroke:#4a90d9
    style P5 fill:#e8f4f8,stroke:#4a90d9
    style P6 fill:#e8f4f8,stroke:#4a90d9
    style P7 fill:#e8f4f8,stroke:#4a90d9
    style IN  fill:#fff3cd,stroke:#f0ad4e
    style OUT fill:#d4edda,stroke:#28a745
```

---

## DIAPOSITIVA 4 — ¿Qué ocurre en clasificación y extracción? *(Bloque 2, pasos 2-3)*

```mermaid
flowchart TB
    DOC["📄 Documento PDF"]

    subgraph CLAS["PASO 2 — Clasificación"]
        M1["🤖 Modelo IA SAREB\n(Document Intelligence)"]
        M2["💬 GPT-4\n(modelo de respaldo)"]
        R_CLAS["Tipo de documento identificado\n+ confianza"]
        M1 -->|"Alta certeza ✅"| R_CLAS
        M1 -->|"Baja certeza ⚠️ → activa fallback"| M2
        M2 --> R_CLAS
    end

    subgraph EXTRAC["PASO 3 — Extracción"]
        E1["🔬 Azure Content Understanding\n(extracción estructurada)"]
        E2["💬 GPT-4\n(completa campos que faltan)"]
        R_EXTRAC["Campos extraídos:\nFinca · Titular · Cargas\nFecha · RefCat · Tasación…"]
        E1 -->|"Campos completos ✅"| R_EXTRAC
        E1 -->|"Campos incompletos ⚠️ → activa fallback"| E2
        E2 --> R_EXTRAC
    end

    DOC --> CLAS --> EXTRAC
```

---

## DIAPOSITIVA 5 — El semáforo de confianza *(Bloque 2, paso 7)*

```mermaid
flowchart LR
    GLOBAL["Confianza global\n= mínimo de las 3 etapas"]

    subgraph CALC["Cómo se calcula"]
        direction TB
        C1["🏷️ Clasificación\n¿con qué certeza identifiqué el tipo?"]
        C2["📋 Extracción\n¿cuántos campos obtuve con fiabilidad?"]
        C3["✅ Validación\n¿cuántas reglas de negocio superé?"]
    end

    subgraph ESTADOS["Resultado final"]
        direction TB
        S1["🟢  OK  ≥ 85%\nProcesamiento automático sin revisión"]
        S2["🟡  REVISIÓN  70–85%\nConviene que alguien lo revise"]
        S3["🔴  ERROR  < 70%\nRequiere intervención humana"]
    end

    CALC --> GLOBAL --> ESTADOS
```

---

## DIAPOSITIVA 6 — Arquitectura funcional (visión de negocio) *(Bloque 3)*

```mermaid
flowchart TB
    subgraph ENTRADAS["Cómo llega el documento"]
        SRC1["📨 Sistema automatizado\n(proceso interno SAREB)"]
    end

    subgraph MOTOR["Motor de procesamiento\n(Azure Functions — SAREB Cloud)"]
        ORCH["🎛️ Orquestador\n13 pasos automáticos"]
    end

    subgraph IA["Servicios de Inteligencia Artificial\n(Microsoft Azure)"]
        direction LR
        AI1["Document Intelligence\nModelos entrenados SAREB"]
        AI2["Content Understanding\nExtracción avanzada"]
        AI3["GPT-4o-mini\nFallback inteligente"]
    end

    subgraph DATOS["Almacenamiento y sistemas"]
        direction LR
        DB["🗄️ Base de datos\n(historial + auditoría)"]
        BLOB["📦 Storage\n(documentos originales)"]
        GDC["📁 GDC SINTWS\n(Gestor Documental)"]
        EXT["🔗 Plugins externos\n(Atlas…)"]
    end

    subgraph RESOLUCION["Resolución de activo"]
        ASSET["🏠 AssetResolver\n¿A qué activo pertenece\neste documento?\n(IDUFIR · RefCat · Dirección)"]
    end

    subgraph ADMIN["Administración"]
        PORTAL["🖥️ Portal Web Admin\n(configuración sin código)"]
    end

    SRC1 --> MOTOR
    MOTOR <-->|"Clasificar / Extraer"| IA
    MOTOR -->|"Archivar"| GDC
    MOTOR -->|"Consultar"| EXT
    MOTOR -->|"Resolver activo"| ASSET
    MOTOR -->|"Guardar resultado"| DB
    MOTOR -->|"Guardar documento"| BLOB
    ADMIN --> MOTOR

    style RESOLUCION fill:#fce4ec,stroke:#c62828

    style MOTOR fill:#e8f4f8,stroke:#4a90d9
    style IA fill:#fff8e1,stroke:#f9a825
    style DATOS fill:#f3e5f5,stroke:#7b1fa2
    style ADMIN fill:#e8f5e9,stroke:#388e3c
```

---

## DIAPOSITIVA 6b — ¿Qué es AssetResolver? *(Bloque 3 — detalle)*

> **Uso recomendado:** mostrar esta diapositiva si el público pregunta "¿cómo sabe el sistema a qué activo pertenece el documento?"

```mermaid
flowchart LR
    DOC["📄 Documento\nprocesado"]

    subgraph EXTRAC["Datos extraídos del documento"]
        D1["IDUFIR\n(identificador registral)"]
        D2["Referencia Catastral"]
        D3["Dirección del inmueble"]
    end

    subgraph AR["🏠 AssetResolver\n(paso 9 del pipeline)"]
        direction TB
        AR1["Busca en la base de datos\nde activos de SAREB\n(DM_POSICION_AAII_TB)"]
        AR2{"¿Activo único\nencontrado?"}
        AR3["✅ Resuelve IdActivo\nse propaga al resto\ndel pipeline"]
        AR4["⚠️ Sin resolución\nel pipeline continúa\nsin IdActivo"]
        AR1 --> AR2
        AR2 -->|"Sí"| AR3
        AR2 -->|"No / múltiples"| AR4
    end

    subgraph CFG["Configurable por tipología"]
        C1["Criterio: IDUFIR\n(más preciso)"]
        C2["Criterio: RefCat\n(complementario)"]
        C3["Criterio: Dirección\n(búsqueda fuzzy)"]
        C4["Modo: AND / OR\nentre criterios"]
    end

    DOC --> EXTRAC --> AR
    CFG -.->|"controla cómo\nse busca"| AR1

    style AR fill:#fce4ec,stroke:#c62828
    style CFG fill:#fff8e1,stroke:#f9a825
```

**Qué aporta:** el sistema no solo extrae datos del documento — también los cruza con la cartera de activos de SAREB para saber automáticamente a qué inmueble corresponde, sin que el operador tenga que buscarlo manualmente.

---

## DIAPOSITIVA 7 — Puntos fuertes (resumen visual) *(Bloque 4)*

```mermaid
mindmap
  root((DocumentIA\nPuntos fuertes))
    🛡️ Fiabilidad
      Fallback automático si falla la IA
      Nunca se pierde un documento
    📊 Transparencia
      Puntuación de confianza por resultado
      Estado OK / REVISIÓN / ERROR visible
    🔁 Eficiencia
      Deduplicación inteligente
      Sin reprocesamiento de duplicados
    ⚙️ Configurabilidad
      Sin necesidad de programar
      Portal de COMPLETAR_GDC_HTTP_BASIC_USERNAMEistración web
    📋 Trazabilidad
      Registro completo de cada ejecución
      Auditable en cualquier momento
    🔗 Integración
      Archivo automático en GDC
      Conexión a sistemas externos via plugins
      Resolución automática de activo (AssetResolver)
    🔒 Seguridad
      Todo en el ecosistema Azure de SAREB
      Secretos en Azure Key Vault
      Los datos nunca salen de SAREB
```

---

## DIAPOSITIVA 8 — Capacidades de expansión *(Bloque 5)*

```mermaid
flowchart LR
    ACTUAL["Sistema actual\n(en producción)"]

    subgraph HOY["Disponible hoy"]
        T1["✅ Notas simples\nregistrales"]
        T2["✅ Motor de validación\n11 tipos de regla"]
        T3["✅ Plugins REST/SOAP\nAtlas · AssetResolver"]
        T4["✅ Portal de\nCOMPLETAR_GDC_HTTP_BASIC_USERNAMEistración web"]
    end

    subgraph PRONTO["En desarrollo"]
        T5["🔧 Reglas cruzadas\nentre campos"]
        T6["🔧 Dashboards operativos\nen tiempo real"]
        T7["🔧 Integración GDC\nretry avanzado"]
    end

    subgraph FUTURO["Planificado"]
        T8["📋 Nuevas tipologías\n(escrituras, contratos…)"]
        T9["📋 Protección GDPR\n(cifrado, enmascaramiento)"]
        T10["📋 Ciclo de vida\nde documentos en Storage"]
    end

    ACTUAL --> HOY
    HOY --> PRONTO
    PRONTO --> FUTURO

    style HOY fill:#d4edda,stroke:#28a745
    style PRONTO fill:#fff3cd,stroke:#f0ad4e
    style FUTURO fill:#e8f4f8,stroke:#4a90d9
```

---

## DIAPOSITIVA 9 — Resumen ejecutivo *(Bloque 6)*

```mermaid
flowchart LR
    subgraph ANTES["❌ Antes"]
        direction TB
        A1["Proceso manual\npor operador"]
        A2["Sin validación\nautomática"]
        A3["Sin trazabilidad"]
        A4["Archivo manual\nen GDC"]
    end

    AHORA["⚡ DocumentIA"]

    subgraph AHORA_LIST["✅ Ahora"]
        direction TB
        B1["Pipeline automático\n13 pasos orquestados"]
        B2["Motor de reglas\nconfigurable"]
        B3["Auditoría completa\npor ejecución"]
        B4["Archivo automático\n+ ObjectID GDC"]
    end

    ANTES -->|"Transformado por"| AHORA
    AHORA --> AHORA_LIST

    style ANTES fill:#fdecea,stroke:#c62828
    style AHORA fill:#fff8e1,stroke:#f9a825
    style AHORA_LIST fill:#d4edda,stroke:#28a745
```

---

---

# Esquema Técnico de Infraestructura

> Destinado al bloque 3 de la presentación o como referencia técnica adjunta.

---

## Infraestructura en producción (Azure SAREB)

```mermaid
flowchart TB
    subgraph CLIENTES["Clientes / Entradas"]
        API_EXT["Sistema Cliente API\n(automatización interna)"]
        ADMIN_FE["Portal Admin Web\nsrbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai\n(Blazor Server)"]
    end

    subgraph FUNC["Azure Functions — srbappprodocai\n(.NET 8 Isolated · Durable Functions v4)"]
        direction TB
        TRIGGER["HTTP Trigger\nPOST /api/IngestDocument\nGET/PUT /management/*"]
        ORCH["Orquestador\nDocumentProcessOrchestrator"]

        subgraph ACTIVITIES["Cadena de actividades (13 pasos)"]
            direction LR
            A1["Normalizar"]
            A2["VerificarDuplicado"]
            A3["SubirBlob"]
            A4["Clasificar"]
            A5["ResolverTipología"]
            A6["Extraer"]
            A7["Prompt"]
            A8["Validar"]
            A9["ObtenerActivo"]
            A10["Integrar\n(plugins)"]
            A11["SubirGDC"]
            A12["Persistir"]
            A1 --> A2 --> A3 --> A4 --> A5 --> A6 --> A7 --> A8 --> A9 --> A10 --> A11 --> A12
        end

        TRIGGER --> ORCH --> ACTIVITIES
    end

    subgraph AI_AZURE["Servicios Azure AI"]
        DI["Azure Document Intelligence\nsrbdiprodocai\nModelos custom clasificación/extracción"]
        CU["Azure Content Understanding\nSweden Central\nExtracción estructurada avanzada"]
        OPENAI["Azure OpenAI\ngpt-4o-mini\nFallback clasificación + extracción + prompt"]
    end

    subgraph STORAGE["Almacenamiento (Azure SAREB)"]
        BLOB["Azure Blob Storage\nsrbstgprodocai\nPDFs originales + markdown sidecar"]
        SQL["SQL Server 2022\nsrbsqlprodocai\nEjecuciones · Validaciones · Auditoría · Tipologías"]
    end

    subgraph EXTERNOS["Sistemas Externos SAREB"]
        GDC["GDC SINTWS\nsrbwidd03.sareb.srb:8090\nGestor Documental Corporativo (SOAP)"]
        ATLAS["Plugins REST/SOAP\nAtlas · otros"]
        ASSET_RES["AssetResolver\nASP.NET Core API\n(Azure App Service)\nResolución activo\nIDUFIR · RefCat · Dirección\nconsulta DM_POSICION_AAII_TB"]
    end

    subgraph OBS["Observabilidad y Seguridad"]
        APPINS["Application Insights\nsrbappiprodocai\nMétricas · Trazas · Alertas"]
        KV["Azure Key Vault\nsrbkvprodocai\nSecretos · Connection strings · API Keys"]
        MI["Managed Identity\nsrbappprodocai\nAcceso sin credenciales a Storage · SQL · KV"]
    end

    API_EXT -->|"HTTPS"| TRIGGER
    ADMIN_FE -->|"HTTPS"| TRIGGER

    A4 -->|"Clasificar"| DI
    A4 -.->|"Fallback GPT"| OPENAI
    A6 -->|"Extraer campos"| CU
    A6 -.->|"Fallback GPT"| OPENAI
    A7 -->|"Prompt libre"| OPENAI

    A3 -->|"Upload PDF"| BLOB
    A2 -->|"SHA256 lookup"| SQL
    A12 -->|"EF Core"| SQL

    A9  -->|"HTTP REST"| ASSET_RES
    ASSET_RES -->|"Consulta SQL\nDM_POSICION_AAII_TB"| SQL
    A10 -->|"HTTP/SOAP"| ATLAS
    A11 -->|"SOAP SubirDocumento"| GDC

    FUNC -->|"Telemetría"| APPINS
    FUNC -.->|"Secretos"| KV
    MI -.->|"RBAC"| BLOB
    MI -.->|"RBAC"| SQL
    MI -.->|"RBAC"| KV

    style FUNC fill:#e8f4f8,stroke:#1565c0
    style AI_AZURE fill:#fff8e1,stroke:#f57f17
    style STORAGE fill:#f3e5f5,stroke:#6a1b9a
    style EXTERNOS fill:#fce4ec,stroke:#880e4f
    style OBS fill:#e8f5e9,stroke:#2e7d32
    style CLIENTES fill:#fafafa,stroke:#757575
```

---

## Flujo de seguridad y datos

```mermaid
flowchart LR
    subgraph PERIMETRO["Perímetro SAREB (Azure Tenant)"]
        direction TB

        subgraph ACCESO["Control de acceso"]
            KV2["Azure Key Vault\nTodos los secretos\ncentralizados aquí"]
            MI2["Managed Identity\nSin contraseñas en código\nni en configuración"]
        end

        subgraph PROCESO["Procesamiento"]
            FUNC2["Azure Functions\nMotor de procesamiento"]
        end

        subgraph DATOS2["Datos"]
            SQL2["SQL Server\nResultados + auditoría"]
            BLOB2["Blob Storage\nDocumentos cifrados en tránsito (HTTPS)"]
        end
    end

    DOC_IN["📄 Documento PDF\n(entrada)"]
    GDC2["GDC SINTWS\n(red interna SAREB)"]

    DOC_IN -->|"HTTPS\nTLS 1.2+"| FUNC2
    KV2 -->|"Referencias @Microsoft.KeyVault(…)"| FUNC2
    MI2 -->|"Token OAuth2\nsin credenciales estáticas"| FUNC2
    FUNC2 --> SQL2
    FUNC2 --> BLOB2
    FUNC2 -->|"SOAP\nred interna"| GDC2

    note1["✅ El documento nunca\nsale del ecosistema SAREB"]

    style PERIMETRO fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px
    style ACCESO fill:#fff8e1,stroke:#f9a825
    style PROCESO fill:#e8f4f8,stroke:#1565c0
    style DATOS2 fill:#f3e5f5,stroke:#6a1b9a
```

---

## Componentes del sistema (vista de módulos)

```mermaid
flowchart TB
    subgraph BACKEND["Backend — DocumentIA.Functions"]
        CORE["DocumentIA.Core\nLógica de negocio\nOrquestador · Actividades · Proveedores IA"]
        DATA["DocumentIA.Data\nAcceso a datos\nEF Core 8 · Entidades · Migraciones"]
        PLUGINS_LIB["DocumentIA.Plugins\nMotor de plugins\nREST · SOAP · DLL custom"]
    end

    subgraph FRONTEND_ADMIN["Frontend — Administración"]
        BLAZOR["DocumentIA.Admin\nBlazor Server .NET 8\nGestión tipologías · Modelos · Plugins"]
    end

    subgraph ENRICHMENTS["Enriquecimiento externo"]
        SAREB_ENR["SarebEnrichments.dll\nPlugin custom\ncargado dinámicamente"]
        ASSET_ENR["DocumentIA.AssetResolver\nASP.NET Core API (.NET 8)\nAzure App Service\nResolución activo por IDUFIR/RefCat/Dirección"]
    end

    CORE --> DATA
    PLUGINS_LIB --> DATA
    BACKEND -->|"REST API"| BLAZOR
    BACKEND -.->|"Carga dinámica DLL"| SAREB_ENR
    BACKEND -.->|"HTTP REST"| ASSET_ENR

    style BACKEND fill:#e8f4f8,stroke:#1565c0
    style FRONTEND_ADMIN fill:#e8f5e9,stroke:#2e7d32
    style ENRICHMENTS fill:#fff8e1,stroke:#f57f17
```

---

## Estado de implementación por componente

```mermaid
pie title Estado de implementación (abril 2026)
    "Completado ✅" : 65
    "En desarrollo 🔧" : 25
    "Planificado 📋" : 10
```

| Componente | Estado |
|---|---|
| Motor de orquestación (13 actividades) | ✅ Completado |
| Clasificación IA + fallback GPT | ✅ Completado |
| Extracción CU + fallback GPT | ✅ Completado |
| Preproceso markdown (layout) | ✅ Completado |
| Persistencia y auditoría (SQL) | ✅ Completado |
| Integración GDC (subir + consultar) | ✅ Completado |
| AssetResolver (IDUFIR · RefCat · Dirección) | ✅ Completado |
| Portal Admin Blazor (CRUD básico) | ✅ Completado |
| CI/CD Azure DevOps + migraciones automáticas | ✅ Completado |
| Infraestructura producción (SQL, KV, MI, Storage) | ✅ Completado |
| Motor de validación (11 tipos de regla) | 🔧 88% — reglas cruzadas pendientes |
| Configuración tipologías (versionado avanzado) | 🔧 80% — import/export pendiente |
| Observabilidad (dashboards App Insights) | 🔧 65% — alertas productivas pendientes |
| GDC retry avanzado + idempotencia | 🔧 80% — Polly pendiente |
| Protección GDPR (cifrado, masking PII) | 📋 Planificado |
| Ciclo de vida Blob (limpieza automática) | 📋 Planificado |

---

---

# Diagrama de Infraestructura Azure — Recursos y Relaciones

> Referencia técnica: todos los recursos Azure en producción, su tipo, y cómo se comunican entre sí.

---

## Mapa completo de recursos Azure (rg-documentia-mvp · West Europe)

```mermaid
flowchart TB
    subgraph SAREB_NET["Red / Ecosistema SAREB"]

        subgraph RG["Resource Group: rg-documentia-mvp  ·  West Europe"]

            subgraph COMPUTE["Cómputo (App Service Plan: srbspprodocai)"]
                FUNCAPP["⚙️ srbappprodocai\nFunction App\n.NET 8 Isolated\nDurable Functions v4\n─────────────────\nOrquestador + 13 Activities\nHTTP Trigger API\nAdmin Management API"]
                ADMINWEB["🖥️ srbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai\nApp Service (Web App)\nBlazor Server .NET 8\n─────────────────\nPortal de COMPLETAR_GDC_HTTP_BASIC_USERNAMEistración\nGestión tipologías · Modelos\nPlugins · Configuración"]
                ASSETWEB["🏠 AssetResolver Web App\nApp Service (.NET 8)\nASP.NET Core API\n─────────────────\nPOST /api/assets/GetAAIIInfo\nBúsqueda IDUFIR · RefCat\nDirección fuzzy\nAuth: X-Api-Key"]
            end

            subgraph STORAGE_GRP["Almacenamiento"]
                STG_DOCS["📦 srbstgprodocai\nStorage Account\n─────────────────\nBlob: PDFs originales\nBlob: markdown sidecar\n(.sha256.md)"]
                STG_JOBS["📦 srbstgproapppdocai\nStorage Account\n─────────────────\nAzureWebJobsStorage\n(estado Durable Functions\norquestación · historial)"]
                SQL["🗄️ srbsqlprodocai\nAzure SQL Server\nDatabase: DocumentIA\n─────────────────\nEjecuciones · Tipologías\nModelos · Validaciones\nAuditoría · Duplicados\n(EF Core 8 · migraciones auto)"]
            end

            subgraph AI_GRP["Inteligencia Artificial"]
                DI["🤖 srbdiprodocai\nDocument Intelligence\nWest Europe\n─────────────────\nModelos custom SAREB\nClasificación · Extracción\nLayout markdown"]
                OPENAI["💬 srboaiprodocai\nAzure OpenAI\nWest Europe\nDeployment: gpt-4o-mini\n─────────────────\nFallback clasificación\nFallback extracción\nPrompt libre"]
                CU["🔬 upe48-mm2avmdm-swedencentral\nAzure AI Foundry\nContent Understanding\nSweden Central\n─────────────────\nExtracción estructurada\navanzada (proveedor\nprincipal extracción)"]
            end

            subgraph OBS_GRP["Observabilidad"]
                APPINS["📊 srbappiprodocai\nApplication Insights\n─────────────────\nMétricas · Trazas\nLogs · Excepciones\nDuración por actividad"]
                LAW["📋 srblawprodocai\nLog Analytics Workspace\n─────────────────\nBackend de AppInsights\nRetención de logs\nKQL queries"]
            end

            subgraph SEC_GRP["Seguridad"]
                KV["🔑 srbkvprodocai\nAzure Key Vault\n─────────────────\nSqlConnectionString\nAPIKeys DI · CU · OpenAI\nGDC credentials\nAssetResolver API Key\nConnection strings Storage"]
                MI["🪪 srbappprodocai\nManaged Identity\n(System Assigned)\n─────────────────\nAcceso sin credenciales\na Storage · SQL · KV"]
            end

        end

        subgraph SAREB_ON["Sistemas On-Premise / Red Interna SAREB"]
            GDC["📁 GDC SINTWS\nsrbwidd03.sareb.srb:8090\nGestor Documental\nCorporativo\n─────────────────\nSubirDocumento (SOAP)\nConsultarDocumento (SOAP)\nObjectID registro"]
            SQL_SAREB["🗄️ BD SAREB\n(DM_POSICION_AAII_TB)\nBase datos activos\ninmobiliarios SAREB\n─────────────────\nIDUFIR · RefCat\nDirección · IdActivo"]
        end

    end

    subgraph CLIENTES["Clientes (dentro de SAREB)"]
        SYS_CLIENTE["📨 Sistema automatizado\n(proceso interno SAREB)"]
        ADMIN_USER["👤 Administrador\n(equipo técnico/negocio)"]
    end

    %% --- Flujos de entrada ---
    SYS_CLIENTE -->|"HTTPS · POST /api/IngestDocument\nGET statusQueryUri (polling)"| FUNCAPP
    ADMIN_USER -->|"HTTPS · navegador"| ADMINWEB
    ADMINWEB -->|"HTTPS · REST API /management/*"| FUNCAPP

    %% --- Function App → Compute ---
    FUNCAPP -->|"HTTPS · POST /api/assets/GetAAIIInfo\nHeader: X-Api-Key"| ASSETWEB

    %% --- Function App → Storage ---
    FUNCAPP -->|"Upload PDF + .md sidecar\n(Blob SDK, MI RBAC)"| STG_DOCS
    FUNCAPP -->|"Estado orquestación\n(AzureWebJobsStorage)"| STG_JOBS
    FUNCAPP -->|"EF Core · leer/escribir\nejecuciones · auditoría"| SQL

    %% --- Function App → AI ---
    FUNCAPP -->|"Clasificar (custom model)\nExtraer Layout markdown\nHTTPS · API Key (KV ref)"| DI
    FUNCAPP -->|"Extraer campos\nHTTPS · API Key (KV ref)"| CU
    FUNCAPP -->|"Fallback clasificación\nFallback extracción\nPrompt libre\nHTTPS · API Key (KV ref)"| OPENAI

    %% --- Function App → Externos ---
    FUNCAPP -->|"SOAP SubirDocumento\nred interna SAREB"| GDC

    %% --- AssetResolver → BD SAREB ---
    ASSETWEB -->|"SQL · lectura DM_POSICION_AAII_TB\n(connection string AssetResolverDb)"| SQL_SAREB

    %% --- Observabilidad ---
    FUNCAPP -->|"Telemetría automática\nSDK AppInsights"| APPINS
    ADMINWEB -->|"Telemetría"| APPINS
    ASSETWEB -->|"Telemetría"| APPINS
    APPINS -->|"Almacena logs"| LAW

    %% --- Seguridad ---
    KV -.->|"@Microsoft.KeyVault(...)\nApp Settings references"| FUNCAPP
    KV -.->|"Secrets"| ADMINWEB
    KV -.->|"Secrets"| ASSETWEB
    MI -.->|"RBAC: Blob Data Contributor"| STG_DOCS
    MI -.->|"RBAC: Blob Owner + Queue + Table"| STG_JOBS
    MI -.->|"RBAC: Key Vault Secrets User"| KV
    MI -.->|"db_datareader / db_datawriter"| SQL

    %% --- Estilos ---
    style COMPUTE fill:#e8f4f8,stroke:#1565c0
    style STORAGE_GRP fill:#f3e5f5,stroke:#6a1b9a
    style AI_GRP fill:#fff8e1,stroke:#f57f17
    style OBS_GRP fill:#e8f5e9,stroke:#2e7d32
    style SEC_GRP fill:#fce4ec,stroke:#c62828
    style SAREB_ON fill:#fafafa,stroke:#757575,stroke-dasharray:5 5
    style CLIENTES fill:#fafafa,stroke:#9e9e9e
    style RG fill:#f9f9f9,stroke:#1565c0,stroke-width:2px
```

---

## Flujo de datos por tipo de comunicación

```mermaid
flowchart LR
    subgraph TIPOS["Leyenda de comunicaciones"]
        direction TB
        T1["─── HTTPS / REST\n(comunicación principal entre servicios)"]
        T2["─── SOAP / XML\n(integración GDC on-premise)"]
        T3["─── SQL / EF Core\n(lectura y escritura de datos)"]
        T4["··· Managed Identity / RBAC\n(acceso seguro sin contraseñas)"]
        T5["··· Key Vault references\n(secretos inyectados en App Settings)"]
    end
```

| Conexión | Protocolo | Autenticación |
|---|---|---|
| `srbappprodocai` → `srbdiprodocai` | HTTPS | API Key (en KV) |
| `srbappprodocai` → `srboaiprodocai` | HTTPS | API Key (en KV) |
| `srbappprodocai` → CU Sweden Central | HTTPS | API Key (en KV) |
| `srbappprodocai` → `srbstgprodocai` | Blob SDK | Managed Identity (RBAC) |
| `srbappprodocai` → `srbstgproapppdocai` | Blob/Queue/Table SDK | Managed Identity (RBAC) |
| `srbappprodocai` → `srbsqlprodocai` | SQL / EF Core | Managed Identity (Entra) |
| `srbappprodocai` → `srbkvprodocai` | HTTPS | Managed Identity (RBAC) |
| `srbappprodocai` → AssetResolver Web App | HTTPS | X-Api-Key (en KV) |
| `srbappprodocai` → GDC SINTWS | SOAP sobre HTTP | Usuario/contraseña SAREB |
| AssetResolver Web App → BD SAREB | SQL Server | Connection string (`AssetResolverDb`) |
| `srbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai` → `srbappprodocai` | HTTPS | Function Key |
| Todos los servicios → `srbappiprodocai` | HTTPS | Connection string AppInsights |

---

## Tabla de recursos Azure (referencia rápida)

| Recurso | Tipo | Región | Propósito |
|---|---|---|---|
| `srbappprodocai` | Function App | West Europe | Motor principal: orquestador + 13 actividades + API |
| `srbwebCOMPLETAR_GDC_HTTP_BASIC_USERNAMEprodocai` | App Service (Web App) | West Europe | Portal de COMPLETAR_GDC_HTTP_BASIC_USERNAMEistración (Blazor Server) |
| AssetResolver Web App | App Service (Web App) | West Europe | Plugin resolución de activo (ASP.NET Core API) |
| `srbspprodocai` | App Service Plan | West Europe | Plan de hosting compartido (Function App + Web Apps) |
| `srbstgprodocai` | Storage Account | West Europe | Documentos PDF + markdown sidecar |
| `srbstgproapppdocai` | Storage Account | West Europe | Estado interno Durable Functions |
| `srbsqlprodocai` | Azure SQL Server/DB | West Europe | Base de datos DocumentIA (EF Core) |
| `srbdiprodocai` | Document Intelligence | West Europe | Clasificación y extracción (modelos custom SAREB) |
| `srboaiprodocai` | Azure OpenAI | West Europe | GPT-4o-mini (fallback + prompt) |
| `upe48-mm2avmdm-swedencentral` | Azure AI Foundry / CU | Sweden Central | Content Understanding (extracción primaria) |
| `srbkvprodocai` | Key Vault | West Europe | Secretos, API Keys, connection strings |
| `srbappiprodocai` | Application Insights | West Europe | Métricas, trazas, logs de todos los servicios |
| `srblawprodocai` | Log Analytics Workspace | West Europe | Backend de Application Insights |
| GDC SINTWS (on-premise) | SOAP Service | Red interna SAREB | Gestor Documental Corporativo |
| BD SAREB / `DM_POSICION_AAII_TB` | SQL Server (on-premise) | Red interna SAREB | Tabla maestra de activos inmobiliarios |
