# Guía de Uso — Clasificación Restringida a un Conjunto de Tipologías

> **Proyecto:** AI DocClassExt — SAREB
> **Audiencia:** Integradores de sistemas, usuarios técnicos
> **Relacionado con:** [CONTRATO_API_HTTP.md](../2_arquitectura_y_diseno/CONTRATO_API_HTTP.md) (contrato formal de `instrucciones.restriccionTipologias`) y [GUIA_CLASIFICACION_DOCUMENTOS.md](GUIA_CLASIFICACION_DOCUMENTOS.md) (guía general de clasificación, §4.5)

---

## Tabla de Contenidos

1. [Qué es y cuándo usarla](#1-qué-es-y-cuándo-usarla)
2. [Contrato de la petición](#2-contrato-de-la-petición)
3. [Qué devuelve](#3-qué-devuelve)
4. [Validaciones y errores](#4-validaciones-y-errores)
5. [Requisitos del catálogo](#5-requisitos-del-catálogo)
6. [Configuración avanzada: prompts editables](#6-configuración-avanzada-prompts-editables)
7. [Limitaciones y comportamiento aguas abajo](#7-limitaciones-y-comportamiento-aguas-abajo)
8. [Solución de problemas](#8-solución-de-problemas)

---

## 1. Qué es y cuándo usarla

La clasificación restringida acota el catálogo de clasificación a un **conjunto cerrado de tipologías candidatas** elegido por el caller en cada petición, mediante `instrucciones.restriccionTipologias`. En lugar de comparar el documento contra el catálogo completo publicado, el sistema solo considera las tipologías indicadas.

La semántica es **estricta**: el clasificador solo puede devolver una tipología del conjunto informado o el centinela `"Desconocido"`. Nunca fuerza el resultado a una tipología fuera de la lista, aunque esa fuera la clasificación "natural" del documento.

**Cuándo usarla:**

- El sistema origen ya sabe, por contexto de negocio, que un lote de documentos solo puede pertenecer a un subconjunto acotado de tipos (por ejemplo, documentación asociada a un expediente concreto que solo admite ciertos tipos documentales).
- Se quiere una señal explícita de "no encaja en ninguna de las candidatas" en lugar de que el sistema fuerce una clasificación fuera de ese conjunto.
- Se quiere, opcionalmente, una **propuesta informativa** de qué podría ser el documento cuando no encaja en ninguna candidata, sin que esa propuesta se use para nada aguas abajo.

**Cuándo NO usarla:** si no se conoce de antemano un conjunto acotado de tipos esperados, la clasificación automática normal (catálogo completo, ver GUIA_CLASIFICACION_DOCUMENTOS.md §4.1–§4.3) es la opción correcta.

---

## 2. Contrato de la petición

La restricción viaja en `instrucciones.restriccionTipologias` dentro del `ContratoEntrada` estándar del endpoint `POST /api/IngestDocument`. Requiere `instrucciones.classification.nivelClasificacion = "TDN1_TDN2"` (la restricción opera a granularidad de tipología, no de familia TDN1 — ver [§4](#4-validaciones-y-errores)).

| Campo | Tipo | Descripción |
|---|---|---|
| `restriccionTipologias.codigos` | string[] | Códigos de tipología permitidos (columna `Codigo` de tipologías publicadas, ej. `"acui.02"`, `"nota-simple"`). Comparación case-insensitive con trim; el backend normaliza a la forma canónica de BD. |
| `restriccionTipologias.proponerSiDesconocido` | bool | `true` = si el resultado final es `"Desconocido"`, se añade una propuesta informativa. Default: `false`. |

Para el resto de campos de la petición (`documento`, `trazabilidad`, autenticación, modo multipart vs. base64 inline) aplica el contrato general — ver [CONTRATO_API_HTTP.md §3](../2_arquitectura_y_diseno/CONTRATO_API_HTTP.md#3-endpoint-de-ingesta). La restricción no cambia nada de esas secciones: el documento se envía igual, ya sea inline en `documento.content.base64` o como `multipart/form-data` con las partes `file` + `metadata`.

### Ejemplo 1 — Restricción simple

```json
{
  "instrucciones": {
    "classification": { "nivelClasificacion": "TDN1_TDN2" },
    "restriccionTipologias": {
      "codigos": ["acui.02", "acui.08", "fich.24"]
    }
  },
  "documento": {
    "name": "acta_comite.pdf",
    "content": { "base64": "<contenido-en-base64>" }
  },
  "trazabilidad": {
    "correlationId": "a1b2c3d4-0000-0000-0000-000000000000",
    "submittedBy": "sistema-origen"
  }
}
```

### Ejemplo 2 — Con propuesta ante Desconocido

```json
{
  "instrucciones": {
    "classification": { "nivelClasificacion": "TDN1_TDN2" },
    "restriccionTipologias": {
      "codigos": ["acui.02", "acui.08", "fich.24"],
      "proponerSiDesconocido": true
    }
  },
  "documento": {
    "name": "informe_estado.pdf",
    "content": { "base64": "<contenido-en-base64>" }
  },
  "trazabilidad": {
    "correlationId": "a1b2c3d4-0000-0000-0000-000000000001",
    "submittedBy": "sistema-origen"
  }
}
```

### Ejemplo 3 — Con resumen documental forzado

`forzarResumenPorDefecto` se combina sin reglas especiales; el resumen se genera también cuando el resultado es `"Desconocido"`.

```json
{
  "instrucciones": {
    "classification": { "nivelClasificacion": "TDN1_TDN2" },
    "forzarResumenPorDefecto": true,
    "restriccionTipologias": {
      "codigos": ["acui.02", "acui.08", "fich.24"],
      "proponerSiDesconocido": true
    }
  },
  "documento": {
    "name": "documento_lote.pdf",
    "content": { "base64": "<contenido-en-base64>" }
  },
  "trazabilidad": {
    "correlationId": "a1b2c3d4-0000-0000-0000-000000000002",
    "submittedBy": "sistema-origen"
  }
}
```

### Multipart vs. base64

Para documentos grandes conviene usar el modo `multipart/form-data` (parte `file` binaria + parte `metadata` con el JSON de `ContratoEntrada`, incluyendo `restriccionTipologias` dentro de `instrucciones` igual que en los ejemplos anteriores) en lugar de `content.base64` inline. El comportamiento de la restricción es idéntico en ambos modos; el detalle del modo multipart/blob-first está en [CONTRATO_API_HTTP.md §3](../2_arquitectura_y_diseno/CONTRATO_API_HTTP.md#3-endpoint-de-ingesta).

---

## 3. Qué devuelve

### 3.1 Eco de la restricción aplicada

El resultado siempre incluye un eco de la restricción efectivamente aplicada en `detalleEjecucion.clasificacion.restriccionTipologias`:

```json
{
  "detalleEjecucion": {
    "clasificacion": {
      "restriccionTipologias": {
        "codigos": ["ACUI.02", "ACUI.08", "FICH.24"],
        "codigosIgnorados": null
      }
    }
  }
}
```

- `codigos`: los códigos efectivamente usados en la clasificación (ya normalizados a su forma canónica de BD).
- `codigosIgnorados`: códigos enviados por el caller que no correspondían a ninguna tipología publicada (`null` si todos eran válidos).

### 3.2 Resultado dentro del conjunto

Si el documento encaja en una de las tipologías candidatas, el resultado es indistinguible de una clasificación normal: `identificacion.tipologia` es el código de la tipología detectada, con su confianza y demás campos habituales.

### 3.3 Resultado `"Desconocido"`

Si ninguna tipología del conjunto encaja, el resultado final es:

```json
{
  "identificacion": {
    "tipologia": "Desconocido",
    "propuestaTipologia": "escritura-compraventa"
  },
  "detalleEjecucion": {
    "clasificacion": {
      "tipologiaDetectada": "Desconocido",
      "confianza": 0.0,
      "fallbackRazon": "fuera_de_conjunto_restringido",
      "restriccionTipologias": {
        "codigos": ["ACUI.02", "ACUI.08", "FICH.24"],
        "codigosIgnorados": null
      }
    }
  }
}
```

- `identificacion.propuestaTipologia` solo se informa cuando `proponerSiDesconocido = true`. Es una **propuesta libre** (texto/código, no necesariamente del catálogo formal), obtenida con el flujo jerárquico completo contra el catálogo entero — no contra el conjunto acotado. No se actúa sobre ella: no dispara extracción, GDC ni integración.
- En `detalleEjecucion.clasificacion.detalleProveedores` aparece una entrada con `proveedor = "propuesta_libre"` cuando la propuesta procede de una llamada adicional (o de un candidato ya descartado por el router durante la cadena normal).
- `confianza` de la propuesta viaja en esa misma entrada de `detalleProveedores`, no en `identificacion`.

### 3.4 Confianza reportada en la fase única

Cuando el resultado cae dentro del conjunto, la confianza es autoinformada por el modelo (`confianza` del JSON de respuesta, típicamente **0.85–0.95** cuando la `gptDescripcion` de la tipología acertada está bien escrita; ver [§5](#5-requisitos-del-catálogo)). Si el modelo no informa confianza, se usa `0.9` por defecto.

---

## 4. Validaciones y errores

El trigger HTTP valida la restricción antes de arrancar la orquestación. Errores `400 Bad Request`:

| Causa | Detalle |
|---|---|
| `codigos` vacío o ausente | `instrucciones.restriccionTipologias.codigos debe contener al menos un código de tipología.` |
| Todos los códigos enviados son inválidos | El mensaje detalla los códigos rechazados. Si solo **algunos** son inválidos, la petición **no** se rechaza: se descartan con aviso y viajan en `codigosIgnorados` (ver [§3.1](#31-eco-de-la-restricción-aplicada)). |
| Combinación con `nivelClasificacion = "TDN1"` | `instrucciones.restriccionTipologias requiere nivelClasificacion TDN1_TDN2 (no es compatible con TDN1)`. El nivel TDN1 devuelve familias, no tipologías, y no puede honrar una restricción expresada a granularidad de tipología. |

`expectedType` sigue siendo un hint independiente y puede convivir con `restriccionTipologias` sin regla cruzada entre ambos.

---

## 5. Requisitos del catálogo

### 5.1 Tipologías publicadas y activas

Solo participan en la restricción las tipologías con estado `Published` y activas. Un código correcto pero de una tipología en `Draft` o `Retired` se descarta como inválido igual que un código inexistente (aparece en `codigosIgnorados`).

### 5.2 Los códigos son de tipología, no de TDN2

`restriccionTipologias.codigos` espera códigos de la columna `Codigo` de **tipologías** (ej. `acui.02`, `fich.24`, `nota-simple`), **no** códigos de familia TDN2 (ej. `ACUI-02`). Enviar un código TDN2 no lo resuelve automáticamente a su tipología asociada: se descarta como código no reconocido y aparece en `codigosIgnorados`. Si esto reduce el conjunto a cero códigos válidos, la petición falla con `400`.

### 5.3 Cómo escribir buenas `gptDescripcion`

Con restricción activa, la vía GPT clasifica en **una única pasada** contra un catálogo plano: una línea por tipología permitida con su `gptDescripcion` completa (sin la jerarquía TDN1→TDN2 del flujo normal). La instrucción del modo restringido asume que el caller **garantiza** que el documento debería ser de uno de los tipos del conjunto — el modelo elige la tipología **más compatible** por contenido, incluso si el documento pudiera encajar de forma natural en otra categoría no listada. Solo responde `"Desconocido"` cuando el contenido no guarda ninguna relación razonable con ninguna de las candidatas.

Esta combinación (instrucción de "elige la más compatible" + comparación en fase única) hace que la **calidad de la `gptDescripcion`** de cada tipología del conjunto sea el factor que más determina el acierto:

- Descripciones **genéricas o circulares** (que repiten el nombre de la tipología sin describir contenido reconocible) producen `"Desconocido"` con alta frecuencia: el modelo no tiene señales de contenido con las que comparar el documento.
- Descripciones con **contenido concreto** —qué es, qué señales textuales lo identifican, y qué NO es (para descartar confusiones con tipologías vecinas)— producen aciertos con confianza 0.85–0.95.

El patrón recomendado es el mismo que ya usan las familias del catálogo TDN1 completo:

```
ES: <qué es el documento, en 1-2 frases concretas>.
SEÑALES: <términos, encabezados o estructuras que suelen aparecer en este tipo de documento>.
NO ES: <tipos de documento con los que se suele confundir, y por qué no lo es>.
DESEMPATE: <criterio adicional si hay ambigüedad frecuente con otra tipología del catálogo>.
```

**Ejemplo bueno** (tipología `acui.02`, "Aprobación/denegación de operación del Comité"):

```
ES: acta o certificado del Comité de Operaciones donde se aprueba, deniega o condiciona una
operación (venta, dación, quita) sobre un activo concreto.
SEÑALES: menciona "Comité", una fecha de sesión, un identificador de operación/activo y un
sentido de la decisión (aprobado/denegado/condicionado).
NO ES: un informe de tasación, un presupuesto de obra, ni una escritura notarial —aunque el
acta se refiera a una operación de compraventa, el documento en sí es el acta del comité, no
la escritura.
```

**Ejemplo malo y su consecuencia real:** una tipología con `gptDescripcion = "Checklist de acondicionamiento del activo"` sin más detalle capturó, en una prueba real, un "informe de estado inicial" de un inmueble que en realidad correspondía a otra tipología del conjunto — la descripción no daba pistas de contenido suficientes para diferenciarlos, y el modelo eligió por similitud superficial del título. Añadir un bloque `NO ES: informes de estado o diagnóstico previos a la intervención; el checklist se rellena DURANTE o DESPUÉS de la obra, no antes` corrige el falso positivo.

> Estas mismas recomendaciones aplican a cualquier tipología usada en clasificación por catálogo (fallback GPT normal, ver GUIA_CLASIFICACION_DOCUMENTOS.md §4.3). En el modo restringido su impacto es mayor porque no hay una fase de familia (TDN1) que actúe de primer filtro.

---

## 6. Configuración avanzada: prompts editables

Los prompts de la fase única restringida son plantillas configurables en `PromptTemplates`, editables desde el Admin igual que el resto de prompts de clasificación (ver [CONTRATO_API_HTTP.md §9.2.bis](../2_arquitectura_y_diseno/CONTRATO_API_HTTP.md#92bis-prompts-de-clasificación)):

| `PromptKey` | Contenido |
|---|---|
| `classification.restricted.system` | System prompt: rol del clasificador y la instrucción de "elige la más compatible / `Desconocido` solo si no hay relación razonable". |
| `classification.restricted.user` | Plantilla de usuario: contexto, catálogo plano (`{CATALOGO}`) y contenido del documento (`{DOCUMENT_TEXT}`). |

Ambas claves siguen el mismo ciclo de vida que el resto de `PromptTemplates` (borrador → activar → rollback; ver [CONTRATO_API_HTTP.md §9.2.bis](../2_arquitectura_y_diseno/CONTRATO_API_HTTP.md#92bis-prompts-de-clasificación)). Si ninguna de las dos filas está sembrada en BD, o si el par está incompleto (solo una de las dos), el sistema usa el fallback embebido en código para ambas — nunca mezcla una plantilla de BD con una de código para este par.

**Qué se puede editar:** tono, énfasis, ejemplos adicionales, redacción del rol del clasificador.

**Qué NO se puede editar:** la instrucción de formato de respuesta JSON (`{"tipologia": ..., "propuesta": ..., "resumen": ..., "confianza": ...}`) no vive en estas plantillas — se añade siempre desde código, después del texto editable. Es el contrato del parser de respuesta: si se pudiera editar desde BD, un cambio de redacción podría romper el parseo sin que el editor lo supiera.

---

## 7. Limitaciones y comportamiento aguas abajo

- **`"Desconocido"` no se persiste como tal ni aparece en el Monitor del Admin.** El resultado `"Desconocido"` por restricción solo viaja en la respuesta de la orquestación (polling del `instanceId`); no queda un registro navegable en el Monitor. Mejora pendiente.
- **Sin extracción, AssetResolver, GDC ni integración con `"Desconocido"`.** Igual que cualquier documento no clasificado: solo se permiten resumen documental y ejecución de prompt (si se solicitaron), nada más.
- **El resumen se genera igual con `"Desconocido"`.** Si la petición incluye `forzarResumenPorDefecto` (o `instrucciones.prompt`), se ejecuta independientemente del resultado de clasificación.
- **`.xlsm` no soportado.** El layout de Document Intelligence no procesa hojas de cálculo `.xlsm`; la clasificación se ejecuta sin texto (contexto vacío) y el resultado no es fiable para este formato.
- **`nivelClasificacion = "TDN1"` es incompatible** con la restricción (ver [§4](#4-validaciones-y-errores)): responde `400`.
- **La propuesta informativa (`proponerSiDesconocido`) no consume el conjunto acotado.** Se calcula contra el catálogo completo con el flujo jerárquico normal, no contra las tipologías candidatas.

---

## 8. Solución de problemas

| Síntoma | Causa probable | Acción |
|---|---|---|
| Casi todos los documentos del lote devuelven `"Desconocido"` a pesar de pertenecer claramente a las tipologías del conjunto | `gptDescripcion` de las tipologías del conjunto es genérica, circular, o describe el *nombre* de la tipología en vez de su *contenido* | Revisar y reescribir las `gptDescripcion` implicadas con el patrón `ES: / SEÑALES: / NO ES:` de [§5.3](#53-cómo-escribir-buenas-gptdescripcion). Confirmar con una muestra de documentos reales antes de desplegar a producción. |
| `codigosIgnorados` incluye códigos que "deberían" estar bien | Se enviaron códigos TDN2 (ej. `ACUI-02`) en vez de códigos de tipología (ej. `acui.02`), o la tipología no está `Published`/activa | Verificar la columna `Codigo` de la tipología en el Admin (no el código de familia TDN2); confirmar el estado de publicación. Ver [§5.1](#51-tipologías-publicadas-y-activas) y [§5.2](#52-los-códigos-son-de-tipología-no-de-tdn2). |
| Un tipo de documento concreto se clasifica sistemáticamente como otra tipología del conjunto (falso positivo) | La `gptDescripcion` de la tipología "imán" es demasiado amplia y captura documentos que en realidad pertenecen a otra candidata | Añadir un bloque `NO ES:` explícito a la `gptDescripcion` de la tipología que está capturando de más, nombrando el tipo de documento que se está confundiendo. Ver el ejemplo del checklist de acondicionamiento en [§5.3](#53-cómo-escribir-buenas-gptdescripcion). |
| `400 Bad Request` con "requiere nivelClasificacion TDN1_TDN2" | Se envió la restricción junto con `classification.nivelClasificacion = "TDN1"` | Cambiar a `"TDN1_TDN2"` o quitar `nivelClasificacion` (el valor por defecto ya es compatible). |
| `400 Bad Request` con "no contiene ningún código de tipología publicada" | Todos los códigos enviados son inválidos (no publicados, mal escritos, o son códigos TDN2) | Revisar la lista completa de `codigos` contra el listado de tipologías publicadas en el Admin. |
| El documento se clasifica correctamente pero con confianza baja (< 0.85) dentro del conjunto | La `gptDescripcion` de la tipología acertada da señales débiles, o el documento tiene contenido ambiguo entre dos candidatas | Reforzar `SEÑALES:` y `DESEMPATE:` en la `gptDescripcion` de las tipologías en conflicto. |
| El campo `identificacion.propuestaTipologia` viene vacío con `"Desconocido"` | `proponerSiDesconocido` no estaba en `true`, o la pasada libre también devolvió sin resultado mapeable | Confirmar `proponerSiDesconocido: true` en la petición; si ya estaba activo, es un caso genuino donde tampoco el catálogo completo reconoce el documento. |
