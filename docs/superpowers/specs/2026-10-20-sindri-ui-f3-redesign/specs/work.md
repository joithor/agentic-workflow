# Spec: Work (Avalonia build spec)

Screen `work` (the rail's Work, chord G Q). Inputs: `screens/work.html` (built by `data/tools/pg_work.py`), `screens/work.notes.md`, `data/PLAIN-WORDS.md`, `docs/csharp/sindri-ui.md`, screens spec section 3. Code read at origin/main: `src/Sindri/Aw.Sindri.Ui/{Views/Queue,Views/Item,ViewModels/Queue,ViewModels/Item,ViewModels/Labels.Items.cs,ViewModels/Shell/ShellViewModel.Items.cs}`, endpoints `GetQueueEndpoint`, `ListItemsEndpoint`, `GetItemEndpoint`, `GetItemTimelineEndpoint`, `CreateItemEndpoint`, `CloseItemEndpoint`, goldens `tests/Aw.Sindri.Tests/Golden/Api/{queue,items,items-id,items-id-timeline}/*.json`.

The page and the item sheet already exist (`QueuePage`, `ItemRow`, `CompactItemRow`, `ItemSheet`, `CreateItemDialog`). This spec re-skins them to the F3 look, makes the queue one table, and adds the states the prototype shows. No endpoint or field changes.

## 1. Goal and decisions

One question: what is every open item, which ones wait on me, and what is each one's stage.

| Q | Decision | Effect |
|---|---|---|
| 1 | One table, not four panels: header (Title, Repo, Status, Stage, Age), then a 32 px group row per served group (Waiting on you, With you, Running, Queued) with its count, then 36 px rows | `QueueGroupViewModel` keeps its group order; `ItemRow` becomes the one-line list row (same shape as the Workflows list row) |
| 2 | The id is a small grey mono string beside the title, never the title | `Labels.ItemIdLabel` stays for the sheet |
| 3 | Status is one badge with the existing words: New, Running, Needs decision, Paused: needs a decision, With you, Done, Cancelled | `Labels.ItemState` unchanged; glyph pairs come from the shared `StatusBadge` (Workflows spec slice 1) |
| 4 | Stage shows a plain name (Scoping, Design, Plan, Build and prove, Merge) or "Not started" | needs a plain-name map; raw `step` is a data gap (section 4) |
| 5 | Filters: All and Running (existing `QueueFilter`), a text filter by id, title or repo (`/`), a repo chip | existing `QueueFilter`, `QueueFilterText`; the repo chip is the existing repo choice, restyled |
| 6 | Closed items are a fold under the table ("Closed (3)"), read on demand from `GET /items?state=done` and `cancelled` | no new endpoint; the fold is collapsed by default |
| 7 | Click opens the item sheet; the sheet shows status, repo, body, the run link, the two close actions with Confirm and Keep, the open decision, details and history | existing `ItemSheetViewModel` and its actions; layout only |
| 8 | A waiting item shows the decision question and its options in the sheet, but answering goes through Needs you (one place to answer) | options render as links to Needs you; the existing desk decide path stays on Needs you |
| 9 | New item is a dialog: Title (required), Repo, Body; Create or Cancel; "Title is required" under the field | existing `CreateItemViewModel`; `C` opens it |

**Not built (prototype only):** the rail, the 56 px top bar, the "View as" switch, Annotations. The shell keeps its 48 px bar and nav; Home's Running and Queued panels keep `QueueScope.Home` and are untouched.

## 2. Regions and layout

| Region | Size and tokens | Avalonia |
|---|---|---|
| Head | `page-title` "Work" and a caption "12 open · 4 waiting on you"; right: New item (`Primary` button) | existing head `Grid` |
| Toolbar | 48 px; segmented All and Running with counts, repo chip (28 px, `Surface2`), filter box 360 max, key hints hidden under 1180 px | segmented `ListBox` as in the Workflows spec 2; `QueueFilter` `TextBox` kept |
| Banner | one line under the toolbar: amber (stale), neutral with spinner (rebuilding), red (stalled) | existing `ViewsBanner` |
| Table | `Surface1` card, hairline, header 28 px sticky, groups 32 px, rows 36 px; columns Title flexible with the id beside it, Repo 110, Status 200, Stage 120, Age 56 right tabular | `ItemsControl` of `QueueGroup`; rows are `ItemRow` |
| Narrow band | under 760 px: Title, Status, Age only | `Classes.compact` on the row from `PageWidthBand` |
| Group empty line | 12 px grey line under the group row with the served `empty` sentence | existing `Empty`/`ShowsEmpty` |
| Closed fold | 36 px row, chevron and "Closed (3)"; rows read-only (no close actions) | new small toggle like `AgentsEndedToggle` |
| Footer | info glyph and "Up to date with the ledger (event 214)." or "Updated up to event 8 of 9." from `asOf` | `Footer` caption |
| Sheet | 480 px drawer: header badge, repo, title 16/600, "Needs decision for 5h 51m", copy chip of the id, body, actions row, amber callout for the open decision, caps sections Details and History | existing `ItemSheet`; the shell sheet, not page-local |
| Dialog | 480 px, `Surface2`, `Radius.LG`; Title, Repo, Body (3 lines), Cancel and Create item | existing `CreateItemDialog` |

No new tokens.

## 3. States (all in the prototype, `?state=`)

| State | What shows | Source |
|---|---|---|
| Items | the table | 200 with groups |
| Loading | seven skeleton rows and "Reading the queue…" | first read in flight |
| Nothing in the queue | the box "Nothing in the queue", a New item button and the copy chip `sindri item add` | `empty` set on the whole queue |
| Filter matches nothing | "No items match" and Clear filter | client filter |
| Rebuilding | the neutral banner "Rebuilding views (cursor 8 of 8)…" and each non-queued group shows it as its empty line | `asOf.rebuilding` |
| Views stalled | the red banner "Views are stalled at 9. Run sindri views rebuild." with a copy chip | `asOf.stalled` |
| Out of date | the last table with the amber stale line | `IsStale` |
| Sindri not running | the box "Sindri is not running" and the copy chip `sindri serve` | shell state `NotRunning` |
| Sheet: open, closed, with a decision | as section 2 | `GET /items/{id}` |
| Close confirm | the two buttons become "Close X as done? Confirm / Keep" | existing confirm flow |
| Create dialog: error | "Title is required" under the field, field marked invalid | client validation or `SND-ITEM-*` |

## 4. Data

Served, used as is: queue `groups[]` (`group`, `items[]`, `empty`), `empty`, `asOf{cursor, ledgerHead, projectedAt, rebuilding, stalled}`; item `{id, title, repo, state, step, since, primaryAction, actions[], owner}`; detail adds `{version, createdBy, waitingOn, instance, createdAt, updatedAt, body, link, launch{...}}`; timeline `entries[]` (`sequence`, `time`, `type`, `actor`, `line`). The close action is `CloseItem:done` or `CloseItem:cancelled` with the item `version`.

Gaps (Annotations on):
- **Plain stage name.** `step` is a raw step id (`triaged`, `produce`). The prototype maps the line's five stages; a `stageName` (or the line stage) is needed from the api, like the Workflows spec's `lineStage`.
- **Waiting for.** `waitingOn` is a decision id; the question text is read from the decision (`GET /decisions/{id}`); the prototype shows the question inline.
- **Start a workflow from an item.** The sheet's button for an item with no run uses the Start dialog with the item preselected (existing `StartDialog`); no new endpoint.
- **Launch.** The item read serves `launch` (targets, blockers). The prototype draws no launch control; the launch spec owns it.

## 5. Actions and keys

| Key or click | Does |
|---|---|
| J, K | move the selection (existing queue keys) |
| Enter, click | open the sheet |
| Esc | close the sheet or the dialog |
| `/` | focus the filter |
| `C` | open New item (existing chord) |
| Mark done, Cancel item | the existing close flow: Confirm sends `CloseItem` with the item `version`; Keep leaves it; a stale version is refused and the sheet re-reads |
| Open the run | opens the run canvas at the item's `instance` |
| Decision options | link to Needs you at that item |
| Copy chips | id, `sindri views rebuild`, `sindri serve`, `sindri item add` |

Protected actions stay terminal copy; closing an item is a `desk`-level action the app already sends.

## 6. Copy (`Labels.Items.cs`; no em dashes, no internal terms)

Reuse every existing label. Change: the column headers lose ALL CAPS (Title, Repo, Status, Stage, Age); group names are the existing `Labels.Group` words; "Closed" fold label and the box titles above are new constants (`ClosedHeader(count)`, `NothingInQueue`, `NoItemsMatch`, `SindriNotRunningTitle`).

## 7. Tests the ui lane must add or change

- `QueueTableRowsTests`: group order, counts, the id beside the title, compact band.
- `QueueStatesTests`: every state in section 3 shows its banner or box and the served empty sentence per group.
- `QueueFilterTests`: All and Running counts, the text filter over id, title and repo.
- `ClosedFoldTests`: collapsed by default, reads `done` and `cancelled` once, rows have no close actions.
- `ItemSheetLayoutTests`: callout for an open decision, confirm and keep, ended items hide the actions.
- Frames at 1440 and 900 for the table, loading, empty and stalled.

## 8. Build order (each slice red test first)

1. **Row and status.** Red: `QueueTableRowsTests`. One-line `ItemRow` with the id beside the title, status badge, plain stage with the "Not started" fallback.
2. **Table, groups and bands.** Red: group rows, empty lines, compact band. Replaces the four panels on the page (Home keeps its panels).
3. **States.** Red: `QueueStatesTests`. Loading skeleton, the empty box, the three banners, Sindri not running.
4. **Closed fold.** Red: `ClosedFoldTests`.
5. **Sheet layout.** Red: `ItemSheetLayoutTests`. Header block, decision callout, Details and History caps sections.
6. **Dialog.** Red: dialog layout and the required-title error.
7. **Pending data (separate PR).** A served plain stage name and the decision question on the item read; each removes its fallback.

## 9. Open items for the ui lane

1. **Plain stage.** Until the api serves it, map only the line's five stages and show the raw step in grey for anything else.
2. **Closed fold read.** One read per state (`done`, `cancelled`) merged and sorted by `since`; confirm the page size.
3. **Answering from the sheet.** The prototype sends the owner to Needs you; if the owner wants answers in the sheet, reuse `DecisionSheet`'s controls.
