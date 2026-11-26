# Pipeline de release de un botón: Fase 2 en PRE y PRO sin pasos manuales (diseño)

Fecha: 2026-11-26 · Línea: core · Work item: pendiente de PBI (se crea con sí explícito; derivado de AB#101570) · Estado: aprobado por el usuario el 2026-11-26 (tras la revisión del constructor; siguiente paso: plan)

## Intención

Quien lanza las releases a PRE y a PRO es un operador ajeno a DocumentIA. Hoy la Fase 2 de la release v1.2.0 son 21 pasos manuales (`docs/releases/v1.2.0/GUIA_OPERADOR_PRE.md`): tres pipelines lanzados en orden, ocho scripts con `-DryRun`/`-WhatIf` y ejecución real, consultas, reinicios y una pausa de la ingesta acordada fuera del sistema. El riesgo principal de la release no está en el código sino en que un operador que no conoce el sistema se equivoque de orden, de parámetro o de entorno.

Se quiere una pipeline única de Azure DevOps, la misma para PRE y para PRO, que el operador lance con un botón eligiendo solo el entorno, que ejecute toda la Fase 2 en el orden ya decidido para la v1.2.0, que lea de una carpeta por versión lo que cambia en cada release (scripts de configuración, versión y modo del clasificador por embeddings, deployment de IA), y que ante cualquier fallo deshaga sola lo hecho hasta ahí y deje el entorno como estaba. El responsable del producto entra al final: puertas 2 y 3 y Go. Las próximas releases traerán nuevas versiones de embeddings y posibles cambios de modelo: la pipeline es fija y los datos van en la carpeta de la versión.

La ventana de PRE de la v1.2.0 se retrasa para hacerla ya con esta pipeline (decisión del usuario, 2026-11-26).

## Objetivos, no objetivos y anti-objetivos

Objetivos:

- Un solo run de pipeline por release y entorno; el operador elige el entorno y aprueba una vez.
- Misma secuencia para PRE y PRO; lo que cambia por release está en `releases/<versión>/`.
- Fallo en cualquier stage: vuelta atrás automática de las capas tocadas, entorno como al empezar, run en rojo con el motivo en el resumen.
- Repetir la pipeline sobre un entorno ya actualizado es inocuo (idempotencia, con registro en BD de lo aplicado).
- 807, 832 y 799 siguen existiendo y funcionando como hoy (decisión del usuario: no se elimina nada), y la guía manual queda como camino de respaldo.
- Puerta 1 (smoke) dentro de la pipeline antes de volver a admitir ingesta; registro del smoke en el Test Plan 100069 desde la pipeline.

No objetivos:

- Puertas 2 (golden) y 3 (coste): fuera, las lanza el responsable del producto como hoy (paso 16 y 19 de la guía). Puerta 4 (deriva) se cubre con el export final de la pipeline.
- Fases 4 y 5 del procedimiento (Go, tag, `master`, `docs/releases/`): siguen siendo del responsable.
- Avisos automáticos: Azure DevOps ya notifica a quien lanzó el run; el operador avisa al responsable (decisión del usuario).
- Modo mantenimiento con respuesta 503 explicativa (cambio de producto): queda como mejora posterior.
- Vuelta atrás de los analyzers de CU y del esquema de BD (las capas 3 y 4 del Anexo B siguen siendo manuales, como hoy).

Anti-objetivos (fracaso aunque funcione):

- Que el operador tenga que leer un log para decidir algo.
- Que la pipeline deje PRE o PRO a medias sin avisar.
- Que una release sin scripts nuevos exija tocar el YAML.
- Que los secretos (cadenas de conexión, function keys) acaben en artefactos del pipeline.

## Hechos verificados el 2026-11-26

- Los tres pipelines están en la raíz del repo: `azure-pipelines.yml` (799, build y despliegue de Functions, Admin y AssetResolver, settings decisivos, borrado de settings heredados, ValidateConfiguration; pool hosted `windows-latest`), `azure-pipelines-ai-artifacts.yml` (832, Guard, Export, AiArtifacts, ConfigSeed, ConfigApply; pool privado `docia-mdp-private`) y `azure-pipelines-migrations.yml` (807, Generate en hosted y Apply en el pool privado). Ninguno usa `templates/`, `extends`, `resources` ni grupos de variables; solo el 799 incluye la plantilla de variables `infra/ai/pipeline-variables.yml`. Ninguno valida la rama por entorno. No existe carpeta `pipelines/` ni `templates/`.
- Service connections: `AI DocClassExt DEV`, `AI DocClassExt PRE`, `AI DocClassExt PRO` y `AI DocClassExt Promocion IA` (832). Environments `dev`, `pre` y `prod`; el check 64 (aprobación manual) está en `pre` y afecta a cada deployment job.
- Todos los scripts que hoy ejecuta el operador se autentican con la sesión `az` activa (token de Entra para SQL, `--auth-mode login` para blobs, `az keyvault`/`az rest` para el resto): en un pipeline, esa sesión es la de la service connection (`AzureCLI@2`). Ninguno tiene credencial propia. Tienen `-DryRun`/`-WhatIf`: `aplicar-luna.ps1`, `copy-embeddings-artifacts.ps1`, `set-lifecycle-documents-clasif.ps1`, `remove-legacy-keyvault-secrets.ps1`, `publish-smk-run.ps1`, `apply-deployments.ps1`. No lo tienen `apply-sql-dev.ps1`, `query-sql.ps1` ni `run-e2e-postdeploy.ps1`.
- No hay registro de scripts SQL de configuración aplicados: el rastro son copias `<Tabla>__bak_<sufijo>` y la idempotencia interna de cada script. El 807 usa `__EFMigrationsHistory` (EF estándar); los `indice-*-pro.sql` se registran a mano en esa tabla.
- El 799 publica los artefactos `drop-functions`, `drop-admin` y `drop-assetresolver`; el 832 `db-config`, `ai-artifacts-<env>-<intento>`, `config-diff` y `config-apply-<intento>`; el 807 `MigrationsScript`.
- El 07 de embeddings (`scripts/migrations/clasificador-embeddings/07-modeloconfig-embeddings-modo-version.sql`) toma `@Modo` y `@Version` de sus `DECLARE`: hoy se copia y se edita a mano.
- Con la identidad permanente del operador humano, sin PIM, fallan `appsettings list`, `keys list`, `restart` y `appsettings set` sobre `srbapppredocai`; la service connection no tiene esa limitación (despliega el 799).
- La pausa de la ingesta no tiene interruptor en el producto; la hace quien envía documentos. Durante la release no hay tráfico en PRO (se cierra el sistema). El task hub `DocumentIAHub` vive en `srbstgpreappdocai` (PRE); leer sus instancias exige rol de datos de tablas o la clave de la cuenta.
- La guía de la v1.2.0 fija el orden de la Fase 2 (decisión del usuario del 2026-11-26, no se reabre): 832 sin altas → pausa de ingesta → 799 → `aplicar-luna.ps1` → reinicio → copia del artefacto v5 → 07 → 832 con altas → reinicio → export y diff → smoke → golden → deriva → Test Plan → coste → Go.

## Enfoques considerados

1. **Pipeline orquestadora por REST.** YAML nuevo que lanza 807, 832 y 799 por la API y espera, con stages propias para lo manual. No toca los pipelines existentes; a cambio, cuatro o cinco runs encadenados, aprobaciones en runs distintos, esperas frágiles y la vuelta atrás del código tiene que relanzar el 799 por REST. Descartado: el operador seguiría viendo varios runs y varias aprobaciones.
2. **Pipeline única por templates (elegido).** Los jobs de 807, 832 y 799 se extraen a templates y se componen en un YAML de release con las stages en el orden de la guía; los tres pipelines actuales pasan a usar los mismos templates y siguen lanzándose igual. Un run, una aprobación, logs juntos, vuelta atrás como stage. Cuesta refactorizar tres YAML que funcionan, con ensayo obligatorio en DEV.
3. Copiar los pasos de los tres pipelines dentro del YAML nuevo. Descartado: dos copias que divergen en la primera release.

El usuario eligió el 2 el 2026-11-26, con la condición de conservar 807, 832 y 799 como pipelines independientes y la guía manual como respaldo.

## Decisiones

Marcadas como «usuario» las que tomó el usuario; el resto son propuestas de este diseño con su motivo, y se pueden corregir.

### Lo que ve el operador

- Pipeline nueva `Release` (`azure-pipelines-release.yml`), un parámetro: `targetEnvironment` con valores `pre` y `prod` (y `dev` solo para ensayos, marcado como tal en la descripción del parámetro). Nada más que elegir (propuesta: cada parámetro adicional es una decisión que el operador no puede tomar).
- La versión no se escribe: la pipeline la lee de `releases/CURRENT` (una línea, por ejemplo `v1.2.0`), que mantiene el responsable del producto en la Fase 0. La stage `Guard` comprueba que existe `releases/<versión>/release.json`, que la rama es la correcta para el entorno (`develop` para `pre`, `master` para `prod`) y que la versión aún no está registrada como aplicada en ese entorno (si lo está, el run termina en verde sin hacer nada y lo dice).
- Una sola aprobación por run: la stage de foto corre como deployment job sobre el environment del entorno (`pre`/`prod`, check 64) y las stages siguientes son jobs normales con la misma service connection. Motivo: en Azure DevOps cada stage que referencia el environment vuelve a pedir aprobación. El constructor comprueba que las service connections no tienen checks propios por stage. Los environments `dev` y `prod` conservan los checks que tengan hoy (solo está documentado el 64 de `pre`); la pipeline no los cambia.
- Pools: build de la solución y Generate de migraciones en el pool hosted `windows-latest`, como hoy; el resto de stages (Foto, Apply, IA, despliegue, Configuracion, Altas, Verificacion, Fin, VueltaAtras) en el pool privado `docia-mdp-private`, porque es el que llega a SQL y a los storages por red privada. Service connections como hoy: la del entorno destino para desplegar, leer claves, reiniciar y aplicar SQL; la del origen para el Export del 832 (DEV para PRE, PRE para PRO, fijo por entorno); `AI DocClassExt Promocion IA` para Promote.
- Al terminar, en verde o en rojo, la pipeline publica un artefacto `release-<env>-<build>` con `resumen.md` (versión, hash, hora de cada stage, resultado del smoke, diff de configuración, qué capas se deshicieron si hubo fallo) y los ficheros que hoy la guía manda a `evidencias/` (export, diff, salida del smoke, salida de los scripts). Sin secretos: nunca el listado completo de app settings.
- El aviso: Azure DevOps notifica a quien lanzó; el operador avisa al responsable (usuario).

### Lo que cambia por release: `releases/<versión>/`

- `release.json`, manifiesto corto: `version`; `embeddings` (`version`, `modo`, `restringidoModo`; opcional); `pasos` (lista ordenada; cada paso es un `.sql` que se aplica con `apply-sql-dev.ps1 -PermitirNoDev` o un `.ps1` con sus argumentos, con `entornos` donde aplica). Ejemplo para la v1.2.0: `aplicar-luna.ps1 -Environment <env>` (la pipeline lanza antes su `-DryRun` y exige exit 0), `copy-embeddings-artifacts.ps1 -Version v5 -TargetEnvironment <env>` (ídem `-DryRun`), y el 07 generado desde el bloque `embeddings` del manifiesto (la pipeline escribe una copia del 07 con los `DECLARE` del manifiesto, igual que hoy hace el operador a mano).
- Lo que ya tiene su sitio no se duplica: los deployments de IA siguen en `infra/ai/deployments.<env>.json` (los aplica el template del 832), las altas por clave natural las sigue calculando ConfigSeed, y `PROMPT_DEFAULTS_MODEL_KEY` y el resto de settings decisivos siguen en el bloque por entorno del YAML del 799 (propuesta: una sola fuente de verdad por dato; el manifiesto solo lleva lo que hoy no tiene sitio).
- Una release sin scripts nuevos tiene un manifiesto con `pasos: []` y la pipeline hace solo código, IA, altas y smoke.
- Cada paso lleva `entornos`. Los de la v1.2.0 llevan `[pre, prod]`: `aplicar-luna.ps1` rechaza `dev` a propósito (en DEV luna ya está aplicada script a script) y la copia del artefacto no tiene sentido con DEV como destino. Para que el ensayo en DEV ejercite la stage `Configuracion` y su vuelta atrás, existe `releases/_ensayo/release.json`, que `Guard` usa en lugar de `CURRENT` cuando el entorno es `dev`: un paso `.sql` inocuo que modifica una fila de `ModeloConfigs` (por ejemplo `FechaActualizacion`) y un `.ps1` con `-DryRun`; la vuelta atrás tiene que devolver esa fila a su estado (decisión de este diseño: el ensayo prueba la mecánica de la pipeline; los pasos reales de cada versión se prueban con su propio `-DryRun` en el entorno destino, que la pipeline ejecuta siempre antes del real).

### Registro de lo aplicado

- Tabla nueva `dbo.ReleaseAplicada` (`Version`, `Paso`, `HashContenido`, `FechaUtc`, `BuildId`, `Resultado`), creada por migración EF (va con el 807, así existe en los tres entornos antes de la primera release por pipeline). La pipeline registra cada paso del manifiesto al aplicarlo y una fila de cierre por versión. Al arrancar, los pasos ya registrados con el mismo hash y `Resultado = Aplicado` se saltan. Motivo: repetir un run (por un fallo corregido) no debe volver a aplicar lo aplicado, y la idempotencia interna de cada script deja de ser la única red.
- `Guard` trata la tabla inexistente como «nada aplicado»: en la primera release por pipeline de PRE y PRO la tabla llega con la stage `Migraciones` de ese mismo run, así que `Guard` no puede exigirla. El registro es por entorno (cada entorno tiene su BD).
- `VueltaAtras` marca las filas del `BuildId` del run como `Resultado = Revertido` y no escribe la fila de cierre; `Guard` ignora las filas `Revertido`, de modo que el reintento vuelve a aplicar los pasos (motivo: tras restaurar las tablas, un paso registrado como aplicado ya no lo está).

### Orden de las stages

Es el orden decidido por el usuario para la v1.2.0, sin cambios:

1. `Guard`: versión, rama, manifiesto, entorno aún no aplicado, prerrequisitos del agente (`pwsh` 7, `az`, acceso a SQL y a los storages con la service connection).
2. `Foto` (deployment job, aprobación): mantenimiento (ver abajo), drenado de Durable con tope de tiempo, foto de las capas que la pipeline va a tocar (ver vuelta atrás), evidencias previas (endpoints de IA y nombres de app settings, como el paso 1 de la guía).
3. `Migraciones`: templates del 807 (Generate y Apply). Sin migraciones pendientes, Apply informa «Pendientes: 0» y sigue.
4. `IA`: templates del 832 (Export y Promote) sin altas.
5. `Codigo`: templates del 799 (Build, Deploy de Functions, Admin y AssetResolver, settings decisivos, borrado de heredados, ValidateConfiguration).
6. `Configuracion`: pasos del manifiesto en orden (para la v1.2.0: `aplicar-luna.ps1`, copia del artefacto v5, 07), cada `.ps1` con su `-DryRun` primero; secretos heredados de Key Vault (`remove-legacy-keyvault-secrets.ps1 -WhatIf` y real) y ciclo de vida (`set-lifecycle-documents-clasif.ps1`), que hoy son los pasos 6 y 7 de la guía y pasan a ser fijos de la pipeline porque son idempotentes; reinicio de la Function App y health.
7. `Altas`: templates del 832 (ConfigSeed y ConfigApply) con `applyMissingConfig=true`; reinicio y health.
8. `Verificacion`: export `<versión>-<env>` y diff frente al origen (puerta 4 anticipada), smoke E2E (`run-e2e-postdeploy.ps1 -Environment <env> -Profile smoke`; el criterio es 6/6 en los tres entornos, DEV incluido, y se confirma en el ensayo: los «FAIL esperados» de DEV son del perfil completo, no del smoke) y publicación del run en el Test Plan 100069 (`publish-smk-run.ps1`, `-WhatIf` y real). La function key la lee la pipeline en el momento con `az functionapp keys list` y la escribe en un `environments.json` temporal del agente que se borra al acabar: ningún secreto almacenado.
9. `Fin`: rehabilitar la ingesta, registro de cierre de la versión, resumen.
10. `VueltaAtras`: solo si falló cualquiera de las stages 3 a 8 (`condition: failed()` sobre ellas); al terminar deja el run en rojo.

### Mantenimiento y drenado (usuario: opción sin cambio de producto)

- La pipeline pone `AzureWebJobs.IngestDocument.Disabled=true` en la Function App (setting estándar del host: la función deja de exponerse y responde 404; las orquestaciones en vuelo terminan), espera a que la tabla de instancias del task hub no tenga `Running` ni `Pending` (consulta con la clave de la cuenta, tope por defecto 30 minutos) y, si no drena a tiempo o no puede leer la tabla, falla sin haber tocado nada más y vuelve a habilitar la ingesta. El setting se quita en `Fin` y en `VueltaAtras`. `IngestDocument` (`Triggers/IngestAPITrigger.cs`) es la única entrada de documentos: el Batch y el agente la llaman por HTTP; health, consultas de markdown, tipologías publicadas, API de administración y el timer de limpieza de blobs siguen activos.
- Dado que el sistema se cierra durante la release, esto es una red de seguridad; no sustituye el aviso a quien alimenta la ingesta, que sigue en el runbook.

### Vuelta atrás automática (usuario)

Por capas, de menos a más invasiva, y solo las que ya se habían tocado:

- **Configuración en BD.** En `Foto`, copia de las tablas de configuración (`ModeloConfigs`, `Tipologias`, `PromptTemplates`, `CatalogoTdn1`, `CatalogoTdn2`; la lista la fija el manifiesto, con esos valores por defecto) a `<Tabla>__rel_<BuildId>`. La vuelta atrás restaura cada tabla desde su copia con `MERGE` por clave primaria (actualiza lo cambiado, reinserta lo borrado, borra lo insertado). Cubre los scripts del manifiesto, el 07 y las altas del 832, que hoy no eran reversibles. Las copias se conservan (son pequeñas) y se nombran en el resumen.
- **App settings.** Foto de las claves que la release toca (`PromptDefaults__ModelKey`, `GDC__BypassSslValidation`, `AI__Resources__*__Endpoint`, el `Disabled` de la ingesta), no del listado completo (lleva cadenas de conexión). La vuelta atrás las repone con `appsettings set`. Los settings heredados borrados no se reponen: su retirada es una decisión cerrada (AB#101450) y el código no los lee.
- **Código.** En `Foto`, la pipeline descarga el paquete que está desplegado en cada una de las tres apps (Functions, Admin, AssetResolver) con la API de Kudu (`GET /api/zip/site/wwwroot/`, con las credenciales de publicación que la service connection puede leer) y lo publica como parte del artefacto del run (código compilado, sin secretos). La vuelta atrás despliega esos tres zips con el mismo template de despliegue. Motivo: no depende de la retención de artefactos de runs anteriores ni de que exista un run previo. Si Kudu no responde desde el pool privado (se comprueba en el ensayo), el plan de reserva es el último run en verde del 799 o de esta pipeline sobre ese entorno, localizado por la API de Azure DevOps con `System.AccessToken`; sin foto y sin run anterior, `Guard` falla con mensaje y no se abre la ventana (el operador no decide si seguir sin vuelta atrás).
- **Deployments de IA y artefacto de embeddings.** No se deshacen: son aditivos e inertes sin filas que los usen (las filas vuelven con la capa de BD). Los analyzers de CU copiados por el 832 sobrescriben los del destino y no tienen vuelta atrás automática; se anota en el resumen y sigue el procedimiento manual (capa 3 del Anexo B), como hoy.
- **Migraciones EF.** No se deshacen (capa 4, manual, como hoy); la v1.2.0 no trae migraciones salvo la de `ReleaseAplicada`.
- Siempre: reinicio, health, rehabilitar la ingesta y resumen con lo deshecho y lo que no.

### Templates y pipelines existentes (usuario: no eliminar nada)

- Carpeta `pipelines/templates/` con un template de stage o de job por bloque: build de la solución, despliegue de Functions, Admin y AssetResolver, settings decisivos y borrado de heredados, ValidateConfiguration, Generate y Apply de migraciones, Export, Promote, ConfigSeed y ConfigApply. Cada template recibe el entorno, la service connection y, opcionalmente, el environment (vacío = job normal).
- `azure-pipelines.yml`, `azure-pipelines-ai-artifacts.yml` y `azure-pipelines-migrations.yml` pasan a componer esos templates manteniendo parámetros, nombres de stage, artefactos y comportamiento. Siguen lanzándose como hoy (a mano o para una capa suelta). El ensayo en DEV de cada uno antes y después de la refactorización es parte del trabajo.
- La guía `GUIA_OPERADOR_PRE.md` se conserva como camino de respaldo si la pipeline no está disponible, con una nota al principio.

### Permisos de la service connection

A pedir a Plataforma antes del hito 2, por entorno: borrar secretos en `srbkv<env>docai` (Key Vault Secrets Officer o equivalente), `Storage Blob Data Reader` en el storage de origen y `Storage Blob Data Contributor` en el de destino (copia del artefacto de embeddings), lectura de la clave de la cuenta del task hub (`listKeys`, incluido en Contributor; si la service connection no es Contributor sobre esa cuenta, `Storage Table Data Reader` sobre ella), lectura de claves y reinicio de la Function App y credenciales de publicación de Kudu (los tiene si despliega), y permiso de escritura en Test Plans para la identidad del build (`System.AccessToken`, Project Collection Build Service). `Guard` comprueba cada permiso con una lectura y falla con un mensaje claro si falta alguno. El nombre de la cuenta del task hub de PRO se toma de `docs/infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md` (el constructor lo verifica).

### Ensayo y entrada en servicio

- Toda la pipeline se ensaya en DEV antes de usarse en PRE: una ejecución completa con `targetEnvironment=dev` sobre el estado actual (ya aplicado; debe acabar en verde sin cambios y con smoke 6/6), y una ejecución con un parámetro oculto `simularFalloEn=<stage>` que fuerza el fallo y comprueba que la vuelta atrás deja DEV como estaba (comparación del export antes y después). Ese parámetro solo se admite en `dev`.
- La v1.2.0 se despliega en PRE con la pipeline (usuario); `releases/v1.2.0/release.json` lleva los tres pasos de la guía actual. Las puertas 2 y 3 y el Go siguen la guía.

## Hitos

1. **Convención y templates.** `releases/` con `CURRENT` y `v1.2.0/release.json`; migración EF de `ReleaseAplicada`; `pipelines/templates/`; refactorización de 807, 832 y 799 sobre los templates, ensayada en DEV; petición de permisos del SPN. Sin pipeline de release todavía.
2. **Pipeline `Release`.** Guard, Foto con mantenimiento y drenado, composición de stages, Configuracion por manifiesto, Verificacion con smoke y Test Plan, Fin y VueltaAtras; ensayo completo en DEV con y sin fallo simulado.
3. **v1.2.0 a PRE con la pipeline** y cierre documental: `RELEASE_MANAGEMENT.md` (la Fase 2 pasa a ser la pipeline; la guía queda como respaldo), `08_CHECKLISTS_DESPLIEGUE.md`, `docs/releases/README.md`, runbook de la v1.2.0, ADR si el usuario lo aprueba (la decisión es de arquitectura de despliegue y difícil de revertir).

## Lo que queda al constructor

- Confirmar en el ensayo que con `IngestDocument` deshabilitada el health y el resto de triggers siguen activos y que la API de Kudu responde desde el pool privado.
- Diseño del `MERGE` de restauración y comprobación de que las tablas de configuración no tienen claves foráneas que lo impidan; si alguna lo impide, se anota y se excluye con motivo.
- Esquema definitivo de `release.json` y validación en `Guard`.
- Localización y descarga del «último run en verde» por API, y qué pasa si no hay ninguno (fallar en `Guard` con mensaje).
- Reparto exacto entre templates de stage y de job, y cómo se pasa el environment opcional.
- Tiempos: tope del drenado, timeouts de stage, reintentos del smoke.

## Riesgos y lo que queda abierto

- Refactorizar tres pipelines que funcionan: se mitiga con el ensayo en DEV antes y después, y con conservar nombres de artefactos y parámetros.
- Permisos del SPN: dependen de Plataforma; sin ellos el hito 2 no se puede ensayar entero.
- La vuelta atrás del código depende de que exista un run anterior en verde con los artefactos todavía retenidos (política de retención de Azure DevOps): el constructor comprueba la retención y, si hace falta, se fija una retención mayor para esta pipeline y el 799.
- Los analyzers de CU no se deshacen automáticamente (igual que hoy).
- El drenado con clave de la cuenta lee un secreto en el agente; no se publica ni se registra.
- Coste de la ventana: el smoke consume IA en el entorno destino; la golden y el coste siguen fuera.
