# Manual de Costes de IA — DocumentIA

## 1. Propósito

Cada ejecución registra lo que ha consumido en servicios de inteligencia artificial, desglosado por llamada y agregado en un total, expresado en euros y en tokens. El dato se persiste en base de datos y se puede consultar desde la aplicación de administración.

Responde a preguntas que antes solo se podían contestar a posteriori y de forma agregada, cruzando la facturación de Azure con la volumetría de la base de datos: cuánto cuesta procesar un documento, qué tipología consume más, si un cambio de modelo ha salido rentable, o cuánto se gastó en una ejecución que terminó en error.

**Alcance: solo servicios de IA.** No contabiliza Blob Storage, SQL, Functions ni ninguna otra infraestructura.

---

## 2. Qué se mide

El pipeline llama a un servicio de IA en siete puntos. Todos quedan registrados.

| Punto | Servicio | Unidad que se factura |
|---|---|---|
| Clasificación con Document Intelligence | Clasificador a medida | Páginas |
| Clasificación generativa, fases 1 y 2 | Azure OpenAI | Tokens |
| Clasificación restringida y rescate | Azure OpenAI | Tokens |
| Layout a Markdown, en sus cinco puntos de llamada | Document Intelligence | Páginas |
| Extracción con Content Understanding | Content Understanding | Páginas, contextualización y tokens del modelo generativo interno |
| Extracción con Document Intelligence a medida | Analizador a medida | Páginas |
| Extracción generativa y prompt libre o resumen | Azure OpenAI | Tokens |

La clasificación forzada por tipo esperado no llama a ningún servicio y no genera consumo. Tampoco lo genera el clasificador por reglas del flujo híbrido, que es código local.

---

## 3. Cómo se usa

### 3.1 Pedir los costes en una petición

Basta con activar el parámetro en las instrucciones de entrada:

```json
{
  "instrucciones": {
    "incluirCostes": true
  }
}
```

El bloque se calcula y **se persiste siempre**, con independencia del parámetro. Lo que este decide es únicamente si viaja en la respuesta. Una petición que no lo pida recibe exactamente la misma salida que antes de existir la funcionalidad.

### 3.2 Leer el resultado

```json
"detalleEjecucion": {
  "costes": {
    "version": "1.0",
    "consumos": [
      {
        "actividad": "Clasificar",
        "operacion": "classification.phase1",
        "proveedor": "AzureOpenAI",
        "modelo": "gpt-4o-mini",
        "tokensEntrada": 12480,
        "tokensEntradaCache": 7200,
        "tokensSalida": 310,
        "costeEur": 0.004312,
        "tarifaAplicada": "gpt-4o-mini@2026-04-01",
        "descartado": false
      },
      {
        "actividad": "Layout",
        "operacion": "layout.prebuilt-layout",
        "proveedor": "DocumentIntelligence",
        "modelo": "prebuilt-layout",
        "paginas": 10,
        "costeEur": 0.085866,
        "tarifaAplicada": "prebuilt-layout@2026-04-01"
      }
    ],
    "costeTotalEur": 0.090178,
    "tokensTotales": 12790,
    "paginasTotales": 10,
    "tarifasCompletas": true,
    "modelosSinTarifa": [],
    "reutilizadaPorDuplicado": false
  }
}
```

Los campos están documentados uno a uno en la sección 6.bis del [contrato HTTP](../contratos/CONTRATO_API_HTTP.md).

### 3.3 Consultarlos en Admin

La sección está en la ruta `/costes`. **No aparece en el menú**, se llega escribiendo la dirección.

Usa los mismos filtros que el Monitor: rango de fechas, tipología, estado, flujo, búsqueda por documento o identificador, y solicitante. Muestra cuatro bloques:

- **Cabecera**: total del periodo, coste por ejecución, tokens, y cuántas ejecuciones tienen coste medido, estimado o ninguno.
- **Desglose** por actividad, por tipología y por modelo, ordenado por coste.
- **Evolución diaria** del gasto.
- **Listado** con columna de coste; el detalle desplegable de cada fila muestra el desglose por llamada.

Un interruptor decide si los importes incluyen las ejecuciones estimadas. Por defecto no las incluye.

---

## 4. Cómo se calcula

### 4.1 La tarifa se resuelve por modelo físico

Cada consumo guarda el **modelo real que se ha usado**: el nombre del despliegue para Azure OpenAI, el identificador del clasificador o `prebuilt-layout` para Document Intelligence, el del analizador para Content Understanding. La tarifa se busca por ese nombre.

No se resuelve por la fila de configuración que lo invocó. La tabla de modelos tiene varias filas que apuntan al mismo despliegue, y ligar el precio a la fila lo duplicaría en media docena de sitios que se desalinearían en cuanto alguien editara uno.

### 4.2 Los tokens cacheados se restan, no se suman

La API de Azure OpenAI devuelve el total de tokens de entrada, y **los cacheados vienen incluidos dentro de ese total**. El sistema resta la parte cacheada y le aplica su precio reducido.

Sumarlos contaría dos veces la parte servida desde caché, justo donde el sistema tiene una tasa de acierto alta por el tamaño del prompt fijo de clasificación.

### 4.3 Dónde ocurre el cálculo

En la actividad, no en el orquestador. El catálogo de tarifas vive en base de datos y el orquestador de Durable Functions debe seguir siendo determinista, así que no puede consultarla. Cada actividad tarifica sus propias llamadas y el orquestador se limita a acumular y sumar.

### 4.4 Lo que se paga aunque se descarte

La clasificación evalúa varios proveedores y se queda con uno. **Todos se han pagado.** Los consumos de los descartados cuentan en el total y quedan marcados con `descartado: true`, de modo que se puede separar el gasto útil del descartado.

Lo mismo ocurre cuando la extracción degrada de Content Understanding al modelo generativo: el consumo del primero se conserva.

### 4.5 Cuando falta una tarifa

Un modelo sin precio en el catálogo **no rompe nada**. El consumo se registra igual con sus tokens y páginas, el coste queda a nulo, el agregado se marca con `tarifasCompletas: false` y `modelosSinTarifa` dice cuáles faltan.

Del mismo modo, un consumo sin ninguna magnitud (el proveedor no devolvió uso, o la llamada se cortó por tiempo de espera) **no se tarifica a cero**: se deja sin coste. Un cero calculado sería indistinguible de un cero real.

### 4.6 Reutilización por duplicado

Una petición resuelta reutilizando una ejecución anterior no gasta IA. Su bloque llega a cero, marcado con `reutilizadaPorDuplicado`, y el coste de la ejecución original va aparte en `costeEjecucionOriginalEur`, a título informativo. Así, sumar costes sobre el histórico no cuenta dos veces el mismo gasto.

---

## 5. El catálogo de tarifas

### 5.1 Dónde vive

Una única fila en `ModeloConfigs` con `Tipo = 4` (Tarifas) y clave `tarifas.ia`. Es la columna JSON que ya existía, así que no hubo cambio de esquema. Se edita desde la pantalla de modelos de Admin, en la sección Tarifas, y el registro se refresca solo con la caché de cinco minutos que usan los demás cargadores.

### 5.2 Formato

```json
{
  "Moneda": "EUR",
  "Tarifas": [
    { "Modelo": "gpt-4o-mini", "VigenteDesde": "2026-04-01",
      "EurEntradaPor1M": 0.416, "EurEntradaCachePor1M": 0.104, "EurSalidaPor1M": 1.662 },
    { "Modelo": "prebuilt-layout", "VigenteDesde": "2026-04-01",
      "EurPorPagina": 0.008586639 }
  ]
}
```

| Campo | Se aplica a |
|---|---|
| `EurEntradaPor1M` | tokens de entrada a precio pleno |
| `EurEntradaCachePor1M` | tokens de entrada servidos desde caché |
| `EurSalidaPor1M` | tokens de salida, razonamiento incluido |
| `EurContextualizacionPor1M` | contextualización de Content Understanding |
| `EurPorPagina` | servicios que facturan por página |

Todos son opcionales; se aplica solo lo informado.

### 5.3 Regla de mantenimiento

**Cambiar un precio es añadir una línea nueva con su fecha de vigencia, nunca editar la anterior.** El sistema aplica a cada ejecución la línea de mayor vigencia que no supere su fecha, así que las ejecuciones antiguas siguen cuadrando con lo que se facturó entonces. Editar una línea reescribiría el pasado.

### 5.4 Dos trampas al fijar precios

**El tipo de despliegue y la región cambian el medidor**, y con él el precio, hasta un 70 por ciento entre variantes del mismo modelo. Hay que mirar el SKU real del despliegue, no solo el nombre del modelo.

**El nombre del medidor no siempre es el evidente.** Para un mismo modelo pueden convivir medidores de modos distintos con precios muy diferentes. La forma fiable de acertar es derivar el precio unitario de la facturación real, dividiendo coste entre cantidad por medidor.

El procedimiento completo, con los comandos, está en el [README del catálogo](../../scripts/migrations/costes-ia/README.md).

---

## 6. Persistencia

Siete columnas en `DocumentoEjecuciones`, todas anulables:

| Columna | Contenido |
|---|---|
| `CosteIAEur` | Coste total de la ejecución |
| `TokensIA` | Entrada + salida + contextualización |
| `CosteLayoutEur` | Desglose por actividad |
| `CosteClasificacionEur` | Desglose por actividad |
| `CosteExtraccionEur` | Desglose por actividad, incluido el modelo interno de Content Understanding |
| `CostePromptEur` | Desglose por actividad |
| `CosteEstimado` | Cierto si el importe procede del relleno retroactivo |

El **desglose por llamada** no tiene columna: viaja dentro de `ContratoSalidaCompletoJson`, en `$.DetalleEjecucion.Costes`. Añade entre dos y tres kilobytes por ejecución.

Las cuatro columnas de actividad existen para poder agregar desde Admin sin abrir el contrato. Hacerlo con `OPENJSON` sobre una ventana de noventa días serían decenas de miles de objetos grandes, que es el problema de rendimiento que el Monitor ya arrastra.

Una actividad sin consumo tarificado queda a nulo, no a cero, para distinguir "no gastó" de "gastó cero".

---

## 7. Ejecuciones anteriores a la funcionalidad

### 7.1 Qué se puede estimar y qué no

El histórico guardó las páginas enviadas al layout y el modelo de clasificación, pero **nunca los tokens** ni las páginas de extracción de Content Understanding.

Como el layout es la mayor parte del gasto, lo derivable coincide con lo caro. Aun así es una **estimación**, y como tal se marca.

| Concepto | Cómo se estima |
|---|---|
| Layout | Páginas del contrato por la tarifa por página |
| Clasificación generativa | Coste medio por documento, parámetro del script |
| Extracción | No se puede estimar; queda a cero |

### 7.2 El script

`scripts/database/backfill-costes-estimados.ps1`, por marca de agua sobre el identificador, como los demás scripts de relleno del proyecto. Se lanza a mano.

```powershell
# Ver cuántas filas tocaría, sin escribir
./backfill-costes-estimados.ps1 -WhatIf

# Ejecutar contra un entorno concreto, por tandas
./backfill-costes-estimados.ps1 -Server srbsqlprodocai.database.windows.net -BatchSize 1000
```

Escribe solo columnas escalares, nunca el contrato. Es idempotente y **nunca pisa un coste medido**: solo toca filas sin coste.

### 7.3 Advertencia importante sobre la facturación

**La suma de costes de un entorno no es comparable con la factura del grupo de recursos.** La cuenta de IA de producción la consumen también desarrollo y preproducción, cuyas ejecuciones viven en otras bases de datos.

---

## 8. Comprobación tras un despliegue

1. Esperar cinco minutos a que caduque la caché del catálogo.
2. Lanzar `scripts/testing/test-costes-ia.ps1`, que hace por sí solo los tres pasos siguientes:

   ```powershell
   pwsh ./scripts/testing/test-costes-ia.ps1 -Environment dev
   ```

   - Lanza una petición con `incluirCostes` y comprueba que el bloque llega con importes, con `tarifasCompletas` en cierto y con el desglose cuadrando con el total.
   - Lanza la misma sin el parámetro y comprueba que la salida no lo incluye, ni por el bloque de costes ni por los consumos de clasificación.
   - Devuelve los dos identificadores para revisarlos después.

   Necesita `tests/e2e-postdeploy/config/environments.json`, que no está versionado porque contiene las claves de función.
3. Consultar las columnas de la base de datos con los dos identificadores: ambas ejecuciones deben tenerlas rellenas, también la que no pidió el bloque.
4. Abrir `/costes` en Admin.

Si alguna ejecución sale con tarifas incompletas, el propio bloque dice qué modelos faltan por tarifar. El paso inicial del script de alta lista los modelos configurados, que es la forma de detectar los que el catálogo no cubre.

Una petición suelta puede resolverse por reglas y no consumir IA de clasificación; para ejercitar clasificación generativa y extracción hace falta la batería de pruebas de extremo a extremo.

---

## 9. Referencias

- [Contrato HTTP, sección 6.bis](../contratos/CONTRATO_API_HTTP.md) — campos del bloque de salida
- [Modelo de datos](../especificaciones/DATA_MODELS_ER_DIAGRAM.md) — columnas y versiones de esquema
- [README del catálogo de tarifas](../../scripts/migrations/costes-ia/README.md) — alta, precios y procedimiento
- [Checklists de despliegue](../08_CHECKLISTS_DESPLIEGUE.md) — orden de migraciones y verificación post-despliegue
- [Manual de uso, sección 5.9.7](../05_MANUAL_USO_CONFIGURACION.md) — la sección de costes del Admin
- [Manual de deduplicación](MANUAL_DEDUPLICACION.md) — por qué una reutilización no gasta IA
- Work items: 100224 (control de costes) y 100235 (sección de Admin y relleno retroactivo)
