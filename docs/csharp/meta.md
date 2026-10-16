# Meta — `aw-meta`

`aw-meta` is the helper CLI the `/meta` skill drives a **meta orchestrator** through: the session that gates and merges
a team's PRs, relays between lane sessions and the owner, and keeps the box healthy. Its job is to make that role
impossible to fail by forgetting. Every inbound lane message is recorded and owes a reply, the Stop hook won't let the
session idle while one is owed, and the lane map, merge gate, owner queue, health check and running log are commands,
not habits.

It is a portable harness for any project. Everything team-specific (lanes, repo, gate scopes, merge order, known
flakes, kickoff prompts, log file) lives in a team config. It runs before the C# switch as an opt-in standalone
NativeAOT CLI (`./setup.sh --with-meta`; the meta and its obligations are Claude Code only, lanes can launch on other agent CLIs, see [Launcher](#launcher); Linux only for now, aw-meta reads /proc, so on macOS setup skips it) that keeps its state under `$AW_STATE_DIR/meta/` and
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
| `aw-meta-launch` (`scripts/meta-launch.sh`) | By default one Warp window, a pane per lane, each attached to tmux session `<team>-<lane>` (on the team's own socket, `tmux -L <team>`) running `scripts/meta-lane.sh`; `launcher.json` can pick another viewer and other lane agents ([Launcher](#launcher)) |
| `scripts/meta-lane.sh` | Runs the lane's agent (Claude Code unless `launcher.json` says otherwise) as `claude -n <team>-<lane>`, resumed by its recorded session id, or fresh with `<prompts>/<lane>.md` |

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
stderr line `AW-META-NNN: <reason>`. Every command but `team-init`, `role-*`, `health`, `stop-check`, `outbox-record` and the lane-side `lane-inbox`, `lane-stop-check` and `lane-reply` needs
the team config (`AW-META-002` otherwise); the hook commands must let any session through when no team is set up.
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
| `lane-send --lane <l> (--text <t> \| --file <f>)` | The meta's send to a **non-Claude** lane (Codex, Cursor, an added agent): stores the message in the lane's inbox, records it as a send to `<team>-<lane>` (which closes the meta's reply obligation as `SendMessage` does), and wakes the lane when all three guards hold (see "Non-Claude lanes"). Exit 0 sent; 1 sent, but the lane is launch-only or its hooks haven't run since launch; 2 unknown lane (`AW-META-016`), a Claude lane (`AW-META-017`: use `SendMessage`) or usage |
| `lane-inbox` | A lane's hook call (lane from `AW_META_TEAM` and `AW_META_LANE`): prints the meta's messages the lane hasn't seen (marks them delivered; they stay owed a reply) and records the heartbeat. Exit 0 (silent when empty or when no team is set up); 2 when the lane variables are unset (`AW-META-014`) |
| `lane-stop-check [--stop-hook-active]` | A lane's stop hook call: exit 1 while the lane owes the meta a reply (prints the owed messages and how to reply), with the loop guard of `stop-check` (`AW_META_STOP_MAX`, default 3); exit 0 otherwise, or with one warning line once the cap lets the stop through. Records the heartbeat as a stop. Exit 2 when the lane variables are unset (`AW-META-014`) |
| `lane-reply (--text <t> \| --file <f> \| --stdin)` | A lane's answer to the meta: records an inbound message `lane:<lane>:<n>` from `<team>-<lane>` (classified like any inbound message), clears what the lane owed, and logs team, lane, provider and session. Exit 0 (silent when no team is set up, or the reply is blank); 2 when the variables are unset (`AW-META-014`) or `lane-sessions/<lane>` has no record (`AW-META-015`) |
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
remove it, and no rule reads a commit date or an age. The one time rule is a change trigger, not an age timer (rule 7).

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

1. Finished: the work is proven finished by an ended pull request, not by reachability (a branch just cut from `origin/main` is reachable too: the 2026-10-16 incident removed a live worktree on such a branch, with no commits and no pull request yet). A worktree on a branch is removable only when that branch's pull request is MERGED (its HEAD is also reachable from `origin/main`; a fetch precedes every sweep) or CLOSED (and nothing is unpushed). A branch with no pull request is always kept ("no pull request: work not finished"), and one whose head is already in `origin/main` with no ended pull request is kept as a "fresh branch, no commits". A branch whose pull request is still open is kept even when its head is merged. A detached worktree that is not a gate tree is always kept ("detached, not a gate tree").
2. Clean: `git status --porcelain --untracked-files=all` is empty, ignoring only untracked files under a directory named like a build output (`worktrees.buildOutputs`, default `bin`, `obj`, `artifacts`, `node_modules`, `dist`). Any other untracked file, and any change to a tracked file, keeps it.
3. Nothing unpushed: every commit of its HEAD is reachable from a remote branch (`git rev-list HEAD --not --remotes` is empty).
4. Idle: no live process has its working directory or an open file inside it (`/proc/<pid>/cwd` and `fd` on Linux, `lsof` where there is no `/proc`). This covers a lane or builder still sitting in it. A scan that cannot run fails the sweep: nothing is removed.
5. Not protected: not the main worktree, not the team's shared checkout (`repo`), not locked (`git worktree lock`), its directory not missing.
6. Under an allowed root: `worktrees.roots` (default `~/repos/aw-wt`, `<repo>/.claude/worktrees`), plus the gate directory `$AW_STATE_DIR/meta/gates` always. A path outside every root, a root itself, or a sibling that shares a root's name prefix is kept.

7. Not changed since the last sweep: the directory, or any file in its top two levels (build output and `.git` left out), has a modification time later than the start of the previous removing sweep (`worktrees.sweep-start` in aw-meta state; a sweep records its start after it removes, and `--dry-run` and `worktree-list` record nothing). This is a change trigger, not an age timer: it protects only trees that changed after the previous sweep, so a tree untouched since then is judged by rules 1 to 6. With no recorded sweep it has nothing to compare with. A tree whose files can't be read is kept. Gate trees are exempt (they are aw-meta's own and rule 1 for them already needs an ended pull request).

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
| `$AW_STATE_DIR/meta/lane-sessions/<lane>.agent` | the launcher (JSON: `agent`, `processName`, `hooks`, `transcripts`) for a lane whose agent isn't Claude Code; read by the lane commands and the lane source |
| `$AW_STATE_DIR/meta/lane-sessions/<lane>.exited` | `scripts/meta-lane.sh` when the lane's claude exited and the pane fell back to its shell; `meta-launch.sh` restarts such a pane unless a claude runs in it again |
| the running log | `aw-meta` (merges, owner decisions, lane maps, kills, worktree sweeps, `log-append`) |
| `$XDG_DATA_HOME/warp-terminal/launch_configurations/<team>-team.yaml` | `scripts/meta-launch.sh` (the `warp` viewer only) |
| `$AW_STATE_DIR/launcher.json` | the owner, or `aw-meta-launch --init-launcher` (read by `scripts/launcher-lib.sh`) |
| `$AW_STATE_DIR/meta/lane-sessions/<lane>.ambiguous` | `scripts/meta-lane.sh` when an agent that reports its own session id left more than one candidate session file |
| `$AW_STATE_DIR/meta/lane-sessions/<lane>.settled` | `scripts/meta-lane.sh` for a lane whose agent reports its own session id: left once the id is recorded or refused; `meta-launch.sh` waits for it before starting the next lane |
| `$AW_STATE_DIR/meta/lane-sessions/.reported.flock` | `scripts/meta-lane.sh`: the team start lock (`flock`; the kernel drops it when the holder dies) that serialises the start of lanes whose agent reports its own session id |

## Launcher

`aw-meta-launch` reads `launcher.json` (`$AW_LAUNCHER_CONFIG`, else `$AW_STATE_DIR/launcher.json`; the contract is
`docs/csharp/sindri-launcher.md`, the design `docs/superpowers/specs/2026-10-15-sindri-session-launcher-design.md`, phase
SL-1). With no file the launcher is what it was apart from the team's own tmux socket (`-L <team>`, below): the Warp layout, Claude Code in every lane (a golden in
`scripts/tests/meta-launch.test.sh` pins the Warp YAML, every tmux command and every claude command line).

| Setting | Effect on `aw-meta-launch` |
|---|---|
| `viewer.kind: warp` (the default with no file) | The multi-pane Warp layout, written to `AW_WARP_LAUNCH_DIR` and opened |
| `viewer.kind: exec-template` | One window per lane (the lanes of this run, so `--lanes` opens only those) running `viewer.argv` with `{title}` and `{name}` = `<team>-<lane>`, `{cwd}` = the repo, `{cmd...}` = `tmux -L <team> attach -t =<team>-<lane>`; `--no-open` opens none, `--dry-run` prints each argv. A terminal that is not installed never stops the sessions: the attach command is printed |
| `viewer.kind: print` | Prints `tmux -L <team> attach -t =<team>-<lane>` per lane |
| `host` | Checked, not used: lanes always run in tmux |
| `meta.lanesAgent`, `meta.lanes.<lane>` | The catalog agent that runs the lane (the meta lane is always Claude Code). The catalog is `config/launcher/builtins.json` plus the file's `agents` |
| `agents.<id>` | `open`/`resume` argv templates (`{bin}`, `{cwd}`, `{name}`, `{seed}`, `{sessionId}`), `processName` (the pane check that restarts a lane whose agent exited) and `transcripts` (the glob that decides resume) |

**Non-Claude lanes launch only.** A lane on `codex`, `cursor` or an added agent starts and resumes, and `aw-meta-launch`
prints `lane <l>: <agent>, launch-only for now: no obligations`. The lane map and `aw-meta health` show such a lane as
they show any lane with no Claude session; do not message a lane marked launch-only. An agent with `sessionId: reported`
chooses its own id: lanes of that kind start one at a time, each waiting up to `AW_META_ID_WAIT` seconds (30) for its id
(the `lane-sessions/<lane>` file a hook writes, else the single new file the catalog's `newestFile` glob finds that no other
lane has recorded; two candidates are refused and leave `<lane>.ambiguous`). "New" means a path that did not exist when
the lane started (a snapshot taken before the agent runs), not a recently modified one. The one-at-a-time start is the
lane's own: `meta-lane.sh` takes a team start lock (`flock` on `lane-sessions/.reported.flock`) before the agent runs and releases it
when the id is recorded or refused, or after a timeout plus a grace window (`AW_META_ID_GRACE` seconds, default 10, during which the lane still watches for its late file and keeps it if it arrives, so the next lane cannot take it), so lanes created together (a Warp pane that creates its session, a
reboot, `--lanes`) are safe too. A lane that cannot start leaves `<lane>.failed` (the reason, the line the launcher prints as `lane <l> failed to start: ...`) and `<lane>.settled`, so the launcher reports it at once instead of waiting out the timeout. A lane refused by the launcher reader (a kickoff prompt starting with `-` once a
`launcher.json` exists) starts no agent and records no id. Obligations for them arrive with SL-1b.

**The team's own tmux socket.** `aw-meta-launch` runs every tmux call as `tmux -L <team>`, where `<team>` is `team.json`'s `team` (for example `-L sindri`): the has-session, new-session, respawn-pane and display-message calls, the `tmux -L <team> new -A` lines of the Warp layout, the printed attach lines and the exec-template viewer's attach argv. The team's server is therefore no longer tmux's default one, and a bare `tmux ls` or `tmux kill-server` in another terminal doesn't touch it. Attach by hand with `tmux -L sindri attach -t sindri-ideas`. The switch takes effect when the team is next relaunched: a server already running on the default socket is left alone (no migration, nothing kills it), but `aw-meta-launch` looks, read-only, for the lanes' `<team>-<lane>` sessions there before it creates anything and refuses (exit 1, naming them) while any exist, because starting them again would run a second agent on the same transcripts: stop the old team, then relaunch. `--allow-legacy` (or `AW_META_ALLOW_LEGACY=1`) skips the check; it is for the owner only, and the meta session never passes it; `AW_META_LEGACY_TMUX_SOCKET` names a `-L` socket to check instead of the default one (tests). Tests never run tmux directly: they source `scripts/test-tmux.sh`, which unsets `TMUX`, uses a temp `TMUX_TMPDIR` and a `-L` socket of its own (`ttmux`), and refuses a socket outside its temp dir.

**Precedence** (environment, then the file, then the defaults): `AW_META_CLAUDE` replaces the `claude-code` entry's `bin`
and `AW_META_CLAUDE_ARGS` is inserted after `{bin}`; both stay for tests and are deprecated for users in favour of an
`"override": true` entry for `claude-code`. `AW_WARP_LAUNCH_DIR` is where the `warp` viewer writes its YAML (the file has no
such field). `AW_LAUNCHER_CONFIG` is passed on to every lane's tmux session.

**Lane variables.** Every lane's environment has `AW_SINDRI_CHILD=1`, `AW_META_TEAM` and `AW_META_LANE`. `meta-lane.sh`
exports them (L-12, SL-1 section 10.2) instead of passing `-e` to `new-session`, so tmux's global environment, the Warp
layout and every tmux command stay as they are with no file, `-L <team>` aside (the owner's condition). The effect is the
same: a `sindri` run inside the lane is a session caller, whatever the agent. A shell left in the pane after the agent
exits still carries them, so run the human's `sindri` approvals from another terminal.

**Writing the file.** `aw-meta-launch --init-launcher [--viewer warp|print|<terminal id>] [--force] [--stdout]` detects the
terminal (no GUI session: `print`; Warp installed: `warp`; `$TERM_PROGRAM` naming a terminal in
`config/launcher/terminals.json`; else the first of those on PATH; else `print`) and writes `launcher.json` (0600, never over
an existing file without `--force`; exit 0 ok, 1 refused, 2 usage). The reader refuses unknown keys, unknown placeholders
and wrong JSON types with `SND-LAUNCH-011` before any session is created; `sindri launch show` is the full check.

## Non-Claude lanes

A lane on Codex, Cursor or an added agent has no `SendMessage` and no transcript the meta can read, so its obligations go through
aw-meta's own files. This is the C# half (SL-1b, part one); the hooks that call these commands at a lane's prompt and stop arrive with the
second part (`config/hooks/meta-lane-gate.sh`, `session-id-report.sh`, the Codex and Cursor adapter mappings), and `scripts/meta-lane.sh` writes
the `.agent` record.

- **Lane identity** is `AW_META_TEAM` and `AW_META_LANE` and nothing else: no lane command on the lane side has a `--lane` flag.
- **Lane source.** The lane map and `health` see a non-Claude lane as `<team>-<lane>` when `lane-sessions/<lane>` holds a session id, `lane-sessions/<lane>.agent` names the agent, and the lane's tmux pane (`tmux display-message -p -t =<team>-<lane>:`) runs the agent's `processName`. With no `.agent` record nothing changes: a team of Claude lanes makes no tmux call and prints what it printed before.
- **State** is in `meta.db` (`lane_mail`, `lane_replies`, and the replies as ordinary `messages` rows) and the key-value store (`lane.<lane>.beat`, `.woke`, `.stop-fingerprint`, `.stop-count`, `.stop-cap`). A reply is an inbound message of the meta with id `lane:<lane>:<n>` from `<team>-<lane>`, so the meta's `stop-check` opens a `reply` obligation from it; the next `lane-send` to that lane closes it. Its fix text is `aw-meta lane-send --lane <lane> ...`, not `SendMessage`.
- **Wake.** `lane-send` types one fixed line into an idle lane only when all of: the lane is idle (its last hook was `lane-stop-check` and it hasn't been woken since), its pane runs the agent's `processName` (a bare shell is "lane down"), and no tmux client is attached. It sends `tmux send-keys -t =<team>-<lane>: -l '<line>'`, then `Enter` (literal mode, exact session; the trailing colon is what lets tmux resolve the pane). Otherwise the message waits for the lane's next hook. `AW_TMUX` names the tmux program. Every call runs on the team's own socket, `tmux -L <team>` (the same server the launcher starts the lanes on); `AW_META_TMUX_SOCKET` names another socket instead (tests and proofs). With no team config and no override there is no socket to look on, so a lane probes as down. Every aw-meta test and the AOT smoke set `AW_TMUX` to a fail-closed stub, never the real tmux.
- **Stop gate.** `lane-stop-check` blocks a stop while the lane owes a reply. A check without `--stop-hook-active` starts a new turn (blocks, resets the count); each continuation with the same owed set counts one block; after `AW_META_STOP_MAX` (default 3) the next is let through with a warning, an incident in the running log and the mark `lane.<lane>.stop-cap`. Only exit 1 blocks; exit 2 (variables unset, a tool error) must pass.
- **Health.** `health` and `lanes` show, for non-Claude lanes only: `taking part`; `launch-only: no obligations` (the CLI has no hook support); `hooks not running: trust them with /hooks in codex` (no hook has run since the session id was recorded); `obligation not delivered (stop cap)`; `obligation not delivered (Cursor loop_limit)` (a Cursor lane blocked as many times as Cursor's default `loop_limit`, 5); `ambiguous session id`. A lane that needs attention makes `health` exit 1.
- **Migration 0003 is one-way.** The first time a build with this change opens `meta.db` it applies migration 0003 (`lane_mail`, `lane_replies`; additive, in a transaction, after copying the database to `meta.db.bak-v2`). From then on an `aw-meta` that doesn't know 0003 refuses the database with `AW-DATA-007`, so the installed binary and the database move together: install the new build before anything else opens the live `meta.db` (run proofs and `dotnet run` against a temp `AW_STATE_DIR`). To roll back, stop the team, restore `meta.db.bak-v2` over `meta.db` and reinstall the old binary; the lane mail written since is lost.

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
