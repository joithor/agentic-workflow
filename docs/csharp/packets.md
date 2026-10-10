# Packets — `aw-packets validate | freeze`

`Aw.Packets` (`src/Packets`, tier 1, small shape: `Aw.Packets.Core` + `Aw.Packets.App`) checks and freezes handoff
packets (factory addendum §17.4). Before the switch it is the standalone NativeAOT CLI `aw-packets`, which writes only
under `${AW_STATE_DIR:-~/.agentic-workflow}/packets/` (spec §4c); nothing installs or schedules it. After the switch,
sindri's C# host exposes the same use cases as `sindri packet …`.

It was generated with `/new-package` (the invocation is in the PR that added it) and grown slice by slice with
`/new-slice`.

## A packet directory

| File | Record | Holds |
|---|---|---|
| `packet.json` | `PacketManifest` | `schemaVersion`, `id` (lower-case letters, digits, `-`, ≤ 64), `repo`, `baseSha` (a full commit sha), `status` (`draft`, `frozen`), `hash`, `links` |
| `links.decisions` | `DecisionLog` | `questions[]`: `id`, `text`, `asker`, and either `answer` or `deferred: { owner, buildMayAssume }`; `affects[]` |
| `links.outOfScope` | `OutOfScope` | `items[]` |
| `links.tasks` | `TaskPlan` | `workstreams[]` (`id`, `title`) and `tasks[]`: `id`, `workstream`, `title`, `deps[]`, `size` (`XS`…`XL`), `writeScope[]` (globs, or `new: <path>`), `readSeeds[]`, `acceptance[]` (`id`, `text`, `evidence[]`, `holdout`), `rules[]`, `budget` (`tokens`, `turns`, `wallMinutes`) |
| `links.design[]`, `links.plans[]`, `links.workflow` | — | Any format; part of the hash by content |

Every JSON file carries `schemaVersion` (1). The JSON Schemas for editors are generated from the records and committed
in `schemas/packets/` (`PacketSchemaTests` fails when a record changes and the schema isn't regenerated). Links are
relative paths with `/` inside the packet directory; `..`, a root, a `:` or a symbolic link is refused.

Write-scope globs are repo-relative: `*` and `?` stay inside one path segment, `**` crosses segments (`src/**`,
`**/guide.md`). `new: src/c.cs` declares a file the task creates.

## Checks (`aw-packets validate <packet-dir> [--repo <dir>] [--json]`)

`--repo` is a directory inside the repository the packet builds (default: the packet directory). The base commit's
file list comes from git.

| Rule | Check |
|---|---|
| `PKT-ID-001` | Task, workstream, question and example ids are unique |
| `PKT-WS-001` | Every task is in a declared workstream |
| `PKT-GRAPH-001` | Every dependency names a task in the packet |
| `PKT-GRAPH-002` | The task graph is acyclic (each cycle reported once: `t1 -> t3 -> t2 -> t1`) |
| `PKT-BASE-001` | `baseSha` is a full commit sha that exists in the repository |
| `PKT-SCOPE-001` | Every write scope matches a file at the base commit, or is `new:` and doesn't exist there |
| `PKT-SCOPE-002` | Two tasks no dependency chain orders don't write the same path (a base file both match, or a declared new path) |
| `PKT-EVID-001` | Every acceptance example names at least one kebab-case evidence kind (addendum §17.11) |
| `PKT-DEC-001` | Every question is answered, or deferred with an owner and a `buildMayAssume` default |
| `PKT-HOLD-001` | At least one acceptance example is held out |
| `PKT-SIZE-001` | **Unchecked**: the size-class budgets aren't defined (open question below) |
| `PKT-HOLD-002` | **Unchecked**: where held-out examples are stored, away from the implementer, isn't defined |
| `PKT-FLOW-001` | **Unchecked**: workflow coverage (addendum §17.7) needs the lens catalog and workflow generation |

The text output lists every rule (`pass`, `FAIL` with the subject, what was found and the fix, or `unchecked` with the
reason). `--json` writes one document, `schemaVersion` 1, with `packet`, `passed` and `checks[]` (one record per
failure, one per passing or unchecked rule; each failure has `rule`, `subject`, `message` and `fix`).

## Freeze (`aw-packets freeze <packet-dir> [--repo <dir>] [--json]`)

Runs the same checks. A packet with a failed check isn't frozen and nothing is written. Otherwise its canonical hash
is recorded at `packets/<id>/frozen/<hex>.json` (`FreezeRecord`: packet, hash, `digestVersion`, `baseSha`,
`frozenAt`, the files the hash covers). Freezing the same hash again writes nothing and says `already frozen`. The
packet directory itself is never changed; the record under `packets/` is authoritative (the sindri ledger takes over
when sindri is ported, spec §4c).

**The hash (`digestVersion` 1, spec §4c Digests):** SHA-256 over the canonical JSON of
`{"digestVersion":1,"files":{<path>:<entry>}}`. A JSON packet file's entry is the file itself, except `packet.json`'s
`status` and `hash`; any other linked file's entry is `sha256:<hex of its bytes>`. Canonical JSON sorts object keys by
ordinal, drops whitespace, escapes only what JSON requires, and keeps numbers as written.

## Exit codes and errors

| Exit | When |
|---|---|
| 0 | Every implemented check passed (and, for `freeze`, the hash is recorded) |
| 1 | A check failed, or the packet is malformed (`AW-PACKETS-003`, `-004`) |
| 2 | A usage error, or the tool couldn't do its job: no packet (`AW-PACKETS-002`), an unreadable repository (`-006`) or packet file (`-008`), an unwritable freeze log (`-007`) |

Errors go to stderr as `aw-packets: <code> <message>` (with `--json`, the `Error` record).

## Ports and adapters

| Port (`Aw.Packets.Core`) | Adapter (`Aw.Packets.App.Infrastructure`) | Fake and contract (`tests/Aw.Packets.Testing`) |
|---|---|---|
| `IPacketSource` | `FileSystemPacketSource` | `InMemoryPacketSource`, `PacketSourceContract` |
| `IRepoSnapshot` | `GitRepoSnapshot` (`git ls-tree -r -z`, 30 s limit per run) | `InMemoryRepoSnapshot`, `RepoSnapshotContract` |
| `IFreezeLog` | `FileFreezeLog` (temp file, then move) | `InMemoryFreezeLog`, `FreezeLogContract` |
| `TimeProvider` (BCL) | `TimeProvider.System` | `FakeTimeProvider` |

## Open questions (the unchecked rules)

- **Size classes (`PKT-SIZE-001`):** what are the budgets (tokens, turns, wall minutes) for `XS`, `S`, `M`, `L` and
  `XL`, and is a task over its class's budget a failure or a split signal?
- **Holdout storage (`PKT-HOLD-002`):** where do held-out examples live so the implementer can't read them (a path
  outside the packet directory, a separate store), and what does the check compare?
- **Workflow coverage (`PKT-FLOW-001`):** the §17.7 coverage checks need the lens catalog and workflow generation (F3).
- **`packet.yaml` vs `packet.json`:** the addendum names `packet.yaml`; this build reads `packet.json` (typed,
  source-generated JSON under NativeAOT with no YAML library). Keep JSON, or add a YAML reader?
