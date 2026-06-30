# Manual de Healthcheck por Componentes

> Ultima actualizacion: 2026-05-01  
> Proyecto: AI DocClassExt — DocumentIA MVP  
> Endpoint: `POST /api/healthcheck` (Function App `srbappprodocai`)  
> Codigo: `src/backend/DocumentIA.Functions/Services/SystemHealthService.cs` + `Triggers/HealthcheckFunction.cs`

Este manual describe los probes que componen el healthcheck del sistema, su semantica, los settings que controlan cada uno, los consumidores conocidos, las situaciones de fallo tipicas y como diagnosticarlos.

Para el contrato completo de request/response ver `docs/contratos/CONTRATO_API_HTTP.md` §4.bis.

---

## 1. Vision general

| Aspecto | Valor |
|---|---|
| Endpoint | `POST /api/healthcheck` |
| Auth | `AuthorizationLevel.Anonymous` (sin Function Key) |
| Metodo | `POST` (no soporta `GET`) |
| Response codes | `200 OK` (healthy/degraded/unconfigured) o `503 Service Unavailable` (unhealthy) |
| Cache interno | 45 s en `IMemoryCache` (clave `SystemHealthService:ComponentsHealthSnapshot`) |
| Lifetime DI | `AddScoped<ISystemHealthService, SystemHealthService>()` (depende de `IGdcService` que es scoped) |
| Fallback | Si `ISystemHealthService` no esta inyectado devuelve payload minimo `{ ok: true, timestamp: "..." }` |

---

## 2. Estados posibles

Cada componente y el agregado pueden tomar uno de estos cuatro estados (en minusculas):

| Status | Significado | Impacto en agregado |
|---|---|---|
| `healthy` | Probe completado con exito. | Si todos son `healthy`, agregado `healthy`. |
| `degraded` | Probe respondio pero con codigo no satisfactorio o timeout no critico. | Eleva agregado a `degraded` si no hay `unhealthy`. |
| `unhealthy` | Probe fallo (excepcion, timeout critico). | Eleva agregado a `unhealthy` y devuelve `503`. |
| `unconfigured` | Falta setting obligatorio o el loader/servicio no esta registrado. | Eleva agregado a `unconfigured` si no hay `degraded` ni `unhealthy`. |

Precedencia agregada: `unhealthy > degraded > unconfigured > healthy`.

---

## 3. Componentes

### 3.1 `functions`

| Campo | Valor |
|---|---|
| Que comprueba | El runtime Functions esta vivo lo suficiente para responder. |
| Implementacion | Constante: `ComponentHealth.Healthy("Running")`. No ejecuta probe externo. |
| Falla cuando | Nunca (si fallara, no se serviria la respuesta). |
| Settings relevantes | Ninguno especifico. |
| Como diagnosticar fallo | Si el endpoint no responde, el problema es de la Function App: revisar `srbappprodocai` en el portal, App Insights `srbappiprodocai` y Resource Health. |

### 3.2 `assetResolver`

| Campo | Valor |
|---|---|
| Que comprueba | Que el Web App del plugin AssetResolver responde a `GET /api/assets/ping`. |
| Implementacion | `IHttpClientFactory.CreateClient("AssetResolver").GetAsync("api/assets/ping")` con timeout 8 s. |
| Settings | `AssetResolver:BaseUrl` y `AssetResolver:ApiKey` (en KV via `@Microsoft.KeyVault(...)` en prod). El cliente HTTP nombrado se configura en `Program.cs` con base URL y header de API key. |
| `healthy` | Respuesta 2xx. `message: "HTTP 200"`. |
| `degraded` | Respuesta no-2xx. `message: "HTTP {code}"`. |
| `unhealthy` | Excepcion HTTP o timeout > 8 s. `message: "Timeout"` o mensaje de excepcion. |
| `unconfigured` | `AssetResolver:BaseUrl` o `AssetResolver:ApiKey` vacios. |
| Diagnostico | 1) Verificar Web App `srbwebpluginassetresolver` esta arrancado y responde a `GET /api/assets/ping`. 2) Si KV reference, validar Managed Identity tiene `Key Vault Secrets User` sobre `srbkvprodocai`. 3) Revisar logs Plugin Web App. |

### 3.3 `gdc`

| Campo | Valor |
|---|---|
| Que comprueba | Que el endpoint SOAP de GDC responde a `consultarObjetoExiste`. |
| Implementacion | `IGdcService.ConsultarDocumentoAsync("HEALTHCHECK_PROBE", "00000000000000000000000000000000", "HLTH", ct)` con timeout 5 s. |
| Settings | `GDC:Endpoint` (URL SOAP), `GDC:Usuario`, `GDC:Password`, `GDC:Aplicacion`. |
| `healthy` | Llamada completada (devuelva `exists=true` o `false`). `message: "Reachable (document found)"` o `"Reachable (document not found)"`. |
| `degraded` | Timeout > 5 s (no se considera unhealthy porque GDC ocasionalmente responde lento sin error funcional). |
| `unhealthy` | SOAP fault, error de red, credenciales rechazadas, etc. |
| `unconfigured` | `GDC:Endpoint` vacio. |
| Diagnostico | 1) Comprobar conectividad de red al endpoint SOAP (puede requerir whitelist NSG). 2) Validar credenciales en KV (`gdc-usuario`, `gdc-password`). 3) Probar con `tests/api-tests/test-gdc-consultar-aislado.ps1`. |

### 3.4 `modelProviders`

Estructura compuesta con tres sub-componentes (cada uno objeto unico, no array):

#### 3.4.1 `modelProviders.classification`

| Campo | Valor |
|---|---|
| Que comprueba | Que `ClassificationModelRegistryLoader` esta registrado en DI. |
| `healthy` | Loader presente. `message: "Loader registered"`. |
| `unconfigured` | Loader no registrado (configuracion DI defectuosa). |
| Settings | Implicitos: la presencia del loader implica que se cargo el registry de modelos de clasificacion. |
| Diagnostico | Revisar `Program.cs` y la configuracion de modelos en BBDD (`ai-models/Classification`). |

#### 3.4.2 `modelProviders.extraction`

Identico al anterior pero con `ExtractionModelRegistryLoader`.

#### 3.4.3 `modelProviders.prompt`

Identico al anterior pero con `PromptModelRegistryLoader`.

> **Limitacion conocida**: el probe actual no valida la conectividad real con cada modelo (DI / Azure OpenAI / Foundry). Solo confirma que el loader estatico esta cargado. La conectividad real con cada modelo se ejercita al usarlo en runtime (clasificar / extraer / prompt). Mejora candidata: ejecutar un ping ligero por proveedor.

---

## 4. Cache de 45 segundos

El snapshot se cachea para evitar martillear servicios externos (GDC, AssetResolver) cuando hay multiples consumidores polleando.

- TTL: 45 s.
- Clave: `SystemHealthService:ComponentsHealthSnapshot`.
- Implementacion: `IMemoryCache` (proceso). Cada instancia de Function host tiene su propia cache.
- Implicacion: si un componente cae, el primer probe tras la ventana lo detecta; los probes siguientes (en los 45 s posteriores) devuelven el mismo resultado en cache.

---

## 5. Consumidores

### 5.1 Frontend Admin (`DocumentIA.Admin`, Blazor)

- Servicio: `MonitorService.GetSystemHealthAsync()`.
- Llamada: `POST /api/healthcheck`.
- Comportamiento: acepta tanto `200` como `503` con payload JSON. Pinta tarjeta "Salud del sistema" en pagina `Monitor` con sub-cards por componente.
- Polling: bajo demanda (refresco manual o navegacion a Monitor).

### 5.2 Frontend Desktop (`DocumentIA.Desktop`, WPF)

- Cliente: `OrchestratorApiClient.GetSystemHealthAsync()`.
- ViewModel: `ProcessingViewModel` expone bindings de salud agregada y por componente.
- UI: panel AGG / FUNCTIONS / ASSET / GDC / MODELS / UPDATED en `MainWindow.xaml`.
- Conversor: `HealthStatusToBrushConverter` en `Styles.xaml` traduce status a color.

### 5.3 Tests

- `DocumentIA.Tests.Unit.Triggers.HealthcheckFunctionTests` (8 tests): cubre casos sin servicio (fallback minimo), con servicio healthy, degraded, unhealthy, unconfigured y verifica codigo HTTP.

### 5.4 Probes externos (recomendado)

| Consumidor | Configuracion sugerida |
|---|---|
| Application Insights Availability Test | URL `POST https://srbappprodocai.azurewebsites.net/api/healthcheck`, esperar `200`, frecuencia 5 min, regiones multiples. |
| Azure Front Door / balanceador (futuro) | Health probe HTTP `POST /api/healthcheck`, intervalo 30 s, umbral 3 fallos consecutivos antes de marcar instance unhealthy. |
| Workbook App Insights | KQL sobre `requests` filtrando `name == "PostHealthcheck"` para latencia y tasa de error. |

---

## 6. Pruebas locales

```powershell
# Funcion local con func host start
$body = @{} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:7071/api/healthcheck" -Body $body -ContentType "application/json"
```

```powershell
# Productivo
Invoke-RestMethod -Method Post -Uri "https://srbappprodocai.azurewebsites.net/api/healthcheck"
```

Si solo se quiere comprobar el endpoint sin probes (escenario tests), puede inyectarse un host alternativo sin `ISystemHealthService` registrado: la respuesta sera el payload minimo `{ ok: true, timestamp: "..." }`.

---

## 7. Troubleshooting

| Sintoma | Probable causa | Accion |
|---|---|---|
| `503` con `assetResolver.status == "unhealthy"` y `message: "Timeout"` | Web App AssetResolver caido o cold-start. | Revisar Resource Health del Web App; forzar warm-up; revisar logs. |
| `assetResolver.status == "unconfigured"` en prod | KV reference no se resolvio. | Verificar Managed Identity tiene rol `Key Vault Secrets User` en `srbkvprodocai`; reiniciar Function App; comprobar `local.settings.json` no override. |
| `gdc.status == "degraded"` con `Timeout` | Latencia GDC alta pero servicio funcional. | Monitorizar; si persiste subir alerta. No bloquea el sistema. |
| `gdc.status == "unhealthy"` con SOAP fault | Credenciales rechazadas o endpoint cambio. | Validar `gdc-usuario`/`gdc-password` en KV; ejecutar `test-gdc-consultar-aislado.ps1`. |
| `modelProviders.*.status == "unconfigured"` | Loader no registrado en DI. | Bug de configuracion: revisar `Program.cs` y verificar que el registry de modelos esta presente. |
| Endpoint devuelve `200` con payload minimo `{ ok: true, timestamp }` | `ISystemHealthService` no se inyecto (constructor sin parametros). | Reiniciar Function App; verificar `AddScoped<ISystemHealthService, SystemHealthService>()` en `Program.cs`. |
| Endpoint devuelve `404` | Despliegue antiguo sin la function. | Republicar Functions (`func azure functionapp publish srbappprodocai`). |

---

## 8. Referencias

- `docs/contratos/CONTRATO_API_HTTP.md` §4.bis Healthcheck (request/response oficial).
- `src/backend/DocumentIA.Functions/Services/SystemHealthService.cs` (logica de probes).
- `src/backend/DocumentIA.Functions/Triggers/HealthcheckFunction.cs` (HTTP trigger).
- `src/backend/DocumentIA.Tests.Unit/Triggers/HealthcheckFunctionTests.cs` (cobertura).
- `docs/01_ARQUITECTURA_SISTEMA.md` (modulo healthcheck en diagrama).
