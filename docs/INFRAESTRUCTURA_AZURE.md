# DocumentIA — Infraestructura Azure
> Resource Group: **SRBRGDOCSAIPROD** · West Europe | Abril 2026

---

```mermaid
flowchart TB

    subgraph CLIENTES["👥 Clientes"]
        SYS["📨 Sistema cliente\n(proceso SAREB)"]
        USR["👤 Administrador"]
    end

    subgraph AZURE["☁️ Azure — SRBRGDOCSAIPROD"]

        subgraph COMPUTE["⚙️ Cómputo  ·  App Service Plan: srbspprodocai"]
            FUNC["srbappprodocai\nFunction App · .NET 8\nDurable Functions v4"]
            ADMIN["srbwebAdminprodocai\nApp Service\nBlazor Server .NET 8"]
            ASSET["AssetResolver\nApp Service · .NET 8\nASP.NET Core API"]
        end

        subgraph DATA["💾 Datos"]
            SQL["srbsqlprodocai\nAzure SQL\nDB: DocumentIA"]
            BLOB["srbstgprodocai\nStorage Account\nBlobs PDF + markdown"]
            JOBS["srbstgproapppdocai\nStorage Account\nDurable Functions state"]
        end

        subgraph AI["🤖 Inteligencia Artificial"]
            DI["srbdiprodocai\nDocument Intelligence\nModelos custom SAREB"]
            CU["Content Understanding\nAzure AI Foundry\nSweden Central"]
            GPT["srboaiprodocai\nAzure OpenAI\ngpt-4o-mini  fallback"]
        end

        subgraph SEC["🔒 Seguridad"]
            KV["srbkvprodocai\nKey Vault\nSecretos · API Keys"]
            MI["Managed Identity\nSystem Assigned\nsin credenciales"]
        end

        subgraph OBS["📊 Observabilidad"]
            AI2["srbappiprodocai\nApplication Insights"]
            LAW["srblawprodocai\nLog Analytics Workspace"]
        end

    end

    subgraph ONPREM["🏢 On-Premise SAREB"]
        GDC["GDC SINTWS\nsrbwidd03:8090\nGestor Documental SOAP"]
        BDSA["BD SAREB\nDM_POSICION_AAII_TB\nActivos inmobiliarios"]
    end

    %% Entradas
    SYS -->|"HTTPS  POST /api/IngestDocument"| FUNC
    USR  -->|"HTTPS  navegador"| ADMIN
    ADMIN -->|"HTTPS  /management/*"| FUNC

    %% Cómputo interno
    FUNC -->|"HTTPS  X-Api-Key"| ASSET

    %% Datos
    FUNC -->|"Blob SDK  MI"| BLOB
    FUNC -->|"AzureWebJobsStorage"| JOBS
    FUNC -->|"EF Core"| SQL

    %% IA
    FUNC -->|"HTTPS  API Key"| DI
    FUNC -->|"HTTPS  API Key"| CU
    FUNC -->|"HTTPS  API Key"| GPT

    %% On-premise
    FUNC  -->|"SOAP  red interna"| GDC
    ASSET -->|"SQL  AssetResolverDb"| BDSA

    %% Observabilidad
    FUNC  -->|"telemetría"| AI2
    ADMIN -->|"telemetría"| AI2
    ASSET -->|"telemetría"| AI2
    AI2   -->|"backend"| LAW

    %% Seguridad (punteado)
    KV -.->|"@KeyVault refs"| FUNC
    KV -.->|"secrets"| ADMIN
    KV -.->|"secrets"| ASSET
    MI -.->|"RBAC  Blob"| BLOB
    MI -.->|"RBAC  Blob/Queue"| JOBS
    MI -.->|"RBAC  Secrets"| KV
    MI -.->|"db_datareader/writer"| SQL

    %% Estilos
    style COMPUTE  fill:#e8f4f8,stroke:#1565c0
    style DATA     fill:#f3e5f5,stroke:#6a1b9a
    style AI       fill:#fff8e1,stroke:#f57f17
    style SEC      fill:#fce4ec,stroke:#c62828
    style OBS      fill:#e8f5e9,stroke:#2e7d32
    style ONPREM   fill:#f5f5f5,stroke:#757575,stroke-dasharray:5 5
    style CLIENTES fill:#f5f5f5,stroke:#9e9e9e
    style AZURE    fill:#fafcff,stroke:#1565c0,stroke-width:2px
```

---

## Comunicaciones

| Origen | Destino | Protocolo | Autenticación |
|---|---|---|---|
| Sistema cliente | `srbappprodocai` | HTTPS | Function Key |
| `srbappprodocai` | `srbdiprodocai` | HTTPS | API Key (KV) |
| `srbappprodocai` | Content Understanding | HTTPS | API Key (KV) |
| `srbappprodocai` | `srboaiprodocai` | HTTPS | API Key (KV) |
| `srbappprodocai` | `srbstgprodocai` | Blob SDK | Managed Identity |
| `srbappprodocai` | `srbstgproapppdocai` | Blob/Queue/Table SDK | Managed Identity |
| `srbappprodocai` | `srbsqlprodocai` | EF Core / SQL | Managed Identity |
| `srbappprodocai` | `srbkvprodocai` | HTTPS | Managed Identity |
| `srbappprodocai` | AssetResolver | HTTPS | X-Api-Key (KV) |
| `srbappprodocai` | GDC SINTWS | SOAP/HTTP | Usuario/pwd SAREB |
| AssetResolver | BD SAREB | SQL Server | Connection string |
| `srbwebAdminprodocai` | `srbappprodocai` | HTTPS | Function Key |

---

## Recursos (referencia rápida)

| Recurso | Tipo | Región |
|---|---|---|
| `srbappprodocai` | Function App (.NET 8 Isolated) | West Europe |
| `srbwebAdminprodocai` | App Service — Blazor Server | West Europe |
| AssetResolver Web App | App Service — ASP.NET Core API | West Europe |
| `srbspprodocai` | App Service Plan | West Europe |
| `srbstgprodocai` | Storage Account (PDFs + sidecars) | West Europe |
| `srbstgproapppdocai` | Storage Account (Durable state) | West Europe |
| `srbsqlprodocai` | Azure SQL Server / DB DocumentIA | West Europe |
| `srbdiprodocai` | Document Intelligence | West Europe |
| `srboaiprodocai` | Azure OpenAI — gpt-4o-mini | West Europe |
| `upe48-mm2avmdm-swedencentral` | Azure AI Foundry / Content Understanding | Sweden Central |
| `srbkvprodocai` | Key Vault | West Europe |
| `srbappiprodocai` | Application Insights | West Europe |
| `srblawprodocai` | Log Analytics Workspace | West Europe |
| GDC SINTWS | SOAP Service (on-premise) | Red interna SAREB |
| BD `DM_POSICION_AAII_TB` | SQL Server (on-premise) | Red interna SAREB |

