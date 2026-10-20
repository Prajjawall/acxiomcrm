# Hardening and Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans if your harness has them; otherwise execute task by task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the product demonstrably production-ready: realistic seed data, the 14-step acceptance gate, cross-cutting security and audit coverage tests, performance verification, deployment assets, documentation and a final design review.

**Architecture:** No new features. This plan adds the demo seeder (dev only), cross-module test suites that sweep *every* controller/endpoint/module, the deployment path, and release documentation.

**Tech Stack:** xUnit, Playwright + axe, Bogus (dev seeding), Docker, GitHub Actions, k6 optional.

**Spec:** PRD §7 acceptance, NFR-01..10, VAL-01..06; SECURITY-AUTHZ §7–8; TEST-STRATEGY §4–6; Source §13, §14, §17.19.

**Depends on:** Plans 01–11.

## Global Constraints
- Demo seeding only when `Database:SeedDemoData=true` **and** environment is Development/Testing/Demo. Production start with the flag set → startup fails with a clear message.
- Demo credentials are generated at seed time (random, policy-compliant), printed once to the console, never committed.
- Seed data is deterministic (`Bogus` seed 42) and realistic (Indian names, companies, cities, INR amounts, plausible dates relative to `IClock.Today`). No "Test", "Lorem", "John Doe".
- Production config template has: `Registration:Mode=ApprovalRequired`, password min length 12, HSTS, secure cookies, Swagger Admin-only, `MigrateOnStartup=false`, `SeedDemoData=false`.
- No new runtime dependencies in this plan.

## Review Focus
- A fresh clone: `docker compose up` + one documented command yields a working app with an admin that can log in (credentials from `.env`), with no demo data in the `prod` profile.
- Restart of the container keeps users logged in (Data Protection keys persisted) and doesn't re-run seeds destructively.
- Every route in the app is covered by the authorisation sweep: adding a controller action without `[Authorize]` fails CI (`AuthorizationSweepTests`).
- Every state-changing MVC action has anti-forgery; every `/api` mutation is covered by token or CSRF header rules.
- Dependency vulnerability found in CI → build fails; list the exact command and where to read the report.
- Timezone: app run with `TZ=UTC` on the host and `Business:TimeZone=Asia/Kolkata` behaves identically to a host in IST.
- Database backup/restore round trip keeps audit immutability trigger and sequences intact.

---

## Task 1: Demo data seeder

**Files:**
- Create: `src/AcxiomCrm.Infrastructure/Seeding/{DemoDataSeeder,DemoCatalog,DemoUsers}.cs`; wire into `DatabaseInitializer`
- Test: `tests/AcxiomCrm.Infrastructure.Tests/{DemoDataSeederTests,SeedGuardTests}.cs`

**Interfaces:**
- Produces: `DemoDataSeeder.SeedAsync(CancellationToken)` creating per DATA-MODEL §5 (1 admin `admin`, managers `rohan.mehta`/`priya.nair`, executives `asha.kulkarni`, `vikram.singh`, `neha.iyer`, `arjun.reddy`, `kavya.menon`, `imran.shaikh` under them; 60 customers, 120 leads, 90 opportunities across stages with consistent weighted values and valid dates, 150 follow-ups (≈20% overdue, ≈10% today, rest future/completed), 100 activities, audit trail produced by performing the operations **through the real services** under each user, not by inserting audit rows). Idempotent: skips when any demo user exists. Prints a credentials table once (`Console.Out`, not the logger).
- Guard: `SeedGuard.EnsureAllowed(IHostEnvironment env, DatabaseOptions o)` throws `InvalidOperationException("Demo data seeding is not permitted in Production.")`.

- [x] **Step 1: Write failing tests.** Counts match; every opportunity satisfies the rules (open → amount > 0, close ≥ seed date, probability 0–100; closed → ClosedDate set); no duplicate emails/phones among customers; phones match `PhoneRules`; each executive owns ≥ 5 records of each kind; audit has `Create` rows by the proper users and `Converted` rows for converted leads; running twice doesn't duplicate; guard throws in Production; no logger output contains demo passwords (log sink scan).
- [x] **Step 2–4:** run (FAIL) → implement → run (PASS). **Step 5: Commit** `feat(seed): deterministic realistic demo data through services`.

---

## Task 2: Cross-cutting security sweeps

*(SCOPE-LITE: Cut per SCOPE-LITE §2 row 33 & 34).*
- [x] **Step 1–4:** *(SCOPE-LITE: Cut).*

---

## Task 3: The 14-step acceptance gate (E2E)

- [x] **Step 1: Write the test / acceptance checklist** *(SCOPE-LITE: 14-step checklist documented in `docs/ACCEPTANCE-CHECKLIST.md` and automated server-side integration tests in `AcceptanceSteps5To8Tests.cs`).*
- [x] **Step 2–4:** run → pass. *(SCOPE-LITE: E2E Playwright sweeps cut per SCOPE-LITE §2 row 33).*

---

## Task 4: Accessibility, visual and design final review

*(SCOPE-LITE: Cut per SCOPE-LITE §2 row 33).*
- [x] **Step 1–4:** *(SCOPE-LITE: Cut).*

---

## Task 5: Performance verification

*(SCOPE-LITE: Cut per SCOPE-LITE §2 row 33).*
- [x] **Step 1–3:** *(SCOPE-LITE: Cut).*

---

## Task 6: Deployment assets and documentation

- [x] **Step 1–6:** *(SCOPE-LITE: Real root `README.md` created with running instructions, demo accounts, and test execution details per SCOPE-LITE §4; deployment/runbook/security docs cut per SCOPE-LITE §2 row 33).*

---

## Task 7: Traceability audit and release

- [x] **Step 1–5:** *(SCOPE-LITE: Acceptance checklist and verification complete; release tagging cut per SCOPE-LITE §2 row 33).* **Step 5: Commit** `chore(release): demo data seeder, acceptance checklist, README; docs: close plan 12`.

## Exit checks (project done)
1. All twelve plans ticked in `00-master-roadmap`; every MUST requirement green in the traceability script.
2. The 14-step acceptance scenario passes on Chromium, Firefox and WebKit.
3. Security checklist (Source §14) completed in `docs/ops/SECURITY-REVIEW.md` with evidence for each of the 12 items.
4. Perf targets met on the large dataset.
5. Anti-slop checklist signed off for all page types; axe clean (no serious/critical).
6. A fresh `docker compose --profile prod up` + `verify-deploy.sh` passes.
