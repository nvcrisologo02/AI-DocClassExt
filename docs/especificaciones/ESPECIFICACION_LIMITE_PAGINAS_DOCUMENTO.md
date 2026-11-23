# Especificación Funcional — Límite de Páginas por Documento

**Versión:** 1.1  
**Fecha:** 2026-05-28  
**Estado:** Implementada (commit `fbf33519`, 2026-05-28). No se aplica en la rama `classificationOnly`. En los entornos desplegados el límite global está sin fijar (0 = sin límite); los límites por tipología dependen de la `ConfiguracionJson` de BD  

> Última revisión: 2026-11-22 (AB#101447, contraste con el orquestador (Paso 3.9), `PipelineSettings`, `TipologiaValidationConfig`, el contrato de entrada y CONTRATO_API_HTTP §5). Hallazgos: el límite se aplica en línea en el orquestador tras clasificar y resolver la tipología; `ApplyPdfPageLimitActivity` no interviene (pertenece al recorte de `classificationOnly` con `maxPagesForClassificationOnly`, está declarada y no se invoca); `classificationOnly` retorna antes del Paso 3.9, por lo que el límite no se aplica en esa rama; `Pipeline__MaxPaginasDocumento` no lo fija el pipeline de despliegue (defecto 0); en el bloqueo se omiten Extraer, Prompt, Validar, ObtenerActivo, Integrar y SubirGDC, se fija `EstadoCalidad = "REVISION"`, `ConfianzaExtraccion = 0`, `ConfianzaValidacion = 1.0` y se persiste; el resumen solo existe si lo produjo la propia clasificación. Comprobado el 2026-11-23 (AB#101449): ninguna tipología activa fija `maxPaginasDocumento` en DEV, PRE ni PRO; los work items 99683-99697 están en Done; `CATALOGO_APP_SETTINGS.md` existe solo en `docs/auxiliares/auxiliares-sin-uso/`. El recuento de páginas en formatos no PDF se deja en el código (`PdfRecorteService`), sin comprobación en vivo.

---

## 1. Contexto y Motivación

Antes de esta funcionalidad, la clasificación limitaba el número de páginas enviadas al modelo, pero la extracción y el resto de la orquestación procesaban el documento completo. Un documento de 200 páginas tiene el mismo coste de clasificación que uno de 10, pero un coste de extracción drásticamente mayor. Sin un límite explícito, documentos grandes pueden consumir cuota de CU de forma desproporcionada, elevar costes y degradar el rendimiento del batch completo.

---

## 2. Definición de las Propiedades Nuevas

### 2.1 `MaxPaginasDocumento` — Nivel de configuración global o de tipología

- **Tipo:** entero positivo opcional
- **Ubicación:** sección `Pipeline` (app setting `Pipeline__MaxPaginasDocumento`, clase `PipelineSettings`, default 0; no hay tabla de configuración) y/o `maxPaginasDocumento` en la `ConfiguracionJson` de la tipología (`TipologiaValidationConfig.MaxPaginasDocumento`)
- **Semántica:** define el máximo de páginas que un documento puede tener para ser procesado más allá de la clasificación. Si el documento supera este valor, el pipeline se detiene tras la clasificación con un resultado controlado.
- **Valor 0 o ausente:** no se aplica límite.

### 2.2 `ForzarProcesadoSinLimitePaginas` — Nivel de instrucción de ejecución

- **Tipo:** booleano opcional, `instrucciones.forzarProcesadoSinLimitePaginas` del contrato de entrada
- **Default:** `false`
- **Semántica:** cuando es `true`, el sistema omite la validación de páginas y procesa el documento completo, asumiendo que el llamador es consciente del coste y la decisión es deliberada.

---

## 3. Flujo Funcional Detallado

### 3.1 Momento de la evaluación

La validación de páginas ocurre **después de la clasificación** y después de que el sistema conoce el número de páginas del documento. `NormalizarActivity` calcula las páginas del PDF al inicio y `PrepararDocumentoClasificacionActivity` las completa si son 0; la comprobación se hace en el Paso 3.9 del orquestador, tras clasificar y resolver la tipología, porque necesita su `MaxPaginasDocumento` (el recuento de páginas lo hace `PdfRecorteService` sobre PDF; en otros formatos depende del código, sin comprobación en vivo). No se puede evaluar antes porque el número de páginas se obtiene durante el procesado inicial. El punto exacto es la transición entre la fase de clasificación/resumen y el inicio de la extracción.

### 3.2 Condición de bloqueo

El bloqueo se activa si se cumplen **las tres condiciones simultáneamente**:

1. `MaxPaginasDocumento` está configurado y es mayor que 0
2. El número de páginas del documento supera `MaxPaginasDocumento`
3. `ForzarProcesadoSinLimitePaginas` es `false` o no está presente

Si cualquiera de las tres no se cumple, el pipeline continúa con normalidad.

### 3.3 Resultado cuando se bloquea

El orquestador **no lanza un error no controlado**. Devuelve un resultado de ejecución con estado específico, distinto de `OK` y distinto de `ERROR` (con `EstadoCalidad = "REVISION"`, `ConfianzaExtraccion = 0`, `ConfianzaValidacion = 1.0`; la instancia Durable termina en `Completed`):

**Estado:** `PAGINAS_EXCEDIDAS`

**Mensaje de resultado:**

```
El documento no ha sido procesado porque supera el límite de páginas configurado.
Páginas del documento: {N}. Límite configurado: {MaxPaginasDocumento}.
Para forzar el procesado use ForzarProcesadoSinLimitePaginas = true en la solicitud.
```

### 3.4 Fases ejecutadas vs. bloqueadas

| Fase | Se ejecuta |
|---|---|
| Ingesta y subida a blob | ✅ Sí |
| Verificación de duplicado | ✅ Sí |
| Clasificación | ✅ Sí |
| Resumen general | Solo si lo generó la propia clasificación (`ResumenCombinado` de TDN1); el prompt libre y el resumen dedicado se omiten |
| **Validación del límite de páginas** | ✅ Sí — aquí se detiene |
| Extracción (CU / GPT) | ❌ No |
| Validación de campos | ❌ No |
| Integración / plugins | ❌ No |
| Persistencia GDC | ❌ No |
| Persistencia en SQL | ✅ Sí — con estado `PAGINAS_EXCEDIDAS` |

La clasificación se persiste con normalidad (y el resumen, solo si ya existía). El documento queda registrado con la tipología identificada y el motivo de no procesado.

---

## 4. Comportamiento de `ForzarProcesadoSinLimitePaginas`

Cuando el llamador envía `ForzarProcesadoSinLimitePaginas = true`:

- La validación de páginas se omite completamente
- El documento se procesa en su totalidad igual que hoy
- Se registra en el log (`[AVISO] Límite de páginas omitido por ForzarProcesadoSinLimitePaginas=true…`, con documento, páginas y límite) que el límite fue omitido deliberadamente; el solicitante queda en `trazabilidad.submittedBy` de la ejecución persistida. No hay evento de telemetría específico (criterio 7: comprobado el 2026-11-23, el código no emite ningún evento ni métrica de telemetría sobre el límite de páginas)
- El estado final es el que corresponda al flujo normal del pipeline (`OK`, `VALIDACION_CON_ERRORES`, `ERROR`…)

Esta traza es **obligatoria para auditoría de coste**: permite identificar a posteriori qué ejecuciones omitieron el límite y cuántas páginas procesaron.

**Log esperado:**

```
[AVISO] Límite de páginas omitido por ForzarProcesadoSinLimitePaginas=true.
Documento: {nombre}. Páginas: {N}. Límite configurado: {MaxPaginasDocumento}.
```

---

## 5. Niveles de Configuración y Precedencia

El límite puede configurarse en dos niveles, con la siguiente **precedencia de mayor a menor**:

1. **Por tipología** — cada tipología puede tener su propio `MaxPaginasDocumento`
2. **Global** — valor por defecto aplicable a todas las tipologías que no definan el suyo propio

- Si una tipología define `MaxPaginasDocumento`, ese valor prevalece sobre el global.
- Si la tipología no lo define, se usa el global.
- Si ninguno está definido, no hay límite.

---

## 6. Ejemplos de Configuración

### Configuración global (app setting `Pipeline__MaxPaginasDocumento`; el host no carga `appsettings.json`):

```json
{
  "Pipeline": {
    "MaxPaginasDocumento": 50
  }
}
```

### Configuración por tipología (`ConfiguracionJson.maxPaginasDocumento`; el ejemplo siguiente es ilustrativo, no el esquema real):

```json
{
  "tipologia": "ESCRITURA_NOTARIAL",
  "maxPaginasDocumento": 20,
  "fields": []
}
```

### Input de orquestación con bypass (fragmento real del contrato de entrada):

```json
{
  "documento": { "nombre": "escritura.pdf", "blobPath": "..." },
  "instrucciones": { "forzarProcesadoSinLimitePaginas": true }
}
```

---

## 7. Impacto en Componentes Existentes (implementados: `TipologiaValidationConfig.cs`, `PipelineSettings.cs`, `ContratoEntrada.cs`, `DocumentProcessOrchestrator.cs` Paso 3.9; el estado se guarda como cadena en `Resultado.Estado`, sin cambio de esquema)

| Componente | Cambio requerido (planificación original) |
|---|---|
| Modelo de configuración de tipología | Añadir `maxPaginasDocumento` entero opcional |
| Configuración global (`appsettings` / tabla) | Añadir `Pipeline:MaxPaginasDocumento` |
| Input del orquestador | Añadir `forzarProcesadoSinLimitePaginas` booleano opcional |
| Orquestador Durable | Añadir paso de validación de páginas tras clasificación, antes de extracción |
| Modelo de resultado de ejecución | Añadir estado `PAGINAS_EXCEDIDAS` |
| Persistencia SQL | Soportar nuevo estado y guardar páginas del documento |
| Telemetría / logging | Registrar bloqueo y bypass con páginas reales y límite aplicado |
| Respuesta HTTP al llamador | Reflejar estado `PAGINAS_EXCEDIDAS` con mensaje descriptivo |
| JSON de tipologías en `config/` | Añadir `maxPaginasDocumento` donde proceda |

---

## 8. Criterios de Aceptación

1. Un documento de 60 páginas con límite configurado a 50 no ejecuta la extracción y devuelve estado `PAGINAS_EXCEDIDAS` con el mensaje descriptivo.
2. El mismo documento con `ForzarProcesadoSinLimitePaginas = true` se procesa completamente y deja traza de que el límite fue omitido.
3. Una tipología con `maxPaginasDocumento = 20` bloquea a 20 páginas aunque el límite global sea 50.
4. Una tipología sin `maxPaginasDocumento` usa el límite global.
5. Si no hay límite global ni de tipología, todos los documentos se procesan sin restricción igual que hoy.
6. La clasificación del documento bloqueado se persiste correctamente (el resumen, solo si ya existía: ver §3.4).
7. El estado `PAGINAS_EXCEDIDAS` es distinguible de `OK` y de `ERROR` en SQL, en la respuesta HTTP y en Application Insights.

---

## 9. Work Items ADO Asociados

> Proyecto **AI DocClassExt** — org **https://sareb.visualstudio.com**

| ID | Nivel | Título |
|---|---|---|
| [99683](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99683) | Epic | Control de costes y límites operativos del pipeline |
| [99684](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99684) | Feature | Límite de páginas por documento |
| [99685](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99685) | Historia | HU1 – Configuración del límite de páginas a nivel global y por tipología |
| [99686](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99686) | Historia | HU2 – Validación del límite en el orquestador Durable tras clasificación |
| [99687](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99687) | Historia | HU3 – Estado `PAGINAS_EXCEDIDAS` en modelo de resultado, SQL y respuesta HTTP |
| [99688](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99688) | Historia | HU4 – Bypass `ForzarProcesadoSinLimitePaginas` con traza de auditoría |
| [99689](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99689) | Historia | HU5 – Telemetría y logging de bloqueo y bypass de límite de páginas |
| [99690](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99690) | Task | Añadir `maxPaginasDocumento` (int opcional) al modelo de configuración de tipología |
| [99691](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99691) | Task | Añadir `Pipeline:MaxPaginasDocumento` a `appsettings` y `CATALOGO_APP_SETTINGS.md` |
| [99692](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99692) | Task | Añadir `maxPaginasDocumento` a los JSON de tipologías en `config/` |
| [99693](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99693) | Task | Implementar paso de validación de páginas en el orquestador Durable |
| [99694](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99694) | Task | Añadir `forzarProcesadoSinLimitePaginas` (bool opcional) al input del orquestador |
| [99695](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99695) | Task | Añadir estado `PAGINAS_EXCEDIDAS` al modelo de resultado de ejecución |
| [99696](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99696) | Task | Actualizar persistencia SQL con estado `PAGINAS_EXCEDIDAS` y páginas del documento |
| [99697](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99697) | Task | Actualizar respuesta HTTP para reflejar `PAGINAS_EXCEDIDAS` con mensaje descriptivo |
