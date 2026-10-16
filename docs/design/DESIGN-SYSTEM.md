# AcxiomCRM — Design System and UI Direction

The interface must look designed, not generated. Sales people live in this tool all day. It should feel like a calm, precise instrument: dense where data is, quiet where it isn't. Bootstrap 5.3 is the structural base (grid, forms, utilities, modal/dropdown behaviour). Its **look** is replaced via Sass variable overrides and our own component styles. A default-Bootstrap blue-button look is a failure.

## 1. Direction

**Concept: "ledger".** A clean working surface, close to a well-made accounting or logistics tool. Neutral warm-grey canvas, white panels with hairline borders instead of shadows, one confident accent colour, tabular numerals, restrained motion. Information density is a feature: compact tables, aligned numbers, small caps labels.

**Reference feel** (not to copy): Linear's calm density, Stripe dashboard's number typography, Attio's lists. Nothing glossy.

## 2. Tokens (`src/AcxiomCrm.Web/assets/scss/_tokens.scss`, exposed also as CSS custom properties)

### Colour (light theme; dark theme is out of scope for v1 but tokens are CSS variables so it can be added)
| Token | Value | Use |
|---|---|---|
| `--ax-canvas` | `#F6F5F2` | app background (warm grey) |
| `--ax-surface` | `#FFFFFF` | cards, tables, modals |
| `--ax-surface-sunken` | `#EFEDE8` | table header, inputs disabled |
| `--ax-border` | `#DEDBD3` | hairlines |
| `--ax-border-strong` | `#C4C0B5` | input borders |
| `--ax-text` | `#1D1F23` | body (contrast on canvas ≥ 14:1) |
| `--ax-text-muted` | `#5C6068` | secondary (≥ 5.5:1 on surface) |
| `--ax-accent` | `#0F5C5A` | deep teal: primary action, links, active nav |
| `--ax-accent-hover` | `#0B4745` | |
| `--ax-accent-soft` | `#E1F0EE` | selected row/nav background |
| `--ax-ink` | `#14222B` | sidebar background (deep ink blue-black) |
| `--ax-ink-text` | `#C9D3D8` | sidebar text, active `#FFFFFF` |
| Semantic | success `#1E7A4C`, warning `#A5620B`, danger `#B3261E`, info `#1F5FA8`, each with `-soft` background tint ≥ 4.5:1 with its text | status badges, alerts |

Rules: **one** accent. No gradients. No purple/indigo-to-pink anything. No glassmorphism, blur, glow or neon. No drop shadows on cards (use border). The only shadow is a small one on floating layers (dropdown, modal, toast).

### Typography (self-hosted WOFF2, `font-display: swap`)
- UI: **IBM Plex Sans** 400/500/600. Data/codes/amounts: **IBM Plex Mono** only for record codes (`CUS-000123`) and audit IDs. Numbers in tables use Plex Sans with `font-variant-numeric: tabular-nums`.
- Scale (rem): 12 (caption/badge), 13 (table body, dense), 14 (body default), 16 (form input, prevents iOS zoom), 20 (section), 24 (page title), 32 (KPI figure, tabular). Line-height 1.45 body, 1.2 headings. Heading weight 600, letter-spacing −0.01em. Labels (`.ax-label`) 12px, 500, uppercase, +0.04em tracking, muted.
- No Inter/Roboto/Poppins defaults, no emoji in UI chrome.

### Space, shape, elevation
4-px base unit: 4, 8, 12, 16, 24, 32, 48. Radius: 4 (inputs/buttons/badges), 6 (cards/modals). No pill-shaped buttons; pills only for status badges. Border 1px. Focus ring: 2px `--ax-accent` with 2px offset, always visible (never `outline: none` without replacement). Min hit area 32px (dense) / 40px (touch).

### Iconography
One set (Lucide), inlined as an SVG sprite `wwwroot/img/icons.svg`, 16/20 px, 1.5 stroke, `currentColor`, `aria-hidden` when paired with text, labelled when alone. No emoji, no mixed icon families.

### Motion
Only functional: 120–180 ms ease-out for dropdowns, modals, toast, row highlight after save. No parallax, bounce, or count-up animations on KPIs. `prefers-reduced-motion: reduce` disables all transitions.

### Charts
Chart.js 4, self-hosted. Palette derived from the accent: teal ramp for sequential, and a fixed categorical set for statuses (New slate, Contacted blue, Qualified teal, Unqualified amber, Converted green, Lost red) that matches badge colours everywhere. Direct labels or a legend, gridlines in `--ax-border`, no 3D, no donut-by-default (use horizontal bars for stages and a line/column for monthly sales), tooltips with INR formatting, accessible: each chart has a visually hidden data table, and `role="img"` plus a summary `aria-label`.

## 3. Layout

- **App shell**: left sidebar 232px (collapsible to icon rail 64px, state persisted; off-canvas under 992px) in `--ax-ink`; top bar 52px with global search, notification bell, user menu; content max-width 1440 with 24px gutters. Page header: title, optional context line, right-aligned primary actions. Breadcrumbs only on detail pages.
- **List pages**: filter bar (inline, not a giant form), dense table (row height 40px), sticky header, sortable column headers with `aria-sort`, row actions in an overflow menu, bulk actions only where defined, pager with total and page-size, empty state with one clear action, loading skeleton rows for async parts.
- **Detail pages**: two-column. Left: summary facts in a definition list. Right: tabbed timeline (Activity, Follow-ups, Opportunities, Audit trail). Primary action top right.
- **Forms**: single column up to 640px or two-column grid for long forms, label above field, hint under label, error under field (text + icon + `aria-describedby`), required marked in the label text not only a red asterisk, submit button right-aligned with a secondary cancel link, disabled state while submitting.
- **Auth pages**: split layout. Left a calm brand panel (solid `--ax-ink`, wordmark, one-line product statement, no stock illustration), right the form.
- **Opportunity board**: columns per stage with sum and weighted sum in the header, cards showing name, customer, amount (INR), close date with overdue marker, owner initials. Keyboard accessible move menu on each card in addition to drag.
- **Dashboard**: KPI strip of 8 cards (figure, label, period delta), then 2 charts in a row, then monthly sales full width, then role-specific panels. KPI cards are quiet: no coloured icon circles.

## 4. Components (each is a partial/tag helper with a documented class API)

`ax-btn` (primary, secondary, quiet, danger, sizes sm/md), `ax-field` (label/hint/error wrapper), `ax-badge` (status with dot + text, never colour alone), `ax-table`, `ax-pager`, `ax-filterbar`, `ax-card`, `ax-stat`, `ax-flash` (alerts and toasts, `role="status"`/`alert`), `ax-modal` (confirm dialog, focus trap, Esc closes, returns focus), `ax-empty`, `ax-avatar` (initials), `ax-nav`, `ax-timeline`, `ax-money`, `ax-datetime`.

Status-to-badge map is defined once in `_StatusBadge.cshtml` for each enum.

## 5. Content and microcopy
Sentence case everywhere. Verbs on buttons ("Create customer", "Save changes", "Convert lead"). Errors say what to do ("Enter a valid email address."). Dates `16 Oct 2026`, with time `16 Oct 2026, 3:30 pm`; amounts `₹12,50,000` (en-IN grouping), compact on charts (`₹12.5L`). Empty states explain the next step in one line. No exclamation marks, no "Oops", no "Awesome", no "Welcome back!" headlines. Seed/demo content uses believable names, companies and cities. No lorem ipsum.

## 6. Anti-slop checklist (a reviewer blocks the PR on any "yes")
1. Default Bootstrap primary blue, or any gradient or glow?
2. Cards with soft big shadows and 16px+ radius everywhere?
3. Coloured icon circles on KPI cards, emoji, or stock illustrations?
4. Centered hero-style empty states with giant icons?
5. Inconsistent spacing (not on the 4-px scale) or more than 2 font families?
6. Placeholder-as-label inputs, or colour-only status?
7. Lorem ipsum, "John Doe", "Test 1" in screenshots/demo data?
8. Tables without alignment rules (numbers not right-aligned and tabular)?
9. Modal for something that should be a page, or `alert()`/`confirm()` used?
10. Raw enum/DB values shown to users (`SalesExecutive`, `InProgress`) instead of display labels ("Sales executive")?
11. Any inline script/style or a CDN dependency?
12. Layout breaks at 768px, or horizontal page scroll?

## 7. Accessibility baseline
Landmarks (`header`, `nav`, `main`, `footer`), skip link, one `h1` per page, logical heading order, labelled controls, visible focus, ≥ 4.5:1 contrast (3:1 for large text/UI components), keyboard operation for menus/modals/board, `aria-live` for flash messages and async result counts, status never conveyed by colour alone, table headers with `scope`, error summary at the top of long forms linking to fields. Automated axe checks run in E2E on every primary page.

## 8. Front-end build
`src/AcxiomCrm.Web/package.json` scripts: `build` (Dart Sass compile + copy vendor libs to `wwwroot/lib`), `watch`. Sources in `assets/scss` (`_tokens`, `_bootstrap-overrides`, `_base`, `components/*`, `pages/*`, `app.scss`) and `assets/js`. Vendor: bootstrap, jquery, jquery-validation, jquery-validation-unobtrusive, chart.js, fonts (`@fontsource/ibm-plex-sans`, `ibm-plex-mono`), lucide sprite. Output is committed? **No**: `wwwroot/lib` and `wwwroot/css` are generated in CI and Docker build; `.gitignore` excludes them.
