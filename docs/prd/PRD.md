# AcxiomCRM — Product Requirements Document

| | |
|---|---|
| Status | Approved baseline for implementation |
| Source | `AcxiomCRM_Complete_Project_Assignment.pdf` (the "Source Spec") |
| Date | 2026-10-16 |
| Audience | Implementing engineers/agents (Antigravity, Codex), reviewers, QA |

Requirement IDs (`AUTH-01`, `CUST-03`, …) are stable. Plans, tests and the traceability matrix in `docs/qa/TEST-STRATEGY.md` refer to them. "MUST" means required for acceptance; "SHOULD" means expected unless a plan records a reason.

---

## 1. Product summary

AcxiomCRM is a role-based web CRM for a sales organisation. It covers the customer-sales lifecycle: lead capture, qualification, conversion, opportunity pipeline, follow-ups and activities, with authentication, authorisation, auditability, validation, a REST API, dashboards and reports.

It must be a production-quality product, not a CRUD demo. The Source Spec (§17.20) says a database CRUD app alone is not complete. Workflows, business rules, security and analytics are the product.

### 1.1 Goals
1. Sales executives manage their own pipeline quickly: few clicks, dense lists, keyboard-friendly.
2. Managers see team pipeline, follow-up discipline and conversion without asking anyone.
3. Admins control users, roles, security posture and can answer "who changed what, when, from where".
4. Every rule in the Source Spec is enforced on the server and demonstrable by an automated test.
5. The UI looks designed by a person: a restrained, data-forward interface with a deliberate design system (see `docs/design/DESIGN-SYSTEM.md`).

### 1.2 Non-goals (v1)
Email/SMS sending, calendar sync, file attachments, multi-tenancy, multi-currency, mobile native apps, custom fields/workflow builder, SSO/OIDC, 2FA, password-reuse history, data import tooling, localisation beyond English/INR, permission editing UI. Each is recorded in §9 with the reason.

### 1.3 Personas
| Persona | Role | Primary jobs |
|---|---|---|
| Asha, Sales Executive | `SalesExecutive` | Work my leads, log calls, schedule follow-ups, move my deals, hit my number |
| Rohan, Sales Manager | `Manager` | See team pipeline and weighted forecast, reassign leads, spot overdue follow-ups, export reports |
| Meera, Administrator | `Admin` | Create/deactivate users, change roles, unlock accounts, review audit trail |

---

## 2. Resolved decisions

The Source Spec contradicts itself in places or leaves choices open. These decisions are final for v1. Implementers must not re-litigate them. Changing one requires editing this table.

| ID | Topic | Decision | Reason |
|---|---|---|---|
| D-01 | Stack | .NET 10 (LTS), ASP.NET Core MVC + Razor, EF Core 10, ASP.NET Core Identity, SQL Server 2022, Bootstrap 5.3, jQuery Validation Unobtrusive, Chart.js 4 | Source Spec §17.4/17.9 mandates ASP.NET Core Identity, EF Core, Chart.js, Razor validation |
| D-02 | Layering | 4 projects: `Domain`, `Application`, `Infrastructure`, `Web` | Maps to Source Spec "Models, Data, Services, ViewModels, Controllers, Views" |
| D-03 | Primary keys | `int` identity keys plus a human-readable code (`CUS-000123`, `LEAD-000123`) on Customer and Lead | Source Spec names `CustomerId`, `CustomerCode`, `LeadCode` |
| D-04 | Entity field conflict (Source §9 vs §17.3) | Use the **union**. §17.3 is the base and §9 fields are added where absent (Address, Notes, Priority, Probability, ModifiedDate) | Neither list alone is complete |
| D-05 | Follow-up relation | Three nullable FKs (`CustomerId`, `LeadId`, `OpportunityId`) with a DB `CHECK` that exactly one is set. Derived `RelatedType` | Keeps referential integrity; Source §9 polymorphic form can't have FKs |
| D-06 | Activity vs Follow-Up | **Follow-Up** is future/planned work with a due date. **Activity** is a record of an interaction (Call, Meeting, Email, Task). Completing a follow-up offers "log as activity" in one step | Source lists both modules without distinguishing them |
| D-07 | Registration | Public `/Account/Register` exists. It always creates role `SalesExecutive`, never lets the visitor choose a role, and obeys `Registration:Mode` = `Open` \| `ApprovalRequired` \| `Disabled`. Dev and demo default to `Open`; the production template defaults to `ApprovalRequired`, where the account is created inactive until an Admin activates it | Satisfies Source acceptance step 2 without a privilege-escalation hole |
| D-08 | Teams | `ApplicationUser.ManagerId` (nullable). A Manager's scope is own records plus records owned by users whose `ManagerId` equals the manager. Admin sees all | Source says "Team" but never defines it |
| D-09 | Role model | Exactly 3 fixed roles, one role per user. Permissions are a code-defined catalogue mapped to roles. The Roles page is read-only (matrix + member counts) and role assignment happens in User Management | Source §4.7 allows single primary role; editing permissions is out of scope |
| D-10 | Deletion | Customers, Leads, Opportunities, Follow-Ups, Activities use **soft delete** (`IsDeleted`). "Delete" in UI/API means deactivate and hide. Users are deactivated, never deleted. Audit log is never deleted | Source: "Delete/deactivate"; preserves history and reports |
| D-11 | Phone rule | Indian mobile: after stripping spaces, hyphens and an optional `+91`/`91`/`0` prefix, it must match `^[6-9]\d{9}$`. Stored normalised as 10 digits | Source §5.1 example |
| D-12 | Email/phone uniqueness | Case-insensitive unique email and unique phone among non-deleted **Customers**, enforced by filtered unique indexes and a service check. Leads must be valid but are not unique, since duplicates are checked at conversion | Source §17.5 (Customer) and §17.7 ("where uniqueness is required") |
| D-13 | Duplicate customer | A duplicate is a non-deleted customer with the same normalised email **or** same phone. Hard block with a message naming the existing record | Source §17.5 |
| D-14 | Business "today" | The business time zone is configurable (`Business:TimeZone`, default `Asia/Kolkata`). All "today"/past/overdue comparisons use the business date from `IClock`. Instants are stored in UTC | Prevents off-by-one around midnight |
| D-15 | Currency | INR, single currency, `decimal(18,2)`, displayed `en-IN` (lakh/crore grouping) | Source implies Indian context |
| D-16 | Opportunity status | `Status` is derived from stage: Qualification/Proposal/Negotiation → `Open`; Won → `Won`; Lost → `Lost`. Stored for query speed, set only by the domain | Source has both Stage and Status |
| D-17 | Probability on close | Won forces 100, Lost forces 0. Open stages accept any 0–100; the UI suggests Qualification 20, Proposal 50, Negotiation 75 | Keeps weighted pipeline honest |
| D-18 | Closed opportunities | Won/Lost are terminal. Only Manager/Admin may reopen (to Negotiation) and it is audited. Amount/date rules apply only while Open | Source rules are scoped to "active" opportunities |
| D-19 | Past close date on edit | The "close date not in the past" rule applies on every save of an Open opportunity, so a stale deal forces the owner to refresh the date | Source §4.8 |
| D-20 | Follow-up past date | New or rescheduled `Planned` follow-ups cannot be before the business today. Completing a past-due follow-up is allowed. Admin/Manager may set a `HistoricalEntry` flag to log an already-completed follow-up with a past date | Source allows an explicit historical process |
| D-21 | Missed follow-ups | A `Planned` follow-up whose date has passed is **Overdue** (derived, shown in red). `Missed` is set explicitly by the user or by Manager bulk action. No background job in v1 | Avoids silent automatic mutation |
| D-22 | API auth | Dual scheme with a policy selector. `Authorization: Bearer` is verified as a JWT (15-minute access token plus rotating 7-day refresh token stored hashed). Otherwise the Identity cookie is used. Cookie-authenticated unsafe API calls require an anti-forgery header | Source needs `/api/auth/login` and a browser-callable API |
| D-23 | Email infrastructure | No SMTP in v1. `IEmailSender` is an abstraction with a logging implementation for dev. Admin password reset sets a temporary password and `MustChangePassword = true` | Source §4.7 permits "reset passwords or trigger secure workflows" |
| D-24 | Hosting | Single container deployable (Dockerfile + compose with SQL Server). Config via environment variables/user-secrets. No secrets in the repo | Source §14 |
| D-25 | Audit storage | Table `AuditLogs`, append-only. Enforced in code (EF interceptor rejects update/delete of audit rows), in DB (migration denies UPDATE/DELETE to the app role where supported, plus a trigger), and in API (no write endpoints) | Source §4.9 |
| D-26 | Reports export | CSV export for all reports (UTF-8 BOM, streamed). No PDF/Excel in v1 | Source: "export where implemented" |
| D-27 | Front-end assets | npm + Dart Sass build to `wwwroot`. All libraries and fonts are self-hosted, none from a CDN, so a strict CSP works | Security + offline dev |
| D-28 | Test DB | Integration tests use Testcontainers SQL Server (Docker required). Filtered indexes and CHECK constraints must be exercised on real SQL Server | EF in-memory/SQLite would hide index bugs |

---

## 3. Functional requirements

### 3.1 Application shell (`SHELL`)
| ID | Requirement | Source |
|---|---|---|
| SHELL-01 | MUST: common layout with responsive sidebar nav, top bar, user menu, notification area, footer. Desktop and tablet are first class; phone is usable | §4.1, §12 |
| SHELL-02 | MUST: nav items are rendered only if the user holds the permission, and the server still enforces it | §4.1 |
| SHELL-03 | MUST: anonymous users are redirected to login with a safe `returnUrl`; open redirects are rejected | §4.1, §17.19-1 |
| SHELL-04 | MUST: consistent alerts (success/error/warning), confirmation modal for destructive actions, pagination component | §4.1, §12 |
| SHELL-05 | SHOULD: global search box (customers, leads, opportunities by name/code) limited to the user's scope | §4.1 |
| SHELL-06 | MUST: friendly error pages for 403/404/500 with a correlation id; no stack traces or exception text | §12, §14 |
| SHELL-07 | SHOULD: header notification bell showing count of overdue and due-today follow-ups for the user | §4.1 |

### 3.2 Authentication and security (`AUTH`)
| ID | Requirement | Source |
|---|---|---|
| AUTH-01 | MUST: login with username **or** email plus password. Inactive users cannot sign in. Generic failure message ("Invalid credentials") that does not reveal which part was wrong | §4.2 |
| AUTH-02 | MUST: logout (POST, anti-forgery) ends the session and redirects to login | §4.2 |
| AUTH-03 | MUST: register per D-07 | §17.9 |
| AUTH-04 | MUST: password policy: min 12 chars in production config (min 8 floor), upper+lower+digit+symbol, not equal to username/email, a small deny-list of common passwords. Values are configurable (`Security:Password:*`) | §6.2 |
| AUTH-05 | MUST: lockout after 5 failed attempts for 15 minutes (configurable). Success resets the counter. Admin can unlock | §6.3 |
| AUTH-06 | MUST: passwords stored only via Identity's hasher. Never logged. Never in DTOs/views | §6, §17.4 |
| AUTH-07 | MUST: auth cookie `HttpOnly`, `Secure`, `SameSite=Lax`, 8h sliding expiry, custom names (`__Host-` prefix in production) | §4.2 |
| AUTH-08 | MUST: global `AutoValidateAntiforgeryToken` on MVC controllers; `ValidateAntiForgeryToken` semantics on every POST form | §17.9 |
| AUTH-09 | MUST: signed-in user can change their own password (requires current password). `MustChangePassword` users are forced to the change screen before anything else | §6.2 |
| AUTH-10 | MUST: login, failed login, lockout, unlock, logout, register, password change and reset write audit entries, never containing secrets | §4.2, §6.3 |
| AUTH-11 | MUST: rate limiting on `/api/auth/*` and `/Account/Login` (fixed-window, per IP, configurable) | §10.1 |
| AUTH-12 | MUST: security headers: CSP (self + nonce, no inline script), `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`/frame-ancestors, HSTS in production | §14 |

### 3.3 Authorisation (`AUTHZ`)
| ID | Requirement | Source |
|---|---|---|
| AUTHZ-01 | MUST: permission catalogue and role mapping per `docs/architecture/SECURITY-AUTHZ.md`; enforced by policy attributes on controllers and by service-level checks | §7 |
| AUTHZ-02 | MUST: record-level scoping. Sales Executive sees only records they own or are assigned; Manager sees own plus team; Admin sees all. Applied in the query (not post-filtered) | §7, §13 |
| AUTHZ-03 | MUST: changing an ID in a URL/API request to a record outside scope returns 404 (not 403) to avoid existence leaks. Unauthorised *actions* on visible records return 403 | §13 |
| AUTHZ-04 | MUST: reassigning an owner requires Manager/Admin. Sales Executives cannot assign records to others. The target owner must be an active user within the assigner's scope | §4.5 |
| AUTHZ-05 | MUST: a Manager cannot modify Admin accounts, change roles, or reach security administration | §7 |

### 3.4 Dashboard (`DASH`)
| ID | Requirement | Source |
|---|---|---|
| DASH-01 | MUST: after login, redirect to Dashboard scoped to the user | §17.11 |
| DASH-02 | MUST: cards: Total Customers, Total Leads, Open Leads, Total Opportunities, Open Opportunities, Won, Lost, Total Pipeline Value. Open Leads = New/Contacted/Qualified. Pipeline Value = Σ Amount of Open opportunities | §17.11 |
| DASH-03 | MUST: Chart.js charts: Lead Status (New, Contacted, Qualified, Lost, Converted, plus Unqualified shown as its own slice), Opportunity Pipeline by stage (count and amount), Monthly Sales (Won amount per month) | §17.12 |
| DASH-04 | MUST: date presets Today, This Week, This Month, Custom, plus All Time. Semantics per §5.4 below. The default preset is This Month | §4.3 |
| DASH-05 | MUST: role flavour. Admin: user/lockout/audit-event counts. Manager: team pipeline by owner. Sales: my upcoming and overdue follow-ups | §4.3 |
| DASH-06 | MUST: all chart data comes from server endpoints that apply scope. The page never embeds unscoped data | §4.3 |

### 3.5 Customers (`CUST`)
| ID | Requirement | Source |
|---|---|---|
| CUST-01 | MUST: create/view/edit/deactivate. Fields per DATA-MODEL (name, company, email, phone, address, city, state, status, owner, notes) | §4.4, §17.3 |
| CUST-02 | MUST: validation per §4 (required, length, email, phone). Name ≤ 150, email ≤ 254, company ≤ 150 | §17.5 |
| CUST-03 | MUST: email and phone uniqueness and duplicate prevention (D-12, D-13) | §17.5 |
| CUST-04 | MUST: list with search (name, email, phone, company), filter (status, owner), sort, pagination | §17.13 |
| CUST-05 | MUST: details page shows a timeline: audit-derived status changes, related leads/opportunities, follow-ups, activities | §4.4 |
| CUST-06 | MUST: status changes (Active/Inactive) are audited. Inactive customers cannot get new opportunities | §4.4 |
| CUST-07 | MUST: deactivate asks for confirmation. It is blocked while the customer has Open opportunities | §12 |

### 3.6 Leads (`LEAD`)
| ID | Requirement | Source |
|---|---|---|
| LEAD-01 | MUST: create/view/edit/deactivate with name, company, email, phone, source, status, priority, expected value, owner, notes | §4.5, §17.3 |
| LEAD-02 | MUST: statuses New, Contacted, Qualified, Unqualified, Converted, Lost. Transitions follow the table in §5.2 and anything else is rejected server-side | §4.5 |
| LEAD-03 | MUST: Expected value in `0 … 100,000,000` (INR); email/phone use the common rules (email may be blank on leads only if phone is present, and vice versa. At least one contact method) | §17.6 |
| LEAD-04 | MUST: assign to a Sales Executive (Manager/Admin; AUTHZ-04). Assignment writes an audit entry | §4.5 |
| LEAD-05 | MUST: convert a **Qualified** lead in one atomic transaction: create a customer (or link an existing one when the user confirms the duplicate suggestion), optionally create an opportunity, mark the lead `Converted` with links to the created records, write an audit entry. Rolls back wholly on any failure | §4.5, §8 |
| LEAD-06 | MUST: list search/filter by name, company, status, assigned user, priority, source | §17.13 |
| LEAD-07 | MUST: Converted and Lost leads are read-only except Manager/Admin reopening a Lost lead to New | §4.5 |

### 3.7 Opportunities (`OPP`)
| ID | Requirement | Source |
|---|---|---|
| OPP-01 | MUST: create/view/edit/deactivate with name, customer, optional source lead, owner, stage, amount, probability, expected close date, source, notes | §4.8, §17.3 |
| OPP-02 | MUST: Amount > 0 while Open, and never negative. Probability 0–100 inclusive. Expected close date ≥ business today while Open (D-19) | §5.3 |
| OPP-03 | MUST: stage movement: Open stages may move freely among themselves. Won/Lost are reachable from any open stage and require a close reason (Lost) or close note (Won). They are terminal (D-18) | §4.8 |
| OPP-04 | MUST: weighted value = `Amount × Probability / 100`, calculated once in the domain and used by UI, API, dashboard and reports | §4.8 |
| OPP-05 | MUST: pipeline board (Kanban by stage, drag or button to move stage) plus table view. Both honour scope and filters | §17.2 |
| OPP-06 | MUST: list search/filter by name, customer, stage, status, owner | §17.13 |
| OPP-07 | MUST: stage changes and reopen are audited with old and new value | §4.8, §4.9 |

### 3.8 Follow-ups (`FU`)
| ID | Requirement | Source |
|---|---|---|
| FU-01 | MUST: schedule against exactly one of customer/lead/opportunity, with date-time, subject, type (Call, Meeting, Email, Visit, Other), notes, assigned user | §4.6 |
| FU-02 | MUST: statuses Planned, Completed, Missed, Cancelled. Only Planned can change, via Complete, Reschedule, Miss or Cancel | §4.6 |
| FU-03 | MUST: new/rescheduled follow-up date not before the business today (D-20) | §5.3 |
| FU-04 | MUST: "Pending" view shows upcoming and overdue, with overdue visually distinct and sorted first | §4.6, §17.2 |
| FU-05 | MUST: complete, reschedule (records old and new date, increments `RescheduleCount`) and miss are audited | §4.6 |
| FU-06 | SHOULD: completing a follow-up offers to log an Activity and to schedule the next follow-up | D-06 |
| FU-07 | MUST: list filter by date range, status, assigned user, related customer/lead | §17.13 |

### 3.9 Activities (`ACT`)
| ID | Requirement | Source |
|---|---|---|
| ACT-01 | MUST: create/edit/view/deactivate. Types Call, Meeting, Email, Task. Fields: subject, description, activity date, status (Planned, Completed, Cancelled), related customer/lead/opportunity (at least one), assigned user | §17.2, §17.3 |
| ACT-02 | MUST: list filter by type, date, status, assigned user | §17.13 |
| ACT-03 | MUST: activities appear in the related record's timeline | §4.4 |

### 3.10 Users and roles (`USR`)
| ID | Requirement | Source |
|---|---|---|
| USR-01 | MUST: Admin creates, edits, activates/deactivates and searches users: name, username, email, role, manager, status | §4.7 |
| USR-02 | MUST: list shows account status and lockout status. Admin can unlock | §4.7, §6.3 |
| USR-03 | MUST: Admin resets a password (temporary password shown once, `MustChangePassword`) | §4.7 |
| USR-04 | MUST: role change is audited with old and new role. An Admin cannot demote or deactivate themselves, and the last active Admin can't be removed | §4.7, §4.9 |
| USR-05 | MUST: user pages and API never expose hashes, security stamps or tokens | §4.7 |
| USR-06 | MUST: Roles page (read-only): the three roles, member counts and the permission matrix | §17.2, D-09 |
| USR-07 | MUST: deactivating a user prompts reassignment of their open leads/opportunities/follow-ups to another user (or leaves them flagged "unassigned-owner" visible to their Manager) | §4.7 |

### 3.11 Audit log (`AUD`)
| ID | Requirement | Source |
|---|---|---|
| AUD-01 | MUST: record user, timestamp (UTC), action, module/entity, record id, result, old value, new value, IP address, details | §4.9, §17.16 |
| AUD-02 | MUST: events: login success/failure, lockout/unlock, logout, create/update/delete of Customer/Lead/Opportunity/FollowUp/Activity, role change, password/security events, lead conversion, stage change, follow-up complete/reschedule | §4.9 |
| AUD-03 | MUST: append-only per D-25 | §4.9 |
| AUD-04 | MUST: Admin UI with filters by user, module, action, result, date range and record id, paginated. Detail view shows an old/new diff | §4.9 |
| AUD-05 | MUST: secrets never stored (passwords, hashes, tokens, reset values are excluded or masked as `***`) | §6.2 |
| AUD-06 | SHOULD: Manager limited view (events about their team's CRM records only, no security events) behind config `Audit:ManagerAccess` (default off) | §7.1 |

### 3.12 REST API (`API`)
Full contract in `docs/api/API-SPEC.md`.
| ID | Requirement | Source |
|---|---|---|
| API-01 | MUST: endpoints for auth, customers, leads, opportunities, follow-ups and reports/pipeline as listed in the Source Spec §10 and §17.14. SHOULD: activities, lead conversion, and refresh | §10 |
| API-02 | MUST: DTOs only. Never EF entities | §4.10 |
| API-03 | MUST: validation at the boundary, RFC 7807 `application/problem+json` errors with field errors, no stack traces | §10.1 |
| API-04 | MUST: status codes 200, 201, 204, 400, 401, 403, 404, 409, 429 as specified per endpoint | §4.10 |
| API-05 | MUST: authorisation per endpoint and record scope identical to the MVC app (same services) | §4.10 |
| API-06 | MUST: OpenAPI document + Swagger UI (non-production, or Admin-only in production) generated from XML docs and annotations | §4.10 |
| API-07 | MUST: pagination envelope, filtering and sorting on list endpoints | §4.10 |
| API-08 | MUST: optimistic concurrency via `ETag`/`If-Match` on PUT (409 on mismatch). MVC uses a hidden `RowVersion` | quality |

### 3.13 Reports (`RPT`)
| ID | Requirement | Source |
|---|---|---|
| RPT-01 | MUST: eight reports: Customer, Lead, Follow-Up, Opportunity, Pipeline (stage-wise, owner-wise, weighted), Conversion, User Activity, Audit | §4.11, §11 |
| RPT-02 | MUST: access per the Source §11 matrix (see SECURITY-AUTHZ). Data always scoped | §11 |
| RPT-03 | MUST: filters (date range, owner, status/stage as relevant), sorting, pagination | §4.11 |
| RPT-04 | MUST: CSV export honouring the active filters and scope (D-26). Cells beginning `= + - @` are escaped against CSV injection | §4.11 |
| RPT-05 | SHOULD: summary row/totals and a chart on Pipeline and Conversion reports | §4.11 |

---

## 4. Validation requirements (`VAL`)

Validation exists on three levels, and the same rule is defined once (in `Domain.Validation`) and reused.

| ID | Requirement |
|---|---|
| VAL-01 | Client-side (jQuery Unobtrusive from Razor/DataAnnotations): required, email, phone, length, date, numeric/range. Inline message beside the field. Submit is blocked |
| VAL-02 | Server-side re-validation of everything. A crafted request that bypasses the browser is rejected with the same messages |
| VAL-03 | Business validation lives in the Domain/Application layers (not in controllers) and returns field-keyed errors |
| VAL-04 | Foreign keys and referenced records are verified (exists, not deleted, in scope, active where relevant) |
| VAL-05 | Text over the maximum length is rejected before DB save and never causes a database truncation error |
| VAL-06 | Messages are user-friendly and exact for the canonical cases below, never exposing database details |

Canonical messages (tests assert these strings verbatim):

| Condition | Message |
|---|---|
| Amount ≤ 0 | `Opportunity Amount must be greater than 0.` |
| Probability outside 0–100 | `Probability must be between 0 and 100.` |
| Expected close date in the past | `Expected Close Date cannot be in the past.` |
| Follow-up date before today | `Follow-up date cannot be earlier than today.` |
| Invalid email | `Enter a valid email address.` |
| Invalid phone | `Enter a valid phone number.` |
| Required blank | `<Field label> is required.` |
| Too long | `<Field label> must not exceed <n> characters.` |
| Duplicate email | `A customer with this email already exists.` |
| Duplicate phone | `A customer with this phone number already exists.` |
| Invalid lead transition | `A lead cannot move from <From> to <To>.` |

---

## 5. Business rules

### 5.1 Ownership and scope
Every owned record has `OwnerId` (Customer, Lead, Opportunity) or `AssignedToId` (FollowUp, Activity). Both expose the common `IOwnedEntity.OwnerId` mapping used by scope filtering. A user's *visible owner set* is: Admin = all; Manager = self + direct reports; SalesExecutive = self.

### 5.2 Lead workflow
| From \ To | New | Contacted | Qualified | Unqualified | Converted | Lost |
|---|---|---|---|---|---|---|
| New | – | ✔ | ✘ | ✔ | ✘ | ✔ |
| Contacted | ✘ | – | ✔ | ✔ | ✘ | ✔ |
| Qualified | ✘ | ✔ | – | ✔ | ✔ (only via conversion) | ✔ |
| Unqualified | ✘ | ✔ | ✘ | – | ✘ | ✔ |
| Converted | ✘ | ✘ | ✘ | ✘ | – | ✘ |
| Lost | ✔ (Manager/Admin) | ✘ | ✘ | ✘ | ✘ | – |

`Converted` can be reached only through the conversion operation, never by editing status.

### 5.3 Opportunity workflow
Create (Qualification) → Proposal → Negotiation → Won / Lost. Backward moves among open stages are allowed. Closing sets `ClosedDate` (business date), forces probability (D-17) and stores the outcome note/reason. See OPP-03 and D-18.

### 5.4 Dashboard period semantics
- Total Customers/Leads/Opportunities, Open Leads, Open Opportunities and Total Pipeline Value are **current snapshots** in scope.
- Each card also shows a secondary figure "+N in period" (created within the selected range).
- Won and Lost show counts of opportunities with `ClosedDate` in the range.
- Monthly Sales charts Won amount by month. For Today/Week/Month presets it shows the trailing 12 months. For Custom it covers the range. For All Time, the trailing 24 months.

---

## 6. Non-functional requirements (`NFR`)

| ID | Requirement |
|---|---|
| NFR-01 | Performance: list pages p95 < 400 ms and dashboard p95 < 800 ms on 50k customers / 100k leads / 100k opportunities seeded locally. All list queries are paginated server-side, indexed and `AsNoTracking` |
| NFR-02 | Security: OWASP ASVS L2-aligned checklist in SECURITY-AUTHZ. No known-vulnerable packages (`dotnet list package --vulnerable` clean in CI) |
| NFR-03 | Reliability: health endpoints `/health/live` and `/health/ready` (DB check). Transactional lead conversion. Idempotent migrations/seed |
| NFR-04 | Observability: Serilog structured logs (console + rolling file), correlation id per request, secrets/PII scrubbed. No logs of request bodies |
| NFR-05 | Accessibility: WCAG 2.2 AA for the primary flows: labels, focus order, visible focus, contrast ≥ 4.5:1, ARIA only where native HTML can't, keyboard operable modals/menus, reduced-motion respected |
| NFR-06 | Compatibility: current Chrome, Edge, Firefox, Safari. Viewports ≥ 768px primary |
| NFR-07 | Maintainability: nullable reference types on, warnings as errors, analyzers on, `.editorconfig`, central package management, ≥ 80% line coverage on Domain and Application |
| NFR-08 | Configurability: every threshold in this document that says "configurable" lives in `appsettings` under typed options classes with validation on startup |
| NFR-09 | Data integrity: DB constraints mirror domain rules (CHECK, FK, filtered unique, `rowversion`) |
| NFR-10 | Deployability: Dockerfile, `docker-compose.yml`, CI (build, test, vulnerability scan, format check) |

---

## 7. Acceptance

The Source Spec acceptance scenario (§17.19, 14 steps) is automated as an end-to-end suite and is the release gate. The traceability from every requirement ID to its tests is in `docs/qa/TEST-STRATEGY.md`. Release criteria:

1. All MUST requirements have at least one passing automated test.
2. The 14-step acceptance scenario passes end-to-end (Playwright).
3. Security checklist (Source §14) reviewed with evidence.
4. No P0/P1 defects open. Static analysis clean. Dependency audit clean.
5. Design review against `docs/design/DESIGN-SYSTEM.md` (including the "no AI-slop" checklist) signed off.

## 8. Milestones

| M | Plan | Outcome |
|---|---|---|
| M0 | 01 Foundation | Solution, CI, DB, Docker, seed, tooling |
| M1 | 02 Identity, 03 Shell & design system | Secure login, roles, layout |
| M2 | 04 Customers, 05 Leads, 06 Opportunities | Core CRM data and workflows |
| M3 | 07 Follow-ups & Activities, 08 Audit | Activity tracking and the audit trail |
| M4 | 09 Users & Roles, 10 REST API | Administration, API |
| M5 | 11 Dashboard & Reports | Analytics |
| M6 | 12 Hardening & Release | Security review, performance, E2E, deploy |

## 9. Deferred (not in v1) and why

| Item | Reason |
|---|---|
| SMTP/email sending, notifications by email | Needs infrastructure/provider decision. `IEmailSender` seam exists |
| 2FA/SSO | Beyond Source Spec. Identity supports adding later |
| Password-reuse history | Source says "consider" (§6.2) |
| Permission editing UI | D-09 |
| Import/export of master data | Not requested |
| Attachments | Not requested |
| Background jobs (auto-mark Missed) | D-21 |
| Multi-tenant, multi-currency | Not requested |

## 10. Risks

| Risk | Mitigation |
|---|---|
| Agent produces generic "template" UI | Design system doc with concrete tokens, a banned-pattern list and a design-review task in plan 03 and plan 12 |
| Scope leaks (owner filtering forgotten on one query) | Single `ScopedQuery` extension, a convention test that fails if a repository exposes an unscoped owned-entity query, and API/MVC IDOR tests for every module |
| Rule drift between MVC and API | Both call the same Application services. Controllers contain no business rules |
| Time-zone bugs on date rules | `IClock` only, no `DateTime.Now/Today` anywhere (analyzer ban list), boundary tests at midnight |
| Docker not available to executor | Fallback documented in plan 01: SQL Server LocalDB/Developer edition via connection string and `TEST_DB_CONNECTION` |

## 11. Glossary
**Scope**: set of owner ids a user may see. **Open (opportunity)**: stage in Qualification/Proposal/Negotiation. **Business date**: calendar date in the configured business time zone. **Weighted pipeline**: Σ Amount × Probability / 100 over Open opportunities.
