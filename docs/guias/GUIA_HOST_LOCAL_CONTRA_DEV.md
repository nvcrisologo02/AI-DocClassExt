# Guía: levantar el host de Functions en local contra la BBDD de DEV

Para probar cambios del backend y de los frontends batch de punta a punta sin desplegar,
escribiendo en la base de datos de DEV.

> **Aviso.** `local.settings.json` viene apuntando a **PRODUCCIÓN**
> (`srbsqlprodocai`, `srbkvprodocai`, GDC `srv_DocumentIA_pro`). Haz copia antes de tocarlo
> y compruébalo siempre antes de lanzar nada.

## Requisitos

| Herramienta | Comprobación | Notas |
|---|---|---|
| Azure CLI con sesión | `az account show` | La conexión al SQL de DEV usa tu identidad Entra |
| Azure Functions Core Tools | `func --version` | Probado con 4.9.0 |
| Node | `node --version` | Solo para ejecutar Azurite |
| Azurite | extensión de VS Code `azurite.azurite` | No hace falta instalarlo por npm |
| sqlcmd | `sqlcmd -?` | Para verificar resultados en BBDD |

El SQL de DEV (`srbsqldevdocai.database.windows.net`) resuelve a IP privada y es accesible
desde la red corporativa. Si `Test-NetConnection ... -Port 1433` falla, revisa la VPN antes
de seguir buscando.

## 1. Copia de seguridad de la configuración

```bash
cd c:/temp/MVP/documento-ia-clasificacion-mvp/src/backend/DocumentIA.Functions
cp local.settings.json local.settings.PRO.backup.json
```

`local.settings.json` está en `.gitignore`, así que estos cambios no se commitean.

## 2. Apuntar la conexión a DEV

La cadena de DEV está en el Key Vault de DEV. **Ojo**: el secreto usa
`Authentication=Active Directory Managed Identity`, que solo funciona dentro de Azure. Al
traerla a local hay que cambiarla a `Active Directory Default`, que usa la cadena de
`DefaultAzureCredential` e incluye el token de `az login`.

```bash
az keyvault secret show --vault-name srbkvdevdocai --name SqlConnectionString \
  --query value -o tsv | python -c "
import json, sys, re
val = sys.stdin.read().strip()
val = re.sub(r'(?i)Authentication\s*=\s*Active Directory Managed Identity',
             'Authentication=Active Directory Default', val)
d = json.load(open('local.settings.json', encoding='utf-8'))
for k in ['ConnectionStrings:DocumentIA', 'SqlConnectionString']:
    d['Values'][k] = val
json.dump(d, open('local.settings.json','w',encoding='utf-8'), indent=2, ensure_ascii=False)
print('Servidor:', re.search(r'(?i)server=([^;]+)', val).group(1))
"
```

Debe imprimir `tcp:srbsqldevdocai.database.windows.net,1433`. No imprimas nunca la cadena
completa por consola.

Comprueba que tu usuario tiene acceso real a la base:

```bash
sqlcmd -S srbsqldevdocai.database.windows.net -d DocumentIA -G \
  -Q "SELECT COUNT(*) FROM Documentos" -h -1
```

## 3. Storage local (Azurite) en puertos alternativos

El puerto 10001 (cola de Azurite) lo ocupa `agentid-service.exe`, un servicio corporativo
que no se puede parar. Por eso Azurite se levanta en 10011/10012 y el host se apunta a esos
puertos.

Ajusta el storage en `local.settings.json`:

```bash
python -c "
import json
p='local.settings.json'
d=json.load(open(p,encoding='utf-8'))
cs=('DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;'
    'AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;'
    'BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;'
    'QueueEndpoint=http://127.0.0.1:10011/devstoreaccount1;'
    'TableEndpoint=http://127.0.0.1:10012/devstoreaccount1;')
for k in ['AzureWebJobsStorage','AzureStorageConnectionString']:
    d['Values'][k]=cs
json.dump(d,open(p,'w',encoding='utf-8'),indent=2,ensure_ascii=False)
print('storage local configurado')
"
```

Y arranca Azurite. **`--skipApiVersionCheck` no es opcional**: sin él, el SDK de Storage
manda una versión de API que Azurite 3.36 no reconoce y el ingest devuelve 500 con
`InvalidHeaderValue`.

```bash
node "$HOME/.vscode/extensions/azurite.azurite-3.36.0/dist/src/azurite.js" \
  --silent --skipApiVersionCheck \
  --location /c/temp/azurite-documentia \
  --blobHost 127.0.0.1 --blobPort 10000 \
  --queueHost 127.0.0.1 --queuePort 10011 \
  --tableHost 127.0.0.1 --tablePort 10012
```

Ajusta la versión de la extensión si has actualizado VS Code.

## 4. Arrancar el host

```bash
cd c:/temp/MVP/documento-ia-clasificacion-mvp/src/backend/DocumentIA.Functions
func start --port 7071
```

Tarda ~1 minuto (compila y adquiere el lock de Durable). Los `404 BlobNotFound` del
arranque son ruido normal con un Azurite recién creado.

## 5. Verificar que apunta a DEV y no a PRO

```bash
curl -s -X POST http://localhost:7071/api/healthcheck
```

Y el contraste que de verdad importa: el total de ejecuciones que devuelve la API debe
cuadrar con el de la base de DEV.

```bash
curl -s "http://localhost:7071/api/management/ejecuciones?page=1&pageSize=1"
sqlcmd -S srbsqldevdocai.database.windows.net -d DocumentIA -G \
  -Q "SELECT COUNT(*) FROM DocumentoEjecuciones" -h -1
```

## 6. Probar los frontends batch

En cada aplicación, configura un entorno apuntando al host local:

- **URL backend**: `http://localhost:7071`
- **Function Key**: vacía (Core Tools no exige clave en local)

En `DocumentIA.Batch` y `DocumentIA.Batch.Classification` se hace desde la gestión de
entornos; en `ClassificationLite`, desde el diálogo de configuración.

Dos cosas que hay que marcar siempre en local:

1. **Force Reprocess activado.** Si el documento ya existe y `ForceReprocess` está a
   `false` (el valor por defecto), el orquestador detecta duplicado, devuelve la ejecución
   anterior y hace `return` **antes de llegar a `Persistir`**. No se inserta fila nueva y
   parece que el cambio no funciona.
2. **Subir a GDC desactivado.** El GDC configurado sigue siendo el de **producción**
   (`srv_DocumentIA_pro`); apuntar la BBDD a DEV no cambia eso.

Recuerda también que `Documentos.SubmittedBy` solo se escribe en el **alta** del documento.
Para validarlo hace falta un PDF que no exista aún en DEV; en un reproceso el valor del
documento no cambia a propósito, y el solicitante nuevo queda en la ejecución.

## 7. Prueba sin interfaz gráfica

Para validar el backend sin abrir los WPF, se puede llamar al ingest directamente. Payload
mínimo equivalente al que mandan los frontends:

```json
{
  "instrucciones": {
    "expectedType": "",
    "classificationOnly": true,
    "executeIntegrarWhenClassificationOnly": false,
    "maxPagesForClassificationOnly": 10,
    "forzarResumenPorDefecto": false,
    "skipDuplicateCheck": true,
    "forceReprocess": true,
    "skipGdcUpload": true,
    "classification": { "provider": "auto", "model": "auto", "nivelClasificacion": "TDN1_TDN2" },
    "extraction": { "provider": "auto", "model": "auto" }
  },
  "documento": { "name": "prueba.pdf", "content": { "base64": "<base64 del PDF>" } },
  "trazabilidad": {
    "correlationId": "<guid>",
    "submittedBy": "DocumentIA.Batch/nombre.apellido@sareb.es"
  }
}
```

`POST http://localhost:7071/api/IngestDocument`, y luego se sondea el `statusQueryGetUri`
de la respuesta hasta `runtimeStatus: Completed`.

Truco para conseguir un documento nuevo sin buscar PDFs: añadir unos bytes al final de uno
existente (`\n%% marca <guid>\n`). Cambia el SHA256 y el PDF sigue siendo válido, porque
todo lo que va tras `%%EOF` se ignora. Hay ejemplos en `docs/auxiliares/Ejemplos/`.

## 8. Volver a dejarlo como estaba

```bash
cd c:/temp/MVP/documento-ia-clasificacion-mvp/src/backend/DocumentIA.Functions
cp local.settings.PRO.backup.json local.settings.json
```

Para el host y Azurite.

## Problemas conocidos

| Síntoma | Causa | Solución |
|---|---|---|
| Ingest devuelve 500, en el log `InvalidHeaderValue ... API version no soportada` | Azurite antiguo frente al SDK de Storage | Arrancar Azurite con `--skipApiVersionCheck` |
| Azurite muere con `EADDRINUSE 127.0.0.1:10001` | `agentid-service.exe` ocupa el 10001 | Usar 10011/10012 y ajustar la cadena de storage |
| Login fallido contra el SQL | La cadena del Key Vault trae `Active Directory Managed Identity` | Cambiarla a `Active Directory Default` y tener `az login` hecho |
| `ClasificarActivity` cancelada a los ~45 s | Llamada a Azure OpenAI cortada desde el portátil | Suele ser transitorio: reintentar. Si es persistente, revisar VPN y proxy |
| La ejecución sale bien pero no aparece nada nuevo en BBDD | Dedup: documento ya existente sin `ForceReprocess` | Documento nuevo o `ForceReprocess` activado |
| No se ve el solicitante en `Documentos` | El documento ya existía; solo se informa en el alta | Comprobarlo en `DocumentoEjecuciones`, que sí lo registra en cada envío |
