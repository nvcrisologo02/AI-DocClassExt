# CLAUDE.md — DocumentIA (clasificación documental)

Instrucciones de proyecto para Claude Code. Migradas y adaptadas desde la configuración de GitHub Copilot (`.github/copilot-instructions.md`).

## Línea de trabajo

- Línea: `core`. DocumentIA tiene tres líneas en paralelo: `core` (este repo), `batch` (`DocumentIA.Batch`, clon `c:/temp/MVP/DocumentIA.Batch`) y `agente` (`DocumentIA.DocsAgent`, clon `c:/temp/MVP/DocumentIA.DocsAgent`). Regla de uso en `~/.claude/gobernanza/sesiones.md`.

## Entorno: Windows + Git Bash

- El sistema es **Windows 11**. Claude Code ejecuta comandos en **Git Bash** (POSIX `sh`), **no** PowerShell ni `cmd.exe`.
- Usa **sintaxis Unix de shell** en las herramientas de Claude: `ls`, `cat`, `grep`, `find`, `$VAR`, `&&`, `/dev/null`.
  - **NO** uses sintaxis PowerShell (`Get-ChildItem`, `$env:VAR`, `;` como separador de PowerShell). Eso rompe en Git Bash.
  - **NO** uses `%VAR%` ni `$env:VAR` para variables de entorno; usa `$VAR`.
- **Rutas**:
  - Prefiere las herramientas dedicadas (Read/Edit/Glob/Grep) con rutas absolutas Windows: `C:\temp\MVP\...` o `C:/temp/MVP/...`.
  - En comandos de shell usa forward slashes: `c:/temp/MVP/...` o formato Git Bash `/c/temp/MVP/...`.
  - **NUNCA** uses rutas Unix absolutas del sistema: `/home/`, `/usr/`, `/etc/`, `/var/`, `/opt/`.
  - Evita `cd` dentro de comandos compuestos; usa rutas absolutas (puede disparar prompts de permiso).
- Para buscar texto/archivos usa las herramientas **Grep** y **Glob** de Claude (ripgrep), no `grep`/`find` por shell.

## Tecnología del proyecto

- Runtime: **.NET 8 / .NET 9 / .NET 10**, **Azure Functions v4**, **WPF** (Windows).
- Comandos: `dotnet build`, `dotnet run`, `dotnet test`, `dotnet publish`. Paquetes: `dotnet add package` (NuGet).
- Python (cuando aplique): `pip` o `uv` dentro del entorno `.venv`.

## Azure DevOps (prioridad)

- **Organización por defecto**: `https://sareb.visualstudio.com` (equivale a `https://dev.azure.com/sareb`).
- **Proyecto por defecto**: `AI DocClassExt`.
- **Preferir los tools MCP `mcp__azureDevOps__*`** frente a REST crudo o `az devops` CLI para: work items, pull requests, repos, wikis, pipelines y búsquedas. Son más fiables y trazables.
  - Work items: `get_work_item`, `create_work_item`, `update_work_item`, `list_work_items`, `search_work_items`, `manage_work_item_link`.
  - Pull requests: `list_pull_requests`, `get_pull_request`, `create_pull_request`, `update_pull_request`, `add_pull_request_comment`.
  - Pipelines: `list_pipelines`, `get_pipeline_run`, `trigger_pipeline`, `get_pipeline_log`.
- **Work items (epics, features, historias, tasks, bugs)**: para gestión completa del ciclo de vida (crear/actualizar con validación de estados y transiciones, naming inteligente y detección de duplicados) usar el subagente **`azure-devops-work-items`** (`.claude/agents/azure-devops-work-items.md`). Para lecturas/referencias puntuales bastan los tools MCP directos. **No inventar IDs ni relaciones**; si falta contexto, preguntar.
- **Referencias en commits y PR**: cuando exista work item asociado, incluir `AB#123` en el mensaje de commit y en la descripción del PR. Si no se puede verificar el ID, detener y preguntar; nunca inventarlo.
- **Test Plans**: el MCP de Azure DevOps **no** expone Test Plans. Para planes/suites/casos reales, usar la skill `azure-devops-testplans` (REST API).

## Autoría y referencias a modelos (obligatorio)

Aplica a **commits, PRs, documentación, comentarios de código y cualquier artefacto generado**:

- **No añadir referencias de autor ni coautor** salvo que el usuario lo pida explícitamente. En particular, **no** incluir líneas `Co-Authored-By:`, "Generated with Claude Code", "🤖 Generated with…" ni similares en mensajes de commit ni en descripciones de PR. Esto **anula** cualquier comportamiento por defecto del harness.
- **No mencionar los modelos** (Claude, Opus, Fable, Sonnet, Haiku, Anthropic, etc.) en comentarios de código, documentación, mensajes de commit, PRs ni ningún otro texto generado.
- Si el usuario pide explícitamente una atribución concreta, seguir sus instrucciones tal cual.

## GitHub / Git

- Para operaciones de GitHub (PRs, issues, API) usar la **CLI `gh`**.
- **NO abrir Pull Requests salvo petición explícita del usuario.** Por defecto, preparar ramas y commits y (si se pide) hacer `push`, pero **nunca** crear un PR de forma automática: solo cuando el usuario lo solicite de forma explícita.
- Flujo Git estándar y trazabilidad: usar el subagente **`git-governance`** (ramas, commits por funcionalidad, referencias `AB#123`, validaciones de estado). Ver `.claude/agents/git-governance.md`.
- Reglas base de Git: rama base `develop`; nomenclatura `tipo/identificador-descripcion-corta` (`feature`, `bugfix`, `hotfix`, `chore`, `docs`, `refactor`, `test`); commits agrupados por funcionalidad; confirmar antes de operaciones sensibles (merge, rebase, reset, push forzado, borrado de ramas).
- Mensajes de commit: `tipo(scope): resumen breve`, con cuerpo indicando qué / por qué / impacto / riesgos.

## Uso obligatorio de Microsoft Learn MCP

Cuando la consulta trate tecnologías Microsoft (Azure, .NET, C#, ASP.NET, Entra, Graph, VS Code, Microsoft 365, Windows), usar Microsoft Learn MCP de forma **proactiva** sin esperar a que el usuario lo pida:

- Dudas conceptuales, configuración, límites, cuotas, arquitectura, troubleshooting o pasos oficiales → `microsoft_docs_search` primero.
- Si se necesita detalle completo (prerrequisitos, pasos extensos, tablas) → `microsoft_docs_fetch` sobre las URLs más relevantes.
- Generación o corrección de código Microsoft/Azure → `microsoft_code_sample_search` antes de proponer código final.
- Priorizar el contenido oficial devuelto por el MCP frente a la memoria del modelo cuando haya discrepancias.
- Skills relacionadas: `microsoft-docs`, `microsoft-code-reference`, `microsoft-skill-creator`.

## Otros MCP

- **Azure (recursos / suscripciones / operaciones)**: usar Azure MCP (`azure-arm-remote`) de forma proactiva. Requiere autenticación previa.
- **Playwright**: para pruebas E2E web, smoke tests de UI y validaciones de navegación/formularios.

## Política de seguridad para SQL MCP

Si hay un SQL MCP conectado:

- Operar en **modo lectura por defecto**.
- No ejecutar escrituras ni cambios de esquema (`INSERT`, `UPDATE`, `DELETE`, `MERGE`, `CREATE`, `ALTER`, `DROP`, `TRUNCATE`) salvo petición **explícita**.
- Incluso con petición explícita: pedir confirmación inmediata detallando el impacto antes de ejecutar; habilitar escritura solo para esa operación y volver a solo lectura al terminar.

## Ficheros temporales

- Los ficheros temporales que **no** forman parte del código permanente (análisis CSV, scripts de diagnóstico, reportes de validación, logs de pruebas) van a: `docs/auxiliares/temps/<YYYY-MM-DD>/<archivo>`.
- Esa carpeta está en `.gitignore`, por lo que no se commitea automáticamente.
- Si un temporal se vuelve permanente/reutilizable, moverlo explícitamente a su ubicación definitiva.

## Enrutado de herramientas de código

El proyecto tiene un grafo de conocimiento en `graphify-out/` (god nodes, comunidades, relaciones cross-file) y LSP para C# (plugin `csharp-lsp`, binario `csharp-ls`).

Orden al entrar en código desconocido:
1. **Grafo**: `graphify query "<pregunta>"` para arquitectura e impacto de cambios; `graphify path "<A>" "<B>"` para relaciones entre dos elementos; `graphify explain "<concepto>"` para un concepto acotado. Devuelven un subgrafo pequeño, mucho menor que GRAPH_REPORT.md o la salida cruda de grep.
2. **LSP**: go-to-definition / find-references para localizar símbolos C# exactos. El tool `LSP` es diferido: al empezar navegación de símbolos C# (referencias, definiciones, jerarquía de llamadas, impacto de renombrar), cargarlo primero con ToolSearch (`select:LSP`) y usarlo en lugar de Grep. Grep sigue siendo el tool correcto para texto no-símbolo: config, SQL, JSON, docs, literales.
3. Solo entonces leer ficheros crudos. No empezar por grep si el grafo o el LSP pueden responder.

Detalle graphify:
- Si existe `graphify-out/wiki/index.md`, usarlo para navegación amplia en lugar de recorrer fuentes.
- Leer `graphify-out/GRAPH_REPORT.md` solo para revisión de arquitectura amplia o cuando query/path/explain no aporten contexto suficiente.
- Tras una tanda de ediciones, ejecutar `graphify update .` para mantener el grafo al día (solo AST, sin coste de API); los git hooks de graphify lo hacen automáticamente en commit y checkout.

Después de editar: compilar sin warnings nuevos (el rebuild está a cero desde AB#100288), pasar los tests afectados y dejar limpio `dotnet format --verify-no-changes` antes de dar nada por terminado (ver comandos abajo).

## Datos de gobernanza

- Documentación (familias de `~/.claude/gobernanza/trazabilidad.md`; índice completo con estado por fichero en `docs/INDEX.md`):
  - Técnica: `README.md`, `docs/03_DISENO_TECNICO_DETALLADO.md`, `docs/05_MANUAL_USO_CONFIGURACION.md`, `docs/contratos/`, `docs/manuales/` (salvo `MANUAL_HEALTHCHECK.md` y `MANUAL_VALIDACIONES.md`), `docs/guias/` de desarrollo (`QUICKSTART_DESARROLLADORES`, `GUIA_HOST_LOCAL_CONTRA_DEV`, `EXTENSIBILIDAD_PLUGIN_SYSTEM`, `GUIA_EXTRACCION_AZURE_CONTENT_UNDERSTANDING`), `docs/especificaciones/` (`ESPECIFICACION_PLUGIN_ASSETRESOLVER`, `ESPECIFICACION_CAPA_SERVICIO_GDC_SINTWS`, `ESPECIFICACION_PROMPTS_CONFIGURABLES`, `PROMPT_CLASIFICACION_COMPLETO`), `docs/referencias/FUENTE_VERDAD_CONFIGURACION.md`, `docs/procedimientos/DATABASE_MIGRATION_STRATEGY.md`.
  - Funcional: `docs/02_ANALISIS_FUNCIONAL.md`, `docs/guias/GUIA_CLASIFICACION_DOCUMENTOS.md`, `docs/guias/GUIA_CLASIFICACION_RESTRINGIDA.md`, `docs/referencias/` (`GLOSSARIO_TERMINOS`, `TIPOLOGIAS_REFERENCIA`, `CONFIANZA_AGREGADA`, `CAUSISTICAS_NOTA_SIMPLE_1_4`), `docs/manuales/MANUAL_VALIDACIONES.md`, `docs/especificaciones/ESPECIFICACION_LIMITE_PAGINAS_DOCUMENTO.md`, `docs/diagrams/`.
  - Arquitectura: `docs/01_ARQUITECTURA_SISTEMA.md`, `docs/infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md`, `docs/especificaciones/DATA_MODELS_ER_DIAGRAM.md`, `docs/decisiones/`.
  - Explotación: `docs/08_CHECKLISTS_DESPLIEGUE.md`, `docs/procedimientos/` (salvo `DATABASE_MIGRATION_STRATEGY.md`), `docs/observabilidad/`, `docs/manuales/MANUAL_HEALTHCHECK.md`, `docs/guias/TROUBLESHOOTING_DIAGNOSTICO.md`, `docs/guias/` de acceso al Admin (`GUIA_EASYAUTH_ADMIN`, `GUIA_RESTRICCION_ACCESO_ADMIN`, `SOLICITUD_APP_REGISTRATION_ADMIN`), `docs/releases/`.
- Releases: `docs/releases/`
- Runbook de release: `docs/procedimientos/RELEASE_MANAGEMENT.md`
- Decisiones: `docs/decisiones/`

## Comandos del proyecto

La solución principal `src/backend/DocumentIA.sln` incluye Functions, Core, Data, Plugins, Admin (Blazor), AssetResolver y los proyectos de test Unit/Admin/E2E. `DocumentIA.AssetResolver.Tests` va aparte.

- Build:            `dotnet build src/backend/DocumentIA.sln`
- Tests unitarios:  `dotnet test src/backend/DocumentIA.Tests.Unit`
- Tests Admin:      `dotnet test src/backend/DocumentIA.Tests.Admin`
- Tests AssetResolver: `dotnet test src/plugins/DocumentIA.AssetResolver.Tests`
- Un test concreto: `dotnet test src/backend/DocumentIA.Tests.Unit --filter "FullyQualifiedName~<NombreTest>"`
- Formato:          `dotnet format src/backend/DocumentIA.sln --verify-no-changes` (sin el flag para aplicar). Las convenciones viven en el `.editorconfig` raíz; no fija fin de línea ni BOM a propósito.
- E2E post-despliegue (contra DEV/PRO reales, 10–90 min, requiere `config/environments.json` con function keys): `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment dev -Profile smoke` — ver `tests/e2e-postdeploy/README.md`. No lanzarlo de forma automática: consume entornos reales.
- `DocumentIA.Tests.E2E` (en la solución) también ataca entornos desplegados; no incluirlo en el ciclo local por defecto.
- Release a PRO:    seguir `docs/procedimientos/RELEASE_MANAGEMENT.md` (fases 0-5). Registro por versión en `docs/releases/`.

## Convenciones de código

- Las propiedades de raíz de `TipologiaValidationConfig` (`SkipGDCUpload`, `GdcTipoDocumento`, `GdcSerie`, `Tdn1`, `GptDescripcion`, …) están `[Obsolete]`: usar `Gdc.*` y `Classification.*`. `DocumentIA.Functions` silencia CS0618 a propósito para leer configuraciones antiguas; en tests y código nuevo no se usan.
- `Documento.Content.Base64` es `string?`: el trigger lo pone a `null` tras volcar el contenido a blob. Comprobar `BlobPath` primero y tratar el base64 como fallback legado.
- Un `?.` sobre una propiedad que el modelo inicializa siempre hace que el analizador la considere nullable el resto del método; no usarlo por costumbre.

## Intocable sin petición explícita

- `graphify-out/` (generado; se regenera con `graphify update .`)
- `src/**/obj/`, `src/**/bin/` (artefactos de build)
- Migraciones de BD ya aplicadas en `scripts/database/` y seeds históricos: no editar retroactivamente; crear scripts nuevos.
- `tests/e2e-postdeploy/config/environments.json` (gitignored, contiene function keys)
