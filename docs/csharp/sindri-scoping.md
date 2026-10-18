# Sindri scoping station (C#): checks, stop view and desk helper

Spec: `docs/superpowers/specs/2026-10-15-sindri-scoping-station-design.md` (§8.2 the checks, §8.3 the packet, §8.4 the stop view).
This page is the reference for what phase SC-K1 built in `src/Sindri/Aw.Sindri.Features/Scoping/` and `Host/Commands/Scoping/`.

## Facts as files

A fact directory (a Step directory, W11) holds `<key>.json` for `fact:<key>` (`scopeMap.json`, `acceptance.json`, `coverage.json`,
`lenses.json`, `brief.json`, `answers.json`, `answerLog.json`, `challenge.json`, `openQuestions.json`, `questions.json`, `decision.json`,
`anchors.json`, `runs.json`) and `<key>.jsonl` for a list (`rounds.jsonl` is `fact:rounds`). `fact:scopePacket` is `{ "hash": "sha256:..." }`.
The gold facts are `tests/fixtures/scoping/facts/expiry/`: the line gold scope packet (`tests/fixtures/line/gold/scope/expiry/`) as facts. The two are one
document: `ScopingGoldFactsTests` asserts the facts equal the packet with `trust`, `schemaVersion` and the brief's `change` and `baseSha` removed.
Both carry a `noBehaviourChange` reason of its own on each surface that has no acceptance item (S22 and S23, the new behaviour, are cited by
AC-1 and AC-2), and every surface sits in exactly one of the two workstreams, so the gold passes SCP-ACC-002 and SCP-MAP-001 as written.

## Verbs

```
sindri scoping check (<address> | --facts <dir>) [--draft <dir>] [--stage draft|evidence] [--json]
sindri scoping show  (<address> | --facts <dir>) [--json]
sindri scoping desk  <block> --facts <dir> --out <dir> [--json]
```

Exit 0 pass, 1 findings, 2 usage, tool error or serve not running (`SND-API-001`, serve is never started). `--draft` overlays draft files and,
like `desk --out`, is refused inside a git worktree (`SND-SCOPING-002`); with an address it must sit under `$AW_SINDRI_HOME/workflow/runs/`.
`check --json` prints `{ stage, outcome, findings, warnings, rules }`; `findings` holds failures only and is the `fact:findings` body.
`show` run as a session (an agent) wraps every row's text and lines in the pack fence (`<untrusted source="scoping">…</untrusted>`, text and `--json`); a human gets the view plain.

## Rules

`SCP-CITE-001/002`, `SCP-COV-001..005`, `SCP-LENS-001`, `SCP-ACC-001..005`, `SCP-ANS-001`, `SCP-TRUST-001`, `SCP-CHAL-001/002`, `SCP-MAP-001` as in §8.2,
plus `SCP-SCHEMA-001` (SC-K's: a model's `scopeMap` and `acceptance` are validated against the schema's `$defs/fact` directly, whatever `schemaVersion` says).
Choices where the spec is silent: a surface is marked `noBehaviourChange: "<reason>"` (SCP-ACC-002/005); a regression item matches `continue to`;
SCP-COV-003 counts nodes of a lens that followed at most one edge, or within one edge of an anchor; SCP-COV-004 reads a brief ref with source `change`
and the `code:` anchors; SCP-ACC-004 checks uniqueness only; the vendor half of SCP-CHAL-002 is not enforced. A rule whose facts are missing is `not-run`
with the missing keys and never fails; at `evidence` it makes the outcome `incomplete`.

Coverage: a source that is `degraded`, or `unavailable`/`not-configured` without matching a row of the Recorded limits table on (lens, state, reason), fails
SCP-COV-002 (outcome `regather` when it is the only failing rule). The one fix-line map is `ScopeFixLines`; the view reads `not searched: <limit> — <fix line>`.
A `not-synced` source reads by its `sync` pair (`null`, queued, running, finished, failed with optional `retryAt` or `failures`).

## Desk helper

`desk` runs `intake`, `check`, `challenge-log`, `record`, `seed` and `evidence` (short or full ids). It writes each output fact to `<out>/<key>.json`,
`report.json` (`{ block, outcome, outputs: { "fact:<key>": "<file>" }, files, findings, warnings }`, what `workflow complete --outcome … --output fact:<key>=<file>` takes),
`check` appends `rounds.jsonl` to `<out>`, and every block reads the past rounds from `<out>/rounds.jsonl` when it exists (else from `--facts`), so the round number, trigger and diff are right when the two directories differ; and `evidence` writes `<out>/scope-packet/` (twelve files, trust stamped by code, `lenses.json` untrusted throughout) and
adds `fact:scopePacket` from `aw-packets hash --kind scoping` (`AW_PACKETS_BIN`, else `aw-packets` on the PATH). `ScopingDeskPacketsTests` runs this through the CLI against a stub that records its arguments (integration lane) and, in the heavy lane, against the built aw-packets: its hash, validate and freeze (under a temp `AW_STATE_DIR`) accept the packet the gold facts build.
Not covered yet: `read-brief`, `gather`, `anchor`, `reanchor`, `propose`.

## Not built yet (SC-K2)

The server's `ScopingView` route (API2), API9's packet-file route, the `scoping.approved` and `scoping.escape` event kinds, and the Ui catalog golden.

Deferred from the SC-K1 review (runs 1 and 2) to SC-K2 or later:

- The evidence-stage `SCP-TRUST-001` and the trust part of `SCP-LENS-001` read `pass` without checking anything, because trust is stamped only into the assembled packet: check the packet's trust once one exists (the freeze re-check needs it), or report `not-run` until then.
- `brief.md` in the packet uses its own backtick fence; API9 wraps a `.md` file in the pack fence (`UntrustedFence`), and the packet moves to it.
- `--draft` with an address is checked against the runs root, not that instance's run area, and skips the worktree check; resolve the instance first and share one `AW_SINDRI_HOME` resolution.
- `SCP-ACC-003` reads `openQuestions` and `questions` without `Need`-ing them, so a Step directory without them fails instead of `not-run`.
- `PacketsCli` does not kill the child process on timeout or Ctrl-C, and reports a tool failure as `InvalidInput`.
- The `noBehaviourChange` marker needs a spec amendment (§8.2 ACC-002 and ACC-005, A.1 and the SC-E seeds); it also means "covered through another surface" for some surfaces (owner decision, in the PR).
- `ScopeSchemas` caches parsed `JsonNode` documents in a static field; materialize them under the lock before `serve` shares the view (API2).
- `workflow.scoping.mapCoverageFloor` is the constant 0.5, to become the profile key.
- Fencing in `show --json` leaves `ScopingRow.Key` and the Open in Context fields plain; a human who pipes `show` is classed as a session, as in the context verbs, and the session test cannot tell the two callers apart under a redirected test host, so the plain-for-a-human path is untested.
- `ScopingGoldFactsTests` compares five of the facts with the line gold and not the stamped trust; compare `ScopePacket.Build` output with the gold files.
- The `archive` line gold scope packet passes SCP-MAP-001 and SCP-ACC-002 as `expiry` does (`ScopingGoldFactsTests` runs both), but it has no facts set; SC-K2's freeze re-check will run the remaining rules on it.
