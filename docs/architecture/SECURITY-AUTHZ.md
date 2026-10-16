# AcxiomCRM — Security and Authorisation Specification

## 1. Permission catalogue (`Domain/Security/Permissions.cs`)

Permission strings are `module.action`. Each becomes an ASP.NET Core authorisation policy of the same name.

| Permission | Meaning |
|---|---|
| `dashboard.view` | see the dashboard (scoped) |
| `customers.view` `customers.create` `customers.edit` `customers.delete` | |
| `leads.view` `leads.create` `leads.edit` `leads.delete` `leads.assign` `leads.convert` | |
| `opportunities.view` `.create` `.edit` `.delete` `.reopen` | |
| `followups.view` `.create` `.edit` `.delete` `.historical` | `historical` = create completed/past entries (D-20) |
| `activities.view` `.create` `.edit` `.delete` | |
| `users.view` `users.manage` `users.unlock` `users.resetpassword` | |
| `roles.view` | |
| `audit.view` `audit.view.team` | `team` = Manager limited view (AUD-06) |
| `reports.customers` `.leads` `.followups` `.opportunities` `.pipeline` `.conversion` `.useractivity` `.audit` | |
| `api.access` | may call the REST API |

## 2. Role → permission mapping

`✔` = granted, `T` = granted with team scope, `O` = granted with own scope. Scope is a record-level rule applied in queries (see §3), not a permission difference.

| Permission group | Admin | Manager | SalesExecutive |
|---|---|---|---|
| dashboard.view | ✔ all | ✔ team | ✔ own |
| customers.view/create/edit | ✔ | ✔ T | ✔ O |
| customers.delete | ✔ | ✔ T | ✘ |
| leads.view/create/edit | ✔ | ✔ T | ✔ O |
| leads.delete | ✔ | ✔ T | ✘ |
| leads.assign | ✔ | ✔ T | ✘ |
| leads.convert | ✔ | ✔ T | ✔ O |
| opportunities.view/create/edit | ✔ | ✔ T | ✔ O |
| opportunities.delete | ✔ | ✔ T | ✘ |
| opportunities.reopen | ✔ | ✔ T | ✘ |
| followups.view/create/edit | ✔ | ✔ T | ✔ O |
| followups.delete | ✔ | ✔ T | ✔ O (own planned only) |
| followups.historical | ✔ | ✔ | ✘ |
| activities.* | ✔ | ✔ T | ✔ O |
| users.view | ✔ | ✔ (team members only, read-only, config) | ✘ |
| users.manage / unlock / resetpassword | ✔ | ✘ | ✘ |
| roles.view | ✔ | ✘ | ✘ |
| audit.view | ✔ | ✘ | ✘ |
| audit.view.team | ✘ | ✔ only if `Audit:ManagerAccess` | ✘ |
| reports.customers/leads/followups/opportunities | ✔ | ✔ T | ✔ O |
| reports.pipeline/conversion | ✔ | ✔ T | ✔ O (scoped view) |
| reports.useractivity | ✔ | ✔ T | ✘ |
| reports.audit | ✔ | ✘ | ✘ |
| api.access | ✔ | ✔ | ✔ |

This table is encoded once in `RolePermissionMap.cs` and a test asserts it cell by cell against this document.

## 3. Record-level scope rules

`IOwnerScope.ResolveAsync()`:
- Admin → `OwnerScope(All: true, [])`.
- Manager → `All: false`, `OwnerIds = {self} ∪ {users where ManagerId = self and IsActive}` (inactive reports are still included so their historical records remain visible; both active and inactive direct reports count).
- SalesExecutive → `{self}`.

Rules every service follows:
1. Reads: `query.InScope(scope)` before any filtering. Single-record reads that miss return `Result.Fail(NotFound)`; never `Forbidden` (AUTHZ-03).
2. Writes: load via the scoped query. Missing → NotFound. Permission missing → Forbidden.
3. Owner assignment: the target owner must exist, be active, and (for Manager) be in the manager's scope. Sales executives cannot set an owner other than themselves.
4. Related-record references (customer on an opportunity, lead/customer on a follow-up) must also be in the caller's scope, otherwise `Validation` error on the referencing field (so IDs can't be probed).
5. Names of *related* records shown on a record the user can already see (e.g. the customer name on their opportunity) are joined without scope, because the foreign key is already on a record they own. Only the name/code is projected, never other fields.
6. Safety net: every module has IDOR integration tests (MVC and API) that create a record owned by user B and assert user A, and a Manager of a different team, get 404 on read/update/delete and that list/search/report/export/dashboard results exclude it. A new owned-entity query without `InScope` fails these tests. There is no global EF filter for scope, because joins across owners (rule 5) would silently drop rows.

## 4. Authentication specification

| Topic | Spec |
|---|---|
| Hasher | ASP.NET Core Identity `PasswordHasher<TUser>` v3 (PBKDF2-HMAC-SHA512, ≥ 600k iterations; configure `PasswordHasherOptions.IterationCount = 600_000`) |
| Policy | `PasswordOptions`: from config. Custom `IPasswordValidator<ApplicationUser>` adds deny-list and "not username/email" rules |
| Lockout | `LockoutOptions.AllowedForNewUsers = true`, from config. `lockoutOnFailure: true` on `PasswordSignInAsync` |
| Login by email or username | resolve user by `FindByEmailAsync` or `FindByNameAsync`; do the same hashing work if the user is not found (dummy hash verify) to blunt timing differences |
| Cookie | name `.AcxiomCrm.Auth` (`__Host-AcxiomCrm.Auth` in prod), `HttpOnly`, `SecurePolicy=Always` (prod), `SameSite=Lax`, sliding 8 h, absolute 12 h, `LoginPath`/`AccessDeniedPath` set, `Events` return 401/403 JSON for `/api` |
| Anti-forgery | `AutoValidateAntiforgeryTokenAttribute` global; cookie `__Host-AcxiomCrm.Csrf`; header name `X-CSRF-TOKEN` for cookie-authed API unsafe methods |
| Post-login redirect | `Url.IsLocalUrl` check, else dashboard |
| Security stamp | validated every 5 minutes (`SecurityStampValidatorOptions.ValidationInterval`) so deactivation/role change/password reset invalidate sessions quickly |
| JWT | HS256 with a ≥ 32 byte key from secrets. Claims: `sub`, `name`, `role`, `jti`, `sec_stamp` hash. Validate issuer, audience, lifetime, `ClockSkew = 30 s`. Short access tokens plus rotating refresh tokens. Reuse of a revoked refresh token revokes the whole chain and writes an audit event |
| Rate limiting | `AddRateLimiter` fixed-window per IP: policy `auth` (default 10/min) on `/Account/Login`, `/Account/Register`, `/api/auth/*`; returns 429 with `Retry-After` |

## 5. Transport and browser hardening

- HTTPS redirection + HSTS (1 year, includeSubDomains) in production; `UseForwardedHeaders` with configured known proxies.
- CSP: `default-src 'self'; script-src 'self' 'nonce-…'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'`. No `unsafe-inline`.
- `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` disabling camera/mic/geolocation, `Cross-Origin-Opener-Policy: same-origin`. Remove `Server` header.
- Razor auto-encodes. `Html.Raw` is banned except in a reviewed helper. Audit JSON diff is rendered through the encoder.
- CSV export escapes formula-leading characters.

## 6. Data protection
- Data Protection keys persisted to the database/volume (not ephemeral container storage) so cookies survive restarts.
- Secrets only via env/secret store. `appsettings.json` contains no secrets. A test fails if `appsettings*.json` contains keys matching `password|secret|signingkey` with non-empty values.
- Logs: no request/response bodies, no `Authorization` header, masked destructuring.

## 7. Source Spec security checklist (§14) → control → evidence

| Checklist item | Control | Evidence (test / artifact) |
|---|---|---|
| Password hashing enabled | Identity PBKDF2 (§4) | `PasswordHashingTests` asserts stored value ≠ plaintext and verifies as `PasswordHasher` v3 |
| Password policy enabled | Options + validator | `PasswordPolicyTests` (table-driven) |
| Account lockout enabled | LockoutOptions | `LockoutTests` (5 failures → locked, unlock) |
| Role-based authorisation | Policies + scope | `PermissionMatrixTests`, `ScopeTests`, IDOR tests |
| Server-side validation | Services | `ServerValidationTests` crafted-request tests |
| Anti-forgery enabled | Global filter | `AntiForgeryTests` POST without token → 400 |
| Sensitive info excluded from logs | Masking | `LogMaskingTests` |
| HTTPS in production | HSTS, redirect, cookie Secure | `SecurityHeadersTests`, deploy checklist |
| API authz + validation | Controllers/services | `Api*Tests` |
| Audit logging for security events | Interceptor + explicit | `AuditCoverageTests` |
| Least privilege | Matrix | `PermissionMatrixTests` |
| DB credentials not in source | Config policy | `NoSecretsInConfigTests`, `.gitignore` |

## 8. Threat checklist (ASVS-aligned) for plan 12
IDOR on every `{id}` route (MVC and API). Mass assignment (ViewModels/DTOs have no `OwnerId`/`Status` bypass fields unless the role may set them). CSRF on cookie-authed API. Open redirect. User enumeration (login, register, reset). Brute force (lockout + rate limit). Session fixation (Identity rotates). XSS (encoder, CSP). SQLi (EF only). CSV injection. Log injection (structured logging, no concatenation). SSRF: n/a (no outbound calls). File upload: n/a (out of scope).
