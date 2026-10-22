# Spec: System (Avalonia build spec)

Screen `system` (the rail's System, chord G .). Inputs: `screens/system.html` (built by `data/tools/pg_system.py`), `screens/system.notes.md`, `data/PLAIN-WORDS.md`, screens spec section 4.6 and 4.8. Code read at origin/main: `src/Sindri/Aw.Sindri.Ui/{Views/Shell/HealthPage.axaml,ViewModels/Shell/Health*.cs,ViewModels/Shell/HealthDetail.cs,ViewModels/StatusText.cs,ViewModels/ServiceRow.cs,ViewModels/Labels.Actions.cs}`, endpoints `GetStatusEndpoint`, `ListServicesEndpoint`, `RunServiceEndpoint`, `PauseServiceEndpoint`, `ResumeServiceEndpoint`, goldens `tests/Aw.Sindri.Tests/Golden/Api/{status,services,commands}/*.json`.

The app's System page today is `HealthPage`: one health sentence, the profile fix line, an Actions card and a Details expander (control plane, services as read-only rows, profile, last event). This spec re-skins it as cards, adds a Services tab with the three service actions the command catalog already marks ready, and shows every health state. It changes no endpoint.

## 1. Goal and decisions

One question: is Sindri itself healthy, and if not, what do I do.

| Q | Decision | Effect |
|---|---|---|
| 1 | The page opens on Overview: one headline (badge plus the health sentence), then "Needs a look" lines when `health.problems` is not empty, then cards: Control plane, Profile, Views, Agents, Services, Actions | The Details expander goes away; its rows become the cards. `HealthViewModel` keeps `Sentence`, `Pill`, `Detail` |
| 2 | A second tab, Services, lists each scheduled service with every, status, what it does, last run, and Run now and Pause or Resume | `ListServices` and the three catalog actions, all `ui: ready` today; sent through `CommandControl`; destructive none |
| 3 | "Background jobs" is a button beside the tabs that opens the Jobs page | existing `JobsLink`; Jobs stays reachable by G J and the palette |
| 4 | Profile approval stays terminal copy (`sindri profile approve` in an amber line) until the setup-once spec builds the typed confirmation | existing `ProfileFix` and `ProtectedActionButton` |
| 5 | Actions card says in numbers what the app can do: ready, terminal on purpose, arriving later (from the served catalog) | existing `PendingSummary`, reworded; counts come from `catalog.json` rows by `ui` |
| 6 | Stop Sindri is the one destructive action here: it asks "Stop Sindri? It stops every service and run." with Confirm and Keep running | existing `StopProcess` catalog row (`ui: ready`, `destructive`) |
| 7 | Health words stay as served: Healthy, Degraded, Stalled, Rebuilding; the headline says the sentence the app already builds | `Labels.HealthWord`, `HealthViewModel.SentenceFor` unchanged |

**Not built (prototype only):** the rail, the 56 px top bar, "View as", Annotations. The shell keeps its 48 px bar; the shell's own health pill keeps its job.

## 2. Regions and layout

| Region | Size and tokens | Avalonia |
|---|---|---|
| Toolbar | 48 px: segmented Overview and Services (count), then the Background jobs button | segmented `ListBox` as in the Workflows spec 2 |
| Headline | full width card, 14 px padding; badge (glyph and word) and the sentence in 16/600; tone amber for degraded, profile or out of date, red for stalled, neutral otherwise | existing `HealthSentence` in a `Border` |
| Needs a look | `Surface1` card, one row per problem: warn glyph, served `text`, code in grey mono | `ItemsControl` of `health.problems` |
| Cards | two columns at 1100 px and up, one below; each card has a 40 px caps header and a label and value list (label 130 px) | `UniformGrid` or a `Grid` of `Border`s; rows are the existing `HealthCell` list |
| Control plane | Process (Running, pid), Epoch and lease (held, expires), API (address, v1), This app (API v1), Last event | `Detail.Process`, `LastEvent` |
| Profile | status badge in the header; Approved, Live (only when different), File | `Detail.Profile`, `ProfileHead` |
| Views | badge Up to date, Rebuilding or Stalled; Read up to "event 214 of 214", Updated; the fix `sindri views rebuild` as a copy chip when stalled or rebuilding | `status.views` |
| Agents | Running, Asking you, Problems, with an Open Agents link | `status.sessions` |
| Services (card) | the first four services with every and last run; header link "6 scheduled" opens the tab | `Services` |
| Actions | three counts and Stop Sindri | `CatalogViewModel` |
| Services tab | table: Service, Every, Status, What it does, Last run, Actions (240 px); under 900 px only Service, Status and Actions | `ItemsControl`; status badge Scheduled, Running now, Paused, Last run failed |

No new tokens.

## 3. States (all in the prototype, `?state=`)

| State | What shows | Source |
|---|---|---|
| Everything is running | headline "Everything is running." and the cards | `health.word = healthy` |
| A service failed | amber headline "Something needs a look: service heartbeat failed." and one problem line with its code | `health.problems` |
| Profile needs approval | amber headline, the terminal command line, Profile card "Changed since approval" with the live hash | `profile.needsApproval` |
| Rebuilding views | neutral headline with a spinner, Views card Rebuilding with the fix chip | `views.rebuilding` |
| Views stalled | red headline, problem line, Views card Stalled | `views.stalled` |
| Stopped answering | the last answer with an amber line "Showing the last answer. Sindri has not answered for 30 seconds." | `ShellState.Stale` |
| Not running | box "Sindri is not running" with the copy chip `sindri serve` | `ShellState.NotRunning` |
| Different versions | box with "This app: API v1" and "Sindri: API v2" | `ShellState.VersionMismatch` |
| Can't read the answer | box "Sindri is running, but this app can't read its answer" | `ShellState.Unreadable` |
| Connecting | headline "Connecting…" and skeleton rows | `ShellState.Connecting` |
| Stop confirm | the Stop button is replaced by the question, Confirm and Keep running | existing confirm flow |

## 4. Data

Served: `GET /sindri/status` -> `running`, `pid`, `epoch`, `leaseExpiresAt`, `profile{approved, live, changedSinceApproval, problem, source, needsApproval}`, `services[{name, every, lastRun}]`, `apiVersion`, `views{cursor, ledgerHead, projectedAt, rebuilding, stalled, stalledAt, stalledType, text}`, `health{word, problems[{code, text, clause}], sentence}`, `sessions{live, asking, problems}`. `GET /sindri/services` -> `services[{name, every, paused, lastRun{slot, outcome, code, ranAt}, nextSlot, job}]`. Catalog `ui` counts: 49 ready, 14 cli-only, 4 pending.

Gaps (Annotations on): **What each service does** is not served; the prototype's one-line plain descriptions are a proposal (a `description` on the service in the profile, or a label map in the app). The Services tab is new UI over served data.

## 5. Actions and keys

| Control | Does |
|---|---|
| Run now | `RunService` with the service name; the row shows Running now with a View job link to the Jobs page |
| Pause, Resume | `PauseService`, `ResumeService`; no confirmation (reversible) |
| Stop Sindri | `StopProcess` after the in-app Confirm; the page then shows Not running |
| Copy chips | `sindri profile approve`, `sindri views rebuild`, `sindri serve` |
| Tabs | click, or Left and Right on the focused segment |

Protected actions (profile approval) stay terminal copy.

## 6. Copy (`Labels.cs`, `Labels.Actions.cs`)

Reuse every health sentence and word. Change: card captions lose ALL CAPS in the label constants (Control plane, Profile, Views, Agents, Services, Actions); add `ServicesTab`, `RunNow`, `PauseService`, `ResumeService`, the four service status words, `StopSindriQuestion`.

## 7. Tests the ui lane must add or change

- `HealthCardsTests`: each card's rows for healthy, degraded and profile states.
- `HealthStatesTests`: every state in section 3 shows its headline or box (the existing state matrix, reworded).
- `ServicesTabTests`: status word per service (paused, running job, failed last run, scheduled), actions call the three routes once.
- `StopConfirmTests`: Confirm sends `StopProcess`, Keep does not.
- Frames at 1440 and 900 for Overview, Services and Not running.

## 8. Build order (each slice red test first)

1. **Headline and problems.** Red: `HealthStatesTests`. Headline badge and tone, the problem lines.
2. **Cards.** Red: `HealthCardsTests`. Control plane, Profile, Views and Agents cards from the existing detail view models; the expander is removed.
3. **Services tab.** Red: `ServicesTabTests`. The tab, the table, Run now, Pause and Resume through `CommandControl`.
4. **Actions and Stop.** Red: `StopConfirmTests`. The counts card and the confirm flow.
5. **Other states.** Not running, versions, unreadable, connecting, stale as boxes and the amber line.

## 9. Open items for the ui lane

1. **Service descriptions.** Decide the source (profile text or an app map); ship the app map first.
2. **Services page home.** If the owner prefers a separate Services page to a tab, the tab body moves as is.
3. **Counts in Actions.** The 14 terminal actions are "on purpose" per the owner's rules; confirm the wording with the owner.
