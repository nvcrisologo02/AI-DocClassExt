# Especificacion Tecnica: Plugin AssetResolver

> Ultima actualizacion: 2026-04-20  
> Proyecto: AI DocClassExt — SAREB  
> Componente: `DocumentIA.AssetResolver`

---

## 1. Proposito

El plugin **AssetResolver** es un servicio HTTP independiente que resuelve el **IdActivo** (codigo de activo inmobiliario SAREB) a partir de datos extraidos de documentos y/o criterios explicitos en request. Consulta las tablas maestras `DM_POSICION_AAII_TB` y `DM_POSICION_AACC_TB` usando cuatro criterios de busqueda configurables: IDUFIR, Referencia Catastral, Direccion fuzzy y Direccion tipificada.

---

## 2. Arquitectura

```
┌─────────────────────────────┐       HTTP POST        ┌──────────────────────────┐
│   DocumentIA.Functions      │ ────────────────────► │  DocumentIA.AssetResolver │
│   (ObtenerActivoActivity)   │ /api/assets/GetAAIIInfo│  (ASP.NET Core API)       │
└─────────────────────────────┘                        └────────────┬─────────────┘
                                                                    │
                                                                    ▼
                                                       ┌──────────────────────────┐
                                                       │ DM_POSICION_AAII_TB/AACC │
                                                       │   (SQL Server / Azure)   │
                                                       └──────────────────────────┘
```

- **Proceso independiente**: corre en puerto 5006 (local) o como Azure App Service.
- **Comunicacion**: HTTP POST JSON via `HttpClientFactory`.
- **Base de datos**: connection string `AssetResolverDb` (lectura de tabla de activos).

---

## 3. Endpoint Principal

### `POST /api/assets/GetAAIIInfo`

#### Request

```json
{
  "correlationId": "guid",
  "documentType": "nota.simple.1_4",
  "extractedData": {
    "IDUFIR_CRU": "12345678901234",
    "Localizacion": "CALLE MAYOR 1, 28013 MADRID"
  },
  "requestedFields": ["#ALL#"],
  "AAII_Search": true,
  "AACC_Search": true,
  "idufirOverride": null,
  "referenciaCatastralOverride": null,
  "modoCombinacionCriterios": "OR",
  "busquedaIdufirHabilitada": true,
  "busquedaReferenciaCatastralHabilitada": false,
  "busquedaDireccionHabilitada": true,
  "busquedaDireccionTipificadaHabilitada": false,
  "direccionTipificada": {
    "pais": null,
    "provincia": null,
    "comunidadAutonoma": null,
    "municipio": "Madrid",
    "poblacion": null,
    "tipoVia": null,
    "calle": "Mayor",
    "numero": "1",
    "bloque": null,
    "puerta": null,
    "codigoPostal": "28013",
    "planta": null
  },
  "mapeoIdufir": ["IDUFIR_CRU"],
  "mapeoReferenciaCatastral": [],
  "mapeoDireccionCompleta": ["Localizacion"],
  "mapeoDireccionNombreVia": [],
  "mapeoDireccionNumero": [],
  "mapeoDireccionMunicipio": [],
  "mapeoDireccionCodigoPostal": [],
  "umbralScoreDireccion": 0.75
}
```

#### Campos del Request

| Campo | Tipo | Default | Descripcion |
|-------|------|---------|-------------|
| `correlationId` | string | **required** | ID de correlacion para trazabilidad. |
| `documentType` | string | `null` | Tipologia del documento (informativo). |
| `extractedData` | dict | `{}` | Campos extraidos del documento (clave-valor). |
| `requestedFields` | string[] | `null` | Columnas a retornar. `#ALL#` = todas las columnas del origen consultado. `null` = solo obligatorias. |
| `AAII_Search` | bool | `true` | Si `true`, consulta origen AAII (`DM_POSICION_AAII_TB`). |
| `AACC_Search` | bool | `true` | Si `true`, consulta origen AACC (`DM_POSICION_AACC_TB`). |
| `idufirOverride` | string | `null` | Valor IDUFIR explicito (ignora extractedData). |
| `referenciaCatastralOverride` | string | `null` | Valor RefCat explicito. |
| `modoCombinacionCriterios` | string | `"OR"` | `AND` = interseccion, `OR` = union. |
| `busquedaIdufirHabilitada` | bool | `true` | Si `false`, IDUFIR no se usa aunque haya aliases. |
| `busquedaReferenciaCatastralHabilitada` | bool | `true` | Si `false`, RefCat no se usa. |
| `busquedaDireccionHabilitada` | bool | `false` | Si `true`, activa busqueda fuzzy por direccion. |
| `busquedaDireccionTipificadaHabilitada` | bool | `false` | Si `true`, activa busqueda por campos tipificados con filtros AND. |
| `direccionTipificada` | object | `null` | Campos directos de busqueda (no salen de extractedData): Pais, Provincia, ComunidadAutonoma, Municipio, Poblacion, TipoVia, Calle, Numero, Bloque, Puerta, CodigoPostal, Planta. |
| `mapeoIdufir` | string[] | `[]` | Claves de extractedData para IDUFIR. |
| `mapeoReferenciaCatastral` | string[] | `[]` | Claves de extractedData para RefCat. |
| `mapeoDireccionCompleta` | string[] | `[]` | Claves para direccion completa (se parsea automaticamente). |
| `mapeoDireccionNombreVia` | string[] | `[]` | Claves para nombre de via. |
| `mapeoDireccionNumero` | string[] | `[]` | Claves para numero. |
| `mapeoDireccionMunicipio` | string[] | `[]` | Claves para municipio. |
| `mapeoDireccionCodigoPostal` | string[] | `[]` | Claves para codigo postal. |
| `umbralScoreDireccion` | double | `0.75` | Score minimo [0.0-1.0] para match por direccion. |

#### Response

```json
{
  "correlationId": "guid",
  "found": true,
  "count": 1,
  "criteriosUsados": {
    "idufir": "12345678901234",
    "referenciaCatastral": null,
    "modoCombinacionCriterios": "OR",
    "direccion": {
      "direccionCompleta": "CALLE MAYOR 1, 28013 MADRID",
      "nombreVia": "CALLE MAYOR",
      "numero": "1",
      "municipio": "MADRID",
      "codigoPostal": "28013",
      "direccionNormalizada": "calle mayor 1 madrid 28013",
      "score": 0.92,
      "candidatosEvaluados": 1523,
      "razon": "Match encontrado con score 0.92"
    },
    "direccionTipificada": {
      "pais": null,
      "provincia": null,
      "comunidadAutonoma": null,
      "municipio": "Madrid",
      "poblacion": null,
      "tipoVia": null,
      "calle": "Mayor",
      "numero": "1",
      "bloque": null,
      "puerta": null,
      "codigoPostal": "28013",
      "planta": null,
      "candidatosEvaluados": 12,
      "razon": "1 activos encontrados con filtros tipificados"
    }
  },
  "activos": [
    {
      "idActivo": "SAR-001234",
      "fchCierre": "2025-12-31",
      "camposSolicitados": { "DES_NOMBRE_VIA": "CALLE MAYOR", "NUM_VIA": "1" }
    }
  ],
  "activosAAII": [],
  "activosAACC": [],
  "countAAII": 0,
  "countAACC": 1,
  "camposConError": [],
  "message": "Encontrado 1 activo con criterio: Idufir OR Direccion",
  "duracionMs": 45,
  "criterioUtilizado": "Idufir OR Direccion",
  "error": null
}
```

---

## 4. Algoritmo de Busqueda

### 4.1 Flujo General

```
1. Validar flags de habilitacion (busquedaXxxHabilitada)
   └─ Si deshabilitado: vaciar aliases, marcar indicatedXxx = false

2. Resolver valores de cada criterio habilitado:
   └─ IDUFIR: override ?? detectar en extractedData via aliases
   └─ RefCat: override ?? detectar en extractedData via aliases
  └─ Direccion: detectar componentes o parsear direccion completa
  └─ DireccionTipificada: leer objeto request.direccionTipificada

3. Si ningun criterio tiene valor resuelto:
  └─ Retornar Found=false, mensaje "No se encontraron criterios"

4. Ejecutar busquedas independientes:
   └─ IDUFIR: SELECT WHERE ID_IDUFIR = @valor
   └─ RefCat: SELECT WHERE ID_REF_CATAST = @valor
  └─ Direccion: pre-filtro en BD (CP o prefijo municipio) + scoring fuzzy en memoria
  └─ DireccionTipificada: filtros AND en BD solo para campos informados

5. Combinar resultados segun modoCombinacionCriterios:
   └─ OR: UNION de resultados
   └─ AND: INTERSECT de resultados (si algun criterio = 0, resultado = 0)
  └─ OR con todos los criterios a 0: resultado vacio (sin excepcion)

6. Deduplicar por ID_ACTIVO_SAREB

7. Construir response con campos solicitados por origen (AAII/AACC)
```

### 4.2 Deteccion de Valores

```csharp
// Logica simplificada de deteccion
string? DetectarValor(extractedData, override, aliases)
{
    if (!string.IsNullOrWhiteSpace(override)) return override;
    
    foreach (var alias in aliases)
    {
        if (extractedData.TryGetValue(alias, out var valor) && !string.IsNullOrWhiteSpace(valor))
            return valor;
    }
    return null;
}
```

### 4.3 Algoritmo de Scoring por Direccion Fuzzy

El algoritmo calcula un score [0.0-1.0] comparando la direccion de entrada contra cada fila de `DM_POSICION_AAII_TB`:

```
Score = (NombreVia * 0.40) + (Numero * 0.30) + (Municipio * 0.20) + (CodigoPostal * 0.10)
```

Cada componente textual se evalua con similitud de Jaccard sobre tokens normalizados:

| Componente | Peso | Columna BD | Comparacion |
|------------|------|------------|-------------|
| NombreVia | 40% | `DES_NOMBRE_VIA` | Jaccard sobre texto normalizado |
| Numero | 30% | `NUM_VIA` | Exacta tras normalizar numero |
| Municipio | 20% | `DES_MUNICP` | Jaccard sobre texto normalizado |
| CodigoPostal | 10% | `NUM_COD_POSTAL` | Exacta tras normalizar CP |

**Normalizacion**: se eliminan tildes, se convierte a minusculas, y se remueven caracteres especiales.

Pre-filtro BD antes del scoring:
- Si hay codigo postal: `NUM_COD_POSTAL = @cp`
- Si no hay CP y hay municipio: `DES_MUNICP LIKE '%prefijo6%'`

Umbral por defecto: `0.75` (si `umbralScoreDireccion` no se informa o es `<= 0`).

### 4.4 Parseo de Direccion Completa

Cuando se usa `mapeoDireccionCompleta`, la cadena se parsea automaticamente:

```
Entrada: "CALLE MAYOR 1, 28013 MADRID"

1. Extraer CP: regex \b\d{5}\b → "28013"
2. Remover CP de la cadena: "CALLE MAYOR 1, MADRID"
3. Split por coma: ["CALLE MAYOR 1", "MADRID"]
4. Municipio:
  - 2 segmentos: ultimo segmento
  - 3 o mas segmentos: penultimo (el ultimo suele ser provincia)
5. Primer segmento = Via + Numero
6. Extraer numero de via: primer match regex \b\d+[A-Z]?\b → "1"
7. Resto = NombreVia: "CALLE MAYOR"

Caso real soportado:
- Entrada: "calle torrente ballester 7, 4C Azuqueca de henares, Guadalajara"
- Resultado parseado: NombreVia="calle torrente ballester", Numero="7", Municipio="Azuqueca de henares"

Resultado:
  NombreVia: "CALLE MAYOR"
  Numero: "1"
  Municipio: "MADRID"
  CodigoPostal: "28013"
```

---

## 5. Combinacion de Resultados

### 5.1 Modo OR (Union)

```
ResultadoFinal = Idufir.Resultados ∪ RefCat.Resultados ∪ Direccion.Resultados

Si DireccionTipificada esta resuelta, tambien participa en OR:
ResultadoFinal = ... ∪ DireccionTipificada.Resultados
```

Ejemplo:
- IDUFIR encuentra: [A, B]
- Direccion encuentra: [B, C]
- Resultado OR: [A, B, C] (deduplicado)

### 5.2 Modo AND (Interseccion)

```
ResultadoFinal = Idufir.Resultados ∩ RefCat.Resultados ∩ Direccion.Resultados
```

**Regla importante**: solo se consideran criterios que tienen valor resuelto. Si un criterio no tiene valor (ej. mapeoIdufir vacio y no hay override), no participa en la interseccion.

Ejemplo:
- IDUFIR (resuelto) encuentra: [A, B, C]
- Direccion (resuelto) encuentra: [B, C, D]
- RefCat (no resuelto): no participa
- Resultado AND: [B, C]

Si cualquier criterio resuelto devuelve 0 resultados, el resultado final es vacio:
- IDUFIR encuentra: [A]
- Direccion encuentra: [] (score < umbral)
- Resultado AND: []

En modo OR, si todos los criterios resueltos devuelven 0 resultados, el resultado final es [] y no se produce excepcion.

---

## 5.3 Direccion Tipificada (AND por campos informados)

La busqueda tipificada no utiliza aliases de `extractedData`. Toma directamente `request.direccionTipificada`.

Reglas:
- Si `busquedaDireccionTipificadaHabilitada=false`, no participa.
- Si el objeto viene vacio (todos null/whitespace), se ignora.
- Cada campo informado agrega un `WHERE` adicional (logica AND).
- Texto: `LIKE '%valor%'`.
- Numero (`NUM_VIA`) y codigo postal (`NUM_COD_POSTAL`): igualdad exacta tras normalizacion.

Mapeo de campos:
- `Pais` -> `DES_PAIS`
- `Provincia` -> `DES_PROVNC`
- `ComunidadAutonoma` -> `DES_COMUNI_AUTO`
- `Municipio` -> `DES_MUNICP`
- `Poblacion` -> `DES_POBLCN`
- `TipoVia` -> `DES_TIPO_VIA`
- `Calle` -> `DES_NOMBRE_VIA`
- `Numero` -> `NUM_VIA`
- `Bloque` -> `DES_BLOQUE`
- `Puerta` -> `DES_PUERTA`
- `CodigoPostal` -> `NUM_COD_POSTAL`
- `Planta` -> `DES_PLANTA`

---

## 6. Configuracion del Plugin

### 6.1 appsettings.json

```json
{
  "ConnectionStrings": {
    "AssetResolverDb": "Server=servidor;Database=AAII;User Id=...;Password=...;"
  },
  "FieldAliases": {
    "Idufir": ["IDUFIR", "IDUFIR_CRU", "CRU", "CodigoRegistroUnico"],
    "ReferenciaCatastral": ["ReferenciaCatastral", "RefCatastral", "Catastral"],
    "DireccionCompleta": ["Localizacion", "Direccion", "DireccionCompleta", "Domicilio"]
  },
  "Logging": {
    "LogLevel": { "Default": "Information" }
  }
}
```

### 6.2 FieldAliases

Los aliases globales se usan como fallback cuando no hay mapeo explicito en la tipologia:

| Alias | Proposito | Defaults |
|-------|-----------|----------|
| `Idufir` | Detectar IDUFIR en extractedData | IDUFIR, IDUFIR_CRU, CRU, CodigoRegistroUnico |
| `ReferenciaCatastral` | Detectar RefCat | ReferenciaCatastral, RefCatastral, Catastral |
| `DireccionCompleta` | Detectar direccion completa | Localizacion, Direccion, DireccionCompleta, Domicilio |

Nota: no existen aliases para DireccionTipificada; se informa por objeto dedicado en request.

---

## 7. Tabla DM_POSICION_AAII_TB

### 7.1 Columnas Relevantes

| Columna | Tipo | Descripcion |
|---------|------|-------------|
| `ID_ACTIVO_SAREB` | varchar | Codigo unico del activo (PK logica). |
| `ID_IDUFIR` | varchar | Codigo IDUFIR del Registro de la Propiedad. |
| `ID_REF_CATAST` | varchar | Referencia Catastral. |
| `DES_NOMBRE_VIA` | varchar | Nombre de la via (calle, avenida, etc.). |
| `NUM_VIA` | varchar | Numero de portal. |
| `DES_MUNICP` | varchar | Nombre del municipio. |
| `NUM_COD_POSTAL` | varchar | Codigo postal. |
| `DES_PAIS` | varchar | Pais. |
| `DES_PROVNC` | varchar | Provincia. |
| `DES_COMUNI_AUTO` | varchar | Comunidad autonoma. |
| `DES_POBLCN` | varchar | Poblacion. |
| `DES_TIPO_VIA` | varchar | Tipo de via. |
| `DES_BLOQUE` | varchar | Bloque. |
| `DES_PUERTA` | varchar | Puerta. |
| `DES_PLANTA` | varchar | Planta. |
| `FCH_CIERRE` | date | Fecha de cierre del activo. |
| `FCH_ALTA` | date | Fecha de alta. |
| `FCH_BAJA` | date | Fecha de baja (null si activo). |
| `DES_SERVICER` | varchar | Servicer asignado. |

### 7.2 Columnas Obligatorias en Response

Siempre se incluyen en la respuesta:
- `ID_ACTIVO_SAREB`
- `FCH_CIERRE`
- `FCH_ALTA`
- `FCH_BAJA`
- `DES_SERVICER`

---

## 8. Ejemplos de Configuracion

### 8.1 Nota Simple 1.4 — Solo IDUFIR

```json
{
  "assetResolver": {
    "enabled": true,
    "busquedaIdufirHabilitada": true,
    "busquedaReferenciaCatastralHabilitada": false,
    "busquedaDireccionHabilitada": false,
    "mapeoIdufir": ["IDUFIR_CRU", "CRU"]
  }
}
```

### 8.2 Nota Simple 1.4 — Solo Direccion

```json
{
  "assetResolver": {
    "enabled": true,
    "busquedaIdufirHabilitada": false,
    "busquedaReferenciaCatastralHabilitada": false,
    "busquedaDireccionHabilitada": true,
    "mapeoDireccionCompleta": ["Localizacion"],
    "umbralScoreDireccion": 0.75
  }
}
```

### 8.3 Todos los criterios con AND

```json
{
  "assetResolver": {
    "enabled": true,
    "modoCombinacionCriterios": "AND",
    "busquedaIdufirHabilitada": true,
    "busquedaReferenciaCatastralHabilitada": true,
    "busquedaDireccionHabilitada": true,
    "mapeoIdufir": ["IDUFIR_CRU"],
    "mapeoReferenciaCatastral": ["ReferenciaCatastral"],
    "mapeoDireccionCompleta": ["Localizacion"],
    "umbralScoreDireccion": 0.8
  }
}
```

### 8.4 Busqueda por Direccion Tipificada

```json
{
  "assetResolver": {
    "enabled": true,
    "modoCombinacionCriterios": "OR",
    "busquedaIdufirHabilitada": false,
    "busquedaReferenciaCatastralHabilitada": false,
    "busquedaDireccionHabilitada": false,
    "busquedaDireccionTipificadaHabilitada": true
  }
}
```

---

## 9. Errores y Troubleshooting

| Sintoma | Causa | Solucion |
|---------|-------|----------|
| `Found=false`, mensaje "No se encontraron criterios" | Ningun criterio habilitado tiene valor en extractedData | Verificar mapeos y aliases. Revisar que extractedData contiene las claves esperadas. |
| Score de direccion siempre bajo | Direccion mal parseada o datos BD incompletos | Revisar logs de `direccionNormalizada`. Verificar que `NOM_VIA`, `NOM_MUNIC` estan poblados en BD. |
| Timeout en busqueda por direccion | Tabla muy grande + scoring en memoria | Considerar filtrar por provincia/municipio primero, o implementar busqueda en BD. |
| Excepcion `Index was out of range` en combinacion OR | Todos los criterios devolvian 0 y no habia conjuntos para combinar | Corregido en `CombinarResultados`: devuelve lista vacia si no hay criterios con resultados. |
| `camposConError` no vacio | Se solicitaron columnas inexistentes | Verificar nombres de columna en `requestedFields`. Usar `#ALL#` para descubrir columnas disponibles. |

---

## 10. Tests Funcionales con Script

Script principal:
- `scripts/Test-AssetResolver.ps1`

Escenarios actuales:
- `idufir-alias-default`
- `idufir-mapeo-personalizado`
- `refcat-directa`
- `idufir-override`
- `modo-or-dos-criterios`
- `modo-and-dos-criterios`
- `direccion-fuzzy`
- `campos-all`
- `campos-limitados`
- `direccion-tipificada`
- `direccion-tipificada-combinada`
- `sin-datos`

Resumen de salida:
- Tabla ASCII con columnas `Escenario`, `Descripcion`, `Resultado`, `IdsActivos`.
- `IdsActivos` muestra los `IdActivo` resueltos por escenario (o `-` si no hay resultado).

---

## 11. Tests Unitarios

El proyecto `DocumentIA.AssetResolver.Tests` incluye tests para:

| Test | Descripcion |
|------|-------------|
| `BuscarActivos_ConIdufirValido_RetornaActivo` | Busqueda basica por IDUFIR. |
| `BuscarActivos_DireccionCompletaDesdeLocalizacion_ResuelveActivo` | Parseo de direccion completa desde campo `Localizacion`. |
| `BuscarActivos_ModoAnd_ExigeCoincidenciaEnTodosLosCriteriosResueltos` | Verifica que AND retorna vacio si criterios no coinciden. |

Ejecutar:
```powershell
cd src\plugins\DocumentIA.AssetResolver.Tests
dotnet test
```

---

## 13. Contrato Funcional de Precedencia de Criterios y Propagacion de IdActivo

> **Estado**: Aprobado — 2026-04-21

Este apartado formaliza las reglas de precedencia y propagacion que rigen el comportamiento del plugin AssetResolver. Sirve como contrato funcional de referencia para desarrollo, pruebas y operaciones.

### 13.1 Precedencia en la Resolucion del Valor de Cada Criterio

Cada criterio (IDUFIR, ReferenciaCatastral, Direccion, DireccionTipificada) resuelve su valor de forma independiente siguiendo este orden de precedencia:

```
1. Override explicito de la peticion HTTP
   (instrucciones.assetResolver.camposBusqueda.idufir / .referenciaCatastral)

2. Valor en extractedData via aliases de tipologia
   (configuracion.mapeoIdufir, .mapeoReferenciaCatastral, .mapeoDireccionCompleta, ...)

3. Valor en extractedData via aliases globales del plugin
   (FieldAliases: Idufir, ReferenciaCatastral, DireccionCompleta)

4. Sin valor → criterio no participa en la busqueda
```

Nota: DireccionTipificada no usa aliases; su valor proviene siempre del objeto `request.direccionTipificada` (path 1 o directamente del campo `instrucciones.assetResolver.camposBusqueda.direccionTipificada` si se informa).

### 13.2 Orden de Fiabilidad de Criterios

Desde el punto de vista funcional, los criterios tienen la siguiente fiabilidad decreciente para identificar univocamente un activo:

| Prioridad | Criterio | Tipo de busqueda | Fiabilidad |
|-----------|----------|-----------------|------------|
| 1 | IDUFIR | Exacta (`ID_IDUFIR = @valor`) | Alta — identificador registral unico |
| 2 | Referencia Catastral | Exacta (`ID_REF_CATAST = @valor`) | Alta — identificador catastral unico |
| 3 | Direccion fuzzy | Scoring Jaccard en memoria | Media — sujeto a normalizacion y umbral |
| 4 | Direccion tipificada | Filtros AND en BD | Media — precision dependiente de campos informados |

Esta tabla de fiabilidad **no implica un orden de ejecucion**: todos los criterios habilitados se ejecutan en paralelo. Su combinacion se controla mediante `modoCombinacionCriterios` (OR / AND).

### 13.3 Reglas de Propagacion de IdActivo

El orquestador aplica las siguientes reglas tras recibir la respuesta del plugin:

| Escenario | Comportamiento |
|-----------|---------------|
| `Count = 0` (no encontrado) | IdActivo **no se modifica**. Se mantiene el valor de `trazabilidad.idActivo` de entrada. |
| `Count = 1` (match unico) | IdActivo **se reemplaza** por `activos[0].idActivo`. Este valor se propaga a IntegrarActivity y a `salida.Integridad.IdActivo`. |
| `Count > 1` (ambiguo) | IdActivo **no se modifica**. La lista de activos encontrados se incluye en `detalleEjecucion.assetResolver.activos` para consulta pero ninguno se selecciona automaticamente. |

**Regla de oro**: la propagacion automatica de IdActivo solo se produce con match unico inequivoco (`Count = 1`). Para los demas casos, el sistema es conservador y no sobreescribe el IdActivo de entrada.

### 13.4 Umbral de Scoring por Defecto

El umbral de scoring para busqueda por direccion fuzzy se rige por:

```
umbralScoreDireccion:
  configuracion tipologia  (si informado y > 0)
  ?? 0.75                  (valor por defecto)
```

El umbral de 0.75 es el valor inicial acordado. Puede ajustarse por tipologia en `ConfiguracionJson.assetResolver.umbralScoreDireccion`. Un valor de `0` o negativo se trata como `0.75` (evita umbral nulo).

### 13.5 Resumen del Contrato

```
┌─────────────────────────────────────────────────────────────────┐
│  CONTRATO FUNCIONAL ASSETRESOLVER — v1.0                        │
│                                                                 │
│  1. Precedencia valor: override > tipologia > global > nulo     │
│  2. Criterios: IDUFIR > RefCat > Direccion > DirecTipificada    │
│     (fiabilidad decreciente; ejecucion en paralelo)             │
│  3. Combinacion: OR (union) o AND (interseccion) configurable   │
│  4. Propagacion IdActivo: solo si Count == 1                    │
│  5. Umbral scoring direccion: 0.75 (ajustable por tipologia)    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 12. Changelog

| Fecha | Version | Cambios |
|-------|---------|---------|
| 2026-04-20 | 1.3.0 | - Alta de busqueda por Direccion Tipificada (`busquedaDireccionTipificadaHabilitada` + objeto `direccionTipificada`).<br/>- Direccion fuzzy: parseo mejorado para cadenas con piso/puerta y provincia en tercer segmento.<br/>- Combinacion OR robusta cuando todos los criterios devuelven 0 resultados.<br/>- Script funcional actualizado con escenarios tipificados y columna `IdsActivos` en resumen. |
| 2026-04-17 | 1.2.0 | - Busqueda por direccion como criterio de primera clase (no fallback).<br/>- Flags `busquedaIdufirHabilitada`, `busquedaReferenciaCatastralHabilitada`, `busquedaDireccionHabilitada`.<br/>- Modo combinacion AND/OR configurable.<br/>- Parseo automatico de direccion completa. |
| 2026-03-15 | 1.1.0 | - Direccion como fallback cuando IDUFIR/RefCat no tienen resultados.<br/>- Scoring fuzzy por direccion. |
| 2026-02-01 | 1.0.0 | - Busqueda por IDUFIR y Referencia Catastral.<br/>- Campos solicitados configurables.<br/>- Aliases globales y por tipologia. |
