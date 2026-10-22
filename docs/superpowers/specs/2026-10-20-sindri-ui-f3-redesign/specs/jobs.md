# Spec: Jobs (Avalonia build spec)

Screen `jobs` (opened from System's "Background jobs" button, chord G J and the palette; it is not a rail tab). Inputs: `screens/jobs.html` (built by `data/tools/pg_jobs.py`), `screens/jobs.notes.md`, `data/PLAIN-WORDS.md`, API P6. Code read at origin/main: `src/Sindri/Aw.Sindri.Ui/{Views/Jobs,ViewModels/Jobs,ViewModels/Labels.Jobs.cs}`, endpoints `ListJobsEndpoint`, `GetJobEndpoint`, `CancelJobEndpoint`, goldens `tests/Aw.Sindri.Tests/Golden/Api/{jobs,jobs-id}/*.json`.

The page exists (`JobsPage`, `JobRow`, `JobSheetView`). This spec re-skins it to the F3 table and sheet and shows every state. No endpoint or field changes.

## 1. Goal and decisions

One question: what is Sindri doing in the background, and did it work.

| Q | Decision | Effect |
|---|---|---|
| 1 | A table, newest first as served: Job (plain kind name plus the key in grey), Status, When, Started by, What happened, Rounds | `JobRow` becomes the one-line list row |
| 2 | Kinds read as plain names (Sync sources, Refresh the context map, Build the context map, Run a service, Approve all workflows, Add a repository, Check agent launch targets, Test the context map); an unknown kind shows as served | a kind-to-words map in `Labels.Jobs`; raw kind stays in the sheet |
| 3 | Status words keep the app's four: Running, Done, Failed, Cancelled, with the shared glyph pair | `Labels.JobChip` words in sentence case |
| 4 | "Started by" keeps the existing translation: Sindri, you, "Sindri's heartbeat service", an agent session | `Labels.JobStartedBy` unchanged |
| 5 | The sheet shows Details, Progress (round and line), What it produced (the cut-short result) and, for a running job, Cancel job with Confirm and Keep and the terminal command | existing `JobSheetViewModel`; actions list comes from the served `actions` |
| 6 | Head says "1 running · 6 jobs"; the empty line is the served sentence | existing `JobsHead`, `empty` |

**Not built (prototype only):** the rail, the 56 px bar, "View as", Annotations. The shell keeps its 48 px bar.

## 2. Regions and layout

| Region | Size and tokens | Avalonia |
|---|---|---|
| Head | `page-title` "Background jobs", caption "1 running · 6 jobs"; a one-line purpose note under the toolbar (`Labels.JobsPurpose`) | existing head |
| Toolbar | 48 px: Back to System, Status filter chip, Kind filter chip, key hints | `Button`, two `ComboBox`es restyled as chips (the served `state` and `kind` query filters) |
| Table | `Surface1` card; header 28 px; rows 36 px; Job flexible 1.6fr, Status 110, When 150, Started by 1.2fr, What happened 130, Rounds 56; under 760 px only Job, Status and Rounds | `ItemsControl` of `JobRow` |
| Sheet | 480 px drawer: status badge, kind title, "Started 1m ago · by Sindri's context-map-refresh service", copy chip of the job id, Cancel job when running, a red callout when failed with the served summary, caps sections Details, Progress, What it produced, To cancel from a terminal | existing `JobSheetView`; the shell sheet |

No new tokens.

## 3. States (all in the prototype, `?state=`)

| State | What shows | Source |
|---|---|---|
| Jobs | the table | 200 with jobs |
| Reading | six skeleton rows and "Reading jobs…" | `Labels.ReadingJobs` |
| No jobs yet | the box with the served sentence and a link to System's Services | `jobs/empty-unfiltered` |
| No running jobs | the table of ended jobs with "No running jobs." as a line above them | `jobs/empty` with the running filter |
| Can't read the list | `Labels.JobsUnreadable` | decode failure |
| Couldn't read just now | red line `Labels.JobsFailed(error)` with Try again; the old table dimmed | refused or 5xx |
| Sheet: running, done, failed, cancelled | as section 2; failed shows the callout; done shows What it produced | `jobs-id/*` |
| Sheet: unknown job | `Labels.JobUnknown` | `SND-JOB-404` |

## 4. Data

Served, as is: list `jobs[{id, kind, key, item, repo, service, state, startedBy, startedAt, endedAt, rounds, summary, code}]`, `empty`, `asOf`; detail adds `progress[{round, line, at}]`, `result`, `cancelledBy`, `actions[]` (`CancelJob` while running), `cli`. The filters are the served `state` and `kind` query parameters (`SND-CLI-003` lists the valid kinds). No gaps beyond the plain kind names, which the app maps itself.

## 5. Actions and keys

| Key or click | Does |
|---|---|
| J, K, Enter, Esc | move, open, close (existing) |
| Cancel job | `CancelJob` (class `job`, ui ready) after the in-app Confirm; a finished job refuses (`SND-JOB-*`) and the sheet re-reads |
| Copy chips | job id, the `cli` cancel command |
| View job (from System) | opens this page with that job's sheet (`?open=`) |

## 6. Copy (`Labels.Jobs.cs`)

Reuse every constant. Change: the four status words to sentence case, the column headers (Job, Status, When, Started by, What happened, Rounds), "1 round" for one; add the kind-to-words map.

## 7. Tests the ui lane must add or change

- `JobRowsTests`: plain kind names, unknown kind as served, status words, "1 round".
- `JobsStatesTests`: every state in section 3.
- `JobSheetLayoutTests`: callout on failed, progress lines, result block, cancel confirm and keep.
- Frames at 1440 and 900 for the table and the running sheet.

## 8. Build order (each slice red test first)

1. **Words.** Red: `JobRowsTests`. Kind map, sentence-case status words, shared badge glyphs.
2. **Table.** Red: row and band tests. The one-line row, header, compact band.
3. **States.** Red: `JobsStatesTests`. Skeleton, empty, unreadable, retry line.
4. **Sheet.** Red: `JobSheetLayoutTests`. Header block, callout, caps sections, cancel flow.

## 9. Open items for the ui lane

1. **Filters.** Today the page has no filter chips; adding them needs the served `state` and `kind` parameters only. Confirm they ship in slice 2 or later.
2. **Kind names.** The plain names are proposals; the owner reviews them with the plain-words pass.
