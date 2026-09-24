# ADR-001: Promoción de artefactos de IA en el sentido DEV → PRE → PRO
Fecha: 2026-09-24 · Estado: aceptada · Work Item: AB#100675 · Session ID: sin Session ID (el proyecto no declara `Gobernanza: activa`)

## Contexto y problema

La Epic AB#100298 da a DEV y PRE sus propios recursos de IA. Los artefactos que no se pueden desplegar desde una definición versionada (analyzers de Content Understanding, clasificadores de Document Intelligence y sus datasets de etiquetado) se copian entre recursos con las Copy API.

El diseño inicial (plan de la Epic, Tarea 18, y solicitud del service connection del 2026-09-22) tomaba PRO como origen único: los proyectos de etiquetado y el entrenamiento estaban en PRO y las copias iban de PRO a PRE y a DEV. La etapa `AiArtifacts` del pipeline excluía `prod`.

Esa copia era la carga inicial y ya está hecha en DEV y PRE. Queda por decidir el sentido de las promociones posteriores, antes de que otro equipo cree el service connection `AI DocClassExt Promocion IA`, porque de esa decisión dependen sus roles.

## Opciones consideradas

- A. Mantener PRO como origen: se etiqueta y entrena en PRO y se copia a PRE y DEV.
- B. DEV → PRE → PRO: se etiqueta y entrena en DEV, se promociona a PRE, se valida con las puertas de PRE y se promociona a PRO.

## Decisión

Se adopta B. Los artefactos de IA se crean en DEV y llegan a PRO solo a través de PRE, por el pipeline `azure-pipelines-ai-artifacts.yml`, con las puertas de PRE en verde y la aprobación del environment `prod` de ADO.

## Motivos

- Es el mismo sentido que siguen el código y la configuración en BD. Con A, PRO recibiría modelos nuevos sin haber pasado por PRE, que es la puerta técnica del runbook de release.
- Etiquetar y entrenar en PRO obliga a trabajar en producción con artefactos todavía sin validar.
- El coste en permisos es bajo. Cognitive Services User, que la Copy API ya exige en origen y destino, tiene DataActions `Microsoft.CognitiveServices/*` y permite escribir en PRO (https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/ai-machine-learning#cognitive-services-user). Solo cambian los roles de almacenamiento de PRO.

## Consecuencias

- Service connection `AI DocClassExt Promocion IA`: incluye el environment `prod` (id 34), y las cuentas `srbstgprodocai` y `srbstgproapppdocai` pasan de Storage Blob Data Reader a Storage Blob Data Contributor. Siguen siendo 15 asignaciones.
- Una identidad no productiva puede crear, sobrescribir y borrar analyzers y clasificadores en tres cuentas de IA de PRO y escribir en dos cuentas de almacenamiento. Lo acotan:
  - el ámbito, limitado a esos cinco recursos, sin plano de control;
  - la autorización del service connection para un único pipeline;
  - la aprobación del environment `prod`;
  - los scripts de copia, que no sustituyen un artefacto con otra definición sin `-Force` y verifican el destino frente al origen.
- `copy-cu-analyzers.ps1`, `copy-di-artifacts.ps1` y `copy-labeling-dataset.ps1` hoy solo copian de PRO a `dev|pre`. Pasan a recibir origen y destino, con los saltos `dev→pre` y `pre→prod`, y `prod→dev|pre` solo con un modificador explícito para recuperar el estado inicial (AB#100675).
- En `prod`, la etapa `AiArtifacts` solo copia artefactos; `apply-deployments.ps1` sigue fuera de PRO.
- Se revisan cuando esté la parametrización: runbook de release (2.4.2), `infra/ai/README.md` y la spec `docs/superpowers/specs/2026-09-15-ia-propia-por-entorno-design.md`. La spec sigue siendo válida en que PRO no cambia de recursos; lo que cambia es que pasa a recibir artefactos por pipeline.
- El etiquetado y el entrenamiento se hacen en los recursos de DEV (Foundry y almacenamiento de DEV).
- A ADO le faltan hoy approval checks en los environments. Hay que configurar el de `prod` antes de la primera ejecución contra PRO.

## Alternativas descartadas y por qué

- A (PRO como origen): mantiene la escritura sobre PRO fuera del pipeline, pero hace que los cambios de IA lleguen a PRO sin pasar por PRE y deja el etiquetado en producción. Solo tenía sentido para la carga inicial.
- Usar dos service connections, uno por salto o uno por entorno: la Copy API de Content Understanding exige la misma identidad en origen y destino.
- Ampliar el service connection de despliegue de PRE con derechos sobre PRO: mezclaría la identidad de despliegue con escritura de datos en producción.
