# Diseño corto · Validación TLS del GDC contra la CA corporativa de SAREB

Fecha: 2026-11-25 · Línea: core · Origen: AB#101451 (punto 1, decisión «remediar ya») · Work item de ejecución: AB#101553 (Task hija de AB#101445, creada el 2026-11-25)

## Intención y porqué

`DocumentIA.Functions` acepta hoy cualquier certificado del GDC SINTWS (`HttpClientHandler.DangerousAcceptAnyServerCertificateValidator`) porque `GDC__BypassSslValidation=true` está fijado en DEV, PRE y PRO y, además, el hosting `Development` lo activa siempre. El motivo histórico es que el host Linux de App Service no confía en la CA interna de SAREB. Esto deja la conexión al GDC (HTTP Basic sobre TLS) expuesta a suplantación del servidor dentro de la red. Se quiere validar la cadena del GDC contra la raíz corporativa en los tres entornos y dejar el bypass solo como interruptor de emergencia.

## Hechos comprobados (2026-11-25)

- El GDC sirve la cadena completa en el handshake en DEV (`srbwidd03`), PRE (`srbwidi03`) y PRO (`SRBWIDP05`): hoja ← «Entidad certificadora Emisora de SAREB» (válida hasta 2031-12-16) ← «Entidad certificadora Raiz de SAREB» (autofirmada, válida 2025-12-16 a 2040-12-16, SHA-256 `EA0A8EAB540A7390B86E0763D368B0D5E204F0205B35640313A6B2DBCE43A367`). Los CN de las hojas coinciden con los hosts de `GDC__Endpoint`.
- `openssl verify` de la hoja de DEV contra la raíz, con la emisora como intermedia, da OK.
- App Service multi-tenant (Windows y Linux) no permite añadir CAs al almacén raíz del host; solo App Service Environment v3 lo hace (Microsoft Learn, «Certificates and the App Service Environment»). La validación debe ir en código.
- .NET admite `X509ChainPolicy.TrustMode = CustomRootTrust` con `CustomTrustStore` en Linux (Microsoft Learn, «TLS/SSL best practices · Custom certificate trust»).
- La CRL de la emisora está en `http://crl.sareb.es/...`; no se garantiza que sea alcanzable desde las Function Apps.
- Ningún otro proyecto del repo ni `DocumentIA.Batch` usa el bypass; el único punto es `Program.cs` de Functions.

## Objetivos

- La Function App valida el certificado del GDC contra la raíz de SAREB en DEV, PRE y PRO y rechaza cualquier otra cadena.
- `GDC__BypassSslValidation` sigue existiendo como interruptor de emergencia: `false` por defecto y en el pipeline; `true` acepta cualquier certificado y deja un aviso en el log al arrancar.
- Rechazos trazados con el emisor, el sujeto y el error de política, para diagnosticar sin volver a activar el bypass.
- Sin cambios en el GDC ni en la red; sin escrituras en Key Vault.

## No objetivos

- Pinning del certificado de servidor (caduca con cada renovación).
- Comprobación de revocación (CRL interna no alcanzable con garantía). Se deja `RevocationMode = NoCheck` y se documenta.
- Añadir la CA al almacén del host (no existe en App Service multi-tenant).
- Tocar la conexión del Admin, del AssetResolver o del Batch (no hablan con el GDC por TLS interno).

## Anti-objetivos

- Que un despliegue deje PRO sin GDC por una validación mal configurada sin un camino de vuelta inmediato: por eso el interruptor se conserva y el despliegue va DEV → PRE → PRO comprobando el health del GDC en cada paso.
- Meter datos públicos (la raíz) en Key Vault.

## Restricciones

- .NET 10, Azure Functions v4 aislado, host Linux. Contrato de settings en `scripts/config/azure-appsettings-contract.json` (lo no secreto lo fija el pipeline).
- El pipeline restablece los app settings en cada despliegue: un `true` manual de emergencia dura hasta el siguiente despliegue, que es el comportamiento deseado.
- Español en documentación; sin coautores ni menciones de modelos; `AB#` en cada commit.

## Enfoques considerados

1. **Raíz versionada en el repo con sobrescritura por configuración (elegido).** PEM público junto al código de Functions, cargado al arrancar; app setting opcional `GDC__TrustedRootCertificate` con el PEM que lo sustituye sin esperar a un despliegue. Sin tocar Key Vault ni pipeline por entorno. Rotación de la raíz = commit; la raíz vive hasta 2040.
2. Raíz como app setting fijado por el pipeline (variable compartida en el YAML, ~1,5 KB base64). Mismo comportamiento que 1 en ejecución; no aporta nada y engorda el YAML. Descartado.
3. Raíz en Key Vault (`GDC--TrustedRootCertificate`). Rota sin desplegar, pero mete un dato público en KV, exige escritura en los tres KV (PIM en PRO) y rompe la regla del contrato. Descartado.

Descartados antes de la lista: instalar la CA en el host (no disponible) y pinning (frágil).

## Cómo se construye (decidido)

- **Fichero de confianza:** `src/backend/DocumentIA.Functions/config/certs/sareb-ca-chain.pem`, con la raíz y la emisora de SAREB (dos bloques PEM), copiado al output con el patrón `config\**` ya existente del csproj (ampliar el `None Update` a `*.pem` o añadir uno equivalente). Incluir la emisora endurece ante un servidor que no envíe la intermedia.
- **Settings nuevos en `GdcSettings`:** `TrustedRootCertificate` (string, PEM, opcional; si viene relleno sustituye al fichero) y `TrustedRootCertificatePath` (string, por defecto `config/certs/sareb-ca-chain.pem` relativo a `AppContext.BaseDirectory`). `BypassSslValidation` (bool, por defecto `false`) pasa a estar tipado en `GdcSettings` en lugar de leerse como string suelto.
- **Validador:** clase `GdcServerCertificateValidator` en `DocumentIA.Core` (junto a los servicios GDC), sin dependencias de Functions. Constructor con la colección de certificados de confianza y un `ILogger`; método `Validate(X509Certificate2? cert, X509Chain? chain, SslPolicyErrors errors)` que: rechaza si no hay certificado; construye un `X509Chain` nuevo con `TrustMode = CustomRootTrust`, `CustomTrustStore` = certificados de confianza, `ExtraStore` = elementos de la cadena recibida, `RevocationMode = NoCheck`; acepta solo si `Build` devuelve true y no hay `RemoteCertificateNameMismatch` ni `RemoteCertificateNotAvailable`; en rechazo traza Warning con sujeto, emisor, `SslPolicyErrors` y los `ChainStatus`. Método estático `LoadTrustStore(string? pem, string path)` que carga el PEM del setting o del fichero (`X509Certificate2Collection.ImportFromPem`).
- **Cableado en `Program.cs`:** `ConfigurePrimaryHttpMessageHandler` lee `GdcSettings`; si `BypassSslValidation` es true, mantiene `DangerousAcceptAnyServerCertificateValidator` y registra un Warning al arrancar («validación TLS del GDC desactivada por GDC__BypassSslValidation»); si no, carga el almacén de confianza y usa el validador. Desaparece la excepción por `IsDevelopment()`. Si el almacén no puede cargarse (fichero ausente o PEM inválido), el arranque falla con un error claro: es preferible a arrancar sin validación.
- **Pipeline:** `GDC__BypassSslValidation=false` en `azure-pipelines.yml` (los tres entornos comparten la línea). Contrato de settings: `GDC__BypassSslValidation` sigue como `LiteralOrNonSecret`/pipeline; se añade `GDC__TrustedRootCertificate` como opcional no secreto, dueño «manual de emergencia», con nota.
- **Despliegue:** DEV primero (pipeline de despliegue a DEV según `RELEASE_MANAGEMENT.md`), comprobar el health del GDC y una subida real; PRE y PRO con la siguiente release, con el mismo check. Camino de vuelta: poner `GDC__BypassSslValidation=true` a mano y reiniciar.
- **Tests (DocumentIA.Tests.Unit):** mini PKI generada con `CertificateRequest` (raíz, intermedia, hoja). Casos: cadena correcta con raíz confiada → acepta; hoja firmada por otra raíz → rechaza; raíz confiada pero `RemoteCertificateNameMismatch` → rechaza; sin certificado → rechaza; `LoadTrustStore` desde PEM en setting, desde fichero y con PEM inválido (excepción). El cableado de `Program.cs` no se testea unitariamente; se verifica en DEV.
- **Documentación (mismo commit):** técnica `MANUAL_CONFIGURACION.md` (nota de `GDC:BypassSslValidation` y nuevos settings), `05_MANUAL_USO_CONFIGURACION.md` (FAQ del certificado), `ESPECIFICACION_CAPA_SERVICIO_GDC_SINTWS.md`, `README` si lista settings; arquitectura `01_ARQUITECTURA_SISTEMA.md` (fila «SSL GDC») e `INFRAESTRUCTURA_REAL_DESPLEGADA.md` (⚠️ bypass); explotación `08_CHECKLISTS_DESPLIEGUE.md`, `RUNBOOK_INCIDENTES_PRODUCCION.md` (paso del GDC y procedimiento de emergencia), `MANUAL_HEALTHCHECK.md`. Funcional: ninguno.
- **ADR:** disparador de seguridad. Se propondrá «ADR-005 · Validación TLS del GDC contra la CA corporativa» al cerrar la unidad; se crea solo con sí explícito.

## Queda para el constructor

- Nombre exacto del namespace y la carpeta en `DocumentIA.Core` (junto a `GdcService`/`ResilientGdcService`).
- Cómo generar la mini PKI en los tests (helper compartido en `Fixtures/`).
- Texto exacto de los mensajes de log.
