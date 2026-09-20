# Diseño: pipeline dedicado de migrations EF Core (BD DocumentIA)

**Fecha:** 2026-08-07
**Estado:** aprobado (pendiente de implementación)
**Work item:** AB#100018

## Problema

Las migrations de EF Core de la BD `DocumentIA` se aplican hoy manualmente desde una máquina local (`dotnet ef migrations script` + `sqlcmd`), fuera de todo pipeline. Esto no deja artefacto auditable, depende de la sesión Entra de una persona y es un paso manual previo a cada despliegue. Además, `docs/procedimientos/DATABASE_MIGRATION_STRATEGY.md` afirma incorrectamente que el pipeline de Functions auto-ejecuta `dotnet ef database update` (no lo hace).

Situación al escribir esto: PRO tiene aplicada hasta `20260615053434_AddSummaryPrompts`; queda pendiente `20260805100351_AgregarSubmittedByEjecucion`. El primer run real del pipeline en prod aplicará exactamente esa migration.

## Decisiones tomadas

1. **Pipeline dedicado** `azure-pipelines-migrations.yml`, separado de los despliegues de aplicación. Mismo patrón que los pipelines existentes: `trigger: none`, parámetro `targetEnvironment` (`dev`/`pre`/`prod`), pool `windows-latest`, variables por entorno.
2. **Enfoque: script idempotente + aprobación** (elegido frente a `dotnet ef database update` directo y frente a migrations bundle): el SQL exacto queda publicado como artefacto del run y es revisable antes de aprobar el Apply en prod.
3. **Identidad: SPN del service connection** (Workload Identity Federation), sin secretos en el pipeline. Requiere alta one-time del usuario en la BD por entorno (ver prerequisitos).

## Arquitectura del pipeline

### Variables por entorno

| Entorno | Service connection | Servidor SQL |
|---|---|---|
| dev | `AI DocClassExt DEV` | `srbsqldevdocai` |
| pre | `AI DocClassExt PRE` | `srbsqlpredocai` |
| prod | `AI DocClassExt PRO` | `srbsqlprodocai` |

BD en todos los entornos: `DocumentIA`.

### Stage 1 — `Generate`

1. `UseDotNet@2` con SDK **8.0.x** (el proyecto `DocumentIA.Data` es `net8.0` con EF Core **8.0.1**; no hace falta el SDK 10 de Functions porque `--project` y `--startup-project` apuntan ambos a `DocumentIA.Data`, cuya `DocumentIADbContextFactory` de diseño resuelve el contexto).
2. `dotnet tool install dotnet-ef --version 8.0.*` (alineado con el paquete EF Core).
3. Generar script idempotente sin conectar a ninguna BD (la factory solo exige que exista una cadena; se le da una dummy vía `SqlConnectionString`):
   ```
   dotnet ef migrations script --idempotent --context DocumentIADbContext \
     --project src/backend/DocumentIA.Data --startup-project src/backend/DocumentIA.Data \
     -o $(Build.ArtifactStagingDirectory)/migrations.sql
   ```
4. Publicar `migrations.sql` como artefacto `MigrationsScript`.

### Stage 2 — `Apply`

Deployment job con `environment: ${{ parameters.targetEnvironment }}` (en el environment `prod` de ADO se configuran approvals, igual que el deploy de Functions). Pasos dentro de `AzureCLI@2` con el service connection del entorno:

1. **Pre-check:** token Entra (`az account get-access-token --resource https://database.windows.net`) + `Invoke-Sqlcmd -AccessToken` para leer `SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC`. Se registra en el log del run.
2. **Aplicar:** `Invoke-Sqlcmd -AccessToken ... -InputFile migrations.sql` (el script es idempotente: solo ejecuta lo pendiente).
3. **Verificar:** volver a leer la última `MigrationId` y compararla con la última migration del repo (derivada de listar `src/backend/DocumentIA.Data/Migrations/*.Designer.cs`). Si no coinciden → el stage falla.

Módulo `SqlServer` de PowerShell: `Install-Module SqlServer` en el agente si no está presente (windows-latest suele traerlo; el paso lo garantiza).

### Mitigación de firewall (riesgo conocido)

No está confirmado que los agentes hosted de Microsoft puedan alcanzar los servidores SQL (no hay visibilidad ARM sobre la config de red de PRO). Mitigación incluida en `Apply`:

- Paso opcional (parámetro `addTransientFirewallRule`, default `true`) que crea una regla de firewall con la IP pública del agente vía `az sql server firewall-rule create` antes de aplicar y la elimina en un paso con `condition: always()`.
- Si el SPN no tuviera permiso ARM para gestionar reglas de firewall, se detectará en el primer run en dev y se decidirá (conceder permiso puntual o activar "Allow Azure services").

## Prerequisito one-time por entorno (manual, admin Entra de la BD)

El SPN de cada service connection no existe hoy como usuario en la BD. Script parametrizado en `scripts/database/grant-pipeline-sql-user.sql` (una versión por entorno o con placeholder), a ejecutar una vez por un admin Entra del servidor:

```sql
CREATE USER [<display name del SPN>] FROM EXTERNAL PROVIDER;
ALTER ROLE db_ddladmin  ADD MEMBER [<display name del SPN>];
ALTER ROLE db_datareader ADD MEMBER [<display name del SPN>];
ALTER ROLE db_datawriter ADD MEMBER [<display name del SPN>];
```

- PRO: SPN `sareb-AI DocClassExt-1ebfe36f-0538-46de-be19-1db6b14c5be3` (appId `96ab6d96-57a6-425b-a668-7666ac96b5c5`). Los display names de DEV (`appId efff077f-…`) y PRE (`appId 1d2b166d-…`) se resuelven al preparar el script.
- Roles: `db_ddladmin` para el DDL; `db_datareader`/`db_datawriter` porque varias migrations hacen seeds (INSERT/UPDATE). Es el mismo trío que ya tiene el usuario `docaisql`.

**Plan B documentado (sin tocar la BD):** usar `DOCIA_SECRET_SQLCONNECTIONSTRING` del variable group `docia-bootstrap-{env}-secrets` (usuario SQL contenido `docaisql`, que ya tiene los tres roles). Solo si la vía SPN diese problemas en algún entorno.

## Documentación a actualizar

- `docs/procedimientos/DATABASE_MIGRATION_STRATEGY.md`: eliminar la afirmación de que el pipeline de Functions aplica migrations; documentar el pipeline nuevo como vía oficial (secciones 4 y 5).
- `docs/08_CHECKLISTS_DESPLIEGUE.md` (paso 8.3) y su copia en `entrega/3_operacion_y_despliegue/`: sustituir el paso manual de `sqlcmd` por la ejecución del pipeline.

## Fuera de alcance

- Automatizar el disparo de migrations dentro de los pipelines de aplicación (se mantiene como pipeline manual independiente; integrarlo como stage previo del deploy de Functions es una evolución futura).
- Blue-green / réplicas de BD descritas en la estrategia antigua.
- Rollback automatizado (`Down`): el procedimiento de rollback sigue siendo manual y documentado; el pipeline solo aplica hacia delante.

## Criterios de éxito

1. Run del pipeline en dev: no-op limpio (dev ya está al día) con artefacto `migrations.sql` publicado y verificación en verde.
2. Run en prod (con aprobación): aplica `20260805100351_AgregarSubmittedByEjecucion` y la verificación confirma que `__EFMigrationsHistory` queda alineada con el repo.
3. Ningún secreto nuevo en pipelines ni en el repo (auth por token del service connection).
