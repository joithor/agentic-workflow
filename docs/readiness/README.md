# Readiness

Agent-readiness measurements for this repo (spec: `docs/superpowers/specs/2026-10-09-agent-readiness-design.md`,
§2, §4c, §8). `aw-readiness check` is the score of record (decision R4); no paid Factory report or Droid is used.

| File | What it is |
|---|---|
| `readiness.yaml` (repo root) | The repo's name, ring, scope and each ring's target level |
| `src/Readiness/` | `Aw.Readiness`: the `aw-readiness` NativeAOT CLI (`Aw.Readiness.Core` + `Aw.Readiness.App`) |
| `baseline-2026-10.md` | The R0 baseline of the TS apps, from the throwaway `scripts/readiness-baseline.mjs` |
| `aot-spike.md` | The R1a AOT spike (R7, R8, R9) |

## aw-readiness check

```bash
dotnet run --project src/Readiness/Aw.Readiness.App -- check            # JIT, from the repo root
bash src/Readiness/aot-smoke/run.sh                                     # publish NativeAOT → artifacts/aot-readiness/bin/aw-readiness
artifacts/aot-readiness/bin/aw-readiness check [--json] [--root DIR] [--no-remote]
```

- **Exit codes:** 0 when the level reaches the target for the repo's ring, 1 when it doesn't, 2 on a usage or tool
  error (an `AW-READINESS-NNN` error on stderr: 001 bad arguments, 002 root not found, 003 `readiness.yaml` missing or
  malformed, 004 history unreadable or unwritable).
- **States:** each criterion is `pass`, `fail`, `n/a` (with a reason) or `unknown` (the check can't be reproduced, or
  its data couldn't be read). Only pass and fail are scored. A failure prints the criterion's fix.
- **Levels:** every repo starts at Level 1; Level N+1 unlocks when at least 80% of the scored Level N criteria pass.
  A level with nothing scored neither unlocks nor blocks; the first scored level under 80% stops the climb. Reported
  per pillar and overall.
- **Delta:** each run is diffed against the repo's previous run. The first run records a baseline and says so.
- **History:** one JSON file per run under `${AW_STATE_DIR:-~/.agentic-workflow}/readiness/runs/<repo>/`. It writes
  nothing else (spec §4c), and nothing installs or schedules it (no `setup.sh`, provider or launchd entry).
- **Host criteria** (BS-4 releases, SEC-1 branch protection, SEC-2 secret scanning) come from read-only `gh repo view`
  and `gh api` GETs run in the checkout (`AW_READINESS_GH` overrides the `gh` binary). Offline (`--no-remote`) or when
  gh can't answer, they are `unknown`, never guessed.
- **Output:** text by default — every state is a word, so colour never carries meaning and `NO_COLOR` has nothing to
  strip. `--json` writes the report object, `schemaVersion: 1` (recorded in
  `tests/Aw.Readiness.Tests/Golden/report.json`): `repo`, `scope`, `recordedAt`, `target {ring, level}`, `level`,
  `holds`, `recordedTo`, `delta {since, levelBefore, levelAfter, changes[], isFirstRun}`, `pillars[]`, `tallies[]`,
  `findings[] {criterion {id, pillar, level, title, fix}, state, evidence}`. A field change bumps `schemaVersion`.

## Scope: the C# template

`scope: csharp-template` checks the C# build surface — `src/`, `tests/`, the root build files, the CI workflows,
`AGENTS.md`, the rules and the repo-level files (templates, devcontainer, CODEOWNERS, dependency updates) — against the
R0 baseline's 34 criteria, with the same ids and levels. The TS apps are not scored: they are frozen until the
cutover. The criteria live in `src/Readiness/Aw.Readiness.Core/Criteria/` (one file per pair of pillars); a change
to a criterion is a reviewed change to its check and its test in `tests/Aw.Readiness.Tests/Criteria/`.

## readiness.yaml

```yaml
version: 1                  # the only version
repo: agentic-workflow      # 1-64 of a-z 0-9 . _ - : the history's directory
ring: 0                     # which target below applies
scope: csharp-template      # the only scope
targets:                    # one item per ring; each ring at most once; level 1-5
  - ring: 0
    level: 4
```

A flat YAML subset (read without a library): `key: value` lines, the `targets:` list, `#` comments. Anything else is
refused with `AW-READINESS-003` naming the line, the key and the fix.

## The R0 baseline

`baseline-2026-10.md` scored the TS apps with `scripts/readiness-baseline.mjs`, a **throwaway** script (Node ≥ 20, no
dependencies). Its scope differs from `aw-readiness`'s (the TS apps, not the C# template), so `aw-readiness` does not
diff against it: its first run records its own baseline. The script stays until the switch for comparison.

```bash
node scripts/readiness-baseline.mjs --out docs/readiness/baseline-2026-10.md   # regenerate (reads gh api, GET only)
node scripts/readiness-baseline.mjs --no-remote                                # offline: remote criteria → unknown
bash scripts/tests/readiness-baseline.test.sh                                  # tests on fixture trees
```
