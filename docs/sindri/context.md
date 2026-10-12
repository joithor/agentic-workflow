# Sindri context retrieval: using it

The context engine answers questions about the product before and during work, with cited, versioned and scrubbed
results (spec `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`). This page is the user's guide;
the C# package is described in `docs/csharp/sindri-context.md`. Each stage adds its own section.

## S-1A: repo docs, `context query`, `context sources`

### Running the C# Sindri before the switch

Until the switch, `~/.local/bin/sindri` is the TypeScript CLI. Every example here uses `$SINDRI`, the C# build:

```bash
SINDRI="dotnet run --project src/Sindri/Aw.Sindri.Host --"      # from the toolkit repo; or the published binary:
SINDRI=artifacts/aot-sindri-smoke/bin/sindri                     # after bash src/Sindri/aot-smoke/run.sh
```

Nothing in `setup.sh`, the providers or MCP registration points at it.

### Profile

The engine reads the approved profile's `context:` section and its `repos:`. Every key is optional; the defaults are
shown. A key this build doesn't know is refused (`SND-CONTEXT-001` with its key path), so a section written for a later
stage (`labeling`, `sources`, `scrub`, …) needs that stage's build.

```yaml
repos:
  - { name: work-app, path: /Users/me/repos/work-app }
context:
  repos: [work-app]                                    # the profile repos the engine maps; default: all of them
  docs: { globs: ["docs/**/*.md", "**/PRD*.md", "**/TDD*.md"] }   # repo-relative; letters, digits and ._-/* only
  deadline: 4s                                         # per query, 100ms to 60s
  maxBudget: 32000                                     # the largest token budget a request may ask for
  minHits: 3
  sessions: { enabled: false }                         # whether agent sessions may read context at all (any transport)
services:
  context-docs-refresh: { every: 5m }                  # indexes the docs at each repo's HEAD; first run at serve start
```

`sindri profile validate` and `sindri profile approve` check the section; an invalid one is never approved. With a `context:` section, every repo in its scope (all profile repos, or those `context.repos` names) needs a
name of letters, digits, `.`, `_` or `-` (at most 100, starting with a letter or digit): rename the repo under
`repos:`, or leave it out of `context.repos`. A profile
without `context:` keeps its hash.

### Keeping repo docs fresh

The repo-docs index holds the documents the globs match at each repo's HEAD, chunked by heading and **scrubbed before
they are stored**: each document is cleaned whole (hidden comments, images, remote link definitions and link targets removed; secrets become `[REDACTED:<rule>]`) and only then chunked, so nothing that spans a chunk boundary slips through. A document the scrubber blocks keeps its row without text; one over 1 MiB, one git can't read on a run, or one whose path holds a control or invisible character (or is near 1 KB long) is counted as skipped. A path like that is never indexed, so its skip shows on every refresh until the file is renamed. The
`context-docs-refresh` service brings it up to date, at serve start and on its cadence, and records
`context.sources.synced` in the ledger. Name it under `services:`, or nothing is indexed.

`sindri context sources sync` is **pending:S-5B**: it is a job, which needs API P6's job runtime, and stage S-5B adds
its route once P6 merges. Until then it answers `SND-CLI-001` (exit 2) and the service above is the way to refresh.

### Asking

```bash
$SINDRI context query "split pane orientation"            # text
$SINDRI context query "split pane orientation" --json     # the API's JSON, unchanged
$SINDRI context query "…" --budget 4000                   # tokens; default 8000, capped at context.maxBudget (32000)
$SINDRI context sources status [--json]                   # each source's state; whether sessions may read
```

HTTP: `GET /sindri/context/query?q=…&budget=…` and `GET /sindri/context/sources`, behind the loopback guard (a
foreign `Host` is 400, a foreign browser `Origin` 403) and the API's caller rules. `--node`, `--item` and `--symbol`
(graph anchors) answer `SND-CONTEXT-003` until the product map exists (S-3).

A text answer:

```text
context q_01a130fa0e7a… · anchors: none · map: none · as of 2026-10-12T16:37:36.5065540+00:00

## records
[C1] Architecture
    trust untrusted · rrf:repo-docs#1
    <untrusted source="repo-docs">## State

    `PaneLayout` (`src/layout/PaneLayout.ts`) is the single source of truth for how a tab's panes are arranged: …
    </untrusted>

budget: 202 of 8000 tokens (estimate)

sources:
  [C1] repo:split-panes/docs/architecture.md  repo-docs split-panes/docs/architecture.md#L5-L10  version 2c5fdb91f990  current
```

- Every item cites a record (`[Cn]`), and every citation has a version: the blob sha for repo docs. A citation is
  `current`, `stale` (the doc changed since the last refresh: both versions are shown), `unchecked` (the version check
  missed the deadline) or `scrub-blocked`.
- Text from a record is **untrusted**: its body and its title (at most 200 characters) stay inside the
  `<untrusted …>` fence, whose attributes and body are escaped so no text can close it.
- A source that doesn't answer is listed as `DEGRADED <source>: SND-CONTEXT-010 …` and the query goes on without it;
  all of them down is `SND-CONTEXT-011`.
- States are words; there is never colour, so `NO_COLOR` holds.

### Who may read

A human (CLI at a terminal, or the app) reads every mapped repo. An agent session, on any transport (the CLI inside a
session, plain HTTP, later MCP), is refused with `SND-CONTEXT-020` while `context.sessions.enabled` is false; with it
on, a session sees only the repo it names in `X-Sindri-Repo` (the CLI sends the repo of its working directory), and
anything else answers `SND-CONTEXT-003`, the same as an unknown anchor.

### Exit codes

`0` results (possibly degraded or empty) · `1` attention: not running (`SND-API-001`), an unknown or out-of-scope
anchor (`003`), a refused question (`006`), session reads off (`020`) · `2` a tool error: invalid profile section
(`001`), the store (`004`), every source down (`011`), a usage error or a pending verb (`SND-CLI-001`, `SND-CLI-003`).

### Where state lives

`$AW_SINDRI_HOME/context/context.db` (default `$AW_STATE_DIR/sindri-app/context/`), owner-only (directory 0700, the
file and its `-wal` and `-shm` 0600). Never delete it: later stages keep review decisions there.

## S-3a: the product map: `context map import`, `export`, `status`

The product map is a graph of what the product is: features, the surfaces people meet them on, the actions there, the
things those actions read and write, flows, decisions, and pointers to records and code (spec §6.1). Before the
extractor (S-4) builds it, you write it by hand and import it. S-3b adds the questions it answers (`node`, `affects`).

### The map file

YAML or JSON, kept **outside every repo** (a file inside a git worktree is refused with `SND-CONTEXT-005`):

```yaml
schemaVersion: 1
kind: authored            # 'export' for a file 'context map export' wrote
repo: aw-split-panes      # the profile's repo name the map describes
nodes:
  - key: feature:layout.split-pane          # the kind is the key's scheme
    label: Split pane                       # provenance defaults to human in an authored map
  - key: surface:command/split-vertical     # surface:<screen|pane|dialog|command|keybinding|setting|menu|route|api|job>/<name>
    label: Split vertically (command)
  - key: code:src/layout/SplitPane.ts
    label: SplitPane.ts
    provenance: deterministic
edges:
  - {from: "surface:command/split-vertical", to: "feature:layout.split-pane", type: part-of}
  - from: code:src/layout/SplitPane.ts
    to: feature:layout.split-pane
    type: implements
    evidence:
      - key: repo:aw-split-panes/src/layout/SplitPane.ts   # file evidence in the repo: what a session's scope follows
aliases:
  - {alias: "feature:layout.splits", target: "feature:layout.split-pane"}
decisions:                                  # each implies a decision node and its 'quotes' edge to the record
  - record: linear:issue/0e5a1b00-0000-4000-8000-000000000103
    quote: toggling orientation keeps the focused pane first, so the cursor never jumps
    wording: Toggling orientation keeps focus on the first pane.
overrides:                                  # human decisions that outlive rebuilds and imports
  - {action: rename, target: "feature:layout.split-pane", label: Splits, who: avery, at: "2026-07-30T09:05:00Z"}
```

- **Kinds:** `feature`, `surface`, `action`, `thing`, `flow`, `decision`, `record`, `code`. **Edges** (from → to):
  `part-of` (feature or surface → feature), `on` (action → surface), `moves-to` (surface → surface), `reads`/`writes`
  (surface or action → thing), `depends-on` (feature → feature), `step-of` (surface or action → flow, with `order`),
  `implements`/`tests` (code → feature, surface or action), `decides` (decision → any), `mentions` (record → any),
  `quotes` (decision → record), `supersedes` (decision → decision).
- Every node and edge may carry `provenance` (`deterministic`, `model`, `human`), `confidence` (0 to 1), `evidence`
  (record keys, with an optional `span`) and `at`. **Trust is never in the file**: it is recomputed from provenance.
- Override actions: `accept`, `reject`, `rename` (`label`), `merge` and `move` (`other`), `add` (`label`, `other` =
  parent feature), `delete`. An override whose node no longer exists is kept as **orphaned**, listed by export and
  status, and applies again if the node comes back.
- The file is all or nothing: `SND-CONTEXT-031` names each shape problem by key path (`edges[3].type: 'reads' doesn't
  go from a feature to a thing`), `SND-CONTEXT-032` each reference to a node the file doesn't define. Nothing is written.

### Importing (a protected command)

```bash
$SINDRI context map import ~/maps/product-map.yaml
# map file /Users/you/maps/product-map.yaml
# repo aw-split-panes, authored: 19 nodes, 19 edges, 1 aliases, 1 decisions, 2 overrides
# hash 3f0c…
# Type the first 6 characters of 3f0c… to import it: 3f0c9a
# Imported map v1 (current) for aw-split-panes: 19 nodes, 19 edges, 1 aliases, 1 decisions, 2 overrides
```

Import is shaped like `profile approve`: only you, at an interactive terminal, can do it (a session, a script or the
app gets `SND-AUTH-002`), and it is bound to the file's content hash, so a file changed after you saw its hash is
refused with `SND-CONTEXT-034`. The new version becomes `current`, the previous one `retained`, and the ledger records
`context.map.imported`. The API lane's shared challenge isn't on this base yet; the import adopts it when that lane
ships it. HTTP: `POST /sindri/context/map/import` with `{"path", "hash", "confirmed"}`; without a hash it only previews.

### Status and export

```bash
$SINDRI context map status [--json]
# map v2 current · aw-split-panes · created 2026-10-12T17:02:11.0000000+00:00
# 19 nodes, 19 edges, 1 aliases, 1 decisions, 2 overrides; 0 orphaned overrides; 1 retained versions
$SINDRI context map export [<name-or-path>] [--json]
# Exported map v2 to $AW_SINDRI_HOME/context/exports/map-v2-20261012T170400Z.json: 19 nodes, …
```

- An export is JSON (`kind: export`) with nodes, edges with their evidence, aliases, decisions and overrides, each with
  its original timestamp and provenance; import it back to restore the map. It is written only under
  `$AW_SINDRI_HOME/context/exports/` (0700; files 0600, by temp file and rename): a name or relative path lands there,
  anything outside is `SND-CONTEXT-033`, a worktree path `SND-CONTEXT-005`.
- HTTP: `GET /sindri/context/map` and `GET /sindri/context/map/export?path=…`.
- **Sessions** (with `context.sessions.enabled: true`) see only the nodes their repo implements: the targets of
  `implements` edges whose file evidence is `repo:<their X-Sindri-Repo>/…`, and the edges between those nodes. A repo
  that implements nothing in the map answers `SND-CONTEXT-003`.
- No map yet: `SND-CONTEXT-030` (fix: `sindri context map import <file>`).

### Doctor and the purge

`$SINDRI doctor` adds `context:map` (warns with no map or with orphaned overrides; how far the map is behind HEAD is
unknown until a build records its commit, S-4) and `context:retention` (warns while exports older than 14 days or
retained versions past the newest 20 are still there). The `context-purge` service removes both: name it under
`services:` in the profile.

### Exit codes (map verbs)

`0` done · `1` attention: not running (`SND-API-001`), refused (`SND-AUTH-002`), out of scope (`003`), a worktree
path (`005`), session reads off (`020`), no map (`030`), an invalid file (`031`, `032`), outside the exports folder
(`033`), the file changed (`034`) · `2` a tool error: the store (`004`), a usage error.
