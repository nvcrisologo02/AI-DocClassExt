# Runbook — Release a PRO de fiabilidad, rendimiento y almacenamiento (septiembre 2026)

Acciones para desplegar en PRO y dejar operativo el contenido de la release:
**AB#100176** (fiabilidad de resumen y persistencia, INC1338832), **AB#100182** (rendimiento del
Monitor), **AB#100165** (almacenamiento) y **AB#100192** (extracción sin modelKey).

> **Añadido el 07/09:** `develop` incorpora también el **control de costes de IA**
> (**AB#100224** y **AB#100235**). Si se despliega `develop` a PRO, ese código viaja con él y
> necesita **dos migraciones más** y **el catálogo de tarifas**, que no viaja en las
> migraciones. Los pasos están intercalados abajo (1.5, 1.6, 3.3 y 5.5). Ver
> [MANUAL_COSTES_IA.md](../manuales/MANUAL_COSTES_IA.md).

- **Commit a desplegar**: `origin/develop` (`091bb5a` o posterior). PRO despliega commits de
  `develop` (el actual en PRO es `16e51bc`, del 14/08); `master` se sincroniza después.
- **Validado en DEV** el 02/09: suite unitaria 999/999, Admin 113/113, E2E smoke 6/6 y full
  31 PASS / 0 FAIL / 1 N/A.
- Todos los pasos manuales usan sesión Entra (`az login`); ninguno necesita credenciales en claro.

## Regla de oro del orden

**El esquema de BD va SIEMPRE antes que el código.** El modelo EF del código nuevo incluye las
columnas `DocumentoEjecuciones.IdActivo` y `Documentos.NormalizacionMarkdownGzip`: si el código
nuevo arranca sin ellas, **cada INSERT de persistencia falla**. El sentido inverso es seguro: el
código actual de PRO ignora las columnas nuevas, el índice cubriente le es transparente y el SP
reescrito mantiene el mismo resultset.

---

## Fase 0 — Preparación (sin impacto)

- [ ] Confirmar que `origin/develop` está en el commit esperado y en verde
      (`git fetch && git log origin/develop -1`).
- [ ] Pasar el **Smoke pre-release PRO** (Test Plan **100069**, casos SMK-1..8) — norma previa a
      toda release a PRO.
- [ ] Elegir ventana de **baja actividad**. Motivo doble: AB#100176 añade llamadas a actividades
      en el orquestador y las orquestaciones en vuelo de la versión anterior pueden fallar por
      no determinismo al reproducirse; y los índices se crean con `ONLINE=ON` pero menos carga
      es menos riesgo en un S0 de 10 DTU.
- [ ] Tener a mano los tres scripts de BD (carpeta local `docs/auxiliares/temps/`, gitignored):
      `2026-09-02/markdown-binario-pro.sql`, `2026-09-02/idactivo-sp-pro.sql`,
      `2026-09-01/indice-monitor-pro.sql`. Los tres son idempotentes (guiados por
      `__EFMigrationsHistory`) y re-ejecutables.
- [x] **Backup pre-release** — HECHO el 03/09/2026: copia `DocumentIA-prerel-202609` creada a
      las **09:11:33 UTC** (`T0` de referencia; el PITR de 7 días cubre cualquier otro instante)
      y **verificada contra el origen**: 66.233 documentos, 70.166 ejecuciones (máx. Id 70202),
      esquema pre-release confirmado (sin `IdActivo` ni `NormalizacionMarkdownGzip`; última
      migración `20260812065020_SeedRestrictedClassificationPrompts`). Estado `Online`, S0.
      Comando usado, por si hay que repetirlo con otra fecha:
    ```bash
    az sql db copy --subscription "Producción Central" \
      --resource-group SRBRGDOCSAIPROD --server srbsqlprodocai --name DocumentIA \
      --dest-name DocumentIA-prerel-202609 --service-objective S0
    ```
    Coste: un S0 adicional (~céntimos/día); se borra tras el periodo de validación (Fase 6).
    **Nota**: si entre este backup y la ventana de BD pasan días u horas con tráfico, valorar
    recrear la copia justo antes de la Fase 1 — el peldaño 5 del plan de restauración pierde
    todo lo posterior a la copia.

## Fase 1 — Ventana de BD (única intervención manual de esquema)

Contra `srbsqlprodocai.database.windows.net` / `DocumentIA`
(`sqlcmd -G` o el patrón token az + `SqlConnection.AccessToken` si el MFA bloquea `-G`).
Presupuestar **30-45 min** (los dos índices sobre una tabla de 1,7 GB dominan el tiempo).

**Disponibilidad: esta fase NO requiere parada.** El sistema puede seguir procesando durante
toda la ventana. Detalle por operación (scripts v2 del 03/09, sin transacción global — la
versión inicial retenía el bloqueo de esquema durante los builds y sí habría bloqueado la tabla):

| Operación | Bloqueo | Efecto |
|---|---|---|
| `ADD COLUMN` nullable (×2) | Sch-M de milisegundos | Solo metadatos; imperceptible |
| `CREATE INDEX ... ONLINE=ON` (×2) | Sch-S al inicio, Sch-M breve al final | La tabla admite lecturas y escrituras durante todo el build |
| `DROP INDEX` / `DROP COLUMN` calculada | Sch-M de milisegundos | Solo metadatos |
| `CREATE OR ALTER PROCEDURE` | ninguno relevante | — |

Impactos residuales, no de disponibilidad: (a) cada Sch-M debe esperar a que terminen las
transacciones en curso sobre la tabla — con carga alta puede encolar peticiones unos segundos,
de ahí la ventana de baja actividad; (b) los builds ONLINE consumen DTU del S0 y las consultas
del Monitor pueden ir más lentas esos minutos (degradación, no corte). El índice cubriente se
crea **antes** de borrar el simple: en ningún momento la tabla se queda sin índice de
`FechaEjecucion`. Los tres scripts son re-ejecutables: si uno falla a medias, se relanza y
completa lo que falte.

- [ ] 1.1 `markdown-binario-pro.sql` (AB#100169) — añade la columna `varbinary` nullable.
      Operación de metadatos, instantánea.
- [ ] 1.2 `idactivo-sp-pro.sql` (AB#100168) — columna `IdActivo` + índice (`ONLINE=ON`), retirada
      de la columna calculada `IdActivoNormalizado` y su índice, y SP reescrito.
- [ ] 1.3 `indice-monitor-pro.sql` (AB#100185) — índice cubriente del Monitor (`ONLINE=ON`),
      retira el índice simple de `FechaEjecucion`.
- [ ] 1.4 Verificar (consultas incluidas en las cabeceras de los scripts):
      `IdActivo` y `NormalizacionMarkdownGzip` existen, `IdActivoNormalizado` no existe,
      índices `IX_DocumentoEjecuciones_FechaEjecucion_Monitor` (1 clave + 17 INCLUDE) e
      `IX_DocumentoEjecuciones_IdActivo_DocumentoId` presentes, y
      `EXEC sp_ObtenerDocumentoEjecucionesPorIdActivo @IdActivo='X'` responde sin error.
- [ ] 1.5 **Costes de IA (AB#100224)** — aplicar las dos migraciones
      `20260907075128_AddCostesIAToEjecuciones` y `20260907103326_AddCostesPorActividadYEstimado`:
      siete columnas anulables en `DocumentoEjecuciones` (`CosteIAEur`, `TokensIA`, las cuatro de
      actividad y `CosteEstimado`). Solo metadatos, instantáneas, sin bloqueo relevante. Generar el
      script idempotente con `dotnet ef migrations script --idempotent`, igual que el resto.
      Verificar: `SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('DocumentoEjecuciones')
      AND (name LIKE 'Coste%' OR name = 'TokensIA')` devuelve las siete.
- [ ] 1.6 **Catálogo de tarifas (AB#100224)** — ejecutar
      `scripts/migrations/costes-ia/01-seed-tarifas-ia.sql`. No es esquema: inserta la fila
      `tarifas.ia` en `ModeloConfigs` (`Tipo=4`). El script copia la tabla a
      `ModeloConfigs__bak_<fecha>` antes de tocarla. **Debe ir antes de la Fase 2**: sin catálogo el
      sistema no falla, pero toda ejecución posterior al despliegue queda con coste nulo y
      `tarifasCompletas=false`, y eso solo se recupera con relleno retroactivo. Verificar:
      `SELECT ModelKey, LEN(ConfiguracionJson) FROM ModeloConfigs WHERE Tipo = 4` devuelve una fila.

> Desde este punto PRO funciona con normalidad con el código antiguo. No hay prisa entre la
> Fase 1 y la Fase 2, pero conviene encadenarlas en la misma ventana.

## Fase 2 — Despliegue del código

- [ ] 2.1 Desplegar `origin/develop` en `srbappprodocai` por el flujo habitual.
- [ ] 2.2 Verificar el commit desplegado:
      `GET .../providers/Microsoft.Web/sites/srbappprodocai/deployments` → debe reportar el
      commit esperado.
- [ ] 2.3 Sonda barata de vida + fix nuevo: `POST /api/IngestDocument` con un nombre `.zip` y
      `base64` mínimo → debe devolver **400** con el mensaje del allowlist (AB#100180). Confirma
      a la vez que el código nuevo está vivo.

## Fase 3 — Backfills del histórico (por lotes, reanudables)

Pueden ejecutarse justo tras la Fase 1 o tras la Fase 2 — solo tocan datos, no esquema. Ambos
scripts avanzan por marca de agua sobre `Id`, son idempotentes y admiten `-MaxBatches` para ir
por tandas.

- [ ] 3.1 `pwsh ./scripts/database/backfill-idactivo.ps1 -Server srbsqlprodocai.database.windows.net`
      (~70k filas; estimar 15-45 min). Al terminar: "Backfill completado".
- [ ] 3.2 `pwsh ./scripts/database/migrar-markdown-a-binario.ps1 -Server srbsqlprodocai.database.windows.net`
      (~64k documentos, 640 MB de Base64; estimar 30-90 min, trocear con `-MaxBatches` si se
      prefiere). El script **verifica integridad byte a byte** contra el origen y aborta ante
      cualquier discrepancia. Al terminar: pendientes 0, discrepancias 0.
      *Nota*: la BD crece temporalmente ~240 MB (la copia binaria); es lo esperado — el ahorro
      llega en la fase de *contract*, fuera de esta release.
- [ ] 3.3 **Opcional** — `pwsh ./scripts/database/backfill-costes-estimados.ps1 -Server srbsqlprodocai.database.windows.net`
      (AB#100235). Estima el coste de las ejecuciones anteriores a la medición a partir de la
      volumetría ya persistida y las marca con `CosteEstimado=1`. Solo llega hasta donde llega el
      dato: en el histórico hay páginas de layout en casi todas las ejecuciones, pero **nunca
      tokens**, así que la clasificación generativa se estima y la extracción no se cubre. Escribe
      solo columnas escalares, nunca el contrato, y **no pisa ningún coste medido**. Ejecutar
      **después** del paso 1.6: sin catálogo no estima nada. Se puede posponer sin riesgo.

## Fase 4 — Alertas (suscripción Producción Central, permisos Monitoring Contributor)

- [ ] 4.1 `az account set --subscription "Producción Central"`.
- [ ] 4.2 `pwsh ./scripts/observability/create-monitor-alerts.ps1 -ActionGroupId <id de srbagoperprodocai>`
      → 8 scheduled query rules (las 3 nuevas de AB#100181: dedup fallido, ExpectedType no
      resoluble, clasificación sin contenido).
- [ ] 4.3 `pwsh ./scripts/observability/create-db-capacity-alert.ps1 -ActionGroupId <id de srbagoperprodocai>`
      → metric alert `srbalertstoprodocai` (storage_percent > 80%, AB#100171).
- [ ] 4.4 Verificar en el portal (Alertas → Reglas) que existen las 9 reglas y tienen el action
      group asociado.

## Fase 5 — Validación funcional

- [ ] 5.1 E2E smoke contra PRO:
      `pwsh ./tests/e2e-postdeploy/run-e2e-postdeploy.ps1 -Environment pro -Profile smoke`
      → 6/6 PASS esperado. (El perfil `full` en PRO es opcional: consume IA real; valorar.)
- [ ] 5.2 Comprobaciones en BD sobre las ejecuciones nuevas (las del smoke sirven):
      `DatosFinalesJson`/`DatosOriginalesJson` = NULL, `ActivityTimelineJson` con datos,
      contrato con `$.DetalleEjecucion.Seguimiento.Actividades = []`, `IdActivo` poblado cuando
      aplica, y en `Documentos` las dos columnas de markdown pobladas y **coincidentes**.
- [ ] 5.3 Monitor Admin en PRO: abrir listado y detalle de una ejecución nueva y de una
      histórica — el timeline debe verse en ambas (re-merge de AB#100167).
- [ ] 5.4 Con uso real acumulado (mismo día o siguiente), medir el "después" de AB#100186
      (App Insights, KQL de `docs/auxiliares/temps/2026-09-01/medicion-monitor-rendimiento.md`)
      y compararlo con la línea base: agregados p50 9.166 ms / p95 30.035 ms. Registrar la tabla
      en AB#100186 y cerrarlo.
- [ ] 5.5 Costes de IA: `pwsh ./scripts/testing/test-costes-ia.ps1 -Environment pro`. Comprueba de
      una pasada que el bloque llega con importes y `tarifasCompletas=true` al pedir
      `incluirCostes`, que **no** aparece sin el parámetro, y que el desglose por llamada cuadra
      con el total. Después, abrir `/costes` en el Admin de PRO (no está en el menú: por URL).
      Esperar cinco minutos tras el paso 1.6 antes de lanzarlo, por la caché del catálogo.

## Fase 6 — Cierres post-release

- [ ] 6.1 Sincronizar `master` con `develop` (merge de sincronización, como el `6764fb8` del
      14/08).
- [ ] 6.2 ADO: cerrar AB#100186 con la medición; pasar a Done los padres AB#100165, AB#100176 y
      AB#100182 (sus pendientes eran todos de este runbook).
- [ ] 6.3 Publicar la rama `fix/100170-markdown-binario` del repo **DocumentIA.Batch** y
      mezclarla (afecta solo al script de auditoría `eval/audit_notext_db.py`; los ejecutables
      no cambian).
- [ ] 6.4 Actualizar las entradas 1.17, 1.18 y 1.19 del historial de
      `DATA_MODELS_ER_DIAGRAM.md`: quitar el "PRO pendiente".
- [ ] 6.5 A los 2-3 días: revisar que las 3 alertas de fiabilidad (AB#100181) están **en
      silencio** — su objetivo es no saltar; si alguna salta, hay una vía no cubierta por los
      fixes. Vigilar también el ratio de `EXTRACCION_INCOMPLETA` (debe bajar: las 13 tipologías
      TDN sin extracción ya no caen ahí, AB#100192).
- [ ] 6.6 Acción humana pendiente de AB#100179: coordinar con el integrador GDC el envío de
      códigos de tipología (o campo vacío) en `expectedType`; la alerta
      `srbalertexpprodocai` medirá si sigue llegando texto libre.

## Qué NO hacer en esta release

- **No** ejecutar la fase de *contract* del markdown (dejar de escribir la columna Base64 o
  borrarla): es lo que rompería la vuelta atrás. Se decidirá con la lectura binaria verificada
  en PRO durante un tiempo.
- **No** aplicar retención/archivado de contratos ni compresión del contrato: pospuesto a medir
  el crecimiento un mes (spec de AB#100165).

## Plan de restauración de BD

**Protección disponible** (verificada el 03/09 contra la configuración real de PRO):

| Mecanismo | Qué da | RPO | RTO |
|---|---|---|---|
| **PITR automático** (retención 7 días, diferencial cada 24 h) | Restaurar a *cualquier instante* de los últimos 7 días, en una BD nueva | Segundos (log continuo) | **Incierto en tier DTU**: restaurar 2,9 GB puede ir de decenas de minutos a horas; Azure no lo garantiza en S0 |
| **Copia pre-release** (`DocumentIA-prerel-202609`, Fase 0) | Foto consistente justo antes de la ventana, **ya restaurada y online** | El instante de la copia (T0) | **Minutos**: solo el swap de nombres |

**Escalera de recuperación — usar siempre el peldaño más bajo posible:**

1. **Falla un script de la Fase 1 a medias** → relanzarlo (idempotente por bloque, completa lo
   que falte). Sin restauración: los scripts solo tocan esquema, no datos.
2. **Hay que deshacer un cambio de esquema concreto** → bloques `Down` de la migración
   correspondiente (índice simple, columna calculada + índice + SP originales, o borrar la
   columna de markdown). Sin pérdida: las columnas origen conservan todo.
3. **El código nuevo da problemas** → redesplegar `16e51bc`. Sin tocar BD: el código antiguo
   convive con el esquema nuevo (verificado) y la escritura dual del markdown garantiza que
   nada escrito se pierde.
4. **Un backfill deja datos sospechosos** → los backfills solo rellenan columnas nuevas;
   basta `UPDATE ... SET IdActivo = NULL` / `SET NormalizacionMarkdownGzip = NULL` en el rango
   afectado y relanzar. (El de markdown, además, verifica byte a byte y aborta solo.)
5. **Último recurso — daño de datos amplio o de causa desconocida** → restauración completa:
   - Opción rápida (copia): `ALTER DATABASE [DocumentIA] MODIFY NAME = [DocumentIA-danada]` y
     `ALTER DATABASE [DocumentIA-prerel-202609] MODIFY NAME = [DocumentIA]` (T-SQL en `master`;
     cerrar conexiones activas antes). Corte de servicio de ~1-2 min durante el swap; las
     cadenas de conexión no cambian.
   - Opción PITR (si el daño se detectó tarde y la copia ya no sirve):
     `az sql db restore --dest-name DocumentIA-restore --time "<UTC>"` y el mismo swap.
   - **Coste asumido de este peldaño**: se pierden las ejecuciones procesadas después del punto
     de restauración (los documentos siguen en blob y GDC, pero su rastro en BD desaparece).
     Por eso es el último recurso y por eso la decisión debe tomarse rápido si llega el caso.

**Cierre**: si a los 7 días de la release no ha hecho falta, borrar la copia
(`az sql db delete --name DocumentIA-prerel-202609 ...`) para no pagar el S0 extra. El PITR
sigue cubriendo los 7 días rodantes de siempre.

## Vuelta atrás

| Componente | Cómo | Pérdida |
|---|---|---|
| Código | Redesplegar el commit anterior (`16e51bc`) | Ninguna. El código antiguo ignora las columnas nuevas; el markdown se sigue leyendo por la columna Base64, que la escritura dual mantiene poblada |
| Esquema markdown | `Down` de `MarkdownBinario` (borra solo la columna binaria) | Ninguna (ensayado en DEV: aplicar→revertir→reaplicar sin pérdida) |
| Esquema IdActivo/SP | `Down` de `IdActivoEscalarYSpPorIdActivo` (restaura columna calculada, índice y SP originales) | Ninguna (las columnas origen del cálculo conservan el histórico) |
| Índice Monitor | `Down` de `IndiceCubrienteMonitorEjecuciones` (restaura el índice simple) | Ninguna |
| Backfills | No requieren revertirse: solo rellenan columnas nuevas | — |

Orden de reversión si hiciera falta completa: primero el código, después el esquema (nunca al
revés: el código nuevo sin columnas nuevas no puede persistir).
