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

`sindri profile validate` and `sindri profile approve` check the section; an invalid one is never approved. A profile
without `context:` keeps its hash.

### Keeping repo docs fresh

The repo-docs index holds the documents the globs match at each repo's HEAD, chunked by heading and **scrubbed before
they are stored**: each document is cleaned whole (hidden comments, remote images and link targets removed; secrets become `[REDACTED:<rule>]`) and only then chunked, so nothing that spans a chunk boundary slips through. A document the scrubber blocks keeps its row without text; one over 1 MiB, or one git can't read on a run, is counted as skipped. The
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
