# split-panes fixture (stage S-FX)

The fixture app of the Sindri context retrieval spec (`docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`
§10): a small terminal-style app, its git history of merged pull requests, a recorded Linear project, recorded Onyx
documents for the same records, and probe questions. Every adapter test, the cascade fixtures, the eval harness and
the hand runs of later stages use it instead of work data.

**Everything here is synthetic.** The team key `SPL`, the workspace `fixture-labs`, the people (Avery Fixture, Rowan
Sample, addresses under `.invalid`), the UUIDs (all `0e5a1b00-0000-4000-8000-…`), the Slack channel `C0FIXTURE01` and
every URL are invented. `FixtureDataTests.Everything_is_synthetic_ids_emails_and_hosts` checks it. Nothing was
captured from a real workspace; don't add anything that was.

The files are embedded in `Aw.Context.Testing` (resource paths are relative to this folder), so a test in any project
reads them through `SplitPanesFixture` without file paths.

## The app (`app/`)

A TypeScript-looking app (it is never compiled or run). One window, tabs, panes inside a tab:

| Path | What it is | Graph node it is meant to map to (§6.1) |
|---|---|---|
| `src/commands/registry.ts` | The command registry: every action is a named command | `surface:command/<id>` per command |
| `src/keybindings/defaults.ts` | Default keys, which only name commands | `surface:keybinding/<key>` |
| `settings/default-settings.json`, `src/settings/schema.ts` | The settings file and its schema (`paneResizeStep`) | `surface:setting/paneResizeStep` |
| `src/layout/PaneLayout.ts` | `PaneLayout`, the state both features share; `snapshot` / `restore` | `thing:pane-layout` |
| `src/layout/SplitPane.ts` | Split Pane: `split`, `setOrientation` (writes `PaneLayout`), `toggleOrientation` | `feature:layout.split-pane`, `feature:layout.split-pane.orientation` |
| `src/layout/PaneResize.ts` | Pane Resize, the sibling: `resizeFocused`, `MIN_PANE_COLUMNS = 10` | `feature:layout.pane-resize` |
| `src/tabs/TabManager.ts` | Tab Manager: `switchTo` / `restoreLayout` read `PaneLayout` | `feature:tabs.tab-manager`, `action:tab-manager.restore-layout` |
| `docs/architecture.md`, `docs/decisions/0001-orientation-toggle.md` | Repo docs for the `repo-docs` adapter | — |
| `tests/**` | Tests per feature (`tests` edges) | — |

The cascade example of §6.1 lives here: Orientation writes `thing:pane-layout`, the tab manager's layout-restore
action reads it, so the tab manager is affected at depth 1; Pane Resize is the sibling that must not be reached.

Each folder `app/NN-…` holds the **full contents** of the files that step adds or changes; the builder copies a step's
files over the working tree, so the files a step lists are exactly the files its merge changes.

## History (`history.json`) and the builder

`FixtureRepo.Build()` makes the app a git repo in a temp directory: the initial commit, then per pull request one
commit `SPL-1NN: <title>` on branch `spl-1NN-…` and a `--no-ff` merge `Merge pull request #N from fixture-labs/<branch>`.
Author, committer and dates come from `history.json`, and git runs with no user, system or hook configuration and none
of the caller's `GIT_*` variables, so the shas are the same on every machine (`FixtureRepoTests`).
`FixtureRepo.PullRequests` lists, per merge, the issue key, branch, merge sha, merge date and the files it changed: the
backtest's file truth (§9.1).

| PR | Issue | Merged | Changes |
|---|---|---|---|
| #1 | SPL-101 Command registry | 2026-07-06 | registry, keybindings, app, registry test |
| #2 | SPL-102 Split a pane | 2026-07-10 | SplitPane, PaneLayout, registry, keybindings, app, test |
| #3 | SPL-103 Toggle split orientation | 2026-07-15 | SplitPane, registry, keybindings, decision doc 0001, test |
| #4 | SPL-104 Restore layout on tab switch | 2026-07-22 | TabManager, PaneLayout (snapshot/restore), registry, test |
| #5 | SPL-105 Resize panes with the keyboard | 2026-07-29 | PaneResize, settings + schema, registry, keybindings, test |

SPL-106 has no merged pull request: it is the issue a corpus builder must leave out.

## Linear (`linear/`)

Recorded GraphQL responses, one per root field, each a full envelope
`{"data": {"<field>": {"nodes": [...], "pageInfo": {...}}}}` in Linear's schema shapes:

| File | Records | For |
|---|---|---|
| `projects.json` | Project "Split panes" (`…0010`), team SPL | project structure (D4) |
| `documents.json` | "PRD: Split panes" (`…0020`), a Linear Document with the goals and non-goals | the PRD only the native adapter reads (D4); key `linear:doc/<uuid>` |
| `project-updates.json` | Two updates (`…0031` on track, `…0032` at risk, naming the width conflict) | project updates (D18) |
| `issues.json` | SPL-101…106 (`…0101`…`…0106`) with comments, state, team, project and GitHub PR attachments | issues with comments and issue↔PR links (D18); keys `linear:issue/<uuid>`, alias `SPL-1NN` (D16) |

The decisions live in issue comments:

| Decision | Where | Status |
|---|---|---|
| A: toggling orientation keeps the focused pane first | SPL-103, comment `…1031` (2026-07-08) | current; matches the code and doc 0001 |
| B0: the minimum pane width is 10 columns | SPL-105, comment `…1051` (2026-07-24) | **superseded** by B |
| B: the minimum pane width is 20 columns | SPL-105, comment `…1053` (2026-08-12) | current; **conflicts with the code** (`MIN_PANE_COLUMNS = 10` in `PaneResize.ts`) |

B is dated after PR #5 merged, so SPL-105 is also the `leaky` case for an as-of cut at the merge (§9.1).

## Onyx (`onyx/documents.json`)

The same six issues as Onyx's Linear connector would index them: `document_id` is the issue UUID, the identifier is
in `metadata.identifier` (structured metadata, never parsed from text; §5.1), `updated_at` equals the issue's
`updatedAt`, `source_type` is `linear`, and the chunks are the description then each comment. One Slack thread
(`C0FIXTURE01__1721900000.000100`, canonical key `slack:C0FIXTURE01/1721900000.000100`) adds a second source type.
Each entry is `{"search_doc": <SearchDoc as Onyx returns it>, "chunks": [...]}`. Onyx has no PRD: its Linear connector
doesn't read Linear Documents (D4).

## Probes (`probes.json`)

Eight questions (§9.2 item 3), each with the anchors a correct answer holds: `nodes` (graph keys, §6.1 scheme),
`absentNodes` (what it must not name, e.g. the sibling), `records` (canonical keys), `files` (repo paths) and
`decisions` (verbatim quote, `current` or `superseded`, and the file it conflicts with). The node keys are the intended
map, not one a stage has built yet; the stage that builds the map (S-3, S-4) checks its keys against them.

## The fakes

Both run on `127.0.0.1` and an ephemeral port, start in a few milliseconds and stop on `DisposeAsync`. Neither
records or logs a header: `Requests` holds method, path, operation and status; `Log` holds the request lines and every
line the web host itself logged (at trace level, in memory, never printed). The tokens are fixture values passed by
`FakeServerOptions` (spec §14 S-L2: a loopback `linear.url` takes an `env:` or `file:` pointer, never the keychain).

`FakeLinearServer` (`Url` = `…/graphql`, `Token`, default `fixture-linear`; accepts `Authorization: <token>` or
`Bearer <token>`):

- Root fields `projects`, `documents`, `projectUpdates`, `issues`: Linear filter objects (nested fields such as
  `team: { key: { eq: "SPL" } }`, `and`/`or`, `some`/`every` over a connection, `eq neq in nin gt gte lt lte null
  eqIgnoreCase contains containsIgnoreCase startsWith`; dates compare as instants), `first` (1–250, default 50),
  `after` (a node id from `endCursor`), `orderBy` (`createdAt` default, or `updatedAt`; ascending), `pageInfo`.
- `project(id)`, `document(id)`, `issue(id)` (an issue also by identifier); a missing one is a GraphQL error with
  `data.<field> = null`, status 200.
- Whole recorded nodes come back whatever the selection set asks for. Object and list arguments must be variables.
- An unknown root field, argument, mutation, fragment spread at the root, or a date compared with a non-date (`P1W`)
  is a 400 that says what to change. No or a wrong token is a 401.

`FakeOnyxServer` (`Url` = base, `Token`, default `fixture-onyx`, `Authorization` = `Bearer <token>`):

- `POST /api/query/document-search` with Onyx's DocumentSearchRequest fields `message` and
  `retrieval_options.{filters.{source_type,time_cutoff},limit,offset}`; `time_cutoff` keeps documents updated at or
  after it. The reply is `{"top_documents": [SearchDoc + score, chunk_ind, match_highlights], "llm_indices": []}`,
  ranked by a deterministic term match (not Onyx's ranking; Sindri ranks, D3).
- `GET /api/document/chunk-info?document_id=&chunk_id=` returns `{"content", "num_tokens"}`, 404 for an unknown one.
- Any other route is a 404 naming the two it serves; a field it doesn't read (`tags`, `document_set`, …) or a missing
  `message` is a 422 in Onyx's validation shape (`detail: [{loc, msg, type}]`).
- The exact HTTP routes and the change feed by updated time are open items of Phase 5 (§12). S-5A pins them against
  the Onyx release it sets up and, if they differ, changes this fake and these recordings in its own PR.

## What later stages add

- **S-1A**: `ContextSourceContract<TSource>` and the Context Core types; it binds the `repo-docs` adapter to a
  `FixtureRepo` and wraps these fakes. Nothing here references `Aw.Context.Core`.
- **S-5A**: the `onyx` adapter against `FakeOnyxServer`. **S-6**: the native `linear` adapter's `.graphql` files against
  `FakeLinearServer`. **S-3 / S-7A**: the cascade fixtures, the probe runner and the backtest over this app.
