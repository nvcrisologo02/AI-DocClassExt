# Fuente de Verdad de Configuracion

## Regla operativa

La fuente de verdad para tipologias, modelos IA y plugins por tipologia es la base de datos gestionada por la Admin API y la aplicacion DocumentIA.Admin.

Los ficheros fisicos JSON del repositorio son artefactos de seed inicial, plantillas o referencia historica. Pueden estar desactualizados respecto a la configuracion real publicada en BBDD y no deben usarse para decidir el comportamiento productivo.

## Dominios

| Dominio | Fuente de verdad runtime | Ficheros fisicos |
|---|---|---|
| Tipologias y versiones | Tabla `Tipologias` / Admin API `/management/tipologias` | `config/tipologias/*.validation.json` solo seed o referencia |
| Modelos IA | Tabla `ModeloConfigs` / Admin API `/management/modelos` | `config/classification/models.json`, `config/extraction/models.json`, `config/prompt/models.json`, `config/layout/models.json` solo seed o referencia |
| Plugins por tipologia | Tabla `PluginTipologiaConfigs` / Admin API `/management/plugins-tipologias/{codigo}` | `config/tipologias/*.plugins.json` solo seed o referencia |

## Creacion o cambio de tipologias

1. Crear o editar la configuracion en BBDD mediante DocumentIA.Admin o Admin API.
2. Mantener el estado `Draft` mientras se prepara y valida.
3. Publicar la version con la operacion `publicar`.
4. Verificar con `GET /api/tipologias` y una ingesta de prueba.

Los JSON pueden ayudar a preparar una plantilla o a sembrar un entorno nuevo sin datos, pero no sustituyen el alta en BBDD ni la publicacion.

## Regla de no borrado

No se deben borrar ficheros JSON historicos, seeds, plantillas o respaldos sin confirmacion explicita. Si se detecta desalineacion con BBDD, documentar la diferencia y decidir despues si se archiva, actualiza o conserva.
