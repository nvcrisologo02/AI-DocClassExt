# Workbook y reporte de rendimiento CU en Application Insights

## Objetivo

Dejar preparadas las consultas para entender la extraccion con Azure Content Understanding separando:

- `CU.PrepareMs`: preparacion antes de CU.
- `CU.LimiterWaitMs`: espera local antes de entrar a `AnalyzeBinaryAsync`.
- `CU.AnalysisMs`: tiempo real de Azure Content Understanding.
- `CU.ParseMs`: parseo y mapeo de respuesta.
- `CU.Attempts`: numero de intentos.

La lectura clave es comparar `CU.LimiterWaitMs` contra `CU.AnalysisMs`:

- Si sube `CU.LimiterWaitMs`, hay cola local/backpressure por concurrencia.
- Si sube `CU.AnalysisMs`, Azure CU esta tardando en analizar.
- Si suben ambos, la carga supera la capacidad comoda del conjunto app + CU.

## Recurso objetivo

- Application Insights: `srbappiprodocai`
- Resource group: `SRBRGDOCSAIPROD`
- Suscripcion: `Produccion Central`

## Opcion A: Workbook importable

Archivo preparado:

- `docs/observabilidad/workbooks/documentia-cu-performance.workbook.json`

Pasos en Azure Portal:

1. Abrir Application Insights `srbappiprodocai`.
2. Ir a **Workbooks**.
3. Crear workbook nuevo.
4. En la barra superior del workbook, entrar en **Advanced Editor** del workbook completo.
5. Pegar el contenido del JSON preparado.
6. Guardar como `DocumentIA - CU Performance`.

No pegar este JSON dentro de **Advanced Settings** o **Advanced Editor** de un bloque Query individual. Si se pega ahi, el portal puede mostrar un error parecido a: `este tipo de elemento es 3 (consulta), pero el JSON proporcionado era undefined`, porque ese editor espera solo la definicion interna de un item y no el template completo del workbook.

Si al importar no toma el recurso automaticamente, seleccionar `srbappiprodocai` en el selector de recurso del Workbook y volver a ejecutar las consultas.

## Opcion B: reporte exportable por PowerShell

Script preparado:

- `scripts/reports/export-cu-performance-insights.ps1`

Ejemplo:

```powershell
.\scripts\reports\export-cu-performance-insights.ps1 `
  -ResourceGroup "SRBRGDOCSAIPROD" `
  -AppInsightsName "srbappiprodocai" `
  -OutputDir ".\artifacts\reports\cu-performance"
```

Genera ficheros JSON y CSV con:

- resumen de metricas CU,
- evolucion temporal p50/p95/p99,
- operaciones con mas espera,
- relacion espera vs analisis,
- errores transitorios CU.

## Consultas KQL principales

### 1. Comprobar si hay datos CU

```kusto
customMetrics
| where timestamp > ago(24h)
| where name startswith "CU."
| summarize eventos=count(), avg_ms=round(avg(value), 1), p95_ms=round(percentile(value, 95), 1), max_ms=max(value) by name
| order by name asc
```

### 2. Evolucion temporal por subfase

```kusto
customMetrics
| where timestamp > ago(24h)
| where name in ("CU.PrepareMs", "CU.LimiterWaitMs", "CU.AnalysisMs", "CU.ParseMs")
| extend tipologia = tostring(customDimensions["Tipologia"])
| summarize p50_ms=percentile(value, 50), p95_ms=percentile(value, 95), p99_ms=percentile(value, 99) by bin(timestamp, 5m), name, tipologia
| render timechart
```

### 3. Operaciones con mas espera

```kusto
customMetrics
| where timestamp > ago(24h)
| where name in ("CU.PrepareMs", "CU.LimiterWaitMs", "CU.AnalysisMs", "CU.ParseMs", "CU.Attempts")
| extend tipologia = tostring(customDimensions["Tipologia"])
| summarize ts=min(timestamp),
          prepare_ms=maxif(value, name == "CU.PrepareMs"),
          wait_ms=maxif(value, name == "CU.LimiterWaitMs"),
          analysis_ms=maxif(value, name == "CU.AnalysisMs"),
          parse_ms=maxif(value, name == "CU.ParseMs"),
          attempts=maxif(value, name == "CU.Attempts")
  by operation_Id, tipologia
| extend total_observado_ms = prepare_ms + wait_ms + analysis_ms + parse_ms
| extend wait_pct = round(100.0 * wait_ms / total_observado_ms, 1)
| order by wait_ms desc
| take 100
```

### 4. Diagnostico rapido

```kusto
customMetrics
| where timestamp > ago(24h)
| where name in ("CU.LimiterWaitMs", "CU.AnalysisMs")
| summarize p50_ms=percentile(value, 50), p95_ms=percentile(value, 95), max_ms=max(value) by name
| extend diagnostico = case(
    name == "CU.LimiterWaitMs" and p95_ms > 10000, "Cola local/backpressure",
    name == "CU.AnalysisMs" and p95_ms > 60000, "Azure CU lento o saturado",
    "Normal o revisar junto a la otra metrica")
```

### 5. Errores transitorios

```kusto
customEvents
| where timestamp > ago(24h)
| where name == "CU.TransientError"
| extend tipologia = tostring(customDimensions["tipologia"]),
         attempt = tostring(customDimensions["attempt"]),
         statusCode = tostring(customDimensions["statusCode"])
| summarize eventos=count() by bin(timestamp, 5m), tipologia, statusCode, attempt
| order by timestamp desc
```
