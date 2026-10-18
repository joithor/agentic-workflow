# recipes/sindri

The recipes Sindri ships. Each is a YAML state machine (`docs/csharp/sindri-workflow.md`) with its eval cases under `evals/<recipe id>/`.

| Recipe | What | Spec |
|---|---|---|
| `workflow.item` | Route an item, claim it, run `workflow.step` as a child | engine spec §12 |
| `workflow.step` | Produce, check, verify one Step | engine spec §12 |
| `workflow.scoping` | Station 1 of the line: scope a brief, tracker item, idea or change; interview; challenge; park at the owner's stop with a scope packet | scoping spec §6.1 |
| `workflow.idea-to-brief` | Block recipe, the child of `workflow.scoping`'s `idea` branch: a short interview turns an idea into a cited brief | scoping spec §6.2 |
| `workflow.line` | A minimal first cut of the line spec's `workflow.line` (the full line, LN-L, grows it later): waits on `workflow.scoping` as a child, then on `workflow.implement` as the build child; ends `done`, `dropped` (scoping unresolved or cancelled) or `stuck` (build unresolved) | line spec §3.5, LN-WF8 |
| `workflow.build` | Station 4 of the line: one TDD builder per phase in its worktree (red tests, green code, scope gate, owner-granted scope amendments) | line spec §7.2 |
| `workflow.prove` | Station 5 of the line: prove every acceptance item on the head (hand test, verify and evidence as children, before/after, hidden scenarios), then freeze the proof pack | line spec §7.3 |
| The block recipes below | The small recipes bugfix and spec-to-PR runs are built from | decision-tree spec (DT-R2) |

```bash
sindri recipe validate recipes/sindri/workflow.scoping.yaml   # exit 0
sindri recipe eval recipes/sindri/workflow.scoping.yaml       # one line per case
```

## Scoping station

- The first input is `kind: payload`; its shape is `schemas/scoping/scope-input.json` (a `oneOf` by `kind`: `brief`, `tracker`, `idea`, `change`).
- Every Step is descriptor-only until its runtime ships (`Aw.Sindri.Features/Blocks/Step/ScopingSteps.cs`), so each runs at the desk. Catalog: `docs/csharp/sindri-workflow.md` "Block catalog (scoping station)".
- `workflow.scoping` lists 25 eval cases: the 22 of the spec's Appendix B.1, `commit-not-held-accepted` and `not-synced-accepted` (the recorded limits beside `index-not-built-accepted`), and `idea-child-unresolved` (the child ends `unresolved`, so the parent is `dropped`). `workflow.idea-to-brief` lists `happy`, `cap`, `done-asking` and `empty`.
- The three `*-accepted` evals carry a coverage row with a recorded limit (`index-not-built` and `history-unlinked`, `commit-not-held`, `not-synced`) through `check pass` to `evidence complete`. The pure engine proves only that the path is reachable; whether `SCP-COV-002` passes the row is SC-K's `ScopePacketChecks`. Every eval uses one coverage shape (`schemas/scoping/coverage.json`).
- Eval payloads for a fact with a schema in `schemas/scoping/` are checked against it by `scripts/check-scoping-schemas.mjs`.
- The loop-cap and no-progress evals assert only the park reason `rounds` or `no-progress`, not which loop parked; the loops were traced by hand.
- The `refreeze` loop (cap 2) closes the `evidence`, `stop`, `freeze` cycle.

## Block recipes (decision-tree recipes, DT-R2)

The small recipes that bugfix and spec-to-PR runs are built from. Each is a child a parent waits for, or a recipe run by itself. Inputs marked `packet` and `payload` use those subject kinds.

| Recipe | Inputs | Outputs | Terminals (resolved / unresolved) |
|---|---|---|---|
| `workflow.report` | issue | resolution | written / - |
| `workflow.investigate` | issue, expected, actual | rootCause | diagnosed / unfixed (after 2 rounds) |
| `workflow.reproduce` | issue, rootCause | check, baseline | reproduced / not-reproduced |
| `workflow.implement` | issue, rootCause | output (the diff) | built / stuck (after 3 rounds) |
| `workflow.hand-test` | phasePacket, workspace | manualTest | works / broken |
| `workflow.verify` | phasePacket, workspace | evidencePack | passed / failed |
| `workflow.evidence-ui` | phasePacket, built | evidenceSummary | passed / failed (one repair of a broken selector) |
| `workflow.evidence-api`, `workflow.evidence-db` | phasePacket, built | evidenceSummary | passed / failed |
| `workflow.evidence-other` | phasePacket, built | evidenceSummary, evidenceReason | recorded / none-found |
| `workflow.evidence-all` | phasePacket, built | evidenceSummary | done / failed, dropped |
| `workflow.evidence` | phasePacket, built | evidenceSummary, decision | done / failed, all-failed, dropped |
| `workflow.review-loop` | change (payload), phasePacket | review | clean / stuck, wrong (failed) |
| `workflow.docs-sync` | pr, phasePacket (`{}` when started by hand) | docsResult, docsPr | opened / not-done; alreadySatisfied (no docs to change or sync off), abandoned (failed) |
| `workflow.ship` | phasePacket, proofPack, review, workspace, lineContext | pr, handoff, returnNotes | shipped / returned (GREEN red or head moved) , refused (failed) |

### Rules every block recipe follows

- A route (`step.judge.choose`) has its desk twin (`step.desk.choose`), and a failed route Step goes to the desk.
- A child wait routes `cancelled` on its own transition, which records nothing.
- `workflow.evidence` nests two deep (evidence, evidence-all, evidence-ui), inside the limit of 4 (`RCP-CHILD-003`).
- A parent keeps only the summaries its children's every path proves, so a parent waiting on `workflow.evidence` receives `evidenceSummary` on `resolved` only (`RCP-WIRE-003`). The instance's own facts carry it as well when a leaf child fails.
- `workflow.evidence`'s unresolved terminals (owner, 2026-10-17):
  - `failed`: a single leaf ran and did not hold. For `other`, that means no evidence was found.
  - `all-failed`: the `all` branch ended unresolved. `evidence-all` stops at its first failing check, so usually one of ui, api and db failed.
  - `dropped`: the child run it waited on was cancelled.

  All three declare `unresolved`, so a parent can't route on which one it was. The `workflow.evidence` child instance's state name shows it.
- `evidence.check` is descriptor-only: a recipe using it validates, and the engine refuses it at run time. Evals answer evidence checks as holding.

### Approval order

Approve leaf recipes first (`report`, `investigate`, `reproduce`, `implement`, `hand-test`, `verify`, `evidence-ui`, `evidence-api`, `evidence-db`, `evidence-other`, `review-loop`), then `evidence-all`, then `evidence`. A child resolves beside its parent, from the same directory.

### Applied from the decision-tree spec

DTC2 to DTC9 as the plan reads them. Out of this change: seeds (DT-R3), `workflow.implement-split` (needs an areas signal, DT-B), and the `branch` output of `workflow.implement` (no Step writes it yet).

`workflow.implement` exports the Step's `fact:output` under that key, since only a child's outputs can be renamed (`RCP-WIRE-002`). `workflow.evidence` names `evidence-ui` as the `from` of its `evidenceSummary` output, a compromise: `from` is a declaration only, and the summary comes from whichever leaf ran. `workflow.reproduce` names `ui-check` as the producer of `check` and `baseline`; the test-check path produces them too.

### Known gaps

- A check cancelled inside `evidence-all` ends `workflow.evidence` in `all-failed`, not `dropped`. `evidence-all` reports it as its own unresolved `dropped`, because a recipe terminal can't declare `cancelled`.
- A change that touches two of ui, api and db is not an `all` change (`all` needs all three). The judge routes it to one leaf, or ends uncertain and the desk picks. Running two leaves needs DT-B or DT-E.

## Line stations: build and prove

- Every Step of `workflow.build` and `workflow.prove` is descriptor-only (`BuildSteps.cs`, `ProveSteps.cs`), so each parks with SND-WF-010 at the desk until its runtime ships. Only the owner may report `step.build.scope-desk`.
- `workflow.build` inputs: `fact:phasePacket`, `fact:workspace` (text), `fact:buildNotes`, `fact:lineContext`. Outputs: `fact:built`, `fact:change`, `fact:redTests`, `fact:activePacket`. Terminals: `built` (resolved), `stopped` (unresolved).
- `workflow.prove` inputs: `fact:phasePacket`, `fact:built`, `fact:workspace` (text), `fact:redTests`. Outputs: `fact:proofPack`, `fact:findings`. Terminals: `proven` (resolved), `failed` (unresolved, `fact:findings`). It waits on `workflow.hand-test`, `workflow.verify` and `workflow.evidence`.
- Every wait on the heavy-job slot parks with SND-WF-010 until the engine's heavy allocator is wired (RCP-RES-001).
