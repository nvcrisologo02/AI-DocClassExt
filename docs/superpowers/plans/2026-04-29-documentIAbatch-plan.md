# Plan de Implementación: DocumentIA.Batch

**Fecha:** 2026-04-29  
**Objetivo:** Aplicación Windows portable para procesado masivo de Notas Simples mediante DocumentIA.

**Estado 2026-04-29:** Sprint 1 iniciado en repositorio dedicado `DocumentIA.Batch`. La base WPF, la configuración local, la carga de PDFs y el editor de prompts por tipología están implementados y compilados correctamente. El código de la aplicación Batch no debe añadirse al monorepo `documento-ia-clasificacion-mvp`.

---

## 1. Visión General

`DocumentIA.Batch` es una aplicación WPF (.NET 8) portable que permite cargar un conjunto de PDFs (Notas Simples), procesarlos en paralelo contra el backend de Azure Functions de DocumentIA, monitorizar el progreso en tiempo real y exportar los resultados a CSV/Excel.

### Integración con el backend existente

La aplicación **no contiene lógica de extracción propia**: actúa como cliente HTTP del backend Azure Functions (DocumentIA.Functions). Flujo por fichero:

1. `POST /api/ingest` con `ContratoEntrada` → devuelve `instanceId` + `statusQueryUri`
2. Polling de `GET {statusQueryUri}` (Durable Functions HTTP Management API) → estados: `Running` | `Completed` | `Failed` | `Terminated`
3. Al completar, `runtimeStatus == "Completed"` → `output` contiene `ContratoSalida` (JSON)
4. `GET /api/tipologias` al arrancar → lista tipologías disponibles (nota.simple.1_0, 1_2, 1_3, 1_4)

---

## 2. Ubicación y Estructura del Proyecto

Repositorio dedicado: `c:\temp\MVP\DocumentIA.Batch`

```
src/
  DocumentIA.Batch/                         ← proyecto WPF
      DocumentIA.Batch.csproj
      App.xaml / App.xaml.cs
      MainWindow.xaml / MainWindow.xaml.cs
      
      Models/                               ← modelos de datos de UI
        FileBatchItem.cs                    ← estado por fichero en cola
        BatchSession.cs                     ← sesión completa de ejecución
        BatchConfig.cs                      ← configuración persitida (JSON)
        ExportRow.cs                        ← fila de exportación CSV/Excel
      
      ViewModels/
        MainViewModel.cs                    ← VM principal (MVVM)
        FileItemViewModel.cs                ← VM por fichero (bindable)
        ConfigPanelViewModel.cs             ← VM panel lateral config
        PromptEditorViewModel.cs            ← VM editor de prompts
        ResultDetailViewModel.cs            ← VM panel detalles post-ejecución
      
      Views/
        PromptEditorDialog.xaml             ← modal edición prompts
        SummaryDialog.xaml                  ← modal resumen post-proceso
        ResultDetailPanel.xaml              ← panel expansible datos extraídos
      
      Services/
        DocumentIAClient.cs                 ← cliente HTTP al backend
        PollingService.cs                   ← gestión colas + polling durable
        ExportService.cs                    ← exportación CSV y Excel
        SettingsService.cs                  ← lectura/escritura BatchConfig.json
        TipologiaService.cs                 ← carga tipologías desde API + prompts locales
      
      Converters/                           ← IValueConverter para WPF
        StatusToColorConverter.cs
        StatusToBadgeConverter.cs
        BoolToVisibilityConverter.cs
        ConfidenceToPercentConverter.cs
      
      Resources/
        Styles.xaml                         ← estilos globales dark theme
        Icons.xaml                          ← Fluent Icons (geometrías)
        Animations.xaml                     ← storyboards spinner / progress
```

---

## 3. Dependencias NuGet

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<PackageReference Include="System.Text.Json" Version="8.*" />          <!-- incluido en .NET 8 -->
<PackageReference Include="ClosedXML" Version="0.104.*" />             <!-- Excel export -->
<PackageReference Include="CsvHelper" Version="33.*" />                <!-- CSV export -->
<PackageReference Include="ModernWpfUI" Version="0.9.6" />             <!-- WinUI3-like dark theme -->
<PackageReference Include="Serilog" Version="3.*" />
<PackageReference Include="Serilog.Sinks.File" Version="5.*" />
```

Nota: durante Sprint 1 no se ha añadido `ProjectReference` al backend para mantener el repo portable y desacoplado. Si se decide reutilizar contratos existentes en un bloque posterior, validar primero la estrategia de dependencias entre repositorios.

Referencia prevista inicialmente para reutilizar modelos:
```xml
<ProjectReference Include="..\..\backend\DocumentIA.Core\DocumentIA.Core.csproj" />
```

---

## 4. Modelo de Datos de UI

### 4.1 `FileBatchItem` (estado en ejecución)

```csharp
public enum BatchFileStatus
{
    Pending,          // en cola, no iniciado
    Queued,           // enviado a API, esperando instanceId
    Running,          // polling activo
    Completed,        // OK
    Revision,         // EstadoCalidad = REVISION
    Error             // Error técnico o EstadoCalidad = ERROR
}

public class FileBatchItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public BatchFileStatus Status { get; set; } = BatchFileStatus.Pending;
    public string StatusLabel { get; set; } = "Pendiente";
    public int? QueueSlot { get; set; }                    // Cola 1..N asignada
    public string? InstanceId { get; set; }
    public string? StatusQueryUri { get; set; }
    public ContratoSalida? Result { get; set; }            // resultado completo
    public string? ErrorMessage { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration => StartTime.HasValue && EndTime.HasValue
        ? EndTime.Value - StartTime.Value : null;
}
```

### 4.2 `BatchSession` (sesión completa)

```csharp
public class BatchSession
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<FileBatchItem> Items { get; set; } = new();
    
    // Métricas calculadas al cierre
    public TimeSpan TotalDuration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
    public int CountOK => Items.Count(x => x.Status == BatchFileStatus.Completed);
    public int CountRevision => Items.Count(x => x.Status == BatchFileStatus.Revision);
    public int CountError => Items.Count(x => x.Status == BatchFileStatus.Error);
    
    // Tiempos medios (calculados de TrazaActividad de cada ContratoSalida)
    public double AvgDurationMs { get; set; }
    public Dictionary<string, double> AvgDurationByActivity { get; set; } = new();
}
```

### 4.3 `BatchConfig` (configuración persistida)

```csharp
public class BatchConfig
{
    public string BackendUrl { get; set; } = "http://localhost:7071";
    public string FunctionKey { get; set; } = string.Empty;
    public string DefaultTipologia { get; set; } = "nota.simple.1_4";
    public bool PromptingEnabled { get; set; } = true;
    public int UmbralConfianza { get; set; } = 85;       // 0-100
    public int NumeroColas { get; set; } = 4;
    public bool EjecutarConAssetResolver { get; set; } = true;
    public bool SubirAGDC { get; set; } = true;
    public int PollingIntervalMs { get; set; } = 2000;
    public string LastExportPath { get; set; } = string.Empty;
    
    // Prompts override por tipología (key = tipologiaCodigo)
    public Dictionary<string, PromptOverride> PromptOverrides { get; set; } = new();
}

public class PromptOverride
{
    public string? SystemPrompt { get; set; }
    public string? UserPromptTemplate { get; set; }
}
```

### 4.4 `ExportRow` (fila CSV/Excel)

```csharp
public class ExportRow
{
    // Identificación
    public string NombreFichero { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string Tipologia { get; set; } = string.Empty;
    public string TipologiaVersion { get; set; } = string.Empty;
    public string FechaProceso { get; set; } = string.Empty;
    public int Paginas { get; set; }
    
    // Integridad
    public string SHA256 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public string CRC32 { get; set; } = string.Empty;
    public string RutaBlobStorage { get; set; } = string.Empty;
    public string ObjectIdGDC { get; set; } = string.Empty;
    public string IdActivo { get; set; } = string.Empty;
    
    // Resultado ejecución
    public string EstadoFinal { get; set; } = string.Empty;         // OK|REVISION|ERROR
    public double ConfianzaGlobal { get; set; }
    public double ConfianzaClasificacion { get; set; }
    public double ConfianzaExtraccion { get; set; }
    public double ConfianzaValidacion { get; set; }
    public string ProveedorClasif { get; set; } = string.Empty;
    public string ProveedorExtrac { get; set; } = string.Empty;
    public bool FallbackClasif { get; set; }
    public bool FallbackExtrac { get; set; }
    public long DuracionTotalMs { get; set; }
    
    // Campos extraídos dinámicos → se añaden como columnas al generar el Excel/CSV
    // key = nombre campo, value = valor
    public Dictionary<string, string> CamposExtraidos { get; set; } = new();
    // key = nombre campo, value = confianza (si la provee CU)
    public Dictionary<string, string> ConfianzaCampos { get; set; } = new();
    
    // Validaciones y revisiones
    public string Validaciones { get; set; } = string.Empty;         // separadas por ", "
    public string CamposRevision { get; set; } = string.Empty;       // separados por ", "
    
    // Asset Resolver
    public string IdsActivosAAII { get; set; } = string.Empty;       // separados por ", "
    public string IdsActivosAACC { get; set; } = string.Empty;       // separados por ", "
}
```

---

## 5. Servicios

### 5.1 `DocumentIAClient`

Responsabilidades:
- `GetTipologiasAsync()` → `GET /api/tipologias` (anon)
- `IngestAsync(string filePathOrBase64, ContratoEntrada entrada)` → `POST /api/ingest` con Function Key
- `PollStatusAsync(string statusQueryUri)` → `GET {uri}` polling Durable HTTP Management

```csharp
public class IngestResponse
{
    public string InstanceId { get; set; } = string.Empty;
    public string StatusQueryGetUri { get; set; } = string.Empty;
}

public class DurableStatusResponse
{
    public string InstanceId { get; set; } = string.Empty;
    public string RuntimeStatus { get; set; } = string.Empty;  // Running|Completed|Failed
    public ContratoSalida? Output { get; set; }                 // cuando Completed
    public string? CustomStatus { get; set; }                   // JSON del SeguimientoOrquestacion Running
}
```

Implementación clave:
- HttpClient con `x-functions-key` header cuando BackendUrl es remoto
- Para ficheros locales: leer bytes, convertir a Base64, incluir en `Documento.Base64`
- Timeout configurable (default: 300s por petición de ingest)

### 5.2 `PollingService`

Responsabilidades:
- Gestionar N slots de ejecución paralela (semaphore con N = `Config.NumeroColas`)
- Por cada fichero: adquirir slot → ingest → polling en loop → liberar slot → tomar siguiente
- Publicar eventos de progreso via `IProgress<FileProgressEvent>` o `ObservableCollection`
- Cancelable via `CancellationToken`

```csharp
public record FileProgressEvent(
    Guid FileId,
    BatchFileStatus NewStatus,
    string StatusLabel,
    int? QueueSlot,
    ContratoSalida? Result,
    string? Error
);
```

Lógica de polling:
```
while (runtimeStatus == "Running") {
    await Task.Delay(Config.PollingIntervalMs);
    status = await client.PollStatusAsync(uri);
    // publicar customStatus parcial al ViewModel → update de StatusLabel en UI
}
```

Mapeo de resultado a estado UI:
- `runtimeStatus == "Failed"` → `BatchFileStatus.Error`
- `runtimeStatus == "Completed"` + `Resultado.EstadoCalidad == "OK"` → `Completed`
- `runtimeStatus == "Completed"` + `Resultado.EstadoCalidad == "REVISION"` → `Revision`
- `runtimeStatus == "Completed"` + `Resultado.EstadoCalidad == "ERROR"` → `Error`
- `runtimeStatus == "Completed"` + `Resultado.Estado == "ERROR"` → `Error`

### 5.3 `ExportService`

Responsabilidades:
- `ExportToCsvAsync(IEnumerable<FileBatchItem> items, string path)`
- `ExportToExcelAsync(IEnumerable<FileBatchItem> items, string path)`
- Descubrimiento dinámico de columnas de campos extraídos: unión de todas las claves de `DatosExtraidos` de todos los items completados, ordenadas alfabéticamente
- Para confianza de campos: si `MetricasDebug` del proveedor CU contiene confianza por campo, mapearlo; sino dejar vacío

Estructura de columnas Excel:
1. Bloque **Identificación**: NombreFichero, Guid, Tipologia, TipologiaVersion, FechaProceso, Paginas
2. Bloque **Integridad**: SHA256, MD5, CRC32, RutaBlobStorage, ObjectIdGDC, IdActivo
3. Bloque **Resultado**: EstadoFinal, ConfianzaGlobal, ConfianzaClasificacion, ConfianzaExtraccion, ConfianzaValidacion, ProveedorClasif, ProveedorExtrac, FallbackClasif, FallbackExtrac, DuracionTotalMs
4. Bloque **Campos Extraídos** (dinámico): columna por cada campo descubierto
5. Bloque **Confianza Campos** (dinámico): columna confianza por cada campo anterior
6. Bloque **Validaciones/Revisiones**: Validaciones, CamposRevision
7. Bloque **AssetResolver**: IdsActivosAAII, IdsActivosAACC

Formato Excel adicional:
- Filas con estilo según `EstadoFinal`: verde suave (OK), amarillo (REVISION), rojo suave (ERROR)
- Fila de cabecera con fondo azul corporativo y texto blanco
- Ancho de columnas auto-fit

### 5.4 `SettingsService`

- Carga/guarda `BatchConfig.json` en `%APPDATA%\DocumentIA.Batch\config.json` (o junto al exe para portabilidad)
- Decisión: guardar junto al `.exe` como `config.json` (portabilidad total → se mueve la carpeta entera)
- Serialización `System.Text.Json` con indentación

### 5.5 `TipologiaService`

- Llama `GET /api/tipologias` al arrancar
- Construye lista de tipologías disponibles para dropdown
- Cachea en memoria durante la sesión
- Fallback a lista hardcoded si backend no disponible al arrancar: `[nota.simple.1_0, 1_2, 1_3, 1_4]`

---

## 6. ViewModels (MVVM - CommunityToolkit.Mvvm)

### 6.1 `MainViewModel` (principal)

Propiedades observables:
- `ObservableCollection<FileItemViewModel> Files`
- `ConfigPanelViewModel Config`
- `bool IsRunning`
- `bool IsCompleted`
- `double ProgressPercent`
- `string ProgressLabel` ("X / Y procesados")
- `BatchSession? CurrentSession`
- `FilterMode CurrentFilter` (All | Errors | Revisions)
- `FileItemViewModel? SelectedFile` → dispara carga de ResultDetailPanel

Comandos:
- `AddFilesCommand` → OpenFileDialog (multi-select PDF)
- `RemoveFileCommand(FileItemViewModel)` → eliminar fichero pendiente
- `ClearAllCommand` → limpiar lista (solo si no Running)
- `StartProcessingCommand` → validar config, iniciar PollingService
- `CancelProcessingCommand` → cancelar CancellationTokenSource
- `FilterCommand(FilterMode)` → actualizar `FilesView` (CollectionViewSource)
- `ExportCommand` → llamar ExportService con SaveFileDialog
- `OpenResultDetailCommand(FileItemViewModel)`

### 6.2 `FileItemViewModel`

Wrapper observable de `FileBatchItem`. Propiedades adicionales:
- `bool IsExpanded` → toggle panel detalles inline
- `string StatusIcon` → Fluent icon key según estado
- `Brush StatusColor` → verde/amarillo/rojo/gris según estado
- `bool IsActionable` → si puede ver resultado (Completed/Revision/Error)
- `bool CanDelete` → solo si Pending

### 6.3 `ConfigPanelViewModel`

- Dos-way binding con `BatchConfig`
- `ObservableCollection<TipologiaDto> AvailableTipologias`
- `OpenPromptEditorCommand` → abre `PromptEditorDialog`
- Validación: NumeroColas [1..10], Umbral [0..100]

### 6.4 `PromptEditorViewModel`

- Campos: `SystemPrompt`, `UserPromptTemplate`
- Loaded desde `Config.PromptOverrides[selectedTipologia]` o valores por defecto de la tipología
- `SaveCommand` → persist en Config
- `ResetToDefaultCommand` → limpiar override (usa config de tipología del backend)

### 6.5 `ResultDetailViewModel`

- Recibe `ContratoSalida`
- Propiedad `IEnumerable<ExtractedFieldRow>` → lista bindable de {Campo, Valor, Confianza}
- Sección Finca, Titulares, Cargas detectada por prefijos de claves o configuración
- `string ValidacionesText` → join de validaciones
- `string AssetResolverText` → IDs AAII y AACC encontrados
- `string SeguimientoText` → timeline de actividades con duraciones

---

## 7. UI - Diseño XAML Detallado

### 7.1 Layout principal (`MainWindow.xaml`)

```
┌─────────────────────────────────────────────────────────────────────┐
│  Barra título: "DocumentIA Batch – Notas Simples"           [─][□][×]│
├──────────────────┬──────────────────────────────────────────────────┤
│  PANEL LATERAL   │  ÁREA PRINCIPAL                                  │
│  (280px fija)    │                                                  │
│                  │  [Barra progreso general] 0%    [Filtros]        │
│  Versión Nota    │  ┌──────────────────────────────────────────┐   │
│  [dropdown v3 ▼] │  │ Dropzone (visible si Files.Count == 0)    │   │
│                  │  │   "Arrastra PDFs aquí o haz clic"         │   │
│  Prompting [ON]  │  └──────────────────────────────────────────┘   │
│  [Editar Prompts]│                                                  │
│                  │  DataGrid / ListView de ficheros:               │
│  Umbral     [85] │  Nombre | Tamaño | Estado | Cola | Acción        │
│  N° Colas    [4] │                                                  │
│                  │  [Panel expansible resultado al clic de fila]   │
│  [✓]AssetResolver│                                                  │
│  [ ] Subir GDC  │  [Botones: Cargar | Limpiar | ► Iniciar | ■ Stop]│
│                  │                                                  │
│  ─────────────── │                                                  │
│  Backend URL:    │                                                  │
│  [______________]│                                                  │
│  Function Key:   │                                                  │
│  [______________]│                                                  │
└──────────────────┴──────────────────────────────────────────────────┘
```

### 7.2 Estados visuales de fila

| Estado    | Badge color | Fondo fila | Icono |
|-----------|------------|------------|-------|
| Pendiente | Gris       | Normal     | ⏳    |
| En cola N | Azul claro | Normal     | 🔄 spinner |
| Extrayendo| Azul       | Normal     | 🔄 spinner |
| Finalizado| Verde      | Verde sutil| ✅    |
| Revisión  | Amarillo   | Amarillo sutil | ⚠️ |
| Error     | Rojo       | Rojo sutil | ❌    |

El `StatusLabel` viene del `customStatus` del orquestador (texto de actividad actual durante Running).

### 7.3 Panel Detalle de resultado (expansible inline)

Cuando usuario hace clic en una fila completada:

```
▼ nota4.pdf                [✅ Finalizado]                [Ver ▼]
  ┌──────────────────────────────────────────────────────────┐
  │  FINCA                                                    │
  │    Ref. Catastral: 1234567XX1233A        Conf: 98%        │
  │    Descripción:    Piso 2ª planta...     Conf: 92%        │
  │  TITULARES                                                │
  │    Nombre: Juan Pérez                   Conf: 99%        │
  │    DNI:    12345678X                    Conf: 97%        │
  │  CARGAS                                                   │
  │    Hipoteca: 100.000 €                  Conf: 95%        │
  │    Embargo:  Agencia Tributaria         Conf: 90%        │
  │  VALIDACIONES: NIF válido ✓ | RefCatastral válida ✓      │
  │  ASSET RESOLVER: AAII: ACT-001, ACT-002 | AACC: -        │
  └──────────────────────────────────────────────────────────┘
```

### 7.4 Modal `SummaryDialog`

Al completar batch → emerge automáticamente:

```
┌────────────────────────────────────────┐
│  Resumen del Procesamiento          [×] │
│                                        │
│  Tiempo Total:  12 min 45 s            │
│  Media por Fichero:  2 min 7 s         │
│                                        │
│  Media por Actividad:                  │
│    Clasificar:    3.2 s                │
│    Extraer:       6.8 s                │
│    Validar:       1.9 s                │
│    SubirGDC:      8.1 s                │
│                                        │
│  [✅ 12 OK]  [⚠️ 3 Revisión]  [❌ 2 Error] │
│                                        │
│  [    Cerrar y Exportar    ]           │
└────────────────────────────────────────┘
```

Al pulsar "Cerrar y Exportar":
1. Cierra diálogo
2. Abre `SaveFileDialog` (filtro: "Excel (*.xlsx)|CSV (*.csv)")
3. Llama `ExportService`
4. Notificación de éxito con ruta del fichero generado

### 7.5 Modal `PromptEditorDialog`

```
┌─────────────────────────────────────────────────────┐
│  Editar Prompts – nota.simple.1_4               [×] │
│                                                     │
│  System Prompt:                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │ Eres un analista documental...              │   │
│  └─────────────────────────────────────────────┘   │
│  User Prompt Template (use {contenido}):            │
│  ┌─────────────────────────────────────────────┐   │
│  │ Genera un resumen ejecutivo...              │   │
│  └─────────────────────────────────────────────┘   │
│                                                     │
│  [Restaurar por defecto]  [Cancelar]  [Guardar]     │
└─────────────────────────────────────────────────────┘
```

---

## 8. Flujo de Ejecución End-to-End

```
Usuario pulsa "Iniciar"
        │
        ▼
MainViewModel.StartProcessingCommand
  → Validar: Files.Count > 0, Config válida
  → new CancellationTokenSource()
  → IsRunning = true
  → PollingService.RunBatchAsync(Files, Config, CancellationToken)
        │
        ▼ [SemaphoreSlim(NumeroColas)]
   Para cada FileBatchItem (ordenado por posición en lista):
        │
        ├─ await semaphore.WaitAsync()
        ├─ item.Status = Queued, QueueSlot = slotNumber
        │
        ├─ Leer bytes del fichero local
        ├─ Base64Encode
        │
        ├─ Construir ContratoEntrada:
        │    Documento.Name = FileName
        │    Documento.Base64 = base64content
        │    Instrucciones.SkipGDCUpload = !Config.SubirAGDC
        │    Instrucciones.AssetResolver.Enabled = Config.EjecutarConAssetResolver
        │    Instrucciones.Prompt.SystemPrompt = Config.PromptOverrides[tipologia]?.SystemPrompt (si override)
        │    Instrucciones.Extraction.Umbral = Config.UmbralConfianza / 100.0
        │    Instrucciones.ExpectedType = Config.DefaultTipologia (tipología seleccionada)
        │
        ├─ POST /api/ingest → {instanceId, statusQueryUri}
        ├─ item.Status = Running, item.InstanceId = instanceId
        │
        └─ Polling loop (Task independiente):
             while (status != Completed/Failed):
               await delay
               GET statusQueryUri
               Parsear customStatus → actualizar item.StatusLabel
             
             Si Completed → mapear ContratoSalida a item.Result + item.Status
             Si Failed    → item.Status = Error + item.ErrorMessage
             semaphore.Release()
             ProgressPercent = completados/total * 100

Al completar todos los ficheros:
  → IsRunning = false, IsCompleted = true
  → Calcular métricas de sesión
  → Abrir SummaryDialog automáticamente
```

---

## 9. Construcción del `ContratoEntrada`

La tipología seleccionada en el dropdown controla `Instrucciones.ExpectedType`. El backend resolverá la versión publicada correspondiente al código de tipología enviado.

Importante: dado que la v1.4 es la `isDefault: true` en la configuración actual, se debe pre-seleccionar en el dropdown al arrancar.

Campos relevantes del contrato:

```json
{
  "instrucciones": {
    "expectedType": "nota.simple.1_4",
    "skipGDCUpload": false,
    "assetResolver": { "enabled": true },
    "extraction": { "umbral": 0.85 },
    "classification": { "umbral": 0.85 },
    "prompt": {
      "systemPrompt": null,
      "userPromptTemplate": null
    }
  },
  "documento": {
    "name": "nota1.pdf",
    "base64": "JVBERi0xLj..."
  },
  "trazabilidad": {
    "correlationId": "batch-{batchSessionGuid}-{fileGuid}"
  }
}
```

---

## 10. Persistencia y Portabilidad

- **Configuración:** `config.json` junto al ejecutable → el usuario puede mover la carpeta completa
- **Logs:** `logs/batch-{fecha}.log` junto al ejecutable (Serilog rolling file)
- **Prompts override:** guardados en `config.json` bajo `promptOverrides`
- **Sin instalador:** publicación como `dotnet publish -r win-x64 --self-contained` → carpeta portable
- La aplicación **no tiene base de datos propia** en esta fase: toda la información de ejecuciones anteriores está en el backend (SQL DocumentIA). El historial de sesiones batch no se persiste entre reinicios de la app (simplificación explícita del MVP).

---

## 11. Tareas de Implementación

### Sprint 1 – Esqueleto + config + carga de ficheros (2-3 días)

- [ ] T1.1 Crear proyecto `DocumentIA.Batch.csproj` (WPF .NET 8, referencias)
- [ ] T1.2 Configurar `ModernWpf` dark theme en `App.xaml`
- [ ] T1.3 Implementar `SettingsService` + `BatchConfig` con load/save JSON
- [ ] T1.4 Implementar `ConfigPanelViewModel` + binding en `MainWindow`
- [ ] T1.5 Implementar `TipologiaService.GetTipologiasAsync()` + `DocumentIAClient` base
- [ ] T1.6 Implementar zona drag-and-drop + `AddFilesCommand` (multi-PDF)
- [ ] T1.7 Implementar ListView de ficheros con columnas básicas + `RemoveFileCommand`

### Sprint 2 – Ejecución + polling (3-4 días)

- [ ] T2.1 Implementar `DocumentIAClient.IngestAsync()` con Base64
- [ ] T2.2 Implementar `DocumentIAClient.PollStatusAsync()` + deserialización Durable response
- [ ] T2.3 Implementar `PollingService.RunBatchAsync()` con `SemaphoreSlim`
- [ ] T2.4 Implementar actualización en tiempo real via `IProgress<FileProgressEvent>`
- [ ] T2.5 Bindings de barra de progreso global + `ProgressLabel`
- [ ] T2.6 Bindings de `StatusLabel` y badge de estado por fila (con animación spinner)
- [ ] T2.7 Botones Iniciar / Cancelar con lógica de estado `IsRunning`

### Sprint 3 – Post-procesado + export (3-4 días)

- [ ] T3.1 Implementar `SummaryDialog` con métricas calculadas desde `BatchSession`
- [ ] T3.2 Implementar `ResultDetailPanel` con secciones Finca/Titulares/Cargas
- [ ] T3.3 Implementar `ResultDetailViewModel` con mapeo de `DatosExtraidos`
- [ ] T3.4 Panel expansible por fila (click en fila → expand inline)
- [ ] T3.5 Implementar filtros rápidos (All | Errores | Revisiones) via `CollectionViewSource`
- [ ] T3.6 Implementar `ExportService.ExportToExcelAsync()` (ClosedXML, columnas dinámicas)
- [ ] T3.7 Implementar `ExportService.ExportToCsvAsync()` (CsvHelper)
- [ ] T3.8 `SaveFileDialog` desde `SummaryDialog` "Cerrar y Exportar"

### Sprint 4 – Prompts + polish + portabilidad (2 días)

- [ ] T4.1 Implementar `PromptEditorDialog` + `PromptEditorViewModel`
- [ ] T4.2 Integrar override de prompts en `ContratoEntrada` al procesar
- [ ] T4.3 Colorización de filas por estado (verde/amarillo/rojo)
- [ ] T4.4 Estilos globales `Styles.xaml` + iconografía Fluent
- [ ] T4.5 Publicación `win-x64 self-contained` + verificar portabilidad
- [ ] T4.6 Tests manuales E2E con batch de 10 Notas Simples reales

---

## 12. Consideraciones de Seguridad

- La `FunctionKey` se guarda en `config.json` en texto plano junto al exe. Para MVP esto es aceptable (uso interno, red corporativa). En producción considerar `DPAPI` (`ProtectedData`) o variable de entorno.
- No se transmiten credenciales de usuario ni se almacena contenido de documentos localmente (solo Base64 temporal en memoria para el envío).
- El backend ya implementa HTTPS con autenticación; la app debe respetar la URL configurada por el usuario.
- Validar que los ficheros cargados tengan extensión `.pdf` y tamaño < límite razonable (ej. 50MB por fichero).

---

## 13. Criterios de Aceptación

1. La app arranca sin instalador desde cualquier carpeta, cargando config.json adyacente.
2. Se pueden arrastrar/soltar múltiples PDFs y eliminarlos antes de ejecutar.
3. El procesado paralelo respeta el número de colas configurado.
4. El estado de cada fichero se actualiza en tiempo real durante el procesado (actividad actual visible).
5. Al completar, emerge el modal de resumen con métricas correctas.
6. "Cerrar y Exportar" genera un Excel con todas las columnas especificadas y filas coloreadas.
7. El CSV exportado contiene los mismos datos que el Excel.
8. Los filtros "Errores" y "Revisiones" funcionan correctamente.
9. El panel de detalle de un fichero completado muestra los campos extraídos con su confianza.
10. Los prompts se pueden editar y el override se aplica en la siguiente ejecución.
11. La configuración (URL, key, umbrales, colas) persiste entre reinicios.
