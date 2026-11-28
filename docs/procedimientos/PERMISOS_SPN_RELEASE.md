# Permisos de las service connections para la pipeline de release — DocumentIA

> Estado: **inventario verificado el 2026-11-27 y permisos completos** (AB#101570 y AB#101583). Los roles que faltaban los asignó el usuario ese mismo día; no queda nada que pedir a Plataforma. La pipeline Release (id 842, `azure-pipelines-release.yml`) se ensayó en DEV con estos permisos (runs en `pipelines/README.md`, apartado «Ensayo»). Fuente de la decisión de diseño: apartado «Permisos de la service connection» de `docs/superpowers/specs/2026-11-26-pipeline-release-un-boton-design.md`.

La pipeline Release despliega un entorno de principio a fin con un solo lanzamiento y se vuelve atrás sola si algo falla. Cada service connection necesita para ello los permisos de este documento. Las lecturas se hicieron con la identidad del usuario (ARM, Azure DevOps REST y SQL con token); lo que no se pudo comprobar así se marca como «no verificado».

## 1. Qué necesita la pipeline

Por cada entorno, la service connection del entorno (la identidad con la que corre la pipeline) necesita:

| Necesidad | Rol o permiso | Recurso | Para qué paso de la pipeline |
|---|---|---|---|
| Borrar secretos heredados de Key Vault | Key Vault Secrets Officer (o equivalente que permita listar y borrar secretos) | Key Vault del entorno | Stage `Configuracion`: `scripts/configuration/remove-legacy-keyvault-secrets.ps1` (primero `-WhatIf`, luego real) |
| Copiar el artefacto de embeddings, lectura en el origen | Storage Blob Data Reader | Storage de documentos del entorno origen | Stage `Configuracion`: `scripts/storage/copy-embeddings-artifacts.ps1` |
| Copiar el artefacto de embeddings, escritura en el destino | Storage Blob Data Contributor | Storage de documentos del entorno destino | Stage `Configuracion`: `scripts/storage/copy-embeddings-artifacts.ps1` |
| Leer la clave de la cuenta del task hub de Durable | `listKeys` (viene con Contributor sobre el resource group) | Storage del task hub del entorno | Stage `Foto`: drenado de Durable (espera a que la tabla de instancias no tenga `Running` ni `Pending`) |
| Leer claves de función y reiniciar la Function App | Lectura de claves de función y reinicio (Contributor sobre el resource group) | Function App del entorno | Stages `Foto`, `Configuracion`, `Altas` (reinicio y health) y `Verificacion` (clave maestra del smoke, ver «Clave maestra» más abajo) |
| Kudu con token de Entra | Contributor sobre el resource group del entorno (el token de ARM de la cuenta activa de `az` autentica contra `scm`) | Function App, Admin y AssetResolver del entorno | Stage `Foto`: descarga del paquete desplegado (vuelta atrás del código). La comprobación y la descarga corren desde un agente alojado (`windows-latest`): el `scm` de la Function App no resuelve desde el pool privado (endpoint privado sin enlace DNS a la VNet del pool) |
| Acceso SQL | `db_ddladmin`, `db_datareader` y `db_datawriter` (`scripts/database/grant-pipeline-sql-user.sql`) | Base `DocumentIA` del entorno | Registro `ReleaseAplicada` y copias `<Tabla>__rel_<BuildId>` |
| Escribir en Test Plans | Escritura en Test Plans del proyecto `AI DocClassExt` para la identidad del build (`System.AccessToken`, Project Collection Build Service) | Plan 100069 | Stage `Verificacion`: `tests/e2e-postdeploy/ado/publish-smk-run.ps1` (`-WhatIf` y real) |

Clave maestra: leer las claves de la Function App (`az functionapp keys list`) devuelve también la clave maestra (`masterKey`), más sensible que la clave `default`: permite administrar la app y ejecutar cualquier función. El smoke la usa a propósito. La ingesta (`IngestDocument`) está deshabilitada mientras dura la release y solo la clave maestra puede ejecutar una función deshabilitada (con cualquier otra clave responde 404). La clave se lee en el momento, se enmascara en el log y no se almacena ni se escribe en evidencias. Quien pueda leer las claves de función de una app (Contributor sobre su resource group) puede por tanto usar la clave maestra.

## 2. Recursos por entorno

Valores de `pipelines/templates/variables/entorno.yml` y de `docs/infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md`.

| | DEV | PRE | PRO |
|---|---|---|---|
| Service connection | `AI DocClassExt DEV` | `AI DocClassExt PRE` | `AI DocClassExt PRO` |
| Resource group | `SRBRGDEVDOCSAI` | `SRBRGPREDOCSAI` | `SRBRGDOCSAIPROD` |
| Key Vault | `srbkvdevdocai` | `srbkvpredocai` | `srbkvprodocai` |
| Storage del task hub (Durable) | `srbstgdevappdocai` | `srbstgpreappdocai` | `srbstgproapppdocai` (tres «p») |
| Storage de documentos (embeddings, contenedor `documentai`) | `srbstgdevdocai` | `srbstgpredocai` | `srbstgprodocai` |
| Function App | `srbappdevdocai` | `srbapppredocai` | `srbappprodocai` |
| Admin | `srbwebadmindevdocai` | `srbwebadminpredocai` | `srbwebadminprodocai` |
| AssetResolver | `srbwebpluginassetresolverdev` | `srbwebpluginassetresolverpre` | `srbwebpluginassetresolver` |
| Servidor SQL | `srbsqldevdocai` | `srbsqlpredocai` | `srbsqlprodocai` |

Los seis storages están en el resource group de su entorno (verificado el 2026-11-27).

Decisión del usuario (2026-11-26): el artefacto de embeddings se copia por saltos, DEV → PRE y PRE → PRO, no DEV → PRO. El manifiesto de la v1.2.0 lleva un paso por salto (`-SourceEnvironment dev` para `pre`, `-SourceEnvironment pre` para `prod`). Por eso `AI DocClassExt PRO` lee del storage de documentos de PRE (otro entorno y otra suscripción) y `AI DocClassExt PRE`, del de DEV.

## 3. Inventario por service connection

Las cuatro service connections son de tipo WIF (federación de identidad de carga de trabajo), del tenant `1a213c5a-2e3d-4ae4-b0ba-075c42f9700e`. Roles leídos con `az role assignment list --all` por suscripción y filtrados por `principalId`. Evidencia detallada (temporal, no versionada): `docs/auxiliares/temps/2026-11-27/permisos-sc-verificacion-2026-11-27.md`. Los apartados 3.1, 3.2 y 3.3 son los que cita `Guard` en sus mensajes de fallo (`apartado 3.1` dev, `3.2` pre, `3.3` prod).

Las seis asignaciones marcadas «asignado» las hizo el usuario el 2026-11-27 con `scripts/configuration/assign-release-sc-permissions.ps1` (PRO con elevación PIM) y se releyeron con `az role assignment list --all` por suscripción: las seis estaban presentes.

### 3.1 `AI DocClassExt DEV` (solo ensayos de la pipeline)

appId `efff077f-57d1-41c5-b66f-7b0d2e00fc0e`, object id `cea262dd-ba86-4647-9245-45b75ee357f3`.

| Rol | Recurso | Origen |
|---|---|---|
| Contributor | RG `SRBRGDEVDOCSAI` | existente |
| Key Vault Secrets Officer | `srbkvdevdocai` | existente |
| Storage Blob Data Reader | `srbstgdevdocai` | asignado el 2026-11-27 |

Sin roles en PRE ni PRO. `copy-embeddings-artifacts.ps1` admite `dev` como destino solo con `-DryRun`, por eso no necesita Contributor de datos.

### 3.2 `AI DocClassExt PRE`

appId `1d2b166d-e608-487e-9711-5c5fba9b157a`, object id `3fc220ce-15f0-4818-b64f-c2cbcba3d7b7`.

| Rol | Recurso | Origen |
|---|---|---|
| Contributor | RG `SRBRGPREDOCSAI` | existente |
| Key Vault Secrets Officer | `srbkvpredocai` | existente |
| Storage Blob Data Reader | `srbstgdevdocai` (suscripción de DEV; origen de la copia DEV → PRE) | asignado el 2026-11-27 |
| Storage Blob Data Contributor | `srbstgpredocai` | asignado el 2026-11-27 |

Sin roles en DEV ni PRO (salvo la lectura de blobs de DEV indicada).

### 3.3 `AI DocClassExt PRO`

appId `96ab6d96-57a6-425b-a668-7666ac96b5c5`, object id `4c989e87-cddd-4fea-8ac7-42a3526173ff`.

| Rol | Recurso | Origen |
|---|---|---|
| Contributor | RG `SRBRGDOCSAIPROD` | existente |
| Key Vault Secrets User | `srbkvprodocai` | existente (no puede borrar secretos) |
| Key Vault Secrets Officer | `srbkvprodocai` | asignado el 2026-11-27 (conserva además Secrets User) |
| Storage Blob Data Reader | `srbstgpredocai` (suscripción de PRE; origen de la copia PRE → PRO) | asignado el 2026-11-27 |
| Storage Blob Data Contributor | `srbstgprodocai` | asignado el 2026-11-27 |

Sin roles en DEV ni PRE (salvo la lectura de blobs de PRE indicada).

### 3.4 `AI DocClassExt Promocion IA`

appId `543f5a54-57b0-463d-b7d3-b3a0e542d507`, object id `cad10f65-0486-4714-a04d-164d1ae9ef33`. La usa `Promote` (la misma identidad opera origen y destino por exigencia de la Copy API de Content Understanding, ADR-001). No se asignó nada nuevo.

| Rol | Recurso | Origen |
|---|---|---|
| Reader | RG de DEV y de PRE | existente |
| Cognitive Services User | recursos de IA de los tres entornos | existente |
| Storage Blob Data Contributor | `srbstgdevdocai`, `srbstgpredocai`, `srbstgprodocai` y `srbstgproapppdocai` | existente |

### 3.5 Otras comprobaciones (2026-11-27)

- Contributor sobre el resource group cubre reinicio, claves de función, Kudu con token de Entra y `listKeys` del storage del task hub (el drenado usa `--auth-mode key`). No da roles de datos de blob: por eso hacían falta los Storage Blob Data Reader/Contributor.
- Los tres Key Vault usan RBAC (`enableRbacAuthorization=true`). El de PRO conserva una access policy (oid `91530052-…`) con get, list, set y delete que no aplica con RBAC.
- SQL: usuario `sareb-AI DocClassExt-<endpoint>` con `db_ddladmin`, `db_datareader` y `db_datawriter` en `DocumentIA` de DEV, PRE y PRO (`grant-pipeline-sql-user.sql` aplicado en los tres). El SID de ese usuario `FROM EXTERNAL PROVIDER` es el appId, no el object id.
- Para resolver object id con `az ad sp show` hay que hacer antes `az logout` y `az login`: con el token de Graph en caché falla con `TokenCreatedWithOutdatedPolicies`.
- Test Plans: Project Build Service con permiso `Create test runs` y `View test runs` en el namespace del proyecto. La escritura real quedó confirmada en los ensayos: el smoke publicó los Test Plan runs 1076994 (run 80302) y 1077000 (run 80306).
- Autorización de pipelines por service connection a fecha 2026-11-27, antes de crear la Release: DEV 799, 800, 802, 803, 807, 832; PRE 799, 800, 801, 802, 803, 807, 832; PRO 799, 800, 801, 802, 807, 832; `Promocion IA` solo 832. Lectura anterior a la pipeline 842.

### 3.6 Qué no está verificado

- Autorización de la pipeline Release (842) en cada service connection. Evidencia indirecta de los ensayos: los runs de DEV (80301, 80302, 80303 y 80306) usaron la conexión de DEV y `Promocion IA` (stage `IA`, `Promote`) y pasaron, por lo que ambas están autorizadas para la 842; la de PRE solo se ejercitó en `Guard` (runs 80307 y 80308, la rama se rechazó después); la de PRO no se ha ejercitado con la 842 y su autorización no está comprobada.
- Escritura real de las service connections de PRE y PRO en Key Vault y storage: los roles se asignaron y se releyeron, pero ninguna escritura se ha ejercitado en PRE ni PRO (en DEV los ensayos solo copian en modo `-DryRun`).
- Los permisos de escritura de los scripts se ejercitan en el primer uso real de cada entorno; una lectura de `Guard` no los demuestra.

## 4. Qué comprueba `Guard`

La stage `Guard` ejecuta `scripts/release/Test-ReleasePermisos.ps1` (lo invoca `scripts/release/Invoke-ReleaseGuard.ps1`) con la service connection del entorno, antes de abrir ninguna ventana y sin modificar nada. Solo hace lecturas y no imprime secretos: el token de Entra solo se envía en la cabecera `Authorization` de la petición a Kudu. Si falla alguna lectura, el run termina en rojo (código de salida 2) con una línea por fallo, `<recurso> - <rol que falta> - PERMISOS_SPN_RELEASE.md, apartado 3.x` (3.1 dev, 3.2 pre, 3.3 prod), y no se toca el entorno.

| Comprobación | Operación | Rol que cubre | dev | pre | prod |
|---|---|---|---|---|---|
| Key Vault del entorno | `az keyvault secret list` | Key Vault Secrets Officer | `srbkvdevdocai` | `srbkvpredocai` | `srbkvprodocai` |
| Storage de documentos de destino | `az storage blob list` (contenedor `documentai`, `--auth-mode login`, un resultado) | Storage Blob Data Reader/Contributor | `srbstgdevdocai` | `srbstgpredocai` | `srbstgprodocai` |
| Storage de documentos de origen de la copia | `az storage blob list` (ídem) | Storage Blob Data Reader | no aplica | `srbstgdevdocai` | `srbstgpredocai` |
| Storage del task hub | `az storage account keys list` | Contributor (`listKeys`) o Storage Table Data Reader | `srbstgdevappdocai` | `srbstgpreappdocai` | `srbstgproapppdocai` |
| Function App: claves de función | `az functionapp keys list` (solo la longitud de `masterKey`) | lectura de claves de función | `srbappdevdocai` | `srbapppredocai` | `srbappprodocai` |
| Function App: estado | `az functionapp show` | lectura y reinicio de la Function App | `srbappdevdocai` | `srbapppredocai` | `srbappprodocai` |
| Acceso SQL | `SELECT 1` en la base `DocumentIA` del servidor del entorno (`SQL_SERVER_NAME`) | `db_datareader` y `db_datawriter` (`grant-pipeline-sql-user.sql`) | `srbsqldevdocai` | `srbsqlpredocai` | `srbsqlprodocai` |
| Kudu alcanzable (las tres apps) | `GET https://<app>.scm.azurewebsites.net/api/settings` con token de Entra (Bearer), tiempo máximo 30 s | Contributor sobre el resource group | las tres apps de dev | las tres apps de pre | las tres apps de prod |

Reparto por job: las lecturas de Key Vault, storage y SQL corren en `GuardJob` (pool privado `docia-mdp-private`, con `-SinKudu`). La comprobación de Kudu corre en `GuardKudu` (agente alojado `windows-latest`, con `-SoloKudu`), porque el `scm` de las apps no resuelve desde el pool privado. La stage falla si falla cualquiera de los dos.

La autenticación básica de SCM está deshabilitada en las apps y no hace falta: Guard y la foto de código usan el token de Entra de la service connection, y la autenticación básica no se reactiva.

Los grupos de recursos son `SRBRGDEVDOCSAI` (dev), `SRBRGPREDOCSAI` (pre) y `SRBRGDOCSAIPROD` (prod).

Lo que `Guard` no comprueba:

- Test Plans: `Guard` no lee el plan 100069 ni prueba el permiso del Build Service. La escritura se ejercita en la stage `Verificacion` con `publish-smk-run.ps1` (confirmada en los ensayos de DEV, apartado 3.5).
- La service connection `AI DocClassExt Promocion IA` (la usa `Promote`, no `Guard`).
- Permisos de escritura. Una lectura no demuestra borrar secretos, subir blobs ni escribir en Test Plans. Esos se ejercitan antes en el modo de solo lectura de cada script (`-WhatIf` o `-DryRun`) y, la primera vez, en el entorno real.
- La autorización de la propia pipeline sobre cada service connection (apartado 3.6).

## 5. Si falta algún permiso

- `Guard` falla en rojo con el recurso y el rol que falta y no toca el entorno. Se resuelve asignando el rol (en PRE y PRO, con la elevación PIM correspondiente) y relanzando.
- Para volver a asignar los roles de este documento: `scripts/configuration/assign-release-sc-permissions.ps1` (con `-WhatIf` solo muestra lo que haría; idempotente; escribe asignaciones de rol, por lo que solo lo ejecuta el usuario).
- El camino manual de `docs/releases/v1.2.0/GUIA_OPERADOR_PRE.md` sigue disponible como respaldo y no depende de la pipeline.

## 6. Pendiente

La petición a Plataforma se cerró sin enviarse: no hace falta, los roles los asignó el usuario el 2026-11-27 y están releídos. Queda solo:

- [ ] Confirmar la autorización de la pipeline Release (842) en `AI DocClassExt PRO` (la de PRE se ejercitó solo en `Guard`), y la de PRE en un run completo (primer uso real en PRE: v1.2.0, AB#101584).
- [ ] Ejercitar la escritura real en Key Vault y storage de PRE y PRO en el primer uso de cada entorno (apartado 3.6).
