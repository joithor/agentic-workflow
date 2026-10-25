# Spec: Agents (Avalonia build spec)

Screen `agents` (the rail's Agents). Inputs: `screens/agents.html` (the prototype, built by `data/tools/pg_agents.py`), `screens/agents.notes.md`, `data/PLAIN-WORDS.md`, `docs/csharp/sindri-ui.md`, screens spec section 4.5 and the collector spec (CU-1). Code read at origin/main: `src/Sindri/Aw.Sindri.Ui/{Views/Agents,ViewModels/Agents,ViewModels/Labels.Agents*.cs,ViewModels/Shell/ShellViewModel.Agent*.cs,Api/Sessions}`, `src/Sindri/Aw.Sindri.Host/Endpoints/{ListSessionsEndpoint,GetSessionEndpoint}.cs`, goldens `tests/Aw.Sindri.Tests/Golden/Api/{sessions,sessions-id}/*.json`.

The page already exists and works (`AgentsPage`, `SessionRow`, `SessionGroup`, `EndedSessionRow`, `SessionSheetView`). This spec re-skins it to the F3 look and adds the states the prototype now shows. It changes no endpoint, no field and no key.

## 1. Goal and decisions

One question: which coding agents are working on this box right now, which one is asking me something, and which belong to a workflow run.

| Q | Decision | Effect |
|---|---|---|
| 1 | Rows are one 36 px line each, in a table with a header (Status, Agent, Repository, Where it runs, What it is doing, For), grouped "Started by Sindri" then "Not part of a workflow" | Replaces the stacked `SessionRow` cards with the list style of the Workflows list (`.lrow`); groups keep `SessionGroupViewModel` |
| 2 | Status is one badge with a glyph and a word: Working, Asking you, Idle (served `state` running, asking, idle; anything else shows its own word, grey) | `SessionChips` words change from ALL CAPS to sentence case; the glyph is never colour alone |
| 3 | A row opens the session sheet (the existing sheet), 480 px, with the same fields as today in four groups: Where it is working, Timing, Host, History | Sheet stays the shell sheet (not page-local); only its layout and caps headings change |
| 4 | "Watch the agent" stays disabled with "Coming soon" while `focus.possible` is false, with the terminal command under it | Unchanged behaviour; the copy chip is the existing `FocusCliFallback` |
| 5 | "Open the run" shows only when the session has a `launch.instance`; it opens the run canvas at that run's line root | Existing `FrameRequested`; the sheet button is the existing `OpenStepFrame` |
| 6 | Recently ended is a fold under the list, collapsed, each row with a copy chip of the served `resume` command or "No resume command" | Existing `EndedSessionRow`; chip uses `Labels.NoResumeCommand` |
| 7 | The page says plainly what it does not show: "Cursor IDE chats aren't shown." | Existing `Footer` |

**Not built (prototype only):** the left rail, the 56 px top bar, the "View as" state switcher, the Annotations switch. The shell keeps its 48 px bar and nav; the search box is a page filter (`/` focuses it) beside the three filters.

## 2. Regions and layout

| Region | Size and tokens | Avalonia |
|---|---|---|
| Head | `page-title` "Agents" + caption `Head` ("5 agents · 1 asking you", `Labels.AgentsCounts`); right: the three filters (provider, status, repo) as 28 px selects with a grey key word | existing `Grid` with `ProviderFilter`, `StateFilter`, `RepoFilter` (keep x:Names), restyled as `.sel` chips |
| Stale line | one amber line with a warn glyph, shown when `IsStale` | existing `AgentsStale` |
| Problem lines | one amber line per `problems[]` entry: the served text, the source in grey mono | existing `AgentsProblems` (`problem-line`) |
| Panel | `Surface1` card, `Radius.LG`, hairline, no inner scroll; sticky column header 28 px; group rows 32 px; session rows 36 px, hairline between | `ItemsControl` of `SessionGroup`; header row is a plain `Grid` |
| Columns | Status 150, Agent (provider in grey + title in 500) flexible 2fr, Repository (repo, then the worktree's last folder) 1.2fr, Where it runs 100, What it is doing 1.4fr, For 56 right aligned, tabular | `Grid ColumnDefinitions` with the widths in `Size.*` keys |
| Narrow band | the panel is a width container: under 640 px only Status 112, Agent and For stay (the sheet carries the rest) | `PageWidthBand` already on the shell; add `Compact` rows via a `Classes.compact` |
| Recently ended | fold row 36 px with a chevron and "Recently ended (3)"; rows: Ended badge, agent, repository, resume chip, "ended 20m ago" | existing `AgentsEndedToggle` and `EndedSessionRow` |
| Footer | caption with an info glyph | existing `AgentsFooter` |
| Sheet | 480 px; header: badge + provider, title 16/600, "In this status for 4m", copy chip of the `ref`; actions (Open the run, Watch the agent); an amber callout when asking ("Answer it in its own terminal; Sindri only shows that it is waiting"); caps sections with a 150 px label column | existing `SessionSheetView`, restyled; no new view model |

No new tokens: every colour is an existing app token (`Surface*`, `Hairline*`, `Attention*`, `Run*`, `Idle*`).

## 3. States (all in the prototype, `?state=`)

| State | What shows | Source |
|---|---|---|
| Agents running | the groups, the counts line | 200 with sessions |
| Reading | five skeleton rows and "Reading sessions…" (`Labels.ReadingSessions`); filters disabled | first read in flight |
| No agents | the served `empty` sentence "No agent sessions are running." plus a button to Workflows | 200 with no sessions |
| Filters match nothing | "No sessions match these filters." and Clear filters | client filter over a non-empty list |
| A source failed | the list plus an amber line per `problems[]` | `problems` not empty |
| Out of date | the last list, unchanged, with the stale line above it | `IsStale` |
| Can't see sessions | the unavailable message `Labels.SessionsUnavailable(detail)` and a link to System | 503 `SND-SESSION-002` (`disabled`, `proc-unreadable`, `unsupported-platform`) |
| Can't read the answer | `Labels.SessionsUnreadable` | the client cannot decode (older app) |
| Session sheet: ended, no resume | "This session has ended." and "There is no command to pick it up again." | `recentlyEnded` row with `resume: null` |

## 4. Data

Served, used as is (no field added): `GET /sindri/sessions` -> `sessions[]`, `recentlyEnded[]` (`session`, `endedAt`, `resume[]`), `problems[]` (`source`, `code`, `text`), `counts` (`live`, `asking`, `outsideWorkflow`), `empty`, `asOf`. `GET /sindri/sessions/{ref}` -> `session`, `cwd`, `stateHistory[]` (`state`, `reason`, `at`, `source`), `asOf`. A session: `ref`, `provider`, `providerLabel`, `id`, `idKnown`, `state`, `asking{reason, tool, since}`, `stateSource`, `estimated`, `repo`, `worktree`, `host{kind, label, socketKind, attachedClients, terminal, session, pane, socket}`, `lane{team, lane}`, `launchId`, `startedAt`, `since`, `focus{method, possible, why}`, `title`, `launch{instance, state, attempt}`, `pid`.

Gaps shown as dashed outlines when Annotations is on (not served; none is needed to build the page):
- **Allowance used.** The old not-mocked text promised "how much of its allowance it has used". No session field carries tokens or allowance. The prototype does not draw it; the sheet footnote says so. Needs a collector field if the owner wants it.
- **What it is doing.** Only state, the asking reason and tool, the launch step and the lane are served; there is no free-text activity line.
- **Watch the agent.** `focus.possible` is false (`not-built`); the button stays disabled.

## 5. Actions and keys

| Key or click | Does |
|---|---|
| J, K, arrows | move the selection through the open groups, then the ended rows if the fold is open (existing `AgentsPageViewModel.Keys`) |
| Enter or click | open the sheet for the row |
| Esc | close the sheet |
| `/` | focus the search |
| Open the run | `FrameRequested` with the launch instance's line root |
| Copy chip | copies the ref, the resume command or the focus command |

No protected action and no write: the page only reads. It never answers an agent.

## 6. Copy (`Labels.Agents*.cs`; no em dashes, no internal terms)

Reuse: `ReadingSessions`, `NoSessionsMatch`, `SessionsUnavailable`, `SessionsUnreadable`, `RecentlyEnded`, `NoResumeCommand`, `OutsideWorkflow` ("Not part of a workflow"), `StartedBySindri`, `CursorChatsNotShown`, the sheet labels. Change: the three state words to sentence case ("Working", "Asking you", "Idle"); a column header set (Status, Agent, Repository, Where it runs, What it is doing, For); "Out of date" line "Showing the last answer. Sindri has not answered for 30 seconds." (existing `StaleText` wording wins if it differs).

## 7. Tests the ui lane must add or change

- `SessionChipsTests`: the three words in sentence case, an unknown state shows its own word.
- `AgentsPageRowsTests`: group order and counts, the compact band drops the three columns.
- `AgentsPageStatesTests`: every state in section 3 renders its line and nothing else (reading, empty, no match, problem line, stale, unavailable, unreadable).
- `SessionSheetLayoutTests`: field groups and the callout for an asking session, the ended sheet with and without a resume command.
- Frames at 1440 and 900 for populated, reading, empty and unavailable (the existing agents frames move).

## 8. Build order (each slice red test first)

1. **State words and badge.** Red: `SessionChipsTests`. Sentence-case words and the `StatusBadge` glyph pair; no layout change.
2. **Table rows.** Red: `AgentsPageRowsTests`. Replaces `SessionRow` with the one-line row, header and group rows; compact band at 640 px; keeps every x:Name and the key walk.
3. **States.** Red: `AgentsPageStatesTests`. Reading skeleton rows, the empty and unavailable boxes, the problem and stale lines as the amber strip.
4. **Sheet.** Red: `SessionSheetLayoutTests`. Header block, callout, caps sections with the 150 px label column, History rows as stage-style rows.
5. **Ended fold.** Red: ended row tests. Resume chip and the no-resume row.

## 9. Open items for the ui lane

1. **Allowance.** Ask the collector lane whether a session can carry work-allowance used; until then the page says nothing about it.
2. **Search.** The served list has no text filter; the search box filters the loaded rows by provider, title and repository on the client.
3. **Host labels.** `host.label` ("tmux · Warp") is served as is; the plain-word pass may want "Terminal (tmux)".
