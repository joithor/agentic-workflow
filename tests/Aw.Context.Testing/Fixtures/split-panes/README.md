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
of the caller's `GIT_*` variables, and `tests/Aw.Context.Testing/.gitattributes` keeps checkouts from converting
the fixture's line endings, so the shas are the same on every machine: `FixtureRepoTests` pins the head
(`GoldenHead`); change it only in a reviewed change to the app files, `history.json` or the builder.
`Build(parent)` creates `parent/aw-split-panes` and refuses one that already exists.
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

SPL-103 gets a follow-up comment on 2026-07-23, after SPL-104 merged, so ordering issues by `createdAt` and by
`updatedAt` gives different orders (101, 102, 103, 104 … vs 101, 102, 104, 103 …).

The decisions live in issue comments:

| Decision | Where | Status |
|---|---|---|
| A: toggling orientation keeps the focused pane first | SPL-103, comment `…1031` (2026-07-08) | current; matches the code and doc 0001 |
| B0: the minimum pane width is 10 columns | SPL-105, comment `…1051` (2026-07-24) | **superseded** by B |
| B: the minimum pane width is 20 columns | SPL-105, comment `…1053` (2026-08-12) | current; **conflicts with the code** (`MIN_PANE_COLUMNS = 10` in `PaneResize.ts`) |

B is dated after PR #5 merged, so SPL-105 is also the `leaky` case for an as-of cut at the merge (§9.1).

## Onyx (`onyx/documents.json`)

The same six issues as Onyx's Linear connector (Onyx **v4.9.0**, `backend/onyx/connectors/linear/connector.py`) would
index them: `document_id` is the issue UUID, `semantic_identifier` is `[<identifier>] <title>` (the only place a
search result carries the `SPL-1NN` identifier; the connector puts it nowhere in `metadata`), `link` is the issue URL,
`updated_at` equals the issue's `updatedAt`, `source_type` is `linear`, and the chunks are the description then each
comment. `metadata` holds only keys the v4.9.0 connector writes, as strings (it applies `str()` to each value): here
`team`, `state`, `priority`, `completed_at` (when set) and `created_at`, taken from `linear/issues.json`. There is no
`identifier` and no `project` key, because the real connector sends neither. One Slack thread
(`C0FIXTURE01__1784972400.000100`, canonical key `slack:C0FIXTURE01/1784972400.000100`) adds a second source type.
Each entry is `{"search_doc": <SearchDoc as Onyx returns it>, "chunks": [...]}`. Onyx has no PRD: its Linear connector
doesn't read Linear Documents (D4).

## Probes (`probes.json`)

Eight questions (§9.2 item 3), each with the anchors a correct answer holds: `nodes` (graph keys, §6.1 scheme),
`absentNodes` (what it must not name, e.g. the sibling), `records` (canonical keys), `files` (repo paths) and
`decisions` (verbatim quote, `current` or `superseded`, and the file it conflicts with). The node keys are the intended
map, not one a stage has built yet; the stage that builds the map (S-3, S-4) checks its keys against them. The spec's
§8.4 result example uses the same keys (`feature:tabs.tab-manager`, `action:tab-manager.restore-layout`).

## The fakes

Both run on `127.0.0.1` and an ephemeral port (`Url` is the address Kestrel bound), start in well under a second and
stop on `DisposeAsync`. Neither records or logs a header: `Requests` holds method, path, operation and status; `Log`
holds the request lines and every line the web host itself logged (at trace level, in memory, never printed). The
tokens are fixture values passed by `FakeServerOptions` (spec §14 S-L2: a loopback `linear.url` takes an `env:` or
`file:` pointer, never the keychain). A request the fake can't answer truthfully is refused with a 4xx that says what
to change; an exception it didn't anticipate becomes a recorded JSON 500 naming it, never Kestrel's empty one.

`FakeLinearServer` (`Url` = `…/graphql`, `Token`, default `fixture-linear`; accepts `Authorization: <token>` or
`Bearer <token>`):

- Root fields `projects`, `documents`, `projectUpdates`, `issues`: Linear filter objects (nested fields such as
  `team: { key: { eq: "SPL" } }`, `and`/`or`, `some`/`every` over a connection, `eq neq in nin gt gte lt lte null
  eqIgnoreCase contains containsIgnoreCase startsWith`; dates compare as instants), `first` (1–250, default 50),
  `after` (a node id from `endCursor`), `orderBy` (`createdAt` default, or `updatedAt`; ascending), `pageInfo`;
  `includeArchived` is accepted and changes nothing (no recorded node is archived). A non-string `orderBy` or `after`
  is a 400.
- `project(id)`, `document(id)`, `issue(id)` (an issue also by identifier); a missing one is a GraphQL error with
  `data.<field> = null`, status 200.
- **Replies follow the selection set**: only the selected fields come back, under their aliases. A field the
  recordings don't have, an object field without subfields, or a scalar with subfields is a 400, so a hand-written
  query (D18) that selects the wrong thing fails here. A nested connection (`comments`, `attachments`, `labels`,
  `teams`) takes `first` and answers `pageInfo` (computed: the recordings hold every child; the cursors are the
  children's `id`, null where a recorded child has none, as labels don't). Nested `after` and other nested arguments
  are refused, so read every child by raising `first`. Selection sets nest at most 32 deep.
- One `query` operation with variable definitions; defaults (`$first: Int = 2`) apply when the request leaves a
  variable out, and an undeclared `$variable` is a 400. Object and list arguments must be variables (inline scalars and
  enums are fine; a non-finite number is a 400). Mutations, fragments, directives, block strings and invalid string
  escapes are 400s.
- Filters: an unknown filter field, a field filter straight on a connection (without `some`/`every`), `some`/`every`
  on a scalar, `null` with a non-Boolean, a null or non-scalar operand (`eq: null`, `in: [null]`; test for null with
  `null: true`), a string comparator with a non-string, values of different types, or a date
  compared with a non-date (`P1W`) is a 400, wherever the clause sits: every clause is evaluated before the results
  are combined, so an earlier failing clause never hides a bad one. A null field or relation fails every comparator
  except `neq`, `nin` and `null: true`; the fake doesn't model Linear's joins beyond that.
- No or a wrong token is a 401.

`FakeOnyxServer` (`Url` = base, `Token`, default `on_fixture-onyx`, `Authorization` = `Bearer <token>`). What is
pinned to **Onyx v4.9.0** (`FakeOnyxServer.PinnedVersion`; checked against the release's source, S-5A):

- **Keys.** A token must be a service-account key (`on_…`) or a personal access token (`onyx_pat_…`), the two kinds
  Onyx accepts; `Start` refuses any other with an `ArgumentException`. As in Onyx (`onyx/auth/utils.py`
  `get_hashed_bearer_token_from_request`), `Authorization: Bearer <token>` works for both kinds and the raw
  `Authorization: <token>` only for an `on_…` key. Onyx's alternative header `X-Onyx-Authorization` isn't served.
- **Unauthenticated.** No key, an unknown key or a wrong form is a **403** with
  `{"error_code": "UNAUTHORIZED", "detail": "Access denied. User is not authenticated."}`, which is what Onyx answers
  (`double_check_user` raises `BasicAuthenticationError`, an `HTTPException` 403, and `main.py` `log_http_error`
  writes it with the canonical code for 403). It is not a 401. `GET /api/me` says `"detail": "Unauthorized"` instead
  (`verify_user_logged_in`).
- **The key's account** (`FakeServerOptions.OnyxAccountType`, `OnyxAdmin`): by default the key belongs to a
  `SERVICE_ACCOUNT` holding `admin`. **`GET /api/me`** returns Onyx's `UserInfo` (`onyx/server/manage/users.py`
  `verify_user_logged_in`, `manage/models.py:165`): `account_type` and `effective_permissions` (every permission for an
  admin; `basic` and the CE grants for a basic key, approximately). A key without `admin` gets Onyx's 403
  `{"error_code": "INSUFFICIENT_PERMISSIONS", "detail": "You do not have the required permissions for this action."}`
  on admin search (`require_permission(FULL_ADMIN_PANEL_ACCESS)`); the document routes need only `basic`.
- **`GET /api/version`** returns `{"backend_version": "v4.9.0"}` and needs no key (it is in Onyx's public endpoint
  list, `onyx/server/auth_check.py`; release images set `ONYX_VERSION` from the git tag).
  `FakeServerOptions.OnyxVersion` overrides the reported version so a test can simulate drift.
- **`POST /api/admin/search`** (`onyx/server/query_and_chat/query_backend.py:36-82`): body `{"query": str,
  "filters": BaseFilters}`, both required. Reply `{"documents": [SearchDoc]}`: the recorded SearchDoc plus `chunk_ind`,
  `score`, `match_highlights`, `is_relevant`, `relevance_explanation`, `source_types`, `is_internet`, `file_id`, at
  most **50** (`NUM_RETURNED_HITS`), one per document id; there is no `limit` or `offset`. Of the filters
  (`onyx/context/search/models.py:104-124`) the fake applies `source_type` (DocumentSource values; an empty list is no
  filter), `updated_at_range {start, end}` and its legacy alias `time_cutoff` (an explicit range wins). Pydantic's
  default ignores fields the models don't have, and so does the fake. A body the real models reject (no `query` or
  `filters`, a non-string query, a non-list or unknown `source_type`, a bad datetime) is Onyx's generic 422
  `{"status_code": 422, "message": "Request validation failed.", "data": null, "error_code": "VALIDATION_ERROR",
  "detail": "Request validation failed."}`, which names no field. A real filter the fake doesn't model
  (`document_set`, `tags`, `created_at_range`) with a value is a 422 `FAKE_UNSUPPORTED` naming it. Ranking is a
  deterministic term match (not Onyx's keyword ranking; Sindri ranks, D3); an empty query (Onyx's random retrieval)
  returns the filtered documents in id order.
- **`GET /api/document/document-size-info?document_id=`** returns `{"num_chunks", "num_tokens"}`; an unknown document is
  a 404 `{"error_code": "NOT_FOUND", "detail": "Document not found"}`. **`GET /api/document/chunk-info?document_id=&chunk_id=`**
  returns `{"content", "num_tokens"}`; an unknown document or chunk is a 404 `"Chunk not found"`. A missing or
  non-integer parameter is Onyx's 422 above. Token counts are word counts, a stand-in for Onyx's tokenizer.
- **Any other route** is a 404 `{"error_code": "NOT_FOUND", "detail": …}`, answered before the key check as in Onyx;
  the detail names the routes the fake serves (Onyx's says `Not Found`).
- **`FakeServerOptions.Delay`** holds every answer back, so a client's deadline can be tested.
- **`FakeServerOptions.OnyxPlant`** rewrites the JSON text of every reply before it is sent, to plant what the
  fixture never holds: a document Onyx marks `hidden` (`OnyxSourceContractTests`), a secret-shaped identifier in a
  link (Sindri's `OnyxContextTests`) or another reported version (`FakeOnyxServerTests`, which checks the rewrite
  itself). The plant must return valid JSON (the server parses it). Null, the default, sends the replies as they are.

There is no change feed (§12) and no route that gives a document's `updated_at` by id.

## What later stages add

- **S-1A**: `ContextSourceContract<TSource>` and the Context Core types; it binds the `repo-docs` adapter to a
  `FixtureRepo` and wraps these fakes. Nothing here references `Aw.Context.Core`.
- **S-5A**: the `onyx` adapter against `FakeOnyxServer`. **S-6**: the native `linear` adapter's `.graphql` files against
  `FakeLinearServer`. **S-3 / S-7A**: the cascade fixtures, the probe runner and the backtest over this app.
- **Not here yet, added by the stage that needs it:** records that change between calls (change feeds, cursor replay,
  removals). `FakeServerOptions` is the place to grow them without breaking callers.
