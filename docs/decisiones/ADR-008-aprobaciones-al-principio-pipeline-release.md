# ADR-008: Dos aprobaciones secuenciales al principio de la pipeline Release con ManualValidation
Fecha: 2026-11-27 · Estado: aceptada · Work Item: PBI AB#101581, Task AB#101583 (hito 2) · Session ID: sin Session ID (proyecto sin `Gobernanza: activa`; traspaso en `docs/auxiliares/temps/2026-11-27/`)

## Contexto y problema

El ADR-007 fija una aprobación por run: la stage `Foto` de la pipeline `Release` es el único deployment job sobre el environment del entorno, y su check pide la aprobación. Al diseñar el hito 2, el responsable del producto pide otra cosa: dejar el run lanzado en espera, que el aprobador solo tenga que pulsar «aprobar», con dos aprobaciones seguidas, y que después la pipeline corra sin más paradas.

Hechos verificados el 2026-11-27:

- Los environments `pre` y `prod` tienen un check de tipo Approval cada uno (64 y 65; timeout 43200 minutos, sin orden de aprobadores); `dev` no tiene ninguno.
- Azure DevOps evalúa los checks de un environment cada vez que arranca una stage que lo usa, y no admite dar por aprobada una stage posterior. Para que nada pare después de las aprobaciones, las stages siguientes no pueden usar el environment.
- `ManualValidation@1` solo corre en un job agentless, admite `approvers` y `notifyUsers` (correos o grupos), `allowApproversToApproveTheirOwnRuns` y `onTimeout` (`reject` o `resume`), y espera como máximo 30 días. Un job agentless no ocupa agente mientras espera.

## Opciones consideradas

- A. Dos `ManualValidation@1` en jobs agentless secuenciales, con los aprobadores en el repositorio, y ninguna stage de la Release sobre los environments.
- B. Una stage inicial que sea la única sobre el environment, con su check configurado con dos aprobadores en secuencia.
- C. A y además el check del environment al principio.

## Decisión

Se adopta A. Después de `Guard`, que solo lee, una stage `Aprobacion` con dos jobs agentless (`Aprobacion2` depende de `Aprobacion1`) con `ManualValidation@1`. Los aprobadores salen de `pipelines/templates/variables/aprobadores.yml` (`aprobador1` y `aprobador2`; al principio los dos son el responsable del producto). `Foto` pasa a ser un job normal, y ninguna stage de la Release usa los environments `pre` ni `prod`, así que los checks 64 y 65 no se piden en ella. Sustituye el punto «una aprobación por run» del ADR-007; el resto del ADR-007 sigue vigente.

## Motivos

- El run se deja lanzado y, una vez aprobado, corre solo hasta el final: es lo que pide el responsable del producto.
- `Guard` va antes porque solo lee: el aprobador aprueba un run ya validado, y un manifiesto mal escrito falla sin pedir nada a nadie.
- Cambiar aprobadores es un commit en un único fichero, sin escrituras de configuración en Azure DevOps.
- El 799 y el 832 lanzados sueltos no cambian: siguen pidiendo los checks 64 y 65.

## Consecuencias

- La Release no aparece en el historial de despliegues de los environments; lo cubre el artefacto `release-<env>-<build>`.
- Los aprobadores viven en el repositorio: quien pueda integrar en `develop` o `master` puede cambiarlos. La protección son las políticas de rama, que se comprueban en el hito 2.
- `Guard` exige los dos aprobadores no vacíos: con `approvers` vacío, cualquiera con permiso de encolar podría aprobar.
- Con quien lanza y quien aprueba siendo la misma persona, `allowApproversToApproveTheirOwnRuns` va a `true`; se revisa al cambiar los aprobadores.
- Rechazo o tiempo agotado (`onTimeout: reject`; tope propuesto de 7 días) dejan el run en rojo sin haber tocado nada, y `VueltaAtras` no se dispara.
- El run despliega el commit con el que se lanzó, no el que haya en la rama al aprobar; las instrucciones de la aprobación muestran entorno, versión, commit y rama.
- Las aprobaciones se piden también en `dev`, para que el ensayo pruebe la mecánica.

## Relación con otras decisiones

Sustituye parcialmente al ADR-007 (solo el punto de la aprobación por run y el deployment job de `Foto`). Detalle en la enmienda del 2026-11-27 de `docs/superpowers/specs/2026-11-26-pipeline-release-un-boton-design.md`.

## Alternativas descartadas y por qué

- B: exige escrituras de configuración en Azure DevOps y cambia también los runs sueltos del 799 y del 832, que pedirían dos aprobaciones en cada stage de despliegue.
- C: tres aprobaciones antes de empezar, para el mismo control.
