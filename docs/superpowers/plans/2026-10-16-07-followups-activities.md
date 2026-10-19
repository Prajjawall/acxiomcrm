# Follow-Ups, Activities and Timeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans if your harness has them; otherwise execute task by task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Schedule, complete, reschedule and miss follow-ups against customers/leads/opportunities; log activities; show a unified record timeline; surface due/overdue work in the header.

**Architecture:** `FollowUp` and `Activity` entities (exactly-one / at-least-one related FK checks), pure `FollowUpRules`, two services in the module-kit shape, a read-only `ITimelineService` that merges audit, follow-ups and activities, and a `NotificationBell` view component.

**Tech Stack:** As plan 04.

**Spec:** PRD FU-01..07, ACT-01..03, SHELL-07, CUST-05, D-05, D-06, D-14, D-20, D-21; DATA-MODEL FollowUps/Activities.

**Depends on:** Plan 06.

## Global Constraints
- Follow-up statuses: Planned, Completed, Missed, Cancelled. Only `Planned` can change. Overdue is **derived** (`Planned` and `FollowUpDate < clock.UtcNow`), never stored (D-21).
- Date rule (D-20): for new and rescheduled follow-ups, the **business date** of `FollowUpDate` must be ≥ business today: `Follow-up date cannot be earlier than today.` Same-day earlier time is allowed. `IsHistoricalEntry=true` requires `followups.historical` (Manager/Admin) and status Completed; skips the date rule.
- Exactly one related id for follow-ups (CHECK + service). Activities need ≥ 1 related id.
- Related record must exist, be in the caller's scope; assignee active and in the caller's assignable set (AUTHZ-04). Executives assign to themselves.
- Subject required ≤ 200; remarks/description ≤ 2000.
- Activity types Call/Meeting/Email/Task; follow-up types Call/Meeting/Email/Visit/Other.
- Audit actions: `FollowUpCompleted`, `FollowUpRescheduled` (old/new date in diff), `FollowUpMissed`, `StatusChanged` (cancel) via `IAuditActionHint`.
- All user-visible times in the business zone.

## Review Focus
- Follow-up dated today at 09:00 when it is now 15:00 → allowed (date-level rule) and immediately shows Overdue.
- Midnight: business date flips at 18:30 UTC; test both sides.
- DST-free zone but the code must not assume a fixed offset: use `TimeZoneInfo` conversions with a second zone in a test (`America/New_York`) across a DST change.
- Rescheduling a Completed/Cancelled/Missed follow-up → `Conflict`. Completing twice → `Conflict`, one audit row.
- Follow-up whose related record was soft-deleted later: still listed, related name shows "(deactivated)".
- Posting both `CustomerId` and `LeadId` → validation error `Choose exactly one related record.`; neither → same.
- Executive A assigns a follow-up to executive B, or links it to B's lead → rejected / NotFound-equivalent error on the field without leaking existence.
- Completing with "log activity" when the activity fails validation rolls back the completion (atomic).
- Timeline for a record the user cannot see → 404, and timeline never leaks out-of-scope users' items for records that are visible (all items belong to the record).

---

## Task 1: Clock extension, entities, rules and migration

**Files:**
- Modify: `src/AcxiomCrm.Application/Abstractions/IClock.cs` (add `DateTime ToUtcFromBusinessLocal(DateTime local)` and `DateOnly BusinessDateOf(DateTime utc)`), `Infrastructure/Time/SystemClock.cs`, `TestSupport/FakeClock.cs`; extend `SystemClockTests`
- Create: `src/AcxiomCrm.Domain/Entities/{FollowUp,Activity}.cs`, `src/AcxiomCrm.Domain/Rules/{FollowUpRules,ActivityRules}.cs`
- Create: `Infrastructure/Persistence/Configurations/{FollowUp,Activity}Configuration.cs`; migration `FollowUpsActivities`
- Modify: `CrmDbContext`, `ICrmDbContext` (`FollowUps`, `Activities`)
- Test: `tests/AcxiomCrm.Domain.Tests/{FollowUpRulesTests,FollowUpEntityTests,ActivityRulesTests}.cs`, `tests/AcxiomCrm.Infrastructure.Tests/{FollowUpConstraintTests,ActivityConstraintTests}.cs`

**Interfaces:**
- Produces:
  - `FollowUpRules.Validate(string? subject, string? remarks, DateOnly followUpBusinessDate, DateOnly today, bool isHistorical, int relatedCount) → IReadOnlyList<FieldError>`; `FollowUpRules.IsOverdue(FollowUpStatus s, DateTime followUpUtc, DateTime nowUtc) → bool`; `FollowUpRules.IsDueToday(FollowUpStatus s, DateOnly followUpBusinessDate, DateOnly today)`.
  - `FollowUp : Entity, IOwnedEntity (OwnerId => AssignedToId), ISoftDeletable, IAuditedEntity, IHasTimestamps` with `CustomerId?, LeadId?, OpportunityId?, FollowUpDate, Subject, Type, Status, Remarks, AssignedToId, CompletedDate?, RescheduleCount, IsHistoricalEntry, RowVersion`; methods `Complete(string? remarks, DateTime nowUtc)`, `Reschedule(DateTime newUtc)`, `MarkMissed()`, `Cancel()` each throwing `InvalidOperationException` unless `Planned`; derived `RelatedType`.
  - `Activity` likewise (`ActivityType Type`, `Subject`, `Description`, `ActivityDate`, three related ids, `AssignedToId`, `Status`).

- [x] **Step 1: Write failing tests.** `SystemClockTests.ToUtcFromBusinessLocal_roundtrips_and_handles_DST_zone`, `BusinessDateOf_at_boundary`; `FollowUpRulesTests`: yesterday invalid with exact message, today valid, tomorrow valid, historical bypass, `relatedCount` 0/2 → `Choose exactly one related record.`, subject required/length; `IsOverdue`/`IsDueToday` truth tables incl. non-Planned never overdue; `FollowUpEntityTests`: state-machine Theory (only Planned transitions; counts); `ActivityRulesTests`; constraint tests: `CK_FollowUps_ExactlyOneRelated`, `CK_Activities_AtLeastOneRelated`, status/type enum checks, FK restrict, index names.
- [x] **Step 2: Run.** Expected: FAIL. **Step 3: Implement.** **Step 4: Run.** Expected: PASS.
- [x] **Step 5: Commit** `feat(followups): clock helpers, entities, rules, migration`.

---

## Task 2: Follow-up service

**Files:**
- Create: `src/AcxiomCrm.Application/FollowUps/{IFollowUpService,FollowUpModels}.cs`, `src/AcxiomCrm.Infrastructure/FollowUps/FollowUpService.cs`
- Test: `tests/AcxiomCrm.Application.Tests/{FollowUpServiceTests,FollowUpWorkflowTests,FollowUpValidationTests,FollowUpAuditTests,FollowUpIdorTests,FollowUpPendingTests}.cs`

**Interfaces:**
- Produces:
```csharp
public sealed record RelatedRef(RelatedType Type, int Id);
public sealed record FollowUpFilter(DateOnly? From = null, DateOnly? To = null, FollowUpStatus? Status = null, string? AssignedToId = null,
    RelatedRef? Related = null, bool OverdueOnly = false, bool DueTodayOnly = false);
public sealed record FollowUpListItem(int Id, RelatedType RelatedType, int RelatedId, string RelatedName, bool RelatedDeactivated, DateTime FollowUpDateUtc,
    string Subject, FollowUpType Type, FollowUpStatus Status, bool IsOverdue, bool IsDueToday, string AssignedToId, string AssignedToName, int RescheduleCount);
public sealed record FollowUpDetail(int Id, RelatedType RelatedType, int RelatedId, string RelatedName, DateTime FollowUpDateUtc, string Subject, FollowUpType Type,
    FollowUpStatus Status, string? Remarks, string AssignedToId, string AssignedToName, DateTime? CompletedDate, int RescheduleCount, bool IsHistoricalEntry,
    bool IsOverdue, DateTime CreatedDate, byte[] RowVersion);
public sealed record CreateFollowUpCommand(RelatedRef Related, DateTime FollowUpDateUtc, string Subject, FollowUpType Type, string? Remarks,
    string? AssignedToId, bool IsHistoricalEntry = false);
public sealed record UpdateFollowUpCommand(DateTime FollowUpDateUtc, string Subject, FollowUpType Type, string? Remarks, string? AssignedToId, byte[] RowVersion); // Planned only
public sealed record CompleteFollowUpCommand(string? Remarks, bool LogActivity, byte[] RowVersion);
public sealed record NotificationCounts(int Overdue, int DueToday);
public interface IFollowUpService {
    Task<PagedResult<FollowUpListItem>> SearchAsync(FollowUpFilter f, PageQuery p, CancellationToken ct = default);   // default sort: overdue first, then date asc
    Task<Result<FollowUpDetail>> GetAsync(int id, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> CreateAsync(CreateFollowUpCommand c, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> UpdateAsync(int id, UpdateFollowUpCommand c, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> CompleteAsync(int id, CompleteFollowUpCommand c, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> RescheduleAsync(int id, DateTime newDateUtc, byte[] rowVersion, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> MarkMissedAsync(int id, byte[] rowVersion, CancellationToken ct = default);
    Task<Result<FollowUpDetail>> CancelAsync(int id, byte[] rowVersion, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);          // Planned only for executives; soft delete
    Task<NotificationCounts> GetNotificationCountsAsync(CancellationToken ct = default);   // current user's assigned Planned items
}
```
- Permissions: view/create/edit by `followups.*`; `IsHistoricalEntry` needs `followups.historical`; Manager bulk "mark missed" is a loop over `MarkMissedAsync` in the controller (no special API).

- [ ] **Step 1: Write failing tests.** Cover: create against each related type; both/neither related error; related out of scope → field error on `Related` without existence leak; assignee rules; yesterday rejected / today accepted via `FakeClock` incl. midnight cases; historical entry permission and rules; `Complete` (status, `CompletedDate`, audit `FollowUpCompleted`, one row), `Complete_with_LogActivity_creates_activity_atomically` and rollback when the activity is invalid; `Reschedule` (increments count, audit old/new date, date rule applies); `MarkMissed`/`Cancel` audits; terminal states `Conflict`; stale rowversion `Conflict`; `Search` filters (date range uses business dates, overdue-only, due-today-only, related, assignee), sort order overdue-first; `GetNotificationCounts` counts only the caller's own Planned items; IDOR set; `Delete` rules (executive: own Planned only, otherwise `Forbidden`).
- [ ] **Step 2–4:** run (FAIL) → implement → run (PASS). **Step 5: Commit** `feat(followups): service with workflow, scope and audit`.

---

## Task 3: Activity service

**Files:**
- Create: `src/AcxiomCrm.Application/Activities/{IActivityService,ActivityModels}.cs`, `src/AcxiomCrm.Infrastructure/Activities/ActivityService.cs`
- Test: `tests/AcxiomCrm.Application.Tests/{ActivityServiceTests,ActivityValidationTests,ActivityIdorTests,ActivityAuditTests}.cs`

**Interfaces:**
- Produces: `ActivityFilter(ActivityType? Type, DateOnly? From, DateOnly? To, ActivityStatus? Status, string? AssignedToId, RelatedRef? Related)`, `ActivityListItem`, `ActivityDetail`, `CreateActivityCommand(ActivityType Type, string Subject, string? Description, DateTime ActivityDateUtc, IReadOnlyList<RelatedRef> Related /* 1..3, distinct types */, string? AssignedToId, ActivityStatus Status)`, `UpdateActivityCommand(... , byte[] RowVersion)`, and `IActivityService` with the standard five methods. Related refs are mapped onto `CustomerId/LeadId/OpportunityId`; an opportunity ref may be combined with its customer.
- Rules: standard module kit; any related ref must be in scope; types/status enums validated; activity date may be any date (history is normal).

- [ ] **Step 1: Write failing tests** for create (one/two/three related, zero rejected, duplicate type rejected), validation messages, scope/IDOR, update/delete, search filters (type, date range by business date, status, assignee, related), audit rows.
- [ ] **Step 2–4:** run → implement → run. **Step 5: Commit** `feat(activities): service`.

---

## Task 4: Timeline service and bell counts

**Files:**
- Create: `src/AcxiomCrm.Application/Timeline/{ITimelineService,TimelineModels}.cs`, `src/AcxiomCrm.Infrastructure/Timeline/TimelineService.cs`
- Test: `tests/AcxiomCrm.Application.Tests/{TimelineTests,TimelineScopeTests}.cs`

**Interfaces:**
- Produces:
```csharp
public enum TimelineKind { Audit, FollowUp, Activity }
public sealed record TimelineItem(DateTime WhenUtc, TimelineKind Kind, string Title, string? Detail, string? ActorName, string? Url);
public interface ITimelineService {
    Task<Result<PagedResult<TimelineItem>>> GetAsync(RelatedRef record, PageQuery p, CancellationToken ct = default);   // NotFound when the record is not visible
}
```
- Merge rules: audit rows for the record (`EntityName` + `RecordId`), follow-ups and activities linked to it (for a **Customer** also those of its opportunities and its source lead; for a **Lead** those of its converted opportunity). Sorted `WhenUtc DESC`, merged in SQL via `UNION ALL` projections then paged (no in-memory merge of unbounded sets). Audit titles are human text (`Status changed from Contacted to Qualified`, `Created`, `Converted to customer CUS-000012`) produced by `AuditTitleFormatter` (unit-tested with the action/JSON fixtures; unknown actions fall back to the action name).

- [ ] **Step 1: Write failing tests.** `TimelineTests`: items from all three kinds, ordering, paging totals, customer timeline includes its opportunities' follow-ups, formatter titles for each audit action in `AuditActions`, no raw JSON leaks, no `OldValue/NewValue` of excluded properties; `TimelineScopeTests`: out-of-scope record → `NotFound`; items never include other records' rows; executive can see timeline of own record only.
- [ ] **Step 2–4:** run → implement → run. **Step 5: Commit** `feat(timeline): unified record timeline`.

---

## Task 5: Follow-ups and activities UI, bell, timeline tabs

**Files:**
- Create: `Controllers/{FollowUpsController,ActivitiesController}.cs`; `ViewModels/{FollowUps,Activities}/*`; `Mapping/{FollowUp,Activity}Mapping.cs`
- Create: `Views/FollowUps/{Index,Create,Edit,Details,_CompleteModal,_RescheduleModal,_Row}.cshtml`, `Views/Activities/{Index,Create,Edit,Details,_Form}.cshtml`, `Views/Shared/_Timeline.cshtml`, `ViewComponents/NotificationBellViewComponent.cs` + `Views/Shared/Components/NotificationBell/Default.cshtml`
- Create: `assets/js/pages/{followups,activities,timeline}.js`; `assets/scss/pages/{_followups,_timeline}.scss`
- Modify: navigation (Follow-ups, Activities), layout top bar (bell), `Customers/Details`, `Leads/Details`, `Opportunities/Details` (Timeline tab using `ITimelineService`, plus "Schedule follow-up" and "Log activity" actions prefilled with the related ref)
- Test: `tests/AcxiomCrm.Web.Tests/{FollowUpsControllerTests,FollowUpAccessTests,FollowUpServerValidationTests,ActivitiesControllerTests,NotificationBellTests,TimelineViewTests}.cs`

**Interfaces:**
- Routes: `GET /FollowUps` (tabs `pending` default | `all`; filters), `GET/POST /FollowUps/Create?relatedType=&relatedId=`, `GET/POST /FollowUps/Edit/{id}`, `POST /FollowUps/{Complete|Reschedule|Miss|Cancel}/{id}`, `POST /FollowUps/Delete/{id}`, `GET /Activities` (+ standard CRUD). Policies from permissions.
- Pending tab layout: groups **Overdue**, **Today**, **Upcoming** with counts; overdue rows have a text "Overdue" badge and a left border accent; row actions Complete, Reschedule, More (Missed, Cancel). Date/time input: separate `date` (min = business today, rendered from `IClock`) and `time` inputs combined server-side. Complete modal: remarks, checkbox "Log as activity", checkbox "Schedule next follow-up" (redirects to Create prefilled). Bell: count badge (`99+` cap) with `aria-label="3 overdue, 2 due today"`, dropdown listing top 5 with links to the pending tab; zero state "Nothing due".

- [ ] **Step 1: Write failing tests.** Controller tests for CRUD/validation display (date input `min` attribute equals business today; yesterday POST → message from server, no row); Complete/Reschedule/Miss/Cancel happy and illegal paths with flash text; IDOR and access matrix; bell shows counts for current user only and updates after completion; `_Timeline` renders items with encoded text and correct links; related-prefill works; `FollowUpServerValidationTests` crafted posts (both related ids, foreign assignee, status tampering ignored).
- [ ] **Step 2–4:** run → implement → run. **Step 5: Commit** `feat(followups): UI, notification bell and timeline tabs`.

---

## Task 6: E2E, design review, exit

- [ ] **Step 1: Write failing tests.** `FollowUpFlowTests`: acceptance step 8 (yesterday rejected client-side via `min` and `axfuturedate`, and server-side with JS disabled); schedule → appears under Upcoming; complete with "log as activity" → timeline on the customer shows both; reschedule shows count and audit; overdue grouping uses seeded past follow-up; bell updates. axe on follow-ups pending, create, activities index, customer details timeline tab.
- [ ] **Step 2:** run/fix. **Step 3:** screenshots + checklist. **Step 4:** Exit checks, tick roadmap. **Step 5: Commit** `test(followups): E2E; docs: close plan 07`.

## Exit checks
1. `dotnet test` green; `FollowUpService` ≥ 90% lines.
2. Manual: schedule a follow-up for yesterday → blocked; complete one with log-activity; check `/Audit` shows `FollowUpCompleted`.
3. FU-01..07, ACT-01..03, SHELL-07, CUST-05 rows green; the CUST-05 timeline tab verified on customers, leads and opportunities.
4. `git grep -nE "DateTime\.(Now|Today|UtcNow)"` src → none.
