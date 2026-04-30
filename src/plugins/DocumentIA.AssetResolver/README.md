# DocumentIA.AssetResolver

Plugin responsable de resolver identificación de activos contra las tablas `DM_POSICION_AAII_TB` y `DM_POSICION_AACC_TB` (ODS).

## Propósito

Consumir datos extraídos (IDUFIR, Referencia Catastral u otros aliases) y devolver información de activos desde la base de datos de posiciones.

- Soporta búsqueda en uno o ambos orígenes mediante flags de request: `AAII_Search` y `AACC_Search`.
- Devuelve resultados separados por origen: `ActivosAAII` y `ActivosAACC`.
- Mantiene `Activos` como agregado legacy para compatibilidad.

## Últimos cambios (2026-04-15)

- Se añadieron campos obligatorios que siempre se incluyen en la respuesta: `FCH_ALTA`, `FCH_BAJA`, `DES_SERVICER`, `IND_STATUS`.
- Resolución por aliases: ahora solo se usa cuando *ambos* campos (IDUFIR y ReferenciaCatastral) vienen vacíos. Si alguno está indicado por override (`IdufirOverride` / `ReferenciaCatastralOverride`) o por mapeo en la tipología (`MapeoIdufir` / `MapeoReferenciaCatastral`), la búsqueda se realiza únicamente por el/los campos indicados.
- `appsettings.Development.json` actualizado para permitir conexión a SQL local durante desarrollo (ejemplo: `127.0.0.1,1433`).
- Añadido proyecto de tests unitarios: `src/plugins/DocumentIA.AssetResolver.Tests`.

## Requisitos (desarrollo)

- .NET 8.0 SDK
- Base de datos con las tablas `DM_POSICION_AAII_TB` y `DM_POSICION_AACC_TB` accesibles desde la cadena de conexión configurada.

## Ejecutar en modo Development

1. Establecer variable de entorno:

   - PowerShell (Windows): `$Env:ASPNETCORE_ENVIRONMENT='Development'`
   - Bash: `export ASPNETCORE_ENVIRONMENT=Development`

2. Ejecutar:

   ```bash
   cd src/plugins/DocumentIA.AssetResolver
   dotnet run
   ```

3. Swagger estará disponible en desarrollo y la API escucha por defecto en `http://localhost:5006`.

## Endpoint principal

- `POST /api/assets/GetAAIIInfo`
  - Header: `X-Api-Key` (la aplicación usa `ApiKeyMiddleware` para validar).
  - Body (ejemplo):

```json
{
  "CorrelationId": "c1",
  "ExtractedData": { "IDUFIR": "ID123" },
  "AAII_Search": true,
  "AACC_Search": true,
  "RequestedFields": ["ID_ACTIVO_SAREB"]
}
```

## Tests

- Proyecto de tests: `src/plugins/DocumentIA.AssetResolver.Tests`.
- Ejecutar:

  ```bash
  dotnet test src/plugins/DocumentIA.AssetResolver.Tests/DocumentIA.AssetResolver.Tests.csproj
  ```

## Notas

- Asegúrate de que la cadena de conexión `ConnectionStrings:AssetResolverDb` en `appsettings.Development.json` apunta a la base correcta que contiene `DM_POSICION_AAII_TB` y `DM_POSICION_AACC_TB`.
- En produccion, `ConnectionStrings:AssetResolverDb` se configura como Key Vault reference al secret `user-ods-dwh` en `srbkvprodocai`.
- Si aparecen errores de tabla no encontrada, verifica el entorno y privilegios del usuario de la cadena de conexión.

---

Para detalles de los cambios en toda la solución ver el README raíz: `../../README.md`.
