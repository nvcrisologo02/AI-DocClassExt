# Manual de Healthcheck por Componentes

> Última revisión: 2026-11-25 (AB#101553, validación TLS del GDC contra la CA de SAREB; verificada en DEV el 2026-11-25; PRE y PRO con la siguiente release). Revisiones previas: 2026-11-22 (AB#101447, contraste con `HealthcheckFunction.cs`, `SystemHealthService.cs`, `Program.cs`, `azure-pipelines.yml` e INFRAESTRUCTURA_REAL_DESPLEGADA.md). Hay tres entornos con el mismo endpoint `POST /api/healthcheck` (`srbappdevdocai`, `srbapppredocai`, `srbappprodocai`; autorización `Anonymous`). Correcciones de la revisión: el timeout del probe a GDC es de 30 s (no 5 s); la clave de caché es `SystemHealthService:ComponentsHealth`; los settings de GDC son `GDC:Endpoint`, `GDC:HttpBasicUsername`, `GDC:HttpBasicPassword` y `GDC:ApplicationId`, con secretos `GDC--HttpBasicUsername` y `GDC--HttpBasicPassword`. El probe de `modelProviders` solo comprueba que los loaders de clasificación, extracción y prompt están registrados (ni el modelo ni la conectividad), así que no distingue `gpt-5.6-luna` (DEV, ADR-004) de `gpt-4o-mini` (PRE y PRO). `ok` es `true` salvo en `unhealthy`: un entorno `unconfigured` devuelve 200 con `ok: true`, por lo que un probe externo debe mirar `status`. Comprobado el 2026-11-23 (AB#101449): no hay Availability Tests en ningún entorno; las tres identidades tienen Key Vault Secrets User en el Key Vault de su entorno; el Feature 99251 y las Tasks 99252-99255 están en Done.
>
> Ultima actualizacion anterior: 2026-05-01  
> Proyecto: AI DocClassExt — DocumentIA  
> Endpoint: `POST /api/healthcheck` en cada Function App: DEV `srbappdevdocai`, PRE `srbapppredocai`, PRO `srbappprodocai` (los ejemplos de este manual usan los nombres de PRO)  
> Codigo: `src/backend/DocumentIA.Functions/Services/SystemHealthService.cs` + `Triggers/HealthcheckFunction.cs`

Este manual describe los probes que componen el healthcheck del sistema, su semantica, los settings que controlan cada uno, los consumidores conocidos, las situaciones de fallo tipicas y como diagnosticarlos.

Para el contrato completo de request/response ver `docs/contratos/CONTRATO_API_HTTP.md` §4.bis.

---

## 1. Vision general

| Aspecto | Valor |
|---|---|
| Endpoint | `POST /api/healthcheck` |
| Auth | `AuthorizationLevel.Anonymous` (sin Function Key) en los tres entornos. A 2026-11-23 la Function App no tiene EasyAuth, su acceso público está habilitado y las restricciones de red son «Allow all» en los tres entornos, así que el endpoint responde desde fuera de la red corporativa |
| Metodo | `POST` (no soporta `GET`) |
| Response codes | `200 OK` (healthy/degraded/unconfigured) o `503 Service Unavailable` (unhealthy) |
| Cache interno | 45 s en `IMemoryCache` (clave `SystemHealthService:ComponentsHealth`) |
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
| Como diagnosticar fallo | Si el endpoint no responde, el problema es de la Function App: revisar la Function App del entorno (`srbappdevdocai` / `srbapppredocai` / `srbappprodocai`) en el portal, su App Insights (`srbappidevdocai` / `srbappipredocai` / `srbappiprodocai`) y Resource Health. |

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
| Diagnostico | 1) Verificar que el Web App de AssetResolver del entorno (DEV `srbwebpluginassetresolverdev`, PRE `srbwebpluginassetresolverpre`, PRO `srbwebpluginassetresolver`) esta arrancado y responde a `GET /api/assets/ping`. 2) Si KV reference, validar que la Managed Identity tiene `Key Vault Secrets User` sobre el Key Vault del entorno (`srbkvdevdocai` / `srbkvpredocai` / `srbkvprodocai`). 3) Revisar logs del Web App. `unconfigured` solo cuando falta `BaseUrl` o `ApiKey` (mensaje distinto en cada caso). |

### 3.3 `gdc`

| Campo | Valor |
|---|---|
| Que comprueba | Que el endpoint SOAP de GDC responde a `consultarObjetoExiste`. |
| Implementacion | `IGdcService.ConsultarDocumentoAsync("HEALTHCHECK_PROBE", "00000000000000000000000000000000", "HLTH", ct)` con timeout 30 s (`CancelAfter(TimeSpan.FromSeconds(30))` en `SystemHealthService`). |
| Settings | `GDC:Endpoint` (URL SOAP), `GDC:HttpBasicUsername`, `GDC:HttpBasicPassword`, `GDC:ApplicationId` (los que lee `GdcSettings`). El probe solo comprueba `GDC:Endpoint` para `unconfigured`. |
| `healthy` | Llamada completada (devuelva `exists=true` o `false`). `message: "Reachable (document found)"` o `"Reachable (document not found)"`. |
| `degraded` | Timeout > 30 s (no se considera unhealthy porque GDC ocasionalmente responde lento sin error funcional). |
| `unhealthy` | SOAP fault, error de red, credenciales rechazadas, etc. |
| `unconfigured` | `GDC:Endpoint` vacio. |
| Diagnostico | 1) Comprobar conectividad de red al endpoint SOAP (puede requerir whitelist NSG). 2) Validar credenciales en KV (`GDC--HttpBasicUsername`, `GDC--HttpBasicPassword`). 3) Probar con `tests/api-tests/test-gdc-consultar-aislado.ps1`. Desde el despliegue de AB#101553 (verificado en DEV el 2026-11-25; PRE y PRO con la siguiente release) el certificado del GDC se valida contra `config/certs/sareb-ca-chain.pem` (`GDC__BypassSslValidation=false`); un fallo de cadena se traza como Warning (sujeto, emisor y `ChainStatus`) por Application Insights (categoría `DocumentIA.Core.Services.GdcServerCertificateValidator`) y por consola (Log Stream); AB#101246 tiene abierto que algunas trazas `DocumentIA.*` no llegan a App Insights. En PRE y PRO la comprobación sigue omitida por `GDC__BypassSslValidation=true` hasta su siguiente despliegue; el pipeline no restablece el interruptor (`ensure-app-settings.ps1` no sobrescribe claves existentes), así que hace falta un `az functionapp config appsettings set` explícito antes de dar la validación por activa. |

### 3.4 `modelProviders`

Estructura compuesta con tres sub-componentes (cada uno objeto unico, no array):

#### 3.4.1 `modelProviders.classification`

| Campo | Valor |
|---|---|
| Que comprueba | Que `ClassificationModelRegistryLoader` esta registrado en DI. |
| `healthy` | Loader presente. `message: "Loader registered"`. |
| `unconfigured` | Loader no registrado (configuracion DI defectuosa). |
| Settings | Implicitos: la presencia del loader implica que se cargo el registry de modelos de clasificacion. |
| Diagnostico | Revisar `Program.cs` y la configuracion de modelos en BBDD (`ModeloConfigs`, filas de clasificacion). El probe no cubre el `LayoutModelRegistryLoader` ni ningun cliente de IA: no distingue el modelo generativo (DEV `prompt.gpt56-luna` desde el 2026-11-20, ADR-004; PRE y PRO `gpt-4o-mini`). |

#### 3.4.2 `modelProviders.extraction`

Identico al anterior pero con `ExtractionModelRegistryLoader`.

#### 3.4.3 `modelProviders.prompt`

Identico al anterior pero con `PromptModelRegistryLoader`.

> **Limitacion conocida**: el probe actual no valida la conectividad real con cada modelo (DI / Azure OpenAI / Foundry). Solo confirma que el loader estatico esta cargado. La conectividad real con cada modelo se ejercita al usarlo en runtime (clasificar / extraer / prompt). Mejora candidata: ejecutar un ping ligero por proveedor.

---

## 4. Cache de 45 segundos

El snapshot se cachea para evitar martillear servicios externos (GDC, AssetResolver) cuando hay multiples consumidores polleando.

- TTL: 45 s.
- Clave: `SystemHealthService:ComponentsHealth`.
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

- `DocumentIA.Tests.Unit.Triggers.HealthcheckFunctionTests` (7 metodos de prueba, 12 ejecuciones contando los casos de `[Theory]`; 12 de 12 en verde el 2026-11-23): cubre casos sin servicio (fallback minimo), con servicio healthy, degraded, unhealthy, unconfigured y verifica codigo HTTP.

### 5.4 Probes externos (recomendado)

| Consumidor | Configuracion sugerida |
|---|---|
| Application Insights Availability Test | Una prueba por entorno: URL `POST https://srbapp<env>docai.azurewebsites.net/api/healthcheck` (DEV `srbappdevdocai`, PRE `srbapppredocai`, PRO `srbappprodocai`), esperar `200`, frecuencia 5 min, regiones multiples. A 2026-11-23 no existe ninguna en DEV, PRE ni PRO: es una propuesta pendiente. |
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
# Entornos desplegados (sustituir el nombre: srbappdevdocai, srbapppredocai, srbappprodocai)
Invoke-RestMethod -Method Post -Uri "https://srbappprodocai.azurewebsites.net/api/healthcheck"
```

Si solo se quiere comprobar el endpoint sin probes (escenario tests), puede inyectarse un host alternativo sin `ISystemHealthService` registrado: la respuesta sera el payload minimo `{ ok: true, timestamp: "..." }`.

---

## 7. Troubleshooting

| Sintoma | Probable causa | Accion |
|---|---|---|
| `503` con `assetResolver.status == "unhealthy"` y `message: "Timeout"` | Web App AssetResolver caido o cold-start. | Revisar Resource Health del Web App; forzar warm-up; revisar logs. |
| `assetResolver.status == "unconfigured"` en un entorno desplegado | KV reference no se resolvio. | Verificar Managed Identity tiene rol `Key Vault Secrets User` en el Key Vault del entorno (`srbkv<env>docai`); reiniciar Function App; comprobar `local.settings.json` no override. |
| `gdc.status == "degraded"` con `Timeout` | Latencia GDC alta pero servicio funcional. | Monitorizar; si persiste subir alerta. No bloquea el sistema. |
| `gdc.status == "unhealthy"` con SOAP fault | Credenciales rechazadas o endpoint cambio. | Validar `GDC--HttpBasicUsername`/`GDC--HttpBasicPassword` en KV; ejecutar `test-gdc-consultar-aislado.ps1`. |
| `modelProviders.*.status == "unconfigured"` | Loader no registrado en DI. | Bug de configuracion: revisar `Program.cs` y verificar que el registry de modelos esta presente. |
| Endpoint devuelve `200` con payload minimo `{ ok: true, timestamp }` | `ISystemHealthService` no se inyecto (constructor sin parametros). | Reiniciar Function App; verificar `AddScoped<ISystemHealthService, SystemHealthService>()` en `Program.cs`. |
| Endpoint devuelve `404` | Despliegue antiguo sin la function. | Redesplegar Functions con el pipeline `azure-pipelines.yml` (o `azure-pipelines-functions.yml`), nunca con publicacion manual. |

---

## 8. Referencias

- `docs/contratos/CONTRATO_API_HTTP.md` §4.bis Healthcheck (request/response oficial).
- `src/backend/DocumentIA.Functions/Services/SystemHealthService.cs` (logica de probes).
- `src/backend/DocumentIA.Functions/Triggers/HealthcheckFunction.cs` (HTTP trigger).
- `src/backend/DocumentIA.Tests.Unit/Triggers/HealthcheckFunctionTests.cs` (cobertura).
- `docs/01_ARQUITECTURA_SISTEMA.md` (modulo healthcheck en diagrama).
- ADO Feature `#99251` y Tasks `#99252`-`#99255` (epica original; comprobado el 2026-11-23: Feature y Tasks en Done).
