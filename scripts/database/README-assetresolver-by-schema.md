# Activación de AssetResolver por esquema (refcat/IDUFIR)

> **Estado:** aplicado en **dev** y **prod** el 2026-07-09. Resultado verificado y detalle
> por tipología en `docs/superpowers/specs/2026-07-09-assetresolver-activacion-por-esquema-design.md`
> (sección "Registro de despliegue"). Este runbook queda como referencia para re-ejecución
> o para nuevas tipologías.

## Ficheros

- `enable-assetresolver-by-schema.sql` — backup + activación.
- `rollback-assetresolver-by-schema.sql` — restauración desde backup.
- `tests/test-assetresolver-by-schema.sql` — validación autocontenida.

## Requisitos del motor SQL Server

El script de test (`tests/test-assetresolver-by-schema.sql`) hace `CREATE DATABASE` +
`USE [$(TestDb)]` para levantar una base sintética (`DocumentIA_ScriptTest`) y ejecutar
ahí sus aserciones. Esto requiere un **motor SQL Server real** con permiso para crear
bases de datos: contenedor local (`docker run mcr.microsoft.com/mssql/server`), instancia
on-prem o Azure SQL Managed Instance. **No funciona contra Azure SQL Database PaaS**
(single database o elastic pool): en ese servicio cada conexión está anclada a una única
base y `USE` no cambia de base de datos dentro de la misma conexión, por lo que el
harness fallaría al intentar `USE [$(TestDb)]` tras el `CREATE DATABASE`.

Los scripts `enable-assetresolver-by-schema.sql` y `rollback-assetresolver-by-schema.sql`
no tienen esta limitación: no usan `CREATE DATABASE` ni `USE`, operan directamente sobre
`dbo.Tipologias` de la base indicada con `-d`, y por tanto **sí funcionan contra la BBDD
DocumentIA del entorno** (dev/pre/prod), sea cual sea el tipo de servicio SQL subyacente.

## Procedimiento por entorno (dev → pre → prod)

Ejecutar los entornos en orden estricto **dev → pre → prod**. En cada entorno, revisar
siempre el dry-run (`@WhatIf=1`) antes de pasar a `@WhatIf=0`; no avanzar de entorno
hasta confirmar que el resultado del dry-run y de la aplicación son los esperados.

1. Dry-run:
   `sqlcmd -S <srv> -d <DocumentIA> -G -v WhatIf=1 -i enable-assetresolver-by-schema.sql -b`
   Revisar el resultset de tipologías afectadas y el `assetResolver` resultante.

2. Aplicar:
   `sqlcmd -S <srv> -d <DocumentIA> -G -v WhatIf=0 -i enable-assetresolver-by-schema.sql -b`
   Anotar el nombre de tabla `Tipologias_AssetResolverBak_<stamp>` que imprime el script
   (mensaje `Backup creado: dbo.Tipologias_AssetResolverBak_<yyyyMMdd_HHmmss>`); ese
   nombre es el que se necesitará como `@BackupTable` para un eventual rollback en este
   entorno.

3. Verificación funcional: ingesta de un documento por tipología afectada; comprobar
   `DetalleEjecucion.AssetResolver.Ejecutado = true`.

4. Rollback (si procede):
   `sqlcmd -S <srv> -d <DocumentIA> -G -v BackupTable="<tabla_backup>" WhatIf=0 -i rollback-assetresolver-by-schema.sql -b`

Solo tras cerrar dev (dry-run + aplicación + verificación, y rollback si hiciera falta)
se repite la misma secuencia en pre, y solo tras cerrar pre se repite en prod.

## Primera ejecución en dev: verificación del valor de `enabled`

En la primera ejecución del harness de test en dev, comprobar explícitamente el valor
que devuelve `JSON_VALUE(ConfiguracionJson,'$.assetResolver.enabled')` tras la
aplicación: debe ser el literal `'true'`, **no** `'1'`. Es el comportamiento esperado de
`JSON_MODIFY` al escribir un valor `BIT` (lo serializa como booleano JSON `true`/`false`,
no como el `1`/`0` que usaría T-SQL para un `BIT`), y las aserciones del test (por
ejemplo la de `preexisting-ar`) comparan literalmente contra `'true'`. Si en el entorno
se observara `'1'` en lugar de `'true'`, sería señal de una versión de motor con
comportamiento distinto y motivo para detener el rollout y revisar antes de seguir a
pre/prod.

## Nombre de la tabla de backup: cuidado con ejecuciones repetidas en el mismo segundo

`enable-assetresolver-by-schema.sql` nombra la tabla de backup como
`Tipologias_AssetResolverBak_<yyyyMMdd_HHmmss>`, con resolución de un segundo. Si se
ejecuta el script dos veces dentro del mismo segundo en el mismo entorno, la segunda
ejecución intentará crear una tabla con el mismo nombre que la primera y fallará al
crear el backup (o colisionará con una tabla ya existente). En la práctica esto no
supone un riesgo real porque la operación es manual y por entorno (no hay automatismos
que disparen ejecuciones consecutivas), pero conviene tenerlo presente: si por error se
lanza el `enable` dos veces seguidas muy rápido, esperar al menos un segundo entre
ejecuciones o verificar el nombre de tabla resultante antes de continuar.

## Robustez a casing (camelCase / PascalCase)

Los `ConfiguracionJson` en prod conviven en **dos casings**: unos en camelCase
(`fields`, `assetResolver`…) y otros en PascalCase (`Fields`, `AssetResolver`…). Como los
paths de `OPENJSON`/`JSON_VALUE`/`JSON_QUERY` son **sensibles a mayúsculas**, el `enable`:

- **lee** cada clave con `COALESCE($.xxx, $.Xxx)` (detecta ambos casings), y
- **escribe** en dos ramas según el casing de la fila (`$.assetResolver.*` vs
  `$.AssetResolver.*`), para no crear una clave duplicada que rompa la lectura
  case-insensitive del backend.

En el dry-run, la columna `IsPascal` indica el casing de cada fila. Tipologías afectadas
que sólo aparecen por esta corrección (PascalCase, antes ignoradas): `cera.15`, `cera.16`
(3 colecciones: `Calcula`/`DireccionPropiedades`/`Resumen` — revisar si `Calcula` procede),
`cera.44.vado`, `cera.46` (refcat anidado en `Resumen`), `nota.simple_bal` (planos).

## Retención

Las tablas `Tipologias_AssetResolverBak_*` se conservan hasta validar; borrado manual y
explícito.
