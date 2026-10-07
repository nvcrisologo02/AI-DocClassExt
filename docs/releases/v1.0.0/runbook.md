# Runbook ejecutado — release vX.Y.Z

Instancia de docs/procedimientos/RELEASE_MANAGEMENT.md. Marcar cada paso con fecha y resultado; tachar con motivo los que no aplican.

## Fase 0 — Preparación y versión
- [ ] **0.1** Fijar el commit candidato: `git fetch origin && git log --oneline -1 origin/develop`. Anotar el hash. — <fecha> · <resultado>
- [ ] **0.2** Decidir la versión SemVer (regla en `docs/releases/README.md`) y el tag anterior (`git describe --tags --abbrev=0 origin/master` o el último `vX.Y.Z`; para la primera release, `deploy-pro-2026-09-20`). — <fecha> · <resultado>
- [ ] **0.3** Crear `docs/releases/vX.Y.Z/` copiando `docs/releases/_plantilla/` y rellenar la cabecera de `release.md`. — <fecha> · <resultado>
- [ ] **0.4** Listar los cambios: `git log <tag-anterior>..<commit> --oneline` y clasificar por PBIs, features, fixes, infraestructura y configuración en `release.md` (los `AB#` salen de los mensajes). — <fecha> · <resultado>
- [ ] **0.5** Identificar las migraciones nuevas frente a la última aplicada en PRO (consulta abajo) y los scripts de la release fuera de EF (índices `ONLINE`, procedimientos, seeds de configuración en `scripts/`). Anotar en `release.md`. — <fecha> · <resultado>
- [ ] **0.6** Identificar cambios de configuración (ModeloConfigs, PromptTemplates, Tipologias, CatalogoTdn1, CatalogoTdn2, PluginTipologiaConfigs) y de artefactos de IA (`infra/ai/deployments.<env>.json`, `infra/ai/cu-analyzers.json`, `infra/ai/di-artifacts.json`). Anotar en `release.md`. — <fecha> · <resultado>
## Fase 1 — DEV
- [ ] **1.1** Build, tests y formato en local sobre el commit candidato con los comandos del bloque siguiente; copiar los n/n a `release.md`. — <fecha> · <resultado>
- [ ] **1.2** E2E en DEV: `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment dev -Profile smoke` y después `-Profile full -Parallel 2`. Requiere `tests/e2e-postdeploy/config/environments.json` (gitignored). FAIL esperados y vigentes están en `tests/e2e-postdeploy/README.md`. — <fecha> · <resultado>
- [ ] **1.3** Export de configuración de referencia desde DEV con `export-config-release.ps1` (comando abajo); copiar el `.hashes.json` a `docs/releases/vX.Y.Z/evidencias/`. — <fecha> · <resultado>
## Fase 2 — PRE, puerta técnica
- [ ] **2.1.1** Pipeline 807 Migrations-BD con `targetEnvironment=pre` desde el commit candidato. El stage Generate publica `migrations.sql`; el stage Apply corre en el pool privado `docia-mdp-private` y su pre-check compara el conjunto de migraciones aplicadas con el del repo (no solo la última). — <fecha> · <resultado>
- [ ] **2.1.2** En el log del stage Apply, `Pendientes: N` lista exactamente las migraciones de 0.5 y el cierre es `Migrations aplicadas y verificadas correctamente` (`MigrationsScript` es idempotente y contiene todas las del repo, no solo las nuevas). — <fecha> · <resultado>
- [ ] **2.1.3** Scripts SQL de la release fuera de EF listados en 0.5, aplicados en PRE con token de Entra y medidos (duración por lote); antes de 2.1.1 si sustituyen a una migración EF (la registran en `__EFMigrationsHistory`). — <fecha> · <resultado>
- Nota previa a 2.2.1 (2026-10-07): el pipeline 802 (solo Functions) run 79679 desplegó `417dca4` en PRE para validar AB#100814 antes de la Fase 2: `Set CU Resiliencia App Settings` en verde con `DOTNET_GCHeapHardLimitPercent = 28`, smoke PRE 6/6 (`20261007-201229-pre-smoke`) y medición del PDF de 57 MB sin OutOfMemory. No sustituye al 799 de 2.2.1.
- [ ] **2.2.1** Pipeline 799 con `targetEnvironment=pre` desde el commit candidato. Stages esperados: Build → DeployFunctions (jobs Functions y Admin) → DeployAssetResolver → ValidateConfiguration. `RunMigrations` aparece como omitido: está deshabilitado a propósito. — <fecha> · <resultado>
- [ ] **2.2.2** `ValidateConfiguration` en verde (contrato de app settings de `scripts/config/azure-appsettings-contract.json` verificado por `scripts/testing/validate-azure-appsettings-contract.ps1`). — <fecha> · <resultado>
- [ ] **2.2.3** Anotar el ID del run y el hash desplegado en la tabla "Runs de pipeline" de `release.md`. — <fecha> · <resultado>
- [ ] **2.2.4** Antes de 2.2.1: guardar en `evidencias/` los endpoints de IA y los nombres de los app settings de `srbapppredocai` (comandos abajo); es de donde restaura el Anexo B, capas 2 y 3. — <fecha> · <resultado>
- [x] **2.2.5** Política de ciclo de vida de los recortes de clasificación en `srbstgpredocai`: `pwsh ./scripts/storage/set-lifecycle-documents-clasif.ps1 -Environment pre -WhatIf` y, revisada la salida, sin `-WhatIf`. Debe conservar las reglas existentes y añadir `documents-clasif-7d` (AB#100814). — 2026-10-07 ~18:05Z · `-WhatIf` conservaba `delete-temp` y añadía `documents-clasif-7d`; aplicada: "Politica escrita. Reglas: delete-temp, documents-clasif-7d"; releída con `-WhatIf` y las dos reglas están.
- [ ] **2.3.1** Revisar `artifacts/db-config/config-vX.Y.Z.sql` (generado en 1.3): solo tablas de configuración, sin keys en claro (`grep -i 'apikey' config-vX.Y.Z.sql` debe devolver vacío). Es referencia; no se aplica. — <fecha> · <resultado>
- [ ] **2.3.2** Export de PRE antes de tocar nada y diff por clave natural frente al de DEV (comandos abajo); toda diferencia que no resuelvan los seeds de la release se explica en `runbook.md` antes de seguir. — <fecha> · <resultado>
- [ ] **2.3.3** Aplicar en PRE los seeds propios de la release listados en 0.5, con token de Entra (ver "Acceso a SQL") y su copia `__bak`; nunca el export completo. Sin seeds en la release, tachar con motivo. — <fecha> · <resultado>
- [ ] **2.3.4** Export de PRE tras 2.3.3 (`-ReleaseTag vX.Y.Z-pre`) y el mismo diff de 2.3.2: solo quedan diferencias explicadas. Su `.hashes.json` va a `evidencias/`: es la referencia de la puerta 4. — <fecha> · <resultado>
- [ ] **2.4.1** Deployments: `pwsh ./scripts/ai/apply-deployments.ps1 -Environment pre` (crea los que faltan; los existentes que difieren se informan como `differs` y no se tocan). — <fecha> · <resultado>
- [ ] **2.4.2** Analyzers CU: `pwsh ./scripts/ai/copy-cu-analyzers.ps1 -Environment pre` (Copy API desde PRO; salta los idénticos). — <fecha> · <resultado>
- [ ] **2.4.3** Clasificadores DI: `pwsh ./scripts/ai/copy-di-artifacts.ps1 -Environment pre`. — <fecha> · <resultado>
- [ ] **2.4.4** Validación: `pwsh ./scripts/ai/validate-analyzer.ps1 -Environment pre` (markdown idéntico y acuerdo de campos ≥ 0,85 frente a PRO). — <fecha> · <resultado>
- [ ] **2.4.5** Alias y modo de acceso, solo si cambian: `scripts/ai/set-resource-aliases.sql` y `scripts/ai/set-auth-mode-identity.sql` contra `srbsqlpredocai` con token de Entra; segunda pasada 0 filas. Después `scripts/ai/clear-model-api-keys.sql` (0 filas con key al terminar). — <fecha> · <resultado>
- [ ] **2.4.6** Reiniciar `srbapppredocai` si 2.4.5 cambió filas: `az functionapp restart -n srbapppredocai -g SRBRGPREDOCSAI`. — <fecha> · <resultado>
- [ ] **2.4.7** Purgar keys de las copias `ModeloConfigs__bak_*` creadas en 2.4.5 (mismo patrón que en DEV el 2026-09-22). — <fecha> · <resultado>
- [ ] **2.4.8** Si 2.4.5 cambió filas, repetir el export de 2.3.4 (`-ReleaseTag vX.Y.Z-pre`, sobrescribe): la referencia de la puerta 4 debe incluir el `ModeloConfigs` final. — <fecha> · <resultado>
- [ ] **2.5.1** Puerta 1 (smoke) pasada; resumen del run y salida de la KQL en `evidencias/`. — <fecha> · <resultado>
- [ ] **2.5.2** Puerta 2 (golden) pasada; `compare.md` en `evidencias/`. — <fecha> · <resultado>
- [ ] **2.5.3** Puerta 4 (deriva) pasada; los dos `.hashes.json` (referencia de 2.3.4 o 2.4.8 y el de la puerta) en `evidencias/`. — <fecha> · <resultado>
- [ ] **2.5.4** Puerta 3 (coste) pasada al día siguiente; cuadre en `evidencias/`. — <fecha> · <resultado>
## Fase 3 — Aprobación
- [ ] **3.1** Smoke pre-release PRO: ejecutar el Test Plan 100069 (casos SMK-1 a SMK-8) contra PRE con el commit candidato y registrar el ID del run de Test Plans y el resultado en `release.md`. El MCP de Azure DevOps no expone Test Plans; usar la REST API (skill `azure-devops-testplans`). — <fecha> · <resultado>
- [ ] **3.2** Revisión del diff de 0.4 por quien aprueba: cada `AB#` listado está en Azure DevOps en To Validate o superior si es PBI o Bug, y en Done si es Task. — <fecha> · <resultado>
- [ ] **3.3** Puerta 3 (coste) cerrada (2.5.4). — <fecha> · <resultado>
- [ ] **3.4** "Go" escrito en el bloque de aprobación de `release.md`: fecha, nombre y las cuatro puertas con fecha de PASS. — <fecha> · <resultado>
## Fase 4 — PRO
- [ ] **4.1** Copia de seguridad: `az sql db copy --name DocumentIA --dest-name DocumentIA-prerel-<yyyyMMdd> --server srbsqlprodocai --resource-group SRBRGDOCSAIPROD` y verificación contra el origen con `query-sql.ps1`: número de filas de `Documentos` y `DocumentoEjecuciones` y última fila de `__EFMigrationsHistory` iguales en las dos BD. Anotar en `runbook.md`. — <fecha> · <resultado>
- [ ] **4.2** Pipeline 807 Migrations-BD con `targetEnvironment=prod` desde el commit candidato. Comprobar en el log del stage Apply que `Pendientes: N` lista las migraciones de 0.5 y que cierra con `Migrations aplicadas y verificadas correctamente`. — <fecha> · <resultado>
- [ ] **4.3** Configuración en PRO, igual que 2.3.2 a 2.3.4: export previo y diff por clave natural sin `ModeloConfigs`, seeds propios de la release con su copia `__bak` (nunca `config-vX.Y.Z.sql`) y export de referencia `vX.Y.Z-pro` (comandos abajo). — <fecha> · <resultado>
- [ ] **4.4** Pipeline 799 con `targetEnvironment=prod` desde el commit candidato; `ValidateConfiguration` en verde. Anotar ID del run. Pipelines por componente solo en el caso que admite el Anexo A. — <fecha> · <resultado>
- [ ] **4.5** Reiniciar `srbappprodocai` solo si la release cambió alias o app settings de IA: `az functionapp restart -n srbappprodocai -g SRBRGDOCSAIPROD`. — <fecha> · <resultado>
- [ ] **4.6** Smoke en PRO: `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile smoke` → 6/6. — <fecha> · <resultado>
- [ ] **4.7** Deriva en PRO: export tras 4.6 (`-ReleaseTag vX.Y.Z-pro-despues`) frente al `config-vX.Y.Z-pro.hashes.json` de 4.3; hash idéntico salvo `ModeloConfigs`, que queda fuera. Toda diferencia se anota en `runbook.md` como cambio en caliente a retroportar a DEV. — <fecha> · <resultado>
- [ ] **4.8** Backfills reanudables, solo si la release los trae y solo después de 4.6 (ejemplos: `scripts/database/backfill-costes-estimados.ps1`, `scripts/database/backfill-markdown-cobertura.ps1`). — <fecha> · <resultado>
- [ ] **4.9** Observación de una hora con las KQL del Anexo D: sin subida de fallos ni de `CU.CircuitOpen`. — <fecha> · <resultado>
- [ ] **4.10** Scripts SQL de la release fuera de EF listados en 0.5 (los de 2.1.3), aplicados en PRO con token de Entra; siempre antes de 4.4 (esquema antes que código) y antes de 4.2 si sustituyen a una migración EF. Anotar la duración por lote. — <fecha> · <resultado>
- [ ] **4.11** Antes de 4.4: guardar en `evidencias/` los endpoints de IA y los nombres de los app settings de `srbappprodocai`, como en 2.2.4 (comandos abajo). — <fecha> · <resultado>
- [ ] **4.13** Política de ciclo de vida de los recortes de clasificación en `srbstgprodocai`: `pwsh ./scripts/storage/set-lifecycle-documents-clasif.ps1 -Environment pro -WhatIf` y, revisada la salida, sin `-WhatIf`; antes de 4.4. Debe conservar las reglas existentes y añadir `documents-clasif-7d` (AB#100814). — <fecha> · <resultado>
- [ ] **4.14** Tras 4.4, comprobar en `srbappprodocai` que `DOTNET_GCHeapHardLimitPercent` vale `28` (lo fija el pipeline 802; AB#100814) y observar Private Bytes de host y worker en la hora de 4.9: ninguno debe superar ~1,4 GiB. — <fecha> · <resultado>
## Fase 5 — Cierre y registro
- [ ] **5.1** Tag anotado sobre el commit desplegado: `git tag -a vX.Y.Z <commit> -m "Release vX.Y.Z a PRO el <fecha>"` y `git push origin vX.Y.Z`. — <fecha> · <resultado>
- [ ] **5.2** `release.md` completo: validación (build, tests), diff, aprobación, runs de pipeline. `runbook.md` con todas las casillas marcadas con fecha y resultado, y las no aplicables tachadas con motivo. — <fecha> · <resultado>
- [ ] **5.3** Sincronizar `master` con un merge `--no-ff` del commit desplegado (no hay fast-forward: `master` lleva sus propios merges), confirmado antes con el responsable del repo, y `git push origin master`. — <fecha> · <resultado>
- [ ] **5.4** Work items de la release a Done en Azure DevOps con un comentario que enlaza `docs/releases/vX.Y.Z/release.md`. — <fecha> · <resultado>
- [ ] **5.5** Fila nueva en la tabla de `docs/releases/README.md`. — <fecha> · <resultado>
- [ ] **5.6** Limpiezas diferidas, con fecha prevista escrita en `runbook.md`: borrar `DocumentIA-prerel-<fecha>` tras el periodo de validación (7 días salvo indicación), borrar las tablas `ModeloConfigs__bak_*` de 2.4.5 y 4.3 una vez purgadas sus keys. — <fecha> · <resultado>
- [ ] **5.7** Commit de `docs/releases/vX.Y.Z/` en `develop`: `docs(release): registro de la release vX.Y.Z (AB#<PBI principal>)`. — <fecha> · <resultado>

## Limpiezas diferidas
- [ ] Copia DocumentIA-prerel-<fecha> borrada el <fecha prevista>
- [ ] Tablas ModeloConfigs__bak_* borradas el <fecha prevista>
