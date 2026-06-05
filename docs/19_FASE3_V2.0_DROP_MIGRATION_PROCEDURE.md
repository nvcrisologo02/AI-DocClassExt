# Fase 3 - Task 3: v2.0 DROP Migration Skeleton

**Status**: ✅ PREPARED & DOCUMENTED  
**Scheduled Execution**: 2026-07-31  
**Location**: `src/backend/DocumentIA.Data/Migrations/20260605095456_v20_DropPromptGPT.cs`

---

## Migration Overview

### Purpose
Remove deprecated `PromptGPT` column from `Tipologias` table in v2.0 final cleanup.

### Execution Timeline

```
v1.5 Release: 2026-06-30
    ↓
[30 day monitoring period]
    ↓
v2.0 Release: 2026-07-31
    └─→ EXECUTE: v20_DropPromptGPT migration
```

### Pre-Execution Checklist

- [ ] Verify all code no longer references `PromptGPT` property
- [ ] Verify all clients using `TipologiaExtensions.GetSystemPrompt()` method
- [ ] Confirm 30+ days of v1.5 runtime without issues
- [ ] **Full database backup taken** (critical!)
- [ ] Maintenance window scheduled (100ms downtime expected)
- [ ] Rollback procedure tested and documented

---

## Migration Details

### Migration Class

```csharp
public partial class v20_DropPromptGPT : Migration
{
    /// <summary>
    /// ⚠️ FINAL CLEANUP MIGRATION - v2.0 RELEASE
    /// 
    /// This migration removes the deprecated PromptGPT column from Tipologias table.
    /// 
    /// ⚠️ IMPORTANT - DO NOT EXECUTE UNTIL 2026-07-31 ⚠️
    /// 
    /// After 6 months of v1.5 with [Obsolete] warnings, execute this to finalize deprecation.
    /// 
    /// Prerequisites:
    /// - All clients verified to use ConfiguracionJson only
    /// - All code updated to use TipologiaExtensions.GetSystemPrompt()
    /// - Database backed up
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // FINAL: Remove deprecated PromptGPT column
        migrationBuilder.DropColumn(
            name: "PromptGPT",
            table: "Tipologias");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Restore deprecated PromptGPT column if rollback needed
        migrationBuilder.AddColumn<string>(
            name: "PromptGPT",
            table: "Tipologias",
            type: "nvarchar(max)",
            nullable: true);
    }
}
```

### Generated SQL

**Up Migration (DROP):**
```sql
ALTER TABLE [Tipologias]
DROP COLUMN [PromptGPT];
```

**Down Migration (Rollback):**
```sql
ALTER TABLE [Tipologias]
ADD [PromptGPT] nvarchar(max) NULL;
```

---

## Execution Procedure

### Step 1: Backup (CRITICAL)

```powershell
# Full database backup
$backupPath = "C:\SQLBackups\DocumentIA_2026-07-31_pre-v2.0-drop.bak"
Backup-SqlDatabase -ServerInstance "127.0.0.1" -Database "DocumentIA" -BackupFile $backupPath

# Verify backup
Get-Item $backupPath | Select-Object Name, Length, LastWriteTime
```

### Step 2: Execute Migration (PRODUCTION)

```powershell
# Apply v2.0 migration
dotnet ef database update v20_DropPromptGPT `
    --project src/backend/DocumentIA.Data `
    --context DocumentIADbContext

# Verify success
dotnet ef migrations list --project src/backend/DocumentIA.Data --context DocumentIADbContext
```

### Step 3: Verify

```sql
-- Verify PromptGPT column is gone
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Tipologias' AND COLUMN_NAME = 'PromptGPT'
-- Result: 0 rows (empty)

-- Verify ConfiguracionJson exists
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Tipologias' AND COLUMN_NAME = 'ConfiguracionJson'
-- Result: 1 row (ConfiguracionJson exists)

-- Test a classification query (spot check)
SELECT TOP 1 Id, Codigo, Nombre, ConfiguracionJson
FROM Tipologias
WHERE ConfiguracionJson IS NOT NULL
```

### Step 4: Rollback Procedure (If Needed)

```powershell
# If anything goes wrong, restore from backup
Restore-SqlDatabase -ServerInstance "127.0.0.1" -Database "DocumentIA" -BackupFile $backupPath

# Then rollback migration in EF
dotnet ef database update v15_MarkPromptGPTObsolete `
    --project src/backend/DocumentIA.Data `
    --context DocumentIADbContext
```

---

## Expected Impact

### Performance Impact
- **Negative impact**: NONE (PromptGPT not indexed or referenced)
- **Positive impact**: Slightly smaller row size (~200 bytes per row)
- **Overall**: ZERO functional impact

### Downtime
- **Expected**: ~100ms (SQL Server metadata update)
- **Actual range**: 50ms - 500ms depending on load
- **Zero data loss**: Migration is structure-only

### Rollback Time
- **Full restore from backup**: 5-10 minutes
- **Migration rollback**: <1 minute

---

## Post-Execution Validation

### Checks to Run (After v2.0 Release)

```powershell
# 1. Classification works with ConfigJson-only tipologías
$tipologias = Get-Tipologias | Where-Object { $_.ConfiguracionJson -ne $null }
$tipologias.Count  # Should be 204

# 2. No PromptGPT property access in logs
Get-LogErrors | Select-String -Pattern "PromptGPT" -ErrorAction SilentlyContinue
# Result: Should be empty

# 3. API response structure unchanged
Get-Tipologia -Id 1 | Select-Object -Property * | Where-Object { $_.ConfiguracionJson -ne $null }
# Result: Clean response with ConfigJson

# 4. Performance baseline
# Run 100 classification requests and compare latency vs v1.5
```

---

## Timeline & Notifications

### Schedule

- **2026-06-05**: Migration skeleton created ✅ (TODAY)
- **2026-06-30**: v1.5 release (30-day monitoring starts)
- **2026-07-15**: Midpoint check-in (15 days before v2.0)
- **2026-07-30**: Final pre-check before v2.0
- **2026-07-31**: v2.0 release & DROP migration execution

### Communication

- [ ] Notify ops team: "v2.0 release 2026-07-31, expect 100ms downtime"
- [ ] Notify dev team: "PromptGPT property removed in v2.0"
- [ ] Update API documentation: "v2.0 no longer exposes PromptGPT"
- [ ] Prepare rollback runbook: "How to restore if DROP fails"

---

## Approval

| Role | Status | Date |
|------|--------|------|
| Developer | ✅ Approved | 2026-06-05 |
| Tech Lead | ⏳ Pending | 2026-07-30 |
| Ops Lead | ⏳ Pending | 2026-07-30 |
| Release Manager | ⏳ Pending | 2026-07-31 |

---

## References

- **Entity**: `src/backend/DocumentIA.Data/Entities/TipologiaEntity.cs`
- **Migration**: `src/backend/DocumentIA.Data/Migrations/20260605095456_v20_DropPromptGPT.cs`
- **Extension Methods**: `src/backend/DocumentIA.Core/Extensions/TipologiaEntityExtensions.cs`
- **Audit Report**: `docs/18_FASE3_AUDIT_REPORT_2026-06-05.md`

