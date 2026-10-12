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

## S-5A: the onyx source, secret pointers, doctor `context:onyx`

Onyx CE (pinned **v4.9.0**) indexes the work product's Linear records, Slack and docs; Sindri searches it as the
`onyx` source beside repo-docs. Setting Onyx itself up (the pinned image, ports bound to `127.0.0.1`, local
embeddings, telemetry off, connectors, the service account and its key) is `docs/sindri/onyx.md`.

### Profile

```yaml
context:
  sources:
    onyx: { url: "http://127.0.0.1:3000", token: "keychain:onyx/sindri-read", version: "v4.9.0" }
    linear: { teams: [ENG, APP] }      # the Linear team keys an Onyx identifier alias (ENG-482) may name
```

- `url`: http or https on a loopback host (`127.0.0.0/8`, `[::1]`, `localhost`), no user, password, query or
  fragment. Anything else is `SND-CONTEXT-055`.
- `version`: the release tag that runs (`v4.9.0`), never `latest`; missing or `latest` is `SND-CONTEXT-055`.
- `token`: a **secret pointer** to the Onyx admin key of a service account Sindri alone uses, never the key itself.
  Missing, or text that isn't a pointer, is `SND-CONTEXT-002`; the message doesn't repeat the text (rotate a key that
  was written into the profile).
- `linear.teams`: team keys (a capital letter, then up to 9 capitals or digits). Linear's own `token` and `url` arrive
  with the native Linear adapter (S-5B); this build refuses them (`SND-CONTEXT-001`), like any unknown key under
  `sources`, `onyx` or `linear`.

`sindri profile validate` reports each of these with its key path (`context.sources.onyx.url: …`).

### Secret pointers

| Pointer | Resolves to |
|---|---|
| `env:NAME` | the environment variable `NAME` of the `sindri` process (fixtures and tests) |
| `file:/absolute/path` | the file's contents without a trailing newline; the file must be a regular file (not a symbolic link) with mode `0600` or stricter |
| `keychain:service/account` | `security find-generic-password -s service -a account -w` (store it with `security add-generic-password -s onyx -a sindri-read -w`) |
| `op:vault/item/field` | `op read op://vault/item/field` (1Password CLI) |

Sindri resolves the pointer in its host (`Composition`), on each Onyx call; the context engine never reads the
keychain or a file. `sindri profile validate` checks only the pointer's form, never resolves it (resolving can prompt
the keychain or run `op`), so `env:NO_SUCH_VAR` passes validate. At run time a pointer that doesn't resolve shows as
`SND-CONTEXT-002` in the onyx row and a query's `degraded` entry, naming the pointer's scheme only; `sindri doctor`
resolves it first and shows the reason: `SND-SECRET-001` (malformed), `002` (doesn't resolve: names the variable, file
or entry) or `003` (an unsafe `file:`: names the file and the `chmod 600` fix), never a value
(`docs/csharp/sindri.md`).

### Asking, status and degraded answers

`sindri context query` fuses Onyx's hits with repo-docs (reciprocal rank fusion). Onyx's Linear issues key on Linear's UUID
(`linear:issue/<uuid>`, with its identifier alias, `ENG-482`, when its team is listed). The aliases are in the
citation: `cites.<n>.aliases` in `--json` and HTTP (every alias of the record, merged across its refs, each scrubbed
like a title), and the first one beside the key in the text form (`linear:issue/<uuid> (ENG-482)`). Onyx v4.9.0 has no route that returns a
document's `updated_at` by id, so **onyx citations stay `unchecked`** (version unchecked) instead of `current` or
`stale`. A citation's `permalink` is scrubbed like every other field: a link the scrub changes at all (redacted or
blocked) is left out, and the citation keeps its key and aliases.

When Onyx doesn't answer, refuses the key, answers with an error status or replies in a shape the pinned release
doesn't have, the answer still returns with repo-docs' results and a `degraded` entry `{ "source": "onyx", "code":
"SND-CONTEXT-010", … }` (the cause leads its message, such as `SND-CONTEXT-051: …`). Only when every source is down is the
answer `SND-CONTEXT-011`. A query doesn't check Onyx's version: version drift is doctor's and `sources status`'s check
(`055`, naming both versions), and a query keeps using an Onyx that reports another version as long as its replies
read; a reply that doesn't degrades onyx (`010`, cause `051`).

Agent sessions don't read the onyx source yet: a session sees intent records only from the Linear teams the profile
maps to its repo (`context.repoTeams`, a later stage), so until then onyx answers humans and services only. A caller
whose scope maps no repo gets no source at all, onyx included.

`sindri context sources status` shows an `onyx` row: `ok`, `degraded` (the key is an admin key of a person's account,
`SND-CONTEXT-053`) or `unavailable` (no answer, `SND-CONTEXT-010`; another version, `055`; key refused, `050`; pointer
unresolved, `002`), with the version and the key's role and account type in its detail.

### Doctor `context:onyx`

`sindri doctor` runs the onyx source's probe for the approved profile: `GET /api/version` (no key) against the pinned
version, then `GET /api/me` with the key for its role and account type.

| Result | When | Fix |
|---|---|---|
| `ok` | Onyx answers the pinned version and the key is an admin key of a service account; or the profile names no onyx (`not configured`) | — |
| `warn` | the key is an admin key of a person's account (`053`); or onyx is named but no repo is mapped, so no source runs | create a service-account key; map a repo |
| `fail` | the key's pointer doesn't resolve (`SND-SECRET-001`..`003`, checked before Onyx is called), Onyx is down (`010`), another version (`055`, naming both), the key lacks admin (`050`), the section is invalid | per the line; see `docs/sindri/onyx.md` |

Onyx's provider and telemetry settings can't be read with the key; `docs/sindri/onyx.md` lists them as a manual check
on each Onyx version change.

### Trying it in a throwaway profile

```bash
export AW_SINDRI_HOME="$(mktemp -d)"            # throwaway state
export SINDRI_ONYX_KEY='on_…'                   # the service account's admin key (or use keychain:onyx/sindri-read)
# add to $AW_SINDRI_HOME/profile/profile.yaml:
#   repos: [{ name: work-app, path: /path/to/work-app }]
#   context:
#     sources:
#       onyx: { url: "http://127.0.0.1:3000", token: "env:SINDRI_ONYX_KEY", version: "v4.9.0" }
#       linear: { teams: [ENG] }
sindri profile validate && sindri profile approve
sindri doctor                                   # context:onyx
sindri context sources status
sindri context query "split pane orientation"
```
