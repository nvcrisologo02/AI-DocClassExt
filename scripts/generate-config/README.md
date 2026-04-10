# Generate-config extractor

This small console app exports Tipologias, Model configs and Plugin configs from the database into the JSON files consumed by `ConfigurationSeedService`.

Usage examples:

PowerShell (using env var for connection):

```powershell
$env:SEED_SOURCE_CONNECTION = 'Server=...;Database=...;Authentication=Active Directory Managed Identity;...'
dotnet run --project scripts/generate-config -- --dry-run --out-dir src/backend/DocumentIA.Functions/config
Remove-Item env:\SEED_SOURCE_CONNECTION
```

Flags:
- `--connection <conn>` : alternative to `SEED_SOURCE_CONNECTION` env var
- `--out-dir <path>` : output folder (default `src/backend/DocumentIA.Functions/config`)
- `--dry-run` : do not write files or move existing config; just print what would be done

Notes:
- If the connection string contains `Authentication=Active Directory Managed Identity` the extractor will use `DefaultAzureCredential` to obtain an access token. Ensure you are authenticated locally (e.g. `az login`) or running from an environment with an assigned identity.
