# Fuente de Verdad de Configuracion

> Última revisión: 2026-11-22 (AB#101447). Alcance: tipologías, modelos de IA, plugins por tipología, tarifas y clasificador por embeddings. Los app settings por entorno están en [MANUAL_CONFIGURACION.md](../manuales/MANUAL_CONFIGURACION.md).

## Regla operativa

La fuente de verdad para tipologias, modelos IA y plugins por tipologia es la base de datos gestionada por la Admin API y la aplicacion DocumentIA.Admin.

Los ficheros fisicos JSON del repositorio son artefactos de seed inicial, plantillas o referencia historica. Pueden estar desactualizados respecto a la configuracion real publicada en BBDD y no deben usarse para decidir el comportamiento productivo.

## Dominios

| Dominio | Fuente de verdad runtime | Ficheros fisicos |
|---|---|---|
| Tipologias y versiones | Tabla `Tipologias` / Admin API `/management/tipologias` | `src/backend/DocumentIA.Functions/config/tipologias/*.validation.json` solo seed o referencia |
| Modelos IA | Tabla `ModeloConfigs` / Admin API `/management/modelos` | `src/backend/DocumentIA.Functions/config/{classification,extraction,prompt,layout}/models.json` solo seed o referencia. El modelo generativo por entorno lo fijan las filas `UseAsFallback` y el app setting `PromptDefaults__ModelKey` (DEV `prompt.gpt56-luna`; PRE y PRO `default.gpt4o-mini`) |
| Plugins por tipologia | Tabla `PluginTipologiaConfigs` / Admin API `/management/plugins-tipologias/{codigo}` | `src/backend/DocumentIA.Functions/config/tipologias/*.plugins.json` solo seed o referencia |
| Clasificador por embeddings | Fila `clasificador.embeddings` de `ModeloConfigs` (tipo Embeddings; modo, umbrales y artefacto) y artefacto JSON en el blob `documentai` | `scripts/migrations/clasificador-embeddings/*.sql` solo alta y cambio de version. Ver ADR-002 |
| Tarifas de IA | Fila `tarifas.ia` de la tabla `ModeloConfigs` (tipo Tarifas) / seccion Modelos del Admin | `scripts/migrations/costes-ia/01-seed-tarifas-ia.sql` solo carga inicial. Ver [MANUAL_COSTES_IA.md](../manuales/MANUAL_COSTES_IA.md) |

## Creacion o cambio de tipologias

1. Crear o editar la configuracion en BBDD mediante DocumentIA.Admin o Admin API.
2. Mantener el estado `Draft` mientras se prepara y valida.
3. Publicar la version con la operacion `publicar`.
4. Verificar con `GET /api/tipologias` y una ingesta de prueba.

Los JSON pueden ayudar a preparar una plantilla o a sembrar un entorno nuevo sin datos, pero no sustituyen el alta en BBDD ni la publicacion.

## Regla de no borrado

No se deben borrar ficheros JSON historicos, seeds, plantillas o respaldos sin confirmacion explicita. Si se detecta desalineacion con BBDD, documentar la diferencia y decidir despues si se archiva, actualiza o conserva.
