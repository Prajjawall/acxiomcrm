# AcxiomCRM — Architecture

Companion to `docs/prd/PRD.md`. This document fixes names, boundaries and contracts. Plans refer to these names verbatim. If a plan needs a different name, change it here first.

## 1. Solution layout

```
AcxiomCrm.sln
global.json                      # pins .NET 10 SDK
Directory.Build.props            # nullable, warnings-as-errors, analyzers, LangVersion
Directory.Packages.props         # central package management (all versions here)
.editorconfig
AGENTS.md
docker-compose.yml  Dockerfile  .dockerignore  .gitignore
.github/workflows/ci.yml
src/
  AcxiomCrm.Domain/              # entities, enums, rules. References nothing.
  AcxiomCrm.Application/         # services, DTOs, abstractions. References Domain.
  AcxiomCrm.Infrastructure/      # EF Core, Identity, audit, clock, JWT. References Application.
  AcxiomCrm.Web/                 # MVC + API controllers, ViewModels, Views, assets. References Application + Infrastructure (composition root only).
tests/
  AcxiomCrm.Domain.Tests/
  AcxiomCrm.Application.Tests/   # services against a real SQL Server via fixture
  AcxiomCrm.Infrastructure.Tests/
  AcxiomCrm.Web.Tests/           # WebApplicationFactory integration: MVC, API, authz
  AcxiomCrm.E2E/                 # Playwright (.NET), the 14-step acceptance scenario
  AcxiomCrm.TestSupport/         # SqlServerFixture, builders, fake clock, auth helpers
docs/ ...
```

Mapping to the Source Spec's evaluation list ("Controllers, Models, Data, Services, ViewModels, Views"): Models → `Domain/Entities`, Data → `Infrastructure/Persistence`, Services → contracts in `Application/*`, implementations in `Infrastructure/<Module>/`, ViewModels/Controllers/Views → `Web`.

Dependency rule: arrows only point inward (`Web → Application → Domain`; `Infrastructure → Application → Domain`). `Web` references `Infrastructure` **only** in `Program.cs`/`Composition/*` to register services. An architecture test (NetArchTest) enforces this.

## 2. Layer responsibilities

| Layer | Contains | Must not contain |
|---|---|---|
| Domain | Entities, enums, value helpers, `Limits`, `PhoneRules`, `EmailRules`, `LeadWorkflow`, `OpportunityRules`, `FollowUpRules`, `Permissions`, `Roles` | EF, ASP.NET, I/O, `DateTime.Now` |
| Application | Service **contracts** (`IXService`), DTOs/commands, `Result`, paging, `IClock`, `ICurrentUser`, `IOwnerScope`, `IAuditWriter`, `ICrmDbContext` | HTTP types, Razor, concrete `DbContext`, SQL Server specifics |
| Infrastructure | Module **service implementations** (`Infrastructure/Customers/CustomerService.cs`, …: they use EF queries and SQL Server error codes), `CrmDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>, ICrmDbContext`, configurations, migrations, interceptors, `SystemClock`, `CurrentUser`, JWT/refresh tokens, seeders, `LoggingEmailSender` | Pure business rules (those belong in Domain). Services only orchestrate |
| Web | Controllers (MVC `Areas` not used, folders per module), API controllers under `Controllers/Api`, ViewModels, Razor views, tag helpers, filters, middleware, `wwwroot`, front-end build | Business rules, direct `DbContext` |

`ApplicationUser : IdentityUser` lives in **Infrastructure/Identity**, so Domain stays free of ASP.NET packages. Domain refers to users only by `string UserId`. (Decision A-01.)

## 3. Cross-cutting contracts

All in `AcxiomCrm.Application` unless stated. Namespaces are `AcxiomCrm.Application.<Area>`.

```csharp
// Common
public enum ErrorKind { None, Validation, NotFound, Forbidden, Conflict }
// FieldError is defined in Domain (AcxiomCrm.Domain.Validation) so domain rules can return it
public sealed record FieldError(string Field, string Message);          // Field "" = form-level
public class Result { bool Succeeded; ErrorKind Kind; IReadOnlyList<FieldError> Errors;
    static Result Ok(); static Result Fail(ErrorKind kind, params FieldError[] errors); }
public sealed class Result<T> : Result { T? Value; static Result<T> Ok(T value); static new Result<T> Fail(...); }
public sealed record PageQuery(int Page = 1, int PageSize = 25, string? Sort = null, bool Desc = false);   // PageSize clamped 1..100
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

// Ambient
public interface IClock { DateTime UtcNow { get; } DateOnly Today { get; } /* business zone */ DateTime ToBusinessLocal(DateTime utc); }
public interface ICurrentUser { string? UserId { get; } string? UserName { get; } string? Role { get; } bool IsAuthenticated { get; } string? IpAddress { get; } string? CorrelationId { get; } }

// Scope
public sealed record OwnerScope(bool All, IReadOnlyCollection<string> OwnerIds) { bool Contains(string ownerId); }
public interface IOwnerScope { Task<OwnerScope> ResolveAsync(CancellationToken ct = default); }   // cached per request
public static class ScopeExtensions { public static IQueryable<T> InScope<T>(this IQueryable<T> q, OwnerScope s) where T : IOwnedEntity; }

// Audit
public sealed record AuditEntry(string Action, string EntityName, string? RecordId, AuditResult Result,
    string? OldValue = null, string? NewValue = null, string? Details = null, string? UserIdOverride = null);
public interface IAuditWriter { Task WriteAsync(AuditEntry entry, CancellationToken ct = default); }

// Data access seam used by all services
public interface ICrmDbContext {
    DbSet<Customer> Customers { get; } DbSet<Lead> Leads { get; } DbSet<Opportunity> Opportunities { get; }
    DbSet<FollowUp> FollowUps { get; } DbSet<Activity> Activities { get; } DbSet<AuditLog> AuditLogs { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    IQueryable<UserSummary> Users { get; }   // projection view over Identity users, never exposes hashes
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}
```

`Application` references `Microsoft.EntityFrameworkCore` (abstractions only, for `DbSet`/`IQueryable` in `ICrmDbContext`): EF *is* the repository and unit of work, so there is no generic repository layer (DRY/YAGNI). Services (in Infrastructure) own the rules orchestration; pure rules stay in Domain.

### Domain base types
```csharp
public abstract class Entity { public int Id { get; protected set; } }
public interface IOwnedEntity { string OwnerId { get; } }
public interface ISoftDeletable { bool IsDeleted { get; } DateTime? DeletedDate { get; } string? DeletedBy { get; } }
public interface IAuditedEntity { }          // marker: captured by the audit interceptor
public interface IHasTimestamps { DateTime CreatedDate { get; } string CreatedBy { get; } DateTime? ModifiedDate { get; } string? ModifiedBy { get; } }
```
Timestamps, soft-delete flags and `RowVersion` are set by the `AuditSaveChangesInterceptor`/`ConcurrencyAndStamps` logic in Infrastructure, never by controllers.

### Service naming and signature conventions
For each module `X` in {Customer, Lead, Opportunity, FollowUp, Activity}:
```csharp
public interface IXService {
    Task<PagedResult<XListItem>> SearchAsync(XFilter filter, PageQuery page, CancellationToken ct = default);
    Task<Result<XDetail>>        GetAsync(int id, CancellationToken ct = default);          // NotFound when out of scope
    Task<Result<XDetail>>        CreateAsync(CreateXCommand cmd, CancellationToken ct = default);
    Task<Result<XDetail>>        UpdateAsync(int id, UpdateXCommand cmd, CancellationToken ct = default);   // cmd carries RowVersion
    Task<Result>                 DeleteAsync(int id, CancellationToken ct = default);       // soft delete
}
```
`XListItem`, `XDetail`, `XFilter`, `CreateXCommand`, `UpdateXCommand` are records in `Application/X/`. Module-specific operations (lead `ConvertAsync`/`AssignAsync`/`ChangeStatusAsync`, opportunity `ChangeStageAsync`/`ReopenAsync`, follow-up `CompleteAsync`/`RescheduleAsync`/`MarkMissedAsync`/`CancelAsync`) are defined in their plans' **Interfaces** blocks.

Services own: authorisation (permission + scope), validation, business rules, persistence, audit. Controllers only translate HTTP ↔ commands/results.

`Result.Kind` → HTTP: Validation 400, NotFound 404, Forbidden 403, Conflict 409. MVC: Validation → add to `ModelState` and redisplay, NotFound → 404 view, Forbidden → 403 view, Conflict → concurrency/duplicate message.

## 4. Request flow (example: create opportunity)

`POST /Opportunities/Create` → anti-forgery filter → `[Authorize(Policy = Permissions.OpportunitiesCreate)]` → model binding + DataAnnotations (client-mirrored) → `OpportunityService.CreateAsync` (permission re-check, scope/ownership, `OpportunityRules.Validate`, customer exists/active/in-scope, save, audit via interceptor) → `Result` → redirect-after-post with a flash alert or redisplay.

## 5. Persistence

- One `CrmDbContext`. Configurations in `Infrastructure/Persistence/Configurations/*Configuration.cs` using `IEntityTypeConfiguration<T>`.
- Global query filter for `ISoftDeletable`. Admin lookups use `IgnoreQueryFilters()` explicitly and sparingly.
- `rowversion` concurrency token on Customer, Lead, Opportunity, FollowUp, Activity.
- Money `decimal(18,2)`, probability `tinyint` with CHECK 0–100, enums stored as `tinyint` with CHECK on valid range. Strings are bounded `nvarchar(n)` per `Limits`.
- All queries: parameterised via EF. `FromSql` is allowed only with `FormattableString`/interpolated parameters and is banned for user-supplied fragments. A test greps for `FromSqlRaw`/`ExecuteSqlRaw` usage.
- Migrations live in `Infrastructure/Persistence/Migrations`; applied at startup only when `Database:MigrateOnStartup=true` (dev/docker). Production uses `dotnet ef migrations bundle`.
- Full schema: `docs/architecture/DATA-MODEL.md`.

## 6. Web layer conventions

- Folder-per-module: `Controllers/CustomersController.cs`, `ViewModels/Customers/*`, `Views/Customers/*`.
- ViewModels carry DataAnnotations for client validation and map to commands via `Mapping/*.cs` (hand-written extension methods, no AutoMapper). Messages come from shared resources in `Validation/Messages.cs` so MVC and API show identical text.
- Shared Razor: `Views/Shared/_Layout.cshtml`, `_AuthLayout.cshtml`, partials `_Pager`, `_FlashAlerts`, `_ConfirmModal`, `_FieldRow`, `_StatusBadge`, `_EmptyState`, and tag helpers `<money>`, `<date-local>`.
- Scripts: ES modules, no inline script (CSP). One file per page behaviour in `wwwroot/js/pages/*.js`, loaded via a `data-page` attribute convention.
- Policies are named from `Permissions` constants and registered by one loop in `Composition/AuthorizationSetup.cs`.
- API controllers inherit `ApiControllerBase` (`[ApiController]`, `[Route("api/[controller]")]`, `ToActionResult(Result)` helper, problem-details mapping).

## 7. Configuration

Typed options with `ValidateOnStart()`:

| Section | Class | Key settings |
|---|---|---|
| `Business` | `BusinessOptions` | `TimeZone` (default `Asia/Kolkata`), `Currency` `INR` |
| `Security:Password` | `PasswordPolicyOptions` | `MinLength`, `RequireUpper/Lower/Digit/Symbol`, `DenyList` |
| `Security:Lockout` | `LockoutOptions` | `MaxFailedAttempts=5`, `LockoutMinutes=15` |
| `Security:Jwt` | `JwtOptions` | `Issuer`, `Audience`, `SigningKey` (≥ 32 bytes, from secret store), `AccessMinutes=15`, `RefreshDays=7` |
| `Security:RateLimit` | `RateLimitOptions` | `AuthPermitPerMinute=10` |
| `Registration` | `RegistrationOptions` | `Mode` |
| `Audit` | `AuditOptions` | `ManagerAccess=false` |
| `Database` | `DatabaseOptions` | `MigrateOnStartup`, `SeedDemoData` (dev only) |
| `ConnectionStrings:Default` | – | from env/user-secrets only |

Bootstrap admin: `Bootstrap:AdminEmail` and `Bootstrap:AdminPassword` come from environment/secrets. The seeder creates the admin only if no Admin exists, and refuses to run with a password that fails policy.

## 8. Logging and observability
Serilog with enrichers (correlation id, user id, never email). `RequestLogging` middleware logs method, path template, status, elapsed. A destructuring policy masks properties named `password`, `token`, `secret`, `hash`. `X-Correlation-Id` is accepted or generated and echoed.

## 9. Architecture decision log
| ID | Decision | Alternatives rejected |
|---|---|---|
| A-01 | `ApplicationUser` in Infrastructure, Domain refers to users by id string | Identity in Domain (breaks purity) |
| A-02 | EF Core as repository/UoW behind `ICrmDbContext` | Generic repository (needless layer) |
| A-03 | Hand-written mapping | AutoMapper/Mapster (hidden behaviour, licensing churn) |
| A-04 | Own result type | Exceptions for validation (slow, noisy) |
| A-05 | Policy-per-permission authorisation | Role strings in `[Authorize(Roles=…)]` (not granular, scatters rules) |
| A-06 | Server-rendered MVC with progressive JS | SPA (not requested, bigger attack surface) |
| A-07 | Testcontainers SQL Server | SQLite/InMemory (do not honour filtered indexes/CHECK/rowversion) |
| A-08 | No MediatR | Not needed at this size |
