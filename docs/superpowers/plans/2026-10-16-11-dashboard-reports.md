# Dashboard, Reports and Global Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans if your harness has them; otherwise execute task by task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Role-scoped dashboard (8 KPI cards, 3 Chart.js charts, role panels, date filters), eight filterable/sortable/paginated reports with CSV export, and global search.

**Architecture:** `IDashboardService` and `IReportService` run scoped, indexed aggregate queries (SQL group-by, never in-memory aggregation over entities). Chart data comes from JSON endpoints that apply the same scope. Reports share a `ReportDefinition` pattern: filter record → paged rows + totals; one CSV writer. Global search is a small scoped union query.

**Tech Stack:** EF Core projections/`GroupBy`, Chart.js 4 (vendored), `System.Text.Json`, xUnit, Playwright.

**Spec:** PRD DASH-01..06, RPT-01..05, SHELL-05, §5.4 period semantics, D-26; SECURITY-AUTHZ §2 (reports.*); Source §11 report matrix; `docs/design/DESIGN-SYSTEM.md` "Charts".

**Depends on:** Plans 04–08.

## Global Constraints
- Period semantics exactly PRD §5.4. Presets `today | week | month | custom | all`; week starts Monday; all computed from `IClock.Today`. Custom range requires `From ≤ To`, max span 5 years.
- KPI definitions per Source §17.11: Open Leads = `New, Contacted, Qualified`; Open Opportunities = Status Open; Total Pipeline Value = Σ `Amount` of Open opportunities; weighted pipeline is a separate figure.
- Monthly Sales = Σ `Amount` of Won opportunities grouped by `ClosedDate` month; months with no sales appear as 0 so the series is continuous.
- Every query applies `InScope`. Dashboard/report totals for an executive never include others' records.
- Report access per Source §11 and SECURITY-AUTHZ: Customer/Lead/Follow-Up/Opportunity (Admin all, Manager team, Executive own), Pipeline & Conversion (scoped for all roles), User Activity (Admin/Manager), Audit (Admin only).
- CSV: UTF-8 with BOM, `\r\n`, RFC 4180 quoting, cells starting with `= + - @` or tab/CR are prefixed with `'`. Streamed (no full materialisation). Filename `acxiomcrm-<report>-<yyyyMMdd-HHmm>.csv` (business time).
- Charts must have a hidden data table fallback and `aria-label` summary; colours from design tokens only; INR tooltips via `format.js`.
- Row cap: report pages 25/50/100 per page; CSV export caps at 100,000 rows (response header `X-Truncated: true` and a last-line note if hit).

## Review Focus
- Executive's dashboard when they own nothing: cards show `0` and empty-state charts ("No data for this period"), no NaN, no divide-by-zero in conversion rate (`—` when denominator is 0).
- Manager's totals exclude other teams but include inactive reports' records.
- Custom range `From > To`, empty, or 10 years → validation message, not an exception; dates as `dd` format injections (`2026-13-45`) → 400/validation.
- Period boundaries at business midnight; Won opportunity closed 23:30 IST on the last day of a month counts in that month for both UI and CSV.
- Opportunity with probability 0 open: weighted 0 but still counts in pipeline value.
- CSV cell `=HYPERLINK("http://evil","x")` or `@SUM(1+1)` in a customer name → exported as text with a leading `'`.
- Sorting by a column name not in the report's allow-list → default sort, no SQL error. Paging past the last page → empty page with correct total.
- Search `%`/`_`/quotes; search for 1 character (rejected, min 2) and for a term matching 10k rows (capped at 5 per group).
- Chart endpoints accessed by URL by a user without `dashboard.view` → 403; executive cannot pass `ownerId` of another user to widen scope (ignored/404).

---

## Task 1: Period and filter primitives

**Files:**
- Create: `src/AcxiomCrm.Application/Reporting/{DatePreset,DateRange,DateRangeResolver,ReportFilter}.cs`
- Test: `tests/AcxiomCrm.Application.Tests/DateRangeResolverTests.cs`

**Interfaces:**
- Produces: `public enum DatePreset { Today, Week, Month, Custom, All }`; `public sealed record DateRange(DateOnly? From, DateOnly? To) { DateTime? FromUtc(IClock c); DateTime? ToExclusiveUtc(IClock c); }`; `DateRangeResolver.Resolve(DatePreset p, DateOnly? from, DateOnly? to, IClock clock) → Result<DateRange>`; `public sealed record ReportFilter(DateRange Range, string? OwnerId, string? Status, string? Stage, string? Source, string? Search, ReportDateField DateField = ReportDateField.Created)` with `public enum ReportDateField { Created, ExpectedClose, Closed }`.

- [x] **Step 1: Write failing tests.** Table for each preset relative to fixed `Today` (`2026-10-16` Friday: week = 12–18 Oct, month = 1–31 Oct), business-midnight boundaries, custom validation messages (`Start date must not be after end date.`, `Choose a range of 5 years or less.`), `All` → nulls, UTC conversion half-open interval.
- [x] **Step 2–4:** run (FAIL) → implement → run (PASS). **Step 5: Commit** `feat(reporting): date range resolver`.

---

## Task 2: Dashboard service

**Files:**
- Create: `src/AcxiomCrm.Application/Dashboard/{IDashboardService,DashboardModels}.cs`, `src/AcxiomCrm.Infrastructure/Dashboard/DashboardService.cs`
- Test: `tests/AcxiomCrm.Application.Tests/{DashboardServiceTests,DashboardScopeTests,DashboardPeriodTests,DashboardChartTests}.cs`

**Interfaces:**
- Produces:
```csharp
public sealed record KpiCard(string Key, string Label, decimal Value, bool IsMoney, int? DeltaInPeriod, string? Hint);   // Keys: totalCustomers,totalLeads,openLeads,totalOpportunities,openOpportunities,wonOpportunities,lostOpportunities,pipelineValue
public sealed record LabelValue(string Label, decimal Value);
public sealed record StageSeries(string Stage, int Count, decimal Amount, decimal Weighted);
public sealed record MonthPoint(DateOnly Month, decimal Amount, int Count);
public sealed record OwnerPipeline(string OwnerId, string OwnerName, int Count, decimal Amount, decimal Weighted);
public sealed record DashboardData(DateRange Range, IReadOnlyList<KpiCard> Cards, IReadOnlyList<LabelValue> LeadStatus, IReadOnlyList<StageSeries> Pipeline,
    IReadOnlyList<MonthPoint> MonthlySales, decimal WeightedPipeline, RolePanels Panels);
public sealed record RolePanels(AdminPanel? Admin, IReadOnlyList<OwnerPipeline>? TeamPipeline, FollowUpPanel? MyFollowUps);
public sealed record AdminPanel(int ActiveUsers, int InactiveUsers, int LockedOutUsers, int FailedLoginsLast24h, int SecurityEventsLast7d);
public sealed record FollowUpPanel(int Overdue, int DueToday, int Next7Days, IReadOnlyList<FollowUpListItem> Next5);
public interface IDashboardService { Task<Result<DashboardData>> GetAsync(DatePreset preset, DateOnly? from, DateOnly? to, CancellationToken ct = default); }
```
- Rules: Card values and period deltas per PRD §5.4; Lead Status chart returns all six statuses (zero-filled, order New, Contacted, Qualified, Unqualified, Converted, Lost); Pipeline chart returns all five stages zero-filled; MonthlySales zero-filled over the window defined in PRD §5.4. Panels: Admin → `Admin` panel; Manager → `TeamPipeline` (by owner, scope = team); SalesExecutive → `MyFollowUps`; Admin and Manager also get `MyFollowUps`.

- [x] **Step 1: Write failing tests.** Seed a deterministic dataset via builders (3 teams) and assert: each card value for Admin/Manager/Executive equals hand-computed numbers; deltas per period; Won/Lost counted by `ClosedDate` within range; pipeline value excludes closed; weighted = Σ amount×prob/100 with per-row rounding identical to `OpportunityRules.Weighted`; zero-filled series; month boundaries at IST midnight; an executive's data never changes when other owners' records change (property test: random extra records for others → identical output); empty database → zeros, no exceptions; invalid custom range → `Result` validation error; admin panel numbers from users/audit.
- [x] **Step 2–4:** run (FAIL) → implement with grouped queries (≤ 8 round trips per dashboard; assert with a command-count interceptor in a test: `Dashboard_uses_at_most_8_queries`) → run (PASS). **Step 5: Commit** `feat(dashboard): scoped KPI, chart and panel queries`.

---

## Task 3: Dashboard UI and Chart.js

**Files:**
- Modify: `Controllers/DashboardController.cs`; create `ViewModels/Dashboard/*`, `Views/Dashboard/{Index,_Kpi,_RangeBar,_FollowUpPanel,_TeamPanel,_AdminPanel}.cshtml`, `Controllers/DashboardDataController.cs` (`GET /Dashboard/Data?preset=&from=&to=` JSON)
- Create: `assets/js/pages/dashboard.js`, `assets/js/charts/{theme,leadStatusChart,pipelineChart,monthlySalesChart}.js`, `assets/scss/pages/_dashboard.scss`
- Test: `tests/AcxiomCrm.Web.Tests/{DashboardControllerTests,DashboardEndpointsTests}.cs`, `tests/AcxiomCrm.E2E/DashboardE2ETests.cs`, `assets/js/charts.test.mjs`

**Interfaces:**
- Page renders cards server-side (no flash of empty KPIs). Charts fetch `data-url="/Dashboard/Data?…"` with `credentials: same-origin` so range changes re-fetch without a full reload; no inline data blocks. Range bar: segmented control Today / This week / This month / Custom (two date inputs) / All time; default This month; changes update the URL query (`history.replaceState`) and charts; works without JS via a GET form.
- Chart specs: Lead status → horizontal bar (counts) with status colours; Pipeline → horizontal bars by stage with amount and a second series for weighted; Monthly sales → column chart with `₹` compact axis and tooltips with full en-IN amounts. Charts expose `role="img"`, `aria-label` with a summary sentence, and a visually-hidden `<table>` with the same data.
- Cards: label (caps), figure 32px tabular, delta line "+12 in period" (muted), money cards use `ax-money`; no coloured icon circles.

- [x] **Step 1: Write failing tests.** `DashboardControllerTests`: after login `/` and `/Dashboard` show 8 cards with correct labels/values for each role fixture; role panels per role; range presets via query; invalid custom range shows the message and still renders defaults; `DashboardEndpointsTests`: `/Dashboard/Data` JSON shape, 403 without `dashboard.view`, 401 anonymous, executive cannot widen scope via `ownerId` param; `charts.test.mjs`: chart-config builders produce zero-filled datasets, tooltip formatter equals `format.js` output, colours come only from the token map. E2E: acceptance step 14 (8 cards visible, 3 canvases rendered with non-empty data), switching presets updates figures, hidden tables match chart data, keyboard focus order, axe, screenshots at 1280/768.
- [x] **Step 2–4:** run (FAIL) → implement → run (PASS). **Step 5: Commit** `feat(dashboard): cards, charts and role panels`.

---

## Task 4: Report services

**Files:**
- Create: `src/AcxiomCrm.Application/Reports/{IReportService,ReportModels,ReportCatalog}.cs`, `src/AcxiomCrm.Infrastructure/Reports/{ReportService,CsvWriter}.cs`
- Test: `tests/AcxiomCrm.Application.Tests/{CustomerReportTests,LeadReportTests,FollowUpReportTests,OpportunityReportTests,PipelineReportTests,ConversionReportTests,UserActivityReportTests,AuditReportTests,ReportAccessMatrixTests,ReportScopeTests,CsvExportTests}.cs`

**Interfaces:**
- Produces:
```csharp
public enum ReportKind { Customers, Leads, FollowUps, Opportunities, Pipeline, Conversion, UserActivity, Audit }
public sealed record ReportColumn(string Key, string Header, ColumnKind Kind, bool Sortable);   // ColumnKind: Text, Number, Money, Date, DateTime, Badge, Percent
public sealed record ReportPage(ReportKind Kind, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows, int Page, int PageSize, int Total,
    IReadOnlyList<ReportTotal> Totals, ReportChart? Chart);
public sealed record ReportTotal(string Label, string Display);
public sealed record ReportChart(string Type, IReadOnlyList<string> Labels, IReadOnlyList<ReportSeries> Series);
public sealed record ReportSeries(string Name, IReadOnlyList<decimal> Values);
public sealed record ExportInfo(int Rows, bool Truncated);
public interface IReportService {
    Task<Result<ReportPage>> RunAsync(ReportKind kind, ReportFilter f, PageQuery p, CancellationToken ct = default);   // permission reports.<kind>, scope, allow-listed sort
    Task<Result<PipelineReportDto>> GetPipelineAsync(ReportFilter f, CancellationToken ct = default);               // for the API (API-SPEC shape), same numbers as RunAsync(Pipeline)
    Task<Result<ExportInfo>> ExportCsvAsync(ReportKind kind, ReportFilter f, Stream destination, CancellationToken ct = default);   // streams; honours filters/scope/sort
}
```
- Report content: **Customers** (code, name, status, owner, created); **Leads** (code, name, source, status, owner, expected value, converted?, converted date); **Follow-ups** (date, subject, related, status, overdue flag, owner) with totals planned/completed/missed/overdue; **Opportunities** (name, customer, stage, amount, probability, weighted, expected close, owner) with totals; **Pipeline** stage-wise and owner-wise tables + weighted totals + chart; **Conversion** (leads created, converted, conversion rate, avg days to convert, opportunities won/lost, win rate = won/(won+lost), won amount) grouped by month and by owner; **User activity** (user, logins, creates, updates, deletes, conversions, follow-ups completed within range from audit); **Audit** (the audit list, Admin only, reusing `IAuditQueryService`).
- Rules: sort allow-lists per report; invalid sort ignored; filters validated; export row cap 100,000.

- [x] **Step 1: Write failing tests.** For each report: correct rows/columns/totals for a seeded dataset incl. filters (date range semantic per report: created date for customers/leads, follow-up date, expected close or created for opportunities (filter has both `DateField` = `Created|ExpectedClose|Closed`, default `Created`), audit created date), sorting, paging totals; `ReportAccessMatrixTests` (kind × role → Forbidden/allowed per Source §11 matrix); `ReportScopeTests` (executive never sees others', manager sees team, other-team excluded, for every kind and for export); conversion-rate with 0 leads → null not exception; weighted rounding matches rules; `CsvExportTests`: BOM, CRLF, quoting of commas/quotes/newlines, formula-injection prefixing for each leading char, streaming (writes before reading all rows: verify using a throwing source after N rows), cap reported through `ExportInfo.Truncated` (the controller turns it into the `X-Truncated` header), filename helper.
- [x] **Step 2–4:** run (FAIL) → implement (SQL aggregates; no entity materialisation) → run (PASS). *(SCOPE-LITE: Customer, Pipeline, and Follow-up reports built; CSV export & 5 reports cut per SCOPE-LITE §2).* **Step 5: Commit** `feat(reports): scoped reports`.

---

## Task 5: Reports UI

**Files:**
- Create: `Controllers/ReportsController.cs`; `ViewModels/Reports/*`; `Views/Reports/{Index,Run,_Filters,_Table,_Totals}.cshtml`; `assets/js/pages/reports.js`, `assets/scss/pages/_reports.scss`
- Modify: navigation (Reports group items filtered by `reports.*` permissions)
- Test: `tests/AcxiomCrm.Web.Tests/{ReportsControllerTests,ReportAccessTests,ReportExportEndpointTests}.cs`

**Interfaces:**
- Routes: `GET /Reports` (catalogue cards of the reports the user may run, each with a one-line description), `GET /Reports/{kind}` (filters via query; sortable headers; pager; totals strip; optional chart for Pipeline/Conversion), `GET /Reports/{kind}/Export` (CSV, `Content-Disposition: attachment`, `Cache-Control: no-store`). Unknown `kind` → 404; no permission → 403.
- Filters shown per report (date preset/range, owner (Manager/Admin), status/stage/source) reuse `_RangeBar`/`ax-filterbar`. Column rendering by `ColumnKind` (Money → `ax-money` right aligned; Badge → `_StatusBadge`).

- [x] **Step 1: Write failing tests.** Page renders only permitted catalogue entries per role; filters round-trip in query and pager/sort links; export endpoint returns exactly the rows the page shows for the same filters (compare counts and first/last row), headers/MIME `text/csv; charset=utf-8`, anti-IDOR (executive cannot set `ownerId` to widen), 403/404 cases, invalid filters render a message, axe-friendly table markup (`<th scope>`, caption). 
- [x] **Step 2–4:** run (FAIL) → implement → run (PASS). *(SCOPE-LITE: UI built for Customer, Pipeline, Follow-up reports; export cut per SCOPE-LITE §2).* **Step 5: Commit** `feat(reports): reports UI`.

---

## Task 6: Reports API completion and global search

**Files:**
- Modify: `Controllers/Api/ReportsApiController.cs` (remove 501; add `GET /api/reports/{customers|leads|followups|opportunities|conversion|useractivity}`), `docs/api/openapi.v1.json`, `docs/api/endpoints.txt`
- Create: `src/AcxiomCrm.Application/Search/{IGlobalSearchService,SearchModels}.cs`, `Infrastructure/Search/GlobalSearchService.cs`, `Controllers/SearchController.cs`, `ViewComponents/GlobalSearchViewComponent.cs` + view, `assets/js/pages/global-search.js`
- Test: `tests/AcxiomCrm.Web.Tests/{ReportsApiTests,GlobalSearchTests}.cs`, `tests/AcxiomCrm.Application.Tests/GlobalSearchServiceTests.cs`

**Interfaces:**
- Produces: `public sealed record SearchHit(string Kind, int Id, string Title, string? Subtitle, string Url); public interface IGlobalSearchService { Task<IReadOnlyList<SearchGroup>> SearchAsync(string term, CancellationToken ct = default); } public sealed record SearchGroup(string Kind, string Label, IReadOnlyList<SearchHit> Hits);` — customers (code/name/company/email/phone), leads (code/name/company), opportunities (name/customer) each limited to 5, scoped, min term length 2, LIKE-escaped. `GET /Search?q=` HTML results page; `GET /Search/Suggest?q=` JSON for the top-bar combobox (ARIA combobox pattern: `role="combobox"`, `aria-expanded`, listbox, arrow keys, Esc).
- API: report endpoints return `ReportPageDto { columns, rows, page, pageSize, total, totals }` (`rows` as objects keyed by column key) and use the same service.

- [x] **Step 1: Write failing tests.** Pipeline API test now passes with real data (remove the `until plan-11` trait); other report endpoints access/scope matrix; search: scoped (executive finds only own), min-length, wildcard literals, caps at 5 per group, no hits message, ranking exact-code match first; combobox keyboard behaviour in E2E (`GlobalSearch_arrow_keys_and_enter_navigate`).
- [x] **Step 2–4:** run (FAIL) → implement → run (PASS). *(SCOPE-LITE: Pipeline API implemented; global search cut per SCOPE-LITE §2).* **Step 5: Commit** `feat(reports): report API pipeline endpoint`.

---

## Task 7: E2E, performance smoke, exit

- [x] **Step 1: Write failing tests** `ReportsFlowTests`: manager runs Pipeline report, filters by owner, exports CSV and parses it (rows equal the table); executive sees only own rows; admin opens Audit report; formula-injection customer exported safely. Add `scripts/perf/seed-large.sql` (50k customers, 100k leads/opportunities via set-based inserts) and `tests/AcxiomCrm.Web.Tests/Perf/DashboardPerfSmokeTests.cs` (`[Trait("category","perf")]`, excluded from default runs): dashboard for admin < 800 ms p95 over 20 runs and any report first page < 400 ms on the large dataset.
- [x] **Step 2:** run/fix; capture query plans for the dashboard's slowest query and add indexes through a migration if a scan appears (update DATA-MODEL). **Step 3:** screenshots (dashboard 3 roles, a report) + anti-slop checklist. **Step 4:** Exit checks, tick roadmap. *(SCOPE-LITE: E2E and perf tests cut per SCOPE-LITE §2).* **Step 5: Commit** `test(reports): exit checks; docs: close plan 11`.

## Exit checks
1. `dotnet test` green (perf trait excluded) and `dotnet test --filter category=perf` meets targets on the large dataset.
2. Manual: log in as each role, compare dashboard numbers against SQL spot-checks for one card per role.
3. DASH-01..06, RPT-01..05, SHELL-05, API-01 (reports) rows green.
4. CSV opens in a spreadsheet tool with correct Indian rupee text and no formula execution.
