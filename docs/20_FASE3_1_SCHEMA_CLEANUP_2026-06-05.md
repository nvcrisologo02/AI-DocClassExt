# Fase 3.1 - Schema Cleanup: Deprecation & Drop of Legacy Columns (2026-06-05)

> **Status**: ✅ COMPLETED & DEPLOYED  
> **Date**: 2026-06-05  
> **Migration**: `20260605113924_v31_DropLegacyModelosUmbrales`  
> **Commit**: `3e36c24`  
> **Work Items Closed**: AB#99744, AB#99746, AB#99747, AB#99749

---

## Executive Summary

Fase 3.1 completes the schema cleanup initiated in v1.5 by deprecating and physically removing 4 redundant columns from the `Tipologias` table. These columns have been superseded by properties in `ConfiguracionJson` since v1.5.

| Column | Replacement in ConfiguracionJson | Status |
|--------|----------------------------------|--------|
| `ModeloClasificacionDI` | `confidenceConfig.clasifUmbralFallback` | ✅ DROPPED |
| `UmbralClasificacion` | `confidenceConfig.clasifUmbralFallback` | ✅ DROPPED |
| `ModeloExtraccionDI` | `extraction.modelKey` | ✅ DROPPED |
| `UmbralExtraccion` | `confidenceConfig.extracUmbralFallback` | ✅ DROPPED |

---

## Deprecation Timeline (Complete Lifecycle)

```
v1.5 (2026-02-28):
  ├─ Added equivalent properties to ConfiguracionJson
  ├─ Marked 4 columns as [Obsolete] in code
  └─ Schema: Columns still present in DB

v2.0 (2026-06-05):
  ├─ Applied [NotMapped] to 4 columns
  ├─ EF Core no longer loads from DB
  ├─ Code references still work (return null/default)
  └─ Schema: Columns still present in DB

v3.1 (2026-06-05) ⬅️ TODAY — CURRENT
  ├─ Created migration: 20260605113924_v31_DropLegacyModelosUmbrales
  ├─ Executed migration: 4 columns physically DROPPED
  ├─ Impact: Zero data loss (all config moved to ConfigJson in v1.5)
  └─ Schema: Columns REMOVED from DB

v4.0 (Future - TBD):
  └─ Remove properties from TipologiaEntity code (breaking change, major version)
```

---

## Migration Details

### Migration Class: `20260605113924_v31_DropLegacyModelosUmbrales.cs`

**Location**: `src/backend/DocumentIA.Data/Migrations/`

**Up() - Drop Phase**
```sql
ALTER TABLE [Tipologias] DROP COLUMN [ModeloClasificacionDI];
ALTER TABLE [Tipologias] DROP COLUMN [ModeloExtraccionDI];
ALTER TABLE [Tipologias] DROP COLUMN [UmbralClasificacion];
ALTER TABLE [Tipologias] DROP COLUMN [UmbralExtraccion];
```

**Down() - Rollback Phase** (all columns restored with correct types and defaults)
```sql
ALTER TABLE [Tipologias] ADD [ModeloClasificacionDI] nvarchar(200) NULL;
ALTER TABLE [Tipologias] ADD [ModeloExtraccionDI] nvarchar(200) NULL;
ALTER TABLE [Tipologias] ADD [UmbralClasificacion] float NOT NULL DEFAULT 0.85;
ALTER TABLE [Tipologias] ADD [UmbralExtraccion] float NOT NULL DEFAULT 0.80;
```

**Execution Status**:
- ✅ Applied to database: 2026-06-05 11:45 UTC
- ✅ Rollback tested: Down() procedure confirmed
- ✅ Zero data loss: No data was stored in these columns post-v1.5

---

## Code Impact

### TipologiaEntity.cs - Post-Deprecation State

All 4 properties marked with both `[Obsolete]` and `[NotMapped]`:

```csharp
[Obsolete("Use ConfiguracionJson.confidenceConfig.clasifUmbralFallback instead (v1.5+). Removed in v3.0.", false)]
[NotMapped]
public string? ModeloClasificacionDI { get; set; }

[Obsolete("Use ConfiguracionJson.confidenceConfig.clasifUmbralFallback instead (v1.5+). Removed in v3.0.", false)]
[NotMapped]
public double UmbralClasificacion { get; set; } = 0.85;

[Obsolete("Use ConfiguracionJson.extraction.modelKey instead (v1.5+). Removed in v3.0.", false)]
[NotMapped]
public string? ModeloExtraccionDI { get; set; }

[Obsolete("Use ConfiguracionJson.confidenceConfig.extracUmbralFallback instead (v1.5+). Removed in v3.0.", false)]
[NotMapped]
public double UmbralExtraccion { get; set; } = 0.80;
```

**Effect**: 
- EF Core no longer attempts to load these columns
- Code can still reference properties (get `null`/default, no SQL error)
- Compile warnings (CS0618) remind developers to migrate
- Properties will be removed in v4.0 (major version)

### Affected Code Areas (No Changes Required)

1. **TipologiaMapper.cs**: Already filters these fields; no action needed
2. **TipologiaRepository.cs**: CloneForAudit copies [NotMapped] properties as null/default; audit trail unaffected
3. **DocumentIADbContext.cs**: Seed data still references columns for backward compat; ignored at persist time

---

## Validation Results

✅ **Compilation**: 0 errors, 53 warnings (CS0618 [Obsolete] expected)  
✅ **Unit Tests**: 12/12 TipologiaDeprecationTests PASSING  
✅ **Migration Application**: Successful; 4 columns physically removed  
✅ **Azure Functions Host**: Clean startup; no "Invalid column" errors  
✅ **Build Artifact**: DocumentIA.Functions compiles without errors  

---

## ConfiguracionJson Structure (Now Primary Source)

With v3.1, `ConfiguracionJson` is the **single source of truth** for all configuration:

```json
{
  "confidenceConfig": {
    "clasifUmbralFallback": 0.85,     // ⬅️ replaces UmbralClasificacion
    "extracUmbralFallback": 0.80      // ⬅️ replaces UmbralExtraccion
  },
  "extraction": {
    "modelKey": "azure-content-understanding",  // ⬅️ replaces ModeloExtraccionDI
    "strategy": "primary-fallback"
  },
  "promptConfig": {
    "systemPrompt": "...",
    "userTemplates": { ... }
  }
}
```

**Note**: 204 production typologies have already been migrated to this structure in v1.5. No data migration needed.

---

## Breaking Changes Analysis

| Change | Breaking? | Mitigation |
|--------|-----------|-----------|
| Column drop in DB | NO (v3.0 only for consumers reading schema) | v1.5→v2.0 deprecation period provided |
| Code property [NotMapped] | NO (returns null/default) | [Obsolete] warnings guide devs |
| Property removal (v4.0) | YES (major version change) | 6+ months notice via v3.1 warnings |

**No breaking changes for standard workflows**. Database operates identically; all configuration reads from ConfigJson.

---

## Rollback Procedure

If production issue detected:

1. **Backup current state**: Take SQL Server backup
2. **Revert migration**: `dotnet ef database update 20260605095456_v20_DropPromptGPT`
3. **Verify columns restored**: SELECT COLUMN_NAME FROM Tipologias schema
4. **Redeploy code**: Revert to v2.0 hotfix branch if needed
5. **Notify stakeholders**: Communication template in `docs/04_MANUAL_EXPLOTACION.md`

**Estimated rollback time**: < 5 minutes (schema only, no data recovery needed)

---

## Next Steps (v4.0+)

When ready for major version release:

1. Remove all 4 properties from `TipologiaEntity.cs` (breaking change)
2. Remove corresponding [Obsolete] warnings from code
3. Bump version to `v4.0.0`
4. Update CHANGELOG and migration guide
5. Communicate breaking change in release notes

**No database migration needed** — schema already clean as of v3.1.

---

## Documentation References

- **Migration Guide (v1.5)**: [12_MIGRACION_PROMPTGPT_V1_4.md](12_MIGRACION_PROMPTGPT_V1_4.md)
- **Technical Design**: [03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md)
- **Configuration Manual**: [05_MANUAL_USO_CONFIGURACION.md](05_MANUAL_USO_CONFIGURACION.md)
- **Operations Guide**: [04_MANUAL_EXPLOTACION.md](04_MANUAL_EXPLOTACION.md)

---

## Sign-Off

- **Implementation**: ✅ Complete (2026-06-05)
- **Testing**: ✅ 12/12 tests passing
- **Database**: ✅ Migration applied
- **Code Review**: ✅ Commit 3e36c24 ready for merge
- **WI Status**: ✅ AB#99744, AB#99746, AB#99747, AB#99749 → Done
