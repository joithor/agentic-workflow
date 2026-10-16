# Sindri session collector: every live agent session on the box, its state, its host, and attach

**Status:** design, revision 1 (before /autoplan) · **Date:** 2026-10-16 · **Owner decisions:** 2026-10-16 (§1, CO1
and CO12) · **Builds on:** the dashboard design `2026-10-09-sindri-dashboard-design.md` (DB), whose collector (§4), hook
spool (§4.2), states (§4.3) and attach (§7) this doc rewrites for the C# app; the API surface spec
`2026-10-10-sindri-api-surface-design.md` (API), §3, §4, §5, §9 and §11 ("these need their own spec once the collector
is designed in C#"); the UI screens spec `2026-10-13-sindri-ui-screens-design.md` (UI), §4.5 Agents and phase U11; the
session launcher spec `2026-10-15-sindri-session-launcher-design.md` (LCH), rev 3.1, and `docs/csharp/sindri-launcher.md`
(the `launcher.json` contract); `docs/csharp/meta.md` (aw-meta's lane map); README rows X49, X55 and X59(c) ·
**Lanes:** the ideas lane builds the collector, its hook and its routes; the API lane reviews the API rows and builds
the viewer-backed attach inside or after P10a; the UI lane builds U11a; the engine and context lanes have no work here
(§10) · **Next:** /autoplan once, then build (CO12).

The owner asked (2026-10-16): "when can Sindri show my active Claude Code sessions, behind tmux or not?" The UI's Agents
page (U11) is drawn, but its data source, the collector, has no C# spec: API §11 and README X49 and X59(c) defer it here.
The dashboard design of 2026-10-09 has the goal right ("agents that Sindri didn't start", DB §1) and was written for the
TypeScript `sindri fleet` that was never built. This doc is that collector, rewritten for the Sindri app:

1. **Discovery** of every live agent session on the box, whoever started it: Claude Code from its own session records
   plus the process table, Codex and Cursor from the process table and their hooks, and what each can't show (§4).
2. **State**: `busy`, `idle` or `waiting`, from the agent's own record where it has one and from a per-session state
   file the `aw:fleet` hook writes (§5).
3. **Host and attach**: which tmux server, terminal or app owns the session, and how a click brings it forward, with
   the cases that can't be done said plainly (§6).
4. **API rows** (§7), **UI rows** (§8), the **configuration seams** (§9), **lanes and phases** (§10, §11).

**Short answer to the owner's question** (§11.2): Claude Code sessions on Linux, in tmux or not, show in
`sindri sessions` and `GET /sindri/sessions` after phases CO-0, CO-2 and CO-3, which wait on nothing that isn't on main
today; in the app after U11a. None of it waits on API P10a.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| CO1 | **Build pre-approved** (owner, 2026-10-16): built once this spec is /autoplan-clean, with no separate review round. The PR stays a draft until the owner has read it | The owner asked for the feature directly; the review loop is /autoplan's |
| CO2 | **The collector is a hosted service inside the Sindri process**, not a CLI that shells out (DB §3 amended by A §17.29). It polls every 2 s, keeps an in-memory snapshot, and serves it through queries; it never writes into an agent's own folders | One process, one API; the app reads the API only (UI §4a) |
| CO3 | **Live state is not ledgered.** `GET /sessions` reads the in-memory snapshot (as API P13's `GET /engine` reads Temporal live). The ledger gets one public event type, `session.changed`, for appearances, departures and entering or leaving `waiting` only; `busy` ↔ `idle` flips are never ledgered | A busy/idle flip per turn per session would write thousands of permanent rows a day for ephemeral facts. Appear, gone and waiting are what a client must not miss |
| CO4 | **The code lives in Sindri, copied and fixed from aw-meta, not shared.** `Aw.Sindri.Core/Sessions/` holds the records, pure rules and ports; `Aw.Sindri.Infrastructure/Sessions/` the `/proc`, record, state-file and tmux adapters. aw-meta's `DirectorySessionDirectory` and `ProcProcessTable` stay where they are | Meta is tier 2 and Sindri tier 3; a shared package would be a new tier-1 package with Core, Infrastructure and a compose edge (`deps.yaml`, csharp.md: Infrastructure may use tier-0 packages only) for about 150 lines, one copy of which retires when Sindri absorbs Meta (`docs/csharp/meta.md` "Sindri absorbs it"). Tier 0 is domain-free, so a kernel-level home is out. The copy also fixes a bug: aw-meta treats a record as live when `/proc/<pid>` exists, without checking `procStart`, so a recycled pid passes (CI-9 files the fix for Meta) |
| CO5 | **A session is live only when its process is live and is the same process.** For Claude Code: `/proc/<pid>/stat` field 22 (start time in clock ticks) equals the record's `procStart` (DB §4.1). For a process found without a record: the pair (pid, start ticks) is its identity | Pids are recycled; a stale `sessions/<pid>.json` must not show a ghost |
| CO6 | **The hook writes a state file, not a log.** `aw:fleet` (`config/hooks/fleet-state.sh`) replaces `$AW_FLEET_DIR/<provider>/<session>.json` atomically (write a temp file in the same directory, `mv`) on each mapped event. One file per session, last write wins, no rotation | DB §4.2's append-only spool needed size rotation, and two writers rotating a shared file race. The collector only needs the latest state. A state file is bounded by construction and pruned by one owner (the collector, §5.4) |
| CO7 | **The hook stores no free text.** It writes the event name, the derived state, the tool name for an approval (`Bash`, never `tool_input`), the Claude notification type (never its `message`), the cwd, the transcript path (a path, never its content), and the agent's pid and start ticks. Nothing in the file needs scrubbing | A hook that holds no prompt or tool text can't leak one; DB §4.2's "short scrubbed detail" is dropped |
| CO8 | **Attach only brings a live session forward; it never starts, resumes, types into or stops one.** It focuses the terminal that already shows the session, or, for a tmux session no terminal shows, opens the configured viewer on `tmux attach` (through LCH's `ITerminalViewer`, CO-4b). An ended session offers its resume command to copy, never a server-side resume (DB §7's rule "never resume a live session" kept) | Two processes writing one session corrupt it (DB §7); focusing is all a human needs to reach a live session |
| CO9 | **The collector never changes a tmux server.** Its only tmux commands are `list-sessions`, `list-panes`, `list-clients` and `display-message -p`, always with `-S <absolute socket path>`, always with `TMUX` and `TMUX_PANE` removed from the child's environment, each bounded by a 1 s timeout. No `kill-*`, `send-keys`, `new-session`, `select-*`, `switch-client` or `set-*`, on any socket, the default one included. An architecture test pins the verb list (CI-5) | The team-socket hazard (2026-10-15: a proof that inherited `TMUX` killed the team): reading the default server is safe, changing it is not |
| CO10 | **Attach is `desk`, human only.** `POST /sessions/{provider}/{id}/attach` is refused to `session` callers with `SND-AUTH-002`; it needs no terminal (the app may call it). The query routes are open to every caller, repo-scoped for sessions (API §3.1) | Attach acts on the human's desktop; an agent has no reason to raise windows. Reading the list is harmless and lets a lane see its teammates |
| CO11 | **Linux first.** Discovery, state and attach ship on Linux (`/proc`). macOS gets a `ps`-based process table and its own focus adapters in a later phase (CO-5); until then the collector reports `SND-SESSION-002 unsupported platform` on macOS and the app shows that sentence | aw-meta is Linux only for the same reason; the owner's box is Linux |
| CO12 | **Sessions in a workflow are grouped under it; the rest are "unattached".** A session whose environment carries `AW_SINDRI_LAUNCH` joins its launch (API P10a's `launches` projection) and through it its item and instance (U11's recipe-state groups). Every other session is unattached, shown in its own group, which U11a builds before P10a exists | Answers the owner's question without waiting on P10a, and leaves U11's drawn groups unchanged |
| CO13 | **The agent catalog is the provider list.** A provider is an `AgentId` from LCH's catalog (`claude-code`, `codex`, `cursor`, or an added entry); the entry's `processName` finds its processes and `transcripts` its transcript. An added agent with a `processName` is discovered with state `unknown` | No second list of agents; a teammate's aider shows up from one `launcher.json` entry |

## 2. Terms

- **Session:** one live agent CLI process and its conversation, named `<provider>/<id>`. `provider` is an `AgentId`
  (LCH SL3); `id` is the agent's session id, bounded by LCH L-9's generic pattern
  `^(?!\.)(?!.*\.\.)[A-Za-z0-9._:-]{1,128}$`. A process whose id can't be learned gets `pid-<pid>-<startTicks>`, which
  fits the pattern and is stable for the process's life.
- **Agent process:** the process running the agent CLI (`claude`, `codex`, `cursor-agent`), matched by the catalog's
  `processName` against `/proc/<pid>/comm`, or by the Claude record's pid.
- **State:** `busy` (a turn is running), `waiting` (the agent waits on the human: an approval, a question or a login),
  `idle` (the turn ended; the human's turn), or `unknown` (no signal). `waiting` carries a `reason`: `approval`,
  `input`, `auth`.
- **State source:** where the state came from: `record` (Claude's `sessions/<pid>.json`), `hook` (the state file),
  `transcript` (only the transcript's modification time) or `none`.
- **Host:** what owns and shows the session (§6.1): `tmux` (with its socket), `warp`, `terminal` (with the terminal's
  id from `terminals.json`), `ide`, `ssh`, `t3`, `unknown`.
- **Unattached session:** a live session that isn't a Sindri launch (CO12).
- **Fleet dir:** `$AW_FLEET_DIR`, default `$AW_STATE_DIR/fleet`, where the hook writes state files (§5).
- **Snapshot:** the collector's latest pass: every session, its state and host, `observedAt`, and one `problem` row per
  source that failed.

## 3. What exists today (2026-10-16, origin/main)

| Piece | State | Where |
|---|---|---|
| Claude Code session records | Written by Claude Code itself, one `<pid>.json` per live session, plus a `<pid>.<hash>.key` beside each that the collector must never open. Fields seen in v2.1.301: `pid`, `sessionId`, `cwd`, `startedAt`, `procStart`, `version`, `kind` (`interactive`), `entrypoint` (`cli`), `tmux` (`<session>:@<window>.%<pane>`, with no socket), `name`, `nameSource`, `status` (`busy`; aw-meta also reads `idle`), `statusUpdatedAt`, `updatedAt`, `pidDomain`, `messagingSocketPath`, `bridgeSessionId`, `peerProtocol`, `peerFeatures`. Undocumented and versioned by Claude Code, so read defensively (aw-meta's rule) | `~/.claude/sessions/` |
| aw-meta's reader | `DirectorySessionDirectory` (pid, sessionId, name, status, cwd; live when `/proc/<pid>` exists, no `procStart` check), `ProcProcessTable` (`/proc/<pid>/stat`, `cmdline`), `ProcFormat.Stat` (comm-safe parse), `ProcessScanner` (cwd and open files, `lsof` on macOS) | `src/Meta/Aw.Meta.Infrastructure/Files/` |
| Codex | No per-process record. Transcripts `~/.codex/sessions/YYYY/MM/DD/rollout-<ts>-<uuid>.jsonl`, first line `session_meta` with `id`, `cwd`, `cli_version`, `originator`, `source`. Hooks: `PermissionRequest`, `SessionStart`, `SessionEnd`, `UserPromptSubmit`, `Stop`, `Interrupt` and others, in `~/.codex/hooks.json`, run only after `/hooks` trust | `~/.codex/`; `config/hooks/adapters/README.md` |
| Cursor | `cursor-agent` (the CLI) is a process with hooks (`sessionStart` with `conversation_id`, `beforeSubmitPrompt`, `stop`). The Cursor IDE's agent chats run inside the IDE's one Electron process and have no process of their own. Not installed on the owner's box, so nothing here is verified against a live session | `config/hooks/adapters/README.md` (documented) |
| Session id hook | `session-id-report.sh` writes a Codex or Cursor session id for a Sindri launch or a Meta lane (SL-1b, merged #269) | `config/hooks/session-id-report.sh` |
| Launcher contract | `launcher.json`, `schemas/launcher.schema.json` with `x-aw-keys`, `config/launcher/{builtins,terminals}.json`, the bash reader (SL-0 #257, SL-1 #261 merged). The C# ports (`ISessionHost`, `ITerminalViewer`, SL-2) are not built: API P10a hasn't started | `docs/csharp/sindri-launcher.md` |
| The owner's live layout | Six Claude Code lanes, each in a tmux session on the **default** socket (`/tmp/tmux-1000/default`; the team predates #264's `-L <team>`), started detached by `meta-lane.sh`, each shown in a Warp tab through a `tmux new -A` client. Warp exports `TERM_PROGRAM=WarpTerminal` and `WARP_FOCUS_URL=warp://session/<32 hex>` into each tab's shell. X11 session (`XDG_SESSION_TYPE=x11`) | read-only `ps`, `/proc`, `~/.claude/sessions` |
| API and UI | P1, P2, P3, P4, P5 merged (the stream, `EventCatalog`, the catalog test); P10a, P11 not started. UI U0–U3 merged; Agents is a placeholder page | `src/Sindri/` |

## 4. Discovery

### 4.1 One pass

Every 2 s (`sessions.pollSeconds`, §9) the collector runs one pass with a 500 ms budget. A source that overruns or
throws is reported as a `problem` row for that source and leaves the others intact (DB §4.1 "failures stay contained").

1. **Process table.** Read `/proc/<pid>/stat` for every numeric entry (aw-meta's `ProcFormat.Stat`, rewritten in
   Sindri: name, state, ppid, start ticks), skipping zombies. Keep the parent map for the whole table (host detection
   walks it, §6.1). Only processes of the current user are read further (`/proc/<pid>` owner uid).
2. **Agent processes.** A process is an agent process when its `comm` equals a catalog entry's `processName`
   (`claude`, `codex`, `cursor-agent` for the built-ins) and its parent isn't an agent process of the same entry (a
   CLI's own child processes are not sessions).
3. **Claude records.** Read every `~/.claude/sessions/*.json` (never `*.key`), at most 256 files and 64 KB each. A
   record is live when CO5 holds. Its pid marks that process as identified.
4. **State files.** Read `$AW_FLEET_DIR/<provider>/*.json` (§5); each carries the agent pid and start ticks, which
   identify a Codex or Cursor process the same way a Claude record does.
5. **Environment allowlist.** For each agent process and each terminal ancestor (§6.1), read `/proc/<pid>/environ` and
   keep only these names: `AW_SINDRI_LAUNCH`, `AW_SINDRI_CHILD`, `AW_META_TEAM`, `AW_META_LANE`, `TMUX`, `TMUX_PANE`,
   `TERM_PROGRAM`, `WARP_FOCUS_URL`, `KITTY_WINDOW_ID`, `KITTY_LISTEN_ON`, `WEZTERM_PANE`, `WINDOWID`, `DISPLAY`,
   `WAYLAND_DISPLAY`, `XDG_SESSION_TYPE`. Every other value is dropped as it is parsed and never logged (CI-3's test
   plants a fake token in a fixture `environ` and asserts it reaches no snapshot, log or response).
6. **Unidentified agent processes.** An agent process with no record and no state file is still a session:
   `<provider>/pid-<pid>-<startTicks>`, state `unknown` (or `transcript` source, §5.3), cwd from `/proc/<pid>/cwd`.
7. **Ended sessions.** A session seen in the last pass and absent now moves to a 10-minute "recently ended" list with
   its resume command (CO8) and is then dropped. Nothing persists across a Sindri restart except what the ledger holds
   (`session.changed`).

### 4.2 Per provider

| Provider | Live | Id | cwd, title | State | Honest limits |
|---|---|---|---|---|---|
| Claude Code | `sessions/<pid>.json` + CO5 | the record's `sessionId` | the record's `cwd`; title = the record's `name` (scrubbed, ≤ 80 chars) | record `status` (`busy`/`idle`), overridden by the hook's `waiting` (§5.2) | `status` is undocumented; an unknown value reads `unknown`. A pending permission may read `busy` in the record, which is why the hook exists (DB §4.2) |
| Codex | the process (`comm` `codex`) | the state file's `session_id` (SessionStart hook); else, on Linux, a `rollout-*.jsonl` among `/proc/<pid>/fd` links, its uuid; else `pid-…` | `/proc/<pid>/cwd`; no title (the row shows the repo and the id's first 8 characters) | hook only (§5.2): `PermissionRequest` gives `waiting`; without trusted hooks, `transcript` source | Whether Codex keeps its rollout open between writes is **unverified** (CO-2's first task records it). Hooks run only after `/hooks` trust; untrusted, the row reads "state unknown: trust the aw:* hooks with /hooks in codex" |
| Cursor (CLI) | the process (`comm` `cursor-agent`) | the state file's `session_id` (`conversation_id`, mapped by `cursor.sh`); else `pid-…` | `/proc/<pid>/cwd` | hook: `beforeSubmitPrompt` → `busy`, `stop` → `idle`. **No `waiting`**: Cursor has no hook event for a pending approval or question | Unverified on a live `cursor-agent` (not installed on the owner's box); the fixtures follow the documented payloads |
| Cursor (IDE chats) | **not discoverable** | — | — | — | The IDE runs every chat in one Electron process with no per-chat process or live record; `~/.cursor/chats/*/<id>/meta.json` is history, not liveness. The Agents page says "Cursor IDE chats aren't shown" once, in its empty or footer text |
| Added entries (aider, …) | the process, by `processName` | `pid-…` | `/proc/<pid>/cwd` | `unknown` | No hook, no record. Shown so they are counted |
| T3 Code | Claude records with `entrypoint: sdk-ts` and a cwd under `~/.t3/worktrees` (DB §4.1) | the record's | as Claude | as Claude | Host `t3`; attach can only copy (LCH §7: T3 owns its processes) |

### 4.3 Repo and launch join

- **Repo:** the cwd's main checkout resolved to an approved profile repo, by API §3.1's rule (git common dir, real path,
  longest match). A cwd outside every profile repo has `repo: null`. A worktree resolves to its repo and keeps its
  worktree path as `worktree`.
- **Launch:** `AW_SINDRI_LAUNCH` from the allowlist names the launch; the snapshot carries `launchId`, and the API joins
  it to the `launches` projection when P10a has built it (CA-6).
- **Meta lane:** `AW_META_TEAM` and `AW_META_LANE` give `lane: {team, lane}`, shown as "meta · sindri/ideas". Before
  SL-1b's exports a Claude lane is found by its tmux session name `<team>-<lane>` only when its team config names it;
  the collector reads no Meta state.

### 4.4 macOS (CO-5, later)

No `/proc`. The process table comes from one `ps -A -o pid=,ppid=,uid=,lstart=,comm=` call per pass; the cwd from
`lsof -a -d cwd -p <pids> -Fn` (aw-meta's `ProcessScanner` fallback). The environment of another process can't be read
on macOS without entitlements, so `AW_SINDRI_LAUNCH`, `TERM_PROGRAM` and `WARP_FOCUS_URL` come only from the state file:
on macOS the hook also records those allowlisted names from its own environment (which it inherits from the agent).
Claude's `procStart` format on macOS is unverified; CO-5 compares the record's `startedAt` to `lstart` within 2 s
instead. Focus adapters: `open -a Warp`, AppleScript for Terminal.app and iTerm2 by tty. Until CO-5, `SND-SESSION-002`.

## 5. State: the `aw:fleet` hook

### 5.1 The file

`$AW_FLEET_DIR/<provider>/<sessionId>.json`, mode 0600, directories 0700, contract `schemas/fleet-state.schema.json`
(2020-12; both the bash writer's tests and the C# reader run every fixture in `tests/fixtures/fleet/` against it):

```jsonc
{
  "schema": 1,
  "provider": "claude-code",            // AgentId
  "sessionId": "3970893f-…",            // checked against L-9's generic pattern before the write
  "event": "PermissionRequest",         // the provider's event name, as received
  "state": "waiting",                    // busy | waiting | idle | ended
  "reason": "approval",                  // waiting only: approval | input | auth
  "tool": "Bash",                        // approval only: the tool name (≤ 64 chars, [A-Za-z0-9_.:-]); never tool_input
  "at": "2026-10-16T11:02:03.123Z",
  "cwd": "/home/…/repo",
  "transcript": "/home/…/x.jsonl",       // a path, or null
  "agentPid": 3023421, "agentStartTicks": 13279367,   // Linux; null where unknown
  "env": { "TERM_PROGRAM": "WarpTerminal" }           // macOS only (§4.4): the allowlisted names; {} on Linux
}
```

### 5.2 Events per provider

| Provider | Event (as installed) | `state` | Notes |
|---|---|---|---|
| Claude Code | `SessionStart` | `idle` | |
| | `UserPromptSubmit` | `busy` | |
| | `PermissionRequest` | `waiting` `approval` | with `tool` |
| | `Notification` with `notification_type` `permission_prompt` / `elicitation_dialog` / `idle_prompt` | `waiting` `approval` / `waiting` `input` / `idle` | other types are ignored (no write) |
| | `Stop` | `idle` | |
| | `SessionEnd` | `ended` | |
| Codex | `SessionStart`, `UserPromptSubmit`, `PermissionRequest`, `Stop`, `Interrupt`, `SessionEnd` | `idle`, `busy`, `waiting approval`, `idle`, `idle`, `ended` | through `adapters/codex.sh`; needs `/hooks` trust |
| Cursor | `sessionStart`, `beforeSubmitPrompt`, `stop` | `idle`, `busy`, `idle` | through `adapters/cursor.sh`, which prints Cursor's required JSON; no waiting state exists |

Which of Claude's `PermissionRequest` and `Notification` fields arrive in the installed version is checked first with
`scripts/probe.sh` (CO-1's first task); the hook takes either.

**Merge rule** (pure, `SessionStateRules.Merge`, CI-2): the newer of the record's `statusUpdatedAt` and the file's `at`
wins, except that a hook `waiting` stays until a later hook event, a later record status, or the transcript's
modification time passing `at + 1 s` (the tool ran, so the approval was given). The transcript is only `stat`ed, never
read. `ended` in a file whose process is still live reads `unknown`.

### 5.3 Without a hook

A session with no state file and no record status gets `source: transcript`: `busy` when the transcript (from the
state file, the record, or the catalog's `transcripts` glob with the id) changed in the last 60 s, else `idle`, and the
row says `estimated` (DB §4.3). With no transcript either: `unknown`.

### 5.4 The hook's rules (`config/hooks/fleet-state.sh`, id `aw:fleet`)

- Reads the payload from stdin with `jq`, maps the event (§5.2), writes the file by temp-and-rename in the same
  directory, prints nothing (the Cursor adapter prints Cursor's JSON), and **always exits 0**. Bounded: no network, no
  CLI calls, one `jq` run and one `mv`; the hook test fails over 100 ms on the CI box.
- `agentPid`: the hook walks up from `$PPID` through `/proc/<pid>/stat` to the first ancestor whose name is the
  provider's process name (at most 8 steps), and records its start ticks. Not found: `null`.
- It is **advisory** (no allow or deny), so it doesn't call `failopen` (`.agents/rules/hooks.md`: advisory hooks don't
  log). A payload it can't read, an id that fails the pattern, or an unwritable directory is a silent exit 0.
- It writes nothing when `AW_FLEET_DISABLE=1` (for a session the human wants unseen).
- **Pruning** is the collector's, never the hook's: each pass deletes a state file whose session is not live and
  whose `at` is older than 24 h, and caps each provider directory at 512 files (oldest first among dead sessions).
- Installed by `scripts/install-fleet.sh --provider claude|codex|cursor` (the lever pattern, `install-lib.sh`), and by
  `./setup.sh --with-fleet` (opt-in, like `--with-meta`; Q1). Codex prints the `/hooks` trust line after install.

## 6. Host detection and attach

### 6.1 Host detection

Walk the parent chain of the agent process (the table from §4.1 step 1), at most 32 steps:

| Found first | Host | Detail kept |
|---|---|---|
| a `tmux: server` process | `tmux` | `socket`: the server's listening socket path, from `/proc/<server>/fd` socket inodes matched in `/proc/net/unix` (no tmux call); `kind`: `sindri` when the path ends in `/sindri`, `team` when it matches a configured team socket, `default` for `/default`, else `other`. Then `tmux -S <socket> list-panes -a -F '#{pane_pid} #{session_name} #{window_index} #{pane_index} #{pane_id}'` (once per socket per 10 s) gives the pane holding the agent's ancestor, and `list-clients -F '#{client_pid} #{client_tty} #{client_session}'` the clients showing that session. Each client's own chain is walked for its terminal |
| `sshd` | `ssh` | none: a remote session can't be focused from here |
| a process whose `comm` is a `terminals.json` binary (kitty, wezterm-gui, gnome-terminal-server, alacritty, konsole, foot, xterm) | `terminal` | `terminal` id, the terminal process pid, `WINDOWID` / `KITTY_WINDOW_ID` / `WEZTERM_PANE` from the shell under it |
| `warp` or `warp-terminal`, or `TERM_PROGRAM=WarpTerminal` on the shell under it | `warp` | `WARP_FOCUS_URL` from that shell, when it matches `^warp://session/[0-9a-f]{32}$` |
| `code`, `cursor` (an IDE's integrated terminal) | `ide` | the IDE id |
| a Claude record with `entrypoint: sdk-ts` under `~/.t3/` | `t3` | — |
| none of these | `unknown` | — |

The Claude record's own `tmux` field (`<session>:@<w>.%<p>`) is cross-checked against `list-panes` and, when the server
can't be reached, used alone with `socket: null`.

### 6.2 Attach

`POST /sessions/{provider}/{id}/attach` (CA-4) appends `session.attach-requested` and returns; the collector's attach
worker performs it after the commit, within 5 s, and appends `session.attach-finished {outcome, method, code?, copy?}`
(the `OpenItem` pattern, API §8.1: the command decides, a service acts). The method is the first that applies:

| Host | Method | Needs | Outcome when it can't |
|---|---|---|---|
| `tmux`, a client attached | focus the client's terminal by the `terminal`/`warp` rows below | as below | `copy`: `tmux -S <socket> attach -t '=<session>'` |
| `tmux`, no client attached | open the configured viewer (LCH `ITerminalViewer.Open`) on `tmux -S <socket> attach -t '=<session>'` | SL-2 (CO-4b) | before SL-2: `copy` with that command |
| `warp` | `xdg-open <WARP_FOCUS_URL>` | the CO-S spike confirms Warp focuses that session's tab | `copy` "Warp tab `<title>`" plus the cwd |
| `terminal` kitty | `kitten @ --to <KITTY_LISTEN_ON> focus-window --match id:<KITTY_WINDOW_ID>` | kitty's remote control on | `x11` row |
| `terminal` wezterm | `wezterm cli activate-pane --pane-id <WEZTERM_PANE>` | — | `x11` row |
| any terminal, X11 | `wmctrl -i -a <window>`, the window found by `wmctrl -lp` matching the terminal pid (or `WINDOWID`) | `wmctrl` on PATH, an X11 session | `copy` |
| `ide`, `t3`, `ssh`, `unknown` | — | — | `copy` with what is known (cwd, tty, IDE name) |

**What is impossible, said plainly** (the row and the outcome both say it, never a silent no-op):
- **A specific tab in a single-process terminal** (GNOME Terminal, Konsole) by pid: they host every tab in one server
  process with one pid; `wmctrl` raises the window, not the tab. The outcome is `focused-window` with "the session is
  in one of this window's tabs".
- **A specific Warp tab without `WARP_FOCUS_URL`** (an older Warp, or a session not started in a Warp shell): Warp has
  no other external API for it; only the app comes forward.
- **Any window on Wayland** without the terminal's own remote control: GNOME and KDE refuse focus requests from another
  process without an activation token, and `wmctrl`/`xdotool` don't work there. Wayland gets the kitty, wezterm and Warp
  URL methods only; everything else is `copy`.
- **A Cursor IDE chat**: no deep link opens a chat (DB §7); `cursor <cwd>` would focus the workspace, which v1 doesn't do
  (copy only).
- **A session over ssh, or on another machine**: copy only.
- **A T3 Code thread**: T3 owns it (LCH §7); copy only until the SL-T spike says otherwise.
- **Focusing without changing tmux**: when no client shows the session, attaching adds a client, which can resize the
  session's windows for its other clients. The collector never selects a window or pane (CO9); the row shows
  `window.pane` so the human can find it.

Every command an attach runs is an argv (no shell), from a fixed list per method; its inputs are checked first: the
socket path is absolute, owned by the user and under `$TMUX_TMPDIR` or `/tmp/tmux-<uid>/`; the session name is one
`list-sessions` just returned; ids match the patterns above; `WARP_FOCUS_URL` matches its pattern. The child
environment drops `TMUX` and `TMUX_PANE`.

## 7. API rows (the API lane reviews; the ideas lane builds CA-1…CA-5)

Routes sit under `/sindri`. Classes and callers follow API §4.

| # | Row | Detail | Phase |
|---|---|---|---|
| CA-1 | `GET /sessions?provider=&state=&repo=&attached=` → `ListSessions` | `SessionList(sessions, recentlyEnded, problems, empty, asOf)`. A `SessionRow`: `ref` (`<provider>/<id>`), `provider`, `providerLabel`, `id`, `idKnown`, `state`, `waiting?{reason, tool?, since}`, `stateSource`, `estimated`, `title?` (scrubbed), `repo?`, `worktree?`, `host{kind, label, socketKind?, session?, pane?, attachedClients?, terminal?}`, `lane?{team, lane}`, `launchId?`, `instance?` and `item?` (CA-6), `startedAt`, `since` (the current state's), `pid`, `attach{method, possible, why?}` (the method §6.2 would use now, and why not). `recentlyEnded[]` rows add `endedAt` and `resume` (argv from the catalog's `resume` template, null when the entry has none). `problems[]`: `{source, code, text}` per failed source. `empty`: "No agent sessions are running." Query: any caller; `session` callers see only rows of their `X-Sindri-Repo`. Never in a row: the transcript path, the cwd outside the repo (`worktree` is repo-relative), the environment, `messagingSocketPath`, `bridgeSessionId`, the socket path for `session` callers | CO-3 |
| CA-2 | `GET /sessions/{provider}/{id}` → `GetSession` | `SessionView`: the row plus `cwd` (human callers only), `host.socket` (human only), `stateHistory[]` (the last 20 state changes of this Sindri run, in memory: `{state, reason?, at, source}`), `asOf`. Unknown ref: `SND-SESSION-404` | CO-3 |
| CA-3 | `session.changed` event | Payload `{provider, sessionId, change: appeared \| waiting \| resumed \| gone, state, reason?, repo?, host: kind, launchId?}`, no text. Public. Key `session.changed:<provider>:<id>:<change>:<observedAtMs>`. Written by `service:collector` on those four changes only (CO3), at most one `waiting` per session per 10 s. An `EventCatalog` row and a row in API §3.4's table | CO-3 |
| CA-4 | `POST /sessions/{provider}/{id}/attach` → `AttachSession` | Body `{expectStartedAt}` (the row's `startedAt`, so a recycled id is refused with `SND-SESSION-409`); response `AttachRequested(accepted, attachId)`; class `desk`, callers `human` (terminal not needed), `session` → `SND-AUTH-002`. Appends `session.attach-requested {attachId, ref}` (not public). The worker appends `session.attach-finished {attachId, outcome: focused \| focused-window \| opened-viewer \| copy \| failed, method, code?, copy?: argv}` (public without `copy`). CLI `sindri sessions attach <provider>/<id> [--json]`, which prints the copy line on `copy` and exits 1 | CO-4a |
| CA-5 | Codes (`SindriErrors`, `docs/csharp/sindri.md`) | `SND-SESSION-404` NotFound: no such live session (or outside a session caller's repo). `SND-SESSION-409` Conflict: the session at that ref is a different process. `SND-SESSION-001` Unavailable: attach found no method (`detail`: host kind, reason; `copy` extension member). `SND-SESSION-002` Unavailable: the collector can't run here (`unsupported-platform`, `proc-unreadable`) | CO-3, CO-4a |
| CA-6 | The launch join | Once P10a's `launches` projection exists, `ListSessions` fills `item` and `instance` (address, recipe, state) for rows with a `launchId`, and the session drops out of `attached=false` | P10a + CO-3 (API lane, inside P10a or right after) |
| CA-7 | Catalog and goldens | `ListSessions`, `GetSession` (queries), `AttachSession` (desk) rows in `commands/catalog.json`; goldens in `tests/Aw.Sindri.Tests/Golden/Api/sessions/` (`empty`, `fixture-three-providers`, `session-scoped`, `problems-proc-unreadable`, `recently-ended`), `sessions-id/` (`claude-tmux`, `codex-hook`, `not-found`, `session-caller-no-cwd`), `sessions-attach/` (`accepted`, `refused-session`, `refused-origin`, `stale-started-at`, `not-found`). The gate runs `Aw.Sindri.Ui.Tests` (it pins the catalog) | CO-3, CO-4a |
| CA-8 | `GET /status` gains `sessions{live, waiting, problems}` (additive) | For the tray and the top bar's "Agents N" count without a second call | CO-3 |

Telemetry: span `sindri.collector.pass` (sources, sessions, duration), metric `sindri.sessions` (gauge by provider and
state), `sindri.collector.overrun` (counter). Logs carry refs and codes only.

## 8. UI rows (the UI lane)

| # | Row | Phase |
|---|---|---|
| CU-1 | **U11a: Agents, unattached group.** The Agents page (UI §4.5) ships with one group, "Not in a workflow", before U11's recipe groups exist: one row per live session, `● provider · title or id8 · repo/worktree · state word (+ reason) · since · host label · [Attach]`. Waiting rows use the attention colour with the word ("waiting · approval · Bash"). Estimated states say `estimated`. Rows from a Meta lane read "meta · sindri/ideas". `recentlyEnded` sits under a fold with the resume command to copy. `problems` render as one line each with the code. Filters: provider, state, repo. The tab count is `sessions.live` from `GET /status`. Refresh: on `session.changed`, and every 5 s while the page is visible (busy/idle flips aren't events, CO3). Empty: the API's `empty` sentence plus "Cursor IDE chats aren't shown" | U11a |
| CU-2 | **Attach** on a row calls CA-4 and shows the outcome in the row's live region: "Brought Warp forward", "Raised the terminal window: the session is in one of its tabs", "Opened <label> on the tmux session", or the copy line with a copy button ("Can't focus on Wayland: run `<copy>`"). Focus rules as LCH L-7 (focus moves to the copy button only if it is still on Attach) | U11a |
| CU-3 | **U11's recipe groups** take launched sessions (CA-6) out of "Not in a workflow" into their recipe state; the group stays for everything else | U11 |
| CU-4 | **Tray:** the tray tooltip adds "N agents · M waiting", from `GET /status`'s `sessions`. The badge itself stays the needs-you count (UI X5, R8-13) unless the owner answers Q3 yes | U11a |
| CU-5 | **Home** (only if the owner answers Q3 yes): a waiting session becomes a needs-you row of kind `session` (`primaryAction` `AttachSession`), urgency below decisions | CO-6 |

## 9. Configuration seams

Box facts go in `launcher.json` (LCH SL2: machine-wide, no approval), behaviour that a shared profile should fix goes in
the profile.

| Where | Key | Default | Meaning |
|---|---|---|---|
| `launcher.json` | `sessions.providers` | every catalog entry with a `processName` | which agents to discover (`["claude-code", "codex"]`) |
| `launcher.json` | `sessions.tmuxSockets` | `[]` | extra socket paths to read besides the ones found from processes (a server whose agents' ancestry is hidden, e.g. by `setsid`) |
| `launcher.json` | `sessions.claudeSessionsDir` | `~/.claude/sessions` | for a relocated Claude config (`CLAUDE_CONFIG_DIR`) |
| `config/launcher/terminals.json` | per terminal `focus` | absent | the focus argv template (kitty, wezterm rows of §6.2), data like the open templates; placeholders `{windowId}`, `{paneId}`, `{listenOn}` added to the closed set for `focus` only |
| profile | `sessions.pollSeconds` | 2 | pass cadence (1–30) |
| profile | `sessions.enabled` | true | the whole collector |
| env | `AW_FLEET_DIR` | `$AW_STATE_DIR/fleet` | the state files (tests, the prover) |
| env | `AW_FLEET_DISABLE` | unset | the hook writes nothing in this session |
| env | `AW_SESSIONS_PROC_ROOT` | `/proc` | tests and the prover's fixture tree only; doctor warns when set |
| env | `AW_SESSIONS_CLAUDE_DIR` | unset | overrides `sessions.claudeSessionsDir` (tests, prover) |

The `sessions` key is new in `launcher.json` schema 1 (additive, optional). The bash reader refuses unknown keys, so
CI-1 adds it to `x-aw-keys` and the bash reader accepts and ignores it in the same PR. `.env.example` gains the four
variables.

## 10. Rows per lane

### 10.1 Ideas lane (the collector, the hook, the contract)

| # | Row |
|---|---|
| CI-1 | Contract files: `schemas/fleet-state.schema.json`; `tests/fixtures/fleet/` (valid and invalid state files, one per §5.2 event, with a `manifest.json` of verdicts); `tests/fixtures/sessions/` (a `/proc`-shaped tree: Claude, Codex, Cursor and aider processes, a tmux server with its `/proc/net/unix` line, a Warp shell, a kitty shell, an sshd chain, a recycled pid whose `procStart` differs, a zombie, an unreadable entry; a `claude-sessions/` dir with matching and stale records and a `.key` file whose read fails the test); the `sessions` key in `launcher.schema.json` and `x-aw-keys`, accepted by the bash reader; `focus` in `terminals.json`; `.env.example`; `deps.yaml` file edges (Hooks → `fleet` dir; Sindri → `fleet` dir, `~/.claude/sessions`, `launcher.json` already); `docs/csharp/sindri-sessions.md` (new) |
| CI-2 | Core: `Aw.Sindri.Core/Sessions/` with `SessionRef`, `ObservedSession`, `SessionState`, `WaitingReason`, `HostInfo`, `StateSource`; pure rules `SessionStateRules.Merge` (§5.2), `HostRules.Detect` (§6.1, over a process-table record), `AttachRules.Choose` (§6.2), `SessionLiveness.Matches` (CO5); ports `IProcessTable`, `IClaudeSessionRecords`, `IFleetStates`, `ITmuxServers`, `ISessionSnapshot`, `ISessionFocus` |
| CI-3 | Infrastructure: `Aw.Sindri.Infrastructure/Sessions/` with `ProcProcessTable` (stat, comm, environ allowlist, cwd, fd links, `net/unix`; root from `AW_SESSIONS_PROC_ROOT`), `ClaudeSessionRecords`, `FleetStateFiles` (read and prune), `TmuxCli` (the four verbs only, CO9) |
| CI-4 | The hosted service `SessionCollector` (`Aw.Sindri.Features/Sessions/`, registered in `AddSindriFeatures`): the pass, the budget, `problems`, the snapshot, `session.changed` writes through the command chain's service caller |
| CI-5 | Architecture test `ARCH-SESS-001`: no type under `Aw.Sindri.Infrastructure.Sessions` builds a tmux argv outside `TmuxCli`, and `TmuxCli`'s verbs are exactly `list-sessions`, `list-panes`, `list-clients`, `display-message`; a unit test asserts every `TmuxCli` call carries `-S` and an environment without `TMUX`, `TMUX_PANE` |
| CI-6 | The hook `config/hooks/fleet-state.sh` (§5.4) and `scripts/install-fleet.sh`; mapping rows in `config/hooks/adapters/README.md`; the hook in `.agents/rules/hooks.md`'s table |
| CI-7 | Attach worker `SessionAttacher` (`Features/Sessions/`) and `ISessionFocus` adapters (`Infrastructure/Sessions/Focus/`: `WarpUrlFocus`, `KittyFocus`, `WeztermFocus`, `X11WindowFocus`), each an argv runner with a 3 s timeout |
| CI-8 | `sindri sessions [--provider P] [--state S] [--repo R] [--json]`, `sindri sessions show <ref> [--json]`, `sindri sessions attach <ref> [--json]` (Host/Commands), reading the routes (they never start the server, API §7: exit 1 `SND-API-001` when it is down) |
| CI-9 | aw-meta fix, filed separately: `DirectorySessionDirectory` checks `procStart` against `/proc/<pid>/stat` (CO4). Not part of the collector's phases; XS |

### 10.2 API lane

CA-1…CA-8 (§7): the lane reviews CA-1…CA-5, CA-7 and CA-8 in the ideas lane's PRs (shapes, classes, catalog) and builds
CA-6 inside P10a or right after it. CO-4b's viewer-backed attach uses SL-2's `ITerminalViewer` and is the API lane's.

### 10.3 UI lane

CU-1…CU-5 (§8): U11a (CU-1, CU-2, CU-4) is a new phase before U11; U11 takes CU-3; CU-5 waits on Q3.

### 10.4 Engine and context lanes

No rows. The engine's agent runtime (P8) already launches through `ILauncher`, so its sessions carry
`AW_SINDRI_LAUNCH` and join through CA-6 with no engine change. The context lane is not involved (repo resolution is
API §3.1's, already in Sindri).

## 11. Phases

Each phase is one PR, driven by /specToProvenPR in its lane, built red first (the slice recipe). Sizes as the scoping
spec: S a few files, M one subsystem slice, L more. **(owner)** steps run on the owner's box. Every proof that starts
tmux uses a private socket `tmux -L aw-co-proof-$$` with `TMUX` unset, and stops only that socket; no proof touches the
default socket or `-L sindri` except read-only.

| Phase | What | Files | Size | Needs on main | Tests | Proof | Lane |
|---|---|---|---|---|---|---|---|
| **CO-0** | CI-1: the contract files, fixtures, schema key, docs page | `schemas/fleet-state.schema.json`, `schemas/launcher.schema.json`, `config/launcher/terminals.json`, `scripts/launcher-lib.sh`, `tests/fixtures/{fleet,sessions,launcher}/`, `.env.example`, `deps.yaml`, `docs/csharp/sindri-sessions.md`, `docs/csharp/sindri-launcher.md` | S | nothing (SL-0, SL-1 merged) | `node scripts/check-launcher.mjs` passes with a new fixture using `sessions` (valid) and one with `sessions.bogus` (invalid); a new `scripts/check-fleet.mjs` validates every `tests/fixtures/fleet/` file against its manifest verdict (Ajv2020); `meta-launch.test.sh` accepts a `launcher.json` with `sessions`; `check-deps`, `check-env-example` pass | the checks above in CI | ideas |
| **CO-2** | CI-2, CI-3, CI-4, CI-5: discovery, state from records and transcripts (no hook yet), host detection, the snapshot, `session.changed` | `src/Sindri/Aw.Sindri.Core/Sessions/*`, `…Infrastructure/Sessions/*`, `…Features/Sessions/SessionCollector.cs`, `SindriErrors.cs` (`-002`), `EventCatalog.cs`, `tests/Aw.Sindri.Tests/Sessions/*`, `tests/Aw.Architecture.Tests/Sessions*` | M | API P1, P2, P5 (merged) | Unit: `SessionLiveness_rejects_a_recycled_pid`, `Merge_keeps_waiting_until_the_transcript_moves`, `Merge_newer_record_status_wins`, `HostRules_finds_the_tmux_server_and_its_socket`, `HostRules_finds_warp_through_a_tmux_client`, `HostRules_reports_ssh`, `Environ_drops_every_name_off_the_allowlist` (planted token), `Pass_reports_a_failing_source_as_a_problem_and_keeps_the_others`, `Key_files_are_never_opened`; integration (the fixture proc tree): the snapshot over `tests/fixtures/sessions/` equals `golden/snapshot.json`; `ARCH-SESS-001` | `sindri-prove.sh` home with `AW_SESSIONS_PROC_ROOT` unset (the real `/proc`) and the real `~/.claude/sessions`: `sindri doctor` gains a `sessions` line listing the six live lanes with host `tmux` socket `default` and Warp clients; then a private-socket tmux pane running a stub `claude` (a `sleep` exec'd as `claude`) with a fixture record in `AW_SESSIONS_CLAUDE_DIR` appears within 2 s, and after it exits, `gone` is in `sindri ledger --type session.changed`; the record kept after exit never shows | ideas |
| **CO-3** | CA-1, CA-2, CA-3, CA-5 (`-404`, `-002`), CA-7 (query rows), CA-8, CI-8 (`sessions`, `sessions show`): the routes and the CLI. **After this phase the owner's question is answered on the CLI and the API** | `…Features/{ListSessions,GetSession}/`, `Host/Endpoints/EndpointTable.cs`, `Host/Commands/{CommandTable,Verbs,Usage}.cs`, `Host/SindriJsonContext.cs`, `Host/CommandCatalog.cs`, `GetStatus`, goldens, `docs/csharp/sindri.md`, `docs/csharp/sindri-sessions.md` | M | CO-2 | Slice tests per route (red first); the goldens of CA-7; `ListSessions_scopes_a_session_caller_to_its_repo_and_hides_paths`; `GetSession_hides_cwd_from_a_session_caller`; catalog test; `Aw.Sindri.Ui.Tests` in the gate | `sindri-prove.sh human sessions` lists the six lanes with state, repo and host; `sindri-prove.sh curl GET /sindri/sessions \| jq` equals `sessions --json`; with no caller header and `X-Sindri-Repo: other` the list is empty; `GET /sindri/status` has `sessions.live = 6` | ideas |
| **CO-1** | CI-6: the `aw:fleet` hook and its installer; `waiting` | `config/hooks/fleet-state.sh`, `scripts/install-fleet.sh`, `setup.sh` (`--with-fleet`), `config/hooks/tests/fleet-state.test.sh`, `config/hooks/adapters/README.md`, `.agents/rules/hooks.md`, `AGENTS.md` (commands, bash test list), `scripts/ci/bash-tests.sh` | S | CO-0 (schema). First task: record real `PermissionRequest` and `Notification` payloads with `scripts/probe.sh on` into the fixtures | Under a temp `HOME`: each provider's real-shaped payload (Claude direct; Codex and Cursor through their adapters) writes a file valid against the schema with the §5.2 state; `tool_input` and notification `message` never appear in the file; an invalid id, a bad payload, an unwritable dir each exit 0 with no file and no stdout (Cursor: the adapter's valid JSON); runs under 100 ms; `AW_FLEET_DISABLE=1` writes nothing; `install-fleet.sh --provider codex` prints the trust line; `./setup.sh --with-fleet --dry-run` prints the installs | **(owner)** after `scripts/install-fleet.sh --provider claude`, a Claude session in default permission mode asking to run `ls` shows `waiting · approval · Bash` in `sindri sessions` within 2 s, and `busy` within 2 s of approving; `session.changed` `waiting` then `resumed` in the ledger | ideas |
| **CO-S** (owner) | Spike: does `xdg-open "$WARP_FOCUS_URL"` focus that Warp tab on Linux (X11)? Does `wmctrl -lp` find Warp's and GNOME Terminal's windows by pid? Does Codex hold its rollout file open (§4.2)? | scratch dir outside the repo; findings into §13 | S | none | — | **(owner)** each answer recorded with the command and the Warp/Codex version; a "no" turns that method into `copy` in CO-4a | ideas |
| **CO-4a** | CA-4, CA-5 (`-001`, `-409`), CA-7 (attach rows), CI-7, CI-8 (`sessions attach`): attach by focus | `…Features/AttachSession/`, `…Features/Sessions/SessionAttacher.cs`, `…Infrastructure/Sessions/Focus/*`, Host tables, goldens | M | CO-3, CO-S | `AttachRules_picks_warp_url_then_x11_then_copy`, `Attach_refuses_a_session_caller`, `Attach_refuses_a_recycled_session` (`-409`), `Focus_argv_never_contains_a_shell`, `Socket_outside_the_user_tmux_dir_is_refused`; a fake `ISessionFocus` records argv; goldens | `sindri-prove.sh human sessions attach claude-code/<a lane's id>` brings that lane's Warp tab forward (or, if CO-S said no, prints the copy line and exits 1); on the private-socket stub with no client it prints `tmux -S … attach -t '=…'`; a `curl` with no caller header gets `SND-AUTH-002` | ideas |
| **U11a** | CU-1, CU-2, CU-4: Agents with the "Not in a workflow" group, Attach, the tray tooltip | `src/Sindri/Aw.Sindri.Ui/Views/Agents*`, `ViewModels/Agents*`, `Api/ISindriApi.cs` (two methods), `Views/Shell/ShellWindow.axaml` (the page), `Views/AppTrayIcons.cs`, `scripts/sindri-ui-fixture.sh` (a sessions section), `docs/csharp/sindri-ui.md` | M | CO-3 goldens (CO-4a's for Attach; without them Attach renders `pending:CO-4a` from the catalog) | View-model tests on the fake `ISindriApi` over CO-3's goldens: grouping, the waiting word and colour token, estimated label, problems lines, empty sentence, `session.changed` refresh, 5 s poll only while visible; headless frames for the page, a waiting row, the copy outcome; focus stays put on refresh | **(owner)** `sindri` opens the app; Agents shows the six lanes with host "tmux · Warp"; a session started by hand in a plain terminal appears within 2 s; Attach on a lane brings its Warp tab forward or shows the copy line | ui |
| **CO-4b** | Viewer-backed attach: a tmux session with no client opens the configured viewer (`ITerminalViewer`) | `…Features/Sessions/SessionAttacher.cs` (the `opened-viewer` branch) | S | SL-2 (in P10a), CO-4a | `Attach_opens_the_viewer_when_no_client_shows_the_session` on the fake viewer | `sindri-prove.sh human sessions attach` on the private-socket stub opens an `exec-template` xterm under `xvfb-run` with one client attached, outcome `opened-viewer` | api |
| **CA-6** | The launch join (§7) | `ListSessions` read, the `launches` projection | S | P10a, CO-3 | `ListSessions_joins_a_launched_session_to_its_item` | `sindri-prove.sh human open ITM-…`; the session's row carries `item` and leaves `attached=false` | api |
| **U11** | CU-3 in UI's U11 as specced | as UI §8 | M | U8, P10a, E-28, CA-6 | as UI §8 | as UI §8 | ui |
| **CO-5** | macOS (§4.4) | `…Infrastructure/Sessions/{PsProcessTable,MacFocus}.cs`, the hook's `env` block | M | CO-4a | the same rule tests over a `ps` fixture | **(owner, on a Mac)** the CO-3 and CO-4a proofs | ideas |
| **CO-6** | CU-5 and the needs-you `session` kind, if Q3 is yes | API P4's projection, `GetNeedsYou`, Home | S | CO-1, CO-3, the owner's answer | `NeedsYou_lists_a_waiting_session_and_clears_it_on_resumed` | a waiting session shows on Home's Needs you and clears when approved | api, ui |

### 11.1 Parallelization plan

```
now ──► CO-0 ──► CO-2 ──► CO-3 ──┬──► CO-1 ──────────────► CO-6 (if Q3)
                                  ├──► CO-4a (needs CO-S) ──► CO-5
now ──► CO-S (owner) ────────────┘        │
                                  CO-3 goldens ──► U11a (ui)
P10a (with SL-2) ──► CO-4b, CA-6 (api) ──► U11 (ui, with U8, E-28)
```

| Wave | Ideas lane (one builder) | API lane | UI lane | Owner |
|---|---|---|---|---|
| 0 | CO-0 | reviews CA rows in CO-0's docs | — | CO-S spike |
| 1 | CO-2 | — | — | — |
| 2 | CO-3 | reviews CO-3 | reads CO-3's goldens into U11a's plan | — |
| 3 | CO-1, then CO-4a | reviews CO-4a | U11a | CO-1's proof |
| 4 | CO-5 (when a Mac is free) | P10a with SL-2 → CO-4b, CA-6 | U11 (after U8) | answers Q3 → CO-6 |

- The ideas lane runs one builder at a time; CO-1 touches only bash and docs, so the meta may give it to a second
  builder beside CO-2 when a heavy slot is free (it needs no dotnet build).
- Nothing in waves 0–3 waits on P10a, P11 or U8. Hotspots: `SindriErrors.cs`, `EventCatalog.cs`, the endpoint and
  command tables, `SindriJsonContext`, `commands/catalog.json`, `ISindriApi.cs`, `ShellWindow.axaml`; each phase
  appends at the anchors (API "Shared files"). CO-3, CO-4a and U11a change catalog goldens, so their gates run
  `Aw.Sindri.Ui.Tests`.

### 11.2 When Sindri shows your sessions

| What | Phase | Waits on |
|---|---|---|
| Claude Code sessions, tmux or not, with busy/idle and host, in `sindri sessions` and `GET /sindri/sessions` (Linux) | CO-3 | CO-0, CO-2; nothing outside the ideas lane |
| …with "waiting on approval" | CO-1 | CO-0 |
| …in the app's Agents page, with Attach | U11a | CO-3 (CO-4a for Attach) |
| Codex and `cursor-agent` sessions | CO-2/CO-3 for the process; CO-1 for ids and states from hooks | Codex `/hooks` trust on the box |
| Sindri-launched sessions grouped under their workflow | CA-6, U11 | P10a, U8 |
| macOS | CO-5 | CO-4a |

## 12. Privacy and threat model

The threat model is the repo's: honest mistakes, not a hostile agent (README, `docs/csharp/bugfix.md` "Threat model").
The rules exist so nothing private leaks by accident:

- **No transcript or prompt text** enters the collector: transcripts are only `stat`ed (§5.2, §5.3), the hook stores
  no free text (CO7), and `capture-pane` (X55, X59(c)'s live preview) is **not** in this spec; it would put screen text
  into the API and needs its own decision (Q4).
- **Titles** (Claude's `name`) pass through `Aw.Redaction` (`Redactor.Default.Scrub`, `docs/csharp/redaction.md`) and
  are capped at 80 characters before they reach the snapshot; `Outcome: Blocked` drops the title.
- **Environment:** the allowlist (§4.1 step 5) is applied while parsing; nothing else from `environ` is kept, logged or
  returned. `/proc/<pid>/environ` is readable only for the user's own processes, which is the only scope read.
- **`.key` files** beside Claude's records are never opened (`*.json` glob only; CO-2's test).
- **Session callers** see only their repo's rows, and no paths or sockets (CA-1, CA-2); public events carry no cwd,
  title or socket (CA-3).
- **Local only:** the API stays loopback (API §6); nothing in the collector sends anything off the box.
- **No writes outside Sindri's home and the fleet dir:** the collector deletes only dead state files in the fleet dir;
  it writes nothing into `~/.claude`, `~/.codex` or `~/.cursor`.

## 13. Review notes

Filled in by /autoplan (§13.1) and CO-S's findings (§13.2).

## 14. Open questions

Each has a provisional answer, used until the owner says otherwise.

| # | Question | Provisional answer |
|---|---|---|
| Q1 | Install the `aw:fleet` hook by default, or opt-in? | Opt-in (`--with-fleet`) until the switch, like `--with-meta`; the switch makes it default with the other hooks |
| Q2 | Should Sindri read the default tmux server at all (the team-socket hazard)? | Yes, read-only, with CO9's four verbs; it is how the owner's current team is found. The owner may restrict reading to `sessions.tmuxSockets` plus `-L sindri` |
| Q3 | Does a session waiting on approval belong in Home's Needs you (R8-13's union)? | Yes, as a `session` row kind below decisions (CO-6), since a permission prompt is literally waiting on the owner. Until answered, only the Agents page and the tray tooltip show it |
| Q4 | Add a live preview (`tmux capture-pane`, X55, X59(c)) to the session sheet? | Not in this spec: it moves screen text into the API. A later decision, with its own scrub and a human-only route |
| Q5 | Add DB §4.3's finer states (`stuck`, `interrupted`, `failed`, `limited`, `crashed`, `background`, subagent roll-up)? | Not in v1: they need transcript reading. A later phase reads the transcript tail (≤ 1 MB, newest first, DB §4.1's bounds) for Claude only, after the owner has used v1 |
| Q6 | Show sessions of other users on a shared box? | No: the current user's processes only |

## 15. Changelog

- **Rev 1 (2026-10-16):** first draft, from the owner's question through the meta. CO1 records the owner's
  pre-approval of the build.
