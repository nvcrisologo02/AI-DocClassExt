# Release v1.0.0

Fecha: pendiente (ventana por fijar) · Tag: v1.0.0 · Commit candidato: `17889bb` (`origin/develop`, 2026-10-07, **definitivo**) · Tag anterior: `deploy-pro-2026-09-20` (`fe1533f`) · Entorno: PRO

Primera versión con el esquema de `docs/releases/README.md`. Paso 0.1 del runbook: commit
candidato fijado el 2026-10-07 en `17889bb`, el merge `--no-ff` de AB#100814 en `develop`
(138 commits por delante de `fe1533f`). Historia del paso: el registro se abrió sobre `291a424`
a la espera de AB#100880; el 2026-10-07 a mediodía se fijó `5414335` (merge de ese Bug) como
candidato provisional y por la tarde se decidió que AB#100814 (OutOfMemory con PDF grandes)
también entraba, lo que reabrió el paso hasta integrar su fix. El árbol de `17889bb` es el de
`b083546`, la punta de la rama `bugfix/100814-oom-pdf-grandes` (17 commits sobre `5414335`,
que ya estaba en `develop` como `7aca20b`).

## Validación

Build: `dotnet build src/backend/DocumentIA.sln` → correcto, 0 advertencias · Tests unitarios: 1430/1430 · Tests Admin: 151/151 · Tests AssetResolver: 14/14 · Formato: ok
E2E DEV: smoke 6/6, full N/A · E2E PRE: smoke 6/6 · E2E PRO: smoke pendiente

Build, los tres juegos de tests y formato son del 2026-10-07 sobre el árbol de `17889bb` (ejecutados
sobre `b083546`, cuyo árbol es idéntico al del merge). Smoke DEV del 2026-10-07 sobre `bb1e81d`
(pipeline 802 run 79674; artefactos `tests/e2e-postdeploy/artifacts/20261007-170046-dev-smoke`):
los tres commits que lo separan del candidato tocan solo el YAML del pipeline y el script de ciclo
de vida, no el código de Functions. Smoke PRE del 2026-10-07 sobre `417dca4` (pipeline 802 run
79679, que solo añade este documento y el runbook sobre `17889bb`; artefactos
`tests/e2e-postdeploy/artifacts/20261007-201229-pre-smoke`). Medición de AB#100814 en PRE tras
ese smoke: ver la entrada del Bug.

## Contenido de la release

### Migraciones y scripts fuera de EF

- `20261005140618_IndiceDocumentosMD5` (AB#100863, AB#100864) · **ya aplicada en PRO** el
  2026-10-07 a las 07:18 UTC con `scripts/database/indice-documentos-md5-pro.sql` (`ONLINE = ON`,
  23,9 s, 16,2 MB, sin carga) y registrada en `__EFMigrationsHistory` (AB#100866). Es la única
  migración nueva frente a PRO, así que el pipeline 807 contra `prod` debe salir con
  `Pendientes: 0`; si sale con 1, parar y revisar antes de aplicar.
- `scripts/migrations/clasificador-embeddings/01-modeloconfig-clasificador-embeddings.sql`
  (AB#100779) con `@Modo='off'` · **antes de desplegar el código**: el orquestador llama a la
  activity de embeddings en todas las ejecuciones y, si la fila no existe, el proveedor deriva con
  `fila_ausente` y emite evento y métrica en cada ejecución. Con la fila en `off` no hay llamada al
  endpoint ni telemetría. Hace copia `ModeloConfigs__bak_<fecha>` y es idempotente.
- `scripts/migrations/clasificador-embeddings/02-tarifas-embeddings-deployments.sql` (AB#100779) ·
  líneas `text-embedding-3-large-030358` y `-010650` en el catálogo `tarifas.ia`. Puede ir antes o
  después del código.
- `scripts/migrations/costes-ia/02-tarifas-cera16v2-gpt41-cached.sql` (AB#100860) · `CERA16_v2`
  (0,859) y `gpt-4.1-cached` (0,429) en `tarifas.ia`. El `01` ya está aplicado en PRO.
- `scripts/ai/set-resource-aliases.sql` (AB#100321) · **después de desplegar el código**, ver
  "Orden crítico" más abajo. Con backup automático.
- `scripts/database/backfill-fecha-expiracion-blob.ps1` (AB#100881) · **no es de la ventana**: se
  ejecuta en D+1, después de ver el primer ciclo del cron limpio.

### Configuración

- `ModeloConfigs` · `clasificador-embeddings/01` · alta de la fila Tipo=5 `clasificador.embeddings`
  con `Modo = off` y `Restringido.Modo = off`.
- `ModeloConfigs` (catálogo `tarifas.ia`, Tipo=4) · `clasificador-embeddings/02` y `costes-ia/02` ·
  cuatro líneas de tarifa nuevas (dos deployments de embeddings, `CERA16_v2`, `gpt-4.1-cached`).
- `ModeloConfigs` · `scripts/ai/set-resource-aliases.sql` · las filas de PRO pasan de `Endpoint`
  explícito a `ResourceAlias` (`openai_primary`, `cu_primary`, `cu_secondary`, `di`).
- App settings de `srbappprodocai`, por el pipeline 799 (`ensure-app-settings.ps1`, que no pisa
  claves existentes): `AI__Resources__openai_primary__Endpoint`,
  `AI__Resources__cu_primary__Endpoint`, `AI__Resources__cu_secondary__Endpoint`,
  `AI__Resources__di__Endpoint`.
- App settings de `srbappprodocai`, a mano (AB#100868): `BlobRetentionCleanupCron = 0 0 3 * * *`,
  `BlobRetention__DefaultDays = 2`, `BlobRetention__BatchSize = 2000`. `appsettings.json` no se
  carga en Functions, así que sin `DefaultDays` ningún documento recibe `FechaExpiracionBlob` y el
  cron no borra nada.
- `PromptTemplates`, `Tipologias`, `CatalogoTdn1`, `CatalogoTdn2`, `PluginTipologiaConfigs`:
  ninguno.

### Artefactos de IA

Ninguno. Esta release no promociona analyzers de Content Understanding, clasificadores de Document
Intelligence ni datasets a PRO: el primer run real del pipeline 832 es AB#100809 y los manifiestos
`.prod.` no existen hasta esa primera copia (AB#100812). `infra/ai/deployments.prod.json`,
`cu-analyzers.json` y `di-artifacts.json` no cambian de contenido aplicado en PRO.

## Orden crítico de la ventana

1. **Configuración antes que código, con una excepción.** `clasificador-embeddings/01` va antes del
   despliegue (razón arriba). `set-resource-aliases.sql` va **después**, porque en cuanto vacía
   `Endpoint` y pone el alias los loaders dependen de los app settings `AI__Resources__*`, y esos
   los crea `ensure-app-settings.ps1` dentro del despliegue de código. `AiEndpointResolver.Resolve`
   da precedencia al endpoint explícito y lanza `InvalidOperationException` si hay alias sin app
   setting mapeado: aplicar el script antes del despliegue dejaría PRO sin clasificación ni
   extracción.
2. **Comprobar los cuatro `AI__Resources__*` con valor** entre el despliegue y el script de alias.
   No basta con que la clave exista: un valor vacío cuenta como no mapeado.
3. **El índice MD5 ya está aplicado**, así que el 807 contra `prod` debe dar `Pendientes: 0`.
4. **Los app settings del cron van después del despliegue y antes del smoke**: el `set` reinicia el
   host, así que sustituye al reinicio del paso 4.5 y deja el smoke como prueba del arranque limpio.

## Diff frente a `deploy-pro-2026-09-20`

### Epic

- AB#100298 `[ENTORNOS] IA propia por entorno (DEV/PRE/PRO) y promoción de artefactos de IA` ·
  cierra cuando Plataforma complete el Step 3 de AB#100321 (retirada de roles cruzados)

### Features

- AB#100302 `[FASE 3] PRO por datos y App Settings; cierre del acceso compartido` · entra en esta
  release
- AB#100301 `[FASE 2] PRE como puerta técnica de release` · el cutover de PRE se hizo el 2026-09-23;
  sigue abierta por AB#100808 y AB#100810, que no son de esta release

### PBIs

- AB#100224 `[COSTES] Control de costes de servicios de IA por ejecución`
- AB#100235 `[COSTES] Sección de costes en Admin y relleno retroactivo`
- AB#100303 `Resolvedor de endpoint de IA por alias de recurso en Core`
- AB#100304 `ResourceAlias y resolución de endpoint en los cuatro registros de modelos`
- AB#100305 `Registro en Functions, seeds models.json y documentación de alias`
- AB#100306 `Variables AI_* por entorno en azure-pipelines.yml y App Settings de alias`
- AB#100307 `Definición de IA en infra/ai y export de analyzers CU`
- AB#100308 `Export de configuración por release y hash de deriva`
- AB#100309 `Spike: Copy API de Content Understanding con flujo de token`
- AB#100310 `Inventario pendiente: analyzers de Foundry 01 DEV/PRE, extractor DI_NS_1.4, dataset de DI Studio, roles cruzados`
- AB#100312 `Copia versionada del dataset de etiquetado CU al storage de DEV con manifiesto`
- AB#100313 `Deployments declarativos por entorno (apply-deployments.ps1)`
- AB#100314 `Reconstrucción de analyzers CU en DEV desde definición y dataset del entorno`
- AB#100315 `Validación post-build de analyzers: acuerdo de campos origen vs destino`
- AB#100316 `Copia de clasificadores y modelos custom de DI a DEV por Copy API`
- AB#100317 `Cutover de DEV: keys en Key Vault, App Settings de alias, set-resource-aliases.sql y vuelta atrás`
- AB#100320 `Ejecución y puertas de PRE (fase 2a): cutover manual con los scripts de DEV y 4 puertas`
- AB#100321 `PRO: alias en ModeloConfigs y App Settings, comprobación de deriva, retirada de roles de DEV y PRE sobre PRO, documentación final` · **abierto, Steps 1 y 2 en esta ventana**
- AB#100664 `Promoción de analyzers CU a DEV por Copy API`
- AB#100675 `Pipeline azure-pipelines-ai-artifacts.yml (fase 2b)`
- AB#100676 `Runbook de release: PRE como puerta técnica`
- AB#100779 `Clasificador híbrido embeddings (A) + GPT en el orquestador` · Fase A en sombra y
  minors de Fase B (AB#100861); en PRO entra en `off`
- AB#100878 `Alinear documentación de API y de infraestructura con el código desplegado`

### Fixes

- AB#100863 `Timeouts SQL de 30 s en VerificarDuplicadoPorMD5Activity: Documentos.MD5 sin índice
  sobre BD S0` · índice ya aplicado en PRO; queda confirmar en Query Store que la consulta por MD5
  baja de 64.702 lecturas a menos de 10 con la primera ingesta de GDC
- AB#100879 `IngestDocument devuelve ex.Message al cliente en el 500 genérico` · merge en develop
  el 2026-10-07
- AB#100880 `SIN_CONTENIDO_DOCUMENTO se devuelve cuando el documento sí tiene contenido` · merge
  en develop el 2026-10-07 (`5414335`); validado en DEV el mismo día: un PDF sin texto cierra en
  `SIN_CONTENIDO_DOCUMENTO` con `CausaSinContenido` "Layout respondió sin texto" (instancia
  `d7c965834da445218885dd341b61fcdf`); el caso de layout fallido queda cubierto por los 46 tests
  unitarios nuevos (reproducirlo en DEV exige un endpoint erróneo en `ModeloConfigs`, SQL en
  `docs/auxiliares/temps/2026-10-07/ab100880-dev-endpoint-erroneo.sql`)
- AB#100814 `OutOfMemory en el orquestador de PRO con PDF grandes en lotes de Batch` · entra en
  la release por decisión del 2026-10-07; **hecho**, mergeado en develop el 2026-10-07
  (`17889bb`, Bug en To Validate). Fix: las activities trabajan con bytes, el recorte viaja por
  los mensajes de Durable como ruta de blob (`documents-clasif/`, 7 días de vida) y no como
  base64, el trigger deserializa el JSON en streaming y el cuerpo inline hacia Document
  Intelligence se escribe en streaming; el tope del heap del GC (`DOTNET_GCHeapHardLimitPercent=28`)
  lo fija el pipeline 802 en los tres entornos. Medido en DEV con un PDF de 57 MB sobre `bb1e81d`
  (run 79674): antes del fix, un solo PDF tiraba el worker (working set 2.546 MiB, instancia
  `2783059919314ed28c7a48476160b2e7`); con el fix, cero OutOfMemory y Document Intelligence
  operativo sin salvavidas (instancias `7204b3d484b44ecbbf1046d131b37427` con recorte y
  `a6e439a7d00c450aa6ff3e3811b6d443` sin recorte), working set 455 MiB con un PDF y 1.931 MiB
  con dos seguidos (criterio: < 2 GiB). Smoke DEV 6/6 sobre ese despliegue
  (`tests/e2e-postdeploy/artifacts/20261007-170046-dev-smoke`); el run 79675 desplegó después
  `2ae103b` (solo YAML del pipeline). Informe en
  `docs/auxiliares/temps/2026-10-07/ab100814-medicion/informe-medicion.md`. Repetido en PRE el
  2026-10-07 sobre `417dca4` (run 79679, `DOTNET_GCHeapHardLimitPercent=28` validado por el
  pipeline): dos ingestas seguidas del mismo PDF (instancias `d86e7121b495436ca805535ab8214ca2`
  y `a513b516750242a8881cfdd9dbc5d85a`), cero OutOfMemory, DI operativo sin salvavidas (2
  analyze del clasificador y 2 de layout en 202, polls en 200), working set 2.043 MiB en el pico
  (cumple el < 2 GiB por 5 MiB; el host sube a 1.066 MiB y el worker retiene 1.298 tras el
  documento completo inline). La memoria que el host de Functions retiene al reenviar cuerpos
  HTTP grandes (~0,9 GiB por ingesta de 76 MB) queda fuera: PBI AB#100899 (ClassificationLite
  por `blobPath`). Las instancias en vuelo en el momento del despliegue reciben el base64
  legado una vez y terminan sin pérdida
- AB#100662 `Admin /costes muestra 0 con 90 días` · el índice ya está en PRO desde el 2026-09-20;
  esta release lleva el aviso del Admin cuando los agregados no llegan
- AB#100258 `Las ejecuciones reutilizadas por duplicado no dejan rastro`
- AB#100020 pre-check de migraciones por conjunto, no solo la última
- AB#100247 / AB#100240 backfills de cobertura y costes reanudables por `-DesdeId`
- AB#100235 `test-costes-ia` resolvía la raíz del repo dos niveles por encima
- AB#100881 script por lotes del backfill de `FechaExpiracionBlob` (herramienta de D+1)

### Infraestructura

- AB#100675 pipeline 832 `azure-pipelines-ai-artifacts.yml` con etapas Guard, Export, AiArtifacts y
  ConfigSeed, parámetro `dryRun` y aprobaciones de `pre` y `prod`; ADR-001 fija el sentido
  DEV → PRE → PRO
- AB#100815 alertas de memoria de PRO por instancia (2,25 GiB) y alerta de OutOfMemory
  (`srbalertoomprodocai`) · **ya aplicadas en Azure**, el repo solo versiona la definición
- AB#100307 / AB#100312 a AB#100316 definición versionada de recursos, deployments, analyzers CU,
  clasificadores DI y datasets en `infra/ai/`
- AB#100306 / AB#100317 plantilla compartida de endpoints de IA y variables `AI_*` por entorno en
  los pipelines de Functions y Admin
- Correcciones de pipeline del 03/09 incorporadas a develop (nombres de app resueltos en tiempo de
  compilación, endpoint GDC de PRE)

### Configuración

Ver "Contenido de la release". Resumen: `ModeloConfigs` con la fila de embeddings en `off`, cuatro
líneas nuevas en `tarifas.ia`, el paso de `Endpoint` a `ResourceAlias` en PRO, los cuatro
`AI__Resources__*__Endpoint` y los tres app settings del cron de limpieza de blobs. Ninguna
migración pendiente. Secretos: ninguno nuevo.

Checklist de AB#100814 (fuera del despliegue de código):

| Elemento | DEV | PRE | PRO |
|---|---|---|---|
| Regla `documents-clasif-7d` de ciclo de vida en la cuenta de documentos (`scripts/storage/set-lifecycle-documents-clasif.ps1`; runbook 2.2.5 y 4.13) | aplicada el 2026-10-07 15:30Z en `srbstgdevdocai`, conserva `delete-temp` | aplicada el 2026-10-07 ~18:05Z en `srbstgpredocai` (salida: "Politica escrita. Reglas: delete-temp, documents-clasif-7d"; 2.2.5 hecho) | pendiente (`srbstgprodocai`, antes de 4.4) |
| App setting `DOTNET_GCHeapHardLimitPercent=28` (pipeline 802; runbook 4.14) | puesto a mano el 2026-10-07 14:18Z y en el pipeline desde `4ab4676` | puesto y validado por el run 79679 del 802 el 2026-10-07 18:11Z | lo pone el pipeline al desplegar; comprobar en 4.14 |
| Contenedor `documents-clasif` | lo crea el código al primer recorte | ídem | ídem |

## Aprobación

| Puerta | Fecha | Resultado | Evidencia |
|---|---|---|---|
| 1 Smoke E2E PRE | | | |
| 2 Golden | | | |
| 3 Coste | | | |
| 4 Deriva | | | |

Test Plan 100069 (SMK-1..8): run pendiente.
Go: pendiente

## Runs de pipeline

| Pipeline | Entorno | Run | Resultado |
|---|---|---|---|
| 802 Functions (validación previa de AB#100814, no sustituye al 799) | pre | 79679 (`417dca4`) | succeeded, 2026-10-07 18:11Z, app settings validados |
| 807 Migrations-BD | pre | | |
| 799 completo | pre | | |
| 807 Migrations-BD | prod | | |
| 799 completo | prod | | |

## Pendientes antes de cerrar la Fase 0

1. ~~AB#100880 desarrollado y mergeado en `develop`~~ · hecho el 2026-10-07 (`5414335`).
2. ~~Repetir el smoke de DEV y PRE sobre `5414335`~~ · hechos el 2026-10-07, 6/6 y 6/6 (runs
   79669 y 79670 del pipeline 802).
3. ~~Fijar el commit en 0.1 y reescribir la cabecera~~ · hecho el 2026-10-07, provisional.
3b. ~~AB#100814 desarrollado y mergeado en `develop`; fijar el commit definitivo en 0.1 y repetir
   build y tests sobre él~~ · hecho el 2026-10-07 (`17889bb`; build, tests y formato sobre su
   árbol). Smoke DEV 6/6 del mismo día sobre `bb1e81d` (mismo código de Functions).
3c. ~~Desplegar el candidato en PRE, validación de app settings con `DOTNET_GCHeapHardLimitPercent`,
   smoke PRE 6/6, medición del PDF de 57 MB contra PRE y regla de ciclo de vida en
   `srbstgpredocai` (2.2.5)~~ · hecho el 2026-10-07 (run 79679 sobre `417dca4`, smoke 6/6,
   medición en la entrada de AB#100814, 2.2.5 en `runbook.md`). El working set con dos PDF
   seguidos quedó en 2.043 MiB, a 5 MiB del criterio: vigilar en la hora de observación de PRO
   (4.14) y no cargar lotes de PDF grandes hasta AB#100899.
4. Bloque 1 del plan de ventana (lectura de los app settings de PRO): obligatorio, porque la
   ausencia de los `AI__Resources__*` se apoya en la comprobación del 05/10 y la cuenta de
   desarrollo dio `AuthorizationFailed` el 06/10.
5. Export de configuración de DEV y diff por clave natural (paso 3 del runbook).

## Fuera de esta release

AB#100899 (ClassificationLite sube el documento a blob y envía `documento.blobPath`: la memoria
del host de Functions con cuerpos HTTP grandes, pieza del lado cliente de AB#100814), AB#100272
(identificador buscable en el Monitor), AB#100805 y AB#100807 (rotar `docaisql`, limpiar copias y tablas `__bak`), AB#100808
(purga de `__bak` de PRE y `CatalogoTdn1.Descripcion`), AB#100809 / AB#100811 / AB#100812 (primer
run real del pipeline 832), AB#100810 (evidencia de las 4 puertas de PRE), AB#100780 / AB#100781 /
AB#100782 (crecimiento del clasificador, promoción del modelo, confusión TASA/CERJ), AB#99934 y
AB#99936 a AB#99940 (migración de deployments de modelos), AB#100220 (seguimiento post-release de
septiembre) y el resto del backlog de calidad de clasificación.
