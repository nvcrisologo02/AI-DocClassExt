# generate-update-script.ps1

Genera un `.sql` de **solo UPDATEs** para llevar los datos de una tabla de un entorno a
otro. Al no haber linked servers entre los servidores SQL, la copia necesita esta fase
intermedia: el script lee del **origen** y emite un fichero que tú revisas y ejecutas
contra el **destino**.

## Qué hace y qué no

- **Sí:** actualiza las filas cuya clave existe en **ambos** entornos.
- **No:** no inserta filas nuevas, no borra las ausentes, y **nunca se conecta al destino**.

Como no se conecta al destino, no puede saber al generar qué claves existirán allí. Por eso
cada `UPDATE` lleva detrás un `IF @@ROWCOUNT = 0 PRINT 'AVISO: sin match en destino -> ...'`:
al ejecutar en el destino, esos avisos son las **claves huérfanas** (existen en origen, no
en destino).

## La clave debe ser de negocio, no el Id

`Id` es una columna identidad y **diverge entre entornos**: el `Id = 7` de dev no es la
misma fila que el `Id = 7` de pro. Usa `Codigo`, `PromptKey`, etc.

Si lo que necesitas es replicar el conjunto de configuración **preservando los Ids** (por
ejemplo para sostener la FK `CatalogoTdn2.Tdn1Id → CatalogoTdn1.Id`), esta no es tu
herramienta: usa `replicate-config-data.ps1`.

## Uso

```powershell
az login   # necesario para -EntraAuth

$src = "Server=tcp:<servidor-dev>.database.windows.net,1433;Database=DocumentIA;Encrypt=True;"

# Catalogo TDN1 completo, casando por Codigo
pwsh ./scripts/database/generate-update-script.ps1 -EntraAuth `
  -SourceConnectionString $src -Table CatalogoTdn1 -KeyColumns Codigo

# Solo el prompt de Phase 2
pwsh ./scripts/database/generate-update-script.ps1 -EntraAuth `
  -SourceConnectionString $src -Table CatalogoTdn1 -KeyColumns Codigo -Columns TDN2_Prompt

# Prompts activos, clave compuesta
pwsh ./scripts/database/generate-update-script.ps1 -EntraAuth `
  -SourceConnectionString $src -Table PromptTemplates `
  -KeyColumns PromptKey,Version -Where "IsActive = 1"
```

El `.sql` sale a `artifacts/db-config/update_<Tabla>_<timestamp>.sql` (carpeta ignorada por git).

## Parámetros

| Parámetro | Obligatorio | Descripción |
| --- | --- | --- |
| `-SourceConnectionString` | Sí | Cadena ADO.NET del origen. Con `-EntraAuth`, solo Server/Database/Encrypt. |
| `-Table` | Sí | Nombre de tabla. |
| `-KeyColumns` | Sí | Columnas de unión. Admite varias: `-KeyColumns PromptKey,Version`. |
| `-Columns` | No | Subconjunto a actualizar. Por defecto, todas menos clave, identidad y computadas. |
| `-Where` | No | Filtra las filas del origen. Sin el `WHERE`: `-Where "IsActive = 1"`. |
| `-Schema` | No | Por defecto `dbo`. |
| `-OutputFile` | No | Ruta del `.sql`. |
| `-NoBackup` | No | Omite el backup de la tabla destino (que va activado por defecto). |
| `-EntraAuth` | No | Obtiene el token vía `az login`. |
| `-SourceAccessToken` | No | Token explícito, alternativa a `-EntraAuth`. |

## Validaciones que abortan la generación

- La tabla o alguna columna no existe.
- `-Columns` incluye una columna clave, la identidad, una computada o una `rowversion`/`timestamp`.
- No queda ninguna columna que actualizar.
- **La clave no es única en el origen.** Es la más importante: con un `UPDATE` por fila, dos
  filas con la misma clave pisarían la misma fila del destino y el último ganaría en
  silencio. Ojo con `PromptTemplates`: `PromptKey` **solo** no es único (hay varias
  versiones), necesita `PromptKey,Version` o un `-Where "IsActive = 1"`.

Aviso que **no** aborta: si una columna clave admite NULL, las filas con NULL nunca casarán
(`WHERE col = NULL` no casa nunca).

Las columnas por defecto excluyen también las de tipo `rowversion`/`timestamp`, que SQL
Server genera sola y rechaza en un `UPDATE`.

## Nota de seguridad: `-Where`

El predicado de `-Where` se concatena **crudo** en las consultas contra el origen (es la
única forma de admitir un filtro arbitrario). Está pensado para que lo escriba a mano el
operador que ejecuta el script, con acceso de lectura a dev. **No lo alimentes desde una
fuente no confiable** (CI, entrada de usuario): sería inyección SQL clásica contra el
origen. El destino nunca se toca desde el script, así que el riesgo se limita al origen.

## Backup de la tabla destino

Por defecto, el `.sql` generado hace un **backup completo de la tabla destino antes de
actualizar**: un `SELECT * INTO [<esquema>].[<Tabla>__bak_<timestamp>]`. Detalles del diseño:

- Se crea **fuera de la transacción**, así que **persiste aunque el `UPDATE` se revierta o
  falle** (si estuviera dentro, un `ROLLBACK` borraría también el backup).
- El nombre lleva un timestamp de **ejecución** (`FORMAT(SYSUTCDATETIME(),...)`), no de
  generación: cada corrida del `.sql` crea su propio backup y no pisa los anteriores ni
  falla al re-ejecutar el mismo fichero.
- Con `XACT_ABORT ON`, si el backup falla el lote se aborta y el `UPDATE` **no** llega a
  ejecutarse: nunca hay `UPDATE` sin backup.

**Requisito:** el usuario que ejecuta el `.sql` en el destino necesita permiso
`CREATE TABLE` (p. ej. `db_ddladmin` o `db_owner`). Si no lo tiene, el backup falla con
`CREATE TABLE permission denied` y —correctamente— el `UPDATE` no se aplica. En ese caso,
pide el permiso o genera el script con **`-NoBackup`**.

Para restaurar, el backup es un snapshot completo de las filas; restaura con un
`UPDATE ... FROM [<Tabla>__bak_<timestamp>]` por la clave, o el método que prefieras. Los
backups no se limpian solos: bórralos cuando ya no los necesites.

## Ejecutar el .sql en el destino

El fichero va en **UTF-8 con BOM**, con el backup y los `UPDATE` en una transacción con
`XACT_ABORT ON`.

- **SSMS:** abre y ejecuta. Detecta el BOM correctamente.
- **sqlcmd:** usa `-f 65001` para la code page de entrada.

Para ensayar sin persistir los cambios, sustituye `COMMIT TRANSACTION` por
`ROLLBACK TRANSACTION` al final del fichero. Ojo: el **backup sí persiste** aunque hagas
`ROLLBACK`, porque se crea fuera de la transacción (justamente para eso).

## Tests

```bash
pwsh -NoProfile -Command "Invoke-Pester -Path ./scripts/database/tests/generate-update-script.Tests.ps1 -Output Detailed"
```

Cubren las funciones puras (escapado de literales, resolución de columnas, emisión del SQL,
codificación del fichero) y `Read-TableRows` con una conexión simulada sobre un reader real
(`DataTableReader`), incluido el caso de 0 filas. Las funciones que abren conexión
(`Get-TableMeta`, `Get-DuplicateKeys`, `New-SqlConnection`) se validan generando contra dev
y ejecutando el resultado contra la propia dev: debe casar todo, no dar ningún aviso y dejar
el `CHECKSUM_AGG` de la tabla intacto.
