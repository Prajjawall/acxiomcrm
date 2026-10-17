# AcxiomCRM Master Roadmap

> **For agentic workers:** This is the index, not an executable plan. Execute the numbered plans below, one at a time, with `superpowers:subagent-driven-development` or `superpowers:executing-plans` if your harness has them, otherwise task by task as written. Read `AGENTS.md` and `docs/EXECUTION-GUIDE.md` first.

**Goal:** Deliver AcxiomCRM, a production-grade role-based CRM, as specified in `docs/prd/PRD.md`.

**Architecture:** Layered ASP.NET Core MVC monolith (Domain / Application / Infrastructure / Web) on SQL Server with Identity, cookie + JWT auth, policy-based authorisation with record-level scope, interceptor-driven audit, and a design-system-driven Razor UI. See `docs/architecture/ARCHITECTURE.md`.

**Tech Stack:** .NET 10, ASP.NET Core MVC/Identity, EF Core 10, SQL Server 2022, Bootstrap 5.3 (Sass-themed), Chart.js 4, xUnit, Testcontainers, Playwright, Serilog, Docker.

**Spec:** `docs/prd/PRD.md` (source: `AcxiomCRM_Complete_Project_Assignment.pdf`).

## Run order

Plan **08 (Audit)** is split in two parts because authentication needs the audit writer before the UI exists. Plan file numbers are stable identifiers; the **Run** column is the order to execute.

| Run | Plan file | Outcome | Depends on | PRD IDs |
|---|---|---|---|---|
| 1 | `2026-10-16-01-foundation.md` | Solution, domain primitives, DB fixture, Docker, CI, health, logging | none | NFR-03,04,07,08,10 |
| 2 | `2026-10-16-08-audit-log.md` **Part A** (Tasks 1–4) | Audit table, writer, interceptor, query service | 1 | AUD-01..05 |
| 3 | `2026-10-16-02-identity-authz.md` | Login, logout, register, lockout, password policy, cookie auth, permissions, scope | 1, 2 | AUTH-*, AUTHZ-01..05 |
| 4 | `2026-10-16-03-app-shell-design-system.md` | Sass design system, layout, shared components, error pages, Playwright harness | 3 | SHELL-01..06, NFR-05 |
| 5 | `2026-10-16-08-audit-log.md` **Part B** (Tasks 5–6) | Audit UI | 4 | AUD-04, AUD-06 |
| 6 | `2026-10-16-04-customers.md` | Module kit, customers | 5 | CUST-*, VAL-* |
| 7 | `2026-10-16-05-leads.md` | Leads, workflow, conversion | 6 | LEAD-* |
| 8 | `2026-10-16-06-opportunities.md` | Opportunities, board | 7 | OPP-*, CUST-06/07 |
| 9 | `2026-10-16-07-followups-activities.md` | Follow-ups, activities, timeline, bell | 8 | FU-*, ACT-*, SHELL-07, CUST-05 |
| 10 | `2026-10-16-09-users-roles.md` | User admin, roles page | 9 | USR-*, AUTH-05/06 |
| 11 | `2026-10-16-10-rest-api.md` | REST API | 9 | API-* |
| 12 | `2026-10-16-11-dashboard-reports.md` | Dashboard, 8 reports, CSV, global search | 9 | DASH-*, RPT-*, SHELL-05 |
| 13 | `2026-10-16-12-hardening-release.md` | Seed data, sweeps, acceptance gate, perf, deploy, release | all | VAL-*, NFR-*, gate |

Runs 10, 11 and 12 are independent of each other after run 9 and may run in parallel branches, except that migrations are created one at a time (rebase before generating a new one). Plan 10 (API) has two `until plan-11` tests that plan 12 (reports) completes.

## Cross-plan conventions
- Each plan ends with **Exit checks**; the next plan may not start until they pass.
- Each module plan adds: entity + EF configuration + migration, Domain rules, Application service, MVC controller/ViewModels/Views, JS (if needed), tests at every level named in the traceability matrix, and appends its rows to `docs/qa/TEST-STRATEGY.md` only if it adds tests not already listed.
- Migrations: one per plan task that changes schema, named `YYYYMMDDHHmm_<Description>`; each is verified by an `Infrastructure.Tests` constraint test.
- Strings shown to users come from `Domain/Validation/Messages.cs` (canonical text from PRD §4).
- Every plan's last task is "Plan exit": run full build/test/format, update the traceability matrix, tick off the roadmap table in this file.

## Status board
Update as work completes.

| Run | Plan | Status | Merged by | Date |
|---|---|---|---|---|
| 1 | 01 Foundation | ☑ | plan/01-foundation | 2026-10-17 |
| 2 | 08 Audit, Part A | ☑ | plan/08-audit-log | 2026-10-17 |
| 3 | 02 Identity & authorisation | ☑ | plan/02-identity-authz | 2026-10-17 |
| 4 | 03 App shell & design system | ☑ | plan/03-app-shell-design-system | 2026-10-17 |
| 5 | 08 Audit, Part B | ☑ | plan/08b-audit-ui | 2026-10-17 |
| 6 | 04 Customers | ☑ | plan/04-customers | 2026-10-17 |
| 7 | 05 Leads | ☐ | | |
| 8 | 06 Opportunities | ☐ | | |
| 9 | 07 Follow-ups & activities | ☐ | | |
| 10 | 09 Users & roles | ☐ | | |
| 11 | 10 REST API | ☐ | | |
| 12 | 11 Dashboard & reports | ☐ | | |
| 13 | 12 Hardening & release | ☐ | | |

## Known cross-plan seams
| Seam | Defined in | Used in |
|---|---|---|
| `IAuditWriter`, `AuditSaveChangesInterceptor`, `IAuditActionHint` | 01 (interface), 08A (implementation), 05 (hint) | 02 (auth events), 04–07, 09 |
| `ICrmDbContext` DbSets | each module plan adds its own `DbSet` | services |
| `RolePermissionMap` | 01 | 02 (policies), 03 (nav), 09 (roles page), 10 |
| `_Layout`, `ax-*` components | 03 | every UI plan |
| `Messages` | 01 | all |
| `ScopeExtensions.InScope` / `IOwnerScope` | 01 (type), 02 (resolver) | all modules |
| `ITimelineService` | 07 | customer/lead/opportunity details tabs (customers show an audit-history tab until 07) |
| `IOpportunityFactory` | 05 (contract + temporary stub) | 06 (real implementation) |
| `IRefreshTokenRevoker` | 09 (no-op) | 10 (real implementation) |
| `IReportService.GetPipelineAsync` | 11 | 10 (`until plan-11` test) |
