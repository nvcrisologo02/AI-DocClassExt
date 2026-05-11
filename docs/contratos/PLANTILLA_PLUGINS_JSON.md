# Plantillas de configuración de plugins por tipología

Este documento ofrece plantillas listas para copiar y adaptar en archivos:

- `src/backend/DocumentIA.Functions/config/tipologias/<tipologia>.plugins.json`

## Reglas generales

- Usar `pluginType` con valores: `rest`, `soap`, `custom`.
- `pluginKey` debe ser único dentro de la tipología.
- `priority` define el orden (1 se ejecuta antes que 2).
- `enabled: false` permite dejar plugins preparados sin ejecutarlos.
- `retryPolicy` es opcional, pero recomendable para servicios externos.

### Contrato funcional recomendado (`idActivo`)

Para compatibilidad con el flujo de persistencia/GDC:

- `IntegrarActivity` envía siempre `idActivo` en el payload al plugin (puede llegar vacío).
- Si el plugin obtiene/resuelve el identificador, debe devolverlo en `ResponseData` con clave `idActivo`.
- El pipeline resuelve el valor final con prioridad:
  1. `DatosFinales["idActivo"]` (valor devuelto por plugins)
  2. `IntegrarInput.IdActivo` (valor original de entrada)
  3. `null` si no existe en ninguno.
- El campo `returnsIdActivo: true` en el JSON es una **anotación documental** que indica que el plugin devuelve `idActivo`. No modifica el comportamiento del motor; es ignorado en la deserialización del modelo.

---

## 1) Plantilla mínima (1 plugin REST)

```json
{
  "tipologiaId": "mi.tipologia",
  "plugins": [
    {
      "pluginKey": "mi-rest-plugin",
      "pluginType": "rest",
      "enabled": true,
      "priority": 1,
      "configuration": {
        "baseUrl": "https://api.midominio.com",
        "endpoint": "/enrich",
        "authType": "None",
        "timeoutSeconds": 30
      }
    }
  ]
}
```

---

## 2) Plantilla REST completa (con headers + retry)

```json
{
  "tipologiaId": "mi.tipologia",
  "plugins": [
    {
      "pluginKey": "catalogo-rest",
      "pluginType": "rest",
      "enabled": true,
      "priority": 1,
      "configuration": {
        "baseUrl": "https://api.midominio.com",
        "endpoint": "/v1/enrichment",
        "authType": "ApiKey",
        "authToken": "REEMPLAZAR_API_KEY",
        "timeoutSeconds": 20,
        "headers": {
          "X-System": "DocumentIA",
          "X-Correlation-Source": "Functions"
        }
      },
      "retryPolicy": {
        "maxRetries": 3,
        "initialDelayMs": 1000,
        "exponentialBackoff": true,
        "retryOnStatusCodes": [408, 429, 500, 502, 503, 504]
      }
    }
  ]
}
```

---

## 3) Plantilla SOAP (con retry)

```json
{
  "tipologiaId": "mi.tipologia",
  "plugins": [
    {
      "pluginKey": "catastro-soap",
      "pluginType": "soap",
      "enabled": true,
      "priority": 2,
      "configuration": {
        "endpoint": "https://soap.midominio.com/service.svc",
        "soapVersion": "1.1",
        "action": "Consultar",
        "namespace": "http://tempuri.org/",
        "authType": "Basic",
        "username": "REEMPLAZAR_USUARIO",
        "password": "REEMPLAZAR_PASSWORD",
        "timeoutSeconds": 30
      },
      "retryPolicy": {
        "maxRetries": 2,
        "initialDelayMs": 1500,
        "exponentialBackoff": false,
        "retryOnStatusCodes": [408, 500, 502, 503]
      }
    }
  ]
}
```

---

## 4) Plantilla Custom (DLL externa)

```json
{
  "tipologiaId": "mi.tipologia",
  "plugins": [
    {
      "pluginKey": "reglas-negocio-custom",
      "pluginType": "custom",
      "enabled": true,
      "priority": 3,
      "configuration": {
        "assemblyPath": "C:\\temp\\MVP\\documento-ia-clasificacion-mvp\\plugins\\SarebEnrichments.dll",
        "className": "Sareb.Enrichments.NotaSimpleEnricher",
        "customConfig": {
          "enableCaching": true
        }
      }
    }
  ]
}
```

---

## 5) Plantilla mixta (REST + SOAP + Custom)

```json
{
  "tipologiaId": "mi.tipologia",
  "plugins": [
    {
      "pluginKey": "mi-rest-plugin",
      "pluginType": "rest",
      "enabled": true,
      "priority": 1,
      "configuration": {
        "baseUrl": "http://localhost:8080",
        "endpoint": "/",
        "authType": "None",
        "timeoutSeconds": 10
      },
      "retryPolicy": {
        "maxRetries": 2,
        "initialDelayMs": 500,
        "exponentialBackoff": true,
        "retryOnStatusCodes": [408, 429, 500, 502, 503, 504]
      }
    },
    {
      "pluginKey": "mi-soap-plugin",
      "pluginType": "soap",
      "enabled": true,
      "priority": 2,
      "configuration": {
        "endpoint": "http://localhost:8081",
        "soapVersion": "1.1",
        "action": "Consultar",
        "namespace": "http://tempuri.org/",
        "authType": "None",
        "timeoutSeconds": 15
      },
      "retryPolicy": {
        "maxRetries": 2,
        "initialDelayMs": 1000,
        "exponentialBackoff": false,
        "retryOnStatusCodes": [408, 500, 502, 503]
      }
    },
    {
      "pluginKey": "mi-custom-plugin",
      "pluginType": "custom",
      "enabled": true,
      "priority": 3,
      "configuration": {
        "assemblyPath": "C:\\temp\\MVP\\documento-ia-clasificacion-mvp\\plugins\\SarebEnrichments.dll",
        "className": "Sareb.Enrichments.NotaSimpleEnricher",
        "customConfig": {
          "enableCaching": true
        }
      }
    }
  ]
}
```

---

## 6) Checklist rápida antes de arrancar

- Archivo guardado en `config/tipologias` con nombre correcto: `<tipologia>.plugins.json`.
- Campo `tipologiaId` alineado con la tipología que llega a `IntegrarActivity`.
- Endpoints accesibles desde el entorno de ejecución.
- Si es `custom`, DLL existente y `className` implementa `ICustomEnricher`.
- Secretos fuera de Git cuando aplique (API keys, usuario/password).
- Si el plugin puede resolver el activo, devolver `idActivo` en `ResponseData`.

---

## 7) Convención recomendada (local / qa / prod)

### 7.1 Naming estándar

- `tipologiaId`: usar dominio funcional + versión cuando aplique (ej.: `nota.simple.1_4`).
- `pluginKey`: formato recomendado `<dominio>-<tipo>-<objetivo>`
  - Ejemplos: `sareb-rest-catastro`, `sareb-soap-registro`, `sareb-custom-rules`.
- `priority`: reservar `1` para plugins críticos; usar `2+` para enriquecimientos no bloqueantes.

### 7.2 Política de secretos

- No commitear valores reales de `authToken`, `username`, `password`.
- En documentación/config base usar placeholders explícitos:
  - `__API_KEY_QA__`, `__SOAP_USER_PROD__`, `__SOAP_PASSWORD_PROD__`.
- Mantener un proceso de inyección de secretos por entorno (pipeline o variable store).

### 7.3 Matriz de diferencias por entorno

| Campo | local | qa | prod |
|---|---|---|---|
| `baseUrl` / `endpoint` | localhost / mock | endpoints de integración QA | endpoints productivos |
| `enabled` | puede ir `false` para pruebas parciales | `true` en validación e2e | `true` estable |
| `retryPolicy.maxRetries` | bajo (1-2) | medio (2-3) | medio/alto según SLA |
| `timeoutSeconds` | bajo para feedback rápido | realista | realista + margen |
| credenciales | mock/dev | secret QA | secret PROD |

---

## 8) Plantilla por entorno (misma tipología)

> Recomendación: usar estos bloques como base y adaptar endpoints/keys según cada entorno.

### 8.1 Local (mock + tiempos cortos)

```json
{
  "tipologiaId": "nota.simple.1_4",
  "plugins": [
    {
      "pluginKey": "sareb-rest-catastro",
      "pluginType": "rest",
      "enabled": true,
      "priority": 1,
      "configuration": {
        "baseUrl": "http://localhost:8080",
        "endpoint": "/enriquecer",
        "authType": "None",
        "timeoutSeconds": 8,
        "headers": {
          "X-System": "DocumentIA-Local"
        }
      },
      "retryPolicy": {
        "maxRetries": 1,
        "initialDelayMs": 300,
        "exponentialBackoff": false,
        "retryOnStatusCodes": [408, 429, 500, 502, 503, 504]
      }
    },
    {
      "pluginKey": "sareb-custom-rules",
      "pluginType": "custom",
      "enabled": true,
      "priority": 2,
      "configuration": {
        "assemblyPath": "C:\\temp\\MVP\\documento-ia-clasificacion-mvp\\plugins\\SarebEnrichments.dll",
        "className": "Sareb.Enrichments.NotaSimpleEnricher",
        "customConfig": {
          "enableCaching": true
        }
      }
    }
  ]
}
```

### 8.2 QA (integración real controlada)

```json
{
  "tipologiaId": "nota.simple.1_4",
  "plugins": [
    {
      "pluginKey": "sareb-rest-catastro",
      "pluginType": "rest",
      "enabled": true,
      "priority": 1,
      "configuration": {
        "baseUrl": "https://qa-api.sareb.local",
        "endpoint": "/v1/catastro/enriquecer",
        "authType": "ApiKey",
        "authToken": "__API_KEY_QA__",
        "timeoutSeconds": 20,
        "headers": {
          "X-System": "DocumentIA-QA"
        }
      },
      "retryPolicy": {
        "maxRetries": 2,
        "initialDelayMs": 800,
        "exponentialBackoff": true,
        "retryOnStatusCodes": [408, 429, 500, 502, 503, 504]
      }
    },
    {
      "pluginKey": "sareb-soap-registro",
      "pluginType": "soap",
      "enabled": true,
      "priority": 2,
      "configuration": {
        "endpoint": "https://qa-soap.sareb.local/registro.svc",
        "soapVersion": "1.1",
        "action": "Consultar",
        "namespace": "http://tempuri.org/",
        "authType": "Basic",
        "username": "__SOAP_USER_QA__",
        "password": "__SOAP_PASSWORD_QA__",
        "timeoutSeconds": 25
      },
      "retryPolicy": {
        "maxRetries": 2,
        "initialDelayMs": 1000,
        "exponentialBackoff": false,
        "retryOnStatusCodes": [408, 500, 502, 503]
      }
    }
  ]
}
```

### 8.3 Prod (estable y auditable)

```json
{
  "tipologiaId": "nota.simple.1_4",
  "plugins": [
    {
      "pluginKey": "sareb-rest-catastro",
      "pluginType": "rest",
      "enabled": true,
      "priority": 1,
      "configuration": {
        "baseUrl": "https://api.sareb.local",
        "endpoint": "/v1/catastro/enriquecer",
        "authType": "ApiKey",
        "authToken": "__API_KEY_PROD__",
        "timeoutSeconds": 25,
        "headers": {
          "X-System": "DocumentIA"
        }
      },
      "retryPolicy": {
        "maxRetries": 3,
        "initialDelayMs": 1000,
        "exponentialBackoff": true,
        "retryOnStatusCodes": [408, 429, 500, 502, 503, 504]
      }
    },
    {
      "pluginKey": "sareb-soap-registro",
      "pluginType": "soap",
      "enabled": true,
      "priority": 2,
      "configuration": {
        "endpoint": "https://soap.sareb.local/registro.svc",
        "soapVersion": "1.1",
        "action": "Consultar",
        "namespace": "http://tempuri.org/",
        "authType": "Basic",
        "username": "__SOAP_USER_PROD__",
        "password": "__SOAP_PASSWORD_PROD__",
        "timeoutSeconds": 30
      },
      "retryPolicy": {
        "maxRetries": 3,
        "initialDelayMs": 1500,
        "exponentialBackoff": false,
        "retryOnStatusCodes": [408, 500, 502, 503]
      }
    },
    {
      "pluginKey": "sareb-custom-rules",
      "pluginType": "custom",
      "enabled": true,
      "priority": 3,
      "configuration": {
        "assemblyPath": "C:\\apps\\documentia\\plugins\\SarebEnrichments.dll",
        "className": "Sareb.Enrichments.NotaSimpleEnricher",
        "customConfig": {
          "enableCaching": true
        }
      }
    }
  ]
}
```

---

## 9) Checklist de promoción entre entornos

- Mantener el mismo `tipologiaId` y `pluginKey` entre local/qa/prod.
- Cambiar solo parámetros ambientales (`baseUrl`, credenciales, timeout, retries).
- Confirmar que no hay placeholders sin reemplazar (`__...__`).
- Validar conectividad de endpoints antes de habilitar `enabled: true` en prod.
- Registrar en PR qué campos cambiaron entre entornos y por qué.
- Verificar en QA que el plugin devuelve `idActivo` cuando aplica (trazabilidad para subida GDC).

