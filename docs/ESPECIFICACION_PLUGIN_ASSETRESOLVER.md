# Especificacion Tecnica: Plugin AssetResolver

> Ultima actualizacion: 2026-04-17  
> Proyecto: AI DocClassExt — SAREB  
> Componente: `DocumentIA.AssetResolver`

---

## 1. Proposito

El plugin **AssetResolver** es un servicio HTTP independiente que resuelve el **IdActivo** (codigo de activo inmobiliario SAREB) a partir de datos extraidos de documentos. Consulta la tabla maestra `DM_POSICION_AAII_TB` usando tres criterios de busqueda configurables: IDUFIR, Referencia Catastral y Direccion.

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
                                                       │   DM_POSICION_AAII_TB    │
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
  "idufirOverride": null,
  "referenciaCatastralOverride": null,
  "modoCombinacionCriterios": "OR",
  "busquedaIdufirHabilitada": true,
  "busquedaReferenciaCatastralHabilitada": false,
  "busquedaDireccionHabilitada": true,
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
| `requestedFields` | string[] | `null` | Columnas a retornar. `#ALL#` = todas. `null` = solo obligatorias. |
| `idufirOverride` | string | `null` | Valor IDUFIR explicito (ignora extractedData). |
| `referenciaCatastralOverride` | string | `null` | Valor RefCat explicito. |
| `modoCombinacionCriterios` | string | `"OR"` | `AND` = interseccion, `OR` = union. |
| `busquedaIdufirHabilitada` | bool | `true` | Si `false`, IDUFIR no se usa aunque haya aliases. |
| `busquedaReferenciaCatastralHabilitada` | bool | `true` | Si `false`, RefCat no se usa. |
| `busquedaDireccionHabilitada` | bool | `false` | Si `true`, activa busqueda fuzzy por direccion. |
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
    }
  },
  "activos": [
    {
      "idActivo": "SAR-001234",
      "fchCierre": "2025-12-31",
      "camposSolicitados": { "NOM_VIA": "CALLE MAYOR", "NUM_VIA": "1" }
    }
  ],
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

3. Si ningun criterio tiene valor resuelto:
   └─ Retornar Found=false, mensaje "No se encontraron criterios"

4. Ejecutar busquedas independientes:
   └─ IDUFIR: SELECT WHERE ID_IDUFIR = @valor
   └─ RefCat: SELECT WHERE ID_REF_CATAST = @valor
   └─ Direccion: Scoring fuzzy sobre todos los registros

5. Combinar resultados segun modoCombinacionCriterios:
   └─ OR: UNION de resultados
   └─ AND: INTERSECT de resultados (si algun criterio = 0, resultado = 0)

6. Deduplicar por ID_ACTIVO_SAREB

7. Construir response con campos solicitados
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

### 4.3 Algoritmo de Scoring por Direccion

El algoritmo calcula un score [0.0-1.0] comparando la direccion de entrada contra cada fila de `DM_POSICION_AAII_TB`:

```
Score = (CodigoPostal * 0.30) + (Municipio * 0.30) + (NombreVia * 0.30) + (Numero * 0.10)
```

Cada componente se evalua con similitud fuzzy (Levenshtein normalizado):

| Componente | Peso | Columna BD | Comparacion |
|------------|------|------------|-------------|
| CodigoPostal | 30% | `COD_POST` | Exacta (1.0 o 0.0) |
| Municipio | 30% | `NOM_MUNIC` | Fuzzy, normalizado a minusculas |
| NombreVia | 30% | `NOM_VIA` | Fuzzy, normalizado |
| Numero | 10% | `NUM_VIA` | Exacta |

**Normalizacion**: se eliminan tildes, se convierte a minusculas, y se remueven caracteres especiales.

### 4.4 Parseo de Direccion Completa

Cuando se usa `mapeoDireccionCompleta`, la cadena se parsea automaticamente:

```
Entrada: "CALLE MAYOR 1, 28013 MADRID"

1. Extraer CP: regex \b\d{5}\b → "28013"
2. Remover CP de la cadena: "CALLE MAYOR 1, MADRID"
3. Split por coma: ["CALLE MAYOR 1", "MADRID"]
4. Ultimo segmento = Municipio: "MADRID"
5. Primer segmento = Via + Numero
6. Extraer numero: regex \b\d+[A-Z]?\b → "1"
7. Resto = NombreVia: "CALLE MAYOR"

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

---

## 7. Tabla DM_POSICION_AAII_TB

### 7.1 Columnas Relevantes

| Columna | Tipo | Descripcion |
|---------|------|-------------|
| `ID_ACTIVO_SAREB` | varchar | Codigo unico del activo (PK logica). |
| `ID_IDUFIR` | varchar | Codigo IDUFIR del Registro de la Propiedad. |
| `ID_REF_CATAST` | varchar | Referencia Catastral. |
| `NOM_VIA` | varchar | Nombre de la via (calle, avenida, etc.). |
| `NUM_VIA` | varchar | Numero de portal. |
| `NOM_MUNIC` | varchar | Nombre del municipio. |
| `COD_POST` | varchar | Codigo postal. |
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

---

## 9. Errores y Troubleshooting

| Sintoma | Causa | Solucion |
|---------|-------|----------|
| `Found=false`, mensaje "No se encontraron criterios" | Ningun criterio habilitado tiene valor en extractedData | Verificar mapeos y aliases. Revisar que extractedData contiene las claves esperadas. |
| Score de direccion siempre bajo | Direccion mal parseada o datos BD incompletos | Revisar logs de `direccionNormalizada`. Verificar que `NOM_VIA`, `NOM_MUNIC` estan poblados en BD. |
| Timeout en busqueda por direccion | Tabla muy grande + scoring en memoria | Considerar filtrar por provincia/municipio primero, o implementar busqueda en BD. |
| `camposConError` no vacio | Se solicitaron columnas inexistentes | Verificar nombres de columna en `requestedFields`. Usar `#ALL#` para descubrir columnas disponibles. |

---

## 10. Tests Unitarios

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

## 11. Changelog

| Fecha | Version | Cambios |
|-------|---------|---------|
| 2026-04-17 | 1.2.0 | - Busqueda por direccion como criterio de primera clase (no fallback).<br/>- Flags `busquedaIdufirHabilitada`, `busquedaReferenciaCatastralHabilitada`, `busquedaDireccionHabilitada`.<br/>- Modo combinacion AND/OR configurable.<br/>- Parseo automatico de direccion completa. |
| 2026-03-15 | 1.1.0 | - Direccion como fallback cuando IDUFIR/RefCat no tienen resultados.<br/>- Scoring fuzzy por direccion. |
| 2026-02-01 | 1.0.0 | - Busqueda por IDUFIR y Referencia Catastral.<br/>- Campos solicitados configurables.<br/>- Aliases globales y por tipologia. |
