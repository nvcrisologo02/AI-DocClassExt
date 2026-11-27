# Permisos de las service connections para la pipeline de release — DocumentIA

> Estado: **solicitud pendiente de enviar** (AB#101570, hito 1). Nadie la ha enviado a Plataforma todavía; este documento es el texto de la petición y la referencia de lo que comprobará la pipeline. No hay respuesta ni fecha comprometida. Fuente: apartado «Permisos de la service connection» de `docs/superpowers/specs/2026-11-26-pipeline-release-un-boton-design.md`.

La pipeline Release (hito 2) despliega un entorno de principio a fin con un solo lanzamiento y se vuelve atrás sola si algo falla. Para ello cada service connection necesita permisos que hoy no tiene todos. Sin ellos el hito 2 no se puede ensayar entero. Esta petición se envía a Plataforma antes de empezar el hito 2.

## 1. Qué se pide

Por cada entorno, a la service connection del entorno (la identidad con la que corre la pipeline):

| Necesidad | Rol o permiso | Recurso | Para qué paso de la pipeline |
|---|---|---|---|
| Borrar secretos heredados de Key Vault | Key Vault Secrets Officer (o equivalente que permita listar y borrar secretos) | Key Vault del entorno | Stage `Configuracion`: `scripts/configuration/remove-legacy-keyvault-secrets.ps1` (primero `-WhatIf`, luego real) |
| Copiar el artefacto de embeddings, lectura en el origen | Storage Blob Data Reader | Storage de documentos del entorno origen | Stage `Configuracion`: `scripts/storage/copy-embeddings-artifacts.ps1` |
| Copiar el artefacto de embeddings, escritura en el destino | Storage Blob Data Contributor | Storage de documentos del entorno destino | Stage `Configuracion`: `scripts/storage/copy-embeddings-artifacts.ps1` |
| Leer la clave de la cuenta del task hub de Durable | `listKeys` (viene con Contributor sobre la cuenta). Si la service connection no es Contributor sobre esa cuenta: Storage Table Data Reader sobre ella | Storage del task hub del entorno | Stage `Foto`: drenado de Durable (espera a que la tabla de instancias no tenga `Running` ni `Pending`) |
| Leer claves de función y reiniciar la Function App | Lectura de claves de función y reinicio | Function App del entorno | Stages `Foto`, `Configuracion`, `Altas` (reinicio y health) y `Verificacion` (la function key del smoke se lee en el momento y no se almacena) |
| Kudu con token de Entra | Contributor sobre el resource group del entorno (el token de ARM de la cuenta activa de `az` autentica contra `scm`) | Function App, Admin y AssetResolver del entorno | Stage `Foto`: descarga del paquete desplegado (vuelta atrás del código) |
| Escribir en Test Plans | Escritura en Test Plans del proyecto `AI DocClassExt` para la identidad del build (`System.AccessToken`, Project Collection Build Service) | Plan 100069 | Stage `Verificacion`: `tests/e2e-postdeploy/ado/publish-smk-run.ps1` (`-WhatIf` y real) |

Lo que ya existe y no se pide de nuevo: el despliegue de las tres apps y la aplicación de app settings (los pipelines 799, 800 y 802 ya lo hacen) y el acceso a SQL del entorno (`scripts/database/grant-pipeline-sql-user.sql` concede `db_ddladmin`, `db_datareader` y `db_datawriter`, suficientes para la tabla `ReleaseAplicada` y las copias `<Tabla>__rel_<BuildId>`; que el script esté aplicado en cada entorno es algo por confirmar).

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

Resource group de los storages: por confirmar (no consta en los ficheros consultados; se asume el del entorno, sin verificar).

Además, para la promoción de IA (`Promote`) la pipeline usa la service connection federada `AI DocClassExt Promocion IA` (la misma identidad opera origen y destino por exigencia de la Copy API de Content Understanding, ADR-001). Sus roles ya existen y no se piden de nuevo. Por confirmar: según `docs/procedimientos/DESPLIEGUE_IA.md` está autorizada solo para el pipeline 832, así que habrá que autorizarla también para la pipeline Release en el hito 2.

## 3. Petición por service connection

Comandos propuestos como referencia; la stage `Guard` del hito 2 fija los definitivos. La spec solo exige que `Guard` compruebe cada permiso con una lectura. Las propuestas no imprimen secretos (solo recuentos, estados o el nombre de la clave).

### 3.1 `AI DocClassExt DEV` (solo ensayos de la pipeline)

| Recurso | Rol | Comprobación de solo lectura de `Guard` |
|---|---|---|
| `srbkvdevdocai` | Key Vault Secrets Officer | `az keyvault secret list --vault-name srbkvdevdocai --query "length(@)"` |
| `srbstgdevdocai` | Storage Blob Data Reader. Contributor solo si el ensayo copia en DEV (`copy-embeddings-artifacts.ps1` admite `dev` como destino solo con `-DryRun`) | `az storage blob list --account-name srbstgdevdocai --container-name documentai --auth-mode login --num-results 1` |
| `srbstgdevappdocai` | Contributor (`listKeys`), o Storage Table Data Reader | `az storage account keys list --account-name srbstgdevappdocai --query "[0].keyName" -o tsv` |
| `srbappdevdocai` | Claves de función y reinicio | `az functionapp keys list -n srbappdevdocai -g SRBRGDEVDOCSAI --query "length(functionKeys)"`; `az functionapp show -n srbappdevdocai -g SRBRGDEVDOCSAI --query state -o tsv` |

### 3.2 `AI DocClassExt PRE`

| Recurso | Rol | Comprobación de solo lectura de `Guard` |
|---|---|---|
| `srbkvpredocai` | Key Vault Secrets Officer | `az keyvault secret list --vault-name srbkvpredocai --query "length(@)"` |
| `srbstgdevdocai` (origen de la copia; decidido: DEV → PRE, `-SourceEnvironment dev`) | Storage Blob Data Reader | `az storage blob list --account-name srbstgdevdocai --container-name documentai --auth-mode login --num-results 1` |
| `srbstgpredocai` (destino) | Storage Blob Data Contributor | `az storage blob list --account-name srbstgpredocai --container-name documentai --auth-mode login --num-results 1` (comprueba lectura; la escritura queda para el paso real) |
| `srbstgpreappdocai` | Contributor (`listKeys`), o Storage Table Data Reader | `az storage account keys list --account-name srbstgpreappdocai --query "[0].keyName" -o tsv` |
| `srbapppredocai` | Claves de función y reinicio | `az functionapp keys list -n srbapppredocai -g SRBRGPREDOCSAI --query "length(functionKeys)"`; `az functionapp show -n srbapppredocai -g SRBRGPREDOCSAI --query state -o tsv` |

### 3.3 `AI DocClassExt PRO`

| Recurso | Rol | Comprobación de solo lectura de `Guard` |
|---|---|---|
| `srbkvprodocai` | Key Vault Secrets Officer | `az keyvault secret list --vault-name srbkvprodocai --query "length(@)"` |
| `srbstgpredocai` (origen de la copia; decidido: PRE → PRO, `-SourceEnvironment pre`) | Storage Blob Data Reader | `az storage blob list --account-name srbstgpredocai --container-name documentai --auth-mode login --num-results 1` |
| `srbstgprodocai` (destino) | Storage Blob Data Contributor | `az storage blob list --account-name srbstgprodocai --container-name documentai --auth-mode login --num-results 1` |
| `srbstgproapppdocai` | Contributor (`listKeys`), o Storage Table Data Reader | `az storage account keys list --account-name srbstgproapppdocai --query "[0].keyName" -o tsv` |
| `srbappprodocai` | Claves de función y reinicio | `az functionapp keys list -n srbappprodocai -g SRBRGDOCSAIPROD --query "length(functionKeys)"`; `az functionapp show -n srbappprodocai -g SRBRGDOCSAIPROD --query state -o tsv` |

Decisión del usuario (2026-11-26): el artefacto de embeddings se copia por saltos, DEV → PRE y PRE → PRO, no DEV → PRO. El manifiesto de la v1.2.0 lleva un paso por salto (`-SourceEnvironment dev` para `pre`, `-SourceEnvironment pre` para `prod`). Por eso `AI DocClassExt PRO` necesita lectura (Storage Blob Data Reader) sobre `srbstgpredocai`, que es un recurso de otro entorno.

### 3.4 Test Plans (común a los tres entornos)

La identidad del build es el Project Collection Build Service del proyecto `AI DocClassExt` y necesita escritura en Test Plans sobre el plan 100069. Comprobación de `Guard`: lectura del plan con `System.AccessToken` (`GET .../_apis/testplan/plans/100069?api-version=7.1`), que prueba el acceso pero no la escritura. La escritura se verifica en el ensayo en DEV con `publish-smk-run.ps1` (`-WhatIf` y real). Por confirmar.

## 4. Cómo se comprueba (stage `Guard`)

`Guard` ejecuta `scripts/release/Test-ReleasePermisos.ps1` con la service connection del entorno, antes de abrir ninguna ventana y sin modificar nada. Solo hace lecturas y no imprime secretos: el token de Entra solo se envía en la cabecera `Authorization` de la petición a Kudu. Si falla alguna lectura, el run termina en rojo (código de salida 2) con una línea por fallo, `<recurso> - <rol que falta> - PERMISOS_SPN_RELEASE.md, apartado 3.x`, y no se toca el entorno.

Lecturas definitivas, por entorno (las tablas del apartado 3 llevan los comandos de referencia):

| Comprobación | Operación | Rol que cubre | dev | pre | prod |
|---|---|---|---|---|---|
| Key Vault del entorno | `az keyvault secret list` | Key Vault Secrets Officer | `srbkvdevdocai` | `srbkvpredocai` | `srbkvprodocai` |
| Storage de documentos de destino | `az storage blob list` (contenedor `documentai`, `--auth-mode login`) | Storage Blob Data Reader/Contributor | `srbstgdevdocai` | `srbstgpredocai` | `srbstgprodocai` |
| Storage de documentos de origen de la copia | `az storage blob list` (ídem) | Storage Blob Data Reader | no aplica | `srbstgdevdocai` | `srbstgpredocai` |
| Storage del task hub | `az storage account keys list` | Contributor (`listKeys`) o Storage Table Data Reader | `srbstgdevappdocai` | `srbstgpreappdocai` | `srbstgproapppdocai` |
| Function App: claves de función | `az functionapp keys list` | lectura de claves de función | `srbappdevdocai` | `srbapppredocai` | `srbappprodocai` |
| Function App: estado | `az functionapp show` | lectura y reinicio de la Function App | `srbappdevdocai` | `srbapppredocai` | `srbappprodocai` |
| Kudu alcanzable (las tres apps) | `GET https://<app>.scm.azurewebsites.net/api/settings` con token de Entra de la SC (Contributor sobre el RG) | Contributor sobre el resource group y acceso de red a `scm` | las tres apps de dev | las tres apps de pre | las tres apps de prod |
| Acceso SQL | `SELECT 1` en la base `DocumentIA` del servidor del entorno (`SQL_SERVER_NAME`) | `db_datareader` y `db_datawriter` (`grant-pipeline-sql-user.sql`) | `srbsqldevdocai` | `srbsqlpredocai` | `srbsqlprodocai` |

La autenticación básica de SCM está deshabilitada en las apps y no hace falta: Guard y la foto de código usan el token de Entra de la service connection, y la autenticación básica no se reactiva.

Los grupos de recursos son `SRBRGDEVDOCSAI` (dev), `SRBRGPREDOCSAI` (pre) y `SRBRGDOCSAIPROD` (prod).

Lo que `Guard` no comprueba:

- Test Plans (apartado 3.4): el script no lee el plan 100069. La escritura se ejercita en el ensayo en DEV con `publish-smk-run.ps1`.
- La service connection federada `AI DocClassExt Promocion IA`: no la usa `Guard`.
- Permisos de escritura. Una lectura no demuestra borrar secretos, subir blobs ni escribir en Test Plans. Esos se ejercitan antes en el modo de solo lectura de cada script (`-WhatIf` o `-DryRun`) y, la primera vez, en el ensayo en DEV.

## 5. Si falta algún permiso

- Sin permisos de Key Vault, Storage o Kudu, la pipeline no se puede ensayar entera en DEV y el hito 2 queda bloqueado en esa parte.
- El camino manual de `docs/releases/v1.2.0/GUIA_OPERADOR_PRE.md` sigue disponible como respaldo y no depende de esta petición.

## 6. Pendiente

- [ ] Enviar la petición a Plataforma (quién la envía: por designar).
- [ ] Anotar aquí la fecha de envío, la respuesta y los roles realmente concedidos por service connection.
- [ ] Conceder a `AI DocClassExt PRO` lectura sobre `srbstgpredocai` (origen de la copia PRE → PRO, decidido el 2026-11-26) y resolver los puntos «por confirmar»: resource group de los storages, autorización de `AI DocClassExt Promocion IA` para la pipeline Release, escritura en Test Plans y aplicación de `grant-pipeline-sql-user.sql` en cada entorno.
