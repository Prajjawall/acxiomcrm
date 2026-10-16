# SCOPE-LITE: college / dev-level scope (overrides the plans)

**Precedence:** this file overrides `docs/superpowers/plans/*`, `PRD.md`, `ARCHITECTURE.md`, `SECURITY-AUTHZ.md`, `API-SPEC.md`, `DESIGN-SYSTEM.md` and `TEST-STRATEGY.md` wherever they conflict. The full documents stay as the "later hardening" reference. Where this file is silent, follow the plan.

**Goal:** a working, clean, well-structured CRM that satisfies every **mandatory** item in the Source Spec (`AcxiomCRM_Complete_Project_Assignment.pdf` §17: modules, mandatory entities and fields, validation, business rules, Identity auth, roles, dashboard + Chart.js, search, REST API, audit, architecture, anti-forgery, no plain-text passwords). Not a hardened, load-tested, deployable product.

## 1. Keep exactly as planned (do not trim)
- All four-layer projects and folder structure (Domain / Application / Infrastructure / Web). Keep the existing Plan 01 work as is.
- **All entities and ALL fields** from DATA-MODEL (Customer, Lead, Opportunity, FollowUp, Activity, AuditLog, user extras) with their human codes, soft delete, enums and `CHECK` constraints that are cheap to add.
- Domain rules and canonical messages (PRD §4): amount > 0, probability 0–100, close date not in past, follow-up date not before today, email/phone, required, length. Client-side (unobtrusive) **and** server-side validation.
- Customer email/phone uniqueness and duplicate prevention. Lead status workflow (all transitions). Lead conversion in one transaction. Opportunity stages, derived status, Won=100 / Lost=0, weighted value. Follow-up statuses and rules.
- ASP.NET Core Identity: login, logout, **register**, password hashing/policy, lockout (5 failures), roles Admin/Manager/SalesExecutive, `ManagerId` team, permission policies, owner **scope** (executive own, manager team, admin all; out-of-scope = 404), anti-forgery on all POST forms.
- Audit log of login/failed login/create/update/delete/role change/status change/conversion (old + new value, user, IP), with an Admin viewer and filters.
- Dashboard: the 8 KPI cards and 3 Chart.js charts (lead status, opportunity pipeline, monthly sales), scoped by role.
- Search/filter + paging on Customers, Leads, Opportunities, Follow-ups, Activities.
- REST API: auth login, customers (full CRUD), leads (list/create), opportunities (list/create), follow-ups (list/create), pipeline report. DTOs only, status codes 200/201/400/401/403/404/409, authorisation on every endpoint.
- User & role management (create, edit, role change, activate/deactivate, unlock, reset password) and a read-only Roles/permissions page.

## 2. Cut (do not build, do not write tests for)
| Area | Cut |
|---|---|
| Plan 01 leftovers | CI pipeline, deployment docs, production Docker profile, `NoSecretsInConfigTests` extras, coverage gates, vulnerability scans. Keep compose DB for dev. |
| Identity (02) | Rate limiting, CSP nonce + strict CSP, extra security headers (keep `X-Content-Type-Options` only), HSTS, `__Host-` cookie names, 5-minute stamp tuning, login timing equalisation, MustChangePassword gate **middleware** (keep the flag + change-password page, no forced redirect) |
| Audit (08) | SQL trigger, DB `DENY`, `IAuditActionHint` (just let the interceptor write `Update`; write explicit `StatusChanged`/`Assigned`/`Converted` rows via `IAuditWriter` only where cheap), separate-connection failure writes, manager team-audit access, `ForRecordAsync` timeline use |
| Design (03) | Sass pipeline, npm build, contrast test, custom fonts, icon sprite, dev components page, Playwright, axe, screenshot review, anti-slop sign-off. **Instead:** commit Bootstrap 5.3, jQuery, jquery-validation(+unobtrusive) and Chart.js dist files into `wwwroot/lib/` (no npm build), plus one `wwwroot/css/site.css` that overrides Bootstrap CSS variables (teal primary `#0F5C5A`, dark sidebar `#14222B`, 4–6 px radius, no gradients/shadows) and system font stack. Layout: sidebar + top bar, role-aware menu, flash alerts, shared `_Pager`, `_StatusBadge`, confirm dialog via a small JS file. Friendly 403/404/500 pages with no exception text. Keep it tidy and consistent; no AI-slop (no emoji, no lorem ipsum, no default-blue buttons). |
| Customers (04) | Row-version concurrency UI, per-field audit title formatting, E2E. Keep service, validation attributes, controller, views, search. |
| Leads (05) | "Link to existing customer" suggestion UI (just block duplicates with the message), typeahead endpoint (use a plain dropdown limited to 200 customers) |
| Opportunities (06) | **Kanban board** (table view only), indian-decimal model binder (plain decimal), stale-date-on-stage-move subtleties |
| Follow-ups (07) | Timeline service, notification bell, historical-entry permission, 3 related-type combos beyond what the form needs (still keep CHECK exactly-one), DST tests |
| Users (09) | Reassignment-on-deactivate flow (just warn), temp-password one-time page (show once in a flash message), last-admin guard **keep** (it is small) |
| API (10) | Refresh tokens/rotation, ETag/If-Match/428, OpenAPI snapshot test, endpoint-inventory test, CORS test, Swagger gating. **Keep:** JWT bearer login (60-minute access token, no refresh) accepted alongside the cookie, `[Authorize]` policies, standard ProblemDetails, Swagger UI in Development |
| Reports/dashboard (11) | Date-preset bar beyond a simple "This month / All time" dropdown, delta lines, 5 of the 8 reports (build only **Customer, Pipeline and Follow-up** reports, each with filters, sorting and paging), CSV export, global search, perf tests |
| Hardening (12) | Security sweeps, three-browser E2E, a11y/keyboard/reduced-motion suites, perf, large seed, DEPLOYMENT/RUNBOOK/SECURITY-REVIEW/USER-GUIDE docs, traceability script, release tagging |
| Testing everywhere | No Playwright/E2E project work, no axe, no architecture-test additions beyond plan 01's, no IDOR *sweep* (but each module keeps one "other user's record → 404" test) |

## 3. Testing level (kept small but real)
- **Domain unit tests:** every rule table (phone, email, lead workflow, opportunity rules, follow-up rules, permissions map).
- **Service tests** (real SQL Server fixture from plan 01): per module, the happy path, each validation message, duplicates, scope (own/team/other → NotFound), conversion rollback, one audit-row assertion.
- **One integration test class per area** (`WebApplicationFactory`): login/logout/register, lockout, a role×route access check for ~10 key routes, anti-forgery rejection, API 401/403/404/400 smoke for customers.
- **Acceptance:** a manual checklist in `docs/ACCEPTANCE-CHECKLIST.md` with the 14 steps from Source §17.19 and a pass/fail column the agent fills in by actually running the app (and one automated integration test covering steps 5–8 server-side).
- Run only the filtered tests for the current task; run the full suite once at the end of each plan.

## 4. New simplifications to apply
- Business time zone: keep `IClock`, default `Asia/Kolkata`, no extra config validation tests.
- Seed data: a small demo seeder (1 admin, 2 managers, 4 executives, ~15 customers, ~25 leads, ~20 opportunities, ~30 follow-ups) created through services. Dev only. Print credentials once.
- Docs to produce at the end: a real `README.md` (what it is, how to run: `docker compose up -d db`, `dotnet run`, demo logins, how to run tests) and the filled acceptance checklist. Nothing else.

## 5. Order (unchanged run order, fewer steps)
08A → 02 → 03 → 08B → 04 → 05 → 06 → 07 → 09 → 10 → 11 → 12(lite: seed + checklist + README).
