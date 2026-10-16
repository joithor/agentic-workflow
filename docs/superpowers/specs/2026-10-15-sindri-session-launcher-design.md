# Sindri session launcher: any terminal, any agent CLI, and a T3 Code spike

**Status:** design, revision 2 (after /autoplan pass 1, §12) · **Date:** 2026-10-15 · **Owner decisions:** 2026-10-15
(§1; rev 1's direction approved, "i approve of 254"; non-Claude Meta lanes allowed, §11 Q5) ·
**Builds on:** the API surface spec `2026-10-10-sindri-api-surface-design.md` (API), §3.4, §3.8, §6, §8.1, §9 and phase
P10a; the UI design spec `2026-10-11-sindri-ui-design.md` (UI), §6.1–§6.7 (E-1…E-5, E-11, E-28) and phases U6a/U6b; the
workflow engine spec `2026-10-10-sindri-workflow-engine-design.md` (WF), §6.5 and P8; README rows X15, X41, X52, X55,
X61 · **Lanes:** the API lane builds the launcher inside P10a; the ideas lane builds the `aw-meta-launch` half, the
non-Claude Meta lanes and runs the T3 Code spike; the UI lane changes copy in U6a; the engine lane takes L-6 into P8
(§6) · **Next:** /autoplan pass 2, then the owner's review, then /specToProvenPR takes each phase to PRs.

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
as three ports, puts the terminal and the agent catalog in a machine file, and keeps the API and UI specs' launch
pipeline: `OpenItem`'s modes, the request, claim and expiry rules (E-1), `session wrap`, the launch secret, liveness,
budgets and the needs-you rows. What it does change in their contracts is listed as numbered edits in §6 (L-1…L-13).

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| SL1 | **`ILauncher` is composed of three ports** (owner, 2026-10-15, batch 1): an **agent catalog** (data: how to start or resume each CLI), a **session host** (who owns the process: `tmux`, or `none` for an app that owns its own) and a **terminal viewer** (what a human sees: `warp`, `exec-template`, `print`, later others). `ILauncher` keeps its name, its owner (API P10a) and its launch pipeline (API §8.1, UI E-1); `WarpLauncher` becomes one viewer adapter | The split already exists in the design (X55); naming it lets a new terminal or CLI be an adapter or a data row, never a change to `OpenItem`'s modes |
| SL2 | **The terminal is a fact about the box, not policy.** It lives in a machine file, `$AW_STATE_DIR/launcher.json`, and changing it needs no profile approval. The approved profile only names agent ids (`launch.default`, `launch.repos.<repo>.default`, UI E-4; `agents.<id>.dailyBudget`, E-28) and, for factory Steps, the entries it trusts (SL8) | Two teammates can share one profile with different terminals. Approval protects what the factory may do, and which window shows a session isn't that |
| SL3 | **Agent targets are data.** Built-in catalog entries for `claude-code`, `codex` and `cursor` carry today's argv from UI §6.1. `launcher.json` may add entries (`aider`, `opencode`, `gemini`…) and may replace a built-in one only with `"override": true` (§4). A target id is an open string (pattern `^[a-z][a-z0-9-]{1,31}$`) everywhere a target is named: `OpenItem.target`, `sindri open --agent`, events (L-9). An id the catalog doesn't know is refused at launch with `SND-LAUNCH-003` | A teammate on another CLI adds one JSON entry. Ids stay stable in events and goldens |
| SL4 | **tmux stays the host for every Step** (X55 unchanged). The `none` host is for **human opens only**: a GUI app that owns its own agent process (T3 Code). The engine's agent runtime (engine P8) refuses a target whose host is `none` | Joining any Step live needs a host Sindri can attach to and capture. A human open needs only to start a session |
| SL5 | **A viewer failure never stops a tmux-hosted session** (generalises API §8.1's Warp rule). The session starts first in the host, then the viewer opens `tmux attach`. `launch.started` gains `viewer {kind, label, outcome, code?}` and `attach` (L-10). On a tmux host, a viewer that fails (`SND-LAUNCH-001`, or `-009` after the host started) is `launch.started` with `viewer.outcome: failed` and the attach command to copy, never `launch.failed`. On a `none` host the viewer is the process owner, so its failure stays `launch.failed` | Same guarantee for every terminal, and the `print` viewer makes an ssh or headless box a supported setup rather than an error |
| SL6 | **One file for Sindri and `aw-meta-launch`** (owner, 2026-10-15: "also allow non-claude agents in them"). The Meta launcher reads the same `launcher.json` for its viewer and for its lanes' agent: `meta.lanesAgent` and per-lane `meta.lanes.<lane>` may name any catalog agent. Warp's multi-pane layout stays a Warp-only capability; other viewers open one window per lane. A non-Claude lane launches in SL-1 and takes part in /meta's obligations from SL-1b (§10) | The live launcher today is Meta's, and a teammate meets it first, on whatever CLI they use |
| SL7 | **T3 Code is a spike first** (owner, 2026-10-15). T3 Code starts and owns its agent process and can't adopt a `claude --session-id` or `codex resume` session (audit, verified in source). It is a second host family, not a viewer, and gets an adapter only if the spike's proof passes (§7) | It changes liveness, usage and takeover, so it is measured before it is designed in |
| SL8 | **Unattended Steps run only approved argv** (review C7). A Step launch (`attended: false`, L-1) uses a built-in entry, or an added or overriding entry whose id and `argvHash` (§4) the approved profile lists as `agents.<id>.stepArgvHash` (L-11). Anything else parks the Step with `SND-LAUNCH-012`. Human opens keep Q1: any entry in the box's file | `launcher.json` needs no approval, so it must not decide what the factory runs unattended |

## 2. Terms

- **Agent catalog entry:** the argv templates and facts for one agent CLI: `bin`, `open`, `resume`, `sessionId` mode,
  `sessionIdPattern`, `reportedId`, `processName`, `transcripts`, `usage` reader id, `mcp` form (§4).
- **Catalog source:** where an entry came from: `builtin`, `override` (a built-in id replaced with `"override": true`)
  or `added`.
- **`argvHash`:** `sha256:` plus the lowercase hex SHA-256 of the entry's canonical JSON for `bin`, `open` and `resume`
  (the templates, before expansion; so no item text and no ids). The same value appears on `launch.started`, in
  `sindri launcher show` and in the profile's `stepArgvHash`.
- **Session host:** the process owner. `tmux` (Sindri's socket `-L sindri`, session `snd-<run>` or `snd-<launch>`), or
  `none`.
- **Terminal viewer:** opens a human-visible window running a command (for a tmux host,
  `tmux -L sindri attach -t …`), or brings an app forward. Its **label** is what the app names it: `Warp` for `warp`,
  `viewer.name` for `exec-template` (required there), none for `print`.
- **Attended:** a launch a human asked for (`OpenItem`, `sindri open`). Unattended launches come from the engine's agent
  runtime (WF §6.5) and skip the viewer (L-1).
- **Launch target:** what `OpenItem.target` names. A target id equals an agent id; the viewer and host come from
  `launcher.json`, so the target menu stays one list of agents (UI §6.1).

## 3. The ports

All three live in `Aw.Sindri.Core/Launch/` beside `ILauncher` (API P10a's `Core/Launch/*`). Adapters live in
`Aw.Sindri.Infrastructure/Launch/`.

| Port | Members | Notes |
|---|---|---|
| `IAgentCatalog` | `Resolve(AgentId) → Result<AgentEntry>`; `All()`; `BuildArgv(AgentEntry, LaunchMode, ArgvInputs) → Result<IReadOnlyList<string>>` | Pure. `ArgvInputs` holds only ids, the repo path, the session name, the seed (a fixed template plus ids, UI §6.2) and the per-launch MCP config path. The result is an argument list, never a string. `AgentEntry` carries its `source` and `argvHash` |
| `ISessionHost` | `Start(HostSession, argv, cwd, env) → Result<HostRef>`; `IsAlive(HostRef)`; `AttachCommand(HostRef) → argv?` (null for `none`); `AttachedClients(HostRef) → int`; `Capture(HostRef)` (later, the collector X59c); `Stop(HostRef)` | `tmux` adapter: `tmux -L sindri new-session -d -s snd-… -c <cwd> -e K=V…` with the argv wrapped by `sindri session wrap`. `none`: `Start` is refused; the viewer starts the process |
| `ITerminalViewer` | `Open(ViewerRequest{title, cwd, argv, host}) → Result<ViewerOutcome>`; `Probe() → TerminalFacts{id, label?, present, version?, problem?}` | `ViewerOutcome` is `opened`, `unconfirmed`, `skipped` (print, or unattended) or `failed(code)` |

**The agent's environment** (review C10). UI §6.3 step 2 put `AW_SINDRI_CHILD=1`, `AW_SINDRI_ITEM`, `AW_SINDRI_LAUNCH`
and the missing-CLI guard into Warp's exec line; with tmux as the host that line only runs `tmux attach`, so they move:
`ISessionHost.Start(env)` passes them with `new-session -e` (tmux ≥ 3.0, checked by the host probe), and
`sindri session wrap` sets them again before it execs the agent, so a session started by hand inside the pane has them
too. `session wrap` keeps the guard: when the agent's `bin` doesn't resolve it prints
`<bin> not found (SND-LAUNCH-003): <fix>` and exits 127. None of these variables is a secret; the launch secret keeps
its 0600 one-time file (API §8.1), and the tmux server's global environment never carries it.

**`ILauncher.Launch`** becomes: resolve the agent (catalog) → for an unattended launch, check SL8 (`SND-LAUNCH-012`)
and refuse a `none` host → refuse at budget (E-28, unchanged) → start in the host → open the viewer on the host's
attach command (attended only) → record `launch.started` with the viewer outcome, `attach`, `catalogSource` and
`argvHash` (L-10). For a `none` host the viewer is asked to start the agent itself (T3 Code, §7), and a viewer failure
is `launch.failed` with the viewer's code; if the viewer started a thread before failing, the adapter stops it.

**Timing inside E-1's 10 s limit** (review C3). Catalog and host start take at most 4 s. The viewer then has a 3 s
observation window: a non-zero exit inside it is `failed SND-LAUNCH-001`; meanwhile the launcher polls
`AttachedClients` (`tmux -L sindri list-clients -t snd-…`) every 250 ms, and the first attached client is `opened`. A
viewer that is still running, or exited 0, with no client at the end of the window is `unconfirmed`. Host start plus
the window stays under 8 s, so the 10 s limit fires only when the host itself hangs; if it fires after the host
started, the outcome is `launch.started` with `viewer.outcome: failed, code: SND-LAUNCH-009` (SL5), and before, it
stays `launch.failed SND-LAUNCH-009`. This rule is the same for `warp` and `exec-template`; it never reports "Sent to
kitty" for a single-instance terminal that exited 0 without a window.

`ILauncher.Probe` (UI E-3) fills `launch.probed` as today. `terminal` keeps its shape; `terminal.id` is now the viewer
kind (`warp`, `exec-template:<name>`, `print`), so no golden changes shape. Added fields (additive, API §9 versioning
rules; L-4): `terminal.label`, `terminal.source` (`file` or `default`), `host: {kind, present, version?, problem?}` and,
on each target row, `source` and `overridden`.

## 4. `launcher.json`

Machine-wide, at `$AW_STATE_DIR/launcher.json`. Read when the launcher service starts and on **Check again**
(`ProbeLaunchTargets`). One JSON Schema file, `schemas/launcher.schema.json`, is the contract for both readers
(review C11): the C# reader parses type-driven through `AwJson` with a source-generated context, rejects unknown keys,
and its tests check every fixture in `tests/fixtures/launcher/` against the schema file and against the reader with
the same verdict; the bash reader (SL-1) reads its allowed key lists and placeholder set from the schema file itself,
not a copy, and refuses what the schema refuses, with the same `SND-LAUNCH-011` wording. The built-in entries live
once, in `config/launcher/builtins.json`: the C# build embeds it and the bash reader reads it. The terminal templates
the generator writes live once, in `config/launcher/terminals.json`.

```jsonc
{
  "schema": 1,
  "viewer": {
    "kind": "exec-template",                   // warp | exec-template | print   (v1)
    "name": "kitty",                           // exec-template only, required: the label in the UI ("Sent to kitty")
    "argv": ["kitty", "--title", "{title}", "--directory", "{cwd}", "--", "{cmd...}"]
  },
  "host": { "kind": "tmux" },                  // tmux (default) | none (only with a viewer that owns processes)
  "agents": {                                  // merged over the built-in claude-code, codex and cursor entries
    "aider": {
      "label": "Aider",
      "bin": "aider",
      "open":   ["{bin}", "--message", "{seed}"],
      "resume": null,                          // no resume: the Resume mode is never offered for this agent
      "sessionId": "none",                     // pre-chosen-uuid | reported | none
      "sessionIdPattern": null,                // required unless sessionId is none: the id must match before use
      "reportedId": null,                      // sessionId: reported only: {"newestFile": "<glob>", "capture": "<regex, one group>"}
      "processName": "aider",                  // the process a pane runs while the agent is up (Meta's claude_in_pane)
      "transcripts": null,                     // a glob with {sessionId}: the agent's transcript (Meta's has_transcript)
      "usage": "none",                         // claude-code | codex | cursor | none (built-in readers only)
      "mcp": "none"                            // claude-config-file | codex-overrides | none
    },
    "claude-code": { "override": true, "bin": "/opt/claude/bin/claude" }   // replaces fields of a built-in entry
  },
  "meta": {
    "lanesAgent": "claude-code",               // the agent for aw-meta-launch's lanes (any catalog id)
    "lanes": { "review": "codex" }             // per-lane overrides, by lane name, in every team on this box
  }
}
```

Rules:
- **Placeholders are a closed set:** `{bin}`, `{cwd}`, `{title}`, `{name}`, `{seed}`, `{sessionId}`, `{mcpConfig}`,
  `{cmd...}`. `{name}` is the session's name (Meta's `-n <team>-<lane>`; Sindri's `sindri ITM-…`). `{cmd...}` is the
  host's attach argv, spliced as separate arguments: Sindri's is `tmux -L sindri attach -t =snd-…`, Meta's is
  `tmux attach -t =<team>-<lane>` on tmux's default socket. Each other placeholder expands to exactly one argument;
  there is no string concatenation inside an argument and no shell. An unknown placeholder fails validation.
- **No option smuggling** (review C18). An expanded `{title}`, `{name}`, `{sessionId}` or `{seed}` that starts with
  `-` is refused before the spawn (ids and the seed template never do; the check is a backstop), and `{cwd}` is always
  absolute. A viewer whose terminal re-parses a single string after `-e` (`gnome-terminal -e`, `konsole -e "…"`,
  `xterm -e` with one string) runs a shell on it; the doc's worked examples use the argv forms (`--` before
  `{cmd...}`), and the generator never writes a single-string `-e` form.
- **Overriding a built-in entry needs `"override": true`** (review C8). Without it, an `agents.claude-code` key is
  `SND-LAUNCH-011` ("`claude-code` is built in: add `"override": true` to replace it"). An override replaces only the
  fields it names; `usage` and `mcp` stay the built-in's unless named. Doctor lists every override with its fields, and
  `launch.probed` marks the target `overridden: true`.
- **Session ids are checked against the entry.** `sessionIdPattern` is required unless `sessionId` is `none`; the
  built-ins carry today's patterns (a UUID for Claude Code; Codex and Cursor from UI §6.3 step 1). A session id that
  doesn't match drops Resume and falls back to Open, as today. For `sessionId: reported`, `session wrap` captures the
  id after start from the newest file matching `reportedId.newestFile` created after the start (the `capture` group,
  checked against `sessionIdPattern`) within 30 s, and reports it on the session's liveness record (L-9); no capture,
  no Resume.
- **`usage` and `mcp` name built-in readers and forms only.** A user-added agent can't add code; with `usage: none`
  its sessions record no tokens, and its target shows `limited: "no usage reader: budgets can't be enforced"` and is
  refused when its profile entry has a `dailyBudget` (`SND-LAUNCH-010`'s fix text names this).
- **Missing file:** the built-in defaults apply: the `warp` viewer when Warp is present, else `print`; `tmux` host; the
  three built-in agents; `meta.lanesAgent: claude-code`. The default viewer is resolved **once per launcher service
  start** (review C19), not per probe, and `terminal.source: default` says so; doctor shows the resolved viewer and the
  command that writes a file (`sindri launcher init`).
- **Invalid file:** the launcher service refuses attended launches with `SND-LAUNCH-011` (§6) and doctor shows the
  parse error and the line; the probe records `terminal.problem: config-invalid`. Errors are checked per section:
  unattended Steps (which skip the viewer) fail only on an error in `agents` or `host`.
- **Permissions** (review C18): the file may run commands as the user, like their shell rc. Doctor warns when the file
  is group- or world-writable, when its owner isn't the current user, or when its parent directory is group- or
  world-writable; the same checks apply to the `AW_LAUNCHER_CONFIG` target. It never holds secrets (secret pointers
  stay in the profile, X18).

**Precedence for the config** (review C12): environment, then the file, then the built-in defaults.
`AW_LAUNCHER_CONFIG` names the file (for tests and the prover's guarded temp home; doctor prints the resolved path).
Meta's existing variables rank above the file for the lanes they already cover: `AW_META_CLAUDE` replaces the
`claude-code` entry's `bin`, `AW_META_CLAUDE_ARGS` is appended to its argv, and `AW_WARP_LAUNCH_DIR` is where the
`warp` viewer writes Meta's launch configuration. They stay for tests; `docs/csharp/meta.md` marks the first two as
deprecated for users in favour of an `override` entry.

**Precedence for the target** stays UI §6.1's: per-click choice → last used in the repo → profile
`launch.repos.<repo>.default` → `launch.default` → `claude-code`. A rule whose agent isn't in this box's catalog is
**skipped**, and resolution goes to the next rule (review C6): the button names the agent that resolved, `chosenBy`
names the rule that supplied it, and the menu shows the skipped default disabled with `SND-LAUNCH-003` and the fix
"add `<id>` to launcher.json or pick another target". It doesn't fail profile validation, because the profile is shared
and the catalog is per box.

**The generator** (review C5). `sindri launcher init [--viewer <name>] [--force] [--print]` writes `launcher.json` for
this box: with no GUI session it picks `print`; else Warp when present; else `$TERM_PROGRAM` when it names a terminal
in `config/launcher/terminals.json`; else the first of those terminals on the login PATH (kitty, WezTerm, GNOME
Terminal, Alacritty, Konsole, foot, xterm). It validates the result against the schema, writes it 0600, refuses to
replace a file without `--force`, and `--print` writes to stdout instead. The bash equivalent,
`aw-meta-launch --init-launcher [--viewer <name>]`, uses the same detection and the same templates file. Doctor's fix
text for a missing file, for `SND-LAUNCH-002` and for `SND-LAUNCH-011` names `sindri launcher init`.
`sindri launcher show [--json]` prints the merged catalog with each entry's source and `argvHash`.

## 5. Viewer adapters (v1)

The viewer outcome on a tmux host is recorded on `launch.started` (SL5); the session keeps running whatever the
viewer does. Only catalog, budget, SL8 and host failures are `launch.failed`.

| Adapter | Open | Probe | Viewer outcome on a tmux host |
|---|---|---|---|
| `warp` | UI §6.3's recipe unchanged: per-launch YAML (serializer, 0600) with one `exec` running the attach argv, `warp://launch/…` through `open`/`xdg-open`, deleted 60 s after the outcome; then §3's 3 s window and client check | bundle id or `warp://` handler; GUI session present | `opened` (a client attached), `unconfirmed`, `failed SND-LAUNCH-001` (URI refused), `failed SND-LAUNCH-009` (§3) |
| `exec-template` | spawns `argv` detached (new session, stdio to `/dev/null`, reaped), placeholders expanded per §4; then §3's 3 s window and client check | `argv[0]` resolves through the login shell (UI §6.3's 5 s probe); GUI session present (`DISPLAY`/`WAYLAND_DISPLAY`, or macOS); a doctor warning when `argv` lacks `{title}` | `opened`, `unconfirmed`, `failed SND-LAUNCH-001` (non-zero exit in the window, or the spawn failed), `failed SND-LAUNCH-009` |
| `print` | nothing opens; `launch.started` carries the attach command, which the app and `sindri open` print | always present | `skipped` |

A viewer the probe finds absent (`SND-LAUNCH-002`: no Warp, the template's binary not on PATH, or no GUI session)
replaces the button before the click, as UI §6.6 does today; its fix names `sindri launcher init` and the `print`
viewer. On a `none` host the same codes are `launch.failed`.

Worked examples for `exec-template` (in `config/launcher/terminals.json`, rendered into `docs/csharp/sindri.md`; data,
not built-in code): GNOME Terminal `["gnome-terminal", "--title={title}", "--working-directory={cwd}", "--",
"{cmd...}"]`; kitty as in §4; WezTerm `["wezterm", "start", "--cwd", "{cwd}", "--", "{cmd...}"]`; Alacritty
`["alacritty", "--title", "{title}", "--working-directory", "{cwd}", "-e", "{cmd...}"]` (Alacritty's `-e` takes argv);
foot, Konsole and xterm alike. GNOME Terminal and `kitty --single-instance` exit 0 through a server even when no window
appears; §3's client check is what makes their outcome honest. Later adapters, each its own small phase when someone
needs it: `iterm` and `macos-terminal` (AppleScript), `vscode`/`cursor` (opens the folder; the attach command is
copied, since VS Code can't be told to run a command from outside without an extension).

Attach (API §8.1, UI §6.2) is the same `Open` call with the host's attach argv; for a `none` host it is not offered
(`SND-LAUNCH-006`, the mode falls back to Open), except T3 Code's deep link if §7 adopts it.

## 6. API, engine and UI edits

Numbered so the owning lane can take them into its spec and phase. This doc edits no other spec.

| # | Spec | Edit | Phase |
|---|---|---|---|
| L-1 | API §8.1, README X15, X55 | `ILauncher` is composed of `IAgentCatalog`, `ISessionHost`, `ITerminalViewer` (§3); "Warp is a viewer" becomes "the configured viewer"; the "a Warp that fails to open leaves the session running" rule is SL5 for every viewer. `LaunchRequest` gains `Attended` (bool): true for `OpenItem` and `sindri open` (a `--headless` server's human opens too; with no GUI its default viewer is `print`), false for the engine's agent runtime. Unattended launches skip the viewer (`viewer {kind: none, outcome: skipped}`), are checked by SL8 and refuse a `none` host. The agent's environment and the missing-CLI guard move to `ISessionHost.Start(env)` and `session wrap` (§3) | P10a |
| L-2 | API §8.1, Phases | P10a's `Infrastructure/Launch/{Warp,Process}Launcher.cs` becomes `Launch/{TmuxSessionHost, WarpViewer, ExecTemplateViewer, PrintViewer, AgentCatalog, LauncherConfigFile, LauncherInit}.cs`; `ProcessLauncher` is gone already (X55). `TriggerNames.OpenRequest`'s doc comment (`TriggerNames.cs:60`, "before the launcher opens Warp") and `NoLaunchRequests`' wording say "the viewer" | P10a |
| L-3 | API §9, UI E-5, §6.6 | New codes: `SND-LAUNCH-011` "launcher.json is invalid" (`detail`: file, line, reason; fix: correct it, run `sindri launcher init --force`, or delete it to use the defaults) and `SND-LAUNCH-012` "this agent entry isn't approved for unattended Steps" (`detail`: id, `argvHash`; fix: approve the profile with `agents.<id>.stepArgvHash`, or use a built-in entry). `SND-LAUNCH-003`'s detail covers `not-in-catalog`. `SND-LAUNCH-002`'s text says "no terminal viewer" instead of Warp. **Re-classed for tmux hosts:** `-001`, and `-009` once the host started, are viewer outcomes on `launch.started` (SL5); on `none` hosts and before the host started they stay `launch.failed`. After a `launch.started` whose viewer outcome isn't `opened`, `launch.confirm` is set as after any start | P10a |
| L-4 | API §3.4 / UI E-3 | `launch.probed` gains `terminal.label`, `terminal.source`, `host {kind, present, version?, problem?}`, and on target rows `source` and `overridden`; `terminal.id` is the viewer kind; target rows gain `limited` reasons `no-usage-reader` and `host-none-human-only` | P10a |
| L-5 | API §3.8 / E-28 | A target with `usage: none` and a `dailyBudget` is refused with `SND-LAUNCH-010` (detail `no-usage-reader`) | P10a |
| L-6 | WF §6.5 (engine P8) | The agent runtime calls `ILauncher` with `Attended: false` (L-1); a target whose host is `none` parks the Step with `SND-LAUNCH-006`'s sibling detail `host-none-human-only`, and an entry SL8 doesn't approve parks it with `SND-LAUNCH-012`; neither launches | engine P8 |
| L-7 | UI §6.1, §6.2, §6.6, Settings, U6a | Copy and states, all from `launch.probed` and `launch.started` (no new view): **opened** "Sent to <label>" (Warp, kitty…), then "Session live" as today; **unconfirmed** "Session started · <label> hasn't attached: run `<attach>`" with a copy button; **viewer failed** "Session started · <label> didn't open (`SND-LAUNCH-001`): run `<attach>`" with a copy button; **print** "Session started: run `<attach>`" with a copy button, and `lastStarted` reads "Started 12m ago · Claude Code · not attached · `<attach>`". §6.6's Warp rows name `<label>`, not "the terminal"; `-007`/`-009`'s fix reads "A <label> window may have opened: look for `sindri ITM-…`, or run `tmux -L sindri ls`". New §6.6 row for `SND-LAUNCH-011`: detected by the probe, shown before the click (the button is replaced by "launcher.json is invalid, line N"; detail file and line; fix as L-3). The disabled-default flow is §4's fall-through, with the reason in the menu. **Picker for N agents:** the menu lists every catalog entry, built-ins first in their fixed order, then added entries by id; `Shift+O` then `1`–`9` picks by position, arrow keys reach the rest. **Accessibility:** every outcome line is announced in the launch block's live region; when the outcome asks the human to run a command (print, unconfirmed, failed) focus moves to the copy button, which is keyboard-reachable and announces "Copied". **Settings** shows the configured terminal read-only ("Terminal: kitty (exec-template) · from `~/.agentic-workflow/launcher.json`", or "default: Warp") with **Check again** | U6a |
| L-8 | `docs/sindri/profile.md` | Regenerated from the C# `SindriProfile` schema (it still documents the TS profile, CFG-42), including E-4's `launch.*`, E-28's `agents.*` and L-11's `stepArgvHash` | phase SL-0 |
| L-9 | UI E-1, E-11, §6.3 step 1; README X52 | Target ids open (review C1): E-1's body `target?` and `item.open-requested.target`, `launch.started.target` and E-11's `sindri open --agent <id>` become the typed id `AgentId` (pattern `^[a-z][a-z0-9-]{1,31}$`); `OpenItem` checks the pattern, and the launcher checks the catalog (`SND-LAUNCH-003`, `not-in-catalog`). `launch.started.sessionId` is bounded by a generic pattern in the event schema (`^[A-Za-z0-9._:-]{1,128}$`) and checked against the entry's `sessionIdPattern` before it is written and again before Resume uses it; the per-target patterns move from the schema to the catalog. `session wrap`'s liveness record gains an optional `sessionId` for `reported` ids (§4). X52's "target ids `claude-code`, `codex`, `cursor`" reads "catalog ids; these three are built in" | P10a |
| L-10 | API §3.4 / UI E-1 | `launch.started` gains (additive) `viewer {kind, label?, outcome: opened\|unconfirmed\|skipped\|failed, code?}`, `attach` (argv built from checked ids, E-16), `catalogSource` (`builtin\|override\|added`) and `argvHash` (review C2, C9). `LaunchView` carries `viewer` and `attach` for the app | P10a |
| L-11 | UI E-28 (profile `agents.*`) | `agents.<id>.stepArgvHash` (optional): the `argvHash` an unattended Step may run for this id when its entry isn't built in (SL8). Doctor prints the current hash beside the approved one when they differ | P10a (profile), engine P8 (use) |
| L-12 | API §6 | `AW_SINDRI_CHILD=1` is set for every Sindri-hosted agent by the host and by `session wrap` (§3), and in every `aw-meta-launch` lane pane (SL-1), so a lane or session running `sindri` is a session caller whatever its CLI | P10a, SL-1 |
| L-13 | `docs/csharp/meta.md` | Launcher rows, the "Claude Code only" statement, the state-files table and the env precedence (§4) follow SL-1 and SL-1b | SL-1, SL-1b |

Nothing else changes in `OpenItem`'s modes, the request, claim and expiry rules, `session wrap`'s heartbeat, the launch
secret, liveness, `session.ended`, `session.usage`, budgets or the needs-you rows.

## 7. T3 Code spike

What the audit found (T3 Code source, `pingdotgg/t3code` main, 2026-10-15; MIT): T3 Code starts and owns its agent
process (Codex's app-server, or the Claude Agent SDK). It is driven from outside through its own MCP server
(`t3_project_list/create`, `t3_thread_launch`, `t3_thread_read`, `t3_thread_wait`) after a one-time OAuth pairing, and
a thread is brought forward with `t3code://threads/<env>/<thread>`. A deep link alone only drafts a prompt.

The spike runs with a scratch project and no Sindri code merged. **The teammate who uses T3 Code runs it on their own
box** (review C14), with the owner watching or reading the evidence. It records the T3 Code version and commit it
proved (review C18); SL-4's probe later reports the installed version and warns outside the tested range.

**The pairing credential** (review C13) lives in `$AW_SINDRI_HOME/auth/t3code.token` (0600, the directory 0700), or
behind a profile secret pointer (X18). It is never in `launcher.json`, argv, events, logs or the spike's evidence; the
evidence (transcripts, command output, screenshots) is passed through `Aw.Redaction`, and screenshots are checked by
hand before they are attached. A T3-hosted session has no launch secret, so it can never `step-report`; its liveness
from `t3_thread_read` is a request Sindri records, not a decision.

| # | Question | Pass when |
|---|---|---|
| T1 | Can a script pair once and then launch a thread with a fixed seed, unattended? | `t3_thread_launch` returns a thread id from a non-interactive client after one pairing |
| T2 | Does the session reach Sindri's MCP endpoint? | The thread's first tool call is `sindri_item` (or the reason it can't be, recorded) |
| T3 | Can Sindri tell the thread is alive and when it ends? | `t3_thread_read`/`wait` gives a state that maps to `session.ended` (`exit` or `lost`) |
| T4 | Can usage be read? | Token counts per thread are readable, or the spike records that `usage: none` applies |
| T5 | Does Claude Code's `UserPromptSubmit` hook fire inside a T3 thread? | Recorded yes or no; no means the first typed turn is not a takeover, so opening stays the only takeover |
| T6 | Does the deep link focus the thread from `open`/`xdg-open`? | The desktop app comes forward on the thread |
| T7 | Is a T3 thread a session caller (review C10)? | `sindri whoami` (or any `sindri` call) run by the thread's agent classifies as a session, because the thread's process carries `AW_SINDRI_CHILD` or the harness probe (API §6) recognises T3's process. Fail blocks SL-4: a thread that the CLI takes for the human is not shipped |
| T8 | Does the teammate get to a live thread (review C14)? | From a fresh pairing, the teammate opens an item and reaches a live T3 thread, or the fallback below, in under 10 minutes; the time is recorded |

**The fallback, written now** (in `docs/csharp/sindri.md`, SL-0): a T3 Code user sets the `print` viewer (or their
terminal's `exec-template`), and `sindri open ITM-…` prints the `tmux -L sindri attach …` command to run in any
terminal, including one inside T3 Code if it has one. The session is the normal tmux-hosted CLI, so liveness, usage,
budgets and takeover all work; T3 Code just isn't the window.

Outcome: a findings note appended to this spec (§11) and an owner decision. If T1–T3 and T7 pass, phase SL-4 builds a
`t3code` adapter as a **viewer that owns its process** with the `none` host: human opens and resumes only (SL4). If
T1, T3 or T7 fails, T3 Code users take the fallback, and the idea stays in the data set.

## 8. Testing

Fits `.agents/rules/csharp-testing.md`:
1. **Contract tests over a fake** (`Aw.Sindri.Testing`): `SessionHostContract` (start → alive; stop → not alive; the
   attach argv names the session; ids are validated before use; `env` reaches the process; `AttachedClients` counts a
   client) and `TerminalViewerContract` (open records one outcome under `FakeTimeProvider`; non-zero exit inside 3 s is
   `failed`; a client inside 3 s is `opened`; none is `unconfirmed`; no secret and no item text in argv or files;
   per-launch files are 0600 and swept), each run against a recording fake and the real adapters that can run
   headless.
2. **Unit:** golden argv per built-in agent and mode; `launcher.json` parsing and merging over the defaults, from the
   shared `tests/fixtures/launcher/` set, each fixture also validated against `schemas/launcher.schema.json` with the
   same verdict; an override without `"override": true` is `SND-LAUNCH-011`; `argvHash` goldens; placeholder expansion
   with spaces, quotes, `$`, newlines and a leading `-` (each stays one argument, a leading `-` is refused); target
   precedence with a skipped default; a `usage: none` target with a budget is refused; SL8 (built-in passes, an added
   entry passes only with a matching `stepArgvHash`); the generator's detection under a fake PATH, `$TERM_PROGRAM` and
   no GUI session.
3. **Integration lane:** the real `TmuxSessionHost` on a private socket and `TMUX_TMPDIR`, a stub agent script in place
   of `claude`, the `print` viewer and an `exec-template` viewer that is a script recording its argv; three viewer
   scripts: one that attaches (`opened`), one that exits 0 without attaching (`unconfirmed`), one that exits 1
   (`failed`, session alive and attachable); the stub agent sees `AW_SINDRI_CHILD`, `AW_SINDRI_ITEM` and
   `AW_SINDRI_LAUNCH`; `session wrap`'s guard exits 127 with `SND-LAUNCH-003` for a missing `bin`; `launch.probed` with
   `host`; the whole launch inside E-1's 10 s limit.
4. **Real-app proof** through `scripts/sindri-prove.sh` (§10's proof column).
5. **Bash:** `scripts/tests/meta-launch.test.sh` gains `AW_LAUNCHER_CONFIG` cases (`print`, a recording
   `exec-template`, an unknown key refused with `SND-LAUNCH-011`, `--init-launcher` under a fake PATH, a non-Claude lane
   agent, `AW_META_CLAUDE` winning over the file); SL-1b's cases are in its row.

## 9. What stays out

- No `iterm`, `macos-terminal`, `vscode` or `zellij` adapter in this batch (each is a later small phase).
- No change to who may open, attach or take over (API §4, §6), the launch secret or `session wrap`'s heartbeat.
- No per-repo terminal choice: the terminal belongs to the box. A per-repo agent default is already the profile's.
- No agent-session detection change beyond L-12 (API §6's marker list, CFG-06); it moves to catalog data in a later
  batch.
- The meta's own pane stays Claude Code in this batch: its inbound messages come from its Claude transcript
  (`docs/csharp/meta.md`). The lanes may run any catalog agent (SL6, SL-1b).
- Remote runners (X61b) aren't designed here; `ISessionHost` is where they would fit. Pane capture (X59c) will pass
  through `Aw.Redaction` before anything is stored.
- Each teammate runs their own Sindri, profile copy and `launcher.json` (review C20). One shared Sindri for a team is
  X61. `launch.started` records the viewer kind, the target and the catalog source (L-10), so a later report can count
  which terminals and agents are used.

## 10. Phases

| Phase | What | Where | Size | Needs | Proof | Lane |
|---|---|---|---|---|---|---|
| **SL-0** | The `launcher.json` schema and data files, the doc, and the regenerated `docs/sindri/profile.md` (L-8): `schemas/launcher.schema.json`, `config/launcher/builtins.json`, `config/launcher/terminals.json` (the worked examples), the shared fixtures, the T3 fallback doc (§7); `AW_LAUNCHER_CONFIG` in `.env.example`; the file edges from the bash and C# readers to the schema and data files and to `launcher.json` in `deps.yaml` | `schemas/`, `config/launcher/`, `tests/fixtures/launcher/`, `docs/csharp/sindri.md`, `docs/sindri/profile.md`, `.env.example`, `deps.yaml` | S | this spec approved | Every fixture validates against the schema with its expected verdict (a small check run in CI); `profile.md` matches `SindriProfile`'s keys (a test lists the schema's keys and fails on a key the doc lacks); `scripts/check-env-example.sh` and `node scripts/check-deps.mjs` pass | ideas |
| **SL-1** | `aw-meta-launch` reads `launcher.json`: viewer `warp` (today's layout), `exec-template` (one window per lane) or `print` (one `tmux attach -t =<team>-<lane>` line per lane); the lanes' agent from `meta.lanesAgent` and `meta.lanes.<lane>` through the catalog (`open`/`resume` argv, `{name}`, `processName` for `claude_in_pane`, `transcripts` for `has_transcript`, `reportedId` for the lane's session id); keys and placeholders checked against the schema file; env precedence (§4); `AW_SINDRI_CHILD=1` exported in every lane pane (L-12); `--init-launcher`. A non-Claude lane launches and `--dry-run` and the launch print "lane <l>: <agent> takes part in /meta's obligations from SL-1b". `install-meta.sh`'s Claude-only check stays until SL-1b. `docs/csharp/meta.md` (L-13) | `scripts/meta-launch.sh`, `scripts/meta-lane.sh`, `scripts/tests/meta-launch.test.sh`, `docs/csharp/meta.md` | S | SL-0 | `meta-launch.test.sh` passes the new cases; on a box with no Warp, `aw-meta-launch --dry-run` with a kitty `exec-template` prints one kitty argv per lane; `aw-meta-launch --init-launcher` under a PATH holding only kitty writes the kitty template; with no file, the owner's Warp layout is byte-identical to today's | ideas |
| **SL-1b** | **Non-Claude Meta lanes** (owner, 2026-10-15). (1) `install-meta.sh` accepts any installed provider: it publishes and links `aw-meta`/`aw-meta-launch` for every provider and installs the lane hooks per provider; the Claude-only check (`scripts/install-meta.sh:32-35`) goes. (2) Lane identity and liveness come from the catalog: a lane source beside today's `~/.claude/sessions` reader (`Aw.Meta.Core/Lanes/LaneSource.cs`, `LiveSession.cs`) reads `lane-sessions/<lane>`, the pane's `processName` and the `transcripts` glob, so `lanes-refresh` and the lane map see a Codex or Cursor lane; `role-set` stays the Claude path. (3) Lane messaging through aw-meta's own files: the meta's sends to a non-Claude lane go to that lane's inbox file under `$AW_STATE_DIR/meta/`, and the lane's replies go through an aw-meta command that records them where `inbox-sync`/`stop-check` (`Aw.Meta.Features/SyncInbox`, `StopCheck`) already read peer messages, since Claude's cross-session `SendMessage` (and `outbox-record`'s PostToolUse payload, `Aw.Meta.Features/OutboxRecord`) isn't available to other CLIs. An idle lane is woken by one fixed line sent to its pane with `tmux send-keys`. (4) The lane reads its inbox at its provider's prompt and stop hooks, and the Stop/obligation gate runs there, through the existing adapters `config/hooks/adapters/codex.sh` and `cursor.sh` (UserPromptSubmit / `beforeSubmitPrompt`, Stop / `stop`), as a new lane hook script beside `meta-stop-gate.sh`/`meta-outbox.sh`, which the adapters' README maps today as unmapped for Codex and Cursor. (5) A lane whose CLI has no hook support is "launch-only: no obligations" in `aw-meta health` and the lane map. **Conditions (the meta, 2026-10-15):** (a) every existing aw-meta command, flag and exit code, and the Claude hook contract (`meta-stop-gate.sh`, `meta-outbox.sh` and their JSON), stays unchanged: non-Claude support is additive only; (b) the pre-ready run, through the heavy lock, includes `Aw.Meta.Tests` unit and integration, `Aw.Architecture.Tests`, `bash src/Meta/aot-smoke/run.sh`, and the bash suites `meta-hooks`, `meta-launch` and `install-meta`; (c) the lane never installs aw-meta itself; the meta reinstalls after merge | `scripts/install-meta.sh`, `src/Meta/…` (Lanes, the lane inbox and reply slices), `config/hooks/` (the lane hook), `config/hooks/adapters/README.md`, `docs/csharp/meta.md`, their tests | M–L | SL-1 | A team with one Claude lane and one Codex lane: the Codex lane appears in the lane map; it receives a message from the meta (at its next prompt, or woken while idle); its stop is gated while it owes a reply and released after it replies; the meta's `stop-check` sees the reply. A lane on a CLI with no hooks shows "launch-only: no obligations" in `aw-meta health`. The Claude lane's behaviour and every existing command's output are byte-identical to before (condition a). The full run of condition (b) passes; nothing in the run installs aw-meta (condition c) | ideas |
| **SL-2** | The three ports and `launcher.json` inside P10a (L-1…L-5, L-9…L-12): `IAgentCatalog` with the embedded built-ins, `TmuxSessionHost` (env, `AttachedClients`), `WarpViewer`, `ExecTemplateViewer`, `PrintViewer`, the config reader against the shared fixtures, `sindri launcher init` and `show`, SL8, `SND-LAUNCH-011`/`-012`, the `launch.started` and `launch.probed` additions, the `TriggerNames.cs:60` comment | `src/Sindri/Aw.Sindri.Core/Launch/`, `…Infrastructure/Launch/`, `…Host/Commands/` | M (inside P10a's L) | API P10a's own needs (P3 + P8), SL-0 | P10a's proofs unchanged with no `launcher.json`; plus `sindri-prove.sh human open ITM-…` with an `exec-template` viewer under `xvfb-run xterm` opens a window titled `sindri ITM-…` with one tmux client attached and `launch.started.viewer.outcome: opened`; a viewer script that exits 0 without attaching records `unconfirmed` and the session stays attachable; with `print`, `open` exits 0 and prints the attach command, and attaching by hand reaches the live agent, whose environment has `AW_SINDRI_CHILD=1`; `sindri launcher init` on that box writes a file the next open uses | API |
| **SL-3** | UI copy and states for any terminal (L-7) | `Views/Launch*`, Settings | S (inside U6a) | SL-2 | U6a's proof with the `print` viewer shows "Session started: run `<attach>`" and the copy button takes focus and announces "Copied"; an invalid `launcher.json` replaces the button before the click with the file's line; a profile default this box lacks resolves to the next rule with the reason in the menu; Settings shows the terminal; with Warp, unchanged | UI |
| **SL-T** | T3 Code spike (§7): T1–T8 answered on the teammate's box | a scratch dir outside the repo; findings in §11 | S | the teammate pairs T3 Code once | The §7 table filled with evidence (commands, ids, redacted screenshots, the T3 version) and an owner decision | ideas |
| **SL-4** | `t3code` viewer with the `none` host, if SL-T passes | `…Infrastructure/Launch/T3CodeViewer.cs` | M | SL-2, SL-T + owner decision | `sindri-prove.sh human open ITM-…` with `viewer.kind: t3code` starts a T3 thread whose first call is `sindri_item`; `sindri` from inside the thread is a session caller; ending the thread writes `session.ended`; Attach isn't offered; the pairing token is only in `auth/` | API |
| **SL-5** | Agent runtime: `Attended: false`, refuses `none` hosts and unapproved entries (L-6, SL8) | engine P8 | XS (inside P8) | SL-2 | Evals: a Step whose target's host is `none` parks with its code and never launches; a Step on an added entry without a matching `stepArgvHash` parks with `SND-LAUNCH-012`; with the hash approved, it launches | engine |

### 10.1 Parallelization plan

The ideas lane runs one builder at a time; the other lanes take their edits into phases they already own.

| Wave | Ideas lane | API lane | UI lane | Engine lane | Owner and teammate |
|---|---|---|---|---|---|
| 0 | SL-0 (schema, data, docs) | reads L-1…L-5 and L-9…L-12 into P10a's plan | reads L-7 into U6a's plan | reads L-6 and L-11 into P8's plan | owner approves this spec; the teammate pairs T3 Code for SL-T |
| 1 | SL-1 (bash), then SL-T with the teammate | P10a with SL-2 folded in | — | — | — |
| 2 | SL-1b (non-Claude Meta lanes); the meta reinstalls aw-meta after its merge | — | U6a with SL-3 | P8 with SL-5 | decides SL-4 from SL-T's findings |
| 3 | — | SL-4 if approved | — | — | — |

SL-0 and SL-1 don't wait on any other lane, so a Claude Code teammate on any terminal can use `aw-meta-launch` before
P10a lands, and a teammate on Codex or Cursor can launch lanes then and take part in /meta's obligations once SL-1b
merges. SL-1b and SL-2 touch no common files and run in parallel. SL-2 doesn't wait on SL-T: the `none` host is in the
port from the start, and only its T3 adapter waits.

## 11. Open questions

| # | Question | Answer |
|---|---|---|
| Q1 | May user-added catalog entries run without profile approval? | Provisional: yes for **human opens**: they run as the user, in the user's terminal, the same as typing the command. The profile still decides which agent is the default, and budgets still apply (L-5). **Unattended Steps** run only built-in entries or entries whose `argvHash` the profile approves (SL8) |
| Q2 | Should a T3 Code user be able to attach to factory Steps? | Provisional: no. Steps stay tmux-hosted (SL4). T3 Code covers human opens and resumes |
| Q3 | Default viewer with no `launcher.json`? | Provisional: `warp` when present, else `print`, resolved once per launcher service start (§4), so the owner's setup is unchanged and a headless box works |
| Q4 | Does `aw-meta` keep bash for launching or move to the C# `ILauncher`? | Provisional: bash with `jq` in SL-1, reading the shared schema and built-ins files (§4); revisit when aw-meta and Sindri share a launcher assembly |
| Q5 | May Meta lanes run non-Claude agent CLIs (review C4)? | **Decided (owner, 2026-10-15): yes**, "also allow non-claude agents in them". SL-1 launches them; SL-1b makes them take part in /meta's obligations through aw-meta's own inbox and outbox files and the per-provider hook adapters. The meta's own pane stays Claude Code in this batch (§9) |

## 12. Review notes (/autoplan pass 1, 2026-10-15)

Findings from `consolidated-review.md` (product, arch, design, devex, security) and what changed.

| Finding | What changed (section) |
|---|---|
| C1 (HIGH) closed target enum | L-9 opens `target`, `--agent`, `launch.started.target` and X52 to `AgentId`, checked against the catalog; `sessionIdPattern` per entry; N-agent picker in L-7 (SL3, §4, §6) |
| C2 (HIGH) viewer failure vs `launch.failed` | `launch.started` gains `viewer` and `attach` (L-10); `-001`/`-009` re-classed for tmux hosts, kept as `launch.failed` for `none` hosts (L-3); §5's last column is now the viewer outcome; L-7 copy (SL5, §5, §6) |
| C3 (HIGH) 10 s viewer wait | 3 s observation window plus a `tmux list-clients` check; `unconfirmed` outcome; the budget fits E-1's 10 s (§3, §5, §8) |
| C4 (HIGH) non-Claude Meta lanes | Owner decided: allowed. SL6 and SL-1 let `meta.lanesAgent` and `meta.lanes.<lane>` name any catalog agent; new phase SL-1b with the meta's three conditions; §11 Q5 decided (SL6, §4, §9, §10, §10.1, §11) |
| C5 (HIGH) hand-written config | `sindri launcher init` and `aw-meta-launch --init-launcher` detect the terminal and write the file; doctor's fix text names it; proofs in SL-1 and SL-2 (§4, §10) |
| C6 (HIGH) `-011` and disabled default in the UI | §6.6 row for `SND-LAUNCH-011` before the click; a default missing from this box falls through to the next precedence rule (§4, L-7) |
| C7 (MEDIUM) unapproved argv in Steps | SL8, `SND-LAUNCH-012`, `agents.<id>.stepArgvHash` (L-11), SL-5's evals; Q1 split by attended (§1, §6, §10, §11) |
| C8 (MEDIUM) silent overrides | `"override": true` required; doctor and `launch.probed` show overrides (§4, L-4) |
| C9 (MEDIUM) no record of what ran | `catalogSource` and `argvHash` on `launch.started` (§2, L-10) |
| C10 (MEDIUM) agent env and guard | `ISessionHost.Start(env)` and `session wrap` set `AW_SINDRI_CHILD`/`ITEM`/`LAUNCH` and keep the `command -v` guard; SL-1 exports `AW_SINDRI_CHILD` in lane panes (L-12); spike T7 (§3, §6, §7) |
| C11 (MEDIUM) two parsers, missing fields | One `schemas/launcher.schema.json` both readers check, shared fixtures and built-ins file; `processName`, `transcripts`, `{name}`, `reportedId` added (§4, SL-0, SL-1) |
| C12 (MEDIUM) env vs file precedence | env > file > defaults; Meta's `AW_META_CLAUDE*` and `AW_WARP_LAUNCH_DIR` ranked; the first two deprecated for users (§4, L-13) |
| C13 (MEDIUM) T3 pairing credential | `$AW_SINDRI_HOME/auth/t3code.token` 0600 or a secret pointer; redacted from evidence (§7) |
| C14 (MEDIUM) spike on the wrong box | The teammate runs SL-T; T8 times the path to a live thread; the fallback is written now (§7, §10) |
| C15 (MEDIUM) voice, `print` copy, a11y, Settings | Copy names the viewer's label; `print` and `lastStarted` copy; live region and focus for the copy button; read-only terminal in Settings (L-7) |
| C16 (MEDIUM) `attended` undefined | Defined in §2 and L-1 (`LaunchRequest.Attended`) |
| C17 (LOW) files without a phase | `.env.example`, `deps.yaml` in SL-0; `docs/csharp/meta.md` in SL-1/SL-1b (L-13); `TriggerNames.cs:60` in L-2 and SL-2 |
| C18 (LOW) leading `-`, `-e`, perms, T3 version | §4's option-smuggling rule and owner/parent-dir checks; the T3 version pinned in §7 |
| C19 (LOW) default viewer flips | Resolved once per service start; `terminal.source` (§4, L-4) |
| C20 (LOW) one Sindri per teammate | One line in §9; usage counted from `launch.started` (L-10) |
