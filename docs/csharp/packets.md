# Packets — `aw-packets validate | freeze | hash`

`Aw.Packets` (`src/Packets`, tier 1, small shape: `Aw.Packets.Core` + `Aw.Packets.App`) checks and freezes handoff
packets (factory addendum §17.4). Before the switch it is the standalone NativeAOT CLI `aw-packets`, which writes only
under `${AW_STATE_DIR:-~/.agentic-workflow}/packets/` (spec §4c); nothing installs or schedules it. After the switch,
sindri offers them as `sindri packet …` by running `aw-packets` (a `cli` edge in `deps.yaml`; spec §4c, addendum §17.4).

It was generated with `/new-package` (the invocation is in the PR that added it) and grown slice by slice with
`/new-slice`.

## A packet directory

| File | Record | Holds |
|---|---|---|
| `packet.json` | `PacketManifest` | `schemaVersion`, `id` (`PacketId`: lower-case letters, digits, `-`, ≤ 64), `repo` (`RepoSlug`: `owner/name`, each side letters, digits, `.`, `_`, `-`, not ending in `.`, not a Windows device name), `baseSha` (text, so a short sha is a PKT-BASE-001 failure rather than a malformed packet), `status` (`draft`, `frozen`), `hash` (`PacketHash`: `sha256:` + 64 hex, or null), `links` |
| `links.decisions` | `DecisionLog` | `questions[]`: `id`, `text`, `asker`, and either `answer` or `deferred: { owner, buildMayAssume }`; `affects[]` |
| `links.outOfScope` | `OutOfScope` | `items[]` |
| `links.tasks` | `TaskPlan` | `workstreams[]` (`id`, `title`) and `tasks[]`: `id`, `workstream`, `title`, `deps[]`, `size` (`XS`…`XL`), `writeScope[]` (globs, or `new: <path>`), `readSeeds[]`, `acceptance[]` (`id`, `text` — none for a held-out example —, `evidence[]`, `holdout`), `rules[]`, `budget` (`tokens`, `turns`, `wallMinutes`) |
| `links.design[]`, `links.plans[]`, `links.workflow` | — | Any format; part of the hash by content |

Every JSON file carries `schemaVersion` (1). The JSON Schemas for editors are generated from the records and committed
in `schemas/packets/` (`PacketSchemaTests` fails when a record changes and the schema isn't regenerated). Links are
relative paths with `/` inside the packet directory; `..`, a root or a `:` is refused, and so is a symbolic link anywhere between the packet directory and the file (a linked file or a linked sub-directory). Every JSON file is UTF-8 without a byte order mark, with no duplicate keys, no unknown members (an optional `$schema` editor hint is allowed at the top of each file) and no null in a list; a packet holds at most 2,000 tasks (`PacketReader.MaxTasks`). Anything else is AW-PACKETS-003.

Write-scope globs are repo-relative: `*` and `?` stay inside one path segment, `**` crosses segments (`src/**`,
`**/guide.md`; `**` only as a whole segment, inside one it acts as `*`); entries are at most 1,024 characters, and matching is a two-pointer walk with no regex (bounded by the entry length, no backtracking blow-up); each distinct entry is matched against the base commit's files once. `new: src/c.cs` declares a file the task creates.

## Checks (`aw-packets validate <packet-dir> [--repo <dir>] [--json]`)

`--repo` is a directory inside the repository the packet builds (default: the packet directory). The base commit's
file list comes from git.

| Rule | Check |
|---|---|
| `PKT-ID-001` | Task, workstream, question and example ids are unique |
| `PKT-WS-001` | Every task is in a declared workstream |
| `PKT-GRAPH-001` | Every dependency names a task in the packet |
| `PKT-GRAPH-002` | The task graph is acyclic: each strongly connected group is reported once, naming every task in it and one cycle through it (`tasks t1, t2, t3 form a cycle: t1 -> t3 -> t2 -> t1`) |
| `PKT-BASE-001` | `baseSha` is a full commit sha that exists in the repository |
| `PKT-SCOPE-001` | Every write scope is a relative glob that matches a file at the base commit, or `new:` a path that can be created there (not an existing file or directory, not under an existing file, and not in conflict with another declared new path); a glob with a leading `/` or a `.`/`..` segment is named as not relative |
| `PKT-SCOPE-002` | Two tasks no dependency chain orders don't write the same path (a base file both match, or a declared new path) |
| `PKT-ACC-001` | Every task has a visible acceptance example, and every visible one has text |
| `PKT-EVID-001` | Every acceptance example names at least one kebab-case evidence kind (addendum §17.11) |
| `PKT-DEC-001` | Every question is answered, or deferred with an owner and a `buildMayAssume` default |
| `PKT-HOLD-001` | Every task holds at least one acceptance example out (addendum §17.4: a share held out, per task); a task with no examples fails |
| `PKT-HOLD-002` | No held-out example keeps its text in the packet: held-out text lives in sindri-private state, mounted only into evaluator Steps and never committed (addendum §17.19); the packet keeps its id and evidence kinds |
| `PKT-SIZE-001` | **Unchecked**: the size-class budgets aren't defined (open question below) |
| `PKT-FLOW-001` | **Unchecked**: workflow coverage (addendum §17.7) needs the lens catalog and workflow generation |

The text output lists every rule (`pass`, `FAIL` with the subject, what was found and the fix, or `unchecked` with the
reason). A rule reports at most 50 failures, then one `omitted` record whose `omitted` field counts the rest (the summary line's failure count includes them). `--json` writes one document, `schemaVersion` 1, with `packet`, `passed` and `checks[]` (one record per
failure, one per passing or unchecked rule; each failure has `rule`, `subject`, `message` and `fix`).

## Freeze (`aw-packets freeze <packet-dir> [--repo <dir>] [--json]`)

Runs the same checks. A packet with a failed check isn't frozen and nothing is written. Otherwise its canonical hash
is recorded at `packets/<owner>/<name>/<id>/frozen/<hex>.json` (per repository: a packet id is unique only within one) (`FreezeRecord`: `schemaVersion`, `repo`, packet, hash,
`digestVersion`, `kind` (`build` unless the record says otherwise), `baseSha` (left out for a kind that has none), `frozenAt`, the files the hash covers). Freezing the same hash again writes nothing and says `already frozen`. The
packet directory itself is never changed; the record under `packets/` is authoritative (the ledger of the C# Sindri
application takes over once it exists, spec §4c; Sindri is built new, not ported, factory addendum §17.29).

**The hash (`digestVersion` 1, spec §4c Digests):** SHA-256 over the canonical JSON of
`{"digestVersion":1,"files":{<path>:<entry>}}`. A JSON packet file's entry is the file itself without its `$schema` editor hint (and `packet.json` without
`status` and `hash`); any other linked file's entry is `sha256:<hex of its bytes>`. Canonical JSON sorts object keys by
ordinal, drops whitespace, keeps numbers as written, and writes strings as System.Text.Json's relaxed encoder does (the Kernel profile): `"` and `\` escaped, control characters in their short or `\u00XX` form, U+2028/U+2029 and characters outside the Basic Multilingual Plane as `\u` escapes (surrogate pairs), everything else as UTF-8. `CanonicalJsonTests` pins examples and one packet's hash; changing these bytes needs a new `digestVersion`.

## Packet kinds (`--kind build|scoping`)

`PacketKinds` (`Aw.Packets.Core.Kinds`) is the registry: a kind is a name and the fixed files its schema lists. Every
command takes `--kind` (anywhere in its arguments; `build`, the packet above, when it isn't given; an unknown kind is a
usage error, exit 2). The `build` kind has no fixed list (`packet.json` links its files); the `scoping` kind (scoping
station spec §8.3, P1) lists twelve.

| File | Format | Checked here |
|---|---|---|
| `scope-map.json`, `acceptance.json`, `coverage.json`, `size.json`, `questions.json`, `challenge.json`, `runs.json`, `sources.json` | one JSON object | well formed (`PKT-SCP-003`) |
| `lenses.json` | one JSON object, `LensesFile` (`schemas/packets/lenses.schema.json`) | well formed, and the schema (`PKT-SCP-004`, `-005`) |
| `answers.jsonl`, `rounds.jsonl` | one JSON object per line (blank lines allowed) | well formed |
| `brief.md` | UTF-8 text | well formed |

**Rules (schema only).** What the files mean (citations, coverage, trust, and every check across files) stays in
Sindri's `ScopePacketChecks`, which `aw-packets` (tier 1) must not reference (tier 3); Sindri calls `aw-packets` through
its `cli` edge. That includes `SCP-LENS-001`, the check that `lenses.json` agrees with `coverage.json`: it reads two
files, so it lives in Sindri, not here. The ids are `PKT-SCP-*`, never `PKT-SCOPE-001/002` (the build kind's write-scope
rules).

| Rule | Holds when |
|---|---|
| `PKT-SCP-001` | all twelve files are present |
| `PKT-SCP-002` | the directory holds no other entry (a sub-directory counts, and is named with a trailing `/`) |
| `PKT-SCP-003` | each JSON file is one strict JSON object (UTF-8, no byte order mark, no duplicate key, no null in a list), each JSON-lines line is one object, `brief.md` is valid UTF-8 |
| `PKT-SCP-004` | `lenses.json` has `schemaVersion` 1 and the four lenses (`code`, `product`, `related`, `history`); each has `anchors`, `depth` (0 to 32) and `hops` (the hop kinds walked, each of `state`, `flow`, `dependency`, `component` at most once, in any order, empty at depth 0), `mapVersion` (a non-empty string, as the context engine writes it, for example `v12@a41b07d`), `foundCount` (at least the nodes and items listed, more when a band is `truncated`), `nodes[]`, `edges[]`, `items[]` and `truncated[]` (bands `code`, `product`, `intent`, each once); ids match `^L-(code\|product\|related\|history)-[1-9][0-9]*$`; `band` and `trust` (`untrusted`, `owner`) are from their enums; no unknown field |
| `PKT-SCP-005` | the lens item ids are unique across the file, and each names the lens it sits in |

`PKT-SCP-004` and `-005` are `unchecked` when `lenses.json` is missing or not well formed.

**`validate --kind scoping <packet-dir> [--json]`** runs those five rules; the output is the build kind's (`kind` is
`scoping`, and there is no `packet`). Exit 0 passed, 1 a check failed, 2 the directory is missing. It takes no `--repo`
and writes nothing.

**`hash <packet-dir> [--kind build|scoping] [--json]`** prints the canonical hash and runs no check; with `--json` it
writes `{schemaVersion, kind, hash, digestVersion, files}` (`files` sorted). It is the one canonicalization:
Sindri's `evidence` and `freeze` Steps call it, and `freeze` records the same value. A scope packet with a file
missing, or a JSON file with a duplicate key, has no hash (`AW-PACKETS-003`, exit 1). Entries the kind doesn't list are
not hashed (`validate` fails them).

**`freeze --kind scoping <packet-dir> --repo-slug <owner/name> --instance <slug> [--json]`** validates, then records the
hash at `packets/<owner>/<name>/<instance>/frozen/<hex>.json` (`instance` is a `PacketId`; the record's `kind` is
`scoping` and it has no `baseSha`). A failed check freezes nothing (exit 1); the same hash again says `already frozen`.

**The scoping hash** is the build kind's canonical form over the twelve files, with `"kind":"scoping"` added to the hashed
document so no build packet can collide with it: a JSON file's entry is the file without its `$schema` hint, a
JSON-lines or text file's entry is `sha256:<hex of its bytes>`. `ScopingDigestTests` pins one packet's hash, computed
independently of the C# code.

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
| `IScopingPacketSource` | `FileSystemScopingPacketSource` (the twelve listed files, a symbolic link refused; the other top-level names, never their content) | `InMemoryScopingPacketSource`, `ScopingPacketSourceContract` |
| `IRepoSnapshot` | `GitRepoSnapshot` (`git ls-tree -r -z`, 30 s limit per run) | `InMemoryRepoSnapshot`, `RepoSnapshotContract` |
| `IFreezeLog` | `FileFreezeLog` (temp file, then move) | `InMemoryFreezeLog`, `FreezeLogContract` |
| `TimeProvider` (BCL) | `TimeProvider.System` | `FakeTimeProvider` |

Every `IPacketSource` adapter reads through `PacketReader` (in `Aw.Packets.Core`), so parsing, link safety and the
errors are the same whatever supplies the bytes; `PacketSourceContract` checks them on each adapter.

## Open questions

- **Size classes (`PKT-SIZE-001`):** addendum §17.5 sets a session's budget "by size class and lens", and sindri's
  profile already has a per-size `sizeBudget`. Do the packet-time budgets live in the profile next to it, which lens
  does a packet-time check use, and is a task over its budget a failure or a split signal?
- **Holdout store (`PKT-HOLD-002` checks the packet side today):** addendum §17.19 puts held-out examples in
  sindri-private state with a canary per set. What is that store's path and format, and how does a packet name a set
  (by example id, as now)? The store side and the canary are checked once the store exists.
- **Workflow coverage (`PKT-FLOW-001`)** is not an open question: §17.4's Workflow part is the frozen output of
  workflow generation (§17.7), which the rollout puts in F3; the rule stays `unchecked` until then.
- **`packet.yaml` vs `packet.json`:** the addendum names `packet.yaml`; this build reads `packet.json` (typed,
  source-generated JSON under NativeAOT with no YAML library). Keep JSON, or add a YAML reader?
