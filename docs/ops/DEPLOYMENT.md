# AcxiomCRM — Deployment & Operations Guide

> [!NOTE]
> This is the operational deployment skeleton established in Plan 01 (Foundation). Complete production hardening, secrets rotation, and monitoring runbooks are detailed in Plan 12/13.

## 1. Target Environments

| Environment | Purpose | Hosting Model | Database |
|---|---|---|---|
| **Development** | Local engineering and integration testing | Local process or `docker-compose.yml` | SQL Server 2022 on localhost:1433 |
| **Testing / CI** | Automated test suites & testcontainers | GitHub Actions runner / Testcontainers | Dynamic containerized SQL Server instances |
| **Staging** | Pre-production validation and UAT | Container / App Service | Azure SQL / Managed SQL Server |
| **Production** | Production CRM workloads | Containerized non-root app (`:8080`) | High-availability SQL Server cluster |

---

## 2. Configuration & Environment Variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` | Hosting environment (`Development`, `Staging`, `Production`) |
| `ConnectionStrings__Default` | Yes | — | SQL Server connection string |
| `Database__MigrateOnStartup` | No | `false` | Apply pending EF Core migrations on host startup |
| `BusinessClock__TimeZoneId` | No | `Asia/Kolkata` | IANA TimeZone identifier for business operations |
| `AuditRetention__Days` | No | `90` | Retention window for CRM audit events |
| `ExportRetention__Days` | No | `7` | Retention window for generated CSV/XLSX exports |
| `LeadDistribution__Strategy` | No | `RoundRobin` | Lead assignment strategy (`RoundRobin`, `LoadBalanced`) |
| `Security__Jwt__Enabled` | No | `false` | Enable JWT authentication for API endpoints |
| `Security__Jwt__SigningKey` | If JWT | — | Minimum 32-byte secret key |

---

## 3. Release Checklist

1. **Pre-release Verification:**
   - [ ] All unit and architecture tests passing (`dotnet test`).
   - [ ] Database migrations verified against target schema.
   - [ ] Vulnerability audit clean (`dotnet list package --vulnerable --include-transitive`).
   - [ ] Client asset bundle built and fingerprinted.
2. **Deployment Execution:**
   - [ ] Database backup executed before migrations.
   - [ ] New container image pulled and healthcheck verified (`/health/live`).
   - [ ] Schema migration verified (`/health/ready` returns `Healthy`).
   - [ ] Smoke test key CRM workflows (authentication, customer list).
3. **Rollback Strategy:**
   - In case of critical failure, roll back to previous image tag and restore database snapshot if migrations included destructive schema alterations.

---

## 4. Backup & Restore Procedures

### Automated Database Backup
- Daily full backups with 30-day retention.
- Transaction log backups every 15 minutes for Point-In-Time recovery.

### Disaster Recovery
1. Provision target SQL Server instance.
2. Restore latest full database backup with `NORECOVERY`.
3. Apply transaction log backups in chronological sequence.
4. Point `ConnectionStrings__Default` to restored database and verify `/health/ready`.

---

## 5. Operations & Runbooks

- **RB-01: Health Check Failures:** Check `/health/ready` logs for database connectivity or migration failures.
- **RB-02: Secret Rotation:** Procedures for updating JWT signing keys and database credentials without downtime.
- **RB-03: Performance Degraded:** Investigate slow queries, index fragmentation, and database connection pooling metrics.
