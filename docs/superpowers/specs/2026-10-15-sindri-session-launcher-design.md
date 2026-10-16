# Sindri session launcher: any terminal, any agent CLI, and a T3 Code spike

**Status:** design, revision 1 (draft for /autoplan) · **Date:** 2026-10-15 · **Owner decisions:** 2026-10-15 (§1) ·
**Builds on:** the API surface spec `2026-10-10-sindri-api-surface-design.md` (API), §8.1 and phase P10a; the UI design
spec `2026-10-11-sindri-ui-design.md` (UI), §6.1–§6.7 and phases U6a/U6b; README rows X15, X41, X52, X55, X61 ·
**Lanes:** the API lane builds the launcher inside P10a; the ideas lane builds the `aw-meta-launch` half and runs the
T3 Code spike; the UI lane changes copy in U6a (§9) · **Next:** /specToProvenPR takes each phase to PRs once the owner
approves this doc.

Sindri's headline action is one click into a live agent session. Today's design runs that session in tmux and shows it
in **Warp**, and starts **Claude Code, Codex or Cursor** from a fixed table (UI §6.1, API §8.1). None of it is built
yet: the code has a stub `ILaunchRequests` and a trigger name that says "opens Warp"
(`src/Sindri/Aw.Sindri.Core/Workflow/Engine/Triggers/TriggerNames.cs:60`). The one launcher running today,
`aw-meta-launch`, wires Warp, tmux and Claude Code together in bash (`scripts/meta-launch.sh`, `scripts/meta-lane.sh`).

A teammate on T3 Code, kitty, GNOME Terminal or an ssh session can't use either. This doc makes the terminal and the
agent CLI configuration before API P10a writes `WarpLauncher`, which is the cheapest moment to do it. It is the first
batch from the ideas lane's data set (ideas 081, 086, 091, 092; configurability audit rows CFG-01, CFG-02, CFG-03,
CFG-41, CFG-42).

The design already separates the pieces: tmux owns the process and Warp only shows it (X55). This doc names that split
as three ports, puts the terminal and the agent catalog in a machine file, and keeps everything the API and UI specs
already contract: `OpenItem`, the modes, `session wrap`, the launch secret, liveness, budgets, the codes and the events.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| SL1 | **`ILauncher` is composed of three ports** (owner, 2026-10-15, batch 1): an **agent catalog** (data: how to start or resume each CLI), a **session host** (who owns the process: `tmux`, or `none` for an app that owns its own) and a **terminal viewer** (what a human sees: `warp`, `exec-template`, `print`, later others). `ILauncher` keeps its name, its owner (API P10a) and its contract (API §8.1); `WarpLauncher` becomes one viewer adapter | The split already exists in the design (X55); naming it lets a new terminal or CLI be an adapter or a data row, never a change to `OpenItem` |
| SL2 | **The terminal is a fact about the box, not policy.** It lives in a machine file, `$AW_STATE_DIR/launcher.json`, and changing it needs no profile approval. The approved profile only names agent ids (`launch.default`, `launch.repos.<repo>.default`, UI E-4; `agents.<id>.dailyBudget`, E-28) | Two teammates can share one profile with different terminals. Approval protects what the factory may do, and which window shows a session isn't that |
| SL3 | **Agent targets are data.** Built-in catalog entries for `claude-code`, `codex` and `cursor` carry today's argv from UI §6.1. `launcher.json` may override them or add more (`aider`, `opencode`, `gemini`…). A target id is an open string (pattern `^[a-z][a-z0-9-]{1,31}$`); one the catalog doesn't know is refused at launch with `SND-LAUNCH-003` | A teammate on another CLI adds one JSON entry. Ids stay stable in events and goldens |
| SL4 | **tmux stays the host for every Step** (X55 unchanged). The `none` host is for **human opens only**: a GUI app that owns its own agent process (T3 Code). The engine's agent runtime (engine P8) refuses a target whose host is `none` | Joining any Step live needs a host Sindri can attach to and capture. A human open needs only to start a session |
| SL5 | **A viewer failure never stops a tmux-hosted session** (generalises API §8.1's Warp rule). The session starts first in the host, the viewer then opens `tmux attach`; if the viewer fails, `launch.started` is still recorded, with `viewer: failed` and the attach command to copy | Same guarantee for every terminal, and the `print` viewer makes an ssh or headless box a supported setup rather than an error |
| SL6 | **One file for Sindri and `aw-meta-launch`.** The Meta launcher reads the same `launcher.json` (viewer and the lanes' agent) through `jq`; Warp's multi-pane layout stays a Warp-only capability, and other viewers open one window per lane | The live launcher today is Meta's, and a teammate meets it first |
| SL7 | **T3 Code is a spike first** (owner, 2026-10-15). T3 Code starts and owns its agent process and can't adopt a `claude --session-id` or `codex resume` session (audit, verified in source). It is a second host family, not a viewer, and gets an adapter only if the spike's proof passes (§7) | It changes liveness, usage and takeover, so it is measured before it is designed in |

## 2. Terms

- **Agent catalog entry:** the argv templates and facts for one agent CLI: `bin`, `open`, `resume`, `sessionId` mode,
  `usage` reader id, `mcp` form.
- **Session host:** the process owner. `tmux` (Sindri's socket `-L sindri`, session `snd-<run>` or `snd-<launch>`), or
  `none`.
- **Terminal viewer:** opens a human-visible window running a command (for a tmux host, `tmux -L sindri attach -t …`),
  or brings an app forward.
- **Launch target:** what `OpenItem.target` names. Today a target id equals an agent id; the viewer and host come from
  `launcher.json`, so the target menu stays one list of agents (UI §6.1).

## 3. The ports

All three live in `Aw.Sindri.Core/Launch/` beside `ILauncher` (API P10a's `Core/Launch/*`). Adapters live in
`Aw.Sindri.Infrastructure/Launch/`.

| Port | Members | Notes |
|---|---|---|
| `IAgentCatalog` | `Resolve(AgentId) → Result<AgentEntry>`; `All()`; `BuildArgv(AgentEntry, LaunchMode, ArgvInputs) → Result<IReadOnlyList<string>>` | Pure. `ArgvInputs` holds only ids, the repo path, the seed (a fixed template plus ids, UI §6.2) and the per-launch MCP config path. The result is an argument list, never a string |
| `ISessionHost` | `Start(HostSession, argv, cwd, env) → Result<HostRef>`; `IsAlive(HostRef)`; `AttachCommand(HostRef) → argv?` (null for `none`); `Capture(HostRef)` (later, the collector X59c); `Stop(HostRef)` | `tmux` adapter: `tmux -L sindri new-session -d -s snd-… -c <cwd>` with the argv wrapped by `sindri session wrap`. `none`: `Start` is refused; the viewer starts the process |
| `ITerminalViewer` | `Open(ViewerRequest{title, cwd, argv}) → Result<ViewerOutcome>`; `Probe() → TerminalFacts{id, present, version?, problem?}` | `ViewerOutcome` is `opened`, `skipped` (print) or `failed(code)` |

`ILauncher.Launch` becomes: resolve the agent (catalog) → refuse at budget (E-28, unchanged) → start in the host →
open the viewer on the host's attach command → record `launch.started` with the viewer outcome. For a `none` host the
viewer is asked to start the agent itself (T3 Code, §7), and a viewer failure is `launch.failed`.

`ILauncher.Probe` (UI E-3) fills `launch.probed` as today. `terminal` keeps its shape; `terminal.id` is now the viewer
kind (`warp`, `exec-template:<name>`, `print`), so no golden changes shape. One field is added: `host: {kind, present,
problem?}` (an additive field, API §9 versioning rules).

## 4. `launcher.json`

Machine-wide, at `$AW_STATE_DIR/launcher.json` (`AW_LAUNCHER_CONFIG` overrides the path). Read when the launcher
service starts and on **Check again** (`ProbeLaunchTargets`). JSON through `AwJson` with a source-generated context;
unknown keys are an error, so a typo shows up in `sindri doctor`.

```jsonc
{
  "schema": 1,
  "viewer": {
    "kind": "exec-template",                   // warp | exec-template | print   (v1)
    "name": "kitty",                           // exec-template only: the label in the UI ("Sent to kitty")
    "argv": ["kitty", "--title", "{title}", "--directory", "{cwd}", "{cmd...}"]
  },
  "host": { "kind": "tmux" },                  // tmux (default) | none (only with a viewer that owns processes)
  "agents": {                                  // merged over the built-in claude-code, codex and cursor entries
    "aider": {
      "label": "Aider",
      "bin": "aider",
      "open":   ["{bin}", "--message", "{seed}"],
      "resume": null,                          // no resume: the Resume mode is never offered for this agent
      "sessionId": "none",                     // pre-chosen-uuid | reported | none
      "usage": "none",                         // claude-code | codex | cursor | none (built-in readers only)
      "mcp": "none"                            // claude-config-file | codex-overrides | none
    }
  },
  "meta": { "lanesAgent": "claude-code" }
}
```

Rules:
- **Placeholders are a closed set:** `{bin}`, `{cwd}`, `{title}`, `{seed}`, `{sessionId}`, `{mcpConfig}`, `{cmd...}`
  (the host's attach argv, spliced as separate arguments). Each expands to exactly one argument (`{cmd...}` to
  several); there is no string concatenation inside an argument and no shell. An unknown placeholder fails validation.
- **`usage` and `mcp` name built-in readers and forms only.** A user-added agent can't add code; with `usage: none`
  its sessions record no tokens, and its target shows `limited: "no usage reader: budgets can't be enforced"` and is
  refused when its profile entry has a `dailyBudget` (`SND-LAUNCH-010`'s fix text names this).
- **Missing file:** the built-in defaults apply: the `warp` viewer when Warp is present (the owner's setup is
  unchanged), else `print`; `tmux` host; the three built-in agents. Doctor reports which applied.
- **Invalid file:** the launcher service refuses to launch with `SND-LAUNCH-011` (new, §6) and doctor shows the parse
  error and the line; the probe records `terminal.problem: config-invalid`.
- **Permissions:** the file may run commands as the user, like their shell rc. Doctor warns when it is group- or
  world-writable. It never holds secrets (secret pointers stay in the profile, X18).

**Precedence for the target** stays UI §6.1's: per-click choice → last used in the repo → profile
`launch.repos.<repo>.default` → `launch.default` → `claude-code`. A profile default naming an agent the machine
catalog lacks is shown as disabled with `SND-LAUNCH-003` and the fix "add `<id>` to launcher.json or pick another
target"; it doesn't fail profile validation, because the profile is shared and the catalog is per box.

## 5. Viewer adapters (v1)

| Adapter | Open | Probe | Fails as |
|---|---|---|---|
| `warp` | UI §6.3's recipe unchanged: per-launch YAML (serializer, 0600) with one `exec` running the attach argv, `warp://launch/…` through `open`/`xdg-open`, deleted 60 s after the outcome | bundle id or `warp://` handler; GUI session present | `SND-LAUNCH-001` (URI refused), `-002` (absent or no GUI session), `-009` (timeout) |
| `exec-template` | spawns `argv` detached (new session, stdio to `/dev/null`), placeholders expanded per §4; exit within 10 s or still running counts as opened | `argv[0]` resolves through the login shell (UI §6.3's 5 s probe); GUI session present (`DISPLAY`/`WAYLAND_DISPLAY`, or macOS) | `-002` (missing binary or no GUI session), `-001` (non-zero exit within 10 s), `-009` |
| `print` | nothing opens; the outcome is `skipped` and `launch.started` carries the attach command, which the app and `sindri open` print | always present | never; `SND-LAUNCH-002` doesn't apply |

Worked examples for `exec-template` (documented in `docs/csharp/sindri.md`, not built-in code):
GNOME Terminal `["gnome-terminal", "--title={title}", "--working-directory={cwd}", "--", "{cmd...}"]`; kitty as above;
WezTerm `["wezterm", "start", "--cwd", "{cwd}", "--", "{cmd...}"]`; Alacritty
`["alacritty", "--title", "{title}", "--working-directory", "{cwd}", "-e", "{cmd...}"]`; foot, Konsole and xterm
alike. Later adapters, each its own small phase when someone needs it: `iterm` and `macos-terminal` (AppleScript),
`vscode`/`cursor` (opens the folder; the attach command is copied, since VS Code can't be told to run a command from
outside without an extension).

Attach (API §8.1, UI §6.2) is the same `Open` call with the host's attach argv; for a `none` host it is not offered
(`SND-LAUNCH-006`, the mode falls back to Open), except T3 Code's deep link if §7 adopts it.

## 6. API, engine and UI edits

Numbered so the owning lane can take them into its spec and phase.

| # | Spec | Edit | Phase |
|---|---|---|---|
| L-1 | API §8.1 | `ILauncher` is composed of `IAgentCatalog`, `ISessionHost`, `ITerminalViewer` (§3); "Warp is a viewer" becomes "the configured viewer"; the "a Warp that fails to open leaves the session running" rule is SL5 for every viewer | P10a |
| L-2 | API §8.1, Phases | P10a's `Infrastructure/Launch/{Warp,Process}Launcher.cs` becomes `Launch/{TmuxSessionHost, WarpViewer, ExecTemplateViewer, PrintViewer, AgentCatalog, LauncherConfigFile}.cs`; `ProcessLauncher` is gone already (X55) | P10a |
| L-3 | API §9 | `SND-LAUNCH-011` "launcher.json is invalid" (`detail`: file, line, reason; fix: correct it or delete it to use the defaults). `SND-LAUNCH-003`'s detail covers "agent not in the catalog". `SND-LAUNCH-002`'s text says "no terminal viewer" instead of Warp | P10a |
| L-4 | API §3.4 / UI E-3 | `launch.probed` gains `host {kind, present, problem?}`; `terminal.id` is the viewer kind; target rows gain `limited` reasons `no-usage-reader` and `host-none-human-only` | P10a |
| L-5 | API §3.8 / E-28 | A target with `usage: none` and a `dailyBudget` is refused with `SND-LAUNCH-010` (detail `no-usage-reader`) | P10a |
| L-6 | WF §6.5 (engine P8) | The agent runtime uses `ILauncher` with `attended: false`, which skips the viewer, and refuses a target whose host is `none` | engine P8 |
| L-7 | UI §6.2, §6.6, U6a | Copy: "Sent to Warp" becomes "Sent to <terminal label>" from `terminal.id`/`viewer.name`; with `print`, "Session started: run `<attach>`" with a copy button; §6.6's Warp rows read "the terminal". No new view | U6a |
| L-8 | `docs/sindri/profile.md` | Regenerated from the C# `SindriProfile` schema (it still documents the TS profile, CFG-42), including E-4's `launch.*` and E-28's `agents.*` keys | phase SL-0 |

Nothing changes in `OpenItem`, the modes, `session wrap`, the launch secret, liveness, `session.ended`, `session.usage`,
budgets or the needs-you rows.

## 7. T3 Code spike

What the audit found (T3 Code source, `pingdotgg/t3code` main, 2026-10-15; MIT): T3 Code starts and owns its agent
process (Codex's app-server, or the Claude Agent SDK). It is driven from outside through its own MCP server
(`t3_project_list/create`, `t3_thread_launch`, `t3_thread_read`, `t3_thread_wait`) after a one-time OAuth pairing, and
a thread is brought forward with `t3code://threads/<env>/<thread>`. A deep link alone only drafts a prompt.

The spike answers six questions on the owner's box, with a scratch project and no Sindri code merged:

| # | Question | Pass when |
|---|---|---|
| T1 | Can a script pair once and then launch a thread with a fixed seed, unattended? | `t3_thread_launch` returns a thread id from a non-interactive client after one pairing |
| T2 | Does the session reach Sindri's MCP endpoint? | The thread's first tool call is `sindri_item` (or the reason it can't be, recorded) |
| T3 | Can Sindri tell the thread is alive and when it ends? | `t3_thread_read`/`wait` gives a state that maps to `session.ended` (`exit` or `lost`) |
| T4 | Can usage be read? | Token counts per thread are readable, or the spike records that `usage: none` applies |
| T5 | Does Claude Code's `UserPromptSubmit` hook fire inside a T3 thread? | Recorded yes or no; no means the first typed turn is not a takeover, so opening stays the only takeover |
| T6 | Does the deep link focus the thread from `open`/`xdg-open`? | The desktop app comes forward on the thread |

Outcome: a findings note appended to this spec (§11) and an owner decision. If T1–T3 pass, phase SL-4 builds a
`t3code` adapter as a **viewer that owns its process** with the `none` host: human opens and resumes only (SL4).
If T1 or T3 fails, T3 Code users get the `print` viewer's copy-the-command flow plus a documented manual path, and the
idea stays in the data set.

## 8. Testing

Fits `.agents/rules/csharp-testing.md`:
1. **Contract tests over a fake** (`Aw.Sindri.Testing`): `SessionHostContract` (start → alive; stop → not alive; the
   attach argv names the session; ids are validated before use) and `TerminalViewerContract` (open records one
   outcome under `FakeTimeProvider`; no secret and no item text in argv or files; per-launch files are 0600 and
   swept), each run against a recording fake and the real adapters that can run headless.
2. **Unit:** golden argv per built-in agent and mode; `launcher.json` parsing and merging over the defaults (goldens,
   including every error); placeholder expansion with spaces, quotes, `$` and newlines (each stays one argument);
   target precedence; a `usage: none` target with a budget is refused.
3. **Integration lane:** the real `TmuxSessionHost` on a private socket and `TMUX_TMPDIR`, a stub agent script in
   place of `claude`, the `print` viewer and an `exec-template` viewer that is a script recording its argv; the
   "viewer failed, session alive and attachable" path; `launch.probed` with `host`.
4. **Real-app proof** through `scripts/sindri-prove.sh` (§10's proof column).
5. **Bash:** `scripts/tests/meta-launch.test.sh` gains `AW_LAUNCHER_CONFIG` cases (`print`, a recording
   `exec-template`, a non-Claude lane agent).

## 9. What stays out

- No `iterm`, `macos-terminal`, `vscode` or `zellij` adapter in this batch (each is a later small phase).
- No change to who may open, attach or take over (API §4, §6), the launch secret or `session wrap`.
- No per-repo terminal choice: the terminal belongs to the box. A per-repo agent default is already the profile's.
- No agent-session detection change (API §6's marker list, CFG-06); it moves to catalog data in a later batch.
- Remote runners (X61b) aren't designed here; `ISessionHost` is where they would fit.

## 10. Phases

| Phase | What | Where | Size | Needs | Proof | Lane |
|---|---|---|---|---|---|---|
| **SL-0** | `launcher.json` schema doc and the regenerated `docs/sindri/profile.md` (L-8); the `exec-template` worked examples | `docs/csharp/sindri.md`, `docs/sindri/profile.md` | S | this spec approved | Doc review; `profile.md` matches `SindriProfile`'s keys (a test lists the schema's keys and fails on a key the doc lacks) | ideas |
| **SL-1** | `aw-meta-launch` reads `launcher.json`: viewer `warp` (today's layout), `exec-template` (one window per lane) or `print`; the lanes' agent from the catalog (`open`/`resume` argv, process name for `claude_in_pane`, transcript glob for `has_transcript`) | `scripts/meta-launch.sh`, `scripts/meta-lane.sh`, `scripts/tests/meta-launch.test.sh` | S | SL-0 | `meta-launch.test.sh` passes the new cases; on a box with no Warp, `aw-meta-launch --dry-run` with a kitty `exec-template` prints one kitty argv per lane; with no file, the owner's Warp layout is byte-identical to today's | ideas |
| **SL-2** | The three ports and `launcher.json` inside P10a (L-1…L-5): `IAgentCatalog` with the built-in entries, `TmuxSessionHost`, `WarpViewer`, `ExecTemplateViewer`, `PrintViewer`, the config reader, `SND-LAUNCH-011`, `host` on `launch.probed` | `src/Sindri/Aw.Sindri.Core/Launch/`, `…Infrastructure/Launch/` | M (inside P10a's L) | API P10a's own needs (P3 + P8) | P10a's proofs unchanged with no `launcher.json`; plus `sindri-prove.sh human open ITM-…` with an `exec-template` viewer under `xvfb-run xterm` opens a window titled `sindri ITM-…` with one tmux client attached; with `print`, `open` exits 0 and prints the attach command, and attaching by hand reaches the live agent | API |
| **SL-3** | UI copy for any terminal (L-7) | `Views/Launch*` | XS (inside U6a) | SL-2 | U6a's proof with the `print` viewer shows "Session started" and the command; with Warp, unchanged | UI |
| **SL-T** | T3 Code spike (§7): T1–T6 answered on the owner's box | a scratch dir outside the repo; findings in §11 | S | owner pairs T3 Code once | The §7 table filled with evidence (commands, ids, screenshots) and an owner decision | ideas |
| **SL-4** | `t3code` viewer with the `none` host, if SL-T passes | `…Infrastructure/Launch/T3CodeViewer.cs` | M | SL-2, SL-T + owner decision | `sindri-prove.sh human open ITM-…` with `viewer.kind: t3code` starts a T3 thread whose first call is `sindri_item`; ending the thread writes `session.ended`; Attach isn't offered | API |
| **SL-5** | Agent runtime refuses `none` hosts (L-6) | engine P8 | XS (inside P8) | SL-2 | An eval: a Step whose target's host is `none` parks with the code, never launches | engine |

### 10.1 Parallelization plan

The ideas lane runs one builder at a time; the other lanes take their edits into phases they already own.

| Wave | Ideas lane | API lane | UI lane | Engine lane | Owner |
|---|---|---|---|---|---|
| 0 | SL-0 (docs) | reads L-1…L-5 into P10a's plan | — | — | approves this spec; pairs T3 Code for SL-T |
| 1 | SL-1 (bash), then SL-T | P10a with SL-2 folded in | — | — | — |
| 2 | — | — | U6a with SL-3 | P8 with SL-5 | decides SL-4 from SL-T's findings |
| 3 | — | SL-4 if approved | — | — | — |

SL-0 and SL-1 don't wait on any other lane, so a teammate without Warp can use `aw-meta-launch` before P10a lands.
SL-2 doesn't wait on SL-T: the `none` host is in the port from the start, and only its T3 adapter waits.

## 11. Open questions

| # | Question | Provisional answer |
|---|---|---|
| Q1 | May user-added catalog entries run without profile approval? | Yes: they run as the user, in the user's terminal, the same as typing the command. The profile still decides which agent is the default, and budgets still apply (L-5). Reversible: an `agents.allowed` profile list can be added later |
| Q2 | Should a T3 Code user be able to attach to factory Steps? | No: Steps stay tmux-hosted (SL4). T3 Code covers human opens and resumes |
| Q3 | Default viewer with no `launcher.json`? | `warp` when present, else `print`, so the owner's setup is unchanged and a headless box works |
| Q4 | Does `aw-meta` keep bash for launching or move to the C# `ILauncher`? | Bash with `jq` in SL-1 (small, live now); revisit when aw-meta and Sindri share a launcher assembly |
