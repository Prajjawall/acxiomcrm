# Execution Guide for Antigravity / Codex

How to turn this plan set into the product without drift.

## 1. Setup (one time, human)
1. `git init`, commit the `docs/` tree and `AGENTS.md` as the baseline. **Do not add a remote.** Install the push blocker: `printf '#!/bin/sh\necho "Pushing is disabled for this repo."; exit 1\n' > .git/hooks/pre-push && chmod +x .git/hooks/pre-push`. Publishing to GitHub is done manually by the owner at the end.
2. Install: .NET 10 SDK, Node 22 LTS, Docker Desktop (or a SQL Server for tests), Git.
3. Create the `main` branch protections you want. Work on a branch per plan: `plan/01-foundation`, …
4. In Antigravity, add `AGENTS.md` as workspace rules. In Codex, it reads `AGENTS.md` automatically. Both support Claude-style skills; none are required (plans are plain Markdown).

## 2. Order and dependencies

Authoritative order is the **Run** column of `docs/superpowers/plans/2026-10-16-00-master-roadmap.md`:

```
 1 Foundation (01)
 2 Audit Part A (08, Tasks 1-4)        authentication needs IAuditWriter first
 3 Identity & authorisation (02)
 4 App shell & design system (03)
 5 Audit Part B (08, Tasks 5-6)        audit UI needs the shell
 6 Customers (04)   includes the "module kit" reused below
 7 Leads (05)
 8 Opportunities (06)
 9 Follow-ups, activities, timeline (07)
10 Users & roles (09)   ┐
11 REST API (10)        ├ independent after 9, parallelisable on branches
12 Dashboard, reports, search (11) ┘
13 Hardening & release (12)
```
Plan file numbers (01-12) are identifiers only. Migrations are serialised: rebase onto `main` before generating one. Plan 10's `until plan-11` tests and plan 05's `until plan-06` test are completed by the later plan named in them.

## 3. Per-plan prompt template

```
You are implementing plan <NN> of AcxiomCRM.
Read, in order: AGENTS.md, docs/prd/PRD.md (sections referenced in the plan), docs/architecture/ARCHITECTURE.md,
the architecture docs named in the plan header, then docs/superpowers/plans/<plan file>.
Execute tasks strictly in order. For every step: do exactly what it says, run the stated command,
compare with the stated expected output, tick the checkbox in the plan file, commit at the commit step.
If a command's output differs from expected, stop and diagnose before moving on.
If anything is ambiguous, write it to docs/OPEN-QUESTIONS.md and pick the most conservative option consistent with the PRD.
At the end run the plan's "Exit checks" and report each result.
```

Use a **fresh session per plan** (or per 3–4 tasks for the large plans) to avoid context bloat.

## 4. Review gates (human or reviewer agent)
After each plan:
1. Run the plan's *Exit checks* yourself.
2. `dotnet build -warnaserror && dotnet test && dotnet format --verify-no-changes`.
3. Diff review against the PRD IDs the plan claims. Check the traceability matrix rows turn green.
4. For UI plans: run the app, view each page at 1280 px and 768 px, run through the anti-slop checklist (DESIGN-SYSTEM §6).
5. Merge to `main` only when green.

## 5. When the plan and reality disagree
Library API changed, a package is unavailable, or a step is impossible: fix the smallest thing, record the deviation in `docs/DEVIATIONS.md` (what, why, impact), and update the affected plan text so later plans stay correct.

## 6. File index

| Path | Purpose |
|---|---|
| `docs/prd/PRD.md` | Requirements and decisions |
| `docs/architecture/ARCHITECTURE.md` | Layers, contracts, config |
| `docs/architecture/DATA-MODEL.md` | Schema |
| `docs/architecture/SECURITY-AUTHZ.md` | Permissions, scope, authn, hardening |
| `docs/api/API-SPEC.md` | REST contract |
| `docs/design/DESIGN-SYSTEM.md` | Tokens, components, anti-slop |
| `docs/qa/TEST-STRATEGY.md` | Test levels, traceability |
| `docs/ops/DEPLOYMENT.md` | Environments, config, release, runbook |
| `docs/superpowers/plans/*.md` | Executable plans 00–12 |
