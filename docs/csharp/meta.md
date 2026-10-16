# Meta — `aw-meta`

`aw-meta` is the helper CLI the `/meta` skill drives a **meta orchestrator** through: the session that gates and merges
a team's PRs, relays between lane sessions and the owner, and keeps the box healthy. Its job is to make that role
impossible to fail by forgetting. Every inbound lane message is recorded and owes a reply, the Stop hook won't let the
session idle while one is owed, and the lane map, merge gate, owner queue, health check and running log are commands,
not habits.

It is a portable harness for any project. Everything team-specific (lanes, repo, gate scopes, merge order, known
flakes, kickoff prompts, log file) lives in a team config. It runs before the C# switch as an opt-in standalone
NativeAOT CLI (`./setup.sh --with-meta`, Claude Code only) that keeps its state under `$AW_STATE_DIR/meta/` and
appends to the configured running log. Its gate and merge commands act on the team's checkout and PRs: `git fetch`, a
gate worktree registered in the repo, the configured gate steps, `gh pr merge`, and the removal of finished worktrees (`git worktree remove`, never forced; see "Worktree cleanup"). Sindri absorbs it later (see the
last section).

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
to install globally. Loop guard: a stop-check without `stop_hook_active` starts a new turn (it blocks and resets the count); each
continuation with `stop_hook_active` and the same open set counts one block, and after `AW_META_STOP_MAX` (default 3) of
them the next continuation is let through with a one-line warning instead of looping the session. Every turn gets the
full allowance again.

`/clear` in the meta pane gives it a new session id under the same `-n` name. `stop-check` and `outbox-record` treat a
session as the meta when its id is the registered one, or when the live session with that id in the sessions directory
(`AW_CLAUDE_SESSIONS_DIR`, `<pid>.json`) carries the registered name; the new id is then registered (as `role-set` would)
and the hook carries on.

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
| `gate-rerun <pr> --step <s> --test <filter>` | Reruns one red test alone and on plain main (the main tree is built first: `--no-build` is dropped there); records `flake` or `real` for that test. A filter that matches no test on the gate tree is an error (AW-META-001), whatever `--ignore-exit-code` the step carries; on main it means the test is not there (a test the PR adds), so a test that fails alone is `real` |
| `merge-check <pr>` | The merge preconditions: gate green (or every failing test of every red step, read from the step log's `failed <name>` lines, or from the bash runner's `FAIL  par|ser  <suite>  ...  exit N` lines (suite paths, deduplicated), was rerun and classified a flake; a log that can't be read, or whose summary `failed: N` differs from the number of `failed <name>` lines (a crash or timeout names no test), refuses), the gate ran every scope its diff needs (every gate run of the PR at the same head and the same main counts: a scope is judged by its newest such run, so `gate-run <pr> <scope>` after a run with one flaky step completes it; a scope whose newest run failed stays red even if an earlier run passed it; runs at another head or main never count; the output names the run each scope came from, e.g. `repo (run 1), dotnet (run 2)`), head and main unmoved since the gate (a docs-only delta needs only the `always` scopes), merge order, not a draft |
| `merge-run <pr>` | `merge-check`, then `gh pr merge <pr> --merge`, logs it and opens `announce-main` with the "main moved to <sha>" text. The merge is recorded as soon as gh succeeds: main's sha comes from a fetch, else from `gh pr view --json mergeCommit`, else it stays unknown ("main moved (#N merged)"). Running it again on a pull request that is already merged with no merge recorded records it and opens `announce-main` |
| `owner [--all]` / `owner-answer <q> --answer <t>` / `owner-ask --text <t>` | The owner question queue (inbound messages starting `OWNER QUESTION`); an answer is dated in the decision log and owed back to the lane |
| `health [--kill-orphans]` | Memory by group, orphaned Temporal dev servers, duplicate MCP server copies, lanes with no live session, heavy-lock holders; `--kill-orphans` kills only orphans by the rule below, by pid |
| `watch [--interval <s>] [--for <dur>]` | Prints one line per settled change (PR opened, merged, ready, head moved, mergeable after two equal polls and never `UNKNOWN`, main moved, disk low) and keeps a heartbeat |
| `worktree-list [--no-fetch]` | Every worktree of the team repo (`git worktree list --porcelain`) with its branch, head, pull request (number and state) and verdict `removable` or `kept` with every reason; changes nothing (it fetches origin first unless `--no-fetch`). Exit 0 |
| `worktree-sweep [--dry-run] [--pr <n>]` | Removes the removable worktrees (see "Worktree cleanup"), all or just pull request n's (its head branch and its gate trees); prints each removed and each kept one with its reasons. `--dry-run` removes nothing. Exit 0, 1 when git refused a removal, 2 when nothing could be judged (git, gh or the process scan failed: nothing is removed) |
| `log-append --kind merge\|decision\|lanes\|incident\|note --text <t>` | Appends a dated line to the running log |
| `status` | Obligations, merge queue (ready pull requests; one leaves it when any merge is recorded for it, or when the watch saw it merged or closed on GitHub, kept under `watch.ended-prs`), owner queue, lanes |

The orphan rule: the command line has `temporal` and `server start-dev`, its `--db-filename` is under `/tmp/aw-sindri-`,
its parent is `systemd --user` or init, and it is older than five minutes.

## Worktree cleanup

Worktrees pile up (a gate tree per pull request, a worktree per lane or builder). `aw-meta` removes the finished ones
itself, on **triggers**, never on a timer: work builds up over time, so how long a worktree has sat there is no reason to
remove it, and nothing in the rules reads a file time or a commit date.

**Triggers.** Each one sweeps only the worktrees that belong to the pull requests it concerns (their head branches and
their gate trees `gates/<pr>/tree` and `gates/<pr>/main-tree`), then logs one `NOTE` line to the running log ("worktrees
merge-run #7: removed 2 (...); kept 1 (path: reasons)") and keeps it as `worktrees.last`.

| Trigger | When |
|---------|------|
| `merge-run <pr>` | after a successful merge (also when it records a merge that already happened); the line is part of its output. A sweep that cannot run never fails the merge |
| the watch | when a poll sees a pull request merged or closed that is not yet in `watch.ended-prs`; one `WORKTREES watch #n: ...` line. A sweep that could not run (GitHub or git unreachable) leaves the pull request in `worktrees.pending-prs` and the next poll tries again |
| `worktree-sweep` | by hand, e.g. when a lane reports a builder done; also the way to retry a worktree that was kept for a reason that has since gone (a process exited, a commit was pushed) |

With `worktrees.enabled: false` the two automatic triggers only report ("would remove"); `worktree-sweep` still removes.

**The rules.** A worktree is `removable` only when all of these hold; any other is `kept` with every failing reason:

1. Merged: its HEAD is reachable from `origin/main` (a fetch precedes every sweep), or its pull request is CLOSED and nothing is unpushed. A detached worktree counts only when its HEAD is reachable from `origin/main`. A branch whose pull request is still open is kept even when its head is merged.
2. Clean: `git status --porcelain --untracked-files=all` is empty, ignoring only untracked files under a directory named like a build output (`worktrees.buildOutputs`, default `bin`, `obj`, `artifacts`, `node_modules`, `dist`). Any other untracked file, and any change to a tracked file, keeps it.
3. Nothing unpushed: every commit of its HEAD is reachable from a remote branch (`git rev-list HEAD --not --remotes` is empty).
4. Idle: no live process has its working directory or an open file inside it (`/proc/<pid>/cwd` and `fd` on Linux, `lsof` where there is no `/proc`). This covers a lane or builder still sitting in it. A scan that cannot run fails the sweep: nothing is removed.
5. Not protected: not the main worktree, not the team's shared checkout (`repo`), not locked (`git worktree lock`), its directory not missing.
6. Under an allowed root: `worktrees.roots` (default `~/repos/aw-wt`, `<repo>/.claude/worktrees`), plus the gate directory `$AW_STATE_DIR/meta/gates` always. A path outside every root, a root itself, or a sibling that shares a root's name prefix is kept.

A **gate tree** (`gates/<pr>/tree`, `gates/<pr>/main-tree`) is aw-meta's own throwaway checkout, so rule 1 reads: its pull request is merged or closed (looked up on GitHub; unknown or open keeps it), and its HEAD is reachable from `origin/main` or is the merge commit the gate made itself (committer `aw-meta@localhost`); rule 3 does not apply. It is also kept while a `gate-run`, `gate-rerun` or `merge-run` of that pull request runs (found by command line, leaving out `aw-meta` itself and its parent shells).

**How it removes.** One worktree at a time, judged again just before its removal (a process may have started since), with
`git worktree remove <path>`: never forced and never a recursive delete, so git itself refuses a worktree that is not clean
(an untracked `obj/` that is not in `.gitignore`, for one); that refusal keeps the worktree and is reported (`git refused to
remove it: ...`, exit 1 for an explicit sweep). `git worktree prune` runs after any removal. **Branches are never deleted.**
A worktree git cannot read is kept with the reason.

## Team config

`$AW_STATE_DIR/meta/team.json` (or `AW_META_CONFIG`). Unknown fields are ignored. Example:
`skills/meta/examples/agentic-workflow.team.json` (its `repo`, `prompts` and `log` are placeholders: set absolute paths, since they are not expanded).

| Field | Meaning |
|-------|---------|
| `team`, `metaLane`, `lanes` | Session and tmux names are `<team>-<lane>` |
| `repo` | Absolute path of the shared checkout (Warp needs an absolute cwd) |
| `prompts` | Directory of `<lane>.md` kickoff prompts for fresh lane sessions |
| `log` | The running log (default `$AW_STATE_DIR/meta/log.md`) |
| `gate.always`, `gate.docsOnly`, `gate.scopes.<name>.{paths,steps}` | The merge gate. A step is `{name, run: argv, heavy, rerun?}`; `heavy` runs it through the heavy-job wrapper (`AW_HEAVY`, used when the file exists); a `run[0]` with a `/` that is not absolute (`scripts/check.sh`) is resolved against the gate tree, not aw-meta's own directory (a bare name goes through PATH); `rerun` is the argv `gate-rerun` uses, with a `{test}` placeholder (default for a `dotnet test` step: its argv plus `--filter-method *<test>*`). Step names are unique across scopes |
| `mergeOrder` | `[{before, after}]`: `after` can't merge while `before` is open |
| `worktrees` | `{roots?, buildOutputs?, enabled?}` for the worktree cleanup (see that section): the directories whose worktrees may be removed (`~` is the home directory), the directory names of untracked build output that don't keep a worktree, and `enabled` (default true; false makes the merge-run and watch triggers only report) |
| `knownFlakes` | Test names (or Class.Method, or a class) a red is checked against before it counts as real; matched as whole dot-separated segments, never as a substring |

## Files

| Path | Written by |
|------|-----------|
| `$AW_STATE_DIR/meta/meta.db` | `aw-meta` |
| `$AW_STATE_DIR/meta/team.json` | `team-init` (then the owner) |
| `$AW_STATE_DIR/meta/gates/<pr>/` | `gate-run`, `gate-rerun`: `tree` (the gate tree, reused per PR), `main-tree` (plain main for reruns), `<run>/<step>.log` |
| `$AW_STATE_DIR/meta/lane-sessions/<lane>` | `scripts/meta-lane.sh` (the lane's Claude session id) |
| `$AW_STATE_DIR/meta/lane-sessions/<lane>.exited` | `scripts/meta-lane.sh` when the lane's claude exited and the pane fell back to its shell; `meta-launch.sh` restarts such a pane unless a claude runs in it again |
| the running log | `aw-meta` (merges, owner decisions, lane maps, kills, worktree sweeps, `log-append`) |
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

`gh` is called as `AW_GH` (default `gh`; tests point it at a stub).

Until then this package stays thin: no HTTP, no daemon, one SQLite file.
