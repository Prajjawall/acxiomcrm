# Identity, Authentication and Authorisation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans if your harness has them; otherwise execute task by task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Secure sign-in (login, logout, register, change password), lockout, password policy, role/permission policies and record-level scope.

**Architecture:** ASP.NET Core Identity on `CrmDbContext`. `IAuthService` (Application contract, Infrastructure implementation) performs credential checks, lockout, and audit. The Web layer owns cookie sign-in. Permissions from `RolePermissionMap` become authorisation policies. `OwnerScopeResolver` implements `IOwnerScope`.

**Tech Stack:** ASP.NET Core Identity, cookie authentication, `Microsoft.AspNetCore.RateLimiting`, anti-forgery, xUnit, `WebApplicationFactory`.

**Spec:** PRD AUTH-01..12, AUTHZ-01..05, D-07, D-08, D-22 (cookie half), D-23; `docs/architecture/SECURITY-AUTHZ.md` §1–5.

**Depends on:** Plan 01 and **Plan 08 Part A** (audit core: `IAuditWriter`, `AuditLog`). Run order: 01 → 08A → 02.

## Global Constraints
- Hasher: Identity v3 PBKDF2, `IterationCount = 600_000`.
- Lockout defaults: 5 failures, 15 minutes (`Security:Lockout`). Password defaults: min length 12 in `appsettings.Production.json`, 8 in Development, upper+lower+digit+symbol required.
- Cookie: `.AcxiomCrm.Auth`, `HttpOnly`, `SameSite=Lax`, `Secure` = Always unless Development/Testing, sliding 8 h, absolute 12 h. Prod name prefix `__Host-`.
- Generic failure text for bad credentials: `Invalid credentials.` (same for unknown user, wrong password, inactive).
- Registration always assigns `Roles.SalesExecutive`; never accepts a role from the request.
- No password/hash/token ever in logs, audit `Details`, DTOs, views.
- Controllers contain no business logic; `AccountController` calls `IAuthService` and `ISessionSigner`.

## Review Focus
- Login with the email in different case/whitespace (`" ASHA@acme.in "`) → succeeds; with username → succeeds.
- Deactivated user with the correct password → `Invalid credentials.`, audit `LoginFailed` with `Details = "inactive"`.
- 5th failure locks; correct password during lockout stays locked and does **not** reset the counter; after lockout expiry correct password works and counter is 0.
- `returnUrl=//evil.com`, `https://evil.com`, `/\evil.com` → redirected to the dashboard.
- Register with a crafted `role=Admin` form field or JSON → ignored; created user is SalesExecutive.
- `Registration:Mode=ApprovalRequired`: new account cannot log in until activated; message does not reveal existence of other accounts. `Disabled` → 404.
- Registering an already-used email or username: `Open` mode returns the field error `This email cannot be used.` / `This username is taken.` (accepted enumeration trade-off for a public sign-up form, mitigated by the rate limit). `ApprovalRequired` mode always shows the same "pending approval" page regardless of duplicates and creates nothing for duplicates.
- Cookie issued before a password change/role change stops working within 5 minutes (security stamp).
- Anonymous POST without anti-forgery token → 400.
- Manager calling an Admin-only probe → 403; unauthenticated → redirect to login (MVC) or 401 (`/api`).

---

## Task 1: Identity configuration, password policy and seeding

**Files:**
- Create: `src/AcxiomCrm.Infrastructure/Identity/{IdentitySetup,PasswordPolicyValidator,PasswordDenyList,RoleSeeder,BootstrapAdminSeeder}.cs`
- Modify: `src/AcxiomCrm.Infrastructure/DependencyInjection.cs`, `appsettings*.json` (non-secret defaults)
- Test: `tests/AcxiomCrm.Infrastructure.Tests/{PasswordPolicyTests,PasswordHashingTests,RoleSeederTests,BootstrapAdminTests}.cs`

**Interfaces:**
- Produces: `IdentitySetup.AddCrmIdentity(this IServiceCollection, IConfiguration)`; `public sealed class PasswordPolicyValidator : IPasswordValidator<ApplicationUser>` (rules: not containing UserName or the email local part (case-insensitive), not in `PasswordDenyList.Contains(string)` of ≥ 50 common passwords embedded as a resource); `public sealed class RoleSeeder { Task SeedAsync(CancellationToken ct); }` idempotent, creates the 3 roles; `public sealed class BootstrapAdminSeeder { Task SeedAsync(CancellationToken ct); }` creates the first Admin only if no Admin exists, from `Bootstrap:AdminEmail`/`Bootstrap:AdminPassword`/`Bootstrap:AdminFullName`; throws `InvalidOperationException` (message names the missing/invalid key but not the password) when the password fails policy; no-op when keys are absent and an Admin exists; throws when absent and no Admin exists **in Production**, but logs a warning in Development.
- Hosted `DatabaseInitializer : IHostedService` runs migrate (if enabled) → `RoleSeeder` → `BootstrapAdminSeeder`.

- [x] **Step 1: Write failing tests.**
  - `PasswordPolicyTests` (Theory, `UserManager.CreateAsync` against the fixture DB): `"Sh0rt!a"` fails (length), `"alllowercase123!"` fails (no upper), `"ALLUPPERCASE123!"` fails, `"NoDigitsHere!!!!"` fails, `"NoSymbolsHere1234"` fails, `"Password123!"` fails (deny-list), `"Asha.Rao-2026!x"` for user `asha.rao` fails (contains username), `"Tr1cky-Harbour-77"` passes. Production-config variant: 11 chars fails, 12 passes.
  - `PasswordHashingTests.Stored_hash_is_not_plaintext_and_is_identity_v3`: create user, read `PasswordHash`, assert `!= plaintext`, first byte after base64-decoding is `0x01`, and `PasswordHasher.VerifyHashedPassword` returns `Success`. `Iteration_count_is_600000` reads bytes 5–8 of the payload.
  - `RoleSeederTests.Seeds_three_roles_once`: run twice → exactly 3 roles named `Admin, Manager, SalesExecutive`.
  - `BootstrapAdminTests.Creates_admin_when_none_exists`, `Does_not_create_second_admin`, `Weak_password_throws_without_echoing_password`, `Missing_config_in_Production_throws`.
- [x] **Step 2: Run** `dotnet test tests/AcxiomCrm.Infrastructure.Tests --filter "PasswordPolicyTests|PasswordHashingTests|RoleSeederTests|BootstrapAdminTests"`. Expected: FAIL.
- [x] **Step 3: Implement** `AddCrmIdentity`: `AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<CrmDbContext>().AddSignInManager().AddDefaultTokenProviders().AddPasswordValidator<PasswordPolicyValidator>()`; `User.RequireUniqueEmail = true`; `SignIn.RequireConfirmedAccount = false`; options bound from `PasswordPolicyOptions`/`LockoutOptions`; `Configure<PasswordHasherOptions>(o => o.IterationCount = 600_000)`; `SecurityStampValidatorOptions.ValidationInterval = 5 min`.
- [x] **Step 4: Run** the tests. Expected: PASS.
- [x] **Step 5: Commit** `feat(identity): identity config, policy validator, role and admin seeding`.

---

## Task 2: Current user and owner scope

**Files:**
- Create: `src/AcxiomCrm.Web/Services/HttpCurrentUser.cs`
- Create: `src/AcxiomCrm.Infrastructure/Scope/OwnerScopeResolver.cs`
- Modify: `src/AcxiomCrm.Web/Program.cs` (register `ICurrentUser`, `IOwnerScope`, `IHttpContextAccessor`)
- Test: `tests/AcxiomCrm.Infrastructure.Tests/OwnerScopeResolverTests.cs`, `tests/AcxiomCrm.Web.Tests/HttpCurrentUserTests.cs`
- Create: `tests/AcxiomCrm.TestSupport/TestUsers.cs` (`FakeCurrentUser` already exists from plan 01)

**Interfaces:**
- Consumes: `ICurrentUser`, `IOwnerScope`, `OwnerScope` (plan 01).
- Produces: `public sealed class HttpCurrentUser : ICurrentUser` (claims `NameIdentifier`, `Name`, `Role`; IP from `HttpContext.Connection.RemoteIpAddress` honouring `UseForwardedHeaders`; `CorrelationId` from `HttpContext.Items`); `OwnerScopeResolver(ICurrentUser, CrmDbContext)` caching its result in a scoped field; `TestUsers.CreateStandardAsync(CrmDbContext)` seeding Admin `admin`, Managers `mgr.north`/`mgr.south`, executives `exec.n1`, `exec.n2` (north), `exec.s1` (south), returning a record with their ids.

- [x] **Step 1: Write failing tests.**
  - `OwnerScopeResolverTests`: `Admin_is_All`; `Manager_scope_is_self_plus_direct_reports` (north manager → `{mgr.north, exec.n1, exec.n2}`, never `exec.s1`); `Manager_scope_includes_inactive_reports`; `Executive_scope_is_self_only`; `Anonymous_scope_is_empty_not_All`; `Result_is_cached_within_scope`.
  - `HttpCurrentUserTests.Reads_claims_and_ip`, `Anonymous_has_null_ids_and_IsAuthenticated_false`.
- [x] **Step 2: Run** the filter for these classes. Expected: FAIL.
- [x] **Step 3: Implement** the types. Anonymous/unknown role → `new OwnerScope(false, [])` (fail closed).
- [x] **Step 4: Run.** Expected: PASS.
- [x] **Step 5: Commit** `feat(auth): current user and owner scope resolver`.

---

## Task 3: `IAuthService` (credentials, lockout, register, change password)

**Files:**
- Create: `src/AcxiomCrm.Application/Auth/{IAuthService,AuthModels}.cs`
- Create: `src/AcxiomCrm.Infrastructure/Auth/AuthService.cs`
- Test: `tests/AcxiomCrm.Infrastructure.Tests/{LoginTests,LockoutTests,RegistrationTests,ChangePasswordTests,AuthAuditTests,LoginTimingTests}.cs`

**Interfaces:**
- Produces:
```csharp
public sealed record LoginCommand(string UserNameOrEmail, string Password);
public enum LoginStatus { Succeeded, InvalidCredentials, LockedOut, MustChangePassword }
public sealed record AuthOutcome(LoginStatus Status, string? UserId = null, DateTime? LockoutEndUtc = null);
public sealed record RegisterCommand(string FullName, string UserName, string Email, string Password);
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);
public interface IAuthService {
    Task<AuthOutcome> AuthenticateAsync(LoginCommand cmd, CancellationToken ct = default);   // no cookie work; updates LastLoginDate, audits
    Task<Result<string>> RegisterAsync(RegisterCommand cmd, CancellationToken ct = default);  // Value = new UserId
    Task<Result> ChangePasswordAsync(ChangePasswordCommand cmd, CancellationToken ct = default);   // current user
    Task RecordLogoutAsync(CancellationToken ct = default);
}
```
- Consumes: `IAuditWriter`, `ICurrentUser`, `IClock`, `RegistrationOptions`.
- Audit actions: `Login`, `LoginFailed` (Details `bad-password|unknown-user|inactive`), `Lockout`, `Logout`, `Register`, `PasswordChanged`. Module `Authentication`, EntityName `User`, RecordId = user id (null when unknown). The attempted username is stored in `Details` as `user=<trimmed, max 100 chars>` for failed attempts.

- [x] **Step 1: Write failing tests** (against the fixture DB with `FakeClock`).
  - `LoginTests`: `Succeeds_with_username`, `Succeeds_with_email_in_any_case_and_whitespace`, `Unknown_user_returns_InvalidCredentials`, `Wrong_password_returns_InvalidCredentials`, `Inactive_user_correct_password_returns_InvalidCredentials`, `MustChangePassword_user_returns_MustChangePassword_status`, `Success_sets_LastLoginDate_from_clock`.
  - `LockoutTests`: `Fifth_failure_locks_and_audits_Lockout`, `Correct_password_while_locked_returns_LockedOut_with_end_time`, `Counter_not_reset_by_attempt_during_lockout`, `Works_after_lockout_expires` (advance `FakeClock` by 15 min — Identity uses `TimeProvider`; inject it), `Success_resets_failed_count`.
  - `RegistrationTests`: `Open_mode_creates_active_SalesExecutive`, `Role_cannot_be_chosen` (command has no role; also assert resulting role), `ApprovalRequired_creates_inactive_user`, `Disabled_returns_NotFound_result`, `Duplicate_email_returns_validation_error_on_Email`, `Duplicate_username_returns_validation_error_on_UserName`, `Weak_password_returns_validation_error_on_Password_with_identity_messages`, `Fields_validated`: FullName required (`Full name is required.`), email via `EmailRules`.
  - `ChangePasswordTests`: `Wrong_current_password_fails_on_CurrentPassword`, `Policy_violation_fails_on_NewPassword`, `Success_clears_MustChangePassword_and_audits_PasswordChanged`, `New_equal_to_current_rejected`.
  - `AuthAuditTests.No_audit_row_contains_the_password_or_hash`: after exercising every path, scan all `AuditLogs` text columns for the test passwords and for `PasswordHash` values.
  - `LoginTimingTests.Unknown_user_still_performs_hash_verification`: assert via a spy `IPasswordHasher<ApplicationUser>` (decorator) that `VerifyHashedPassword` was called once for an unknown user (dummy hash).
- [x] **Step 2: Run** `dotnet test tests/AcxiomCrm.Infrastructure.Tests --filter "LoginTests|LockoutTests|RegistrationTests|ChangePasswordTests|AuthAuditTests|LoginTimingTests"`. Expected: FAIL.
- [x] **Step 3: Implement** `AuthService`. Lookup: trim input, if it contains `@` try `FindByEmailAsync` else `FindByNameAsync`, falling back to the other. Use `SignInManager.CheckPasswordSignInAsync(user, pwd, lockoutOnFailure: true)`; map `IsLockedOut` → `LockedOut`, `Succeeded` → `Succeeded` or `MustChangePassword` when the flag is set. Unknown user: verify against a static dummy hash. Registration validates with `EmailRules`/`Limits`, creates with `UserManager.CreateAsync`, adds role. `Disabled` returns `Result.Fail(ErrorKind.NotFound)`. Never put raw `IdentityError.Description` for email/username duplicates into messages that reveal other accounts beyond the Open-mode trade-off documented in Review Focus.
- [x] **Step 4: Run.** Expected: PASS.
- [x] **Step 5: Commit** `feat(auth): authentication service with lockout, registration and password change`.

---

## Task 4: Cookie auth, security headers, anti-forgery, rate limiting, authorisation policies

**Files:**
- Create: `src/AcxiomCrm.Web/Composition/{AuthenticationSetup,AuthorizationSetup,SecurityHeadersSetup,RateLimitSetup}.cs`
- Create: `src/AcxiomCrm.Web/Authorization/{PermissionRequirement,PermissionHandler,PermissionPolicyProvider}.cs`
- Create: `src/AcxiomCrm.Web/Middleware/{SecurityHeadersMiddleware,MustChangePasswordMiddleware}.cs`
- Create: `src/AcxiomCrm.Web/Services/{ISessionSigner,SessionSigner}.cs`
- Modify: `src/AcxiomCrm.Web/Program.cs`
- Test: `tests/AcxiomCrm.Web.Tests/{PermissionMatrixTests,CookieSettingsTests,AntiForgeryTests,SecurityHeadersTests,RateLimitTests,MustChangePasswordGateTests,ReturnUrlTests}.cs`
- Modify: `tests/AcxiomCrm.TestSupport/WebAppFixture.cs` (add `MapProbeEndpoints`, `AsUser(role)` creating a real signed-in cookie client)

**Interfaces:**
- Produces: `PermissionRequirement(string Permission)`; `PermissionHandler` succeeds when `RolePermissionMap.Has(role, permission)` for the user's role claim **and**, for `audit.view.team`, `AuditOptions.ManagerAccess` is true; `PermissionPolicyProvider : IAuthorizationPolicyProvider` creating a policy per known permission string (unknown policy name → fallback to deny-all, never allow); `ISessionSigner { Task SignInAsync(string userId, CancellationToken ct = default); Task SignOutAsync(); }` wrapping `SignInManager.SignInAsync`/`SignOutAsync`; `MustChangePasswordMiddleware` redirects authenticated users with the flag to `/Account/ChangePassword` except for that action, logout and static files (API requests get 403 with problem `password-change-required`).
- Probe endpoints in tests: `GET /_probe/{permission}` for every `Permissions.All`, `RequireAuthorization(permission)`.

- [x] **Step 1: Write failing tests.**
  - `PermissionMatrixTests.Role_gets_expected_status_for_every_permission` (Theory over role × permission): 200 when `RolePermissionMap.Has`, else 403; anonymous: 302 to `/Account/Login?ReturnUrl=…`; anonymous on `/api/_probe/...` style path: 401. `Audit_team_permission_requires_config_flag` with flag on/off.
  - `CookieSettingsTests`: login response `Set-Cookie` contains `httponly`, `samesite=lax`, and (non-Development) `secure`; name equals `.AcxiomCrm.Auth`; `Production_uses_host_prefix`.
  - `AntiForgeryTests`: `POST /Account/Login` without token → 400; with token from GET → proceeds; `POST /Account/Logout` without token → 400.
  - `SecurityHeadersTests`: every response (including 404 and static) has `Content-Security-Policy` equal to the SECURITY-AUTHZ §5 policy with a per-request nonce, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`; no `Server` header; HSTS only when `Production`.
  - `RateLimitTests`: 11th login POST within a minute from one IP → 429 with `Retry-After`; other IP unaffected (header `X-Forwarded-For` via test `ForwardedHeaders` config).
  - `MustChangePasswordGateTests`: flagged user requesting `/Dashboard` → 302 `/Account/ChangePassword`; can post the change form; after change reaches dashboard.
  - `ReturnUrlTests` (Theory) per Review Focus.
- [x] **Step 2: Run** `dotnet test tests/AcxiomCrm.Web.Tests --filter "PermissionMatrixTests|CookieSettingsTests|AntiForgeryTests|SecurityHeadersTests|RateLimitTests|MustChangePasswordGateTests|ReturnUrlTests"`. Expected: FAIL (Account controller arrives in Task 5, so write the controller stub used by tests in this task: `GET/POST /Account/Login`, `POST /Account/Logout`, `GET/POST /Account/ChangePassword` returning minimal views; they are completed in Task 5).
- [x] **Step 3: Implement** the setup classes: cookie scheme options from SECURITY-AUTHZ §4; `AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))`; nonce generated per request into `HttpContext.Items["csp-nonce"]` and exposed by a `CspNonceTagHelper` for `<script>` tags; `AddRateLimiter` fixed window policy `auth`, applied with `[EnableRateLimiting("auth")]`; `UseForwardedHeaders` configured from `ForwardedHeaders:KnownProxies`.
- [x] **Step 4: Run.** Expected: PASS.
- [x] **Step 5: Commit** `feat(security): cookie auth, permission policies, headers, anti-forgery, rate limiting`.

---

## Task 5: Account MVC flows (login, register, logout, change password, access denied)

**Files:**
- Create: `src/AcxiomCrm.Web/Controllers/AccountController.cs`
- Create: `src/AcxiomCrm.Web/ViewModels/Account/{LoginViewModel,RegisterViewModel,ChangePasswordViewModel}.cs`
- Create: `src/AcxiomCrm.Web/Views/Account/{Login,Register,ChangePassword,AccessDenied,RegistrationPending}.cshtml`, `src/AcxiomCrm.Web/Views/Shared/_AuthLayout.cshtml` (structure only; styling by plan 03)
- Create: `src/AcxiomCrm.Web/Controllers/DashboardController.cs` (stub `Index` guarded by `dashboard.view`, replaced by plan 11)
- Test: `tests/AcxiomCrm.Web.Tests/{AccountLoginFlowTests,AccountRegisterFlowTests,LogoutTests,ChangePasswordFlowTests}.cs`

**Interfaces:**
- Consumes: `IAuthService`, `ISessionSigner`, `RegistrationOptions`.
- Produces: routes `/Account/Login` (GET/POST, `returnUrl`), `/Account/Register`, `/Account/Logout` (POST), `/Account/ChangePassword`, `/Account/AccessDenied`. ViewModels use DataAnnotations: `[Required(ErrorMessage = "Email or username is required.")]`, `[StringLength]` using `Limits`, `[DataType(DataType.Password)]`, `[Compare]` for confirm password, messages from `Messages`.
- Behaviour: successful login → `ISessionSigner.SignInAsync`, redirect to local `returnUrl` else `/Dashboard`; `LockedOut` → form error `Your account is temporarily locked. Try again in <n> minutes.`; `InvalidCredentials` → `Invalid credentials.` (model-level); `MustChangePassword` → sign in then redirect to ChangePassword. Logout → `RecordLogoutAsync`, sign out, redirect to Login. Login/Register POST are `[EnableRateLimiting("auth")]`.

- [x] **Step 1: Write failing tests.**
  - `AccountLoginFlowTests`: `Valid_login_redirects_to_dashboard_and_sets_cookie`, `Invalid_login_shows_generic_error_without_cookie`, `Locked_account_shows_lockout_message`, `Returns_to_local_returnUrl`, `Authenticated_user_visiting_login_is_redirected_to_dashboard`, `Anonymous_dashboard_redirects_to_login_with_returnUrl`.
  - `AccountRegisterFlowTests`: `Open_mode_registers_then_logs_in_to_dashboard`, `ApprovalRequired_shows_pending_page_and_cannot_login`, `Disabled_returns_404`, `Posted_role_field_is_ignored`, `Invalid_fields_redisplay_with_field_errors` (assert `data-val-*` attributes rendered on the inputs for client-side validation: required, email, length, regex-free).
  - `LogoutTests.Post_logs_out_and_audits`, `Get_logout_is_405`.
  - `ChangePasswordFlowTests.Wrong_current_password_shows_error`, `Success_shows_flash_and_invalidates_other_sessions` (security stamp updated, old cookie rejected after validation interval is forced to 0 in the test).
- [x] **Step 2: Run** the filter for these classes. Expected: FAIL.
- [x] **Step 3: Implement** the controller/views. Views use semantic HTML and the `ax-*` class names from DESIGN-SYSTEM §4 (`ax-field`, `ax-btn`, `ax-flash`) without styling; every input has a `<label for>`, `autocomplete` attributes (`username`, `current-password`, `new-password`), and `aria-describedby` for validation spans. No inline scripts; include validation scripts via the layout's `scripts` section.
- [x] **Step 4: Run.** Expected: PASS.
- [x] **Step 5: Commit** `feat(auth): account login, register, logout, change-password flows`.

---

## Task 6: Plan exit

- [ ] **Step 1: Walk the flows manually** with the dev DB: register, login, lockout (5 bad passwords), unlock via DB `LockoutEnd = NULL` (UI unlock comes in plan 09), logout. Observe audit rows in `AuditLogs`.
- [ ] **Step 2: Run** Exit checks, tick roadmap, commit `docs: close plan 02`.

## Exit checks
1. `dotnet test` green. New classes present: all in the AUTH/AUTHZ rows of `docs/qa/TEST-STRATEGY.md` §4 except `UnlockTests`, `ManagerCannotAdministerTests`, `AuthzApi*` (plans 09/10).
2. `SELECT TOP 20 * FROM AuditLogs ORDER BY AuditLogId DESC` shows `Login`, `LoginFailed`, `Lockout`, `Register`, `Logout` rows and no secrets.
3. `curl -sI localhost:<port>/Account/Login` shows CSP, nosniff, referrer-policy; no `Server`.
4. `git grep -n "Html.Raw" src/` empty; `git grep -nE "(?i)password" src/ --include=*.cs | grep -i "log"` shows no logging of password values (manual skim).
