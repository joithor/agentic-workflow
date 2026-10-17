# recipes/sindri

The recipes Sindri ships. Each is a YAML state machine (`docs/csharp/sindri-workflow.md`) with its eval cases under `evals/<recipe id>/`.

| Recipe | What | Spec |
|---|---|---|
| `workflow.item` | Route an item, claim it, run `workflow.step` as a child | engine spec §12 |
| `workflow.step` | Produce, check, verify one Step | engine spec §12 |
| `workflow.scoping` | Station 1 of the line: scope a brief, tracker item, idea or change; interview; challenge; park at the owner's stop with a scope packet | scoping spec §6.1 |
| `workflow.idea-to-brief` | Block recipe, the child of `workflow.scoping`'s `idea` branch: a short interview turns an idea into a cited brief | scoping spec §6.2 |

```bash
sindri recipe validate recipes/sindri/workflow.scoping.yaml   # exit 0
sindri recipe eval recipes/sindri/workflow.scoping.yaml       # one line per case
```

Scoping notes:

- The first input is `kind: payload`; its shape is `schemas/scoping/scope-input.json` (a `oneOf` by `kind`: `brief`, `tracker`, `idea`, `change`).
- Every Step is descriptor-only until its runtime ships (`Aw.Sindri.Features/Blocks/Step/ScopingSteps.cs`), so each runs at the desk. Catalog: `docs/csharp/sindri-workflow.md` "Block catalog (scoping station)".
- `workflow.scoping` lists 25 eval cases: the 22 of the spec's Appendix B.1, `commit-not-held-accepted` and `not-synced-accepted` (the recorded limits beside `index-not-built-accepted`), and `idea-child-unresolved` (the child ends `unresolved`, so the parent is `dropped`). `workflow.idea-to-brief` lists `happy`, `cap`, `done-asking` and `empty`.
- The three `*-accepted` evals carry a coverage row with a recorded limit (`index-not-built` and `history-unlinked`, `commit-not-held`, `not-synced`) through `check pass` to `evidence complete`. The pure engine proves only that the path is reachable; whether `SCP-COV-002` passes the row is SC-K's `ScopePacketChecks`. Every eval uses one coverage shape (`schemas/scoping/coverage.json`).
- Eval payloads for a fact with a schema in `schemas/scoping/` are checked against it by `scripts/check-scoping-schemas.mjs`.
- The loop-cap and no-progress evals assert only the park reason `rounds` or `no-progress`, not which loop parked; the loops were traced by hand.
- The `refreeze` loop (cap 2) closes the `evidence`, `stop`, `freeze` cycle.
