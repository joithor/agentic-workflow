# Meta — `aw-meta`

`aw-meta` is the helper CLI the `/meta` skill drives a **meta orchestrator** through: the session that gates and merges
a team's PRs, relays between lane sessions and the owner, and keeps the box healthy. Its job is to make that role
impossible to fail by forgetting. Every inbound lane message is recorded and owes a reply, the Stop hook won't let the
session idle while one is owed, and the lane map, merge gate, owner queue, health check and running log are commands,
not habits.

It is a portable harness for any project. Everything team-specific (lanes, repo, gate scopes, merge order, known
flakes, kickoff prompts, log file) lives in a team config. It runs before the C# switch as an opt-in standalone
NativeAOT CLI (`./setup.sh --with-meta`, Claude Code only) that writes only under `$AW_STATE_DIR/meta/` and the
configured running log. Sindri absorbs it later (see the last section).

Source: `src/Meta` (full shape: `Core`, `Features`, `Infrastructure`, `Host`; NativeAOT binary `aw-meta`).
Tests: `tests/Aw.Meta.Tests` (rules, slices, contract bindings) and `tests/Aw.Meta.Testing` (fakes and contract bases).
AOT smoke: `bash src/Meta/aot-smoke/run.sh`. Shell side: `config/hooks/meta-stop-gate.sh`, `config/hooks/meta-outbox.sh`,
`scripts/meta-launch.sh`, `scripts/meta-lane.sh`, `scripts/install-meta.sh`; skill `skills/meta/SKILL.md`.

## Pieces

| Piece | What it does |
|-------|--------------|
| `aw-meta` | State in `$AW_STATE_DIR/meta/meta.db` (SQLite, WAL): messages, sends, lane map versions, gates, merges, owner questions |
| Stop hook `aw:meta-stop-gate` | Runs `aw-meta stop-check` with the session id and transcript path; exit 1 from it blocks the stop (hook exit 2, the list on stderr) |
| PostToolUse hook `aw:meta-outbox` (matcher `SendMessage`) | Pipes the hook payload to `aw-meta outbox-record`, which records the send and closes what it settles |
| `aw-meta-launch` (`scripts/meta-launch.sh`) | One Warp window, a pane per lane, each attached to tmux session `<team>-<lane>` running `scripts/meta-lane.sh` |
| `scripts/meta-lane.sh` | Runs the lane's Claude session as `claude -n <team>-<lane>`, resumed by its recorded session id, or fresh with `<prompts>/<lane>.md` |

## Why the transcript

No Claude Code hook sees an inbound cross-session message, so `stop-check` reads them from the meta session's transcript
(Claude Code 2.1.300). A message delivered while the session is idle is a `type:"user"` record with
`origin.kind == "peer"` (`from`, `name`, `msg_id`, `body`). A message that arrives mid-turn is a `type:"attachment"`
record with `attachment.type == "queued_command"` and the same `attachment.origin`. Both are recorded once, keyed on
`msg_id` (else the attachment's `delivery_id`). `stop-check` reads from a stored byte offset, so each stop costs only
the new lines.

Sends are seen by the PostToolUse hook: `tool_input.to`, `tool_input.message`, and `msg_id` in the tool response.

## Obligations

An obligation is something the meta owes. They are derived on each `stop-check` from the recorded messages, sends,
merges, lane map versions and owner answers, not stored as state.

| Kind | Opened by | Closed by |
|------|-----------|-----------|
| `reply` | every inbound lane message | a later send to that peer; `inbox-dismiss <id> --reason`; `inbox-defer <id> --until` (reopens when due) |
| `broadcast-lanes` | a new lane map version | a send of the map text to every mapped lane |
| `announce-main` | `merge-run` | a send containing the new main sha to every mapped lane |
| `relay-owner` | `owner-answer` | a send of the `OWNER ANSWER` text to the asking lane |
| `rearm-watch` | a started watch whose heartbeat is older than two intervals | `watch` running again |

`stop-check` exits 0 silently for any session that isn't the registered meta session (`role-set`), so the hook is safe
to install globally. Loop guard: when the same open set has blocked `AW_META_STOP_MAX` (default 3) stops in a row with
`stop_hook_active`, it lets the stop through with a one-line warning instead of looping the session.

## Commands

Every command takes `--json`. Exit 0 ok, 1 attention (read the output and act), 2 usage or tool error; an error is one
stderr line `AW-META-NNN: <reason>`. Every command but `team-init`, `role-*`, `health`, `stop-check` and `outbox-record` needs
the team config (`AW-META-002` otherwise); the two hook commands must let any session through when no team is set up.
`stop-check` prints what is owed on stdout (the hook moves it to stderr for Claude Code).

| Command | What it does |
|---------|--------------|
| `team-init --team <t> --repo <abs dir> --lanes a,b [--meta-lane meta] [--log <file>] [--force]` | Writes a starter team config (`--force` to overwrite one) |
| `team-show` | Prints the team config |
| `role-set --session <id> [--name <n>]` / `role-show` | Registers the meta session (the launcher does this for the meta pane) |
| `inbox-sync --session <id> --transcript <path>` | Records new peer messages from the transcript |
| `inbox [--all]` | Open reply obligations (or every message) |
| `inbox-dismiss <id> --reason <r>` / `inbox-defer <id> --reason <r> --until <iso8601\|+30m>` | Settles a reply without sending one |
| `outbox-record` | Stdin: the PostToolUse payload. Records a send and closes what it settles |
| `stop-check --session <id> --transcript <path> [--hook-active]` | Syncs, refreshes the lane map, prints every open obligation with the command that settles it; exit 1 if any |
| `lanes-refresh` / `lanes [--announce]` | Rebuilds the lane map from live sessions named `<team>-<lane>` (`~/.claude/sessions`), then lane announcements ("API lane: …"); `lanes` refreshes first; `--announce` prints the `LANE MAP v<n>:` text |
| `gate-run <pr> [scope...]` | Runs the merge gate in a gate tree (PR head merged onto `origin/main`); scopes come from the changed paths and the team config |
| `gate-rerun <pr> --step <s> --test <filter>` | Reruns one red test alone and on plain main; records `flake` or `real` |
| `merge-check <pr>` | The merge preconditions: gate green (or reds all flakes), head and main unmoved since the gate (a docs-only delta needs only the `always` scopes), merge order, not a draft |
| `merge-run <pr>` | `merge-check`, then `gh pr merge <pr> --merge`, logs it and opens `announce-main` with the "main moved to <sha>" text |
| `owner [--all]` / `owner-answer <q> --answer <t>` / `owner-ask --text <t>` | The owner question queue (inbound messages starting `OWNER QUESTION`); an answer is dated in the decision log and owed back to the lane |
| `health [--kill-orphans]` | Memory by group, orphaned Temporal dev servers, duplicate MCP server copies, lanes with no live session, heavy-lock holders; `--kill-orphans` kills only orphans by the rule below, by pid |
| `watch [--interval <s>] [--for <dur>]` | Prints one line per settled change (PR opened, merged, ready, head moved, mergeable after two equal polls and never `UNKNOWN`, main moved, disk low) and keeps a heartbeat |
| `log-append --kind merge\|decision\|lanes\|incident\|note --text <t>` | Appends a dated line to the running log |
| `status` | Obligations, merge queue, owner queue, lanes |

The orphan rule: the command line has `temporal` and `server start-dev`, its `--db-filename` is under `/tmp/aw-sindri-`,
its parent is `systemd --user` or init, and it is older than five minutes.

## Team config

`$AW_STATE_DIR/meta/team.json` (or `AW_META_CONFIG`). Unknown fields are ignored. Example:
`skills/meta/examples/agentic-workflow.team.json`.

| Field | Meaning |
|-------|---------|
| `team`, `metaLane`, `lanes` | Session and tmux names are `<team>-<lane>` |
| `repo` | Absolute path of the shared checkout (Warp needs an absolute cwd) |
| `prompts` | Directory of `<lane>.md` kickoff prompts for fresh lane sessions |
| `log` | The running log (default `$AW_STATE_DIR/meta/log.md`) |
| `gate.always`, `gate.docsOnly`, `gate.scopes.<name>.{paths,steps}` | The merge gate. A step is `{name, run: argv, heavy}`; `heavy` runs it through the heavy-job wrapper |
| `mergeOrder` | `[{before, after}]`: `after` can't merge while `before` is open |
| `knownFlakes` | Test names a red is checked against before it counts as real |

## Files

| Path | Written by |
|------|-----------|
| `$AW_STATE_DIR/meta/meta.db` | `aw-meta` |
| `$AW_STATE_DIR/meta/team.json` | `team-init` (then the owner) |
| `$AW_STATE_DIR/meta/gates/<pr>/` | `gate-run`, `gate-rerun` (tree and step logs) |
| `$AW_STATE_DIR/meta/lane-sessions/<lane>` | `scripts/meta-lane.sh` (the lane's Claude session id) |
| the running log | `aw-meta` (merges, owner decisions, lane maps, kills, `log-append`) |
| `$XDG_DATA_HOME/warp-terminal/launch_configurations/<team>-team.yaml` | `scripts/meta-launch.sh` |

## Threat model

`aw-meta` guards against an honest orchestrator forgetting, not against a deliberately hostile one: the session can
always dismiss an obligation, and the loop guard lets a stuck stop through. The hooks fail open (`HOOK-FAILOPEN`,
`.agents/rules/hooks.md`): a missing or broken `aw-meta` never blocks a session. Inbound message bodies are lane text,
stored and printed but never executed. `health --kill-orphans` kills by pid only after the full rule matches; it never
matches a pattern against every process.

## Sindri absorbs it

| aw-meta | Sindri (planned) |
|---------|------------------|
| `meta-launch.sh`, `meta-lane.sh` | `ILauncher` (API P10a/b, engine P8) |
| obligations, owner queue | needs-you (API P4, P11) |
| `watch`, `gate-run`, `merge-check`, `merge-run` | the PR babysitter (factory addendum §17.9–17.10) |

Until then this package stays thin: no HTTP, no daemon, one SQLite file.
