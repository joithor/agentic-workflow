# Spec: Home (Avalonia build spec)

Screen `home` (the rail's Home, the default page, chord G H). Inputs: `screens/home.html` (built by `data/tools/pg_home.py`), `screens/home.notes.md`, `data/PLAIN-WORDS.md`, screens spec section 4.1 and 4.2. Code read at origin/main: `src/Sindri/Aw.Sindri.Ui/{Views/Shell/HomePage.axaml,ViewModels/Shell/ShellViewModel.Home.cs,ViewModels/Shell/ShellViewModel.Strip.cs,ViewModels/Shell/AttentionSummary.cs,ViewModels/Queue,ViewModels/NeedsYou,Views/NeedsYou/NeedsYouRow.axaml,ViewModels/Labels.NeedsYou.cs,ViewModels/Labels.Strip.cs}`, goldens `tests/Aw.Sindri.Tests/Golden/Api/{needs-you,queue,workflow-instances,status,setup}/*.json`.

`HomePage` exists: a setup line, Needs you rows, the workflow strip, Running and With you, Queued, and the health strip. The prototype's Needs you screen (`needs-you.html`) is the full inbox behind "All 7 →"; this spec is the Home page that links to it, which the prototype had no page for.

## 1. Goal and decisions

One question: what needs me right now, and is anything else moving.

| Q | Decision | Effect |
|---|---|---|
| 1 | Two columns: Needs you on the left (five newest rows, then "All 7 →"), Running and Queued on the right; the health strip across the bottom | `HomePage` regrouped; every x:Name stays |
| 2 | A Needs you row is a 28 px kind tile (amber), the plain title, the item and kind in grey, the waiting time right; the row opens the item in Needs you | existing `NeedsYouRow`; "new" word beside new rows (not colour alone) |
| 3 | Running rows carry the shared status badge (Working, You took over) with the stage and step in grey and the time in the step | existing strip rows plus `StatusBadge` |
| 4 | Queued shows the three oldest items and "Show all 3 in Work" | existing `Queue.Queued` and `ShowAllInQueue`, link text "in Work" |
| 5 | The health strip is five cells (Process, Epoch and lease, API, Services, Profile), each a label and a word | existing `Strip` cells |
| 6 | The setup line shows only until all six setup steps are done: "Finish setting up Sindri", "2 of 6 done", a meter, the next step, Continue | existing `Setup.HomeLineText`, `CountText`, `HomeStepText` |
| 7 | "Mark all seen" is a text link in the Needs you header with Undo after it (existing behaviour) | existing `NeedsYou.Seen` |
| 8 | Start workflow is the primary button on the toolbar | existing `WorkflowsStart` |

**Not built (prototype only):** the rail, the 56 px bar, "View as", Annotations. The shell keeps its 48 px bar and nav; the summary line under the title is the existing `Attention.SummaryLine`.

## 2. Regions and layout

| Region | Size and tokens | Avalonia |
|---|---|---|
| Summary bar | 48 px: "7 need you · 4 running · 1 with you · 3 queued", right: Start workflow | existing head `Grid` |
| Setup line | full width card with an info glyph, bold title, count, 120 px meter, next step, Continue (`Primary`) | existing setup `Border` |
| Needs you | `Surface1` card, caps header "Needs you" with Mark all seen right; rows min 52 px; footer link "All 7 →" | `ItemsControl` of `NeedsYouRow` |
| Running | card, header "Running" with "5 open" right; rows with the badge, title, stage and step, time | `ItemsControl` of strip rows |
| Queued | card, header "Queued" with the count; rows with a New badge, title, repo; footer link | queue rows |
| Health strip | five cells separated by hairlines, label in 11 px caps, value in 13 | existing strip |
| Under 1100 px | the two columns stack; the strip becomes two columns | `PageWidthBand` |

No new tokens.

## 3. States (all in the prototype, `?state=`)

| State | What shows | Source |
|---|---|---|
| Busy day | all regions populated | normal |
| First run | the setup line above the regions; Profile cell says "not approved" | `GET /setup` not complete |
| Nothing needs you | "Nothing needs you.", "Nothing running. Start a workflow to see it here.", "Nothing queued." | empty groups |
| Loading | skeleton rows in Needs you and Running | first read |
| Out of date | the last answer with the amber line | `IsStale` |
| Sindri not running | the box "Sindri is not running" and the copy chip `sindri serve` | `ShellState.NotRunning` |

Also on Home today and unchanged: the held banner (`NeedsYou.HasBanner`), the "N now need you" announcement and the overflow line.

## 4. Data

All served: needs-you rows (`kind`, title, item, since), the line instances (`root`, status words, step), queue groups, `status` (process, epoch, lease, api, services, profile, health, sessions), `setup` (step states). No gaps beyond those already listed in the Needs you and Workflows specs (plain step names, the stop number on a row).

## 5. Actions and keys

| Key or click | Does |
|---|---|
| J, K, Enter | move and open a Needs you row (existing) |
| Click a Running row | opens the run canvas |
| Mark all seen, Undo | existing seen flow |
| Start workflow | existing `StartDialog` |
| All 7 →, Open Workflows, Show all in Work | go to Needs you, Workflows, Work |
| Continue | goes to Settings at the next step |

## 6. Copy

Reuse `Labels.NeedsYou*`, `Labels.Strip*`, `Labels.Summary`. Change: the Running header drops "RUNNING 4 · WITH YOU 1" for "Running" with "5 open"; "Show all 3 in Queue" becomes "in Work" with the page's name; sentence-case panel captions.

## 7. Tests the ui lane must add or change

- `HomeLayoutTests`: region order and band stacking at 900 px.
- `HomeStatesTests`: every state in section 3.
- `HomeRunningRowsTests`: badge words for running and taken over.
- Frames at 1440 and 900 for busy, first run and nothing needs you.

## 8. Build order (each slice red test first)

1. **Running rows with the badge.** Red: `HomeRunningRowsTests`.
2. **Two-column layout and bands.** Red: `HomeLayoutTests`.
3. **States.** Red: `HomeStatesTests`. Loading skeletons, empty lines, the not-running box.
4. **Strip and setup line restyle.** Red: layout tests for the five cells and the meter.

## 9. Open items for the ui lane

1. **Two Needs you surfaces.** Home's panel and the full inbox share row data; keep one `NeedsYouRowViewModel` and let Home show five.
2. **Home versus Needs you in the rail.** The prototype's rail Home opens this page; `needs-you.html` keeps Home highlighted because it is a part of Home in the app (the full inbox opens over it).
