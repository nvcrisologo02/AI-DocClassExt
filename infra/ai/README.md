# infra/ai — definición versionada de IA por entorno

Definición versionada de los recursos de Inteligencia Artificial (Azure OpenAI,
Content Understanding y Document Intelligence) que usa DocumentIA en cada
entorno, en el marco del plan "IA propia por entorno" (AB#100298). Esta
carpeta es la fuente de verdad de qué recurso, deployment, analyzer o
clasificador debe existir en cada entorno; no crea ni modifica nada por sí
sola, la aplican las tareas de promoción (ver más abajo).

## Regla de alcance

**Solo entran en `infra/ai/` los artefactos referenciados por filas activas de
`ModeloConfigs`** (`Activo = 1`) en cada entorno: los deployments, analyzers y
clasificadores que el sistema usa realmente en producción. El resto del
Foundry de PRO (analyzers de pruebas, versiones descartadas, experimentos)
no se versiona aquí — queda listado en `inventory-prod-foundry.json` como
inventario de referencia para una limpieza futura del recurso origen.

## Ficheros

- **`resources.<env>.json`** (`dev` / `pre` / `prod`): inventario de cuentas
  de Azure AI por entorno — `openai_primary`, `cu_primary`, `cu_secondary` y
  `di` — con su grupo de recursos y endpoint. Es el mapa que resuelve los
  alias de recurso (`ResourceAlias`) introducidos en las Tareas 1-4 del plan.

- **`deployments.<env>.json`**: los deployments de modelo (nombre de
  deployment, modelo base, versión, SKU y capacidad) que debe tener cada
  cuenta OpenAI del entorno. `deployments.dev.json` y `deployments.pre.json`
  describen el **estado deseado** (incluyen `gpt-5-mini`, que a día de hoy
  solo existe en PRO); son las Tareas 1 y 2 del plan las que lo crean en DEV y
  PRE. `deployments.prod.json` refleja los deployments reales verificados por
  ARM (`az cognitiveservices account deployment list`) el 2026-09-21, e
  incluye `gpt-4o` y un segundo `gpt-5-mini` en la cuenta secundaria
  (`srbaisrv-westeurope`) que no estaban en la hipótesis inicial del plan.

- **`di-artifacts.json`**: clasificadores y modelos personalizados de
  Document Intelligence en el recurso origen (`srbdiprodocai`, PRO) que hay
  que copiar a DEV y PRE, con el nombre de configuración que los referencia.
  Incluye una nota conocida: el recurso origen expone hoy `DI_NS1.4_v0`, no
  `DI_NS_1.4_v1` — la resolución de esa discrepancia queda para la Tarea 8,
  antes de ejecutar cualquier copia.

- **`analyzers/*.json`**: definición de cada analyzer de Content
  Understanding referenciado por `ModeloConfigs`, exportada desde el recurso
  origen de PRO sin los campos de solo lectura (`status`, `createdAt`,
  `lastModifiedAt`, `warnings`, `supportedModels`). Son la entrada para
  reconstruir el analyzer en DEV y PRE (Content Understanding no ofrece copia
  entre recursos; hay que recrear la definición). Generados por
  `scripts/ai/export-analyzer-definitions.ps1`.

- **`inventory-prod-foundry.json`**: listado completo de analyzers custom del
  recurso Foundry de PRO (excluye los `prebuilt-*`), con un flag `referenced`
  que marca cuáles de ellos están también en `analyzers/*.json`. Sirve para
  identificar en el futuro qué analyzers del Foundry de PRO ya no se usan y
  se pueden retirar.

## Flujo de promoción (Tareas 10 a 14 — aún no existen)

El orden previsto para aplicar esta definición a DEV y PRE es:

1. **Datasets** — preparar los datos de entrenamiento/referencia que
   necesiten los analyzers y clasificadores antes de recrearlos.
2. **Deployments** — crear en cada cuenta OpenAI de DEV/PRE los deployments
   de `deployments.<env>.json` (asegura que el modelo/versión/SKU exista
   antes de que algo dependa de él).
3. **Analyzers de Content Understanding, por reconstrucción** — crear cada
   analyzer de `analyzers/*.json` en el recurso Foundry del entorno destino a
   partir de la definición exportada (no hay copia directa entre recursos).
4. **Clasificadores de Document Intelligence, por Copy API** — copiar los
   artefactos de `di-artifacts.json` desde el recurso origen al recurso
   destino usando la Copy API de Document Intelligence (esta sí soporta copia
   entre recursos).
5. **Validación** — comprobar que DEV/PRE clasifican con los recursos propios
   y no con los de PRO, y que los resultados son equivalentes.

Estos pasos corresponden a las Tareas 10-14 del plan y todavía no están
implementados; esta carpeta es su entrada de datos.

## Estado inicial de DEV y PRE

pendiente (Tarea 8)

## Regenerar los analyzers

```powershell
pwsh scripts/ai/export-analyzer-definitions.ps1 -Ids CU_NS_1.4_3,CU_NS_1.5_0,CU_NS_1.6_0_GGAA,CERA16_v1,CERA44_vado,CERA46
```

El script hace únicamente operaciones de lectura (GET) contra el recurso
Foundry origen; no crea, modifica ni borra ningún analyzer. Añadir un id a la
lista y volver a ejecutar actualiza `analyzers/<id>.json` y regenera
`inventory-prod-foundry.json` completo.

Nota de implementación: el script obtiene el cuerpo de cada analyzer con
`Invoke-WebRequest` y un token de `az account get-access-token`, decodificando
la respuesta explícitamente como UTF-8, en vez de leer la salida de texto de
`az rest`. Se verificó que `az rest` recodifica su salida con la página de
códigos activa de la consola y sustituye caracteres no representables (tildes,
eñes) por el carácter de sustitución U+FFFD, perdiendo contenido real de las
definiciones (por ejemplo, "Dirección" llegaba corrupto). Leer los bytes de la
respuesta directamente evita esa pérdida.
