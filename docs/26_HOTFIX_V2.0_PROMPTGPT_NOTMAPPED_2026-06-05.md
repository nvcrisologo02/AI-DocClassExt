# Fix: v2.0 Migration - EF Core Mapping Error

**Date:** 2026-06-05  
**Issue:** After v2.0 migration (DROP PromptGPT column), Azure Functions failed with:
```
Microsoft.Data.SqlClient.SqlException (0x80131904): Invalid column name 'PromptGPT'
```

**Root Cause:** EF Core tried to load `PromptGPT` column from database, but column was already dropped in v2.0 migration.

---

## Solution Applied

**Changed:** [TipologiaEntity.cs](src/backend/DocumentIA.Data/Entities/TipologiaEntity.cs)

```csharp
// BEFORE (caused SQL error):
[Obsolete("Use ConfiguracionJson.promptConfig instead (v1.4+). Removed in v2.0.", false)]
[Column(TypeName = "nvarchar(max)")]
public string? PromptGPT { get; set; }

// AFTER (fixed - now marked as [NotMapped]):
[Obsolete("Use ConfiguracionJson.promptConfig instead (v1.4+). Removed in v2.0.", false)]
[NotMapped]
public string? PromptGPT { get; set; }
```

**Effect:**
- ✅ EF Core no longer attempts to load PromptGPT from database
- ✅ Code can still reference PromptGPT (but marked [Obsolete])
- ✅ No SQL errors when loading Tipologías
- ✅ Property returns null/default when accessed (no DB call)

---

## Verification

**Tests:** 12/12 Deprecation Tests Passing ✅

```
Correctas! - Con error: 0, Superado: 12, Omitido: 0, Total: 12, Duración: 2 s
```

**Build:** DocumentIA.Functions - 0 Errors, 39 Warnings (obsolescence only) ✅

---

## Next Steps

1. **Restart Azure Functions host** - Should now start without "Invalid column name 'PromptGPT'" error
2. **Run smoke tests** - Verify classification/extraction workflows work
3. **Monitor Application Insights** - Check for any residual errors

---

## Why [NotMapped] is the Right Solution

| Approach | Pros | Cons |
|----------|------|------|
| **[NotMapped]** ✅ | Preserves property for backward compat, clean, tested | None |
| Remove entirely | Clearest signal | Breaks code referencing PromptGPT (even [Obsolete]) |
| `modelBuilder.Ignore()` | Also works | Less discoverable than attribute |
| Leave as-is | Shortest change | Causes SQL errors (was the problem) |

**Decision:** [NotMapped] balances safety, backward compatibility, and clarity.

---

## Related Changes

- **Migration:** [20260605095456_v20_DropPromptGPT.cs](src/backend/DocumentIA.Data/Migrations/20260605095456_v20_DropPromptGPT.cs) - Dropped column from BD
- **Entity:** [TipologiaEntity.cs](src/backend/DocumentIA.Data/Entities/TipologiaEntity.cs) - Marked property as [NotMapped]
- **Tests:** 12 deprecation tests confirm property is still referenceable but not mapped

---

## Troubleshooting

If Azure Functions still fails to start:

1. **Verify [NotMapped] attribute applied:**
   ```csharp
   grep -A2 "Obsolete.*PromptGPT" src/backend/DocumentIA.Data/Entities/TipologiaEntity.cs
   ```

2. **Rebuild and restart:**
   ```powershell
   dotnet clean
   dotnet build --configuration Release
   # Restart Functions host
   ```

3. **Check for other PromptGPT column references:**
   ```powershell
   Get-ChildItem -Recurse -Include "*.cs" | Select-String "\.PromptGPT\s*=" | Where-Object { $_ -notmatch "Obsolete|#pragma" }
   ```

---

**Status:** ✅ FIXED - Azure Functions ready to restart.
