# Application Shell and Design System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans if your harness has them; otherwise execute task by task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **UI work:** read `docs/design/DESIGN-SYSTEM.md` completely before Task 2. If your harness has a frontend-design skill, use it as a *reviewer* of the result; the tokens in the design doc are the source of truth.

**Goal:** A distinctive, accessible application shell: Sass design system on Bootstrap, layouts, shared components, flash/confirm/pager patterns, error pages, and the front-end build.

**Architecture:** npm + Dart Sass build writes `wwwroot/css` and `wwwroot/lib` (git-ignored). Razor layouts and partials/tag helpers expose the `ax-*` component API. Small ES modules enhance behaviour with no inline script. Playwright is introduced here for visual and accessibility checks.

**Tech Stack:** Bootstrap 5.3 (Sass), Dart Sass, IBM Plex via `@fontsource`, Lucide icons (sprite), jQuery Validation Unobtrusive, Chart.js (vendored now, used in plan 11), Node test runner, Playwright for .NET + axe-core.

**Spec:** PRD SHELL-01..06, NFR-05, D-15, D-27; `docs/design/DESIGN-SYSTEM.md` (all).

**Depends on:** Plan 02.

## Global Constraints
- Tokens exactly as DESIGN-SYSTEM §2. Fonts IBM Plex Sans/Mono only. One accent `#0F5C5A`.
- No CDN URLs anywhere; CSP `script-src 'self' 'nonce-…'` and `style-src 'self'`: no inline styles/scripts, no `style=""` attributes (use utility/ax classes). Dynamic widths (progress bars) use CSS custom properties set via `data-*` and a tiny module, not `style=`.
- Money format `₹12,50,000` (en-IN), dates `16 Oct 2026` / `16 Oct 2026, 3:30 pm` in the **business** zone.
- Every interactive element reachable by keyboard with a visible focus ring; `prefers-reduced-motion` honoured.
- Enum display labels live in `Web/Presentation/Labels.cs` (e.g. `SalesExecutive` → `Sales executive`); views never print raw enum names.

## Review Focus
- Sidebar collapsed state persisted; first paint must not flash the wrong state (class set by a CSP-safe external script in `<head>`, `defer` not used for this one tiny module).
- 768 px viewport: sidebar off-canvas, tables scroll inside their container, **no horizontal page scroll**.
- Long unbroken strings (200-char customer name, email) wrap or truncate with a title, never break layout.
- Validation summary and inline errors announced to screen readers (`aria-live`), focus moves to the first invalid field on failed submit.
- Double-click on a submit button submits once.
- 500 error page shows a correlation id but never exception text; 404 for an unknown route and 403 for a forbidden action look the same shape and are distinct in copy.
- User with no permissions for a nav group sees no empty group heading.

---

## Task 1: Front-end toolchain

**Files:**
- Create: `src/AcxiomCrm.Web/package.json`, `package-lock.json`, `.nvmrc` (Node 22), `scripts/build-assets.mjs`, `scripts/verify-assets.mjs`
- Create: `src/AcxiomCrm.Web/assets/scss/app.scss` (imports only), `assets/js/app.js` (entry stub), `assets/img/icons.svg` (sprite from Lucide: the ~30 icons listed in `assets/img/ICONS.md`)
- Modify: `.gitignore` (ignore `wwwroot/lib`, `wwwroot/css`, `wwwroot/js/dist`), `Dockerfile` (run `npm ci && npm run build` in the node stage), `.github/workflows/ci.yml` (setup-node + build + `npm audit --omit=dev`)
- Test: `src/AcxiomCrm.Web/scripts/verify-assets.test.mjs` (node:test)

**Interfaces:**
- Produces: `npm run build` → `wwwroot/css/app.css`, `wwwroot/lib/{bootstrap,jquery,jquery-validation,jquery-validation-unobtrusive,chartjs}/…`, `wwwroot/fonts/*.woff2`, `wwwroot/img/icons.svg`, `wwwroot/js/dist/app.js` (esbuild bundle, ES2022, source map in dev). `npm run watch`.

- [ ] **Step 1: Write failing test** `verify-assets.test.mjs`: after `npm run build`, assert the outputs above exist; `app.css` contains no `url(http`, `@import url(http` or `//cdn`; font files referenced by `@font-face` exist; `dist/app.js` contains no `eval(` or `new Function(`; total `app.css` < 250 KB.
- [ ] **Step 2: Run** `npm --prefix src/AcxiomCrm.Web test`. Expected: FAIL (nothing built).
- [ ] **Step 3: Implement** the build script (sass `--load-path=node_modules`, esbuild, file copy with an explicit allow-list so only needed vendor files ship). Fonts: Plex Sans 400/500/600 and Plex Mono 400, Latin + Latin-ext subsets, WOFF2 only.
- [ ] **Step 4: Run** `npm --prefix src/AcxiomCrm.Web ci && npm --prefix src/AcxiomCrm.Web run build && npm --prefix src/AcxiomCrm.Web test`. Expected: PASS.
- [ ] **Step 5: Commit** `feat(web): front-end toolchain with Sass, esbuild and vendor allow-list`.

---

## Task 2: Design tokens, Bootstrap theming and base styles

**Files:**
- Create: `assets/scss/_tokens.scss`, `_bootstrap-overrides.scss`, `_base.scss`, `_typography.scss`, `_utilities.scss`; update `app.scss` import order
- Test: `src/AcxiomCrm.Web/scripts/contrast.test.mjs`

**Interfaces:**
- Produces CSS custom properties `--ax-*` from DESIGN-SYSTEM §2 and Bootstrap variable overrides (`$primary: #0F5C5A`, `$body-bg`, `$body-color`, `$border-color`, `$border-radius: 4px`, `$border-radius-lg: 6px`, `$font-family-sans-serif`, `$font-size-base: .875rem`, `$btn-*`, `$input-*`, `$table-*` dense padding, `$enable-shadows: false`, `$enable-gradients: false`, `$enable-rounded: true`).
- Helper classes: `.ax-label`, `.ax-num` (tabular, right-aligned), `.ax-code` (Plex Mono), `.ax-muted`, `.ax-visually-hidden`.

- [ ] **Step 1: Write failing test** `contrast.test.mjs`: parse `_tokens.scss`, and assert WCAG contrast ≥ 4.5 for text/background pairs: `text/canvas`, `text/surface`, `text-muted/surface`, `accent/surface` (links), `white/accent` (primary button), `ink-text/ink`, each semantic colour on its `-soft` background; ≥ 3 for `border-strong/surface` (input borders) and focus ring on canvas and surface.
- [ ] **Step 2: Run** `npm --prefix src/AcxiomCrm.Web test`. Expected: FAIL (tokens missing).
- [ ] **Step 3: Implement** the tokens and overrides. Adjust a token only if the contrast test proves it fails, and mirror any change into DESIGN-SYSTEM §2 in the same commit.
- [ ] **Step 4: Run** build + tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat(design): tokens, Bootstrap theme overrides and typography`.

---

## Task 3: Layouts, navigation and flash messages

**Files:**
- Create: `src/AcxiomCrm.Web/Views/Shared/{_Layout,_AuthLayout,_FlashAlerts,_UserMenu,_Sidebar}.cshtml`, `Views/_ViewImports.cshtml`, `Views/_ViewStart.cshtml`
- Create: `src/AcxiomCrm.Web/Navigation/{NavItem,NavGroup,NavigationProvider}.cs`
- Create: `src/AcxiomCrm.Web/Services/{IFlash,TempDataFlash}.cs`, `Web/Presentation/Labels.cs`
- Create: `assets/scss/components/{_shell,_nav,_flash}.scss`, `assets/js/modules/{sidebar,flash}.js`
- Test: `tests/AcxiomCrm.Web.Tests/{NavigationVisibilityTests,ShellLayoutTests,FlashTests}.cs`

**Interfaces:**
- Produces:
  - `public sealed record NavItem(string Label, string Icon, string Controller, string Action, string Permission, int? Badge = null)`; `NavigationProvider.GetFor(ClaimsPrincipal user) : IReadOnlyList<NavGroup>` returning groups **Workspace** (Dashboard), **Sales** (Customers, Leads, Opportunities), **Activity** (Follow-ups, Activities), **Insights** (Reports), **Administration** (Users, Roles, Audit log). Items not yet built by later plans are registered with `Enabled = false` and omitted from the output until their plan flips the flag via `NavigationProvider.Register(...)` in its own setup class; each module plan adds its own item.
  - `public interface IFlash { void Success(string msg); void Error(string msg); void Warning(string msg); void Info(string msg); IReadOnlyList<FlashMessage> Drain(); }` backed by TempData (JSON).
  - Layout sections: `Title` (page `<title>` = `{Title} · AcxiomCRM`), `Header` (title, context, actions), `Scripts`; `ViewData["Page"]` becomes `data-page` on `<body>` for per-page JS loading.

- [ ] **Step 1: Write failing tests.**
  - `NavigationVisibilityTests` (Theory role × item): Admin sees Administration group; Manager sees Sales/Activity/Insights, no Administration (Roles/Audit hidden; Users hidden unless `users.view`); SalesExecutive sees Workspace/Sales/Activity/Insights only; groups with zero visible items are not rendered.
  - `ShellLayoutTests`: authenticated page has landmarks `header`, `nav[aria-label="Primary"]`, `main#main`, `footer`; first focusable element is the skip link targeting `#main`; one `<h1>`; `<title>` pattern; `<html lang="en">`; current nav item has `aria-current="page"`; user menu shows full name and role label (`Sales executive`), not the email.
  - `FlashTests`: success flash after a POST-redirect renders once with `role="status"`, errors with `role="alert"`; message is HTML-encoded (`<b>` in a message appears escaped); a second request shows nothing.
- [ ] **Step 2: Run** `--filter "NavigationVisibilityTests|ShellLayoutTests|FlashTests"`. Expected: FAIL.
- [ ] **Step 3: Implement.** Sidebar 232 px (collapsed 64 px) with state in `localStorage` (try/catch), applied by a head module before paint. Off-canvas under 992 px with a menu button (`aria-expanded`, `aria-controls`), Esc closes, focus returns to the button. Top bar 52 px with page-independent user menu (Bootstrap dropdown). Notification bell and global search are rendered only when their provider is registered (plans 07 and 11). Footer: product name, version from assembly info, no marketing copy.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(shell): layouts, role-aware navigation and flash messages`.

---

## Task 4: Shared components (pager, field rows, badges, empty state, confirm, formatters)

**Files:**
- Create: `Views/Shared/{_Pager,_FieldRow,_StatusBadge,_EmptyState,_ConfirmModal,_FilterBar,_PageHeader,_ValidationSummary}.cshtml`
- Create: `src/AcxiomCrm.Web/TagHelpers/{MoneyTagHelper,DateLocalTagHelper,SortHeaderTagHelper,CspNonceTagHelper}.cs`
- Create: `src/AcxiomCrm.Web/Presentation/{PagerModel,StatusBadgeModel,Formatters}.cs`
- Create: `assets/scss/components/{_buttons,_forms,_table,_badge,_pager,_modal,_empty,_card,_timeline,_filterbar}.scss`
- Create: `assets/js/modules/{confirm,forms,format}.js`, `assets/js/format.test.mjs`
- Test: `tests/AcxiomCrm.Web.Tests/{FormattersTests,PagerTests,StatusBadgeTests,TagHelperTests}.cs`

**Interfaces:**
- Produces:
  - `Formatters.Money(decimal amount) → "₹12,50,000"` (no decimals when whole, else 2); `Formatters.MoneyCompact(decimal) → "₹12.5L"`/`"₹3.2Cr"`/`"₹45K"`; `Formatters.Date(DateOnly)`, `Formatters.DateTime(DateTime utc, IClock)` per DESIGN-SYSTEM §5.
  - `PagerModel(int Page, int PageSize, int Total, string RouteAction, IDictionary<string,string?> Query)` with `TotalPages`, `FirstItem`, `LastItem`, page-window logic (first, last, current ± 2, ellipses).
  - `<ax-money value="@x" />`, `<ax-date value="@d" />`, `<ax-datetime value="@dt" />`, `<th ax-sort="name" ax-current-sort="@Model.Sort" ax-desc="@Model.Desc">` generating a link with `aria-sort`.
  - `_StatusBadge` takes `(string Domain, Enum value)` and renders dot + display label with the colour mapping from DESIGN-SYSTEM §2 (lead statuses: New slate, Contacted info, Qualified teal, Unqualified warning, Converted success, Lost danger; opportunity stages and statuses; follow-up statuses incl. derived "Overdue" danger; customer Active success / Inactive neutral).
  - `data-confirm="Message"` on a `<button type="submit">` opens `_ConfirmModal` (focus trap, Esc, focus return, destructive variant) before submitting. Without JS the form posts normally (progressive enhancement) so server rules still protect.

- [ ] **Step 1: Write failing tests.**
  - `FormattersTests` (Theory): `1250000 → "₹12,50,000"`, `99999 → "₹99,999"`, `1234.5 → "₹1,234.50"`, `0 → "₹0"`, `-500 → "-₹500"`; compact `1250000 → "₹12.5L"`, `32000000 → "₹3.2Cr"`, `45000 → "₹45K"`, `999 → "₹999"`; `Date(2026-10-16) → "16 Oct 2026"`; `DateTime(2026-10-16T10:00Z)` → `"16 Oct 2026, 3:30 pm"` in IST.
  - `PagerTests`: window for page 1/10, 5/10, 10/10; `FirstItem/LastItem` ("26–50 of 112"); single page renders no pager; query string preserved and `page` replaced.
  - `StatusBadgeTests`: each enum value of the 6 status domains has a label and class (reflection over `Enum.GetValues` so a new enum value without a mapping fails); output has text, not only colour; unknown value throws in Development.
  - `TagHelperTests`: `ax-money` encodes and emits `ax-num`; `ax-sort` toggles direction and sets `aria-sort`.
  - `format.test.mjs` (node): client formatter mirrors the server for the same inputs (used by chart tooltips in plan 11).
- [ ] **Step 2: Run** `dotnet test --filter "FormattersTests|PagerTests|StatusBadgeTests|TagHelperTests"` and `npm test`. Expected: FAIL.
- [ ] **Step 3: Implement.** Labels centralised in `Labels.cs`; add a `LabelsTests.Every_enum_value_has_a_human_label_without_PascalCase_leakage` (no label equals `value.ToString()` when that contains an uppercase letter after the first character).
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(design): shared components, tag helpers and formatters`.

---

## Task 5: Form behaviour and client-side validation integration

**Files:**
- Modify: `assets/js/modules/forms.js`; create `assets/js/modules/validation.js`
- Create: `Views/Shared/_ValidationScriptsPartial.cshtml` (loads jQuery, jquery-validate, unobtrusive with the nonce)
- Test: `tests/AcxiomCrm.E2E/{PlaywrightFixture,AppHost}.cs`, `tests/AcxiomCrm.E2E/ShellBehaviourTests.cs`

**Interfaces:**
- Produces: validation module that (a) places messages in the `ax-field` error slot with `aria-describedby`, `aria-invalid`, (b) on a failed submit focuses the first invalid control and fills a polite `aria-live` summary ("2 fields need attention"), (c) disables the submit button and shows a busy state after a valid submit, restoring on `pageshow`.
- `AppHost`: starts `AcxiomCrm.Web` as a child process (`dotnet run --no-build`, random free port, `Environment=Testing`, DB from fixture), waits for `/health/ready`, exposes `BaseUrl`; `PlaywrightFixture` manages browser + context per test, trace on failure to `TestResults/traces`.

- [ ] **Step 1: Write failing tests** in `ShellBehaviourTests` (E2E, Playwright): `Skip_link_moves_focus_to_main`; `Sidebar_collapse_persists_across_reload`; `Confirm_modal_traps_focus_and_Esc_returns_focus` (use a temporary test-only page `/_dev/components` registered only in Testing env, rendering one of each component, to avoid dependence on later modules); `Failed_submit_focuses_first_invalid_field_and_announces_summary` (login form with empty fields); `Double_click_submits_once` (count requests); `No_horizontal_scroll_at_768` (`document.documentElement.scrollWidth <= innerWidth`) on login and the component page; `No_external_requests` (record all requests; every host equals the app host).
- [ ] **Step 2: Run** `dotnet test tests/AcxiomCrm.E2E --filter ShellBehaviourTests` after `pwsh tests/AcxiomCrm.E2E/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium`. Expected: FAIL.
- [ ] **Step 3: Implement** modules and the dev-only components page (`/_dev/components`: guarded by `env.IsEnvironment("Testing") || env.IsDevelopment()`; returns 404 otherwise, with a test asserting 404 in Production).
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(shell): accessible form behaviour and E2E harness`.

---

## Task 6: Auth pages, error pages and dashboard placeholder styling

**Files:**
- Modify: `Views/Account/*.cshtml`, `Views/Shared/_AuthLayout.cshtml`; create `assets/scss/pages/{_auth,_error}.scss`
- Create: `src/AcxiomCrm.Web/Controllers/ErrorController.cs`, `Views/Error/{Error,Forbidden,NotFound}.cshtml`, `ViewModels/ErrorViewModel.cs`
- Modify: `Views/Dashboard/Index.cshtml` (empty-state placeholder: "Dashboard widgets arrive with reporting", visible only until plan 11)
- Test: `tests/AcxiomCrm.Web.Tests/ErrorPagesTests.cs`

**Interfaces:**
- `ErrorController`: `/Error` (500), `/Error/{code}` for 403 and 404 via status-code re-execution. `ErrorViewModel(string CorrelationId, int StatusCode)`.
- Auth layout: split screen. Left panel `--ax-ink` with wordmark "AcxiomCRM" and a single line "Customers, pipeline and follow-ups in one place." Right panel form, max 400 px. Under 768 px the panel collapses to a thin brand bar.

- [ ] **Step 1: Write failing tests.** `ErrorPagesTests`: `Unknown_route_returns_404_view_with_correlation_id`; `Unhandled_exception_returns_500_view_without_message_or_stack` (a Testing-only endpoint throws `InvalidOperationException("secret detail")` and the body must not contain `secret detail` or `at AcxiomCrm`); `Forbidden_action_returns_403_view`; `Api_path_errors_return_problem_json_not_html`; `Development_environment_still_hides_exception_text_from_pages` (developer exception page disabled; details only in logs).
- [ ] **Step 2: Run** `--filter ErrorPagesTests`. Expected: FAIL.
- [ ] **Step 3: Implement** error pages and restyle auth views. Error pages: status code in `ax-code`, one sentence of explanation, primary action "Go to dashboard", correlation id displayed in a copyable `<code>`.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(shell): auth page design, friendly error pages`.

---

## Task 7: Design and accessibility review gate

**Files:**
- Create: `tests/AcxiomCrm.E2E/{AccessibilityTests,VisualCaptureTests}.cs`, `docs/design/screens/README.md`
- Modify: add `Deque.AxeCore.Playwright` to `Directory.Packages.props`

- [ ] **Step 1: Write failing tests.** `AccessibilityTests.Page_has_no_serious_or_critical_axe_violations` (Theory over `/Account/Login`, `/Account/Register`, `/_dev/components`, `/Dashboard` signed in, `/Error/404`): run axe (wcag2a, wcag2aa, wcag21aa, wcag22aa tags) and fail on `serious|critical`. `Keyboard_tab_order_reaches_every_nav_item`. `VisualCaptureTests.Captures_pages_at_1280_and_768` saves PNGs to `docs/design/screens/<page>-<width>.png` (not asserted).
- [ ] **Step 2: Run** E2E accessibility tests. Expected: FAIL initially if any violation exists; fix the markup/styles (not the test).
- [ ] **Step 3: Review** the screenshots against DESIGN-SYSTEM §6 (the 12-point anti-slop checklist). Record the outcome in `docs/design/screens/README.md` as a table `page | width | checklist result | notes`. Fix any "yes" answer before proceeding.
- [ ] **Step 4: Run** all E2E tests. Expected: PASS.
- [ ] **Step 5: Commit** `test(design): axe accessibility gate and screenshot review`.

---

## Task 8: Plan exit

- [x] Run Exit checks, tick the roadmap, commit `docs: close plan 03`.

## Exit checks
1. `npm --prefix src/AcxiomCrm.Web run build && npm test` green; `dotnet test` (Web.Tests + E2E) green.
2. Manual: run the app, log in as each role (bootstrap admin plus a registered user), confirm nav differences, collapse the sidebar, resize to 768 px, tab through the page, trigger a 404.
3. `git grep -nE "https?://" src/AcxiomCrm.Web/Views src/AcxiomCrm.Web/assets` shows no external asset URLs (documentation links excluded).
4. DESIGN-SYSTEM anti-slop checklist: all twelve answers "no", recorded in `docs/design/screens/README.md`.
