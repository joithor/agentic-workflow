# Agent readiness for agentic-workflow — design

Status: draft for review · 2026-10-09
Target: **Level 5 (Autonomous)** on Factory's agent-readiness framework. Level 4 is a prerequisite for the
factory work in `2026-10-09-sindri-factory-addendum.md` (§17.16). Level 5 is reached together with that work.

## 1. Why

The factory addendum asks agents to build, review and evolve this repo with little human direction. Agents can
only do that safely in a repo that tells them how to work and checks what they do: one command to build and test,
enforced style and types, fast and trustworthy CI, clear boundaries between packages, and machine-readable task
discovery. Today the repo has strict TypeScript and good tests per package, but none of it is enforced in CI. So
the refactoring comes first, and the factory is built on top of it.

This is also the first job for the core tenet: readiness becomes a Sindri module (§8) that keeps measuring the
repo and proposes the next fix.

## 2. The framework

Sources: [Factory agent readiness](https://factory.com/product/agent-readiness), [docs overview](https://docs.factory.com/agent-readiness/overview).

- **Levels:** 1 Functional · 2 Documented · 3 Standardized · 4 Optimized · 5 Autonomous. Passing **80% of a
  level's criteria** unlocks the next level.
- **Nine pillars:** Style & Validation · Build System · Testing · Documentation · Development Environment ·
  Debugging & Observability · Security · Task Discovery · Product & Experimentation.
- **Scope:** some criteria are evaluated once per repo (for example CODEOWNERS and branch protection). Others are
  evaluated per app, and a monorepo reports them as fractions such as `3 / 4`. Here the apps are `mcp-bridge`,
  `scorer`, `judge`, `sindri`, `skills/ui-evidence`, `skills/bugFixOrchestrator` and the `mods/aw-live` plugin.
- **Tools:** `/readiness-report` and `/readiness-fix` (Factory app or Droid CLI), plus a Readiness Reports API.
- **Gap:** Factory doesn't publish the full criteria list. Factory's own report is the score of record when we can
  run it. Our own check (§8) tracks the criteria we can reproduce, and it marks every criterion where our check and
  Factory's might disagree.

## 3. Where the repo stands (quick check, 2026-10-09)

| Pillar | Present | Missing |
|---|---|---|
| Style & Validation | `strict: true` in all six TS packages | No linter, no formatter, no pre-commit for lint or types, no `.editorconfig` |
| Build System | `npm run build` and `typecheck` per package; `setup.sh` | No root workspace or single command; no pinned Node version (`.nvmrc`); no lockfile policy across packages |
| Testing | Vitest in every package; bash test suites; real-browser test in ui-evidence | No CI running them; no test lanes or time budgets; no flaky-test detection; no coverage gate in CI |
| Documentation | `AGENTS.md` (with `CLAUDE.md` symlink), `README.md`, `planning/*`, rules index and `sync-rules.sh --check` | Freshness isn't checked automatically; commands in `AGENTS.md` aren't executed by a check |
| Development Environment | `setup.sh --dry-run`, `docs/onboarding.html` | No devcontainer, no `.env.example` |
| Debugging & Observability | JSONL tick logs and hook decision logs in sindri; scorer reports | No shared structured-logging convention, no metrics or tracing (OTel) for bridge or sindri |
| Security | Safety hooks, sindri scrubber pre-commit | **`main` is not protected**; no CODEOWNERS; no secret scanning in CI; no dependency update bot |
| Task Discovery | Plan files read by the plan-file tracker; 9 labels | No issue or PR templates; no labeling scheme |
| Product & Experimentation | scorer telemetry; judge prompt-sorter shadow mode; evolve offline comparisons | Not framed or reported as product analytics and experiments |

**Biggest gap:** there is **no CI at all** (`.github/workflows` doesn't exist), and `main` has no protection. The
merge gate in `AGENTS.md` is run by hand.

## 4. Engineering standards (what "ready" means here)

These match the standards set for the ring-2 product, adapted to this repo. They become rules (§17.6 of the
addendum) with deterministic checks wherever possible.

**Types**
- `strict` plus `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes` and `noImplicitOverride` in every
  package. Migration is per package, with a ratchet: a new error is a CI failure, and the old error count only
  goes down.
- No `any` outside Fastify boundaries (already a merge-gate item; now a lint rule). No non-null `!` assertions
  without a comment.
- Types are checked within a budget per package (`tsc --extendedDiagnostics` time is recorded in CI; a regression
  over 20% fails).

**Code shape**
- Each package is organized as a pure core (domain logic, no I/O), use cases, and adapters (I/O). Ports are
  injected (the `deps` pattern sindri already uses). Boundaries are checked by `dependency-cruiser`: core imports
  no adapter, and packages import each other only through declared entry points.
- Size and complexity budgets per file and function (for example ≤ 400 lines per file, cyclomatic complexity
  ≤ 15), enforced by the linter with a ratchet. The sindri shape signals (§6.2) report trends.
- One way to do each thing: a shared `Result` type, error codes, CLI output conventions and logging. Duplicates
  are listed in §6 and merged.

**Tests**
- Lanes: `unit` (no I/O, < 10 s per package), `integration` (in-memory SQLite and fakes, < 60 s), `heavy`
  (real browser, real index builds; run alone). Each test file declares its lane. A test that is too slow for its
  lane fails CI.
- Zero automatic retries. A flaky test is quarantined by a CI job that reruns failures, records them, and opens an
  issue.
- Coverage thresholds per package stay as `planning/TESTING.md` sets them, now enforced in CI.
- Every test follows `/testAudit`'s authoring gate.

**Lint and format**
- Decision R1 (§9): **Biome** for formatting and most lint rules (one fast tool), plus `typescript-eslint` only
  for the type-aware rules Biome lacks (floating promises, unsafe `any`) and `dependency-cruiser` for boundaries.
- `shellcheck` and `shfmt` for the bash in `config/`, `scripts/` and `providers/`.

## 5. Criteria plan by level

Each row is a criterion family, the change that satisfies it, and its owner workstream (§6).

| Level | Criterion family | Change | WS |
|---|---|---|---|
| 1 | README, linter, type checker, unit tests | Biome + typed lint; root scripts | W1, W2 |
| 2 | AGENTS.md, devcontainer, pre-commit hooks, branch protection | Devcontainer; pre-commit (format, lint, scrub, shape signals); protect `main` with required checks | W2, W5, W4 |
| 2 | Pinned dependencies, documented build | Root workspace, `.nvmrc`, lockfile per package checked in CI | W1 |
| 3 | Integration tests, tests runnable locally | Lanes, one root `test` command | W3 |
| 3 | Secret scanning, CODEOWNERS | GitHub secret scanning + push protection, a scrub job in CI, CODEOWNERS | W4 |
| 3 | Structured logging, tracing, metrics | One logger module (JSON lines, levels, ids); OTel spans and metrics in bridge and sindri (exporter off by default, local collector in the devcontainer) | W7 |
| 3 | Issue and PR templates, labels | Templates; label scheme (`type:`, `area:`, `size:`, `ring:`) synced from a file | W6 |
| 4 | Fast CI feedback | Required CI under 5 minutes for lint, types and unit tests; affected-package detection; heavy lanes nightly | W3 |
| 4 | Flaky-test detection | The quarantine job (§4) | W3 |
| 4 | Deployment frequency | "Release" here means promoting a stable channel (§7.7 of the main spec). Track promotion frequency and lead time | W8 |
| 4 | Documentation freshness | A check that executes every command in `AGENTS.md` and fails on drift; link checks; doc age against code changes | W8 |
| 4 | Product analytics and experiments | Report scorer and ledger telemetry as product analytics; evolve's A/B and the judge's shadow modes as experiment infrastructure, with a page that lists them | W9 |
| 5 | Self-improving systems | Sindri evolve (Plan 5), plus the readiness module proposing its own fixes (§8) | Addendum |
| 5 | Complex requirements decomposed into parallel execution | Packets, workflow generation and stacked epics (addendum F0–F3) | Addendum |

## 6. Workstreams (the refactoring basis)

| WS | Scope | Notes |
|---|---|---|
| W1 Workspace | Root `package.json` with npm workspaces for the six packages (keep per-package lockfiles or move to one, decision R2); `.nvmrc`; root `typecheck`, `test`, `lint`, `build` | Doesn't change how `setup.sh` builds |
| W2 Style | Biome config, typed lint, `.editorconfig`, shellcheck and shfmt; ratchet files for existing violations | Fix-forward in small PRs per package |
| W3 Tests and CI | GitHub Actions: lint, types, unit and integration per affected package, bash tests, `sync-rules --check`, `setup.sh --dry-run`, `claude plugin validate`; nightly heavy lane; quarantine job; time budgets | The `AGENTS.md` merge gate becomes required checks |
| W4 Security | Branch protection with required checks and linear history; CODEOWNERS; secret scanning and push protection; Dependabot (or Renovate) with grouped updates; scrub job in CI | Single maintainer: CODEOWNERS is for the protected modules (§7.7) |
| W5 Dev environment | Devcontainer (Node, uv, Docker-in-Docker for Serena and sindri containers, Ollama optional); `.env.example` covering every env var the packages read | |
| W6 Task discovery | Issue templates (bug, feature, skill change, idea card), PR template with the evidence section, label sync | Matches the addendum's work-item kinds |
| W7 Observability | One shared logging module; OTel in bridge and sindri; `doctor` reports exporter state | |
| W8 Docs and delivery | `AGENTS.md` command executor; freshness check; promotion metrics | |
| W9 Product signals | A page that maps existing telemetry and experiment machinery onto the pillar | Mostly documentation of what exists |
| W10 Code shape | Pure core / adapters split where a package mixes them; dependency-cruiser rules; size and complexity ratchets; merge duplicate utilities (locks, `Result`, error codes, CLI output, scrubbing) found by the code index's clone layer | The largest piece. Driven by the index's clone report, one package per PR |

**Order:** W1 → W2 → W3 → W4 come first, because they make every later change checkable. Then W6, W5, W7 and W8
run in parallel. W10 runs package by package once W3 is in. W9 last.

## 7. Turning it on

Every workstream ends with a "Turn it on" task, as Sindri plans do: the check becomes required on `main`, and the
next PR shows it passing.

## 8. Readiness as a Sindri module

- `sindri readiness check [--json]` runs our reproducible criteria, per repo and per app, and records each result
  in the ledger.
- When the Factory CLI is available, `sindri readiness import <report>` stores Factory's report as the score of
  record and lists every criterion where the two disagree.
- A weekly standing workflow at the `propose` tier turns the top failing criteria into work items, so the factory
  keeps the repo ready as it changes.
- Ring-1 repos get the same check through `sindri repo onboard` (record-only first).
- The criteria list is a registered artifact. Scouting (§17.18) watches Factory's published criteria for changes.

## 9. Decisions needed

| # | Decision | Recommendation |
|---|---|---|
| R1 | Lint and format tooling | Biome + typed `typescript-eslint` rules + dependency-cruiser |
| R2 | One lockfile vs per-package | Keep per-package lockfiles for now (`setup.sh` builds each); revisit after W1 |
| R3 | Branch protection for a solo maintainer | Required checks and no force pushes; admins may bypass only through a recorded label. The factory's own merges still go through the checks |
| R4 | Score of record | Factory's `/readiness-report` once Droid is installed; our check until then |
| R5 | Tighter TS flags | Adopt with a per-package ratchet, not a big-bang fix |

## 10. Rollout and sizing (rough, one builder with agents)

| Step | Contents | Size | Exit |
|---|---|---|---|
| R0 | Baseline: run Factory's report if possible, plus our check; record the level per pillar | XS | Baseline in the ledger |
| R1 | W1–W4 | M (1–2 weeks) | Level 2 passed, CI required on `main` |
| R2 | W5–W8 | M | Level 3 passed |
| R3 | W10 ratchets driven to targets, fast CI, flake quarantine, W9 | L (2–3 weeks) | Level 4 passed. **Factory work (F0) starts here** |
| R4 | Addendum F0–F3 plus the readiness module | With the addendum | Level 5 passed |
