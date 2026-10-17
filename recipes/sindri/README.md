# Block recipes (decision-tree recipes, DT-R2)

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

## Rules every block recipe follows

- A route (`step.judge.choose`) has its desk twin (`step.desk.choose`), and a failed route Step goes to the desk.
- A child wait routes `cancelled` on its own transition, which records nothing.
- `workflow.evidence` nests two deep (evidence, evidence-all, evidence-ui), inside the limit of 4 (`RCP-CHILD-003`).
- A parent keeps only the summaries its children's every path proves, so a parent waiting on `workflow.evidence` receives `evidenceSummary` on `resolved` only (`RCP-WIRE-003`). The instance's own facts carry it as well when a leaf child fails.
- `workflow.evidence`'s unresolved terminals (owner, 2026-10-17):
  - `failed`: a single leaf ran and did not hold.
  - `all-failed`: the `all` branch ended unresolved. `evidence-all` stops at its first failing check, so usually one of ui, api and db failed.
  - `dropped`: the child run it waited on was cancelled.

  A parent sees all three as `unresolved` and can't route on which one it was.
- `evidence.check` is descriptor-only: a recipe using it validates, and the engine refuses it at run time. Evals answer evidence checks as holding.

## Approval order

Approve leaf recipes first (`report`, `investigate`, `reproduce`, `implement`, `hand-test`, `verify`, `evidence-ui`, `evidence-api`, `evidence-db`, `evidence-other`, `review-loop`), then `evidence-all`, then `evidence`. A child resolves beside its parent, from the same directory.

## Applied from the decision-tree spec

DTC2 to DTC9 as the plan reads them. Out of this change: seeds (DT-R3), `workflow.implement-split` (needs an areas signal, DT-B), and the `branch` output of `workflow.implement` (no Step writes it yet).

`workflow.implement` exports the Step's `fact:output` under that key, since only a child's outputs can be renamed (`RCP-WIRE-002`). `workflow.evidence` names `evidence-ui` as the `from` of its `evidenceSummary` output, a compromise: `from` is a declaration only, and the summary comes from whichever leaf ran. `workflow.reproduce` names `ui-check` as the producer of `check` and `baseline`; the test-check path produces them too.

## Known gaps

- A check cancelled inside `evidence-all` ends `workflow.evidence` in `all-failed`, not `dropped`. `evidence-all` reports it as its own unresolved `dropped`, because a recipe terminal can't declare `cancelled`.

- A change that touches two of ui, api and db is not an `all` change (`all` needs all three). The judge routes it to one leaf, or ends uncertain and the desk picks. Running two leaves needs DT-B or DT-E.
