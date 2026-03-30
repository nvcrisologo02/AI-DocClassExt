# API Tests - DocumentIA MVP

Scripts de prueba para las Azure Functions del MVP.

## Prerequisitos

1. Azure Functions corriendo localmente: `func start`
2. Docker containers activos (Azurite): `docker-compose up -d`

## Archivos

### test-requests.http
Archivo para usar con **REST Client** extension de VS Code.

**Instalación de REST Client:**
1. Abrir VS Code
2. Ir a Extensions (Ctrl+Shift+X)
3. Buscar "REST Client" por Huachao Mao
4. Instalar

**Uso:**
- Abrir `test-requests.http` en VS Code
- Click en "Send Request" sobre cada prueba
- Las respuestas aparecen en panel lateral

### test-ingest.ps1
Script PowerShell para prueba completa end-to-end.

**Uso:**
```powershell
.\test-ingest.ps1

Resultado:

Envía documento de prueba

Muestra instanceId

Espera 2 segundos

Consulta estado automáticamente

Guarda instanceId en last-instance-id.txt

check-status.ps1
Script para consultar el estado de una orquestación.

Uso:

powershell
# Usando el último instanceId guardado
.\check-status.ps1

# Con instanceId específico
.\check-status.ps1 -InstanceId "abc123..."
Flujo de Prueba Recomendado
Iniciar servicios:

powershell
# Terminal 1: Docker
docker-compose up -d

# Terminal 2: Functions
cd src/backend/DocumentIA.Functions
func start
Ejecutar prueba:

powershell
# Terminal 3: Tests
cd tests/api-tests
.\test-ingest.ps1
Ver logs en tiempo real:

Observar Terminal 2 (func start) para ver el pipeline ejecutándose

Consultar estado después:

powershell
.\check-status.ps1
Endpoints Disponibles
POST /api/IngestDocument - Ingestar documento

GET /runtime/webhooks/durabletask/instances/{id} - Estado de orquestación

GET /runtime/webhooks/durabletask/instances - Listar todas las instancias

POST /runtime/webhooks/durabletask/instances/{id}/terminate - Terminar orquestación

Estructura de Request
json
{
  "instrucciones": {
    "expectedType": "Tasacion",
    "skipDuplicateCheck": false,
    "forceReprocess": false,
    "classification": { "model": "auto", "umbral": 0.85 },
    "extraction": { "model": "auto", "umbral": 0.80 }
  },
  "documento": {
    "name": "nombre.pdf",
    "content": { "base64": "..." }
  },
  "trazabilidad": {
    "correlationId": "UNIQUE-ID",
    "submittedBy": "user@sareb.es",
    "idGDC": null,
    "idActivo": "ACT-123"
  }
}
Tips
Los PDFs de prueba son válidos pero minimalistas

El base64 incluido es un PDF de 1 página válido

Para crear tu propio PDF: usa create-test-pdf.ps1

Los instanceIds son únicos por ejecución

Las orquestaciones persisten en Azurite