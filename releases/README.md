# releases/

Convención que lee la pipeline `Release` (stage `Guard`) para saber qué se despliega y qué scripts se ejecutan en cada entorno.

## Estructura

- `CURRENT`: una línea con la versión en curso (`v1.2.0`). Se toleran espacios y salto de línea final (LF o CRLF).
- `release.schema.json`: contrato del manifiesto (JSON Schema draft 2020-12). Es documentación; la validación real la hace `scripts/release/Test-ReleaseManifest.ps1` con comprobaciones en PowerShell, sin dependencias externas.
- `<version>/release.json`: manifiesto de cada versión (`v1.2.0/`).
- `_ensayo/`: release de ensayo para probar la pipeline en `dev` sin tocar una versión real.

## Manifiesto

| Campo | Obligatorio | Descripción |
|---|---|---|
| `version` | sí | `vX.Y.Z` (o `_ensayo`); debe coincidir con el nombre de la carpeta |
| `embeddings` | no | `version` (`vN`), `modo` y `restringidoModo` (`off`, `sombra` o `hibrido`); los tres campos son obligatorios si el bloque existe |
| `pasos` | sí | lista ordenada de pasos; `[]` si la release no lleva scripts |

Cada paso: `tipo` (`sql` o `ps1`), `ruta` (relativa a la raíz del repo, debe existir), `argumentos` (opcional; en los `.ps1` es obligatorio e incluye el marcador `<env>`, que la pipeline sustituye por el entorno) `plantilla` (opcional, solo en pasos `sql`; único valor: `embeddings`) y `entornos` (lista no vacía de `dev`, `pre`, `prod`; `dev` solo se admite en `_ensayo`). Los `.ps1` deben admitir `-DryRun` o `-WhatIf`.

`plantilla: "embeddings"` marca el paso SQL que la pipeline del hito 2 genera sustituyendo los `DECLARE` del script por los valores del bloque `embeddings` (`version`, `modo`, `restringidoModo`).

Reglas cruzadas que valida `Test-ReleaseManifest.ps1`:

- El bloque `embeddings` existe si y solo si hay exactamente un paso con `plantilla: "embeddings"`.
- Si un paso `.ps1` lleva `-Version <x>` en `argumentos` y existe el bloque `embeddings`, `<x>` debe coincidir con `embeddings.version`.
- No se admiten dos pasos con la misma `ruta` que compartan algún entorno (`dbo.ReleaseAplicada` identifica el paso por versión y ruta).
- `dev` solo se admite en `entornos` de `_ensayo`; una versión real (`vX.Y.Z`) no lo declara, porque `dev` siempre usa `_ensayo`.

## Resolución por entorno

- `dev` usa siempre `releases/_ensayo/release.json`, aunque `CURRENT` apunte a otra versión.
- `pre` y `prod` usan `releases/<contenido de CURRENT>/release.json`.
- Solo se devuelven los pasos cuyo `entornos` contiene el entorno pedido.
- `_ensayo` solo puede declarar `entornos: ["dev"]`.

## Scripts

```powershell
pwsh -NoProfile -File ./scripts/release/Get-ReleaseManifest.ps1 -Environment pre -AsJson
pwsh -NoProfile -File ./scripts/release/Test-ReleaseManifest.ps1 -ManifestPath ./releases/v1.2.0/release.json
```

`Test-ReleaseManifest.ps1` termina con código 0 si el manifiesto es válido y con 2 listando los errores. `Get-ReleaseManifest.ps1` lo invoca antes de devolver el objeto y falla con esa salida.

## Release sin scripts

Se crea igualmente `releases/<version>/release.json` con `"pasos": []` (y sin `embeddings` si no aplica) y se actualiza `CURRENT`.
