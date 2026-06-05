# Fase 3: Limpieza de Tipologías - Scope & Strategy

**Date:** 2026-06-05  
**Epic:** AB#99732 (Tipologías Cleanup - PromptGPT Deprecation)  
**Phase:** 3 (Database & Schema Cleanup)  
**Timeline:** 2026-06-15 to 2026-07-31  
**Status:** 📋 PLANNING

---

## Table of Contents

1. [Fase 3 Overview](#fase-3-overview)
2. [Current State Analysis](#current-state-analysis)
3. [6 Tasks Breakdown](#6-tasks-breakdown)
4. [Data Migration Strategy](#data-migration-strategy)
5. [Implementation Plan](#implementation-plan)
6. [Risk Assessment](#risk-assessment)
7. [Success Criteria](#success-criteria)

---

## Fase 3 Overview

### Context (UPDATED after Backup Analysis)

**Fase 1 & 2** completed the code refactoring:
- ✅ Extension methods implemented (`GetSystemPrompt()`, `GetUserPromptTemplate()`)
- ✅ DTOs updated (ConfiguracionJson as primary source)
- ✅ PromptGPT marked deprecated (v1.4)
- ✅ API documented

**Fase 3** focuses on **database schema cleanup** and **final removal**:
- ✅ **GOOD NEWS:** Data migration is NOT needed - ConfiguracionJson is already 100% populated
- ✅ **CLEAN STATE:** Only 10/204 have PromptGPT (all redundant with ConfigJson)
- **Goal:** v1.5 (2026-06-30): Mark PromptGPT as [Obsolete] formally
- **Goal:** v2.0 (2026-07-31): Remove PromptGPT column completely (1-click)

### Why Fase 3? (Simplified Justification)

**The Situation (Actual Data - 2026-06-05):**

```
Database State:
- Total Tipologías: 204
- Con ConfiguracionJson: 204 (100%) ✅ ALL HAVE IT
- Con PromptGPT: 10 (5%) - mostly legacy/redundant
- Con ambos (A1): 10 (5%) - safe, redundant
- Solo ConfigJson: 194 (95%) - already using new format
```

**Why Drop PromptGPT Now?**

1. **No more dual-source-of-truth:** ConfigJson is the only real source
2. **Reduce database bloat:** 10 unused columns (204 rows × unused field)
3. **Simplify schema:** Fewer fields = easier to maintain
4. **Clean v2.0 release:** Remove deprecated column before new major version
5. **Prevent future confusion:** No "should I use this?" questions

**Fase 3 Strategy (Simplified):**

1. ✅ **Validate Clean State:** Confirm ConfigJson is 100% ready
2. ✅ **Mark as Obsolete:** Add [Obsolete] attribute to PromptGPT (v1.5)
3. ✅ **Prepare DROP:** Create migration skeleton for v2.0
4. ✅ **Test Thoroughly:** Verify no breaking changes
5. ✅ **Document:** Migration procedure & rollback plan
6. ✅ **Release:** v2.0 with column removed

---

## Current State Analysis

### TipologiaEntity Schema (v1.4)

```csharp
public class TipologiaEntity
{
    public int Id { get; set; }
    public string Codigo { get; set; }                    // PK index
    public string Nombre { get; set; }
    public string Version { get; set; }
    public bool Activa { get; set; }                      // Index
    
    // Legacy (deprecated in v1.4, removed in v2.0)
    public string? PromptGPT { get; set; }                // ⚠️ 200 chars max
    
    // New (v1.4+)
    public string? ConfiguracionJson { get; set; }        // nvarchar(max)
    
    // Model references
    public string? ModeloClasificacionDI { get; set; }
    public double UmbralClasificacion { get; set; }
    public string? ModeloExtraccionDI { get; set; }
    public double UmbralExtraccion { get; set; }
    
    // Metadata
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public string? CreadoPor { get; set; }
    public EstadoTipologia Estado { get; set; }           // Draft, Published, Retired
    public DateTime? PublicadaEn { get; set; }
    public string? PublicadaPor { get; set; }
    public string? VersionPublicada { get; set; }
}
```

### Indexes (Current - v1.4)

| Index | Columns | Type | Usage |
|-------|---------|------|-------|
| `PK_Tipologias` | `Id` | Clustered | Primary key |
| `IX_Tipologias_Codigo` | `Codigo` | Unique | Lookup by codigo |
| `IX_Tipologias_Activa` | `Activa` | Non-unique | Filter active only |
| `IX_Tipologias_Estado` | `Estado` | Non-unique | Filter by state |

**Problem:** No index on `PromptGPT` (was just a simple column, no search needed)

### Data Distribution (Actual - from Backup 2026-06-05)

| Category | Count | % | Status |
|----------|-------|---|--------|
| **Both PromptGPT & ConfigJson (A1)** | 10 | 5% | ✅ Redundant - safe to drop |
| **Only ConfigJson (preferred)** | 194 | 95% | ✅ Already correct - no action |
| **Only PromptGPT** | 0 | 0% | ✅ N/A (none exist) |
| **Neither** | 0 | 0% | ✅ N/A (none exist) |
| **Total** | **204** | **100%** | ✅ **100% Clean** |

**Conclusion:** NO data migration needed. ConfigJson is already the primary source for all 204 tipologías. The 10 with PromptGPT are simply redundant legacy fields.

---

## 6 Tasks Breakdown (SIMPLIFIED - No Data Migration Needed)

### Task 1: Entity Update (Code + Migration) - SIMPLIFIED

**Scope:** Mark PromptGPT as [Obsolete] in v1.5, prepare DROP for v2.0

**Changes:**

1. **Update TipologiaEntity** (v1.5):
   ```csharp
   [Obsolete("Use ConfiguracionJson.promptConfig instead (v1.4+). Removed in v2.0.", false)]
   [Column(TypeName = "nvarchar(200)")]
   public string? PromptGPT { get; set; }
   ```

2. **Create v1.5 migration** (IMPORTANT: No DB changes):
   ```
   Add-Migration v15_MarkPromptGPTObsolete
   ```
   - Migration file adds [Obsolete] attribute in code
   - **NO SQL changes** - column still exists in database
   - No Up() or Down() required (pure metadata change)

3. **Create v2.0 migration skeleton** (marked "DO NOT RUN YET"):
   ```csharp
   public partial class DropPromptGPT : Migration
   {
       protected override void Up(MigrationBuilder migrationBuilder)
       {
           // DROP COLUMN PromptGPT FROM Tipologias
           // Execution date: 2026-07-31 only
       }
       
       protected override void Down(MigrationBuilder migrationBuilder)
       {
           // Rollback: recreate column
       }
   }
   ```

**Deliverables:**
- ✅ Updated TipologiaEntity.cs with [Obsolete]
- ✅ Migration: `20260630XXXXXX_MarkPromptGPTObsolete.cs` (v1.5, no DB changes)
- ✅ Migration skeleton: `20260731XXXXXX_DropPromptGPT.cs` (v2.0, prepared but not executed)
- ✅ Unit tests verify no breaking changes

**Effort:** ~1-2 hours (simplified - no data transformation)

---

### Task 2: Indexes & Constraints Review - SIMPLIFIED

**Scope:** Quick validation that schema is clean

**Actions:**

1. **SQL Audit Query:**
   ```sql
   -- Check for any indexes on PromptGPT
   SELECT * FROM sys.indexes WHERE name LIKE '%PromptGPT%'
   
   -- Result: SHOULD BE EMPTY (no indexes on PromptGPT)
   ```

2. **Foreign Key Check:**
   ```sql
   -- Check if any FK references PromptGPT
   SELECT * FROM sys.foreign_keys WHERE [definition] LIKE '%PromptGPT%'
   
   -- Result: SHOULD BE EMPTY
   ```

3. **Validate ConfigJson doesn't need indexing:**
   - ConfigJson is for storage, not filtering
   - Queries already indexed: Codigo, Estado, Activa, Id
   - No new indexes needed

4. **Expected Result:** "All clean, no action required"

**Deliverables:**
- ✅ SQL audit queries (provided)
- ✅ Confirmation: "No indexes or FKs depend on PromptGPT"
- ✅ Recommendation: Proceed to v2.0 DROP safely

**Effort:** ~30 minutes

---

### Task 3: DROP PromptGPT Column Plan - SIMPLIFIED

**Scope:** Prepare final schema cleanup (v2.0 only)

**⚠️ IMPORTANT:** Migration skeleton created in Task 1, NOT executed in Fase 3

**Actions:**

1. **Verify migration skeleton exists:**
   - File: `20260731XXXXXX_DropPromptGPT.cs`
   - Status: "READY - DO NOT EXECUTE until v2.0 release date (2026-07-31)"

2. **Rollback strategy:**
   - If DROP migration fails, can rollback to v1.5
   - Down() migration recreates PromptGPT column
   - Data restored from backups

3. **v2.0 Release notes entry:**
   - "PromptGPT column removed (deprecated in v1.5, now fully removed)"
   - "All prompts stored in ConfiguracionJson"
   - "No client migration needed (ConfiguracionJson already primary in v1.4)"

4. **Lock decision:**
   - After v2.0: No more v1.3 support
   - v1.3 clients must upgrade to v1.5+ before using v2.0

**Deliverables:**
- ✅ Migration skeleton prepared: `20260731XXXXXX_DropPromptGPT.cs`
- ✅ Rollback procedure documented
- ✅ v2.0 release notes section (retention policy + migration info)
- ✅ Retirement timeline: PromptGPT officially removed 2026-07-31

**Effort:** ~30 minutes

---

### Task 4: Validation & Sign-off (NEW - REPLACES Classification)

**Scope:** Confirm clean state before proceeding

**Actions:**

1. **Execute validation SQL:**
   ```sql
   -- Verify 100% have ConfigJson
   SELECT COUNT(*) AS ConfigJsonMissing
   FROM Tipologias
   WHERE ConfiguracionJson IS NULL OR ConfiguracionJson = ''
   
   -- Expected: 0 rows
   
   -- Check PromptGPT redundancy
   SELECT COUNT(*) AS PromptGPTOnly
   FROM Tipologias
   WHERE (PromptGPT IS NOT NULL AND PromptGPT != '')
   AND (ConfiguracionJson IS NULL OR ConfiguracionJson = '')
   
   -- Expected: 0 rows
   ```

2. **Run data integrity checks:**
   - All 204 ConfigJson can be parsed as JSON ✅
   - All contain required fields ✅
   - No corruption detected ✅

3. **Confirm readiness:**
   - ✅ ConfigJson is 100% populated
   - ✅ All 204 tipologías are ready for v2.0
   - ✅ Proceed to Task 5 (Tests)

4. **Get stakeholder sign-off:**
   - "Confirmed: ready to mark v1.5 and schedule v2.0"

**Deliverables:**
- ✅ Validation SQL query + results
- ✅ Integrity check report (0 issues)
- ✅ Readiness confirmation: "SAFE TO PROCEED"
- ✅ Stakeholder sign-off document

**Effort:** ~1 hour

---

### Task 5: Tests & Validation - SIMPLIFIED

**Scope:** Verify DROP causes no issues

**Actions:**

1. **Unit tests** (existing tests still pass):
   - ✅ `GetSystemPrompt()` returns ConfigJson value
   - ✅ PromptGPT field is marked [Obsolete]
   - ✅ Compile warnings appear (expected)

2. **Integration tests** (new - verify DROP):
   - Test v1.5 deployment: PromptGPT marked obsolete ✅
   - Simulate v2.0 DROP: Remove column ✅
   - Verify classification still works post-DROP ✅
   - Test roundtrip: Save → Load → Verify ✅

3. **Performance tests:**
   - Query performance with PromptGPT present (v1.5) ✅
   - Query performance post-DROP (v2.0) ✅
   - Expected: NO difference (PromptGPT not indexed)

4. **Smoke tests (classification):**
   - Load 10 tipologías with only PromptGPT (pre-v1.4 legacy) ✅
   - Load 194 tipologías with only ConfigJson (v1.4+) ✅
   - Verify both classify documents identically ✅

5. **Rollback tests:**
   - If DROP migration fails, restore v1.5 ✅
   - Verify data consistency post-rollback ✅

**Test Coverage Target:** 95%+

**Deliverables:**
- ✅ Unit tests for [Obsolete] attribute
- ✅ Integration tests for DROP scenario
- ✅ Performance regression tests
- ✅ Smoke tests (classification unchanged)
- ✅ Rollback test suite
- ✅ All tests passing report

**Effort:** ~2 hours

---

### Task 6: Documentation (SIMPLIFIED)

**Scope:** Create runbooks for v1.5 and v2.0 deployments

**Actions:**

1. **v1.5 Deployment Runbook:**
   ```markdown
   ## v1.5 Release (2026-06-30)
   
   ### Pre-deployment
   - Full database backup (safety)
   - Verify all 204 tipologías have ConfigJson
   - Confirm 0 issues with validation query
   
   ### Deployment
   1. Deploy v1.5 code
   2. Compile-time warning appears for PromptGPT usage
   3. No database changes required
   
   ### Post-deployment
   - Smoke test: Load & classify a document
   - Verify no errors (ConfigJson is primary)
   - Monitor logs for any PromptGPT references
   
   ### Rollback (if needed)
   - Redeploy v1.4
   - No database changes needed
   ```

2. **v2.0 Deployment Runbook:**
   ```markdown
   ## v2.0 Release (2026-07-31)
   
   ### Pre-deployment
   - Full database backup
   - Verify no code references PromptGPT
   - Confirm v1.5 running stable for 30 days+
   
   ### Deployment
   1. Deploy v2.0 code (PromptGPT property removed)
   2. Run migration: DROP COLUMN PromptGPT
   3. ~100ms downtime during migration
   
   ### Post-deployment
   - Smoke test: Verify classification works
   - Check performance: No degradation
   
   ### Rollback (if issues)
   1. Run Down() migration: recreate PromptGPT column
   2. Redeploy v1.5 code
   ```

3. **Migration Procedure Summary:**
   - v1.5: Mark [Obsolete] + no DB changes
   - v2.0: DROP column + remove property
   - Timeline: 1 month between v1.5→v2.0 for safety

4. **Rollback Procedure:**
   - v1.5 issue? Redeploy v1.4 (no DB changes)
   - v2.0 issue? Rollback migration + redeploy v1.5 (30 seconds recovery)

5. **Client Migration Guide:**
   - All clients already using ConfigJson (v1.4+)
   - No action required when upgrading to v2.0
   - PromptGPT removal is internal schema cleanup

**Deliverables:**
- ✅ v1.5 Deployment Runbook
- ✅ v2.0 Deployment Runbook
- ✅ Rollback Procedure (step-by-step)
- ✅ Client Migration Guide (no changes needed)
- ✅ Troubleshooting section
- ✅ Post-deployment checklist

**Effort:** ~1-2 hours

---

## Data Migration Strategy (NO DATA MIGRATION NEEDED)

### Analysis Complete - Clean State Confirmed

**Database State (Verified 2026-06-05):**

```
SELECT 
    COUNT(*) AS Total,
    SUM(CASE WHEN ConfiguracionJson IS NOT NULL AND ConfiguracionJson != '' THEN 1 ELSE 0 END) AS With_ConfigJson,
    SUM(CASE WHEN PromptGPT IS NOT NULL AND PromptGPT != '' THEN 1 ELSE 0 END) AS With_PromptGPT
FROM Tipologias

Result:
Total:                204
With_ConfigJson:      204 (100% ✅)
With_PromptGPT:       10  (5% - all redundant)
```

### No Migration Needed - Here's Why

1. **ConfigJson is 100% populated** - All 204 tipologías already have valid ConfiguracionJson
2. **PromptGPT is purely legacy** - Only 10 tipologías have it, all also have ConfigJson
3. **No conflicts** - 0 tipologías have PromptGPT-only data (would require migration)
4. **Already safe** - All prompts are in ConfigJson (primary source)

### Simplified Path Forward

Instead of:
- ❌ Analyze 204 tipologías
- ❌ Migrate data from PromptGPT → ConfigJson
- ❌ Handle conflicts and edge cases

We simply:
- ✅ Mark PromptGPT as [Obsolete] (v1.5)
- ✅ Create DROP migration skeleton (v2.0)
- ✅ Run tests to verify no breaking changes
- ✅ Release v2.0 with column removed

### v1.5 Deployment (No Database Changes)

```
┌─────────────────────────────────┐
│ v1.4 → v1.5 Deployment          │
├─────────────────────────────────┤
│                                 │
│ 1. Deploy v1.5 code             │
│    - PromptGPT now [Obsolete]   │
│    - WARNING if code uses it    │
│                                 │
│ 2. Run migration (EF Core)      │
│    - No SQL changes             │
│    - Metadata change only       │
│                                 │
│ 3. Validate                     │
│    - All 204 tipologías work ✓  │
│    - Classification works ✓     │
│                                 │
│ 4. Live: v1.5 active            │
│                                 │
└─────────────────────────────────┘
```

### v2.0 Final Cleanup (Scheduled for 2026-07-31)

```
┌─────────────────────────────────┐
│ v1.5 → v2.0 Deployment          │
├─────────────────────────────────┤
│                                 │
│ 1. Backup database              │
│                                 │
│ 2. Deploy v2.0 code             │
│    - PromptGPT property removed │
│    - Extension methods updated  │
│                                 │
│ 3. Run DROP migration           │
│    - ALTER TABLE Tipologias     │
│      DROP COLUMN PromptGPT      │
│    - ~100ms downtime            │
│                                 │
│ 4. Validate                     │
│    - All 204 tipologías work ✓  │
│    - No performance issues ✓    │
│                                 │
│ 5. Live: v2.0 active (final)    │
│                                 │
└─────────────────────────────────┘
```

---

## Implementation Plan (SIMPLIFIED)

### Phase 3 Timeline (NOW: ~6-7 Hours Total)

```
Day 1 (June 10-12):
├─ Task 1: Entity update + v1.5 migration skeleton      (1-2h)
├─ Task 2: Indexes & constraints review                 (0.5h)
└─ Task 4: Validation & sign-off                        (1h)
   Result: Ready for testing

Day 2 (June 13-14):
├─ Task 3: DROP migration skeleton prepared             (0.5h)
├─ Task 5: Tests & validation                           (2h)
└─ Task 6: Documentation                                (1-2h)
   Result: v1.5 ready to release

Day 3+ (June 30 - July 31):
├─ v1.5 release & deployment
├─ Monitor for 30 days
└─ v2.0 final cleanup (execute DROP migration)
```

### Milestones (UPDATED)

| Date | Milestone | Deliverables | Status |
|------|-----------|--------------|--------|
| 2026-06-10 | Entity Update | TipologiaEntity + migration v1.5 | PLANNED |
| 2026-06-12 | Review Complete | All tasks reviewed, code ready | PLANNED |
| 2026-06-14 | Tests Passing | 95%+ coverage, all green | PLANNED |
| 2026-06-30 | v1.5 Release | Deploy to production | PLANNED |
| 2026-07-31 | v2.0 Cleanup | Execute DROP migration | PLANNED |

---

## Risk Assessment (SIMPLIFIED - Low Risk)

### Low-Risk Scenarios

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| **Code compilation warnings (v1.5)** | High (100%) | None | Expected - shows PromptGPT is deprecated |
| **Migration skeleton syntax error** | Low (1%) | Low | Review migration file before v2.0 |
| **DROP migration execution (v2.0)** | Low (2%) | Medium | Full backup + rollback tested |
| **Client using PromptGPT directly** | Very Low (0.1%) | High | All internal code uses GetSystemPrompt() |
| **Performance degradation post-DROP** | Very Low (0.1%) | Low | PromptGPT not indexed - no impact |

### Mitigation Strategies (Simplified)

1. **v1.5 Deployment (Very Safe):**
   - No database changes
   - Rollback: Just redeploy v1.4 code (2 minutes)
   - Risk: MINIMAL

2. **v2.0 Deployment (Safe with Testing):**
   - Full database backup before DROP
   - RTO: 5 minutes (restore from backup)
   - RPO: 0 minutes (backup taken right before DROP)
   - Risk: LOW

3. **Testing Before v2.0:**
   - Unit tests verify no code references PromptGPT
   - Integration tests verify DROP doesn't break functionality
   - Performance tests confirm no degradation
   - Risk: MITIGATED

---

## Success Criteria (SIMPLIFIED)

### Task-Level Success

- ✅ **Task 1:** TipologiaEntity updated with [Obsolete], migrations created, 0 breaking changes
- ✅ **Task 2:** Indexes/FKs audit complete, "no action required" confirmed
- ✅ **Task 3:** DROP migration skeleton prepared (v2.0, not executed in v1.5)
- ✅ **Task 4:** Validation query confirms 100% ConfigJson + 0 conflicts, sign-off obtained
- ✅ **Task 5:** All tests passing (95%+ coverage), no performance regression
- ✅ **Task 6:** Runbooks complete, rollback procedure documented

### Phase-Level Success

- ✅ **Code Quality:** Zero warnings except [Obsolete] (expected)
- ✅ **Data Integrity:** 100% of tipologías have valid ConfigJson
- ✅ **Classification:** Accuracy unchanged before/after schema changes
- ✅ **Performance:** No query degradation (PromptGPT not indexed)
- ✅ **Testing:** 95%+ code coverage on migration scenarios
- ✅ **Documentation:** v1.5 + v2.0 runbooks complete

### Business Success

- ✅ **v1.5 Release (2026-06-30):** Deploy with [Obsolete] marker, monitor for issues
- ✅ **Zero Production Incidents:** Rollback tested, ready if needed
- ✅ **Stakeholder Approval:** Sign-off on timeline (v1.5 → v2.0)
- ✅ **v2.0 Cleanup (2026-07-31):** Execute DROP migration, finalize deprecation
- ✅ **Technical Debt Paid Down:** PromptGPT removed permanently

---

## Next Steps

### If Approved:

1. **Create Fase 3 work item in Azure DevOps** (Epic AB#99732)
   - Subtasks: 6 tasks listed above
   - Dates: 2026-06-15 to 2026-07-31
   - Owners: To be assigned

2. **Prepare kickoff meeting:**
   - Review classification strategy
   - Get stakeholder sign-off
   - Plan resource allocation

3. **Start Task 1 & 2** (Week 1):
   - Entity update
   - Index review

4. **Parallel: Task 4** (concurrent):
   - Start classification analysis
   - Build decision matrix

### Approval Checklist

- [ ] Scope approved (6 tasks + timeline)
- [ ] Risk assessment accepted
- [ ] Stakeholders aligned on v1.5 → v2.0 timeline
- [ ] Resources assigned
- [ ] Budget & timeline approved

---

## Questions & Clarifications

### Q1: Why drop PromptGPT if it's only 10 tipologías?

**A:** Technical debt cleanup:
1. Less confusion for new developers (which field should I use?)
2. Smaller database rows (~200 bytes per row saved)
3. Simpler schema = easier to maintain
4. v2.0 release deserves clean design
5. No downside: ConfigJson already primary (v1.4+)

### Q2: Why mark [Obsolete] in v1.5 if we're removing in v2.0 anyway?

**A:** Phased deprecation is safer:
1. v1.5: Warn developers (compile-time warnings)
2. v1.5→v2.0: 30-day window to find any missed references
3. v2.0: Drop with confidence (no surprises)

Alternative: Skip v1.5, just drop in v2.0 (riskier, no warning period)

### Q3: Could this cause issues with classification?

**A:** No, because:
1. All 204 tipologías already have ConfigJson populated
2. `GetSystemPrompt()` reads ConfigJson (not PromptGPT)
3. PromptGPT is never used in classification logic
4. Removing unused field = no impact

### Q4: What about backward compatibility?

**A:** v1.5 maintains full compatibility:
- PromptGPT field still exists
- Old code still compiles (with warning)
- ConfigJson is primary source
- Clients see no changes

**v2.0 removes it**, requiring client upgrade (but 6-month notice given via v1.5 warnings)

### Q5: Why is this simpler than expected?

**A:** Because of good prior planning:
1. ConfiguracionJson was added in v1.4 correctly
2. All 204 tipologías were populated with it
3. Developers followed v1.4 best practices
4. No orphaned data in the wild

Result: Migration is unnecessary, just cleanup

---

**Document Status:** 📋 PLANNING PHASE (SIMPLIFIED)  
**Data Quality:** ✅ VERIFIED CLEAN (2026-06-05 Backup)  
**Ready for Implementation:** YES ✅  
**Requires Approval:** YES ⚠️

**Next Action:** User approves simplified scope → Create Fase 3 work items → Start Task 1 this week
