# Generador de scripts UPDATE entre entornos (dev → pro)

**Fecha:** 2026-07-16
**Estado:** Diseño aprobado — pendiente de plan de implementación

## Problema

Los cambios de configuración (catálogos y prompts) se preparan y prueban en **dev** y
después hay que llevarlos a **pro**. No hay linked servers entre los servidores SQL de
los entornos, de modo que no es posible un `INSERT ... SELECT` cruzado: hace falta una
**fase intermedia** que extraiga los datos del origen y produzca un artefacto ejecutable
contra el destino.

Casos inmediatos: `CatalogoTdn1` (columna `TDN2_Prompt`, prompts de clasificación Phase 2)
y `PromptTemplates` (columna `Content`). La operación se repite con frecuencia y entre
varios entornos (dev/pre/pro), así que el mecanismo debe ser genérico y no una migración
puntual.

**Objetivo:** un script PowerShell que, dada **una tabla** y **las columnas que actúan como
clave**, se conecte al origen y emita un `.sql` de **solo UPDATEs** listo para revisar y
ejecutar a mano en el destino.

### Por qué la clave no puede ser el `Id`

`CatalogoTdn1.Id` y `PromptTemplates.Id` son columnas **identidad**. Sus valores divergen
entre entornos: el `Id = 7` de dev no es la misma fila que el `Id = 7` de pro. La única
unión estable entre entornos es la **clave de negocio** (`Codigo`, `PromptKey` + `Version`).
Esto es lo que distingue esta herramienta de `replicate-config-data.ps1`, que hace MERGE
por PK con `IDENTITY_INSERT` para **preservar** los Id (necesario allí para sostener la FK
`CatalogoTdn2.Tdn1Id → CatalogoTdn1.Id`).

## Decisiones acordadas

| Decisión | Valor |
| --- | --- |
| Semántica | **Solo UPDATE.** Filas que casan por clave en ambos entornos. Sin INSERT, sin DELETE |
| Clave | `-KeyColumns` **acepta varias columnas** (`PromptTemplates` = `PromptKey` + `Version`) |
| Columnas a actualizar | Todas menos clave/identidad/computadas; `-Columns` acota a un subconjunto |
| Ubicación | **Script nuevo autocontenido**; `replicate-config-data.ps1` queda **intacto** |
| Forma del SQL | **Un `UPDATE` por fila**, cada uno seguido de un aviso `IF @@ROWCOUNT = 0` |
| Alcance | **Solo genera** el `.sql`. No se conecta nunca al destino. No hay modo Apply/Copy |
| Filtro de origen | `-Where` opcional (p.ej. `IsActive = 1` en `PromptTemplates`) |
| Salida | `artifacts/db-config/` (ya en `.gitignore`, línea 99) |
| Codificación | **UTF-8 con BOM** |

### Decisiones descartadas y por qué

- **Upsert / espejo:** rechazados. Solo-UPDATE es lo pedido y lo más seguro para pro: no
  toca identidades ni FKs, y no puede borrar nada.
- **Añadir un `-Mode UpdateOnly` a `replicate-config-data.ps1`:** rechazado. Choca con su
  contrato (lista fija de tablas en orden FK, MERGE por PK, `IDENTITY_INSERT`, `-Mirror`,
  modos Apply/Copy), que quedaría inerte o incorrecto. Resultaría un script con dos
  personalidades.
- **Módulo `.psm1` compartido:** rechazado. Evita duplicar los helpers, pero obliga a tocar
  un script que ya funciona contra pro. Se asume el coste conocido: `ConvertTo-SqlLiteral`
  y la detección de esquema quedan **duplicados** en dos ficheros.
- **Staging temporal / CTE con VALUES:** rechazados frente a un `UPDATE` por fila, que es
  revisable línea a línea y permite comentar filas sueltas antes de ejecutar en pro.

## Contexto técnico verificado

- **Literales largos no se truncan.** Un literal `N'...'` que excede 4.000 caracteres se
  promociona implícitamente a `nvarchar(max)`
  ([nchar and nvarchar](https://learn.microsoft.com/sql/t-sql/data-types/nchar-and-nvarchar-transact-sql#remarks)).
  La truncación a `nvarchar(4000)` solo afecta a la **concatenación** de literales. Como
  cada valor se emite como literal único, `TDN2_Prompt` (`nvarchar(max)`) y `Content`
  (hasta 16.000 chars por CHECK constraint) son seguros.
- **Codificación del fichero.** sqlcmd solo omite la conversión de code page si el fichero
  de entrada es Unicode; en otro caso aplica la code page actual
  ([sqlcmd utility](https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility#command-line-options)).
  Los prompts llevan acentos, así que un UTF-8 **sin** BOM puede leerse como Windows-1252 y
  corromper el contenido. Se emite **con** BOM. Nota operativa: sqlcmd auto-reconoce UTF-16;
  para UTF-8 puede requerir `-f 65001` según versión. SSMS detecta el BOM correctamente.
- **Esquema de las tablas objetivo:**
  - `CatalogoTdn1`: `Id` (identidad, PK), `Codigo` (natural, ≤10), `Nombre`, `Descripcion`,
    `TDN2_Prompt` (`nvarchar(max)`).
  - `PromptTemplates`: `Id` (identidad, PK), `PromptKey` + `Version` (natural **compuesta**,
    con `UQ_PromptTemplate_Key_Version`), `Content`, `IsActive`, auditoría.
- **Autenticación.** Los servidores SQL usan Entra ID. Se reutiliza el patrón ya probado en
  `replicate-config-data.ps1`: `az account get-access-token --resource https://database.windows.net/`
  y asignación a `SqlConnection.AccessToken`. La cadena de conexión solo lleva
  Server/Database/Encrypt.
- **Lección heredada de `replicate-config-data.ps1`:** un `SqlDataReader` implementa
  `IEnumerable`; devolverlo desde una función PowerShell lo desenrolla y lo rompe. El
  `ExecuteReader()` debe hacerse **siempre en el sitio de uso**, nunca en un helper que
  retorne el reader.

## Diseño

### Artefacto

Fichero único: `scripts/database/generate-update-script.ps1`. Autocontenido.

### Parámetros

| Parámetro | Obligatorio | Descripción |
| --- | --- | --- |
| `-SourceConnectionString` | Sí | Cadena ADO.NET del origen. |
| `-Table` | Sí | Nombre de tabla, p.ej. `CatalogoTdn1`. |
| `-KeyColumns` | Sí | `string[]`. Columnas de unión: `Codigo`, o `PromptKey,Version`. |
| `-Columns` | No | `string[]`. Subconjunto a actualizar. Por defecto, todas las elegibles. |
| `-Where` | No | Predicado que filtra las filas del origen, sin el `WHERE`. |
| `-Schema` | No | Por defecto `dbo`. |
| `-OutputFile` | No | Por defecto `artifacts/db-config/update_<Tabla>_<timestamp>.sql`. |
| `-EntraAuth` | No | Obtiene token vía `az login`. |
| `-SourceAccessToken` | No | Token explícito, alternativa a `-EntraAuth`. |

### Flujo

1. **Conectar** al origen (token Entra si procede).
2. **Leer el esquema real** desde `sys.columns` / `sys.indexes`: columnas, cuáles son
   identidad, cuáles computadas, nulabilidad. Nada hardcodeado — el script resiste cambios
   de columnas.
3. **Validar y abortar si procede** (ver sección siguiente).
4. **Leer las filas**: `SELECT <clave + columnas> FROM <tabla> [WHERE <-Where>]`.
5. **Emitir** el `.sql` en UTF-8 con BOM.

### Validaciones (el valor real del script)

Todas se ejecutan **antes** de generar nada. Un fallo aborta con mensaje explícito.

| Validación | Motivo |
| --- | --- |
| La tabla existe y tiene columnas | Error temprano y claro en vez de SQL inválido. |
| `-KeyColumns` existen en la tabla | Ídem. |
| `-Columns` existen y no son clave, identidad ni computada | Un `SET` sobre una identidad o computada falla en destino; sobre la clave es incoherente. |
| El conjunto de columnas a actualizar no queda vacío | Un `UPDATE` sin `SET` no es SQL válido. |
| **La clave es única en el origen** | **Crítico.** Con un `UPDATE` por fila, una clave duplicada en dev genera dos UPDATEs contra la misma fila de pro y **el último gana en silencio**. Se comprueba con `GROUP BY <clave> HAVING COUNT(*) > 1`. |
| Aviso (no aborta) si alguna columna clave es nullable | `WHERE col = NULL` nunca casa; esas filas se perderían sin ruido. |

### Forma del `.sql` generado

```sql
-- ============================================================================
-- DocumentIA - UPDATE de configuracion entre entornos
-- Origen : srbsqldevdocai.database.windows.net/DocumentIA
-- Tabla  : [dbo].[CatalogoTdn1]
-- Clave  : [Codigo]
-- Set    : [Nombre], [Descripcion], [TDN2_Prompt]
-- Filtro : (ninguno)
-- Filas  : 23        Generado: 2026-07-16 10:22:31
--
-- SOLO UPDATE: no inserta ni borra filas.
-- Para ensayar sin persistir, sustituye COMMIT TRANSACTION por ROLLBACK TRANSACTION.
-- ============================================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

UPDATE [dbo].[CatalogoTdn1]
SET [Nombre] = N'Escrituras',
    [Descripcion] = N'Escrituras publicas de compraventa',
    [TDN2_Prompt] = N'Eres un clasificador...'
WHERE [Codigo] = N'ESC';
IF @@ROWCOUNT = 0 PRINT 'AVISO: sin match en destino -> [Codigo] = ESC';

COMMIT TRANSACTION;
PRINT 'Generado desde srbsqldevdocai/DocumentIA. Filas emitidas: 23.';
```

Clave compuesta → `WHERE [PromptKey] = N'classification.phase1.system' AND [Version] = 3`.

El `IF @@ROWCOUNT = 0 PRINT` recupera la detección de **claves huérfanas** (filas que
existen en dev pero no en pro). Es necesario porque el generador nunca se conecta al
destino: no puede saber al generar qué casará. Sin ese aviso, la semántica solo-UPDATE
descartaría esas filas en silencio.

### Tipado de literales

Se **copia** la lógica de `ConvertTo-SqlLiteral` de `replicate-config-data.ps1`, ya probada
(el script es autocontenido por decisión, ver arriba): `NULL`, `byte[]` → `0x...`, `bool` →
`1`/`0`, fechas en formato ISO redondo, `guid`, numéricos con `InvariantCulture`, y todo lo
demás → `N'...'` con `'` escapada como `''`.

Consecuencia asumida de la duplicación: un fallo de escapado corregido en un fichero no
llega al otro. Si en el futuro aparece un tercer consumidor, ese es el momento de extraer
el módulo compartido que aquí se descartó.

## Verificación

Prueba objetiva **sin tocar pro**, aprovechando que un UPDATE de dev sobre dev es un no-op
semántico:

1. `CHECKSUM_AGG` sobre `CatalogoTdn1` en dev → valor A.
2. Generar con `-Table CatalogoTdn1 -KeyColumns Codigo`.
3. Ejecutar el `.sql` **contra la propia dev**.
4. Esperado: **0 avisos** de huérfanas, `CHECKSUM_AGG` sigue siendo A, y el recuento de
   filas actualizadas coincide con el de filas emitidas.

Esto ejercita conexión, token, lectura de esquema, escapado de literales (incluidos
`TDN2_Prompt` con acentos, comillas y saltos de línea) y la forma del SQL. Casos
adicionales:

- **Clave compuesta:** `-Table PromptTemplates -KeyColumns PromptKey,Version`.
- **Filtro:** `-Table PromptTemplates -KeyColumns PromptKey,Version -Where "IsActive = 1"`.
- **Subconjunto:** `-Table CatalogoTdn1 -KeyColumns Codigo -Columns TDN2_Prompt`.
- **Fallos esperados:** clave duplicada en origen aborta; `-Columns Id` aborta; tabla
  inexistente aborta.
- **Codificación:** abrir el `.sql` generado en SSMS y confirmar que los acentos de
  `TDN2_Prompt` se ven correctamente.

## Fuera de alcance

- Modos Apply/Copy contra el destino. El script solo genera; la ejecución en pro es manual.
- INSERT de claves nuevas y DELETE de claves ausentes.
- Sincronización multi-tabla en orden FK — eso ya lo cubre `replicate-config-data.ps1`.
- Comparación previa dev↔pro: requeriría conectar a ambos, y se decidió no conectar al destino.
