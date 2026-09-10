# Juego de pruebas de validación de la cobertura de markdown

**Fecha:** 2026-09-10
**Estado:** Diseño aprobado; pendiente de plan de implementación
**Depende de:** [Política única de obtención y persistencia de markdown por cobertura de páginas](2026-09-08-markdown-cobertura-paginas-design.md) (AB#100245), integrada en `develop` en el commit de merge `07910a8`
**Work items:** elemento padre [100262](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/100262), tareas 100263 a 100270. Relacionado con [100245](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/100245).

## Problema

El runner de pruebas post-despliegue ([`run-e2e-postdeploy.ps1`](../../../tests/e2e-postdeploy/run-e2e-postdeploy.ps1)) **no toca la base de datos**. No contiene ni una llamada a `sqlcmd`, `Invoke-Sqlcmd` ni `SqlConnection`: asevera exclusivamente sobre el JSON que devuelve la orquestación.

Las invariantes que introduce AB#100245 viven, casi todas, en la fila de `Documentos`:

- que la cobertura persistida **nunca se degrade**,
- que el texto guardado y su cobertura declarada **nunca se desacoplen**,
- que el markdown aportado por el llamante **nunca se persista**,
- que una actualización de documento **no reescriba** las columnas de markdown.

Ninguna de ellas es observable desde la respuesta HTTP de una sola ejecución. La consecuencia concreta: el defecto crítico que apareció en la revisión final de la rama —una fila que nacía con un recorte de tres páginas marcado como documento completo, y que a partir de ahí servía ese recorte como completo para siempre— **habría pasado la batería e2e entera en verde**.

Existe además un segundo hueco. El script [`backfill-markdown-cobertura.ps1`](../../../scripts/database/backfill-markdown-cobertura.ps1) escribe cobertura sobre el histórico y tiene un modo `-WhatIf`; hoy nada comprueba que lo que cuenta el `-WhatIf` sea lo que después escribe.

## Objetivo

Un juego de pruebas que demuestre las invariantes de cobertura de markdown contra un entorno real, incluyendo el estado que queda en base de datos, y que distinga con claridad entre *la invariante está violada* y *la prueba no pudo ejecutarse*.

No sustituye al e2e post-despliegue ni al smoke: los complementa en el eje que ellos no cubren.

## Decisiones de alcance

| Decisión | Elegido | Motivo |
|---|---|---|
| Dónde se ejecuta | Solo a mano, desde red corporativa | Los tres servidores SQL tienen *Deny Public Network Access*; solo se alcanzan por private endpoint. Con ejecución manual el acceso SQL es viable y da buen diagnóstico. |
| Preparación del estado | Híbrido: el pipeline crea la fila, SQL la muta | Fabricar una fila por SQL obligaría a inventar SHA256, MD5 y un blob gzip coherente con el documento enviado. Creándola con el pipeline la fila es genuina; la mutación es un `UPDATE` de una o dos columnas. |
| Entorno y huella | Solo DEV, con limpieza antes y después | El juego escribe filas deliberadamente corruptas. La huella de pruebas ya ha dado problemas en este proyecto (`migracion-dev`, la fila 8061 de AB#100258). |
| Forma respecto al e2e | Script hermano que reutiliza las librerías | El ciclo de vida es distinto (sembrar/limpiar alrededor del run), es forzosamente secuencial y solo admite DEV. Meterlo en el runner sería añadirle un modo que apaga la mitad de su comportamiento. |
| Expresión de los casos | Declarativo en JSON | Es el idioma del e2e, permite leer el inventario sin leer código, y la matriz de cobertura funcional sigue siendo la fuente de verdad de qué se valida. |

## Hallazgo que sostiene el diseño

Los tres campos de cobertura **ya viajan en la respuesta**. `MarkdownPaginas`, `MarkdownCompleto` y `MarkdownFuente` están en [`ContratoSalida.cs:214-218`](../../../src/backend/DocumentIA.Core/Models/ContratoSalida.cs#L214-L218) y el orquestador los publica en [`DocumentProcessOrchestrator.cs:366-368`](../../../src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs#L366-L368).

El runner ya sabe aseverar sobre rutas de `DetalleEjecucion` —lo hace con `NivelClasificacion`, `ClassificationOnly` y `RecorteAplicado`—, así que buena parte de las invariantes son observables en caja negra mediante secuencias de varias pasadas:

- *la segunda ejecución reutiliza de base de datos* → la segunda pasada asevera `MarkdownFuente = "BaseDatos"`;
- *el markdown del llamante no persiste* → la primera pasada asevera `Fuente = "Caller"` y la segunda que **no** es `"BaseDatos"`;
- *la cobertura no se degrada* → una pasada posterior que pide el documento completo debe seguir viendo `BaseDatos` y `MarkdownCompleto = true`.

El acceso SQL queda así reducido a lo que de verdad lo necesita: el estado final de la fila, la siembra de estados imposibles de alcanzar por pipeline, y el backfill.

## Arquitectura

### Piezas nuevas

| Fichero | Responsabilidad |
|---|---|
| `tests/e2e-postdeploy/run-validacion-markdown.ps1` | Punto de entrada. `-Environment` con `ValidateSet` de un solo valor (`dev`). Siempre secuencial. Modos: normal, `-WhatIf` y `-SoloLimpieza`. |
| `tests/e2e-postdeploy/lib/postdeploy-db.ps1` | **La única pieza que habla SQL.** Conectar, sembrar la mutación, tomar instantáneas de columnas, aseverar y borrar. |
| `tests/e2e-postdeploy/cases-validacion/markdown-cases.json` | Los casos. Directorio propio para que el glob del runner e2e (`cases/*-cases.json`) no los recoja. |
| `tests/e2e-postdeploy/tests/postdeploy-db.Tests.ps1` | Pester sobre las funciones puras de la capa SQL, sin base de datos real. |
| `tests/e2e-postdeploy/tests/cases-validacion-schema.Tests.ps1` | Validación del esquema nuevo. Hermano del existente, no extensión: mezclar los dos vocabularios enturbiaría ambos. |
| `tests/e2e-postdeploy/corpus/control/documento-12-paginas-marcado.pdf` | Documento control con marcador único por página (ver más abajo). |

### Piezas modificadas

| Fichero | Cambio |
|---|---|
| `tests/e2e-postdeploy/coverage/functional-matrix.json` | Área `MDW-*` nueva (nueve entradas). |
| `tests/e2e-postdeploy/config/environments.sample.json` | Bloque de conexión SQL: servidor y base de datos, **sin contraseña**. |
| `tests/e2e-postdeploy/lib/postdeploy-config.ps1` | `Get-E2EEnvironment` devuelve hoy solo `Name`, `BaseUrl`, `FunctionKey` y `HealthCheck`. Se amplía para exponer el bloque SQL. Cambio aditivo: el runner e2e no se entera. |
| `tests/e2e-postdeploy/tools/generate_corpus.py` | Generación del documento control marcado. |

### Piezas reutilizadas sin tocar

- [`tests/api-tests/documentia-e2e-common.ps1`](../../../tests/api-tests/documentia-e2e-common.ps1): construcción de la petición, espera de la orquestación y `Test-CaseAssertions`. **El vocabulario de aserciones sobre el JSON se reutiliza tal cual**; no se inventa lenguaje de aserción nuevo.
- `lib/postdeploy-report.ps1`: generación del informe y los artefactos.

### Dos detalles del contrato que condicionan la implementación

1. **El markdown servido está en `DetalleEjecucion.Postproceso.Markdown`**, no en la raíz de la salida: `InformacionPostproceso` cuelga de `DetalleEjecucion` ([`ContratoSalida.cs:179`](../../../src/backend/DocumentIA.Core/Models/ContratoSalida.cs#L179)). Las aserciones de marcador usan esa ruta.
2. **`New-DocumentIARequestBody` lee las opciones del objeto de caso plano** (`$Case.classificationProvider`, `$Case.forceReprocess`, `$Case.markdown`…), no de un bloque anidado. El runner debe construir, por cada pasada, un objeto de caso sintético que combine los campos de nivel superior del caso con el `request` y las `assertions` de esa pasada. El markdown aportado por el llamante viaja en `$Case.markdown`, que acaba en `classification.markdown`.
3. **`Invoke-DocumentIAE2ECase` solo devuelve `PASS`, `FAIL` o `SKIP`.** El estado `ERROR` lo produce el runner nuevo; no hay que esperarlo de la librería compartida.

### Frontera

`postdeploy-db.ps1` es la única pieza con conocimiento de SQL. Si en el futuro el juego tuviera que ejecutarse sin acceso a base de datos —por ejemplo en un pipeline con agente hosted—, se desactiva esa capa y las aserciones de caja negra siguen dando señal. El diseño pierde tres invariantes, no se cae.

## El documento control marcado

El corpus actual tiene un `documento-12-paginas.pdf` sin contenido distinguible por página. Se añade un documento control cuyas páginas llevan un marcador único y buscable: `MARCA-PAGINA-01` … `MARCA-PAGINA-12`.

Esto convierte la cobertura en algo **directamente observable en la respuesta**, sin descomprimir el blob gzip, sin comparar tamaños y sin depender de cómo separe las páginas Document Intelligence:

- un markdown que dice ser completo y no contiene `MARCA-PAGINA-12` no es completo;
- un recorte a tres páginas debe contener `MARCA-PAGINA-03` y no `MARCA-PAGINA-04`.

Las aserciones necesarias (`expectOutputPathContains`, `expectOutputJsonNotContains`) **ya existen** en `Test-CaseAssertions`. El generador de corpus ya está en el repositorio, así que el coste es bajo.

## Esquema de casos

Cuatro bloques por caso. Ejemplo completo:

```json
{
  "caseKey": "MDW-MDW3", "group": "MDW", "id": "MDW3", "domain": "validacion",
  "name": "Una ejecucion de 3 paginas no degrada una fila con cobertura completa",
  "covers": ["MDW-03"],
  "documentPath": "tests/e2e-postdeploy/corpus/control/documento-12-paginas-marcado.pdf",
  "seed": {
    "request": { "classificationProvider": "auto", "forceReprocess": true },
    "mutacion": null,
    "precondicion": [
      { "columna": "MarkdownCompleto", "esperado": true },
      { "columna": "MarkdownPaginas",  "esperado": 12 }
    ]
  },
  "pasadas": [{
    "nombre": "pide solo 3 paginas",
    "request": {
      "classificationProvider": "gpt", "classificationOnly": true,
      "maxPagesForClassificationOnly": 3, "skipDuplicateCheck": true
    },
    "assertions": {
      "expectedRuntimeStatus": "Completed",
      "expectOutputPathEquals": [
        { "path": "DetalleEjecucion.MarkdownFuente",   "value": "BaseDatos" },
        { "path": "DetalleEjecucion.MarkdownCompleto", "value": "True" }
      ]
    }
  }],
  "dbAssertions": [
    { "columna": "MarkdownCompleto", "esperado": true },
    { "columna": "MarkdownPaginas",  "esperado": 12 },
    { "columna": "NormalizacionMarkdownGzip",       "noEncoge": true, "noEsNulo": true },
    { "columna": "NormalizacionMarkdownCompressed", "noEncoge": true, "noEsNulo": true }
  ]
}
```

**Las dos columnas de markdown se comprueban siempre juntas.** Desde AB#100169 el markdown se escribe en paralelo en `NormalizacionMarkdownGzip` (binaria) y `NormalizacionMarkdownCompressed` (base64, forma histórica que se mantiene para poder revertir código o base de datos). Aseverar solo sobre una dejaría pasar una degradación que afectase a la otra.

### `seed`

- `request`: parámetros de la ejecución que **crea** la fila de partida.
- `mutacion`: `UPDATE` opcional sobre columnas concretas, para estados que el pipeline no sabe producir (fila histórica con `MarkdownPaginas` NULL; fila envenenada con recorte marcado completo). `null` cuando la fila genuina ya es el estado deseado.
- `precondicion`: aserciones sobre la fila **antes** de las pasadas.

Si la precondición no se cumple, el caso resulta **ERROR**, no FAIL: no hubo estado de partida, así que el resultado no significa nada. Este es el antídoto contra el resultado verde vacío.

### `pasadas`

Lista ordenada. Cada pasada tiene `nombre`, `request` y `assertions`, y estas últimas usan el vocabulario existente sin extensiones.

### `dbAssertions`

Se evalúan tras la última pasada. Además de valores exactos por columna, se admiten dos comparaciones contra la instantánea tomada antes de las pasadas:

- `noEncoge`: el valor binario o textual no puede reducir su longitud.
- `noDisminuye`: el valor numérico no puede bajar.

Degradar el texto siempre lo encoge, así que `noEncoge` detecta la degradación sin descomprimir nada.

### Limpieza

Automática, no declarativa. El script borra por SHA256 del documento del caso **antes y después** de cada uno. El borrado en cascada de `Documentos` arrastra `Ejecuciones` y sus postprocesos y validaciones ([`DocumentIADbContext.cs:103-118`](../../../src/backend/DocumentIA.Data/Context/DocumentIADbContext.cs#L103-L118)), así que es una sola sentencia.

El borrado *previo* no es redundante: los casos de reutilización necesitan que la fila no exista al empezar, y los restos de un run interrumpido envenenarían el siguiente.

## Invariantes cubiertas (área `MDW`)

| id | Qué demuestra | Cómo |
|---|---|---|
| MDW-01 | Texto y cobertura declarada viajan juntos; la fila nunca queda con el texto de una cobertura y las columnas de otra | Escenario del defecto crítico (PDF largo con prompt libre). Una pasada de verificación pide el documento completo y su markdown debe contener `MARCA-PAGINA-12` |
| MDW-02 | La segunda ejecución reutiliza el markdown persistido sin volver a llamar a Layout | Dos pasadas; la segunda asevera `MarkdownFuente = "BaseDatos"` |
| MDW-03 | La cobertura nunca se degrada: pedir menos páginas no rebaja la fila | Siembra completa, pasada de tres páginas, `dbAssertions` con `noDisminuye` y `noEncoge` |
| MDW-04 | El markdown aportado por el llamante no se persiste | Primera pasada con markdown aportado asevera `Fuente = "Caller"`; la segunda que no es `"BaseDatos"` |
| MDW-05 | Los formatos que no admiten rango de páginas se declaran completos | DOCX y XLSX del corpus; asevera `MarkdownCompleto = true` |
| MDW-06 | El recorte declara cobertura parcial, no completa | `maxPagesForClassificationOnly = 3`; contiene `MARCA-PAGINA-03`, no contiene `MARCA-PAGINA-04`, `MarkdownCompleto = false` |
| MDW-07 | Fila histórica con `MarkdownPaginas` NULL se trata como cobertura desconocida | `mutacion` que pone la columna a NULL; la pasada siguiente no debe servirla como completa |
| MDW-08 | El backfill en `-WhatIf` cuenta exactamente las filas que después escribe, y no toca orígenes que no garantizan documento completo | Siembra de filas con distintos `OrigenMarkdown`; se compara el recuento del `-WhatIf` con el efecto real |
| MDW-09 | Actualizar un documento existente no reescribe las columnas de markdown | Segunda ingesta sin `forceReprocess`; `dbAssertions` sobre las cuatro columnas de markdown |

MDW-01 a MDW-07 cubren la funcionalidad; MDW-08 valida el script de backfill y MDW-09 el camino de `UPDATE` que no se detectó hasta la revisión final de rama.

## Ciclo de vida de un run

1. Cargar los casos y validar el esquema.
2. Conectar a la base de datos y **verificar contra qué servidor**.
3. Por cada caso, en serie: limpiar antes → sembrar → comprobar precondición → ejecutar las pasadas en orden → evaluar `dbAssertions` → limpiar después.
4. Generar el informe con `postdeploy-report.ps1`.

### Verificación del servidor

`environments.json` está en `.gitignore` y se edita a mano; una errata podría apuntar a PRO, y este script emite `DELETE`. Antes de la primera sentencia se comprueba que el servidor conectado es el de DEV y se aborta si no lo es.

El `ValidateSet` de un solo valor protege el parámetro; esta comprobación protege la configuración. Son dos defensas distintas para dos fallos distintos.

### Conexión

Token vía `az account get-access-token --resource https://database.windows.net/` asignado a `SqlConnection.AccessToken`. Sin contraseña en ningún fichero.

**Riesgo a verificar primero:** hay constancia de que `sqlcmd -G` falla por MFA en este entorno y de que el camino que funciona es el token de `az`. Si el token tampoco funcionase desde PowerShell, el diseño se apoyaría en otro mecanismo de conexión y habría que revisarlo antes de continuar.

### Estados

| Estado | Significado |
|---|---|
| PASS | La invariante se cumple |
| FAIL | **La invariante está violada** |
| ERROR | **El caso no pudo ejecutarse**: no conectó, la siembra no produjo el estado esperado, la orquestación no llegó a `Completed` |
| SKIP | El caso no aplica en la ejecución actual |

La distinción entre FAIL y ERROR es deliberada y no cosmética. Una suite de validación que informa FAIL cuando quiere decir «no pude conectar» enseña a ignorar los rojos, y entonces deja de servir para nada.

### Run interrumpido

`-SoloLimpieza` borra por los SHA256 de los documentos declarados en los casos. Esos hashes se calculan del fichero del corpus y son deterministas, así que la limpieza funciona aunque el run anterior muriese antes de escribir una sola fila.

## Coste

El juego consume Document Intelligence y GPT reales en DEV: nueve casos con siembra más una o dos pasadas suponen del orden de veinte a veinticinco ejecuciones, estimadas en veinte a cuarenta minutos.

No es una suite de integración continua. Es la que se pasa antes de dar por buena la funcionalidad, igual que el perfil `full` del e2e.

## Condición de aceptación

> **Estado (2026-09-10):** la demostración por mutación **no se ejecuta**, por decisión del usuario. Exigía desplegar a DEV código modificado una vez por mutación, y el objetivo de este juego es validar el código que se va a desplegar a PRO, no medir la calidad de la suite. En su lugar, la prueba de que los casos discriminan es el **rastreo contrafáctico** que hizo el revisor de cada tarea (para cada caso, qué aserción se pondría roja si la invariante fallara), registrado en el ledger de ejecución y en los informes de revisión de las Tareas 5, 7a y 7b. La tabla de mutaciones queda en el plan como referencia si algún día se quiere ejecutar en una sesión con despliegues.

**Cada caso `MDW` debía demostrarse por mutación antes de considerarse hecho:** romper a propósito la invariante en el código de producción y confirmar que el caso se pone rojo.

Sin esa demostración no sabemos si el caso prueba algo. En la implementación de AB#100245 apareció exactamente esa trampa: una aserción que se cumplía siempre y un test que parecía verde por razones ajenas a lo que decía verificar.

Las funciones puras de la capa SQL —construcción de sentencias, comparación de esperado contra real, resolución de qué limpiar— se desarrollan con TDD y Pester sin base de datos real. Lo que toca la red no se prueba unitariamente.

## Fuera de alcance

- **Ejecución contra PRO.** Ni siquiera es expresable por parámetro.
- **Ejecución en pipeline.** Los servidores SQL no son alcanzables desde agentes hosted. Si algún día se quisiera, el camino sería un agente self-hosted en el pool `documentia-selfhosted`, y la capa SQL tendría que poder desactivarse.
- **Medición del impacto en la calidad de clasificación.** Reprocesar un documento puede dar otra clasificación, porque la segunda ejecución recupera de base de datos el markdown de mayor cobertura sin recortarlo. Medirlo corresponde al corpus golden del harness de evaluación (AB#99948), no a este juego de pruebas.
- **Descompresión del blob de markdown.** El documento control marcado hace innecesario inspeccionar el binario.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| El token de `az` no sirve para conectar desde PowerShell | Verificarlo en el primer paso de la implementación, antes de construir nada encima |
| Una errata en `environments.json` apunta a PRO y el script borra filas | Verificación del servidor antes de la primera sentencia, más `ValidateSet` de un solo valor |
| Un run interrumpido deja filas que envenenan el siguiente | Limpieza antes y después de cada caso, más el modo `-SoloLimpieza` |
| Un caso pasa en verde sin probar nada | Demostración por mutación como condición de aceptación |
| El esquema de casos se queda corto para una invariante futura | Es un fichero JSON con su test de esquema; ampliarlo es barato y el test avisa de las incoherencias |
