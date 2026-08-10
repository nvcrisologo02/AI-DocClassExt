# Document Intelligence — transporte inline y guarda de contenido vacío

**Fecha:** 2026-08-10
**Estado:** Aprobado. Work items creados, pendiente de plan de implementación
**Work item:** AB#100031 (tasks AB#100032 a AB#100037)

## Problema

Document Intelligence es el único proveedor de IA al que el sistema pasa el documento
**por referencia**: se genera un SAS del blob y se manda como `urlSource` para que DI se lo
descargue. Los demás lo reciben **empujado** en el cuerpo de la petición: Content Understanding
descarga el blob dentro del Function App y manda los bytes
(`AzureContentUnderstandingProvider.cs:72-84`), y el LLM recibe el markdown ya extraído.

Los storage de documentos de dev (`srbstgdevdocai`) y pre (`srbstgpredocai`) tienen
`publicNetworkAccess = Disabled`, por lo que solo son accesibles a través de su private endpoint.
DI, que vive fuera de esa red, no puede descargar el blob y responde:

```
400 InvalidRequest / innererror InvalidContent:
"Could not download the file from the given URL."
```

20 ocurrencias en dev entre 2026-08-06 10:41 y 2026-08-09 21:35.

### No es un problema de configuración de red que se pueda afinar

La documentación oficial es explícita en dos puntos que descartan la vía de "abrir solo lo justo":

- *"Can Document Intelligence access data in my storage account if it is behind a virtual network
  or firewall? **No**, not directly."*
  ([FAQ de Document Intelligence](https://learn.microsoft.com/azure/ai-services/document-intelligence/faq?view=doc-intel-4.0.0))
- *"Shared access signatures cannot be used to access firewall-protected storage."*

La excepción del firewall (trusted services o resource instance rule) se concede en función de la
**identidad administrada** del recurso, no de su IP, y una petición autenticada con SAS no la
cumple. Las IP de salida de DI no son públicas ni estables, así que una regla IP tampoco sirve.
Quedan dos caminos, y **ambos exigen tocar código**:

| Vía | Qué implica | Por qué no se elige ahora |
| --- | --- | --- |
| Identidad administrada | MI de DI + `Storage Blob Data Reader` + resource instance rule + **quitar el SAS de la URL** | Requiere permisos `roleAssignments/write` sobre el storage y coordinación con redes. No es más rápido, porque también lleva cambio de código |
| **Inline (esta spec)** | Descargar el blob en el Function App y mandar `base64Source` | El Function App ya alcanza el storage por private endpoint. Cero cambios de red, cero exposición |

### El síntoma que lo hizo visible

Ejecución `991dbe19-99a5-4c5d-9ff0-f3b3e4eb3d7d` (dev, 2026-08-09 21:35, tipología `prpe.09`):
las tres fuentes de texto se agotaron —clasificación saltada por `ExpectedType`, extracción
deshabilitada en la tipología, layout caído por lo anterior— y el prompt se envió igualmente con
el hueco `{contenido}` vacío. El modelo respondió *"No has adjuntado el documento"* y esos 2.372
caracteres se persistieron como resumen, con `EstadoFinal = OK` y `ConfianzaGlobal = 1.0`.

El defecto está en `OpenAIPromptDataProvider.BuildUserMessage:380-383`: sin markdown y sin base64
utilizable hace `contenido = string.Empty` y manda el prompt igual, sin guarda ni error.

Arreglar solo el transporte cierra *esta* causa. Cualquier otra que deje a DI sin texto —cuota,
timeout, PDF corrupto, que ya ocurre— volvería a producir un resumen falso. Por eso las dos piezas
van juntas.

## Decisiones de diseño (acordadas)

| Decisión | Valor |
| --- | --- |
| Mecanismo de activación | App setting por entorno, no autodetección ni cambio incondicional |
| Ámbito | Los tres providers DI: Layout, Clasificar y Extraer |
| Dónde vive el flag | App settings (`DocumentIntelligence__UseInlineContent`), **no** en `ModeloConfigs` |
| Valores | dev = `true`, pre = `true`, pro = `false` |
| Qué pasa sin texto del documento | **Fallo duro**: no se llama al LLM, actividad en `Failed`, ejecución en estado de error |
| Reintento ante `InvalidContent` | Se extiende de Clasificar a los tres, como red de seguridad |
| Límite de tamaño para volver a `urlSource` | **No se implementa**. Solo se registra el tamaño al ir inline |

### Justificación de las dos menos obvias

**El flag en app settings y no en `ModeloConfigs`.** El origen del documento es una decisión de
transporte, no del modelo. En BD habría que replicarla en tres filas y podría divergir entre ellas;
como app setting es una sola variable por entorno, visible en el pipeline y auditable.

**Sin límite de tamaño.** Con el flag activo solo en dev y pre, y documentos que hoy son de
kilobytes, un umbral sería una condición sin caso de uso y habría que elegir el valor a ciegas.
El resolutor registrará el tamaño en cada envío inline; si algún día hay presión de memoria, el
umbral se elegirá con datos. DI en S0 admite 500 MB por documento, así que el límite real lo pone
la memoria del worker, no el servicio.

## Alcance

**Dentro:**

1. App setting nuevo, contrato canónico y variables de pipeline por entorno.
2. Resolutor compartido del origen del documento para peticiones a DI.
3. Los tres providers DI pasan a usarlo.
4. Reintento ante 400 `InvalidContent` en los tres providers.
5. Guarda de contenido vacío antes de llamar al LLM.
6. Estado de ejecución nuevo y su representación en el monitor.
7. Pruebas unitarias de lo anterior.

**Fuera:**

- La vía de identidad administrada (queda documentada arriba como alternativa futura).
- Cualquier cambio en la configuración de red de los storage.
- El comportamiento de PRO, que conserva `urlSource`.
- La causa por la que `prpe.09` no tiene extracción habilitada, que es configuración de tipología.

## Diseño

### 1. Configuración

Nuevo binding `DocumentIntelligence:UseInlineContent` (bool, default `false`), expuesto como app
setting `DocumentIntelligence__UseInlineContent`.

- Se declara en `scripts/config/azure-appsettings-contract.json` como no secreto, owner pipeline.
- `azure-pipelines.yml` lo fija por entorno con la variable `DI_USE_INLINE_CONTENT`:
  dev = `true`, pre = `true`, pro = `false`.

El default `false` es deliberado: si el setting falta, el comportamiento es el actual.

> **Pendiente de confirmar antes de implementar:** que el storage de documentos de PRO es
> alcanzable públicamente. Se asume que sí porque hoy funciona, pero no se ha verificado (las
> lecturas de producción quedaron fuera de esta sesión). Si resultara estar cerrado, PRO también
> necesitaría `true` y el flag dejaría de tener sentido como distinción por entorno.

### 2. Resolutor compartido

Clase nueva `DocumentIntelligenceSourceResolver` en `src/backend/DocumentIA.Functions/Services/`.
Es la pieza que hoy no existe y la razón de que los tres providers hayan divergido.

```csharp
public sealed record DiSource(object Body, bool UsingUrlSource);

Task<DiSource> ResolveAsync(
    string? blobPath,
    string? base64Override,   // contenido que pisa al blob (hoy solo lo usa Clasificar)
    string? base64Entrada,    // contenido que trae la petición; vacío en modo blob-first
    CancellationToken cancellationToken);
```

Hacen falta **dos** parámetros de contenido, no uno, porque tienen precedencias distintas frente al
blob: el override gana al `blobPath`, mientras que el contenido de la entrada solo se usa cuando no
hay `blobPath`. Colapsarlos en un único parámetro cambiaría el comportamiento actual de Clasificar.

Reglas, evaluadas en orden:

| # | Condición | Resultado |
| --- | --- | --- |
| 1 | `base64Override` no vacío | `base64Source(base64Override)` |
| 2 | `blobPath` vacío | `base64Source(base64Entrada)` |
| 3 | `UseInlineContent = true` | Descarga el blob → `base64Source` |
| 4 | El SAS resultante es loopback | Descarga el blob → `base64Source` |
| 5 | Resto | `urlSource` con el SAS |

Si se llega a la regla 2 con `base64Entrada` vacío, el resolutor devuelve `base64Source` vacío
igual que hoy; detectar esa situación no es su responsabilidad, sino de la guarda de la sección 4.

Las reglas 1, 2 y 4 ya existen hoy, dispersas entre providers; la 3 es la nueva.

La regla 4 **no interviene en dev ni pre**: `IsLoopbackUrl` solo devuelve true para loopback,
`localhost` o `127.0.0.1` (`AzureDocumentIntelligenceClasificarProvider.cs:250-260`), y en esos
entornos el SAS apunta al FQDN real del storage. Se conserva porque es lo que permite desarrollar
en local contra Azurite; perderla al unificar sería una regresión.

Dependencias: `IBlobStorageService` y `IOptions<DocumentIntelligenceSettings>`. No conoce HTTP, lo
que permite probarlo aislado.

Al aplicar la regla 3 o la 4 se registra el tamaño en bytes del documento descargado.

### 3. Providers

Los tres pasan a construir el cuerpo con el resolutor:

| Provider | Línea del SAS actual |
| --- | --- |
| `AzureDocumentIntelligenceLayoutMarkdownProvider` | 57 |
| `AzureDocumentIntelligenceClasificarProvider` | 46 |
| `AzureDocumentIntelligenceExtraerDataProvider` | 67 |

Además, el reintento ante 400 `InvalidContent` que hoy solo tiene Clasificar
(`AzureDocumentIntelligenceClasificarProvider.cs:76-88`) se extiende a Layout y Extraer: si se usó
`urlSource` y DI responde 400 `InvalidContent`, se descarga el blob y se repite la llamada con
`base64Source`. Es una red de seguridad, no el mecanismo principal: protege el caso de que alguien
cierre un storage sin tocar el flag.

Consecuencia medible en dev: hoy Clasificar funciona pagando una llamada fallida por documento
(el reintento ha saltado 20 veces entre el 1 y el 6 de agosto). Con el flag activo, esa llamada
desaparece.

### 4. Guarda de contenido vacío

En `OpenAIPromptDataProvider`, antes de construir el mensaje y llamar al modelo: si no hay markdown
**ni** base64 utilizable, no se invoca al LLM y se devuelve `PromptResultado` con `Error`.

Aplica a los dos caminos: prompt libre y resumen dedicado (`ForzarResumenPorDefecto`). En
particular, el resumen de respaldo `BuildFallbackSummary` no debe usarse para tapar la ausencia de
contenido.

En el orquestador, `EjecutarPromptLibreAsync` detecta ese error y:

- marca la actividad `Prompt` en `Failed`, con el motivo,
- fija `salida.Resultado.Estado = "SIN_CONTENIDO_DOCUMENTO"`,
- no escribe `DatosExtraidos["Resumen"]` ni `DatosExtraidos["ResultadoPrompt"]`.

El valor sigue la convención existente del campo (`OK`, `DUPLICADO`, `NO_CLASIFICADO`,
`BAJA_CONFIANZA_CLASIFICACION`, `VALIDACION_CON_ERRORES`, `PENDIENTE_REINTENTO`).

**Detalle de implementación que no es opcional.** Hoy la llamada va envuelta en
`EjecutarPasoNegocio`, que marca `Completed` cuando la actividad devuelve sin excepción
(`DocumentProcessOrchestrator.cs:261-276`). Como la guarda devuelve un resultado con `Error` y
**no** lanza, la actividad se marcaría `Completed` y añadiría `Prompt` a
`seguimiento.ActividadesCompletadas`. Marcarla después como `Failed` sobrescribe el estado pero
**no la quita de esa lista** (`MarcarFinActividad:221-225` solo añade), dejando el contrato
incoherente: `Estado = Failed` y a la vez presente entre las completadas.

Por eso el paso `Prompt` deja de usar `EjecutarPasoNegocio` y pasa a marcar su estado
explícitamente: `MarcarInicioActividad`, inspección del resultado, y una única llamada a
`MarcarFinActividad` con `Completed` o `Failed`. El estado se decide una sola vez.

Tampoco vale lanzar una excepción desde la actividad: `EjecutarPasoNegocio` la relanza, y eso
abortaría la orquestación antes de `Persistir`, con lo que no se guardaría nada — ni siquiera la
clasificación, que sí es válida. El objetivo es persistir la ejecución **marcada como fallida**, no
perderla.

### 5. Monitor

`MonitorTabla.razor:135-141` mapea a color solo `OK`, `REVISION` y `ERROR`; todo lo demás cae en
`bg-secondary` (gris). Se añade `SIN_CONTENIDO_DOCUMENTO` a la rama `bg-danger` para que un fallo
de este tipo se vea como fallo y no como estado desconocido.

## Pruebas

**Resolutor** (`DocumentIA.Tests.Unit`, nuevo fichero): un caso por regla, cinco en total, con
`IBlobStorageService` simulado. Se verifica el cuerpo generado y el flag `UsingUrlSource`, y que
la descarga del blob solo ocurre en las reglas 3 y 4.

**Guarda:** que con markdown y base64 vacíos **no** se invoca al cliente del LLM y se devuelve
`Error`. Es la aserción central: comprobar solo el valor devuelto dejaría pasar una regresión en la
que se llama al modelo y luego se descarta la respuesta.

**Orquestador** (`DocumentProcessOrchestratorTests`, que ya tiene el patrón): sin texto disponible,
la ejecución cierra en `SIN_CONTENIDO_DOCUMENTO`, la actividad `Prompt` queda en `Failed` y no se
persiste resumen.

**Reintento:** ante un 400 `InvalidContent` con `urlSource`, el provider repite con `base64Source`.
Un caso por provider.

## Riesgos

**Cambian las métricas de operación.** Ejecuciones que hoy cierran `OK` pasarán a cerrar en error.
Es el comportamiento correcto, pero altera los paneles y cualquier informe que cuente ejecuciones
OK. Antes de desplegar conviene medir cuántas ejecuciones históricas caerían en el nuevo estado, y
avisar a quien consuma esos datos.

**Memoria del worker.** Con transporte inline el documento pasa por el Function App: descarga a
memoria más la cadena base64, que añade un tercio sobre el tamaño original. Acotado porque el flag
solo se activa en dev y pre, y mitigado por el registro de tamaños que permitirá decidir si algún
día hace falta un umbral.

**PRO no queda cubierto.** Con el flag en `false`, el camino inline no se ejercita en producción.
Si en el futuro se cierra el storage de PRO, el reintento ante `InvalidContent` evitará la rotura,
pero a costa de una llamada fallida por documento hasta que se active el flag.

**Estado nuevo en consumidores externos.** El valor `SIN_CONTENIDO_DOCUMENTO` viaja en el contrato
de salida. Hay que revisar si algún cliente batch discrimina por `EstadoFinal` con una lista
cerrada de valores.

## Verificación

1. Reprocesar en dev el documento de la ejecución `991dbe19` con `ForceReprocess` y comprobar que
   `Postproceso.Markdown` no es nulo y que el resumen contiene datos del documento.
2. Comprobar en logs que no aparece ningún `InvalidContent` de DI en ese reproceso.
3. Forzar un caso sin texto (tipología con extracción deshabilitada y layout inaccesible) y
   comprobar que la ejecución cierra en `SIN_CONTENIDO_DOCUMENTO`, sin resumen persistido, y que el
   monitor la pinta en rojo.
4. Ejecutar un documento en local con Azurite para confirmar que la regla 4 sigue viva.

## Referencias

- Ejecución con el síntoma: `991dbe19-99a5-4c5d-9ff0-f3b3e4eb3d7d` (dev, Id 7715).
- `docs/superpowers/specs/` — specs previas del proyecto.
- [Managed identities for Document Intelligence](https://learn.microsoft.com/azure/ai-services/document-intelligence/authentication/managed-identities?view=doc-intel-4.0.0)
  — alternativa descartada para esta iteración.
