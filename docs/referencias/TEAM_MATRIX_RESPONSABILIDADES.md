# Team Matrix & Responsabilidades — DocumentIA

## 1. RACI MATRIX

Who is **R**esponsible, **A**ccountable, **C**onsults, **I**nformed:

### Componentes Principales

| Componente | Responsible | Accountable | Consults | Informed |
|-----------|-------------|-------------|----------|----------|
| **Azure Functions Code** | Dev-Backend | Tech Lead | Devops | QA, Product |
| **Plugin System** | Dev-Backend | Tech Lead | Architect | Dev-Other |
| **SQL Database** | Dev-Backend | DBA | Architect | Devops |
| **Storage & Blobs** | Dev-Backend | Devops | Security | Dev-All |
| **Key Vault** | Devops | Security | Architect | Dev-All |
| **CI/CD Pipelines** | Devops | Tech Lead | Dev-Backend | Dev-All |
| **Production Deployment** | Devops | Tech Lead | Architect | Dev-All |
| **Monitoring & Alerts** | Devops | Tech Lead | Dev-Backend | Support |
| **Documentation** | Tech Lead | Architect | Dev-All | Support |
| **Security & Compliance** | Security | Architect | Tech Lead | Dev-All |

---

## 2. ON-CALL ROTATION

### Schedule (Google Calendar: documentia-oncall@...)

```
Week of June 10:
- Mon-Tue: Carlos (lead dev)
- Wed-Thu: María (devops)
- Fri-Sat: Juan (architect)
- Sun: Carlos (backup)

Week of June 17:
- [Rotate]
```

### On-Call Responsibilities

**Oncall Dev** (week rotation):
- Available 24/7 during assigned week
- Response time: < 30 min for P1, < 2 hours for P2
- Duty: Debug issues, coordinate with team, authorize hotfixes
- Support: Escalate complex issues to lead architect

**Escalation Path:**
1. Oncall Dev (first responder)
2. Tech Lead (if not responding or needs expertise)
3. Lead Architect (if infrastructure/data issue)
4. Microsoft Support (if Azure service issue)

### Contact Info (confidential in actual team doc)

| Role | Name | Phone | Slack |
|------|------|-------|-------|
| Oncall Dev | [Rotation] | +34-XXX | @oncall-dev |
| Tech Lead | XXX | +34-XXX | @tech-lead |
| Lead Architect | XXX | +34-XXX | @architect |
| Devops | XXX | +34-XXX | @devops-team |
| Security | XXX | +34-XXX | @security |

---

## 3. SLA & RESPONSE TIMES

### Incident Severity Levels

| Severity | Impact | Response | Resolution | Example |
|----------|--------|----------|------------|---------|
| **P1** | Complete outage | 30 min | 2 hours | All documents failing |
| **P2** | Significant degradation | 2 hours | 8 hours | 50% error rate |
| **P3** | Minor issue | 4 hours | 1 week | Slow classification |

### Monthly SLA Targets

- **Availability:** 99.5% (prod)
- **P50 Latency:** < 8 sec
- **P99 Latency:** < 30 sec
- **Error Rate:** < 1%

---

## 4. CHANGE APPROVAL PROCESS

### Minor Changes (Hot-plugged, low risk)

- **Approval:** Tech Lead only
- **Testing:** Smoke tests required
- **Communication:** Update CHANGELOG
- **Deployment:** Can go straight to prod

Examples:
- Configuration tuning
- Alert threshold changes
- Non-breaking SQL updates

### Major Changes (Requires board)

- **Approval:** Lead Architect + Tech Lead + Security
- **Testing:** Full test suite + staging validation
- **Communication:** Pre-announcement, post-summary
- **Deployment:** Staged (dev → staging → prod)

Examples:
- Plugin system changes
- Database schema migration
- Security policy updates
- Infrastructure changes

### Process

1. Create proposal: Describe change, impact, rollback plan
2. Send to approval team (Slack channel #changes)
3. Wait for 2 approvals (24 hours max)
4. If approved: Schedule deployment window
5. Execute deployment (see RELEASE_MANAGEMENT.md)
6. Post-execution review (24 hours later)

---

## 5. TRAINING & ONBOARDING

### New Developer Onboarding (2 days)

**Day 1:**
- Setup development environment (QUICKSTART_DESARROLLADORES.md)
- Read architecture docs (ARQUITECTURA_SISTEMA.md)
- Create first plugin (EXTENSIBILIDAD_PLUGIN_SYSTEM.md)
- First PR review

**Day 2:**
- Production incident simulation (using RUNBOOK_INCIDENTES_PRODUCCION.md)
- Shadow oncall dev for 4 hours
- Granted production access
- Buddy assignment for 1 week

### New Devops Onboarding (1 day)

- Infrastructure tour (INFRAESTRUCTURA_DESPLIEGUE.md)
- Deployment walkthrough (RELEASE_MANAGEMENT.md)
- Monitoring setup (OBSERVABILIDAD_KQL.md)
- Incident response drill

### Quarterly Training

- Architecture review (Architect leads)
- Security training (Security team)
- New features deep-dive (Tech Lead)

---

## 6. VACATION & COVERAGE

### Oncall Handover

When taking vacation:
1. Pass oncall to teammate
2. Handover meeting (30 min)
3. Document known issues
4. Provide emergency contact

### Peak Support Times

- June-September: Extra support staff (summer load)
- December: Reduced support (holidays)
- Post-release: Full team on-call for 48 hours

---

## 7. PERFORMANCE & GOALS

### Metrics by Role

**Dev-Backend:**
- Code review turnaround: < 24 hours
- Bug fix rate: 80%+ of reported bugs fixed within 1 sprint
- Documentation: New features documented before merge

**Devops:**
- Deployment success rate: > 99%
- Mean time to recovery (MTTR): < 30 min for P1
- Alert false-positive rate: < 5%

**Tech Lead:**
- Code review accuracy: > 95% catch rate
- Architecture decision documentation: 100%
- Team training: Quarterly sessions
