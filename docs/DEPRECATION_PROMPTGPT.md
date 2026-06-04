# ⚠️ DEPRECATION NOTICE: PromptGPT Field

**Status:** DEPRECATED  
**Effective Version:** v1.4.0  
**Removal Target:** v2.0.0 (Q3 2026)  
**Migration Path:** ConfiguracionJson → PromptConfig

---

## Summary

The `PromptGPT` field in the `Tipologias` table is **deprecated** as of v1.4.0.

All prompt configuration must be managed exclusively through `ConfiguracionJson.PromptConfig`:
```json
{
  "promptConfig": {
    "systemPrompt": "string",
    "userPromptTemplate": "string"
  }
}
```

---

## Why This Change?

**Problem:**
- `PromptGPT` duplicates prompt configuration already stored in `ConfiguracionJson`
- Creates data inconsistency: prompt defined in BOTH places
- Increases schema complexity (+1 field = +1 serialization path)
- Complicates migrations and version upgrades

**Solution:**
- **Single source of truth:** `ConfiguracionJson` only
- **Cleaner schema:** Remove redundant columns
- **Simplified logic:** Consistent data access patterns
- **Better performance:** One JSON parse, not multiple field loads

---

## Migration Timeline

### Phase 1: Deprecation Notice (v1.4 - Current)
- `PromptGPT` still readable and writable
- **New code should use `ConfiguracionJson.PromptConfig` only**
- API returns deprecation warning in response headers
- Validation detects inconsistencies

### Phase 2: Data Migration (v1.5 - June 30, 2026)
- SQL scripts consolidate `PromptGPT` → `ConfiguracionJson.PromptConfig`
- Frontend wizard updated to use JSON only
- `PromptGPT` field becomes read-only (legacy support)

### Phase 3: Column Removal (v2.0 - July 31, 2026) ⚠️ BREAKING
- `PromptGPT` column dropped from database
- EF migration applied
- Requires database backup before upgrade
- **NOT COMPATIBLE with v1.x deployments**

---

## For API Consumers

### Current (v1.4 - Still Supported)
```json
GET /api/admin/tipologias/1

{
  "id": 1,
  "codigo": "tasacion",
  "promptGPT": "You are a...",  // ⚠️ DEPRECATED
  "configuracionJson": {
    "promptConfig": {
      "systemPrompt": "You are a...",
      "userPromptTemplate": "..."
    }
  }
}
```

### Recommended (v1.4+)
```json
{
  "id": 1,
  "codigo": "tasacion",
  "configuracionJson": {
    "promptConfig": {
      "systemPrompt": "You are a...",
      "userPromptTemplate": "..."
    }
  }
}
```

**Note:** Future versions will not include `promptGPT` field.

---

## For Backend Developers

### ❌ DO NOT USE (Deprecated)
```csharp
var prompt = tipologia.PromptGPT;  // ❌ DEPRECATED

// UPDATE operations
tipologia.PromptGPT = "new prompt";  // ❌ DEPRECATED
await db.SaveChangesAsync();
```

### ✅ USE INSTEAD (ConfiguracionJson)
```csharp
var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(
    tipologia.ConfiguracionJson!,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
);

var prompt = config?.PromptConfig?.SystemPrompt;  // ✅ CORRECT

// UPDATE operations
config.PromptConfig = new PromptConfig
{
    SystemPrompt = "new prompt",
    UserPromptTemplate = "..."
};

tipologia.ConfiguracionJson = JsonSerializer.Serialize(config);
await db.SaveChangesAsync();
```

---

## For Frontend Developers

### ❌ DO NOT USE (Deprecated)
```csharp
// In TipologiaWizardStateService
Draft.PromptGPT = source.PromptGPT;  // ❌ DEPRECATED
```

### ✅ USE INSTEAD (ConfiguracionJson)
```csharp
// Parse ConfiguracionJson
var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(
    source.ConfiguracionJson ?? "{}",
    JsonOptions
);

Draft.PromptSystemPrompt = config?.PromptConfig?.SystemPrompt ?? "";
Draft.PromptUserTemplate = config?.PromptConfig?.UserPromptTemplate ?? "";
```

---

## Validation: Inconsistency Detector

Starting v1.4, a validator checks for inconsistencies between `PromptGPT` and `ConfiguracionJson.PromptConfig`:

```
⚠️ WARNING: Tipologia 'tasacion' has inconsistent prompt configuration:
  - PromptGPT in table:      "You are a real estate appraiser..."
  - ConfiguracionJson prompt: "You are a financial analyst..."
  
  Resolve by running: TipologiaValidator.SyncPromptToJson(tipologiaId)
```

---

## FAQ

### Q: Can I still use PromptGPT in v1.4?
**A:** Yes, but it's not recommended. Use `ConfiguracionJson.PromptConfig` instead.

### Q: What happens if I upgrade to v2.0 without migrating?
**A:** **BREAKING CHANGE.** The `PromptGPT` column is deleted. All prompts must be in `ConfiguracionJson`.

### Q: How do I migrate existing prompts?
**A:** See `docs/auxiliares/2026-06-04/VISUAL-SUMMARY.md` for migration scripts. Or wait for v1.5 automated migration.

### Q: Can I keep using the old schema?
**A:** No. v2.0 removes the column entirely. Plan migration now.

### Q: Which version should I target?
**A:** **Immediately:** Use `ConfiguracionJson` for new code  
**Before July 31:** Ensure all `PromptGPT` usage is migrated  
**After Aug 1:** Update to v2.0

---

## Timeline Summary

| Date | Version | Status | Action |
|------|---------|--------|--------|
| Jun 4 | v1.4.0 | 🟡 Deprecated | Start using ConfiguracionJson |
| Jun 30 | v1.5.0 | 🟡 Transitional | Automated migration available |
| Jul 31 | v2.0.0 | 🔴 Removed | **BREAKING - PromptGPT column deleted** |

---

## Resources

- **Full Migration Guide:** [docs/auxiliares/2026-06-04/auditoria-promptgpt-completa.md](../auxiliares/2026-06-04/auditoria-promptgpt-completa.md)
- **Technical References:** [docs/auxiliares/2026-06-04/referencias-promptgpt-tecnicas.md](../auxiliares/2026-06-04/referencias-promptgpt-tecnicas.md)
- **Visual Summary:** [docs/auxiliares/2026-06-04/VISUAL-SUMMARY.md](../auxiliares/2026-06-04/VISUAL-SUMMARY.md)
- **Work Item:** [AB#99732 - Epic](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/99732)

---

## Questions?

Contact: Backend Architecture Team  
Slack: #tipologias-cleanup  
Email: dev-team@company.com

---

**Last Updated:** 2026-06-04  
**Next Review:** 2026-06-15
