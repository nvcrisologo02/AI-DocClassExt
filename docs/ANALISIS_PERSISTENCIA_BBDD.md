# Análisis de Persistencia en Base de Datos

## 📊 Configuración de Base de Datos

**Servidor**: SQL Server (SQL Server 2019+)  
**Base de Datos**: `DocumentIA`  
**Conexión**: `Server=localhost,1433;Database=DocumentIA;User Id=sa;Password=COMPLETAR_SQL_PASSWORD;`

---

## 📝 Tablas y Datos Guardados

### 1. **Tabla: Documentos** (Principal)
Almacena información del documento procesado.

| Campo | Tipo | Descripción | ¿Se Guarda? |
|-------|------|-------------|-----------|
| `Id` | int (PK) | ID único del registro | ✅ Auto |
| `Guid` | string(100) | GUID del documento | ✅ Sí |
| `NombreArchivo` | string(500) | Nombre original del archivo | ✅ Sí |
| `SHA256` | string(64) | Hash SHA256 del documento | ✅ Sí |
| `CRC32` | string(8) | Hash CRC32 del documento | ✅ Sí |
| `TamanoBytes` | long | Tamaño del archivo en bytes | ⚠️ No se guarda |
| `Tipologia` | string(100) | Tipo de documento detectado (ej: "nota.simple.1_3") | ✅ Sí |
| `Estado` | string(100) | Estado del procesamiento | ✅ Sí |
| `ConfianzaGlobal` | double? | Confianza entre 0 y 1 | ✅ Sí |
| `Paginas` | int | Número de páginas | ✅ Sí |
| `RutaBlobStorage` | string(500) | Ruta en blob storage (usar después) | ✅ Sí (cuando la subida a blob se realiza correctamente) |
| `CorrelationId` | string(100) | ID de correlación (trazabilidad) | ✅ Sí |
| `SubmittedBy` | string(200) | Quién envió el documento | ⚠️ No se guarda* |
| `IdGDC` | string(100) | ID GDC (trazabilidad externa) | ⚠️ No se guarda* |
| `IdActivo` | string(100) | ID del activo asociado | ⚠️ No se guarda* |
| `FechaCreacion` | DateTime | Fecha/hora de creación | ✅ Sí |
| `FechaProceso` | DateTime? | Fecha/hora de procesamiento | ✅ Sí |
| `FechaActualizacion` | DateTime? | Fecha/hora de última actualización | ⚠️ No se actualiza |

**\*** Campos disponibles pero no se mapean desde el contrato de entrada actual.

---

### 2. **Tabla: ResultadosProcesamiento** (Detalles)
Almacena los resultados detallados del procesamiento.

| Campo | Tipo | Descripción | ¿Se Guarda? |
|-------|------|-------------|-----------|
| `Id` | int (PK) | ID único del resultado | ✅ Auto |
| `DocumentoId` | int (FK) | ID del documento relacionado | ✅ Sí |
| **CLASIFICACIÓN** | | | |
| `ModeloClasificacion` | string(200) | Modelo usado para clasificar (ej: "GPT-4", "Claude") | ✅ Sí |
| `ConfianzaClasificacion` | double? | Confianza de la clasificación (0.0-1.0) | ✅ Sí |
| `FallbackLLM` | bool | Si usó LLM como fallback | ✅ Sí |
| **EXTRACCIÓN** | | | |
| `ModeloExtraccion` | string(200) | Modelo usado para extraer datos | ✅ Sí |
| `LayoutEnabled` | bool | Si se usó análisis de layout | ✅ Sí |
| `DatosExtraidosJson` | nvarchar(max) | **JSON con TODOS los datos extraídos del documento** | ✅ Sí |
| **POSTPROCESO** | | | |
| `NormalizacionesJson` | nvarchar(max) | JSON con normalizaciones aplicadas | ✅ Sí |
| `ValidacionesJson` | nvarchar(max) | JSON con resultados de validación | ✅ Sí |
| `InconsistenciasJson` | nvarchar(max) | JSON con inconsistencias detectadas | ✅ Sí |
| **INTEGRACIÓN** | | | |
| `ModuloIntegracion` | string(200) | Plugins ejecutados (ej: "mock-enrichment,mock-soap-catastro,sareb-business-rules") | ✅ Sí |
| `ResultadoIntegracion` | string(50) | Resultado: OK, REVISION o ERROR | ✅ Sí |
| **TIEMPOS** | | | |
| `TiempoNormalizacionMs` | int? | Tiempo normalización (ms) | ⚠️ No se guarda |
| `TiempoClasificacionMs` | int? | Tiempo clasificación (ms) | ✅ Sí |
| `TiempoExtraccionMs` | int? | Tiempo extracción (ms) | ✅ Sí |
| `TiempoValidacionMs` | int? | Tiempo validación (ms) | ⚠️ No se guarda |
| `TiempoIntegracionMs` | int? | Tiempo integración (ms) | ⚠️ No se guarda |
| `TiempoTotalMs` | int? | Tiempo total (ms) | ⚠️ No se guarda |
| `FechaCreacion` | DateTime | Fecha/hora de creación del resultado | ✅ Sí |

---

### 3. **Tabla: Auditorias**
Registro de log de todas las acciones.

| Campo | Descripción | ¿Se Guarda? |
|-------|-------------|-----------|
| `DocumentoId` | ID del documento relacionado | ✅ Sí |
| `Accion` | "Procesamiento Completo" o "Reprocesamiento" | ✅ Sí |
| `Nivel` | "Info" o "Warning" según estado | ✅ Sí |
| `Mensaje` | Mensaje descriptivo de la acción | ✅ Sí |
| `DetallesJson` | JSON completo del resultado | ✅ Sí |
| `FechaHora` | Fecha y hora de la auditoría | ✅ Sí |

---

## ⚠️ ¿Por Qué A Veces No Se Guarda Nada?

### Causa 1: **La actividad PersistirActivity nunca se ejecuta**
**Síntomas**: No hay registros en la BD después de ejecutar el test.

**Verificar**:
```powershell
# Mira los logs de Azure Functions
func host start

# Busca: "Persistiendo resultado para documento"
# Si no aparece, la orquestación no llegó al paso 7
```

**Causas posibles**:
- ❌ La orquestación falló antes de llegar a persistencia
- ❌ Error en validación, clasificación o extracción
- ❌ Error en integración de plugins

---

### Causa 2: **Error en la conectividad de Base de Datos**
**Síntomas**: 
- Log muestra: `"Error durante la persistencia: Connection refused"`
- O: `"Invalid connection string"`

**Verificar**:
```powershell
# Test de conectividad a SQL Server
Test-NetConnection localhost -Port 1433

# Si uses Docker, verifica que el contenedor esté corriendo
docker ps | grep mssql
```

**Soluciones**:
```powershell
# 1. Iniciar SQL Server si no está en Docker
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=COMPLETAR_SQL_PASSWORD" `
  -p 1433:1433 `
  -d mcr.microsoft.com/mssql/server:2019-latest

# 2. Crear base de datos
sqlcmd -S localhost -U sa -P "COMPLETAR_SQL_PASSWORD" -Q "CREATE DATABASE IF NOT EXISTS DocumentIA"

# 3. O usar el script limpiador
.\clean-and-test.ps1
```

---

### Causa 3: **El contrato no incluye datos de trazabilidad**
**Síntomas**: 
- Documento se guarda pero campos vacíos en `SubmittedBy`, `IdGDC`

**Causa**: El script `test-multi-plugin.ps1` no mapea estos campos

**Fix**: Actualizar payload en test:
```powershell
$payload = @{
    documento = @{ ... }
    trazabilidad = @{
        correlationId = [Guid]::NewGuid().ToString()
        submittedBy = "test-user"  # ← Aquí va
        idGDC = "GDC-001"            # ← Aquí va
    }
    instrucciones = @{ ... }
} | ConvertTo-Json
```

---

### Causa 4: **Tabla no existe**
**Síntomas**: 
- Log: `"Invalid object name 'dbo.Documentos'"`

**Verificar y crear tablas**:
```sql
-- En SQL Server Management Studio o sqlcmd

CREATE TABLE Documentos (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Guid NVARCHAR(100) NOT NULL,
    NombreArchivo NVARCHAR(500) NOT NULL,
    SHA256 NVARCHAR(64) NOT NULL,
    CRC32 NVARCHAR(8) NOT NULL,
    Tipologia NVARCHAR(100),
    Estado NVARCHAR(100) NOT NULL DEFAULT 'Pendiente',
    ConfianzaGlobal FLOAT,
    Paginas INT,
    CorrelationId NVARCHAR(100) NOT NULL,
    SubmittedBy NVARCHAR(200),
    IdGDC NVARCHAR(100),
    FechaCreacion DATETIME NOT NULL DEFAULT GETUTCDATE(),
    FechaProceso DATETIME
);

CREATE TABLE ResultadosProcesamiento (
    Id INT PRIMARY KEY IDENTITY(1,1),
    DocumentoId INT NOT NULL,
    ModeloClasificacion NVARCHAR(200),
    ConfianzaClasificacion FLOAT,
    FallbackLLM BIT,
    ModeloExtraccion NVARCHAR(200),
    LayoutEnabled BIT,
    DatosExtraidosJson NVARCHAR(MAX),
    NormalizacionesJson NVARCHAR(MAX),
    ValidacionesJson NVARCHAR(MAX),
    InconsistenciasJson NVARCHAR(MAX),
    ModuloIntegracion NVARCHAR(200),
    ResultadoIntegracion NVARCHAR(50),
    TiempoClasificacionMs INT,
    TiempoExtraccionMs INT,
    FechaCreacion DATETIME NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (DocumentoId) REFERENCES Documentos(Id)
);

CREATE TABLE Auditorias (
    Id INT PRIMARY KEY IDENTITY(1,1),
    DocumentoId INT NOT NULL,
    Accion NVARCHAR(100),
    Nivel NVARCHAR(50),
    Mensaje NVARCHAR(MAX),
    DetallesJson NVARCHAR(MAX),
    FechaHora DATETIME NOT NULL,
    FOREIGN KEY (DocumentoId) REFERENCES Documentos(Id)
);
```

---

## 🔍 Cómo Verificar Qué Se Guardó

### Query para ver documentos y resultados:
```sql
SELECT 
    d.Id,
    d.NombreArchivo,
    d.Tipologia,
    d.Estado,
    d.ConfianzaGlobal,
    r.ModeloClasificacion,
    r.ConfianzaClasificacion,
    r.ModuloIntegracion,
    d.FechaProceso,
    d.FechaCreacion
FROM Documentos d
LEFT JOIN ResultadosProcesamiento r ON d.Id = r.DocumentoId
ORDER BY d.FechaCreacion DESC;
```

### Query para ver datos extraídos:
```sql
SELECT 
    d.NombreArchivo,
    SUBSTRING(r.DatosExtraidosJson, 1, 500) AS 'Datos Extraídos (primeros 500 chars)',
    SUBSTRING(r.ValidacionesJson, 1, 500) AS 'Validaciones (primeros 500 chars)'
FROM Documentos d
LEFT JOIN ResultadosProcesamiento r ON d.Id = r.DocumentoId
WHERE d.Estado = 'OK'
ORDER BY d.FechaCreacion DESC;
```

### Query para ver auditoría:
```sql
SELECT 
    DocumentoId,
    Accion,
    Nivel,
    Mensaje,
    FechaHora
FROM Auditorias
ORDER BY FechaHora DESC;
```

---

## 📋 Checklist de Depuración

Si no se guarda nada, verifica en orden:

- [ ] Azure Functions está ejecutándose: `func host start`
- [ ] SQL Server está disponible: `Test-NetConnection localhost -Port 1433`
- [ ] Base de datos existe: `documentIA`
- [ ] Tablas existen: `Documentos`, `ResultadosProcesamiento`, `Auditorias`
- [ ] Log contiene: `"Persistiendo resultado para documento"`
- [ ] No hay errores de conexión en los logs
- [ ] Payload contiene los datos correctamente
- [ ] La orquestación completó exitosamente (estado = "Completed")
- [ ] Ejecuta SQL Server: `SELECT COUNT(*) FROM Documentos;`

---

## 🎯 Resumen

**✅ Qué se guarda siempre:**
- Información del documento (nombre, hash, tipo, estado)
- Confianza de clasificación y extracción
- Datos extraídos completos (JSON)
- Resultados de validación (JSON)
- Plugins ejecutados
- Fechas y horarios de procesamiento
- Auditoria de acciones

**⚠️ Qué falta o no se guarda:**
- Tamaño del archivo (campo `TamanoBytes`)
- Ruta en blob storage
- Información de trazabilidad del contrato (mapear campos)
- Tiempos de postproceso
- Tiempo total de procesamiento

