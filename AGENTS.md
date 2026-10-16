# AGENTS.md — AcxiomCRM

Instructions for any coding agent (Antigravity, Codex, Claude Code). Read this first, every session.

## What this is
AcxiomCRM: a role-based CRM on .NET 10 / ASP.NET Core MVC / EF Core / Identity / SQL Server. Everything is specified in `docs/`. **Code must follow the docs. If the docs are wrong or silent, stop and record the question in `docs/OPEN-QUESTIONS.md`, don't invent behaviour.**

## Scope level (READ FIRST)
This build is **college / dev level**, not production. `docs/SCOPE-LITE.md` overrides the plans wherever they conflict: it lists what to keep (all mandatory modules, entities and fields) and what to cut. Do not build or test anything on its "Cut" list.

## Reading order
1. `docs/prd/PRD.md` (requirements and the decisions table D-xx; never contradict it)
2. `docs/architecture/ARCHITECTURE.md` (names, layers, contracts)
3. `docs/architecture/DATA-MODEL.md`, `SECURITY-AUTHZ.md`, `docs/api/API-SPEC.md`
4. `docs/design/DESIGN-SYSTEM.md` before touching any view/CSS/JS
5. The plan you are executing: `docs/superpowers/plans/` (start from `00-master-roadmap`)

## How to work
- Execute **one plan task at a time, in order**. Each step has a checkbox: tick it in the plan file when done.
- TDD: write the failing test, run it and see it fail for the right reason, implement, see it pass, then commit. Do not write production code without a failing test, except scaffolding tasks that say so.
- Commit **locally** after every task with a Conventional Commit message (`feat(customers): …`). Never commit failing tests. Never `--no-verify`. Never push (hard rule 0).
- Do not start a task whose dependencies (listed in the plan header) are not merged/complete.
- Names, signatures and file paths in the plan's **Interfaces** blocks are contracts. Use them exactly. If you must deviate, update ARCHITECTURE.md and the plan in the same commit and say why in the commit body.
- Keep scope tight: no extra features, no speculative abstractions, no new dependencies without adding them to `Directory.Packages.props` and noting why in the commit.

## Commands (valid after plan 01)
```
dotnet build -warnaserror
dotnet test                                   # needs Docker for SQL Server (Testcontainers)
dotnet test --filter "FullyQualifiedName~Customer"
dotnet format --verify-no-changes
npm --prefix src/AcxiomCrm.Web run build      # front-end assets
docker compose up -d db                       # local SQL Server on 1433
dotnet run --project src/AcxiomCrm.Web
```
If Docker is unavailable, set `TEST_DB_CONNECTION` to a SQL Server instance the tests may create/drop databases on.

## Hard rules (never break)
0. **Local only. Never publish.** This project is not on GitHub and the owner will publish it manually. Never run `git push`, `git remote add`, `gh` (any subcommand), or create/open PRs, forks or remotes. Never configure credentials or tokens. If `git remote -v` shows anything, stop and tell the owner. If a plan step says "push", "PR" or "CI run", skip it and note it in `docs/DEVIATIONS.md`. Local commits on local branches are the only git writes allowed.
1. No business rules in controllers or views; they live in Domain/Application.
2. Every owned-record query uses `InScope(...)`. Missing record or out-of-scope → `NotFound`.
3. Server-side validation always. Client-side validation mirrors it, never replaces it.
4. No `DateTime.Now/UtcNow/Today`; use `IClock`. No `FromSqlRaw`/`ExecuteSqlRaw` with concatenation.
5. No secrets, passwords or tokens in source, config files, logs or audit records.
6. No inline `<script>`/`style=` and no CDN assets (CSP). No `Html.Raw` on user data.
7. UI must satisfy `docs/design/DESIGN-SYSTEM.md`, including the anti-slop checklist. No lorem ipsum, emoji or default Bootstrap look.
8. No mocks for the database. Tests use the SQL Server fixture.
9. Canonical validation message strings are in PRD §4. Use them verbatim.
10. Do not edit generated migration files by hand except to add the SQL the plan names.

## Definition of done (per task)
Tests written first and passing; `dotnet build -warnaserror` clean; `dotnet format --verify-no-changes` clean; relevant PRD requirement IDs are covered by tests named in `docs/qa/TEST-STRATEGY.md`; plan checkboxes ticked; committed.

## Antigravity / Codex notes
- Prefer running tasks as separate, small agent sessions (one task each) with this file plus the plan file as context. Do not paste the whole `docs/` tree into one prompt.
- Terminal commands are long-running (`dotnet test` with containers): run them to completion and read the result. Do not assume success.
- When a step says "Expected: FAIL", a pass means the test is wrong. Fix the test.
