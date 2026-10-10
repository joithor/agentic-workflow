# Rule records (C# §4b rules)

Every rule in the readiness spec's §4b (`docs/superpowers/specs/2026-10-09-agent-readiness-design.md`) has a **rule
record**: one YAML file that states the rule, why it exists, which code it applies to, and the check that enforces it.
The record shape is the factory addendum's (§17.6, `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md`),
plus the fields this repo needs to track gaps. Later, Sindri selects records into sessions and `sindri rules explain
<id>` prints one; neither exists yet (sindri is still TypeScript).

| Path | What |
|---|---|
| `rules/engineering/csharp/<slug>.yaml` | One record per rule; the file name is the id's last part |
| `rules/engineering/csharp.inventory.yaml` | The §4b rule list: every record id with its spec section |
| `schemas/rule-record.schema.json` | The record schema (JSON Schema, for editors and the check) |
| `scripts/check-rules.mjs` | The check (`node scripts/check-rules.mjs [--json]`; merge gate item 9, CI "Rule records name real checks") |
| `scripts/tests/check-rules.test.sh` | Its tests, on the fixture repo in `scripts/tests/fixtures/check-rules/base` |

Records use the same YAML subset as `deps.yaml` (block mappings and sequences, quoted scalars, flow lists of scalars,
comments; no flow mappings, anchors or block scalars), so the check needs no install step.

## A record

```yaml
schemaVersion: 1
id: engineering.csharp.no-catch-exception      # <kind>.<language>.<slug>; the file is <slug>.yaml
kind: engineering                              # taste | engineering | process | product-precedent | security | lens
section: "4b.3"                                # the spec heading the rule comes from (matches the inventory)
text: "No catch (Exception) outside Host and Infrastructure boundary adapters."
why: "Catching everything hides bugs that should crash loudly."
select:                                        # where the rule applies (addendum §17.6 selectors)
  languages: [csharp]
  paths: ["src/**"]
checks:                                        # what enforces it; several checks are fine
  - type: analyzer
    id: "CA1031"
    status: active                             # active: exists in this tree | pending: lands with pendingPr
gap: closed                                    # closed | partial | pending | open (below)
source:
  type: adr
  ref: "docs/superpowers/specs/2026-10-09-agent-readiness-design.md#4b3"
status: active                                 # proposed | active | retired
```

Optional fields: `gapNote` (what isn't covered, and how the rule is followed until it is), `relatedIds` (ids the C#
rules docs mention for this rule that aren't its checks, such as `MA0004`, switched off because `CA2007` owns
`ConfigureAwait`), and per check `ref`, `entries`, `pendingPr` and `note`.

### Check types

| `type` | `id` | Exists when |
|---|---|---|
| `analyzer` | a Roslyn diagnostic id (`CA1031`, `MA0051`, `AW1005`) | it appears in `.editorconfig`, `tests/.editorconfig`, `Directory.Build.*`, `CodeMetricsConfig.txt` or `src/Aw.Analyzers` |
| `compiler` | a `CS…` or `IL…` id | always (the compiler and trim analyzers); only the form is checked |
| `banned-symbols` | `RS0030`, with `entries` | `BannedSymbols.txt` lists every entry and `Directory.Build.props` wires the file |
| `archunit` | `ARCH-…` | it appears under `tests/Aw.Architecture.Tests` |
| `check-deps` | `DEPS-…` | it appears in `scripts/check-deps.mjs` or `scripts/deps/` |
| `lane-cap` | `LANE-…` | it appears under `tests/` |
| `check-rules` | `RULE-…` | it appears in `scripts/check-rules.mjs` |
| `build-setting` | a literal (`<IsAotCompatible>true</IsAotCompatible>`, `AW0001`) | it appears in `Directory.Build.*`, `Directory.Packages.props`, `global.json`, `.editorconfig` or `nuget.config` |
| `test` | a class or member name, with `ref` | `ref` (a file or directory) exists and contains the name |
| `script` | a name, with `ref` | `ref` exists |
| `ci` | a workflow step name, with `ref` | the workflow file contains the step name |
| `review` | a short label | never looked up: it isn't a deterministic check |

### Gap status

| `gap` | Means | The check requires |
|---|---|---|
| `closed` | An active deterministic check enforces the rule | at least one active check that isn't `review` |
| `partial` | An active check covers part of the rule | an active deterministic check and a `gapNote` saying what isn't covered |
| `pending` | A check in an open PR will cover it | at least one `pending` check with its `pendingPr` |
| `open` | No deterministic check yet; the rule is followed by hand | no active or pending deterministic check, and a `gapNote` |

A `pending` check names its PR (`pendingPr`) because the check isn't in this tree yet. When that PR merges, the check
reports `WARN RULE-CHECK-002 pending check now exists` (exit 0) until someone sets the check `status: active`, drops
`pendingPr` and updates `gap`. A check called `active` that the tree doesn't have fails (`RULE-CHECK-001`).

## What the check enforces

| Rule id | Fails when |
|---|---|
| `RULE-YAML-001` | a record or the inventory is outside the YAML subset |
| `RULE-SCHEMA-001` | a record doesn't match `schemas/rule-record.schema.json` |
| `RULE-ID-001` | a record's id doesn't end in its file name |
| `RULE-SPEC-001` | a record's `section` differs from the inventory's |
| `RULE-CHECK-001` | a check marked `active` isn't in the tree |
| `RULE-CHECK-002` | (warning) a `pending` check is now in the tree: flip it to `active` |
| `RULE-GAP-001` | `gap` disagrees with the checks (table above) |
| `RULE-COVER-001` | an inventory rule has no record |
| `RULE-COVER-002` | a check id that `.agents/rules/csharp.md` or `csharp-testing.md` names (`CA…`, `MA…`, `RS…`, `IDE…`, `AW…`, `DEPS-…`, `ARCH-…`, `LANE-…`, `RULE-…`) belongs to no record's `checks` or `relatedIds` |
| `RULE-COVER-003` | a record isn't in the inventory |
| `RULE-COVER-004` | an inventory section isn't a heading in the spec |

Every failure prints the rule id, what it measured, the limit and the fix. Exit codes: 0 ok (warnings allowed), 1 a rule
failed, 2 a tool error (a missing `rules/` directory or schema, an unknown argument).

## Changing rules

- **New check for an existing rule** (an analyzer id, an ArchUnitNET test): add it to the rule's record in the same PR,
  `status: active`, and update `gap`. If the PR also names the id in the C# rules docs, `RULE-COVER-002` makes that
  step impossible to forget.
- **A check that lands in another PR:** add it as `status: pending` with `pendingPr: <n>`; the warning tells whoever
  merges it to flip the record.
- **New §4b rule** (a spec change): add the record file and its inventory line together.
- **A rule leaves the spec:** set `status: retired` rather than deleting the record, and keep it in the inventory.
