# Runbook ejecutado — release vX.Y.Z

Instancia de docs/procedimientos/RELEASE_MANAGEMENT.md. Marcar cada paso con fecha y resultado; tachar con motivo los que no aplican.

## Fase 0 — Preparación y versión
- [ ] **0.1** Fijar el commit candidato: `git fetch origin && git log --oneline -1 origin/develop`. Anotar el hash. — <fecha> · <resultado>
- [ ] **0.2** Decidir la versión SemVer (regla en `docs/releases/README.md`) y el tag anterior (`git describe --tags --abbrev=0 origin/master` o el último `vX.Y.Z`; para la primera release, `deploy-pro-2026-09-20`). — <fecha> · <resultado>
- [ ] **0.3** Crear `docs/releases/vX.Y.Z/` copiando `docs/releases/_plantilla/` y rellenar la cabecera de `release.md`. — <fecha> · <resultado>
- [ ] **0.4** Listar los cambios: `git log <tag-anterior>..<commit> --oneline` y clasificar por PBIs, features, fixes, infraestructura y configuración en `release.md` (los `AB#` salen de los mensajes). — <fecha> · <resultado>
- [ ] **0.5** Identificar las migraciones nuevas: `ls src/backend/DocumentIA.Data/Migrations | tail` frente a la última aplicada en PRO (`SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC`). Anotar en `release.md`. — <fecha> · <resultado>
- [ ] **0.6** Identificar cambios de configuración (ModeloConfigs, PromptTemplates, Tipologias, CatalogoTdn1, CatalogoTdn2, PluginTipologiaConfigs) y de artefactos de IA (`infra/ai/deployments.<env>.json`, `infra/ai/cu-analyzers.json`, `infra/ai/di-artifacts.json`). Anotar en `release.md`. — <fecha> · <resultado>
## Fase 1 — DEV
- [ ] **1.1** Build y tests en local sobre el commit candidato: — <fecha> · <resultado>
- [ ] **1.2** E2E en DEV: `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment dev -Profile smoke` y después `-Profile full -Parallel 2`. Requiere `tests/e2e-postdeploy/config/environments.json` (gitignored). FAIL esperados y vigentes están en `tests/e2e-postdeploy/README.md`. — <fecha> · <resultado>
- [ ] **1.3** Export de configuración de referencia desde DEV: — <fecha> · <resultado>
## Fase 2 — PRE, puerta técnica
- [ ] **2.1.1** Pipeline 807 Migrations-BD con `targetEnvironment=pre` desde el commit candidato. El stage Generate publica `migrations.sql`; el stage Apply corre en el pool privado `docia-mdp-private` y su pre-check compara el conjunto de migraciones aplicadas con el del repo (no solo la última). — <fecha> · <resultado>
- [ ] **2.1.2** Comprobar en el run que el artefacto `MigrationsScript` contiene exactamente las migraciones de 0.5 y que `__EFMigrationsHistory` de PRE las registra. — <fecha> · <resultado>
- [ ] **2.2.1** Pipeline 799 con `targetEnvironment=pre` desde el commit candidato. Stages esperados: Build → DeployFunctions (jobs Functions y Admin) → DeployAssetResolver → ValidateConfiguration. `RunMigrations` aparece como omitido: está deshabilitado a propósito. — <fecha> · <resultado>
- [ ] **2.2.2** `ValidateConfiguration` en verde (contrato de app settings de `scripts/config/azure-appsettings-contract.json` verificado por `scripts/testing/validate-azure-appsettings-contract.ps1`). — <fecha> · <resultado>
- [ ] **2.2.3** Anotar el ID del run y el hash desplegado en `runbook.md`. — <fecha> · <resultado>
- [ ] **2.3.1** Revisar `artifacts/db-config/config-vX.Y.Z.sql` (generado en 1.3): solo tablas de configuración, sin keys en claro (`grep -i 'apikey' config-vX.Y.Z.sql` debe devolver vacío). — <fecha> · <resultado>
- [ ] **2.3.2** Aplicar en PRE con token de Entra: — <fecha> · <resultado>
- [ ] **2.3.3** Segunda pasada del mismo comando: debe informar 0 filas cambiadas (idempotencia). — <fecha> · <resultado>
- [ ] **2.4.1** Deployments: `pwsh ./scripts/ai/apply-deployments.ps1 -Environment pre` (crea los que faltan; los existentes que difieren se informan como `differs` y no se tocan). — <fecha> · <resultado>
- [ ] **2.4.2** Analyzers CU: `pwsh ./scripts/ai/copy-cu-analyzers.ps1 -Environment pre` (Copy API desde PRO; salta los idénticos). — <fecha> · <resultado>
- [ ] **2.4.3** Clasificadores DI: `pwsh ./scripts/ai/copy-di-artifacts.ps1 -Environment pre`. — <fecha> · <resultado>
- [ ] **2.4.4** Validación: `pwsh ./scripts/ai/validate-analyzer.ps1 -Environment pre` (markdown idéntico y acuerdo de campos ≥ 0,85 frente a PRO). — <fecha> · <resultado>
- [ ] **2.4.5** Alias y modo de acceso, solo si cambian: `scripts/ai/set-resource-aliases.sql` y `scripts/ai/set-auth-mode-identity.sql` contra `srbsqlpredocai` con token de Entra; segunda pasada 0 filas. Después `scripts/ai/clear-model-api-keys.sql` (0 filas con key al terminar). — <fecha> · <resultado>
- [ ] **2.4.6** Reiniciar `srbapppredocai` si 2.4.5 cambió filas: `az functionapp restart -n srbapppredocai -g SRBRGPREDOCSAI`. — <fecha> · <resultado>
- [ ] **2.4.7** Purgar keys de las copias `ModeloConfigs__bak_*` creadas en 2.4.5 (mismo patrón que en DEV el 2026-09-22). — <fecha> · <resultado>
- [ ] **2.5.1** Puerta 1 (smoke) pasada; resumen del run y salida de la KQL en `evidencias/`. — <fecha> · <resultado>
- [ ] **2.5.2** Puerta 2 (golden) pasada; `compare.md` en `evidencias/`. — <fecha> · <resultado>
- [ ] **2.5.3** Puerta 4 (deriva) pasada; salida de `config-hash.sql` en `evidencias/`. — <fecha> · <resultado>
- [ ] **2.5.4** Puerta 3 (coste) pasada al día siguiente; cuadre en `evidencias/`. — <fecha> · <resultado>
## Fase 3 — Aprobación
- [ ] **3.1** Smoke pre-release PRO: ejecutar el Test Plan 100069 (casos SMK-1 a SMK-8) contra PRE con el commit candidato y registrar el ID del run de Test Plans y el resultado en `release.md`. El MCP de Azure DevOps no expone Test Plans; usar la REST API (skill `azure-devops-testplans`). — <fecha> · <resultado>
- [ ] **3.2** Revisión del diff de 0.4 por quien aprueba: cada `AB#` listado está en estado To Validate o superior en Azure DevOps. — <fecha> · <resultado>
- [ ] **3.3** Puerta 3 (coste) cerrada (2.5.4). — <fecha> · <resultado>
- [ ] **3.4** "Go" escrito en el bloque de aprobación de `release.md`: fecha, nombre y las cuatro puertas con fecha de PASS. — <fecha> · <resultado>
## Fase 4 — PRO
- [ ] **4.1** Copia de seguridad: `az sql db copy --name DocumentIA --dest-name DocumentIA-prerel-<yyyyMMdd> --server srbsqlprodocai --resource-group SRBRGDOCSAIPROD` y verificación contra el origen: número de filas de `Documentos` y `DocumentoEjecuciones` y última fila de `__EFMigrationsHistory` iguales en las dos BD. Anotar en `runbook.md`. — <fecha> · <resultado>
- [ ] **4.2** Pipeline 807 Migrations-BD con `targetEnvironment=prod` desde el commit candidato. Comprobar en `MigrationsScript` que el conjunto aplicado es el de 0.5. — <fecha> · <resultado>
- [ ] **4.3** Configuración: aplicar en PRO el mismo `config-vX.Y.Z.sql` de 2.3 (`replicate-config-data.ps1 -Mode Apply` contra `srbsqlprodocai.database.windows.net`); segunda pasada 0 filas. Si la release trae seeds propios (catálogos, tarifas), van aquí, con su copia `__bak`. — <fecha> · <resultado>
- [ ] **4.4** Pipeline 799 con `targetEnvironment=prod` desde el commit candidato; `ValidateConfiguration` en verde. Anotar ID del run. — <fecha> · <resultado>
- [ ] **4.5** Reiniciar `srbappprodocai` solo si la release cambió alias o app settings de IA: `az functionapp restart -n srbappprodocai -g SRBRGDOCSAIPROD`. — <fecha> · <resultado>
- [ ] **4.6** Smoke en PRO: `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile smoke` → 6/6. — <fecha> · <resultado>
- [ ] **4.7** Deriva en PRO: `config-hash.sql` contra `srbsqlprodocai` frente a `config-vX.Y.Z.hashes.json`. Toda diferencia se anota en `runbook.md` como cambio en caliente a retroportar a DEV. — <fecha> · <resultado>
- [ ] **4.8** Backfills reanudables, solo si la release los trae y solo después de 4.6 (ejemplos: `scripts/database/backfill-costes-estimados.ps1`, `scripts/database/backfill-markdown-cobertura.ps1`). — <fecha> · <resultado>
- [ ] **4.9** Observación de una hora con las KQL del Anexo D: sin subida de fallos ni de `CU.CircuitOpen`. — <fecha> · <resultado>
## Fase 5 — Cierre y registro
- [ ] **5.1** Tag anotado sobre el commit desplegado: `git tag -a vX.Y.Z <commit> -m "Release vX.Y.Z a PRO el <fecha>"` y `git push origin vX.Y.Z`. — <fecha> · <resultado>
- [ ] **5.2** `release.md` completo: validación (build, tests), diff, aprobación, runs de pipeline. `runbook.md` con todas las casillas marcadas con fecha y resultado, y las no aplicables tachadas con motivo. — <fecha> · <resultado>
- [ ] **5.3** Sincronizar `master` con el commit desplegado (merge fast-forward desde `develop`; confirmar antes con el usuario del repo) y `git push origin master`. — <fecha> · <resultado>
- [ ] **5.4** Work items de la release a Done en Azure DevOps con un comentario que enlaza `docs/releases/vX.Y.Z/release.md`. — <fecha> · <resultado>
- [ ] **5.5** Fila nueva en la tabla de `docs/releases/README.md`. — <fecha> · <resultado>
- [ ] **5.6** Limpiezas diferidas, con fecha prevista escrita en `runbook.md`: borrar `DocumentIA-prerel-<fecha>` tras el periodo de validación (7 días salvo indicación), borrar las tablas `ModeloConfigs__bak_*` de 2.4.5 y 4.3 una vez purgadas sus keys. — <fecha> · <resultado>
- [ ] **5.7** Commit de `docs/releases/vX.Y.Z/` en `develop`: `docs(release): registro de la release vX.Y.Z (AB#<PBI principal>)`. — <fecha> · <resultado>

## Limpiezas diferidas
- [ ] Copia DocumentIA-prerel-<fecha> borrada el <fecha prevista>
- [ ] Tablas ModeloConfigs__bak_* borradas el <fecha prevista>
