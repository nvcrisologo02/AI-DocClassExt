# ADR-007: Pipeline de release de un botón por step templates con vuelta atrás automática
Fecha: 2026-11-26 · Estado: aceptada · Work Item: AB#101570 (release v1.2.0, origen) y PBI AB#101581 (Tasks AB#101582, AB#101583 y AB#101584 por hito) · Session ID: sin Session ID (proyecto sin `Gobernanza: activa`; traspaso en `docs/auxiliares/temps/2026-11-26/`)

## Contexto y problema

La Fase 2 de una release a PRE o PRO son hoy 21 pasos manuales (`docs/releases/v1.2.0/GUIA_OPERADOR_PRE.md`) que ejecuta un operador ajeno a DocumentIA: tres pipelines lanzados en orden, ocho scripts con ensayo y ejecución real, consultas, reinicios y una pausa de la ingesta acordada fuera del sistema. El riesgo principal de la release está en que ese operador se equivoque de orden, de parámetro o de entorno.

Hechos verificados el 2026-11-26 que condicionan la decisión (spec `docs/superpowers/specs/2026-11-26-pipeline-release-un-boton-design.md`):

- Los pipelines 799 (`azure-pipelines.yml`), 832 (`azure-pipelines-ai-artifacts.yml`) y 807 (`azure-pipelines-migrations.yml`) no usan templates, `extends` ni `resources`; cada uno lleva sus pasos en línea.
- Los scripts que ejecuta el operador se autentican con la sesión `az` activa, que en un pipeline es la de la service connection; ninguno tiene credencial propia. Con la identidad permanente del operador, sin PIM, fallan varias operaciones sobre la Function App que la service connection sí puede hacer.
- No existe registro de los scripts de configuración aplicados: el rastro son copias `<Tabla>__bak_<sufijo>` y la idempotencia interna de cada script.
- La ingesta no tiene interruptor en el producto: la pausa la hace quien envía documentos.

Hay que decidir cómo ejecutar la Fase 2 de forma repetible por un operador que no toma decisiones y cómo recuperar el entorno si algo falla.

## Opciones consideradas

- A. Pipeline orquestadora que lanza 807, 832 y 799 por la API REST y espera a cada uno.
- B. Pipeline única `Release` que compone step templates extraídos de 807, 832 y 799.
- C. Copiar los pasos de los tres pipelines dentro del YAML nuevo.

## Decisión

Se adopta B. Los pasos de 807, 832 y 799 se extraen a step templates (`pipelines/templates/steps/`, con las variables compartidas en `pipelines/templates/variables/entorno.yml`; 11 templates, hito 1, hecho en la rama de AB#101570) y una pipeline única `Release` (hito 2) los compone en el orden de la guía. Los tres pipelines siguen existiendo y la guía manual queda como respaldo.

- El operador elige solo el entorno. La versión sale de `releases/CURRENT` y lo que cambia por release está en `releases/<versión>/release.json` (pasos `.sql` y `.ps1` con `entornos`, bloque `embeddings` y un paso con `plantilla: embeddings` que genera el script 07 con los valores del bloque). Para ensayar en DEV, `Guard` usa `releases/_ensayo/release.json`.
- Lo aplicado se registra en `dbo.ReleaseAplicada` (migración EF `20261126190743_ReleaseAplicada`, que viaja con el 807). `HashContenido` es el hash del contenido efectivo ejecutado, no del fichero de origen. Un reintento salta lo ya aplicado; los pasos revertidos no cuentan como aplicados.
- Mantenimiento: la pipeline pone `AzureWebJobs.IngestDocument.Disabled=true` y espera a que el task hub no tenga orquestaciones en vuelo antes de tocar nada; lo quita al terminar y en la vuelta atrás.
- Vuelta atrás automática por capas, solo de lo ya tocado: configuración en BD por copias `<Tabla>__rel_<BuildId>` y `MERGE` por clave primaria; app settings que la release toca, repuestos desde su foto; código por el paquete desplegado en cada app (descargado con Kudu) o, si Kudu no responde, el último run en verde. Los deployments y analyzers de IA y las migraciones EF no se deshacen (siguen siendo manuales).
- Una aprobación por run (la stage de foto es el único deployment job sobre el environment). Las puertas 2 (golden) y 3 (coste) y el Go quedan fuera, en manos del responsable del producto. No hay avisos automáticos: Azure DevOps notifica a quien lanzó el run.

## Motivos

- Un run, una aprobación y los logs juntos; el operador no decide nada ni lee logs para decidir.
- Los templates evitan dos copias divergentes de los mismos pasos y dejan 807, 832 y 799 funcionando como hoy.
- El registro en BD hace inocuo repetir el run y sustituye a la idempotencia interna de cada script como única red.
- La vuelta atrás cubre la configuración en BD, que hasta ahora no era reversible (scripts del manifiesto, script 07 y altas del 832).
- La pipeline es fija y los datos de cada release van en una carpeta por versión: una release sin scripts nuevos no obliga a tocar el YAML.

## Consecuencias

- Positivas: un solo run por release y entorno, idempotencia con rastro en BD, secuencia idéntica en PRE y PRO, vuelta atrás automática de configuración, app settings y código.
- Negativas y riesgos:
  - Hay que refactorizar tres YAML que funcionan. Se mitiga comparando el YAML expandido antes y después y ensayando cada pipeline en DEV.
  - Los permisos de la service connection (Key Vault, storages, claves de la Function App, Kudu, Test Plans) dependen de Plataforma; sin ellos el hito 2 no se ensaya entero (`docs/procedimientos/PERMISOS_SPN_RELEASE.md`).
  - Los analyzers de Content Understanding y las migraciones EF no tienen vuelta atrás automática; el resumen del run lo dice y se sigue el procedimiento manual.
  - `releases/_ensayo/` es de un solo uso en `Guard` (un paso `.sql` que modifica una fila): queda por resolver en el hito 2 cómo repetir el ensayo.
  - El drenado lee la clave de la cuenta del task hub en el agente; no se publica ni se registra.
- Quedan para el hito 3 la entrada en servicio con la v1.2.0 en PRE y el cierre documental (`RELEASE_MANAGEMENT.md`, `08_CHECKLISTS_DESPLIEGUE.md`).

## Relación con otras decisiones

Complementa ADR-001 (promoción de artefactos de IA, cuyos templates reutiliza) y ADR-005 (altas de configuración por clave natural, que la pipeline invoca con `applyMissingConfig`). No sustituye a ninguna.

## Alternativas descartadas y por qué

- A: deja cuatro o cinco runs encadenados con aprobaciones en runs distintos, esperas frágiles y una vuelta atrás del código que tendría que relanzar el 799 por REST. El operador seguiría viendo varios runs.
- C: dos copias de los mismos pasos que divergen en la primera release.

---

> Nota posterior (2026-11-27): el punto «una aprobación por run» de la Decisión, y el deployment job de `Foto` que lo sostiene, los sustituye el [ADR-008](ADR-008-aprobaciones-al-principio-pipeline-release.md) (dos aprobaciones secuenciales al principio con ManualValidation). El resto de esta decisión sigue vigente.
