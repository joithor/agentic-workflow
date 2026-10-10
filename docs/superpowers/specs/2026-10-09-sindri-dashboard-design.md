# Sindri dashboard: one place to see every agent (Plan 6 design)

Status: approved in conversation, section by section, 2026-10-09. This document is the written spec for review.
It refines and widens the main Sindri spec §10.5 (`2026-10-07-sindri-design.md`). Where the two disagree, this
document wins for the dashboard; the §10.5 security and accessibility rules stay unless replaced here.

## 1. Purpose

The dashboard is the builder's own "T3 / Warp" view of all agent work on the machine. In one place the builder can:

- see the progress of every agent, across every provider agentic-workflow supports (Claude Code, Codex, Cursor),
  including agents that Sindri didn't start;
- see the latest indexing status of each onboarded repo;
- see the work Sindri started on its own (observe, index builds, the weekly evolve job, scope runs, proposals,
  channel builds);
- see at a glance what needs them, and act on it.

The dashboard is read-mostly. The builder works with an agent by **attaching** to it in its own host (T3, Warp,
Cursor, a terminal). There is no chat inside the dashboard.

**Success looks like this:**
- the builder can tell, without reading text, that a page has something that needs them;
- every live agent on the machine appears within one poll;
- states are right, including approvals, questions, crashes and interruptions;
- no human gate from the CLI can be bypassed through the browser.

**Out of scope for this plan:**
- in-app chat or terminals;
- starting new worker agents (that's the Sindri runner, rollout step 3a: its sessions appear here once it exists);
- container metrics;
- a native app.

## 2. Prior art checked: T3 Code

T3 Code (github.com/pingdotgg/t3code, read at b1ec4b36) was reviewed against this design.

**What T3 does differently.** It tracks only the sessions it starts itself, through each provider's SDK or protocol:
- the Claude Agent SDK;
- the Codex app-server JSON-RPC;
- the Cursor SDK;
- ACP, for third-party agents.

That gives it a pushed event stream. It reads `~/.claude/projects` and `~/.codex/sessions` only to import history,
never for live state. Observing every agent on the machine is therefore new ground. This design takes from T3:
- its state model and the order states rank in;
- its bounded way of scanning transcripts;
- its loopback auth model.

**Taken from T3:**
- State names, and the priority approval > input > working > background > failed/limited > ready.
- Separating `background`, where the agent is stopped but child tasks will wake it, from `ready`, where it's the user's turn.
- Classifying Claude turns from the result record: `terminal_reason` `aborted_tools` or `aborted_streaming`
  means interrupted. A 429 or 529 API status means failed or limited, even when the result says `success`.
- Codex's `thread/status/changed` flags (`waitingOnApproval`, `waitingOnUserInput`) when an app-server is reachable.
- Transcript scanning order and limits: newest first, at most 1 MB per transcript, a cap on the number of sessions
  per source, and each source kept separate so one failure can't affect another.
- Subagents roll up to their parent: child rows are hidden, the parent counts pending children, and a child's
  approval request raises the parent's status.

**New here:**
- observing external sessions;
- the hook spool (§4.2);
- the `stuck` state, which T3 has no watchdog for.

## 3. Architecture

```
provider stores ─┐
hook spool ──────┼─► sindri fleet (collector + state rules) ─► sindri fleet --json ─┐
sindri ledger ───┤                                                                  ├─► sindri dashboard (HTTP, 127.0.0.1)
gh / bridge ─────┘    sindri status/evolve/index … --json ──────────────────────────┘        │
                                                                                             └─► menu-bar badge (SwiftBar)
```

- **One source of truth:** the server only runs `sindri … --json` commands and serves their output, plus static files.
  Actions are a fixed list of CLI commands, so the dashboard holds no state of its own.
- **Polling:** the browser polls every 5 s; there is no websocket in v1. T3 pushes events only because it owns the
  processes; an observer has to poll.
- **New code:**
  - `sindri/src/fleet/`: adapters, the hook spool reader, state rules, and the needs-you aggregator;
  - `sindri/src/dashboard/`: the server, static UI, and attach resolver;
  - an `aw:fleet` hook in `config/hooks/` (with per-provider adapters);
  - a SwiftBar plugin;
  - docs in `docs/sindri/dashboard.md`.

## 4. Collector (`sindri fleet`)

### 4.1 Adapters

Each provider adapter has the same interface, `discover(ctx) → Session[]`, where a `Session` is:

```
{ provider, id, parentId|null, cwd, repo|null, worktree|null, host: "t3"|"warp"|"terminal"|"cursor"|"tmux"|"unknown",
  pid|null, alive, startedAt, updatedAt, title, activity, signals: {...}, estimated: boolean }
```

- **Claude Code:**
  - Liveness, busy or idle, entrypoint, name, cwd and session id come from `~/.claude/sessions/<pid>.json`.
    A record only counts as live if its `procStart` matches the running process with that pid, so a recycled pid
    isn't mistaken for the session.
  - History, the final result record and subagents come from `~/.claude/projects/<slug>/<id>.jsonl` and its
    `subagents/` folder, through the transcript readers Sindri and the scorer already have.
  - The host comes from the entrypoint and cwd. For example, `sdk-ts` with a cwd under `~/.t3/worktrees` means T3.
- **Codex:**
  - History comes from `~/.codex/sessions/**/rollout-*.jsonl`, and liveness from the process list.
  - When a Codex app-server can be reached, `thread/status/changed` flags are authoritative.
- **Cursor:** `~/.cursor/chats/*/<id>/meta.json` and the agent CLI state, with liveness from the process list.
  States are marked `estimated` unless hook events say otherwise.
- **Sindri jobs:** read from the ledger and lock files: observe, index builds per repo, the weekly evolve job,
  scope runs, the heavy-job lock holder and its queue.

**Rules for every adapter:**
- **Read-only:** never write into a provider's folders.
- **Bounded:** each pass reads only the tails of recently changed files, newest first, at most 1 MB per transcript,
  with a cap on sessions per source and a time budget per pass.
- **Failures stay contained:** a failing adapter returns an error entry for its own section and doesn't affect the others.
- **Scrubbed:** every text field goes through the profile scrubber, and titles and activity lines are capped in length.
- **Privacy:** the collector follows the source-based privacy rules when they land (separate plan). Workplace text
  shown locally is fine, because it never leaves the machine and the dashboard serves only 127.0.0.1.

### 4.2 The `aw:fleet` hook spool

The `aw:fleet` hook is installed for every provider through the existing per-provider hook adapters. It appends
one line per event to `$AW_STATE_DIR/sindri/fleet/spool/<provider>.jsonl` (mode 0600):
- for permission requests and notifications: `PermissionRequest`, `Notification`;
- for turn ends: `Stop`;
- for questions, plan exits and auth needs, where the provider exposes them;
- for subagent start and stop.

Each line holds the provider, session id, event, timestamp and a short scrubbed detail. It never holds the full
tool input. The hook is fast, never blocks, and always exits 0.

The spool is the authoritative "waiting" signal. A pending permission otherwise looks like `busy` in both
`sessions/<pid>.json` and the transcript. The spool is rotated by size.

### 4.3 States

Exactly one state per session, always shown as text. Priority is listed highest first.

| State | Rule |
|---|---|
| `approval` | A pending tool-permission request (spool, Codex `waitingOnApproval`), not yet resolved |
| `input` | The agent asked the user something (question tool, elicitation, Codex `waitingOnUserInput`) |
| `plan ready` | A proposed plan waits for approval (plan-mode exit) |
| `auth` | The provider needs the user to log in again |
| `working` | Alive and busy, or a transcript write in the last 60 s |
| `background` | Stopped, but its own subagents or monitors are still running and will wake it (not the user's turn) |
| `ready` | Alive and idle, nothing pending; the user's turn. Carries an unread "done" marker until seen |
| `stuck` | Busy with no output for more than `fleet.stuckMinutes` (default 10), excluding sessions in `approval` or with running background tasks |
| `interrupted` | The last turn was stopped partway (Claude `terminal_reason` `aborted_*`, Codex `interrupted`) |
| `failed` | The last turn ended in an error (API error even with `success`, transport error) |
| `limited` | Usage limit reached; shows the reset time when the provider gives one |
| `crashed` | The process is gone but the transcript never closed (no final turn, or it stopped mid-tool-call) |
| `ended` | The process exited and the transcript closed normally |

- **Compaction** shows as activity text ("compacting context"), not as a state.
- **Estimated states:** providers without a status file or hook events get states guessed from transcript activity,
  and those rows are labelled `estimated`.
- **Subagents and teammates:** shown nested under their parent. The parent shows a count of children, and a child
  in `approval` or `input` raises the parent's state.
- **Testing:** each rule is a pure function over the adapter output, tested from fixtures.

### 4.4 Needs you

One list, sorted by state priority, then oldest first. It holds:
1. Sessions in `approval`, `input`, `plan ready` or `auth`, quoting the scrubbed question or reason.
2. Sindri decisions:
   - a won proposal ready to adopt;
   - a profile change waiting for approval;
   - staged proposals ready to publish;
   - a channel build ready to promote (soak done).
3. Failures:
   - sessions in `failed`, `limited`, `stuck` or `crashed`;
   - an index build that failed or is stale;
   - the weekly job ending in `attn`;
   - `doctor` reporting `fail`;
   - an unhealthy judge.
4. PRs in onboarded repos that request the builder's review, or that agents opened and that are ready to merge
   (from `gh`, cached for 2 minutes).
5. Unread bridge messages addressed to the builder.

## 5. Views

The app opens on **Needs you** when it isn't empty, otherwise on **Agents**. Deep links: `/agent/<provider>/<id>`,
`/repo/<name>`, `/job/<id>`.

1. **Needs you:**
   - Each row shows what's needed, which agent or job, the repo, how long it's been waiting, and the reason, with one primary action.
   - Agent rows: **Attach** (§7).
   - CLI commands that need a typed confirmation at a terminal (`evolve adopt`, `channel promote`,
     `profile approve`, `evolve publish`) appear as a copyable command and never run from the browser.
   - Safe commands (reject a proposal, park or unpark, acknowledge a failure) run in place through the CLI.
2. **Agents:**
   - Grouped by repo, then worktree.
   - Each row shows the provider (as text), host, title, state, activity line, turn age, today's tokens and cost
     (from `scorer live`), and a count of children.
   - Expanding a row shows the recent timeline (turns, tool calls, approvals, compactions) as read-only escaped text.
   - Filters: provider, state, repo.
3. **Repos:** per onboarded repo:
   - index age, last build result, files and symbols, commits behind `HEAD`;
   - last observe and backlog size;
   - recent scope runs;
   - open PRs and their review state;
   - worktrees with live agents.
4. **Automatic work:**
   - observe and index runs;
   - the weekly evolve job and its steps;
   - scope runs;
   - the proposal pipeline (proposed → compared → staged → published → merged);
   - channel builds and soak timers;
   - the heavy-job lock holder and queue.
5. **Today:** cost against budget, number of agents and turns, warnings, judge health, `doctor` status, and notifications sent.

**States for every view:**
- **Loading:** skeleton rows on first load only.
- **Empty:** the same sentence the CLI prints.
- **Error:** a banner with the error code and the CLI command that shows more.
- **Stale:** "updated Ns ago". After 3 failed polls: "sindri not responding: run `sindri doctor`".

### 5.1 The "needs you" signal (colour and motion)

"This page needs you" must be noticeable without reading.

- **One attention colour:** a strong amber-to-red accent, checked for contrast in light and dark mode. It is used
  only for needs-you, so it always means one thing.
- **Motion:** when a new item arrives, its row slides in with a pulsing left-edge bar, and the **Needs you** nav item
  and the page header band pulse. The pulse stops once the row has been seen (scrolled into view and focused) or acted on.
- **Urgency:** `approval` and `input` pulse; `failed` and `limited` show the colour without motion; everything else stays calm.
- **Visible from elsewhere:**
  - the tab title becomes `(N) Needs you · Sindri`;
  - the favicon gets an attention dot that blinks once;
  - an optional OS notification fires for `approval` and `input` only;
  - the menu-bar badge switches to the attention colour with a count.
- **Accessibility:**
  - Under `prefers-reduced-motion`, motion becomes a static high-contrast outline plus the text label.
  - State is always written out in words too.
  - One polite `aria-live` region announces changes ("2 now need you").
  - Polling never moves focus, scroll position or form input.
  - Everything is keyboard-reachable in a visible order.

### 5.2 Menu-bar badge

A SwiftBar plugin shows "3 working · 1 needs you", refreshed every 30 s from `sindri fleet --json`. Its dropdown
lists the items that need you, and clicking one opens the dashboard at that item. No action runs from the menu bar.

## 6. Security

- **Binding:** listens on `127.0.0.1` only, on the port set in `dashboard.port`, and refuses any other interface.
  If the port is taken, it exits with an error naming the port and the profile key; it never silently picks another port.
- **Token:** `sindri dashboard --url` prints a URL with a one-time token. The first page load exchanges it for an
  `HttpOnly`, `SameSite=Strict` session cookie and removes it from the URL. The token changes on every restart.
  A 401 response says "run `sindri dashboard --url`".
- **Request checks:**
  - Every request must carry a `Host` of `127.0.0.1:<port>` or `localhost:<port>`.
  - Every action must also carry a matching `Origin` and a CSRF token.
  - No CORS headers are sent.
- **Headers:**
  - CSP `default-src 'self'`, with no inline scripts or styles and no external fonts or CDNs;
  - `frame-ancestors 'none'`;
  - `Referrer-Policy: no-referrer`.
- **Actions:**
  - Only a fixed list of CLI commands runs, with checked arguments and no shell.
  - Commands that need a typed confirmation at a terminal are only shown, never run.
- **Untrusted text:**
  - All agent-written text is scrubbed in the collector and rendered with `textContent` only, never as HTML.
  - Control and invisible characters are shown escaped, using the shared sanitizer (`invisible.ts`, from #82).
- **Remote use:** works unchanged over `ssh -L`.

## 7. Attach

**Rule:** never resume a session that's still running, because two processes writing the same session corrupt it.
- **The session is alive:** bring its host to the front.
- **The session has ended, crashed or been interrupted:** resume it in a new tab.

| Host | Alive | Ended / crashed / interrupted |
|---|---|---|
| T3 Code | Focus T3, opening at the thread if T3 offers a deep link, otherwise showing the thread name and worktree | Same; resuming happens inside T3 |
| Warp / terminal (Claude CLI) | Focus the window that owns the session's terminal, labelled with the tab's tty and title | New Warp tab at the cwd running `claude --resume <id>` |
| Codex | Focus the owning terminal or app | `codex resume <id>` in a new tab |
| Cursor | `cursor <cwd>` focuses the workspace (a specific chat can't be opened directly; the row says so) | Same |
| tmux (Sindri workers, later) | `tmux attach -t <session>` in a new tab | Same |

- **Launching:** the server opens apps through a fixed list of commands (`open -a`, Warp's `warp://` launch URL,
  `cursor`, `tmux`) with checked arguments:
  - ids must match the provider's format;
  - paths must exist inside a known repo or worktree.
- **Uncertain matches:** if the resolver isn't sure which host owns a session, it shows the candidates and the copyable command.
- **Over an ssh tunnel:** attach shows the local command to run instead.

## 8. Profile and CLI

**Profile keys (new):**
- `dashboard.port` (default 7190);
- `dashboard.notify` (OS notifications on/off, default on);
- `fleet.stuckMinutes` (default 10);
- `fleet.sources` (enable or disable each provider adapter).

**CLI (new):**
- `sindri fleet [--json] [--provider P] [--state S]`;
- `sindri dashboard [--url] [--port N]`;
- `sindri fleet hook <provider>` (the `aw:fleet` hook entry point).

**Installer:**
- `scripts/install-sindri.sh` installs the hook for every installed provider and adds the SwiftBar plugin when
  SwiftBar is present;
- the dashboard runs on demand (no launchd job in v1).

## 9. Testing

- **Adapters:** fixture folders for each provider:
  - real-shaped `sessions/*.json`, jsonl transcripts with subagents, Codex rollouts and Cursor `meta.json`;
  - recycled pids, truncated transcripts, and a corrupt file in one provider while the others still render;
  - size and file-count limits.
- **States:** a table-driven test per state from event sequences, including:
  - every `aborted_*` reason;
  - 429 and 529 errors that still report `success`;
  - a pending approval that must not show as `stuck`;
  - background children that keep the parent in `background`.
- **Hook spool:** real-shaped hook payloads for all three providers, run through `aw:fleet` and its adapters.
  The hook never blocks and always exits 0.
- **Server:**
  - wrong `Host`, a missing or wrong `Origin`, a missing CSRF token and a wrong cookie are each refused;
  - the token works once and changes on restart;
  - the CSP header is set;
  - a command outside the allowed list is refused;
  - a command that needs a terminal can only be shown, never run.
- **UI (Playwright):**
  - the `aria-live` announcement, and focus and scroll staying put while polling;
  - the attention pulse appearing, then stopping once the row is seen;
  - `prefers-reduced-motion` showing the static outline instead;
  - keyboard reachability;
  - OSC, ANSI and HTML payloads staying inert;
  - the tab title and favicon.
- **Attach:** resolver tests with a fake launcher, including that a live session is never resumed.
- **End to end:** the PR runs the real `sindri dashboard` against this machine's real sessions. The builder checks
  it visually, and screenshots are kept as evidence.
- **Merge gate:** the AGENTS.md merge gate applies, with 100% coverage on new sindri code.

## 10. Rollout and follow-ons

- **Plan 6** builds everything above. It follows the source-based privacy work being built in parallel and must
  not conflict with it. The collector reads the same profile.
- **The Sindri runner** (rollout step 3a) later adds Sindri-started worker sessions as a sixth source with host
  `tmux`, and their park, unpark and pause actions.
- **Plan 7** (the onboarding guide generator) comes after this. It can reuse the collector for "who is working
  where" and the repo index data.
