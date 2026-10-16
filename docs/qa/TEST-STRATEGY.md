# AcxiomCRM — Test Strategy and Traceability

## 1. Pyramid and tooling

| Level | Project | Tooling | What it proves |
|---|---|---|---|
| Unit (pure) | `Domain.Tests` | xUnit, plain `Assert`/Shouldly, `TheoryData` | Rules: phone/email normalisation, lead workflow, opportunity/follow-up rules, weighted amount, permission map |
| Service | `Application.Tests` | xUnit + Testcontainers SQL Server (`SqlServerFixture`, one container per run, one database per test class via respawn) | Services against real SQL Server: validation, scope, duplicates, transactions, audit |
| Infrastructure | `Infrastructure.Tests` | same fixture | Migrations apply, constraints/CHECKs/filtered indexes behave, interceptor, audit immutability, seeders |
| Integration | `Web.Tests` | `WebApplicationFactory<Program>`, fixture DB, `TestAuthHandler` + real cookie/JWT flows for auth tests | MVC and API through the HTTP pipeline: authz, anti-forgery, validation, status codes, headers |
| E2E | `E2E` | Playwright for .NET (Chromium; Firefox/WebKit nightly), axe-core | The 14-step acceptance scenario, client-side validation, keyboard paths, accessibility |
| Static | CI | `dotnet format --verify-no-changes`, analyzers, `dotnet list package --vulnerable --include-transitive`, `npm audit --omit=dev` | Hygiene |

Frameworks banned: FluentAssertions (licence), Moq (telemetry history). Use Shouldly and NSubstitute where a double is truly needed. Prefer real implementations and fakes (`FakeClock`, `FakeCurrentUser`) over mocks.

`TestSupport` provides: `SqlServerFixture`, `TestDb` (reset between tests), `FakeClock(DateTime utcNow, string tz)`, `UserBuilder`, `CustomerBuilder`, `LeadBuilder`, `OpportunityBuilder`, `FollowUpBuilder`, `TestUsers` (admin, two managers with teams, three executives), `ServiceHarness` (builds the service graph with a chosen current user), `WebAppFixture` + `HttpClientExtensions.AsUser(role)`.

## 2. Rules for every test
- Name: `Method_Condition_Expected`. One behaviour per test.
- Time: only `FakeClock`. Boundary cases at business-midnight (23:59/00:00 `Asia/Kolkata`, and the UTC offset day mismatch).
- Authorisation matrix tests are table-driven across the three roles plus anonymous.
- Each module has **IDOR tests** (SECURITY-AUTHZ §3.6).
- Canonical validation messages are asserted verbatim (PRD §4).
- No sleeps. No order dependence. Tests must pass in parallel per class.

## 3. Coverage gates (CI)
Domain ≥ 90% line, Application ≥ 80% line, whole solution ≥ 70%. Mutation testing is out of scope.

## 4. Traceability matrix
Every PRD requirement maps to the named test class(es). Plans create these classes; plan 12 verifies none is missing (`scripts/check-traceability.ps1`/`.sh` greps the matrix against test names).

| Requirement | Test class(es) |
|---|---|
| SHELL-01..05 | `ShellLayoutTests` (Web), `NavigationVisibilityTests`, E2E `AcceptanceScenario` |
| SHELL-03 | `ReturnUrlTests` |
| SHELL-06 | `ErrorPagesTests` |
| SHELL-07 | `NotificationBellTests` |
| AUTH-01 | `LoginTests`, `LoginTimingTests` |
| AUTH-02 | `LogoutTests` |
| AUTH-03 | `RegistrationTests` (3 modes, role fixed) |
| AUTH-04 | `PasswordPolicyTests` |
| AUTH-05 | `LockoutTests`, `UnlockTests` |
| AUTH-06 | `PasswordHashingTests`, `LogMaskingTests` |
| AUTH-07 | `CookieSettingsTests` |
| AUTH-08 | `AntiForgeryTests` |
| AUTH-09 | `ChangePasswordTests`, `MustChangePasswordGateTests` |
| AUTH-10 | `AuthAuditTests` |
| AUTH-11 | `RateLimitTests` |
| AUTH-12 | `SecurityHeadersTests` |
| AUTHZ-01 | `PermissionMatrixTests`, `RolePermissionMapTests` |
| AUTHZ-02..04 | `OwnerScopeTests`, `*IdorTests` per module, `AssignmentRulesTests` |
| AUTHZ-05 | `ManagerCannotAdministerTests` |
| DASH-01..06 | `DashboardServiceTests`, `DashboardScopeTests`, `DashboardEndpointsTests`, E2E |
| CUST-01..07 | `CustomerServiceTests`, `CustomerValidationTests`, `CustomerDuplicateTests`, `CustomersControllerTests`, `CustomerIdorTests`, `CustomerTimelineTests` |
| LEAD-01..07 | `LeadServiceTests`, `LeadWorkflowTests` (Domain), `LeadConversionTests` (incl. rollback), `LeadAssignmentTests`, `LeadIdorTests` |
| OPP-01..07 | `OpportunityRulesTests` (Domain), `OpportunityServiceTests`, `OpportunityStageTests`, `OpportunityBoardTests`, `OpportunityIdorTests` |
| FU-01..07 | `FollowUpRulesTests` (Domain), `FollowUpServiceTests`, `FollowUpAuditTests`, `FollowUpIdorTests` |
| ACT-01..03 | `ActivityServiceTests`, `ActivityIdorTests`, `TimelineTests` |
| USR-01..07 | `UserAdminServiceTests`, `LastAdminGuardTests`, `UserAdminAuthorisationTests`, `UserResponseShapeTests`, `RolesPageTests` |
| AUD-01..06 | `AuditInterceptorTests`, `AuditImmutabilityTests`, `AuditCoverageTests`, `AuditQueryTests`, `AuditSecretMaskingTests`, `AuditManagerAccessTests` |
| API-01..08 | `AuthApiTests`, `CustomersApiTests`, `LeadsApiTests`, `OpportunitiesApiTests`, `FollowUpsApiTests`, `ReportsApiTests`, `ProblemDetailsTests`, `ConcurrencyApiTests`, `OpenApiContractTests`, `ApiResponseShapeTests` |
| RPT-01..05 | `*ReportTests` (eight), `ReportAccessMatrixTests`, `CsvExportTests` (injection), `ReportScopeTests` |
| VAL-01 | E2E `ClientValidationTests` |
| VAL-02..06 | `ServerValidationTests` (crafted requests), `ValidationMessagesTests` |
| NFR-01 | `scripts/perf/seed-large.sql` + k6/`dotnet` smoke in plan 12 |
| NFR-03 | `HealthEndpointsTests`, `LeadConversionTests` rollback |
| NFR-05 | E2E `AccessibilityTests` (axe) |
| NFR-07 | `ArchitectureTests` (NetArchTest) |
| NFR-08 | `OptionsValidationTests` |
| NFR-09 | `ConstraintTests` (Infrastructure) |

## 5. The release-gate scenario (Source §17.19) as E2E

`AcceptanceScenario` runs these 14 steps in order against a fresh DB with the dev seed: (1) anonymous request to `/Customers` redirects to login; (2) register + login lands on Dashboard; (3) invalid email/phone shows inline errors and blocks submit; (4) crafted POST with invalid data is rejected 400/redisplay; (5) amount ≤ 0 rejected; (6) probability 101 rejected; (7) past close date rejected; (8) follow-up dated yesterday rejected; (9) executive sees only own scope; (10) manager sees team pipeline and reports; (11) admin sees users/roles/audit; (12) create/update/delete a record produces audit entries; (13) `/api/customers` returns authorised JSON (cookie and bearer); (14) dashboard shows 8 KPI cards and 3 charts with data.

## 6. Manual verification checklist (plan 12)
Keyboard-only run of login → create lead → convert → create follow-up. Screen-reader spot check of one form and one table (NVDA). 768 px and 1280 px screenshots reviewed against the anti-slop checklist. Dependency and secret scans. Backup/restore of the DB volume.
