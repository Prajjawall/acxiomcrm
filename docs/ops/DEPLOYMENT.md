# AcxiomCRM — Deployment

> Skeleton created with the planning set. **Plan 01 Task 7** fills the dev/compose sections and **Plan 12 Task 6** completes everything else. Do not leave a heading empty at release.

## 1. Environments
(Development, Staging, Production: purpose, database, config source, who deploys)

## 2. Configuration reference
(Every option from `docs/architecture/ARCHITECTURE.md` §7 with env-var names such as `Security__Jwt__SigningKey`, required/optional, default)

## 3. Secrets
(Where each secret lives, rotation procedure)

## 4. First-run procedure
(Migrations bundle, bootstrap admin, health verification)

## 5. Upgrade and rollback
(Backup, bundle, smoke test with `scripts/verify-deploy.sh`, rollback)

## 6. Backup and restore
(Schedule, retention, restore drill including audit trigger/sequence checks)

## 7. TLS, proxies and headers
(Forwarded headers, HSTS, known proxies)

## 8. Observability
(Logs, correlation ids, health probes)

## 9. Scaling notes
(Single instance vs multi; shared Data Protection keys, shared rate-limit store)

## 10. Release checklist
(Version, changelog, tag, image scan, config review, backup, smoke, rollback verified)
