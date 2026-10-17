# line fixture (phase LN-F)

The fixture of the Sindri spec-to-proven-PR line (`docs/superpowers/specs/2026-10-15-sindri-spec-to-proven-pr-line-design.md`
§10.3, §13 LN-F). It extends `tests/fixtures/scoping/` (the team notes app) **by reference**: the repo, the map, the tracker
and the scoping gold stay there and nothing is copied. The design, plan and prove stations' check tests, the prompt evals
and the desk walk use it instead of work data.

**Everything here is synthetic.** The change is the scoping fixture's `expiry` ("shared links expire after a chosen time",
the tuning change); `archive` is held out and has a scope packet only. No real company, app or person appears. The canary
phrases are random word groups, and no file holds a secret-shaped string (the history script reads what it needs at run time).

| Part | What |
|---|---|
| `gold/scope/{expiry,archive}/` | The frozen **scope packet** of each change, the 13 files of scoping spec §8.3 (`scope-map`, `acceptance`, `coverage`, `lenses` …, `index`). Built from the scoping gold, so ids, texts and surfaces agree with it. `lenses.json`'s `hops` is an array of hop kinds from `state, dependency, flow, component` in cascade order (`[]` at depth 0), as many as `depth` |
| `gold/design/expiry/` | The gold **design** packet (§3.2 row 2): `spec.md` in the `plain` pack, `sections`, `coverage`, **three open decisions** (D1 the picker's choices, D2 what the expired page shows, **D3 where expiry runs**), `signals` (a measurable signal per item), `contracts` (one edit to decision 0001, quoted from the repo), `lens-rounds` (the `design-five` pack, two rounds), `style`, `escapes` (RG-4, the export regression the design adds), `runs`. `expiry.formal.spec.md` is the same design in the `formal` style |
| `gold/plan/expiry/` | The gold **plan** packet (§3.2 row 3, §6): `packet.json` (PKT's task plan, four phases with `writeScope`, test changes, hidden entries with no text), `graph.json` (waves, stacks, bases), `exec-review.json`, `holdout.json` (set hashes and counts), `answers.jsonl`. P1 the expiry field and migration; P2 the public route's expired page, stacked on P1; P3 the expiry picker, beside P2 (also on P1); P4 regression coverage for export and the digest, after both |
| `gold/freeze.json` | The freeze record of each packet (hash, `by: owner`) |
| `holdout/expiry/P1..P4.{set,seal}.json` | **Hidden-scenario sets** (§9.1): two scenarios and one canary scenario each, and the seal binding the set to the plan. In the real line they live outside every repo; they are here because they are fixture data |
| `packs/style/{plain,formal}/` | Two style packs (§5.1) that differ in headings, section limits, forbidden phrases, table rule, line cap and voice, so `DSN-STYLE-001` is tested both ways |
| `packs/lenses/{design-five,design-two}/` | The five-lens pack (product, architecture, design, devex, security; the default) and a two-lens pack (architecture, time-and-storage; the non-default one) |
| `profile-packs.json` | The profile keys that name the default packs, and the pins (§5.1) |
| `answers/owner.json` | Scripted owner answers at stop 2. Visit 1 picks **D3.b against the recommendation** and so forces one redesign of `sec-2.2` and `sec-3`, recording the conflict with AC-2 and widening P2 by `src/jobs/nightly.ts`; visit 2 approves. The scoping answers (stop 1) stay the scoping fixture's |
| `gold/lens/design-defect.json`, `gold/build/red.json`, `gold/prove/*` | The planted items' data (below) |
| `history/build.sh` | Builds the scoping history, then the branch `line/p1`: the red commit, the green one, a change outside allowedFiles, and a commit message with a canary. `golden-head.txt` pins main and the four tags |
| `planted.json` | The manifest of planted items, each tagged |
| `selfcheck.mjs` | The fixture's checks (below), and the check CLIs `style-check`, `diff-scope`, `leak-check` |

## What is planted

| Id | Item | Where | Found by |
|---|---|---|---|
| `PL-LENS` | A design lens finding that only a non-default pack raises: sec-3 stored as local time with no zone | `gold/lens/design-defect.json` | `design-two`'s `time-and-storage` lens names the triggers; no `design-five` prompt does |
| `PL-SCOPE` | A builder change outside `allowedFiles`: P1 edits `src/digest/digest.ts` | `history/build.sh`, tag `line/p1-out-of-scope` | `selfcheck.mjs diff-scope <repo> main line/p1-out-of-scope P1` exits 1 |
| `PL-RED` | A red test that passes on the red commit | `gold/build/red.json`, tag `line/p1-red` | exactly one `outcome: passed` |
| `PL-CANARY` | A hidden scenario's canary phrase in a commit message | tag `line/p1-canary` | the phrase is read from `holdout/expiry/P1.set.json` at run time and is in no other fixture file |
| `PL-LEAK` | An evaluator report whose observed line quotes its scenario | `gold/prove/holdout-result.leaky.json` | `selfcheck.mjs leak-check` (the 8-word overlap of §9.3) |

## What `selfcheck.mjs` checks

`node tests/fixtures/line/selfcheck.mjs` (no dependencies; exit 0 pass, 1 fail, 2 usage): every gold file's shape and keys; the
freeze records against the directories and the packet chain (scope, design, plan; the acceptance chain); SCP-LENS-001; the
design rules that run on plain files (`DSN-COV-001/002`, `ACC-001/002/003`, `DEC-001`, `CITE-001`, `XSPEC-001/002/003`, `STYLE-001`
under both packs, `LENS-001/002/003`, `ESC-001`); the plan's DAG (P2 on P1, P3 beside P2, P4 after both, P2 and P3 writing
disjoint paths), `PKT-SCOPE-001/002`, `PLN-SIZE/ACC/SIG/STACK/EXEC-001`; the hidden sets, seals and counts; each canary in its
set and nowhere else, and no 8-word run of scenario text outside the store (the planted report excepted); the two style packs
really differ; the pins; the owner answers; every planted item exists once and is tagged; no secret-shaped file.
`bash scripts/tests/line-fixture.test.sh` runs it, breaks a copy nine ways to show each class of check can fail, and builds the
history twice (same head, pinned tags).

## Using it

```bash
bash tests/fixtures/line/history/build.sh "$TMP"     # → $TMP/fixture-notes (the scoping repo plus line/p1), prints main's head
node tests/fixtures/line/selfcheck.mjs               # the fixture's own checks
node tests/fixtures/line/selfcheck.mjs style-check <spec.md> plain|formal
bash scripts/tests/line-fixture.test.sh              # the CI suite
```

## Choices (where the spec left a shape open; for review)

1. **Hashes.** `aw-packets hash` has no `scoping`, `design` or `plan` kind yet (LN-PKT1). Each packet's hash here is `dirHash`: SHA-256 over the sorted `path NUL sha256(bytes) LF` lines of its files. `index.json` carries `parents` (hashes), and `acceptance: { self, extends }` (the file hash of its own `acceptance.json`, and of the one it extends). Pack pins use the same `dirHash`. These are stand-ins; the owner proof below swaps them for `aw-packets hash`.
2. **Plan packet status.** `packet.json` has `status: draft` and `hash: null`, as `aw-packets validate` reads it before a freeze; the frozen state is `gold/freeze.json`, the stand-in for the record `aw-packets freeze` writes. No phase packets (§3.2 row 4) are included: `step.plan.freeze` makes them by code from the plan.
3. **Gold design and plan are the candidate at stop 2 under the recommended answers** (settle `assumed`). The D3.b redesign is described by `answers/owner.json` (`expects.designRevision`: the sections to change, the conflict to record, P2's widened scope), not by a second design.
4. **Digest notice (AC-6) sits in P4.** The spec says P4 is regression coverage for export and the digest; AC-6 (the scope's afterAnswers item) must be in exactly one phase (`PLN-ACC-001`), and its code is `src/digest/digest.ts`, which only P4 writes. So P4 holds AC-6, RG-2 and RG-4.
5. **RG-4 (export unchanged) is added by the design.** The spec names export in P4 but the scoping gold lists export as a decoy. The design adds RG-4 citing sec-4, and records two `line.escape` rows (`DSN-ESC-001`).
6. **P3 depends on P1**, not on P2, so it is parallel to P2 and stacked on the same base; P4 has two unmerged deps and is `waitsForMerge`. Stacks in `graph.json` are numbers (P1 and P2 in stack 1, P3 in stack 2); a base rule is `default-branch` or `dependency-head`.
7. **Only `expiry` has a design, plan and hidden sets.** `archive` is held out and gets a scope packet only.
8. **The two-lens pack is the non-default one.** The built-in default is `design-five` (§5.2); `design-two` replaces the architecture lens's text and adds `time-and-storage`.
9. **Style pack sections are generic** (Summary, Behaviour, Data, Proof, Rows to other lanes), with `###` subsections for behaviours carrying the section ids (`sec-2.2`); the formal pack's headings share none of them.
10. **Hidden scenarios** have no `text` field; `steps` and `expected` are the text. The canary is a scenario of `kind: canary` with a `phrase`. A set's hash is the SHA-256 of its file; the seal also holds the SHA-256 of the plan's `packet.json`.
11. **The red test "passes on the red commit"** is recorded in `gold/build/red.json` (the proof pack's `red.json` shape, simplified); the repo is never compiled or run, so the suite checks the tag, the file and the counts, not a test run.
12. **Packet files are generated once and frozen.** There is no generator in the tree: an edit to a gold file fails the freeze check, which prints both hashes to paste into `gold/freeze.json` (and `index.json` of a child packet) in a reviewed change.
13. **Spec conflicts.** §6.1's `step.plan.freeze` freezes the design and plan together; the fixture records both in `freeze.json` at the same instant. `hops` follows the instruction (an array of hop kinds), not any integer reading.

## Remaining owner proof (what closes later)

Not built here, and the fixture is ready for each:

- `sindri design check --facts <dir> [--stage draft|evidence]` over `gold/design/expiry` (lands in **LN-K**): expect 0 findings, and findings for each broken copy the suite makes.
- `sindri plan check --facts <dir>` over `gold/plan/expiry` (**LN-K**): expect 0 findings; P2/P3 sharing a file must give `PLN-PKT-001`.
- `aw-packets validate gold/plan/expiry --repo <built fixture-notes>` (needs the plan packet kind, **LN-PKT1**), and `aw-packets hash`/`freeze` to replace the stand-in hashes of Choice 1.
- `sindri pack check packs/style/formal` and the other three packs (**LN-API4**); `recipe prompt-eval` over the seeds (**SC-E**, LN-S1).
- Holdout log scan on `line/p1-canary` (`PRV-HOLD-003`, **prove station**) and `PRV-HOLD-001/002` over the leaky report.
