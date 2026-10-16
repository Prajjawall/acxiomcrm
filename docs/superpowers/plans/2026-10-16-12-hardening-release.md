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

- [ ] **Step 1: Write failing tests.** Counts match; every opportunity satisfies the rules (open → amount > 0, close ≥ seed date, probability 0–100; closed → ClosedDate set); no duplicate emails/phones among customers; phones match `PhoneRules`; each executive owns ≥ 5 records of each kind; audit has `Create` rows by the proper users and `Converted` rows for converted leads; running twice doesn't duplicate; guard throws in Production; no logger output contains demo passwords (log sink scan).
- [ ] **Step 2–4:** run (FAIL) → implement → run (PASS). **Step 5: Commit** `feat(seed): deterministic realistic demo data through services`.

---

## Task 2: Cross-cutting security sweeps

**Files:**
- Create: `tests/AcxiomCrm.Web.Tests/Sweeps/{AuthorizationSweepTests,AntiForgerySweepTests,IdorSweepTests,ResponseHeadersSweepTests,ApiShapeSweepTests,SqlInjectionTests,XssTests,CsvInjectionSweepTests,AuditCoverageTests,ConstraintParityTests}.cs`
- Create: `tests/AcxiomCrm.Application.Tests/ArchitectureAndConventionTests.cs`

**Interfaces:**
- Consumes: `EndpointDataSource`, the seeded test users, every service.
- Tests:
  - `AuthorizationSweepTests.Every_endpoint_requires_authorization_except_allowlist`: enumerate all endpoints; allow-list = `/Account/Login|Register|AccessDenied`, `/health/*`, static files, `/api/auth/login|refresh`, error routes. Every other endpoint has a non-empty authorization policy set, and a request without credentials never returns 2xx.
  - `AntiForgerySweepTests`: every non-GET MVC endpoint rejects a request without a token (400); `Api` cookie-auth mutations rejected without header.
  - `IdorSweepTests`: for each module and each `{id}` route (MVC + API) as executive A against B's record and a foreign-team manager → 404; list/search/report/export/dashboard exclude B's data (assert by sentinel names).
  - `SqlInjectionTests`: classic payloads (`' OR 1=1 --`, `'; DROP TABLE Customers;--`, `%`, `_`, `[a-z]`, unicode quotes) in every search/filter/sort parameter across MVC and API → status 200/400 (never 500), results equal literal-match expectations, tables intact, no SQL text in responses. Plus a source scan test: no `FromSqlRaw`, `ExecuteSqlRaw`, string-concatenated `FromSql`, `SqlCommand` with `+` concatenation under `src/`.
  - `XssTests`: create records with `"><img src=x onerror=alert(1)>`, `<script>`, `javascript:` URLs in every text field; render list/details/timeline/audit/report/search pages and assert encoded output (`&lt;`), no raw tag, CSP header present, no inline handlers in markup.
  - `CsvInjectionSweepTests`: every report export with hostile cell values.
  - `AuditCoverageTests` (AUD-02): table-driven list of every audited operation (create/update/delete for the five entity types, lead status change/assign/convert, opportunity stage/reopen, follow-up complete/reschedule/miss, user role change/activate/deactivate/unlock/reset, login success/fail/lockout/logout/register/password change) → perform via the real services/HTTP and assert **exactly one** audit row with the right action/module/entity/record/user/IP and no secrets.
  - `ConstraintParityTests`: for each domain rule with a DB CHECK (amount, probability, status-stage, contact, converted, follow-up related, phone), direct SQL violation fails → confirms DB mirrors the domain.
  - `ArchitectureAndConventionTests`: layering (plan 01) plus: services expose no public method taking/returning EF entities; controllers don't inject `CrmDbContext`/`ICrmDbContext`; every `IOwnedEntity` type is referenced via `InScope` in its service (Roslyn-free textual check of `Infrastructure/<Module>/*Service.cs` files for `InScope(` presence).
- [ ] **Step 1: Write the sweeps.** **Step 2: Run** `dotnet test --filter "FullyQualifiedName~Sweeps|ArchitectureAndConvention"`. Expected: some FAIL on first run → **fix the product code** (add missing attributes, scopes, encoding) rather than weakening a sweep. Record each fix in the commit body.
- [ ] **Step 3: Run again.** Expected: PASS.
- [ ] **Step 4: Commit** `test(security): authorisation, CSRF, IDOR, injection, XSS and audit coverage sweeps`.

---

## Task 3: The 14-step acceptance gate (E2E)

**Files:**
- Create: `tests/AcxiomCrm.E2E/AcceptanceScenarioTests.cs`, `tests/AcxiomCrm.E2E/Pages/*` (page objects: `LoginPage`, `RegisterPage`, `DashboardPage`, `CustomerFormPage`, `OpportunityFormPage`, `FollowUpFormPage`, `AuditPage`)

**Interfaces:** one test class executing the PRD §7 / Source §17.19 steps **in order** against a fresh DB with demo seed, using real browser interactions; helper `RawHttp` for the crafted-request step.

- [ ] **Step 1: Write the test** with these steps as named `await Step(n, "...")` blocks, each asserting the Source expectation:
  1. anonymous GET `/Customers` → lands on `/Account/Login?ReturnUrl=…`; also `/api/customers` → 401.
  2. register a new user (Open mode) → auto sign-in → URL `/Dashboard`.
  3. create customer with invalid email and phone → inline messages `Enter a valid email address.` / `Enter a valid phone number.`, no POST sent.
  4. crafted POST (valid token, invalid fields) via `RawHttp` → 200 redisplay with the same messages / API 400; row count unchanged.
  5–7. opportunity: amount 0 → `Opportunity Amount must be greater than 0.`; probability 101 → `Probability must be between 0 and 100.`; yesterday → `Expected Close Date cannot be in the past.` (client and server).
  8. follow-up yesterday → `Follow-up date cannot be earlier than today.`
  9. login as seeded executive → only own data in Customers/Leads/Opportunities/Follow-ups lists (assert by seeded counts), reports limited, `/Users` → 403, `/Audit` → 403.
  10. login as seeded manager → team pipeline report and board totals include team members, exclude other team; `/Users` read-only; `/Roles` → 403.
  11. login as admin → Users, Roles, Audit reachable and functional.
  12. create, update, delete a customer → three audit entries (verify on `/Audit` filtered by record) with correct user and old/new values.
  13. `GET /api/customers` with cookie session and with a freshly issued Bearer token → JSON 200 scoped; without credentials → 401.
  14. dashboard: 8 KPI cards and 3 rendered charts with data for each role.
- [ ] **Step 2: Run** `dotnet test tests/AcxiomCrm.E2E --filter AcceptanceScenarioTests`. Expected: FAIL until everything is wired; fix product code (not the test) for genuine defects.
- [ ] **Step 3: Run** on Chromium, then Firefox and WebKit (`PLAYWRIGHT_BROWSER=firefox|webkit`). Expected: PASS on all three (record any browser-specific fix).
- [ ] **Step 4: Commit** `test(e2e): 14-step acceptance scenario`.

---

## Task 4: Accessibility, visual and design final review

**Files:**
- Modify: `tests/AcxiomCrm.E2E/AccessibilityTests.cs` (Theory over **every** primary page per role), create `tests/AcxiomCrm.E2E/KeyboardJourneyTests.cs`, update `docs/design/screens/README.md`

- [ ] **Step 1: Write failing tests.** axe (wcag2a/aa, 21aa, 22aa) over all list/create/edit/details pages, board, dashboard, each report, audit, users, roles, error pages, auth pages — fail on serious/critical; `KeyboardJourneyTests`: login → create lead → change status → convert → schedule follow-up → complete it, using only keyboard (Tab/Shift+Tab/Enter/Space/Esc/arrows) with no mouse events; `ReducedMotionTests` (emulated `prefers-reduced-motion`: no transitions longer than 0.01 ms computed on key components); zoom 200% at 1280 wide has no horizontal page scroll and no clipped controls; text-spacing override doesn't break layout (axe `avoid-inline-spacing` passes).
- [ ] **Step 2:** Fix all findings. **Step 3: Manual review**: capture 1280 and 768 screenshots for every page type; complete the DESIGN-SYSTEM §6 anti-slop checklist in `docs/design/screens/README.md` with `yes/no` per page and a signed-off date; confirm consistent spacing, badges, empty states, and microcopy against DESIGN-SYSTEM §5; NVDA/VoiceOver spot check on one form and one table, recorded.
- [ ] **Step 4: Commit** `test(a11y): full-page axe, keyboard journey, final design review`.

---

## Task 5: Performance verification

**Files:**
- Create: `scripts/perf/{seed-large.sql,run-perf.ps1,run-perf.sh}`, `tests/AcxiomCrm.Web.Tests/Perf/ListPerfSmokeTests.cs`; modify DATA-MODEL if indexes are added

- [ ] **Step 1: Write failing perf smoke tests** (`category=perf`): with the large seed (50k customers, 100k leads, 100k opportunities, 200k follow-ups, 500k audit rows) assert p95 over 30 requests: list pages first page < 400 ms, search by name prefix < 400 ms, details page < 300 ms, dashboard < 800 ms, each report first page < 400 ms, audit search with date filter < 400 ms; plus `No_N_plus_one` (command count per list page ≤ 3, per details page ≤ 8) using a command-counting interceptor.
- [ ] **Step 2: Run** against the large database. Expected: any miss → capture `SET STATISTICS IO`/actual plan, add or adjust an index in a new migration, update `DATA-MODEL.md`, re-run until PASS. Do not "fix" by raising thresholds.
- [ ] **Step 3: Commit** `perf: verify and tune list, dashboard and report queries`.

---

## Task 6: Deployment assets and documentation

**Files:**
- Modify: `Dockerfile`, `docker-compose.yml` (profiles `dev`, `prod`), `.github/workflows/ci.yml` (add `e2e`, `docker-build`, `trivy`/`dotnet list package --vulnerable`), `appsettings.Production.json`
- Create: `docker-compose.prod.example.yml`, `.env.example` (final), `scripts/{migrate-bundle.sh,backup-db.sh,restore-db.sh,verify-deploy.sh}`
- Complete: `docs/ops/DEPLOYMENT.md`; create `README.md` (project root, replacing the placeholder), `docs/ops/RUNBOOK.md`, `docs/ops/SECURITY-REVIEW.md`, `CHANGELOG.md`, `docs/USER-GUIDE.md`
- Test: `tests/AcxiomCrm.Web.Tests/{ProductionConfigTests,DataProtectionPersistenceTests,HealthEndpointsTests}.cs`

**Interfaces / content:**
- `DEPLOYMENT.md`: architecture diagram (text), environments (dev, staging, prod), configuration table (every option from ARCHITECTURE §7 with env-var names `Security__Jwt__SigningKey` etc., required/optional, default), secret handling, TLS termination and forwarded headers, first-run procedure (`dotnet ef migrations bundle` → run bundle → start app → bootstrap admin), upgrade and rollback procedure (take backup, apply bundle, smoke test with `verify-deploy.sh`, rollback = restore backup + previous image), backup/restore with retention advice, log locations, health probe usage, scaling note (single instance unless Data Protection and rate limiter are shared), known limits.
- `RUNBOOK.md`: unlock a user from DB if UI is unavailable, rotate JWT signing key (invalidates tokens by design), reset the only admin, investigate "who changed X" with audit SQL examples, handle lockout storms, disk/log retention.
- `SECURITY-REVIEW.md`: Source §14 checklist table with control, evidence (test class names and file paths), result, reviewer and date; plus SECURITY-AUTHZ §8 threat checklist with outcomes.
- `USER-GUIDE.md`: short task-oriented guide per role (screens, how to convert a lead, how to read the pipeline board, how to export reports) with the exact UI labels.
- `ProductionConfigTests`: starting the app with `Environment=Production` and default `appsettings.Production.json` but **missing** signing key / connection string / bootstrap admin → fails fast with a message naming the setting; with `SeedDemoData=true` → fails; cookies are `__Host-` prefixed and `Secure`; Swagger gated; `Registration:Mode` default `ApprovalRequired`. `DataProtectionPersistenceTests`: a cookie issued by one app instance is accepted by a second instance sharing the key store.

- [ ] **Step 1: Write the failing tests above.** **Step 2: Run** `--filter "ProductionConfigTests|DataProtectionPersistenceTests"`. Expected: FAIL.
- [ ] **Step 3: Implement** config validation, key ring persistence (`PersistKeysToDbContext` via a `DataProtectionKeys` table migration, or a mounted volume path from `DataProtection:KeysPath`; choose DbContext and update DATA-MODEL), documentation and scripts.
- [ ] **Step 4: Verify the deploy path from scratch** on a clean machine/container host: `cp .env.example .env` (edit secrets) → `docker compose --profile prod up -d --build` → run `scripts/verify-deploy.sh` which checks `/health/ready`, logs in as the bootstrap admin through the API, fetches `/api/customers`, and checks security headers. Then run `scripts/backup-db.sh` and `scripts/restore-db.sh` into a scratch database and confirm audit trigger and sequences work (`INSERT` works, `UPDATE AuditLogs` fails).
- [ ] **Step 5: Run** `dotnet list package --vulnerable --include-transitive` and `npm audit --omit=dev`; resolve or document each finding in `SECURITY-REVIEW.md`.
- [ ] **Step 6: Commit** `chore(release): production config, deployment docs, runbook, security review`.

---

## Task 7: Traceability audit and release

**Files:**
- Create: `scripts/check-traceability.sh`, `scripts/check-traceability.ps1`; modify `docs/qa/TEST-STRATEGY.md` (final matrix with test file paths and pass status), `CHANGELOG.md`

- [ ] **Step 1: Write the script** that parses requirement IDs from `docs/prd/PRD.md` (`^\| (AUTH|AUTHZ|…)-\d+`) and the matrix, and fails when any MUST requirement lacks at least one existing test class name found by `git grep "class <Name>"` in `tests/`.
- [ ] **Step 2: Run** the script. Expected: lists any gaps. Add the missing tests (or correct names), re-run until exit 0.
- [ ] **Step 3: Full verification** (run each, read the output, report counts): `dotnet build -warnaserror`; `dotnet test` (all, incl. E2E on three browsers); `dotnet test --filter category=perf`; `dotnet format --verify-no-changes`; `npm --prefix src/AcxiomCrm.Web test`; `scripts/check-traceability.sh`; CI green on the release branch.
- [ ] **Step 4: Release checklist** (`docs/ops/DEPLOYMENT.md` §Release): version bump (`Directory.Build.props` `Version`), changelog, tag `v1.0.0`, container image tagged and scanned, prod config reviewed, backup taken, smoke test passed, rollback verified.
- [ ] **Step 5: Commit and tag** `chore(release): v1.0.0`.

## Exit checks (project done)
1. All twelve plans ticked in `00-master-roadmap`; every MUST requirement green in the traceability script.
2. The 14-step acceptance scenario passes on Chromium, Firefox and WebKit.
3. Security checklist (Source §14) completed in `docs/ops/SECURITY-REVIEW.md` with evidence for each of the 12 items.
4. Perf targets met on the large dataset.
5. Anti-slop checklist signed off for all page types; axe clean (no serious/critical).
6. A fresh `docker compose --profile prod up` + `verify-deploy.sh` passes.
