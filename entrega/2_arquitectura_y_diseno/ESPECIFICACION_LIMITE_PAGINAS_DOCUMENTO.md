# Especificación Funcional — Límite de Páginas por Documento

**Versión:** 1.0  

---

## 1. Contexto y Motivación

Actualmente la clasificación limita el número de páginas enviadas al modelo, pero la extracción y el resto de la orquestación procesan el documento completo. Un documento de 200 páginas tiene el mismo coste de clasificación que uno de 10, pero un coste de extracción drásticamente mayor. Sin un límite explícito, documentos grandes pueden consumir cuota de CU de forma desproporcionada, elevar costes y degradar el rendimiento del batch completo.

---

## 2. Definición de las Propiedades Nuevas

### 2.1 `MaxPaginasDocumento` — Nivel de configuración global o de tipología

- **Tipo:** entero positivo opcional
- **Ubicación:** configuración global (`appsettings` / tabla de configuración) y/o JSON de tipología
- **Semántica:** define el máximo de páginas que un documento puede tener para ser procesado más allá de la clasificación. Si el documento supera este valor, el pipeline se detiene tras la clasificación con un resultado controlado.
- **Valor 0 o ausente:** no se aplica límite.

### 2.2 `ForzarProcesadoSinLimitePaginas` — Nivel de instrucción de ejecución

- **Tipo:** booleano opcional en el input de la orquestación
- **Default:** `false`
- **Semántica:** cuando es `true`, el sistema omite la validación de páginas y procesa el documento completo, asumiendo que el llamador es consciente del coste y la decisión es deliberada.

---

## 3. Flujo Funcional Detallado

### 3.1 Momento de la evaluación

La validación de páginas ocurre **después de la clasificación** y después de que el sistema conoce el número de páginas del documento. No se puede evaluar antes porque el número de páginas se obtiene durante el procesado inicial. El punto exacto es la transición entre la fase de clasificación/resumen y el inicio de la extracción.

### 3.2 Condición de bloqueo

El bloqueo se activa si se cumplen **las tres condiciones simultáneamente**:

1. `MaxPaginasDocumento` está configurado y es mayor que 0
2. El número de páginas del documento supera `MaxPaginasDocumento`
3. `ForzarProcesadoSinLimitePaginas` es `false` o no está presente

Si cualquiera de las tres no se cumple, el pipeline continúa con normalidad.

### 3.3 Resultado cuando se bloquea

El orquestador **no lanza un error no controlado**. Devuelve un resultado de ejecución con estado específico, distinto de `OK` y distinto de `ERROR`:

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
| Ingesta y subida a blob | Sí |
| Verificación de duplicado | Sí |
| Clasificación | Sí |
| Resumen general | Sí |
| **Validación del límite de páginas** | Sí — aquí se detiene |
| Extracción (CU / GPT) | No |
| Validación de campos | No |
| Integración / plugins | No |
| Persistencia GDC | No |
| Persistencia en SQL | Sí — con estado `PAGINAS_EXCEDIDAS` |

La clasificación y el resumen se persisten con normalidad. El documento queda registrado con la tipología identificada y el motivo de no procesado.

---

## 4. Comportamiento de `ForzarProcesadoSinLimitePaginas`

Cuando el llamador envía `ForzarProcesadoSinLimitePaginas = true`:

- La validación de páginas se omite completamente
- El documento se procesa en su totalidad igual que hoy
- Se registra en telemetría y en el log que el límite fue omitido deliberadamente, incluyendo el número de páginas y quién lo solicitó
- El estado final es `OK` o `ERROR` según el resultado normal del pipeline

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

### Configuración global (`appsettings` o tabla de configuración):

```json
{
  "Pipeline": {
    "MaxPaginasDocumento": 50
  }
}
```

### Configuración por tipología (JSON de tipología):

```json
{
  "tipologia": "ESCRITURA_NOTARIAL",
  "maxPaginasDocumento": 20,
  "fields": []
}
```

### Input de orquestación con bypass:

```json
{
  "documentoId": "abc123",
  "tipologia": "ESCRITURA_NOTARIAL",
  "forzarProcesadoSinLimitePaginas": true
}
```

---

## 7. Impacto en Componentes Existentes

| Componente | Cambio requerido |
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
6. La clasificación y el resumen del documento bloqueado se persisten correctamente.
7. El estado `PAGINAS_EXCEDIDAS` es distinguible de `OK` y de `ERROR` en SQL, en la respuesta HTTP y en Application Insights.
