# Infraestructura & Despliegue — DocumentIA

## 1. TOPOLOGÍA DE DESPLIEGUE

### Visión General (Diagrama ASCII)

```
┌─────────────────────────────────────────┐
│         Azure Subscription              │
│  (Org: sareb, Project: AI DocClassExt)  │
└──────────────┬──────────────────────────┘
               │
       ┌───────┴────────┐
       │                │
   ┌─────────┐      ┌─────────────┐
   │ Staging │      │ Production  │
   └────┬────┘      └────┬────────┘
        │                │
    ┌───┴──────┐     ┌───┴──────────┐
    │          │     │              │
┌────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
│ App    │  │ Function │  │ App      │  │ Function │
│Service │  │ App      │  │Service   │  │App       │
│Admin   │  │ Functions│  │Admin     │  │Functions │
└────────┘  └──────────┘  └──────────┘  └──────────┘
    │          │              │          │
    └──────────┴──────────────┴──────────┘
               │
       ┌───────┴───────┐
       │               │
    ┌──────────┐   ┌──────────┐
    │ SQL DB   │   │ Storage  │
    │Server    │   │Account   │
    └──────────┘   └──────────┘
       │               │
       └───────┬───────┘
            ┌──────────┐
            │ Key      │
            │ Vault    │
            └──────────┘
```

### Resource Groups

- **RG-Staging:** App Service (admin), Function App, SQL, Storage, Key Vault
- **RG-Production:** App Service (admin), Function App, SQL, Storage, Key Vault (separate)

### Servicios por Ambiente

#### Staging
| Servicio | SKU | Capacity | Notes |
|----------|-----|----------|-------|
| App Service | B2 | 2 cores, 3.5 GB RAM | Shared instance |
| Function App | Premium EP1 | 1 core, 3.5 GB RAM, always on | Warm starts |
| SQL Database | S2 | Standard 2, 50 DTU | Staging load |
| Storage | Standard LRS | 100 GB allocado | Local redundancy |
| Key Vault | Standard | N/A | No rate limit risk |

#### Production
| Servicio | SKU | Capacity | Notes |
|----------|-----|----------|-------|
| App Service | P1v2 | 2 cores, 7 GB RAM | Auto-scale enabled |
| Function App | Premium EP2 | 2 cores, 7 GB RAM, always on | Auto-scale 2-10 instances |
| SQL Database | S3 | Standard 3, 100 DTU | Higher load, SLA 99.9% |
| Storage | Standard GRS | 500 GB allocado | Geo-redundant |
| Key Vault | Standard | Premium option | Rate limit: 300 req/10s |

---

## 2. CAPACIDAD & ESCALADO

### Límites Actuales

| Componente | Límite Actual | Límite Azure | % Utilizado |
|-----------|---|---|---|
| Function instances | 10 (auto-scale max) | 200 | 5% |
| SQL connections | 100 pool size | Limited by tier | Check monitoring |
| Storage throughput | 60 GB/s | Account limit | Low |
| Key Vault | 300 req/10s | 300 req/10s | At risk if surge |

### Escalado Manual (SLA trigger)

**Cuándo escalar:**
- P99 latency > 30 sec (vertical)
- Error rate > 5% (investigate first, then scale)
- CPU > 80% (vertical or horizontal)
- Memory > 85% (vertical)

**Cómo escalar SQL:**
```powershell
Set-AzSqlDatabase -ResourceGroupName "RG-Prod" -ServerName "doc-ia-sql" `
  -DatabaseName "DocumentIA" -Edition "Premium" -Capacity 4 -RequestedServiceObjectiveName "P4"
```

**Cómo escalar Function App:**
1. Portal: Function App → Scale up → Increase EP tier
2. Auto-scale rules updated automatically
3. Monitor 15 min para stabilization

---

## 3. COST BREAKDOWN (Actual + Forecast)

### Monthly Cost Baseline (Production)

| Service | Size | Monthly Cost |
|---------|------|------|
| App Service P1v2 | 2 cores | $150 |
| Function Premium EP2 | 2 cores, 10 instances peak | $400 |
| SQL Database S3 | 100 DTU | $300 |
| Storage Account GRS | 500GB | $25 |
| Key Vault | Standard | $1 |
| Application Insights | 5GB/day ingestion | $50 |
| **TOTAL** | | **$926/month** |

### Cost per Document Processed
- Azure services: ~$0.001
- Plugins (CU, GPT): ~$0.04
- **Total per doc: ~$0.041** (varies by plugins used)

### Cost Optimization Opportunities
1. Reduce Function concurrent instances from 10 to 8 (monitor P99 first)
2. Use Consumption Plan instead of Premium (trade-off: cold starts)
3. Batch plugin calls to reduce token usage
4. Archive old documents to cool storage

---

## 4. NETWORKING TOPOLOGY

### VNet & Subnets (if applicable)

Current: Public endpoints for all services (no VNet isolation)

Recommended for future:
- VNet: 10.0.0.0/16
- Subnet-Services: 10.0.1.0/24 (App Service, Function)
- Subnet-DB: 10.0.2.0/24 (SQL)
- Private endpoints for Storage, Key Vault

### Firewall Rules (Current)

- App Service: No IP restrictions (public)
- SQL Server: Azure services allowed, no IP whitelist
- Storage: Public, but requires authentication
- Key Vault: Public, but requires RBAC

### Recommended Hardening

1. Add IP whitelist for known clients
2. Enable service endpoints (not private endpoints yet)
3. Monitor suspicious IPs in logs

---

## 5. DISASTER RECOVERY

### RTO/RPO Targets

| Component | RTO | RPO |
|-----------|-----|-----|
| Function App | 15 min (redeploy) | N/A (stateless) |
| App Service | 15 min (redeploy) | N/A (stateless) |
| SQL Database | 30 min (manual failover) | 1 min (automatic backups) |
| Storage Account | 1 hour (manual failover to GRS pair) | 1 min |

### Backup Strategy

**SQL Database:**
- Automated backups: Every 5 min (transaction logs)
- Full backups: Daily
- Retention: 35 days
- Geo-replicated: Yes (GRS)

**Storage Account:**
- GRS enabled: 200km failover
- LRS backup: Daily snapshot
- No manual snapshots configured

**Configuration:**
- Key Vault: Backed up manually before major changes
- host.json, appsettings: In Git (single source of truth)

### Recovery Procedures

**For SQL corruption:**
1. Restore from backup: `Restore-AzSqlDatabase -FromPointInTimeBackup ...`
2. Verify data integrity
3. Failover application if needed

**For storage loss:**
1. Check GRS status: If primary down, failover to secondary
2. Update connection strings in Key Vault
3. Test access
4. Failback when primary recovered

---

## 6. MONITORING & ALERTS

### Key Metrics

Monitored in Application Insights + Azure Monitor:

- Request count & latency
- Error rate & exceptions
- Dependency latency (external APIs)
- Storage throughput
- SQL DTU usage
- Memory by instance

### Alert Rules (Current)

| Alert | Threshold | Action |
|-------|-----------|--------|
| P99 latency | > 30 sec | Page on-call dev |
| Error rate | > 5% | Page on-call dev |
| SQL DTU | > 80% | Email (manual scale) |
| Storage full | > 90% | Email |

See OBSERVABILIDAD_KQL.md for query details.

---

## 7. COMPLIANCE & SECURITY

### Data Residency
- All data: EU (Spain) region
- Backup: Geo-replicated (EU secondary)
- No US data storage

### Encryption
- Storage: At-rest encryption (AES-256)
- Transit: TLS 1.2+ required
- Key Vault: HSM-backed keys

### Access Control
- Authentication: Azure AD / Managed Identities
- SQL: Database users with minimal roles
- Key Vault: RBAC, no shared keys

### Audit Logging
- Activity logs: 90 day retention
- SQL audit: Enabled on production
- Application Insights: 90 day retention
