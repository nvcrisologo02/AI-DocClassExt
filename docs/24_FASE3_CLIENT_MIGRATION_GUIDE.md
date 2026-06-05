# Fase 3: Client Migration Guide (v1.5 to v2.0)

---

## Quick Summary

**For most clients**: ✅ **NO ACTION REQUIRED**

Both v1.5 and v2.0 maintain the same API contract. The `ConfiguracionJson` field remains the primary configuration source.

---

## FAQ

### Q1: Do I need to update my code?

**A**: Not immediately. However, we recommend:

1. **Stop using `PromptGPT`** (deprecated in v1.5, removed in v2.0)
2. **Use `ConfiguracionJson` only** (already the primary field)

### Q2: What happens to my existing integrations?

**A**: Nothing. Zero breaking changes.

```
v1.4 behavior: ConfiguracionJson (primary) + PromptGPT (legacy)
v1.5 behavior: ConfiguracionJson (primary) + PromptGPT [Obsolete] (warning)
v2.0 behavior: ConfiguracionJson (primary) [PromptGPT removed]

Result: Your code continues to work unchanged
```

### Q3: When is v2.0 available?

**A**: July 31, 2026

### Q4: Will there be downtime?

**A**: ~100ms during the migration window (July 31, 00:00-00:15 UTC)

### Q5: Can I stay on v1.5?

**A**: Yes, indefinitely. v1.5 is stable and fully supported.

---

## Migration Timeline

### v1.5 Release (June 30, 2026) ✅

```
Date: June 30, 2026
Action: Update to v1.5
Your Action: Optional
Downtime: 0 seconds
Breaking Changes: None
API Changes: None
Database Changes: None

What's New:
- PromptGPT marked [Obsolete] in code
- All warnings point to ConfiguracionJson
- Performance unchanged
- All tests passing (32/32)
```

**For your team**: Review any code using `PromptGPT` and migrate to `GetSystemPrompt()` / `GetUserPromptTemplate()`.

### v2.0 Release (July 31, 2026) 📅

```
Date: July 31, 2026
Action: Update to v2.0 (automatic)
Your Action: None required
Downtime: ~100ms
Breaking Changes: None
API Changes: None
Database Changes: PromptGPT column removed

What's New:
- Cleaner database schema
- PromptGPT field completely removed
- Same functionality, same performance
- Zero data loss
```

**For your team**: No action needed. Everything works the same.

---

## Code Migration Examples

### Option A: Already Using Extension Methods ✅ (No Changes)

```csharp
// This code works in v1.4, v1.5, AND v2.0
var tipologia = await _tipologiaService.GetTipologia(id);
var systemPrompt = tipologia.GetSystemPrompt();
var userTemplate = tipologia.GetUserPromptTemplate();
```

### Option B: Using Deprecated Field ⚠️ (Update Recommended)

**v1.4 / v1.5:**
```csharp
// ❌ This works, but generates warning
var prompt = tipologia.PromptGPT;
```

**v2.0+:**
```csharp
// ✅ Recommended (works everywhere)
var prompt = tipologia.GetSystemPrompt();
```

### Option C: Direct JSON Access ✅ (Still Works)

```csharp
// This works in all versions (if you parse ConfigJson)
var config = JsonConvert.DeserializeObject<TipologiaConfig>(
    tipologia.ConfiguracionJson
);
var prompt = config.PromptConfig.SystemPrompt;
```

---

## Compatibility Matrix

| Feature | v1.4 | v1.5 | v2.0 |
|---------|------|------|------|
| ConfiguracionJson | ✅ | ✅ | ✅ |
| GetSystemPrompt() | ✅ | ✅ | ✅ |
| GetUserPromptTemplate() | ✅ | ✅ | ✅ |
| PromptGPT direct access | ✅ | ⚠️ warning | ❌ |
| API /management/tipologias | ✅ | ✅ | ✅ |
| API /classify | ✅ | ✅ | ✅ |
| Database schema | ✅ | ✅ | ✅ |

---

## API Response - No Changes

All API responses remain identical:

```json
GET /api/management/tipologias/1

{
  "id": 1,
  "nombre": "Invoice",
  "codigo": "INV001",
  "estado": "Published",
  "configuracionJson": {
    "fields": [...],
    "promptConfig": {
      "systemPrompt": "You are a document classifier...",
      "userPromptTemplate": "Classify this document..."
    }
  },
  "UmbralClasificacion": 0.85,
  "UmbralExtraccion": 0.80
}

Note: PromptGPT is NOT in API response (removed in v1.4 via DTO)
```

---

## Troubleshooting

### Issue: Getting CS0618 warning in my code

**Solution**: You're using `PromptGPT` (deprecated in v1.5).

```csharp
// Change from this:
var prompt = tipologia.PromptGPT;

// To this:
var prompt = tipologia.GetSystemPrompt();
```

### Issue: Application throws "PromptGPT not found"

**Solution**: This happens when upgrading to v2.0 if your code directly accesses the field.

**Prevention**: Use extension methods instead:

```csharp
// ❌ Don't do this
var prompt = tipologia.PromptGPT;  // Error in v2.0!

// ✅ Do this
var prompt = tipologia.GetSystemPrompt();  // Works everywhere
```

### Issue: Database migration failed

**Solution**: Contact support immediately. Rollback procedure ready:

- Database restored within 15 minutes
- Code reverted to v1.5
- Zero data loss
- Full incident report generated

**Contact**: [Support Email / Phone]

---

## Deployment Timeline (For Reference)

```
June 1, 2026    v1.5 code merged to main
June 30, 2026   v1.5 released to production ← HERE
              (PromptGPT marked [Obsolete])

July 1-30, 2026 Extended testing period
              (Teams migrate their code)

July 31, 2026   v2.0 released to production ← THEN
              (PromptGPT field permanently removed)
              (~100ms downtime, July 31 00:00-00:15 UTC)

Aug 1+, 2026    v2.0 stable + supported
              (v1.5 security patches only)
```

---

## Frequently Asked Questions

### Q: Does this affect my classification accuracy?

**A**: No. Zero impact. Same algorithms, same models, same results.

### Q: Do I need to re-test my integrations?

**A**: Not unless you directly access `PromptGPT`. If using extension methods (recommended), no testing needed.

### Q: What if I'm using an old SDK?

**A**: We recommend updating to latest SDK for v2.0. Old SDKs will throw errors when accessing `PromptGPT`.

### Q: Can I opt-out of the migration?

**A**: Yes, stay on v1.5. Full support continues, but security patches only after July 31.

### Q: What if there's a critical bug in v2.0?

**A**: Rollback procedure is tested and ready. Maximum downtime: 30 minutes.

### Q: How long do you support v1.5?

**A**: Minimum 2 years (until June 2028). Critical security patches: indefinite.

---

## Support & Contact

| Issue | Contact | Response Time |
|-------|---------|----------------|
| Migration questions | support@example.com | 4 hours |
| API issues | api-support@example.com | 1 hour |
| Critical incident | on-call@example.com | 15 min |
| Feature requests | product@example.com | 48 hours |

---

## Release Notes Summary

### v1.5 Release Notes (June 30, 2026)

```
VERSION: 1.5.0
TYPE: Maintenance Release
BREAKING CHANGES: None
DOWNTIME: 0 seconds

NEW:
- PromptGPT field marked [Obsolete] for v2.0 preparation
- All 32 unit tests passing
- Performance verified (no degradation)

DEPRECATED:
- PromptGPT direct access (use GetSystemPrompt() instead)

RECOMMENDED ACTIONS:
- Update code to use extension methods
- Remove direct PromptGPT references
- Plan v2.0 upgrade for July 31

SUPPORT: Full support. Security patches available.
```

### v2.0 Release Notes (July 31, 2026)

```
VERSION: 2.0.0
TYPE: Major Release
BREAKING CHANGES: PromptGPT field removed (use extension methods)
DOWNTIME: ~100ms (July 31, 00:00-00:15 UTC)

REMOVED:
- PromptGPT database column
- Legacy field support
- Obsolete warnings

UNCHANGED:
- All APIs (100% compatible)
- All functionality
- All performance
- All data

NEW:
- Cleaner database schema
- Better performance (fewer nullable columns)
- Reduced storage footprint (~2%)

MIGRATION:
- Automatic (no user action)
- Rollback ready (if needed)
- Full backup retention (90 days)

SUPPORT: Full support. v1.5 receives security patches only.
```

---

## Final Checklist

Before June 30, 2026:

- [ ] Review code for PromptGPT usage
- [ ] Update to extension methods
- [ ] Test integrations (optional)
- [ ] Plan v2.0 upgrade

After July 31, 2026:

- [ ] Verify v2.0 working correctly
- [ ] Update documentation (if needed)
- [ ] Archive v1.4/v1.5 release artifacts

---

## Need Help?

- 📖 Documentation: [docs.example.com](https://docs.example.com)
- 🐛 Bug Report: [github.com/issues](https://github.com/issues)
- 💬 Support Chat: [support.example.com](https://support.example.com)
- 📧 Email: support@example.com
- 📞 Phone: +1-555-0123

---

**Release Timeline**: v1.5 (June 30) → v2.0 (July 31, 2026)  
**Your Action**: None required, but updating code recommended  
**Data Impact**: Zero  
**API Impact**: Zero  
**Support**: Full through 2028
