# Opportunity Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans if your harness has them; otherwise execute task by task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Opportunity pipeline with business rules (amount, probability, close date), stage workflow, weighted value, board and table views, and lead-conversion opportunity creation.

**Architecture:** `OpportunityRules` in Domain is the single source of the rules and of `WeightedAmount`. `IOpportunityService` orchestrates; `OpportunityFactory` (implements `IOpportunityFactory` from plan 05) shares the same rules/creation path. MVC offers table and board views over the same service.

**Tech Stack:** As plan 04; drag-and-drop with the native HTML5 API plus keyboard menu fallback (no library).

**Spec:** PRD OPP-01..07, D-16..D-19, VAL canonical messages, CUST-06/07 (finishing); DATA-MODEL Opportunities.

**Depends on:** Plan 05.

## Global Constraints
- Stages: Qualification(1), Proposal(2), Negotiation(3), Won(4), Lost(5). Status derived (D-16): 1–3 → Open, 4 → Won, 5 → Lost.
- While Open: `Amount > 0`; `0 ≤ Probability ≤ 100`; `ExpectedCloseDate ≥ IClock.Today`. Always: `0 ≤ Amount ≤ 1,000,000,000`.
- Closing: Won → probability 100, Lost → 0 (D-17); `ClosedDate = IClock.Today`; `CloseNote` required (≤ 500): `Enter a note about why this opportunity was won.` / `Enter the reason this opportunity was lost.`
- Won/Lost terminal; reopen (Manager/Admin, `opportunities.reopen`) → `Negotiation`, probability 75 (editable after), `ClosedDate/CloseNote` cleared, audited as `StageChanged` with details `reopened`.
- Weighted value `Amount × Probability / 100`, rounded **half away from zero to 2 decimals**, computed only by `OpportunityRules.Weighted(amount, probability)`.
- Customer must exist, be Active and in scope; inactive customers cannot get new opportunities (CUST-06).
- Canonical messages verbatim (PRD §4).

## Review Focus
- Amount `0`, `-0.01`, `0.001` (rounds to 0.00?). Input is validated **after** rounding to 2 decimals; `0.004` → `Opportunity Amount must be greater than 0.`
- Probability `101`, `-1`, `100`, `0`, non-integer `50.5`.
- Expected close date `yesterday` rejected, `today` accepted, at business midnight (IST 23:59 vs 00:00).
- Editing a Won/Lost opportunity's amount to 0 is **allowed to be rejected**: terminal records are read-only (`Conflict`: `This opportunity is closed and cannot be edited.`), so the open-only rules never become a loophole.
- Moving stage backwards (Negotiation → Qualification) on an opportunity whose close date has passed: rejected until the date is updated in the same request.
- Dragging a card to Won without a note: modal asks for the note; the server still rejects an empty note.
- Weighted pipeline totals on the board equal the sum of per-card weighted values (rounding consistency).
- Deleting a customer with open opportunities is blocked (CUST-07); once all are Won/Lost or deleted, it is allowed.
- Lead conversion creating an opportunity with an invalid amount: whole conversion rolls back (plan 05 test now becomes real).

---

## Task 1: Domain rules, entity, migration

**Files:**
- Create: `src/AcxiomCrm.Domain/Entities/Opportunity.cs`, `src/AcxiomCrm.Domain/Rules/OpportunityRules.cs`
- Create: `Infrastructure/Persistence/Configurations/OpportunityConfiguration.cs`; migration `Opportunities`
- Modify: `CrmDbContext`, `ICrmDbContext` (`DbSet<Opportunity> Opportunities`)
- Test: `tests/AcxiomCrm.Domain.Tests/{OpportunityRulesTests,OpportunityEntityTests}.cs`, `tests/AcxiomCrm.Infrastructure.Tests/OpportunityConstraintTests.cs`

**Interfaces:**
- Produces:
  - `OpportunityRules`: `static decimal Weighted(decimal amount, int probability)`; `static OpportunityStatus StatusFor(OpportunityStage stage)`; `static byte DefaultProbability(OpportunityStage stage)` (20/50/75/100/0); `static IReadOnlyList<FieldError> Validate(string? name, decimal amount, int probability, DateOnly? expectedClose, OpportunityStage stage, DateOnly today, string? notes, string? closeNote)`; `static bool IsOpen(OpportunityStage)`.
  - `Opportunity : Entity, IOwnedEntity, ISoftDeletable, IAuditedEntity, IHasTimestamps`: `Name, CustomerId, LeadId?, Amount, Stage, Status, Probability, ExpectedCloseDate (DateOnly), ClosedDate?, CloseNote?, Source?, OwnerId, Notes, RowVersion`; `static Create(...)`, `UpdateDetails(...)` (throws if closed), `MoveTo(OpportunityStage stage, DateOnly today, string? note)` (applies D-17, throws on invalid), `Reopen()`.
  - Probability type is `byte` in the entity/DB and `int` in commands (validated before narrowing).

- [x] **Step 1: Write failing tests.**
  - `OpportunityRulesTests` (verbatim canonical messages): amount table (`0`, `-1`, `0.004`, `0.01`, `1_000_000_000`, `1_000_000_000.01`), probability table, close date table with `today` fixed (`yesterday`, `today`, `tomorrow`), `Closed_stage_skips_amount_and_date_open_rules`, `Weighted` table (`(1250000, 40) → 500000.00`, `(33.33m, 33) → 11.00` (`10.9989`→`11.00`), `(0.01m, 50) → 0.01` half away from zero), `StatusFor` for all stages, `DefaultProbability`.
  - `OpportunityEntityTests`: `MoveTo_Won_sets_probability_100_closeddate_and_status`, `MoveTo_Lost_sets_0`, `Won_requires_note`, `MoveTo_from_closed_throws`, `Reopen_clears_close_fields`, `UpdateDetails_on_closed_throws`.
  - `OpportunityConstraintTests`: `CK_Opps_Amount`, `CK_Opps_Probability`, `CK_Opps_OpenAmountPositive`, `CK_Opps_StageStatus`, `CK_Opps_ClosedDate`, FK restrict customer/lead/owner, index names.
- [x] **Step 2: Run** `--filter "Opportunity"` across Domain and Infrastructure tests. Expected: FAIL.
- [x] **Step 3: Implement.**
- [x] **Step 4: Run.** Expected: PASS.
- [x] **Step 5: Commit** `feat(opportunities): rules, entity and migration`.

---

## Task 2: Opportunity service and factory

**Files:**
- Create: `src/AcxiomCrm.Application/Opportunities/{IOpportunityService,OpportunityModels}.cs`
- Create: `src/AcxiomCrm.Infrastructure/Opportunities/{OpportunityService,OpportunityFactory}.cs` (delete `NotAvailableOpportunityFactory`)
- Modify: `CustomerService.DeleteAsync` (CUST-07 guard), `CustomerService` tests
- Modify: `LeadConversionTests` (replace the `until plan-06` test)
- Test: `tests/AcxiomCrm.Application.Tests/{OpportunityServiceTests,OpportunityStageTests,OpportunityValidationTests,OpportunityIdorTests,OpportunityAuditTests,OpportunityBoardQueryTests}.cs`

**Interfaces:**
- Produces:
```csharp
public sealed record OpportunityFilter(string? Search = null, int? CustomerId = null, OpportunityStage? Stage = null, OpportunityStatus? Status = null, string? OwnerId = null);
public sealed record OpportunityListItem(int Id, string Name, int CustomerId, string CustomerName, string CustomerCode, decimal Amount, int Probability,
    decimal WeightedAmount, OpportunityStage Stage, OpportunityStatus Status, DateOnly ExpectedCloseDate, bool CloseDateOverdue, string OwnerId, string OwnerName);
public sealed record OpportunityDetail(int Id, string Name, int CustomerId, string CustomerName, string CustomerCode, int? LeadId, decimal Amount, int Probability,
    decimal WeightedAmount, OpportunityStage Stage, OpportunityStatus Status, DateOnly ExpectedCloseDate, DateOnly? ClosedDate, string? CloseNote,
    LeadSource? Source, string OwnerId, string OwnerName, string? Notes, DateTime CreatedDate, DateTime? ModifiedDate, byte[] RowVersion);
public sealed record CreateOpportunityCommand(string Name, int CustomerId, decimal Amount, int Probability, DateOnly ExpectedCloseDate,
    OpportunityStage Stage, LeadSource? Source, string? OwnerId, string? Notes);          // Stage must be an open stage
public sealed record UpdateOpportunityCommand(string Name, int CustomerId, decimal Amount, int Probability, DateOnly ExpectedCloseDate,
    LeadSource? Source, string? OwnerId, string? Notes, byte[] RowVersion);                // no Stage: use ChangeStageAsync
public sealed record BoardColumn(OpportunityStage Stage, int Count, decimal Amount, decimal WeightedAmount, IReadOnlyList<OpportunityListItem> Cards);
public interface IOpportunityService {
    Task<PagedResult<OpportunityListItem>> SearchAsync(OpportunityFilter f, PageQuery p, CancellationToken ct = default);   // sorts: name, amount, weighted, expectedCloseDate, stage
    Task<Result<OpportunityDetail>> GetAsync(int id, CancellationToken ct = default);
    Task<Result<OpportunityDetail>> CreateAsync(CreateOpportunityCommand c, CancellationToken ct = default);
    Task<Result<OpportunityDetail>> UpdateAsync(int id, UpdateOpportunityCommand c, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result<OpportunityDetail>> ChangeStageAsync(int id, OpportunityStage to, string? closeNote, byte[] rowVersion, CancellationToken ct = default);
    Task<Result<OpportunityDetail>> ReopenAsync(int id, byte[] rowVersion, CancellationToken ct = default);
    Task<IReadOnlyList<BoardColumn>> GetBoardAsync(OpportunityFilter f, int maxCardsPerColumn = 50, CancellationToken ct = default);   // Open stages + Won/Lost closed in the last 30 business days
}
```
- `OpportunityFactory : IOpportunityFactory` creates through the same internal path (rules, customer checks, audit) in the **caller's** transaction/context (no second SaveChanges commit; caller commits).
- Audit: stage changes use `IAuditActionHint` `StageChanged` (old/new stage + probability in the diff); reopen adds `Details = "reopened"` via the writer on the same hint path.

- [ ] **Step 1: Write failing tests.**
  - `OpportunityServiceTests`: `Create_valid_derives_status_weighted_and_owner`, `Create_executive_owner_forced`, `Create_for_inactive_customer_rejected` (error on `CustomerId`: `Choose an active customer.`), `Create_for_out_of_scope_customer_rejected_same_message`, `Create_with_closed_stage_rejected`, `Update_open_applies_rules`, `Update_closed_is_conflict`, `Delete_soft_deletes`, `Search_filters_and_sorts`, `Weighted_in_list_equals_domain_rule`.
  - `OpportunityValidationTests`: PRD §5.4 examples as exact-message tests; Review Focus numerics; midnight boundary with `FakeClock` (UTC 18:29 / 18:30).
  - `OpportunityStageTests`: Theory of open-to-open moves; Won with/without note; Lost with/without reason; terminal → any move `Conflict`; reopen by manager OK/executive `Forbidden`/other-team `NotFound`; backward move with stale close date rejected until date updated (service-level: `ChangeStageAsync` rejects with `Expected Close Date cannot be in the past.` on field `ExpectedCloseDate`); audit has old/new stage; Won sets `ClosedDate = clock.Today`.
  - `OpportunityBoardQueryTests`: columns for 5 stages in order; counts/amount/weighted per column equal the sum of cards when not truncated, and totals still reflect **all** rows when cards are truncated by `maxCardsPerColumn`; scope respected; closed in last 30 days only.
  - `OpportunityIdorTests`, `OpportunityAuditTests` per earlier patterns.
  - Updated `CustomerServiceTests.Delete_blocked_while_open_opportunities_exist` (`Conflict`, message `This customer has open opportunities. Close or reassign them first.`), `Delete_allowed_when_only_closed_opportunities`.
  - `LeadConversionTests.Creates_opportunity_when_requested`, `Invalid_opportunity_rolls_back_conversion` (now real).
- [ ] **Step 2: Run** `dotnet test --filter "Opportunity|LeadConversion|CustomerServiceTests"`. Expected: FAIL.
- [ ] **Step 3: Implement.** Board totals via a separate grouped aggregate query (never summing only materialised cards).
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(opportunities): service, factory, board query and customer deletion guard`.

---

## Task 3: Opportunities controller and views (table)

**Files:**
- Create: `src/AcxiomCrm.Web/Controllers/OpportunitiesController.cs`
- Create: `ViewModels/Opportunities/{OpportunityListViewModel,OpportunityFormViewModel,OpportunityDetailsViewModel,ChangeStageViewModel}.cs`, `Mapping/OpportunityMapping.cs`
- Create: `Views/Opportunities/{Index,Create,Edit,Details,_Form,_StageActions,_CloseModal}.cshtml`, `assets/scss/pages/_opportunities.scss`, `assets/js/pages/opportunities.js`
- Modify: navigation ("Opportunities", `opportunities.view`, icon `trending-up`); Customer details gets **Opportunities** tab; Lead convert view enables the opportunity section (`IOpportunityFactory.IsAvailable`)
- Test: `tests/AcxiomCrm.Web.Tests/{OpportunitiesControllerTests,OpportunityAccessTests,OpportunityServerValidationTests,OpportunityClientValidationMarkupTests}.cs`

**Interfaces:**
- Routes: `GET /Opportunities` (`view=table|board`, filters, paging), `GET /Opportunities/Details/{id}`, `GET/POST Create` (optional `customerId` preselect and `leadId` ignored), `GET/POST Edit/{id}`, `POST Delete/{id}`, `POST ChangeStage/{id}`, `POST Reopen/{id}`.
- Form fields use `AxRange`/`AxFutureDate`/`AxRequired`. Amount is a text input with `inputmode="decimal"`, accepts `12,50,000` and `1250000.50` (server model binder `IndianDecimalModelBinder`: strips commas/spaces/₹, invariant parse, rejects more than 2 decimals with `Enter an amount with at most 2 decimal places.`). Customer field is a typeahead using `/Customers/Lookup`. Probability is a number input 0–100 with stage-default hint ("Typical for Proposal: 50").
- Details: summary facts (Amount, Probability, **Weighted value**, Expected close with overdue marker, Stage badge, Owner), stage action bar (next/prev open stages and Won/Lost buttons; Won/Lost open `_CloseModal` requiring a note, form posts the note), Reopen for Manager/Admin; History tab (audit).

- [ ] **Step 1: Write failing tests.** Controller tests mirroring plan 04/05 patterns plus: `Amount_binder_accepts_indian_grouping_and_rejects_three_decimals`; `Form_inputs_carry_data_val_attributes_for_range_and_futuredate` with today's value injected; `ChangeStage_to_Won_without_note_redisplays_with_error`; `Closed_opportunity_shows_no_edit_button`; access matrix (executive can't reopen/delete; manager can for team); IDOR (404). `OpportunityServerValidationTests`: crafted POSTs for Amount `0`, `-5`, Probability `101`, close date yesterday, stage field tampering on Edit (ignored) and customer from another team (rejected) each assert message + no row change.
- [ ] **Step 2: Run.** Expected: FAIL. **Step 3: Implement.** **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(opportunities): controller and views`.

---

## Task 4: Pipeline board

**Files:**
- Create: `Views/Opportunities/{Board,_BoardColumn,_BoardCard}.cshtml`, `assets/js/pages/opportunity-board.js`, `assets/scss/pages/_board.scss`
- Modify: `OpportunitiesController` (`Board` action, `POST /Opportunities/Move` JSON endpoint)
- Test: `tests/AcxiomCrm.Web.Tests/OpportunityBoardTests.cs`, `tests/AcxiomCrm.E2E/OpportunityBoardE2ETests.cs`

**Interfaces:**
- `POST /Opportunities/Move` body `{ id, toStage, closeNote?, rowVersion }` (anti-forgery via header `RequestVerificationToken`), returns `200 { card, columnTotals }` or `400/409` problem JSON; the same service call as the form path.
- Board: five columns; headers show count, total and weighted total (`ax-money`, compact format); cards: name, customer, amount, close date (overdue marker with text "Overdue"), owner initials with `title`. Won/Lost columns show only items closed in the last 30 days with a "View all" link to the filtered table. Each card has a **Move to…** menu button (keyboard-accessible) in addition to drag. Failed move reverts the card and announces the error in an `aria-live` region.

- [ ] **Step 1: Write failing tests.** `OpportunityBoardTests`: HTML has 5 columns in order; header totals equal service board totals; card for out-of-scope opportunity absent; `Move` open→open OK; `Move` to Won without note → 400 with field error; stale rowVersion → 409; move without anti-forgery header → 400; executive moving someone else's card → 404. E2E: `Move_card_via_menu_updates_totals_and_persists_after_reload`, `Drag_card_to_Negotiation`, `Won_move_asks_for_note_modal`, `Failed_move_reverts_and_announces`; axe on board.
- [ ] **Step 2–4:** run → implement → pass. **Step 5: Commit** `feat(opportunities): pipeline board with keyboard-accessible moves`.

---

## Task 5: E2E, design review, exit

- [ ] **Step 1: Write failing tests** `OpportunityFlowTests`: acceptance steps 5–7 of the Source scenario (amount 0 rejected, probability 101 rejected, past close date rejected) through the real form with client-side messages **and** with JS-disabled crafted posts (use `request.PostAsync` with token); happy path create → move → Won with note; weighted figure displayed equals Amount × Probability / 100.
- [ ] **Step 2:** run/fix. **Step 3:** screenshots (list, board, details) + anti-slop checklist in `docs/design/screens/README.md`. **Step 4:** Exit checks, tick roadmap. **Step 5: Commit** `test(opportunities): E2E; docs: close plan 06`.

## Exit checks
1. `dotnet test` green; `OpportunityService` + `OpportunityRules` coverage ≥ 90%.
2. Manual: enter amount `0`, probability `101`, yesterday's date in the UI → three inline messages with exact PRD text; repeat with browser JS disabled → same messages from the server.
3. `SELECT Amount, Probability, ... FROM Opportunities` shows `CK_Opps_*` constraints reject direct inserts violating rules.
4. OPP-01..07 and CUST-06/07, LEAD-05 rows green in the traceability matrix.
