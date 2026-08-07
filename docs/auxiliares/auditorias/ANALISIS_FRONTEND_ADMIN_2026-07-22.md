# Análisis del frontend de administración (DocumentIA.Admin)

**Fecha:** 2026-07-22
**Alcance:** toda la aplicación `src/frontend/DocumentIA.Admin` (17 páginas, 4 servicios, layout, componentes comunes) más cruce con los endpoints admin/monitor del backend (`DocumentIA.Functions/Triggers`).
**Naturaleza:** solo análisis y propuestas. No se ha modificado código.
**Perfiles de usuario considerados:** equipo técnico (diagnóstico), negocio/operación (visión agregada) y administrador de configuración (coherencia de tipologías/modelos/prompts/plugins).
**Restricción de stack acordada:** Bootstrap 5 puro; se permite una librería ligera de gráficos **solo** para el Monitor.

---

## 1. Resumen ejecutivo

La aplicación es un panel Blazor Server (.NET 8) funcionalmente completo para el MVP: cubre todo el ciclo de vida de tipologías (draft/publicar/retirar, versionado, diff, auditoría), modelos, prompts versionados con rollback, plugins, catálogos TDN1/TDN2 y un monitor de ejecuciones con detalle muy granular. El cruce frontend–backend confirma que **no hay llamadas rotas**: todas las rutas que invoca la UI existen en el backend.

Los puntos débiles se concentran en cuatro frentes:

1. **Seguridad** — sin autenticación, cadena de conexión SQL visible en pantalla, configuración por defecto apuntando a PRO y dependencia de CDN externo.
2. **Acciones destructivas sin confirmación** — borrar modelos, catálogos y prompts, o publicar/retirar tipologías, se ejecuta con un solo clic.
3. **Deriva frontend–backend** — código muerto (manejo de 403 que nunca se dispara), un hueco real (el backend permite editar un prompt *activo*), un bug latente (edición de modelos Layout) y endpoints/tests obsoletos.
4. **Representación de la información** — el Monitor tiene datos excelentes pero mezclados en dos bloques de KPIs que no cuadran entre sí, sin tendencia temporal ni gráficos; la página de Configuración es un listado plano en lugar de un panel de coherencia.

---

## 2. Fortalezas

| Área | Fortaleza |
|---|---|
| Monitor — detalle | El detalle expandible por ejecución es de gran calidad diagnóstica: timeline por actividad con duración proporcional y razón de fallback, confianza por campo extraído, resultado de cada plugin (duración, error), estado GDC (intentos, ObjectId), integridad (SHA, IdActivo cambiado) y validaciones con severidad. Es la mejor pieza de la app. |
| Monitor — agregados | El "Cuadro de Mando" desglosa por tipología y por modelo con barras apiladas OK/Revisión/Error, confianza media coloreada por umbral y fallbacks. Los umbrales de color (confianza ≥0.7/≥0.5, duración >30s/>60s) son coherentes en toda la página. |
| Gobernanza de configuración | El ciclo draft → publicar → retirar está aplicado de forma consistente en tipologías, plugins y prompts. Tipologías añade versionado por familia, diff entre versiones con filtro por tipo de cambio y auditoría consultable — muy por encima de lo habitual en un MVP. |
| Validación en cliente | `TipologiaAdminService.ValidarConfiguracionJson` valida umbrales 0–1, coherencia umbralRevision ≤ umbralOK, fieldMappings contra fields, duplicados, y reglas GDC condicionales. `PromptPlaceholderValidator` guía los placeholders requeridos por clave. |
| Servicios tipados | Los 4 servicios extraen el mensaje de error del cuerpo JSON (`TryReadError`) en lugar de mostrar códigos HTTP crudos; DTOs completos y `PropertyNameCaseInsensitive`. |
| Editor JSON | `JsonEditorComponent` (jsoneditor) con modos tree/form/code, pantalla completa y validación; reutilizado en tipologías, modelos y plugins. |
| Salud del sistema | El healthcheck integrado en el Monitor muestra el estado por componente (functions, assetResolver, gdc, providers de clasificación/extracción/prompt). |

---

## 3. Hallazgos críticos (P0)

### 3.1 Sin autenticación ni identidad de usuario
`Program.cs` no configura ningún middleware de autenticación/autorización. Cualquiera con acceso de red puede publicar tipologías, activar prompts o borrar catálogos. Además, toda la auditoría registra usuarios ficticios hardcodeados: `"ADMIN-UI"` (tipologías/plugins) y `"admin"` (prompts), con lo que la trazabilidad de la auditoría de tipologías —una de las fortalezas— queda vacía de contenido.

**Propuesta:** activar autenticación Entra ID. La vía de menor esfuerzo si la app corre en App Service es EasyAuth (sin cambios de código, solo infraestructura); la vía completa es `Microsoft.Identity.Web` + `AddAuthentication(OpenIdConnectDefaults)` y propagar `User.Identity.Name` a los campos `Usuario`/`PublishedBy` en los servicios, sustituyendo los literales. Esto convierte la auditoría en real.

### 3.2 Cadena de conexión SQL renderizada en pantalla
[ConfiguracionConsulta.razor:252-258](../../../src/frontend/DocumentIA.Admin/Components/Pages/ConfiguracionConsulta.razor#L252-L258) muestra `SqlConnectionString` si el backend no la devuelve enmascarada, y [SystemConfigService.cs:144-156](../../../src/frontend/DocumentIA.Admin/Services/SystemConfigService.cs#L144-L156) la extrae expresamente ("Solo mostrar si no está masked"). El filtrado de settings sensibles se hace en cliente por nombre de clave (`Password`, `Key`), que es frágil.

**Propuesta:** eliminar la fila de SQL Connection de la UI (el backend ya expone server/db enmascarados en `effectiveSqlConnection`, que es lo útil para diagnóstico) y confiar exclusivamente en el enmascarado de servidor, no en filtros de cliente.

### 3.3 `appsettings.json` apunta a PRO por defecto
El `BaseUrl` por defecto del repo es `https://srbappprodocai.azurewebsites.net/api/`. Cualquier ejecución local sin perfil Development (o un despliegue con configuración incompleta) opera directamente contra producción sin ninguna señal visual.

**Propuesta:** dejar el `BaseUrl` vacío o en localhost en el JSON commiteado y exigirlo por configuración de entorno; añadir un banner permanente en el layout con el entorno y host del backend efectivo (dato que ya devuelve `management/configuration`), con color distintivo cuando sea PRO.

### 3.4 Acciones destructivas y de publicación sin confirmación
- **Borrar** en Modelos ([Modelos.razor:54-59](../../../src/frontend/DocumentIA.Admin/Components/Pages/Modelos.razor#L54-L59)), Catálogo TDN1/TDN2 y Prompts ejecuta al primer clic.
- **Publicar / Retirar / Pasar a Draft** en TipologiaDetail y PluginsTipologias, con efecto inmediato en el pipeline de clasificación (invalida el snapshot de tipologías), tampoco confirman.

**Propuesta:** diálogo de confirmación Bootstrap (modal reutilizable `ConfirmDialog`) para todo lo irreversible o con efecto en producción, indicando el impacto ("Publicar hará que esta versión se use en la próxima clasificación").

### 3.5 Hueco de negocio: el backend permite editar un prompt ACTIVO
`PromptManagementService` maneja 403 asumiendo que el backend bloquea editar/borrar prompts activos. El cruce con `PromptsAdminFunction.cs` demuestra que:
- `Admin_UpdatePromptTemplate` (línea 209) **no valida `IsActive`**: se puede sobrescribir en caliente el contenido del prompt en uso por el clasificador.
- `Admin_DeletePromptTemplate` devuelve **409**, no 403, así que ambos bloques `if (Forbidden)` del frontend son código muerto.

Agrava el problema que `StartEditPrompt` carga "la última versión" (`versions.FirstOrDefault()`), que puede ser la activa, y el editor la abre con `IsReadOnly="false"`.

**Propuesta:** (a) backend: rechazar `PUT` sobre prompts activos (409) igual que el delete — el flujo correcto ya existe: crear nueva versión draft y activarla; (b) frontend: en `StartEditPrompt`, si la última versión está activa, precargar el editor en modo "nueva versión" (Id=0 con contenido copiado) en lugar de edición directa; alinear el manejo de errores a 409.

### 3.6 Bug latente: editar un modelo de tipo Layout crea un duplicado
`GetModeloByIdAsync` ([TipologiaAdminService.cs:141-147](../../../src/frontend/DocumentIA.Admin/Services/TipologiaAdminService.cs#L141-L147)) busca solo en clasificación/extracción/prompt, **omitiendo layout**. Al abrir `/modelos/edit/{id}` de un modelo Layout, el lookup falla en silencio, `ModeloEdit` se queda con un modelo nuevo (Id=0) y "Guardar" crea un registro duplicado en vez de actualizar.

**Propuesta:** añadir `layout` al lookup (o mejor, un endpoint `GET management/modelos/id/{id}` en backend y usarlo). Mientras tanto, `ModeloEdit` debería mostrar error si `Id.HasValue` y no se encontró la entidad, en vez de continuar como alta.

---

## 4. Funcionalidad obsoleta o muerta (limpieza)

| Elemento | Evidencia | Acción propuesta |
|---|---|---|
| `Counter.razor`, `Weather.razor` | Plantilla Blazor sin tocar desde 2026-03-27; no enlazadas en el NavMenu pero accesibles por URL (`/counter`, `/weather`) | Eliminar |
| Link "About" → learn.microsoft.com en `MainLayout.razor` | Resto de plantilla | Eliminar o sustituir por versión/entorno de la app |
| Tests E2E del wizard (`DocumentIA.Tests.E2E/Wizard*.cs`, 4 ficheros) | El wizard de tipologías se eliminó del frontend en junio (commits `619ee4a` "remove wizard" y `444d9fd` "remove non-functional wizard link"); los tests se auto-skipean o fallarían contra rutas inexistentes | Eliminar el proyecto o reescribir E2E contra las páginas actuales (lista/edición/publicación) |
| Endpoints `GET management/tipologias/{id}/export` y `POST management/tipologias/import` | Marcados `[Obsolete]` en `TipologiasAdminFunction.cs:448/503` desde 2026-06-04; ningún servicio del frontend los llama | Eliminar del backend (o, si el export ZIP se considera útil, exponerlo en la UI de detalle — decidir, no dejar a medias) |
| Manejo de 403 en `PromptManagementService` (líneas ~112 y ~186) | Código muerto: el backend nunca devuelve 403 (ver 3.5) | Alinear a 409 al corregir 3.5 |
| Campos legacy duplicados al guardar tipologías | `NormalizeTipologiaConfigJsonForCompatibility` ([TipologiaEdit.razor:261-307](../../../src/frontend/DocumentIA.Admin/Components/Pages/TipologiaEdit.razor#L261-L307)) escribe en cada guardado los campos `[Obsolete]` (`SkipGDCUpload`, `Tdn1`, `GdcSerie`… planos) además de los bloques v1.2 (`Gdc`, `Classification`) | Planificar la retirada: confirmar que ya no queda consumidor del formato legacy y eliminar la duplicación (hoy cada guardado re-siembra deuda) |
| Botón "Borrar" en Modelos | El backend hace soft-delete (`Activo=false`, devuelve 200 + entidad), no borra; el usuario cree que borra | Renombrar a "Desactivar" (y ofrecer "Reactivar" sobre inactivos), o implementar borrado real si es lo que se quiere |
| `@rendermode InteractiveServer` por página | `App.razor` ya aplica `InteractiveServer` global en `<Routes>`; las declaraciones en Monitor/ConfiguracionConsulta/JsonEditor son redundantes y el resto de páginas no lo declaran — inconsistencia cosmética que confunde | Unificar (quitar las declaraciones por página) |

---

## 5. Monitorización: representación de la información

Los datos disponibles son buenos; el problema es de organización y de ausencia de dimensión temporal.

### 5.1 Dos bloques de KPIs que no cuadran entre sí
La página muestra simultáneamente: (a) "Tarjetas de resumen" calculadas sobre **las N ejecuciones cargadas y filtradas en cliente** (25/50/100) y (b) el "Cuadro de Mando" calculado en servidor sobre **un período de días** (7–365). Dos cifras de "total", "errores" y "% fallback" distintas a la vez, sin que la UI explique la diferencia de base. Para negocio esto resta credibilidad al panel.

**Propuesta:** una única cabecera de KPIs, la del agregado por período (servidor), y reservar la lista de ejecuciones como "actividad reciente" sin tarjetas propias (basta el contador "Mostrando X de Y" que ya existe).

### 5.2 Sin tendencia temporal
`management/ejecuciones/agregados` devuelve un único snapshot del rango (sin buckets por día/semana), así que la UI no puede responder "¿la tasa de error está subiendo?" o "¿bajó la confianza tras el último cambio de prompt?" — las preguntas clave de operación.

**Propuesta (requiere backend + frontend):**
- Backend: extender el endpoint con `&bucket=day|week` devolviendo la serie `[{fecha, total, ok, revision, error, fallbacks, confianzaMedia, duracionMediaMs}]`. Es un `GROUP BY` sobre datos ya persistidos, sin nueva captura.
- Frontend (excepción de charts acordada): un gráfico de área/barras apiladas OK/Revisión/Error por día + línea de confianza media, y sparklines por tipología en la tabla del cuadro de mando. Librería ligera vía JS interop (Chart.js o ApexCharts, un solo `<canvas>` por gráfico, sin dependencia de componentes Blazor de terceros) — mismo patrón de interop que ya se usa con jsoneditor.

### 5.3 Filtros solo en cliente y sin dimensiones clave
Los filtros actuales (flujo, actividad, estado de actividad) se aplican en memoria sobre las últimas N cargadas. No se puede: buscar por nombre de documento o GUID, filtrar por tipología, por estado final, ni por rango de fechas; tampoco paginar más allá de 100 (el backend admite `top` hasta 200 y la UI solo ofrece hasta 100).

**Propuesta:** llevar los filtros al servidor (`management/ejecuciones?estado=&tipologia=&desde=&hasta=&q=` + paginación por cursor o `skip/top`). Prioridad para operación: búsqueda por GUID/nombre de documento (hoy, para localizar una ejecución concreta reportada por un usuario, hay que ojear la lista).

### 5.4 Detalle no enlazable
El detalle de ejecución solo existe como fila expandida; no hay URL para compartir un caso ("mira esta ejecución") entre técnicos o adjuntar a un ticket.

**Propuesta:** ruta `/monitor/{guid}` que renderice el mismo panel de detalle (el componente ya existe, extraerlo del `<tr>` a un componente propio) + botón "copiar enlace" junto al GUID.

### 5.5 Errores silenciados en dashboard y healthcheck
`LoadDashboardAsync` y `GetSystemHealthAsync` capturan cualquier excepción y devuelven null ("silent fail"): si el backend de agregados falla, el usuario ve "Sin datos de agregados disponibles" y un badge gris "sin datos", indistinguible de "no hay ejecuciones". Para un monitor, no distinguir "no hay datos" de "no puedo consultar" es un defecto.

**Propuesta:** distinguir tres estados: datos / vacío / error de consulta (con el mensaje y hora del último dato bueno).

### 5.6 Detalles menores
- La cabecera dice "Fecha (UTC+1)" pero `ToSpainTime` aplica DST — en verano es UTC+2. Rotular "hora España" y ya.
- El timer de auto-refresh usa `new Timer(async _ => …)` (lambda async void): excepciones no observadas y posible solapamiento de refresecos si uno tarda más que el intervalo. Sustituir por `PeriodicTimer` con bucle controlado.
- El auto-refresh recarga lista y health pero no el cuadro de mando — decidir y homogeneizar.
- Coste: **no existe hoy ninguna métrica de coste/tokens en el backend** (verificado en el cruce). Si se quiere "coste por ejecución/tipología" en el Monitor, es un item de backlog de backend (persistir usage de los providers en `DetalleEjecucion`) previo a cualquier UI. No se propone UI de coste hasta entonces.

## 6. Configuración: representación de la información

### 6.1 De listado plano a panel de coherencia
`ConfiguracionConsulta` hoy muestra contadores y listas truncadas ("… y N más") sin enlaces. Para el perfil "administrador de configuración", lo valioso no es contar sino **detectar incoherencias**. Con los datos que la página ya carga se pueden computar en cliente, sin backend nuevo, chequeos como:

- Tipología **publicada** cuyo `ConfiguracionJson` referencia un `modelKey` que no existe o está inactivo en Modelos.
- Tipología publicada con extracción habilitada pero sin modelo de extracción activo.
- Config de plugins en Draft para una tipología publicada (¿se olvidó publicar?).
- Prompt key requerida por el flujo (`classification.phase1/2.system/user`) sin versión activa.
- TDN1/TDN2 referenciados en tipologías que no existen en los catálogos.

**Propuesta:** cabecera de la página = lista de avisos (verde "todo coherente" / warnings accionables con enlace directo a la página de edición correspondiente). Los contadores actuales pasan a segunda fila. Cada item listado enlaza a su detalle (hoy no hay ningún enlace).

### 6.2 Settings de Functions: aprovechar source/provider
El backend devuelve por cada setting `key/value/source/provider`, pero la UI lo aplana a "key: value" y muestra 5. Precisamente el incidente conocido de GDC ("secreto KV huérfano pisa el app setting" en PRO) se diagnostica con la columna *source*.

**Propuesta:** tabla completa (colapsable) con columnas Clave / Valor / Origen (AppSetting vs KeyVault) / Provider, con resaltado cuando un mismo key tenga precedencia inesperada.

### 6.3 Rendimiento y duplicación de cargas
La página dispara ~10 llamadas HTTP **secuenciales** y duplicadas: carga tipologías/modelos/plugins ella misma y `SystemConfigService.GetConfigurationResumenAsync()` vuelve a cargarlos todos. 

**Propuesta:** una sola carga (pasar los datos ya cargados al servicio o eliminar la doble vía) y paralelizar con `Task.WhenAll`.

## 7. Consistencia de UX (transversal)

- **Dos generaciones de UI conviven:** Monitor/Prompts con cards, badges e iconografía cuidada; Tipologías/Modelos/Catálogos con tablas austeras sin estado de carga homogéneo ("Cargando..." plano vs spinners) ni badges de estado (el estado de tipología se muestra como texto plano; en Monitor tiene colores). Unificar con 3-4 componentes compartidos: `PageHeader`, `StateBadge` (Draft/Published/Retired con los mismos colores en toda la app), `ConfirmDialog`, `LoadingIndicator`.
- **Iconografía mixta:** emojis (📜 ✏️ 🗑️ ✅ 💾) en Prompts/Configuración vs clases Bootstrap Icons en NavMenu/Monitor. Elegir Bootstrap Icons (ya cargadas) y retirar emojis en botones de acción — los emojis sin texto además penalizan accesibilidad.
- **Mensajes de resultado:** cada página gestiona su `_message/_error` con alerts inline que persisten hasta la siguiente acción. Un servicio de toasts/notificaciones simple (Bootstrap toast + `IToastService` propio) homogeneiza y evita estados residuales.
- **`Modelos.razor` construye la tabla con `RenderTreeBuilder` manual** (70 líneas de `OpenElement/CloseElement`): innecesario en Razor; un `RenderFragment` con markup normal o un subcomponente `ModelosTable` es más legible y mantenible. Además la página no tiene estado de carga ni muestra errores de borrado.
- **Catálogo TDN2:** el filtro por TDN1 es un texto libre ("Ej: ESCR") pudiendo ser un dropdown alimentado del catálogo TDN1 (la API ya existe). El campo "Buscar" de esa página requiere pulsar "Aplicar" mientras que en TDN1/Tipologías filtra al teclear — unificar.
- **Plantillas de TipologiaEdit hardcodean claves de modelo** (`di-clasificacion-default`, `cu-notasimple-v1`, series GDC) que pueden derivar respecto al catálogo real de Modelos; alimentar las plantillas/desplegables desde `management/modelos/*` evitaría publicar tipologías que referencian modelos inexistentes (mismo chequeo que 6.1).
- **Home vacía:** dos botones (Tipologías/Modelos) que ni siquiera cubren la navegación actual. Convertirla en landing con: entorno + salud (reutilizando healthcheck), KPIs del día (reutilizando agregados) y accesos a las 8 secciones. Bajo esfuerzo, alto efecto para todos los perfiles.
- **"Comparar versiones (A-2)"**: título con código interno de tarea visible al usuario; renombrar.
- **Idioma:** UI en español con textos ingleses residuales (estados "unhealthy", "modified/added/removed" en diff, "About"). Unificar o traducir en presentación.

## 8. Arquitectura (deuda a vigilar)

- **El frontend referencia directamente `DocumentIA.Core`, `DocumentIA.Data` y `DocumentIA.Plugins`** (csproj) para usar entidades EF (`TipologiaEntity`, `ModeloConfigEntity`…) como modelos de UI, mientras que Monitor y Prompts definen DTOs propios. Esto acopla la UI al esquema de datos (cualquier cambio de entidad arrastra al Admin y viceversa) y mezcla dos estilos. Dirección propuesta: proyecto `DocumentIA.Contracts` con los DTOs de la API admin, compartido entre Functions y Admin, y retirar la referencia a `Data`/`Plugins` del frontend. No urgente, pero cada página nueva que use entidades EF encarece la separación futura.
- **Validaciones duplicadas cliente/servidor** (tipología, prompts): asumido y razonable para UX, pero conviene un comentario/manifiesto de "fuente de verdad = backend" para que no diverjan (el caso 3.5 es exactamente esa divergencia).
- **jsoneditor desde CDN (cdnjs) con versión fijada 9.10.2:** en entorno corporativo con egress restringido la pantalla de edición JSON quedaría rota. Servirlo desde `wwwroot/lib` (self-host). Bootstrap ya se sirve local — hacer lo mismo.

---

## 9. Plan de acción propuesto (priorizado)

| Prioridad | Acción | Esfuerzo estimado | Refs |
|---|---|---|---|
| **P0** | Autenticación Entra ID + usuario real en auditoría (sustituir "ADMIN-UI"/"admin") | M (S si EasyAuth) | 3.1 |
| **P0** | Retirar SQL connection string de la UI; enmascarado solo en servidor | S | 3.2 |
| **P0** | BaseUrl seguro por defecto + banner de entorno/host efectivo | S | 3.3 |
| **P0** | ConfirmDialog en borrados y publicar/retirar | S | 3.4 |
| **P0** | Cerrar edición de prompts activos (backend 409 + flujo "nueva versión" en UI) | M | 3.5 |
| **P0** | Fix lookup de modelos Layout (evitar duplicados al editar) | S | 3.6 |
| **P1** | Monitor: unificar KPIs (una sola cabecera basada en agregados) | S | 5.1 |
| **P1** | Monitor: filtros en servidor + búsqueda GUID/documento + paginación | M | 5.3 |
| **P1** | Monitor: serie temporal (bucket en backend) + gráficos (Chart.js/ApexCharts vía interop) | M | 5.2 |
| **P1** | Monitor: ruta `/monitor/{guid}` con detalle enlazable | S | 5.4 |
| **P1** | Configuración: panel de coherencia con chequeos cruzados y enlaces | M | 6.1 |
| **P1** | Configuración: tabla de settings con source/provider; eliminar doble carga y paralelizar | S | 6.2, 6.3 |
| **P1** | Limpieza de obsoletos: Counter/Weather/About, tests E2E wizard, endpoints export/import, 403 muerto, "Borrar"→"Desactivar" en modelos | S | 4 |
| **P1** | Self-host de jsoneditor (retirar CDN) | S | 8 |
| **P2** | Componentes compartidos (StateBadge, PageHeader, toasts) + iconografía unificada | M | 7 |
| **P2** | Home como landing (salud + KPIs + navegación) | S | 7 |
| **P2** | Distinguir vacío vs error en dashboard/health; PeriodicTimer en auto-refresh | S | 5.5, 5.6 |
| **P2** | Dropdown TDN1 en catálogo TDN2; plantillas de tipología alimentadas del catálogo de modelos | S | 7 |
| **P2** | Proyecto de contratos DTO; retirar referencia del frontend a Data/Plugins | L | 8 |
| **P2** | Retirada del formato legacy en ConfiguracionJson (previa verificación de consumidores) | M | 4 |
| Backlog | Métricas de coste/tokens (requiere primero captura y persistencia en backend) | L | 5.6 |

Esfuerzos: S ≤ ½ día · M = 1-3 días · L > 3 días (orientativos, por item).

---

## 10. Verificación del cruce frontend–backend (anexo)

- **Llamadas de la UI sin endpoint:** ninguna.
- **Endpoints admin sin UI:** solo los dos `[Obsolete]` de export/import de tipologías. `GET tipologias` (público) lo consume el pipeline, no el Admin.
- **Discrepancias de semántica:** DELETE de modelos = soft-delete con 200 (vs 204 y borrado físico en catálogos); prompts: 409 vs 403 esperado y falta de bloqueo de edición de activos (detallado en 3.5).
- **Granularidad de monitor disponible hoy:** lista resumida (top ≤200), detalle completo por ejecución (fases, campos, plugins, validaciones, GDC, integridad), agregado único por período con desglose tipología/modelo. Sin series temporales ni coste.
