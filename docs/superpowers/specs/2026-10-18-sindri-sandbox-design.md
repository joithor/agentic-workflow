# Sindri sandbox phase: confining an unattended Step session on the host

**Status:** design, revision 2 (after /autoplan round 1 and the engine lane's review, §14; round 2 pending) · **Date:**
2026-10-18 · **Owner decisions:** none yet; §13 asks for them. Inherited: the threat model is honest mistakes, not a
deliberate bad actor (README "Gates guard against…", A §17.28.3, `docs/csharp/bugfix.md` "Threat model"); a Step only
tightens its policy (owner, 2026-10-17; engine P8 D8); Steps run in tmux on the host (README X55) · **Closes:** README
X59(a) · **Builds on:** the session launcher spec `2026-10-15-sindri-session-launcher-design.md` (LCH), SL4, SL8, §3, L-6,
and `docs/csharp/sindri-launcher.md` "Model and policy forms" (the scoping-station spec's L-17); the scoping-station spec
`2026-10-15-sindri-scoping-station-design.md` (SC), W9 (the read-only capability) and W11 (the Step directory); the workflow
engine spec `2026-10-10-sindri-workflow-engine-design.md` (WF), §5.5, §6.5, P8; the blocks-over-MCP spec
`2026-10-18-sindri-blocks-over-mcp-design.md` (BM); the session collector spec `2026-10-16-sindri-session-collector-design.md`
(CO); the first Sindri design `2026-10-07-sindri-design.md` (D), §8.2 and §13.1, which this replaces for the host (§4); the
line spec `2026-10-15-sindri-spec-to-proven-pr-line-design.md` (LN), LN-WF3, LN-WF6, LN-LCH1; the engine lane's binding
constraints for P8 (cited **engine P8 D3, D8, D9, D10, D11**; D4, D7 and the 2026-10-18 `launch.outcome` update where named)
and its review of this spec (E1-…, §14) · **Lanes:** the API lane builds the sandbox inside the launcher (SB-1…SB-5b); the
ideas lane runs the spike and builds the macOS adapter, the GitHub credential path and the per-repo declaration; the
engine lane (P8) consumes the contract, owns the engine side of §6 and §7, and reviewed it; the UI lane builds the copy
(SB-6) · **Next:** /autoplan round 2, then the owner reads §13, then /specToProvenPR takes each phase to PRs.

Co-written with the engine lane (P8 consumes it).

Today a Step's agent session is just a CLI in a tmux pane on the owner's box, as the owner. `ILauncher` (LCH §3) starts it
with `Attended: false`, in a per-run worktree, with a `StepPolicy` (L-17) that names a permission mode and a writable
path, and nothing stops the session from reading `~/.ssh`, writing the owner's other repos, running `git push`, or
starting a build storm. D §8.2 answered that with a container per session and a model proxy; the owner then chose tmux on
the host (X55) and the container's tmux "later". This spec is the "before any unattended Step runs on the host" phase
that X59(a) promised, and it is deliberately smaller than D §8.2: a thin, per-launch confinement of the agent's process
tree, built from tools the box already has, with the CLI's own policy inside it as a second layer.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| SB1 | **Every Step session runs confined, or does not run.** Confinement keys on the **Step run** (a launch or a resume that carries a `StepRef`, or reuses a launch that has a stored plan), never on the `Attended` flag. A resume or an `OpenItem` of a Step's session reuses the stored plan `launches/<id>.sandbox.json` and is confined; if the plan is gone it is refused (`-015`, reason `no-stored-plan`). A bare `sindri open` of an item with no Step run, and Meta lanes, are not confined (Q5). A launch whose confinement can't be built or proven never falls back to unconfined (§6). The one loosening an owner can choose is a time-boxed network *learn mode* (Q10), which still confines the filesystem and the credentials | The owner wants Steps unattended; one unconfined run is what the spec prevents. Keying on `Attended` would let a resumed Step come back unconfined (R1-02) |
| SB2 | **Sindri owns the boundary; the agent CLI's own policy is the second layer.** Outer: a Sindri-built sandbox around the CLI's whole process tree (bubblewrap on Linux, Seatbelt on macOS) plus a cgroup scope for limits. Inner: L-17's per-launch policy file (mode, allow and deny rules, Codex overrides), which gives the agent a clear refusal instead of `EACCES` and still helps on a target Sindri hasn't taught the outer layer (§5) | Cursor and added entries have no usable inner sandbox, and three CLIs' sandbox behaviour changes by version. But their rules are cheap and L-17 already needs them |
| SB3 | **Filesystem: read-only everywhere, writable in one small set.** Writable: the run's worktree, the run's own ref directory in the repo's git dir (SB5), the **run directory** (§3.1), a private scratch, and a short list of tool caches and hook spool directories. `$HOME` is an empty tmpfs rebuilt from an allowlist; Sindri's state and the secret stores are denied except named paths (§3.1). The worktree stays an ordinary host directory, readable by the engine after the session ends (engine P8 D10) | Matches `StepPolicy`: with `WriteScope` the worktree is the one writable path; without it nothing in the repo (engine P8 D8) |
| SB4 | **Network: default deny, one allowlisting proxy, loopback to Sindri.** The session has no network of its own. It reaches Sindri's API and `/sindri/mcp` and the egress proxy, both through unix-socket forwarders; `NO_PROXY` keeps the Sindri port off the proxy (§3.2) | The honest mistakes are `curl \| sh`, a typo'd package, a push to a real remote. A namespace plus a name-checking proxy works alike on Linux and (with Seatbelt's localhost rule) macOS |
| SB5 | **The git directory is read-only, with one writable directory: the run's ref directory.** The launcher creates the worktree and its branch `sindri/<item>/<run>/work` (engine P8 D8: the launcher creates the worktree) and tells the sandbox the directory `<ns>` = `sindri/<item>/<run>`. The sandbox binds `<repo>/.git` read-only and read-write only `objects/`, `worktrees/<name>/`, `refs/heads/<ns>/` and `logs/refs/heads/<ns>/`. The branch is one level inside its own directory because a ref update is a lock-file-and-rename in the ref's directory: with the branch directly under `refs/heads/`, either `refs/heads/` would have to be writable or the first commit would fail with `cannot lock ref`. `config`, `hooks/`, `packed-refs`, `refs/stash`, tags, other runs' directories and `refs/remotes` are read-only (E1-I6) | A commit needs the shared git dir; with the whole `.git` writable, one `git branch -D` rewrites the owner's branches. The self-check proves it with a real ref update (§6), not `commit --dry-run` |
| SB6 | **No process-ID namespace** (accepted limit, Q7). The heavy-job wrapper finds a session by walking to the nearest `claude` ancestor (`~/.agentic-workflow/bin-r1b/heavy`) and the collector reads the process table (CO §4). The cost: a same-user `pkill`, and a shared `/proc` (§3.1, "what it can't stop"). The safety hooks and the inner deny rules cover the common forms | Honest-mistake model; the lock design is the owner's |
| SB7 | **Limits are a cgroup scope under one parent slice, plus a watchdog, and a kill ends the session with a named reason.** CPU, memory and pids come from `systemd-run --user --scope` properties (including `RuntimeMaxSec` as a backstop that outlives `session wrap`) under a parent slice with its own total memory cap; wall time from the `session wrap` watchdog. A kill ends in `session.ended` with the new reason `limit` and `kind` `memory` or `wall` (§3.3); the engine maps it to `step.failed(limit)` (a new engine rule, E1-I2). **The wall clock pauses whenever the run is suspended, a park or a takeover** (E1-B2); it resumes with what remained | A runaway session must hit its own limit, not take the box down. A park keeps its session (engine P8 D11), so a clock that kept running would kill the session an owner is about to answer |
| SB8 | **The engine passes a policy; the launcher maps it.** The engine gives `Attended: false`, `Mode: Open`, `Model: null`, the worktree as cwd, `StepRef` and a `StepPolicy` (`WriteScope` → the worktree is the only writable path, otherwise ReadOnly; `AllowedTools` empty = the CLI's defaults for the mode) and **never names a host, argv, bin, environment variable or mount** (engine P8 D8). The sandbox owns every other environment variable and the policy → confinement mapping (§5). `AW_SINDRI_LAUNCH_SECRET` is set by `session wrap` and survives the environment scrub | Keeps the engine out of per-CLI detail |
| SB9 | **Confinement is fixed at launch and does not change for the life of the session.** Takeover and handback neither loosen nor re-tighten it, and never relaunch (§7; engine P8 D11). Owner question Q1 | The session is one live process tree; a bubblewrap mount namespace can't be changed after the fact, and a relaunch would end the conversation the human joined |
| SB10 | **No owner credentials in the session, with one named exception: the target's own model login.** The environment is built from an allowlist, not scrubbed from the owner's (§3.4). The agent home is read-only except the minimum the CLI writes (§3.1); the inner policy denies reading the credential files (§5). GitHub access is `none` by default; the 1 h agent GitHub App token (`scripts/agent-token.sh`) is minted by the launcher outside the session and delivered through a bound directory only with `agent-app` (SB-7) | "No owner tokens"; a leak into a transcript needs the token to be reachable first |
| SB11 | **Each refusal is its own `SND-LAUNCH` code with a kind, and the engine routes on the code** (engine P8 D8). Three kinds: **no runtime** (the Step has no agent runtime on this box: a `deskCapable` Step goes to the desk, any other parks with the existing NoRuntime park `SND-WF-010`; P8 D7), **launch park** (a new engine park kind `launch` whose Resume starts a **new run**: a new run token, launch id and worktree), and **step failure** (§6). Never auto-retried within the same run (E1-B1, E1-I1, E1-I5) | The engine's park answers are fixed by kind; `spec-conflict` parks of other kinds have no resume, so a bare park could only be cancelled |
| SB12 | **The mechanism is a port, with two non-test-only-confusable implementations.** `ISandbox` in `Aw.Sindri.Core/Sandbox/`: `Probe()`, `Plan(SandboxRequest) → Result<SandboxPlan>` (pure), `SelfCheck(SandboxPlan)`, `Wrap(SandboxPlan, argv) → argv`. Adapters: `BubblewrapSandbox` (Linux), `SeatbeltSandbox` (macOS), the product default **`UnavailableSandbox`** (always `-014`, registered until SB-5a), and two test-only types in `Aw.Sindri.Testing`: **`PassThroughSandbox`** (`Wrap` returns the argv unchanged, `SelfCheck` passes) and `RecordingSandbox`. The pass-through is registered **only** in tests and in the proof fixture (`sindri-prove.sh`'s guarded temp home), never in the product Host (E1-B3) | P8 must prove a real agent launch before the real sandbox exists, without any product switch that turns confinement off |
| SB13 | **`.devcontainer/` is not the sandbox** (docs/devcontainer.md, readiness spec §4d). Neither is Docker: its socket is denied inside the session. Containers stay an option for the "container's tmux later" step of WF §6.5, behind the same port (§4) | Repo rule; Docker-in-the-agent is root on the host |
| SB14 | **Work that needs the box's heavy capacity goes through Sindri's blocks.** BM's `sindri_execute_tool_call` runs heavy gates, evidence and browsers host-side under the Step's policy and the heavy slots. The session's own `dotnet build` and test run still work (§3.3, §3.5) but signature-verified restores, binary fetches, whole-repo gates and the bash-test suite are expected host-side (§3.5) | One owner of the box-wide cap; keeps Docker and browser sockets out of the session |
| SB15 | **Denials are visible and tunable.** Every denial class is counted per run and recorded; the agent is told it is confined and can ask why; the owner sees what was blocked on the item and in the failure row, and gets a one-line profile edit to allow it (§3.6). A denial that can't be seen cannot be fixed, and the first runs will need fixes | The allowlists are guesses; without this loop the first runs stall opaquely (R1-05) |

## 2. Terms

- **Confined session:** an agent CLI process tree started by `session wrap` inside the sandbox of §3, inside a cgroup
  scope, in the tmux session `snd-<run>` (LCH SL4, WF §6.5). `session wrap`, tmux, the launcher and the egress proxy run
  outside it.
- **StepPolicy:** the engine's per-launch policy (L-17; SC W9): mode, allowed tools, writable paths. SB8 fixes the two
  values the engine sends and what each means in the sandbox.
- **Sandbox plan:** the data the pure builder produces: mounts (read-only, read-write, denied), network rules,
  environment allowlist, limits, credentials. Hashed (`planHash`) and summarised on `StartedLaunch` (§8).
- **Agent home:** where a CLI keeps its login, settings and session records: `~/.claude` and `~/.claude.json` for Claude
  Code, `~/.codex` for Codex. Today the owner's own (Q2).
- **Run directory:** `$AW_SINDRI_HOME/workflow/runs/<instance>/<step>/` (0700; SC W11, `AW_SINDRI_RUN_DIR`), where a Step
  leaves its output files. It is a launcher-provided scratch area, **not** a writable path of the `StepPolicy`: the engine
  never sees it (Q11 reconciles this with engine P8 D8).
- **Suspension:** a park or a takeover of a run (WF §5.5). The session stays alive through both (engine P8 D11).
- **Threat model.** The sandbox stops the honest mistakes: `rm -rf` or a redirect in the wrong directory, a `git push` or
  `gh pr create` from a session that shouldn't, a read of a secret by accident and its leak into a transcript, a
  `curl | sh`, a build storm or runaway test that takes the box down, an agent editing another repo or the owner's
  dotfiles. It does **not** try to contain an agent that probes for an escape: no seccomp filter, no pid or ipc isolation,
  nothing against a kernel exploit. A review finding that needs a crafted escape is answered here.

## 3. What the sandbox does

### 3.1 Filesystem

The plan is a stack of bind mounts over the host root, bottom to top:

1. **Root, read-only.** `/` is bound read-only, with a fresh `/dev` and `/proc`, and `/tmp` replaced by a private directory.
   The agent reads the system, the toolchains and the other repos; it can't write any of them.
2. **`$HOME` replaced.** An empty tmpfs over `$HOME`; the **home allowlist** is bound back, read-only unless noted
   (`sandbox.fs.homeAllow`, detected by `sindri sandbox check` and declared per repo, §3.7):

   | Entry | Mode | Why |
   |---|---|---|
   | `.dotnet`, `.nvm`, `.cargo`, `.rustup`, `.local/bin` | ro | Toolchains (`DOTNET_ROOT`, `NVM_DIR`) |
   | `.nuget/packages` | rw | A restore writes new packages; shared with the owner's builds (Q3) |
   | `.npm/_cacache` | rw | Same, for npm; shared |
   | `.local/share/NuGet`, `.dotnet` first-run files | not bound for writing | Replaced by per-launch scratch paths: `NUGET_HTTP_CACHE_PATH`, `DOTNET_CLI_HOME` and `npm_config_logs_dir` point into the scratch (§3.4), because those tools write there on the first call |
   | The agent home of the target | **rw except the owner's configuration** (below) | The CLI needs it |
   | `.gitconfig` | **not bound** | Replaced by the per-launch gitconfig (§3.4) |

   Everything else in `$HOME`, including `.ssh`, `.aws`, `.config`, `.gnupg`, shell histories and the browsers' profiles,
   doesn't exist for the session. An allowlist, not a deny list, is the default for `$HOME`: a deny list names only what
   someone thought of.

   **The agent home is writable only where the CLI writes** (R1-03, Q2 A). `~/.claude` is bound read-write and then
   `settings.json`, `settings.local.json`, `hooks/`, `skills/`, `commands/`, `agents/`, `plugins/`, `CLAUDE.md`, `mcp.json`
   and `statusline.sh` are re-bound **read-only** over it; `~/.codex`: `config.toml`, `hooks.json` and `prompts/` read-only,
   the rest writable. The session's own records (`projects/`, `sessions/`, `todos/`, `session-env/`, `file-history/`,
   `shell-snapshots/`, `history.jsonl`, caches, and the login file for its token refresh) stay writable. So a Step can't
   rewrite the owner's settings or safety hooks, but it can still **read** other sessions' transcripts and the login file;
   the inner policy denies reading the login files (§5) and the credential is never printed by the CLI itself. Where the
   CLI rewrites a root-level file by rename (`~/.claude.json`), the spike (SB-S1 item 2) decides between binding the
   directory and an isolated home (SB-8). **The trigger for SB-8:** the first time `sindri sandbox denials` shows a Step
   reading another session's transcript, or a second person uses the box, whichever is first; the owner decides then.
3. **Outside `$HOME`, a short deny list** over the read-only root:

   | Denied | Why |
   |---|---|
   | `$AW_STATE_DIR` (default `~/.agentic-workflow`) and `$AW_SINDRI_HOME`, **except** the entries below | The ledger, the profile, the human token, every launch secret, judge and scorer stores, other runs' areas |
   | `/var/run/docker.sock`, `/run/podman`, `/run/user/<uid>` (ssh-agent, gpg-agent and dbus sockets) | The Docker socket is root on the host; the agent sockets sign as the owner |
   | `/etc/ssh` host keys, `/root`, other users' homes | Not the session's |
   | The holdout store and other runs' worktrees (LN-LCH1) | Added by the line's capability policy through the same plan |
   | `~/Library/Keychains` (macOS) | The agent App's key and the owner's logins |

   **The exceptions inside the state directories** are a closed inventory, taken from what the toolkit's hooks actually
   touch (`~/.claude/hooks`, `config/hooks`, checked 2026-10-18). They exist so the safety hooks run in a Step as they do in
   a lane; a hook that can't run fails open, so a missing path would silently switch a guard off (R1-01):

   | Path | Mode | Used by |
   |---|---|---|
   | `$AW_STATE_DIR/hooks/` (the Codex and Cursor copies), `$AW_STATE_DIR/bin/` (`aw-meta`, `judge`), `$AW_STATE_DIR/bin-r1b/`, `$AW_STATE_DIR/toolkit` (a link into the repo, readable through the root) | ro | Every hook script, `meta-*` hooks, the `heavy` wrapper |
   | `$AW_STATE_DIR/judge/` (`sessions/`, `outbox/`, `teammate-names.jsonl`) | rw | `done-gate`, `external-write-guard`, `turn-origin`, `send-gate`, `outbox-flush`, `record-teammate-name`, `subagent-start-map` |
   | `$AW_STATE_DIR/context-guard/`, `digests/`, `probe/` | rw | `context-guard`, `probe-log` |
   | `$AW_STATE_DIR/locks/` | rw | The `heavy` wrapper's `mkdir` mutexes |
   | `$AW_STATE_DIR/heavy-slots-cap`, `heavy-meta-reserve` | ro | `heavy` |
   | `$AW_STATE_DIR/meta/` | **denied** | `meta-lane-gate`, `meta-outbox`, `meta-stop-gate` read it only when `AW_META_LANE` is set; a Step has no lane variables, so they pass straight through (LCH §10.2) |
   | `$AW_SINDRI_HOME/launches/<this launch>/` (the net directory, the token directory, the plan summary) | ro (net: sockets) | §3.2, §3.4 |
   | The run directory | rw | The Step's outputs |
   | `~/.local/bin/sindri` and the one discovery file the `sindri` CLI reads for the loopback port (the lane settles which, SB-S1) | ro | `sindri-nudge`, `workflow complete`, `sandbox explain` |

   No ledger, token or secret file is ever among them. `session-id-report.sh` writes `launches/<id>.session-id`, which is
   outside this list: a Step's Claude session id is pre-chosen (LCH SL-2), so the hook's write is skipped and nothing
   depends on it. The **hook canary** (§6, canary `hooks-run`) runs `block-destructive.sh` with a `rm -rf /` payload and
   `detect-secrets.sh` with a planted key and requires both to deny, and writes and removes a probe file in `judge/sessions/`;
   it fails the launch (`-016`) if any hook can't run, and `sindri doctor` repeats it.
4. **Writable.**
   - The **worktree** (`<repo>/.claude/worktrees/sindri-<instance>-<run>`, the cwd; LN-WF2's phase worktree when the line
     owns one) with `WriteScope`; read-only otherwise (engine P8 D8).
   - The **run ref directory** (SB5), same condition.
   - The **run directory**, always (Q11).
   - A **private scratch** directory, `$run-area/tmp`, mounted as `/tmp` and `$TMPDIR` and holding the per-launch cache
     paths; removed when the run closes. Not a RAM tmpfs: it would count against the cgroup and fill silently.
   - The tool caches and hook spool directories above.
5. **Not unshared:** user, pid and ipc namespaces (SB6; the user namespace is bubblewrap's own). The session runs as the
   owner, so files it creates in the worktree are the owner's, and the engine reads the worktree's `HEAD` and
   `git status` from outside after the session ends with no permission change (engine P8 D10). The sandbox never rewrites
   ownership, overlays or copies the worktree; the end of a session removes only the scratch. The engine's own git calls on
   the worktree should run with `-c core.hooksPath=/dev/null -c core.fsmonitor= -c safe.directory=<worktree>` and check that
   the worktree's `.git` file still points at `<repo>/.git/worktrees/<name>`, since the `.git` file and
   `worktrees/<name>/config.worktree` are writable to the session (E1-S2).

**What it can't stop, said plainly:** reading another repo's `.env` (the root is readable); `pkill` (SB6); a write through
a tool cache that poisons a later build; filling the disk (a growth check on the worktree and scratch runs on the
watchdog's tick and records `sandbox.limit-hit {kind: disk}` as a warning, it does not kill); and, because `/proc` is
shared, a same-user process can read `/proc/<pid>/environ` or walk `/proc/<pid>/root` around the denials. The state
directory is hidden from honest access only, not from someone looking for a way around (R1-22). `sandbox.fs.denyExtra` takes
more paths.

### 3.2 Network

On Linux the session runs in its own **network namespace** with only a loopback interface (verified on this box: `lo` is
up with `--unshare-net`). Two forwarders connect it to two services, **end to end**:

| | Outside (Sindri host service, per launch) | The path | Inside (started before the agent, in the sandbox) |
|---|---|---|---|
| Sindri | A listener on a unix socket `…/launches/<id>/net/sindri.sock` that relays to `127.0.0.1:<sindri port>` | The net directory is bound into the sandbox at `/run/sindri/net` (the exception row of §3.1) | `socat TCP-LISTEN:<sindri port>,bind=127.0.0.1,fork UNIX-CONNECT:/run/sindri/net/sindri.sock` on the **same port number** the per-launch MCP config and `AW_SINDRI_*` already name |
| Egress | A listener on `…/net/proxy.sock` running the CONNECT proxy | same directory | `socat TCP-LISTEN:<proxy port>,bind=127.0.0.1,fork UNIX-CONNECT:/run/sindri/net/proxy.sock` |

The outside half is an `EgressService` inside the Sindri host (not in `session wrap`), so the launcher can check the whole
path before starting the agent; the inside half is a small init shell in the sandbox that starts both `socat`s and then
`exec`s the agent. Both die with the run. **Start order:** outside listeners, then the inside init, then the self-check
(which exercises both forwarders from inside the namespace), then the agent. The environment sets `HTTPS_PROXY`,
`HTTP_PROXY`, `ALL_PROXY` (and lower-case) to the proxy port, and **`NO_PROXY=127.0.0.1,localhost,::1`** so the MCP and
API calls to the Sindri port go straight to the forwarder, not through the proxy, which would refuse them. A
`socat` that is missing is `-014` (`no-forwarder`); a forwarder that fails to start or doesn't carry a call is `-016`
(canary `net-forward`). SB-S1 may replace `socat` with a small helper; the code stays.

The proxy does no TLS interception: it checks the requested host name and port, then relays bytes; it resolves names itself
(the session has no DNS) and refuses a target that resolves to loopback, link-local or a private address unless a group lists
that address. Every request's verdict is counted per host in a per-run `egress.log` (host, port, verdict, count; no URLs) in
the run area, and §3.6 shows it. The allowlist is a set of named **host groups** in the approved profile
(`sandbox.network.groups`) and per repo (§3.7):

| Group | Hosts | Given to |
|---|---|---|
| `model` | `api.anthropic.com` for `claude-code`; `api.openai.com`, `chatgpt.com` for `codex`; an entry names its own | every Step |
| `packages` | `api.nuget.org`, `*.nuget.org`, `registry.npmjs.org` | `WriteScope` Steps (a restore on a changed lock file) |
| `signing` | the certificate-revocation and timestamp hosts the signed restore contacts; **filled by SB-S1 item 9**, which runs `scripts/dotnet-verify-packages.sh` in the sandbox | none by default (§3.5: the verify step is expected host-side) |
| `releases` | the hosts named in `binaries.lock` | none by default (`binaries-lock.sh fetch` is host-side) |
| `github` | `github.com`, `api.github.com` | only with `credentials.github: agent-app` (SB-7) |

Telemetry and update checks are turned off in the inner policy (`DISABLE_TELEMETRY`,
`CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`, Codex's equivalents). A tool that honours only a config file for its proxy has
no network, which is the intended failure, and §3.6 makes it visible. In **learn mode** (Q10) the proxy allows every host
and records it; the filesystem and credentials stay confined.

On macOS there is no network namespace: Seatbelt denies all network and allows `network-outbound` only to
`localhost:<proxy port>` and `localhost:<sindri port>`; the same proxy and `egress.log` apply (SB-9).

### 3.3 Process limits, and how they compose with the heavy-job wrapper

`session wrap` starts the agent as `systemd-run --user --scope -p … --slice sindri-steps.slice -- bwrap … -- <agent argv>`; the
tty is the tmux pane's, so attach works as before (WF §6.5). Limits, defaults changeable in the profile (`sandbox.limits.*`,
Q4):

| Limit | Mechanism | Default | When hit |
|---|---|---|---|
| Memory (per session) | `MemoryHigh` (80% of max), `MemoryMax`, `MemorySwapMax=0`, `OOMPolicy=kill` | `auto`: 20% of RAM, at most 12 GiB | The scope is killed; `session.ended reason: limit kind: memory` |
| Memory (all Step sessions) | the parent `sindri-steps.slice` with its own `MemoryMax` | 60% of RAM | The kernel bounds the sum, so the arithmetic can't be wrong; a kill inside it ends one session as above |
| Concurrent sessions | the launcher counts live scopes in the slice | `sandbox.limits.sessions` = 3 | The launch is refused with `-020` (a step failure that says to retry when a session ends) |
| CPU | `CPUWeight=50` and `CPUQuota` = (cores × 50)% (systemd's quota is per CPU: 8 cores = 400%) | half of all cores | Throttled only; never a kill |
| Processes | `TasksMax` | 2048 | New forks fail with `EAGAIN`; recorded once as `sandbox.limit-hit {kind: pids}`; no kill |
| Wall time | the `session wrap` watchdog (SIGTERM, 10 s, then `systemctl --user kill --signal=SIGKILL --kill-whom=all`), and `RuntimeMaxSec` on the scope as a backstop that works if wrap or tmux died | the Step's `timeout` plus 10 minutes; 4 h when it has none; at most `workflow.steps.longMax` plus 10 minutes (LN-WF7) | `session.ended reason: limit kind: wall`. **The clock pauses while the run is suspended** (SB7) |
| Disk | a growth check on worktree and scratch at the watchdog tick | warn at 10 GiB | `sandbox.limit-hit {kind: disk}`; no kill |

**The heavy-job wrapper keeps box-wide concurrency; the cgroup only caps one session's size.** Rules:

1. A session is not a heavy job: it mostly waits on a model and takes no slot.
2. A command the agent runs through `heavy` (`dotnet build`, tests) takes slots as today: the locks are on
   `$AW_STATE_DIR/locks/` (rw), the cap files are read (ro), and the wrapper finds the session by walking to the `claude`
   ancestor (SB6). Its children are in the scope, so they count against the memory ceiling. **A raw `dotnet test` or
   `npm test` would bypass `heavy`:** the sandbox's shim directory (first on `PATH`) holds `dotnet` and `npm` shims that call
   the real tool through `heavy`, so the bypass the owner's own sessions have is closed in Steps.
3. A Step that already holds a heavy slot (LN-WF3's `waitFor: { resource: heavy }`) gets `AW_HEAVY_LOCK_HELD=1`, so the
   agent's own `heavy` call runs straight through instead of waiting for a second slot. A Step that holds none gets nothing.
4. The doctor line prints `slots × ceiling` against RAM and warns over 100%; the ceiling is not a reservation, and the
   wrapper's memory guard still gates the third slot.
5. A SIGKILLed scope skips the wrapper's `trap`. The session-end sweep removes `locks/heavy-session/<pid>.lock` of the dead
   scope's ancestor, **after SB-3 reads what the wrapper's own stale check already tolerates** (it tests `kill -0` on the
   holder) and keeps only what the wrapper doesn't clear; SB-3 has the test. A Codex Step has no `claude` ancestor, so the
   wrapper falls back to the parent shell and per-session serialisation is lost for it: accepted, listed in §11.

### 3.4 Credentials and environment

**Environment from an allowlist**, one list in code with a reason per name, tested against a known-good set taken from
CLAUDE.md's own commands. `session wrap` builds it: `PATH` (the toolchain dirs behind the shim dir), `HOME`, `LANG`, `TERM`,
`TMPDIR`, `USER`, `SHELL`, `DOTNET_ROOT`, `DOTNET_CLI_HOME` (scratch), `DOTNET_SKIP_FIRST_TIME_EXPERIENCE`,
`DOTNET_CLI_TELEMETRY_OPTOUT`, `DOTNET_NOLOGO`, `NUGET_PACKAGES`, `NUGET_HTTP_CACHE_PATH` (scratch), `NVM_DIR`,
`npm_config_cache`, `npm_config_logs_dir` (scratch), `npm_config_ignore_scripts=true`, `NODE_OPTIONS` only if the repo
sets it, `GIT_CONFIG_GLOBAL` (the per-launch gitconfig), the proxy variables and `NO_PROXY`, `AW_SANDBOX=1`,
`AW_SANDBOX_PLAN` (the value-free summary, §3.6), `AW_SINDRI_INSTANCE`, `AW_SINDRI_RUN_DIR`, `AW_SINDRI_WORKTREE`,
`AW_SINDRI_BRANCH` (the run ref), `AW_SINDRI_CHILD`, `AW_SINDRI_ITEM`, `AW_SINDRI_LAUNCH`, `AW_SINDRI_HOME` only when it isn't
the default, `AW_HEAVY_LOCK_HELD` under rule 3 of §3.3, and `AW_SINDRI_LAUNCH_SECRET` (engine P8 D8). A test fails on
any name matching `*_TOKEN`, `*_KEY`, `*_SECRET`, `*PASSWORD*`, `AWS_*`, `GH_*`, `GITHUB_*` or `AGENT_APP_*` unless it
is on the list with a reason (the launch secret, the target's model credential). LN-LCH1's scrub is subsumed.

**The per-launch gitconfig** (R1-15), `GIT_CONFIG_GLOBAL`, a file in the run area bound read-only, replaces the owner's
`.gitconfig`: a fixed identity (`user.name`, `user.email` of the Step), `commit.gpgsign=false`, `tag.gpgsign=false`, an
empty `credential.helper`, `safe.directory=<worktree>`, `gc.auto=0`, `gc.pruneExpire=never`, and a `core.hooksPath` to a
per-launch directory holding a refusing `pre-push` and links to the repo's own hooks (so `.git/hooks`, shared with the
owner's checkout, stays untouched). The owner's identity, signing key and credential helpers never reach the session, and a
signed-commit setup can't break an unattended commit.

**Model login.** The one owner credential in the session is the target's own: Claude Code's login file or the macOS
keychain item it reads, Codex's `auth.json`, or `ANTHROPIC_API_KEY`/`OPENAI_API_KEY` in API-key mode (the model
credential's name is on the allowlist with that reason). The CLI cannot run without it. Q2: (A) the owner's own agent home,
selectively writable (§3.1), or (B) a separate agent home that the owner logs in to once (`sindri sandbox login <target>`),
which also keeps the owner's history and settings out of Step sessions but needs the readers to follow
`CLAUDE_CONFIG_DIR`/`CODEX_HOME` and the hooks installed into it. **Recommendation: A for the first runs, B (SB-8) at the
trigger in §3.1.** The login is checked at launch only; an expiry mid-run shows as a model error in the session and is
accepted (§11).

**GitHub.** `credentials.github: none` is the default and the only value built before SB-7. A Step that needs a push or PR
emits an effect (WF §5.7, P6) that Sindri sends host-side, matching LN-WF6's `build` capability. With `agent-app`
(SB-7):

1. The launcher, as the owner outside the session, runs `scripts/agent-token.sh --repo <name>` ("owner/session launcher
   only", spec §4d) and writes the token to `launches/<id>/gh/token` (0600) inside a **directory** bound read-only at a fixed
   path; the shims read `<dir>/token`. A single-file bind would keep the old inode after an atomic rename (R1-14).
2. Two shims first on `PATH` (`gh` and a git credential helper) read that file at each call. `session wrap` mints a fresh
   token every 45 minutes and renames it inside the directory.
3. The `github` host group opens in the proxy; the pre-push refuser is replaced by one that allows only the run's branch.
4. A mint failure (key missing, no network, App uninstalled) is `-018`, a step failure.

The App's private key never enters the session; it lives in the keychain or `AGENT_APP_KEY_FILE`, outside the home
allowlist.

### 3.5 What works inside, and what to use instead

The commands in CLAUDE.md, against the mount stack. Rows marked "probe" are SB-S1 item 9 and must be confirmed there before
SB-2 is merged; the rest follow from the plan.

| Command | In a Step | If it fails or is refused |
|---|---|---|
| `git status`, `diff`, `log`, `show`, `add`, `commit` in the worktree | Works. Identity from the per-launch gitconfig, unsigned | |
| `git checkout -b <anything>`, `git branch -D`, `git tag`, `git stash`, `git update-ref` on another ref | Fails (`cannot lock ref`): only `refs/heads/sindri/<item>/<run>/` is writable. The agent is told its branch (`AW_SINDRI_BRANCH`) | Commit on the run branch; a sub-branch `sindri/<item>/<run>/try-2` is allowed |
| `git fetch`, `git pull`, `git push`, `git gc`, `git worktree add`, `git config` (repo) | Fails: no network to the remote, `FETCH_HEAD`, `packed-refs`, `config` read-only | Sindri sends the push and PR as effects; ask the block for a fetch |
| `dotnet build -warnaserror`, `dotnet test` (unit lane) | Works (probe): `~/.nuget/packages` rw, http-cache and CLI home in scratch, through the `heavy` shim | A restore needing a new package needs the `packages` group (`WriteScope` Steps have it) |
| `dotnet format --verify-no-changes` | Works (probe) | |
| `scripts/dotnet-verify-packages.sh`, `scripts/binaries-lock.sh fetch` | Fails: certificate and release hosts aren't in a group | Run host-side (SB14); the `signing` and `releases` groups exist if the owner wants them in (probe 9) |
| `npm ci`, `npm test` in a TS package | Works (probe): `.npm/_cacache` rw, logs in scratch; install scripts are off (`npm_config_ignore_scripts`) | A package that needs a script fails loudly; allow it per repo |
| `bash scripts/ci/bash-tests.sh`, `bash scripts/tests/*.test.sh` | Mostly works; tests that start tmux use a private socket; those that mint tokens, run Docker or need `$HOME` state fail | Run the whole suite host-side as the gate; a Step runs the one test it touches |
| `node scripts/check-deps.mjs`, `scripts/sync-rules.sh --check` | Work (read-only repo scripts) | |
| `scripts/sindri-prove.sh`, anything starting Sindri | Fails: no state dir | Not a Step's job |
| `docker`, `playwright` browsers, `serena` | Fail: no socket, no display | Ask a block (SB14) |
| `sindri workflow complete`, `sindri sandbox explain`, MCP `sindri-step` | Works through the Sindri forwarder | |
| Hooks (`block-destructive`, `detect-secrets`, `done-gate`, …) | Run, from the read-only agent-home settings, with the spool paths of §3.1 | The hook canary (§6) proves it at every launch |

### 3.6 Seeing and fixing denials

The agent can't see why something is missing (a denied path doesn't exist; a denied host times out). The loop (SB15):

- **The agent knows.** Every confined session has `AW_SANDBOX=1` and `AW_SANDBOX_PLAN`, a value-free summary file; the
  Step's launch brief (the seed's fixed template, LCH §3) gains one paragraph: "You are in a sandbox. A missing path may be
  denied, not absent. Run `sindri sandbox explain <path|host|command>` before working around it." `explain` is a pure lookup
  in the summary, offline, and answers "denied by rule X, because Y; the owner can allow it by Z".
- **Every denial is counted.** Network: per host in `egress.log`. Inner policy: the CLI's own refusals (read from the
  session's transcript tail, best effort). Filesystem: bubblewrap reports nothing, so a path in the plan's deny list that
  appears in a failed tool call of the transcript tail is recorded as a *likely* denial. `sandbox.denied` is an event at the
  first, tenth and hundredth occurrence of each (kind, target); the rest are counts.
- **The owner sees them.** `sindri sandbox denials [--run <r>] [--since <t>] [--json]` lists them grouped by target and rule.
  The item sheet and the Step's failure or park row show "Blocked: github.com × 3 (the `github` group is off for this Step)".
  Each line carries the fix: the one-line profile edit to allow it for this repo (`sandbox.repos.<repo>.network.allow`) and
  the button that opens it. A weekly roll-up (the weekly job, ledger-only) says "14 refusals, 9 to the same host" so the
  owner can turn repeats into decisions.
- **Learn mode** (Q10): the network may run unenforced for a bounded first period, recording instead of blocking.
- **Reproduce it.** `sindri sandbox exec <launch> [-- <cmd>]` starts a shell (or the command) inside a fresh sandbox built
  from that launch's stored plan, with the same limits and a copy of the scratch, so an owner can run the failing command
  exactly as the agent saw it. It is not the agent's session and doesn't touch it.

### 3.7 Toolchains and other products

The defaults assume dotnet, node and rust. For any other repo the allowlists are a per-repo declaration, not a machine-wide
edit: `sandbox.repos.<repo>` in the profile (`homeAllow`, network groups and extra hosts, cache paths). `sindri repo
onboard` runs `sindri sandbox check` against the repo, detects its toolchains (project files), and offers the entries; the
owner approves them with the profile. A repo without a declaration gets the built-in defaults.

## 4. Mechanism options

| Option | Filesystem | Network | Limits | Verdict |
|---|---|---|---|---|
| **bubblewrap** (`bwrap`; installed here, 0.9.0; verified 2026-10-18 under the AppArmor user-namespace restriction) | Full: read-only root, tmpfs home, per-path binds | Own namespace plus unix-socket forwarders | None; pair with a cgroup scope | **Linux primary.** The only unprivileged tool that does read-only root with a rebuilt home and a per-session network namespace; Claude Code's own Linux sandbox uses it |
| **systemd-run `--user --scope`** (cgroup v2; `cpu memory pids` delegated here; linger on) | None | None (`IPAddressDeny` needs BPF a user manager can't load) | Memory, CPU, tasks, kill, `RuntimeMaxSec` | **Linux limits**, with bubblewrap. No user manager is `-014` |
| **Landlock** (in the kernel's LSM list) | Allowlist only: can't carve a deny out of a granted tree, can't hide a path | TCP port rules only | None | **Fallback adapter, not built in v1.** Fits where `bwrap` is blocked; Codex uses it internally |
| **Containers** (rootless podman, Docker) | Full; the worktree needs a UID mapping, so files the engine reads would be owned by a subuid, against engine P8 D10 | Full | Full | **Not now.** D §8.2's design and the "container's tmux later" of WF §6.5; same port, new adapter |
| **Devcontainer** | n/a | n/a | n/a | **No** (docs/devcontainer.md, SB13) |
| **Firejail** and setuid sandboxes | Good | Good | Some | **No.** Not installed, setuid, no gain over `bwrap` |
| **sandbox-exec** (Seatbelt, macOS) | Full by path; can name the keychain files | Outbound by `localhost:port` or IP, no names | None (the watchdog polls memory) | **macOS primary** (SB-9). Deprecated but what Claude Code and Codex use there |

**Lean on, wrap, or both: both, with Sindri's layer the guarantee (SB2).**

| Question | Claude Code's built-in sandbox | Codex's built-in sandbox | Recommendation |
|---|---|---|---|
| What it gives | Settings `sandbox` key (bubblewrap/Seatbelt): Bash-tool commands confined, a domain allowlist, `autoAllowBashIfSandboxed` | `--sandbox read-only\|workspace-write\|danger-full-access`, `sandbox_workspace_write.*`, `--ask-for-approval` | Real, but each covers the CLI's tool calls only, differs by version, and is absent in Cursor |
| Whole session tree? | Only Bash-tool children | Only model-run commands | No: the outer layer wraps the CLI itself |
| Nested in Sindri's bwrap? | Needs a nested user namespace, which AppArmor may refuse | Same | **Off by default** (`sandbox.inner: auto`): the self-check tries the inner one once per box and keeps it only if it starts; the result is per pinned version (SB-S1) |
| Use it for | Permission rules, mode, and the inner copy of §3.2 | `approval_policy="never"` plus the overrides | Always: the inner policy file is L-17's `policy` form (§5). Advice with a good error, not the boundary |

## 5. Per-target forms

The engine's `StepPolicy` is mapped in two places: the **outer plan** (§3, the same for every target) and the **inner
policy** (L-17's `policy` form). With no human attached, the inner policy never relies on a prompt: a call outside it is
refused, not asked about. **The keys in this table are provisional until SB-S1 item 6 records which keys each pinned CLI
honours**; the built-in forms are tested against that record.

| Engine input | Outer plan | Claude Code (`claude-settings-file`, `--settings <path>`) | Codex (`codex-overrides`, `-c`) | Cursor |
|---|---|---|---|---|
| `Mode: Default`, `WriteScope` | worktree and run ref directory writable, run directory writable | `permissions.defaultMode: "dontAsk"`; `allow` `Read`, `Edit`, `Write` under the worktree and run directory, `Bash`, `mcp__sindri-step__*`; `deny` `Read` of the denied paths **and the login files** (`~/.claude/.credentials.json`, `~/.codex/auth.json`), `Bash(env)`, `Bash(printenv*)`, `Bash(git push:*)`, `Bash(gh:*)`, `Bash(rm -rf /*)`-style rules | `sandbox_mode="workspace-write"`, `sandbox_workspace_write.writable_roots=[<worktree>, <run dir>]`, `network_access=false`, `approval_policy="never"`; or, if the inner sandbox can't start (§4), `danger-full-access` with the outer layer as the only sandbox | **Reserved**: parks `-015` until a box with the CLI verifies a config argument |
| `ReadOnly` (no `WriteScope`) | only the run directory writable | `dontAsk`; `allow` `Read`, `Glob`, `Grep`, `Edit`/`Write` under the run directory only, `mcp__sindri-step__*`, a read-only `Bash` set (`git status`, `diff`, `log`, `show`, `sindri`); `deny` `Edit`/`Write` elsewhere | `sandbox_mode="read-only"`; the run directory is permitted **by the outer layer only** (Codex's `writable_roots` applies under `workspace-write`, so it is not claimed here; SB-S1 item 6 records whether the CLI tolerates the outer write) | Reserved |
| `AllowedTools` non-empty | no change | the list replaces `allow`'s tool entries; denies stay | the nearest `-c` set; a tool Codex can't express is `-015` | Reserved |
| A Step only tightens (owner, 2026-10-17) | the profile's deny list and groups are the floor; a Step may remove a group from itself, never add one | same for rules | same | same |

The Claude settings file also sets `sandbox.enabled: false` unless the box's `sandbox.inner` is on, sets
`disableBypassPermissionsMode: "disable"`, and relies on the owner's hooks in the read-only agent-home `settings.json`. Q9
asks whether `dontAsk` or `bypassPermissions` is better inside the sandbox.

**Targets that can't be confined.** An entry whose `policy` form is `none` or reserved, whose `open` template the plan can't
wrap (a GUI app that owns its process, the `none` host), or whose `bin` the plan can't make runnable inside the sandbox (a
path under a denied tree) parks with `-015`. `-013` is "the entry has no form for what the Step asked"; `-015` is "the
entry has the form but the sandbox can't confine what the form lets through". `argvHash` (SL8) is unchanged: the plan and
policy file are per-launch values, so one approval holds across runs. The wrapper argv (`systemd-run … bwrap …`) is Sindri's,
not part of any entry.

## 6. Launch flow, refusals and codes

`ILauncher.Launch` for a Step run becomes (LCH §3's order, new steps in bold): resolve the agent → SL8 (`-012`) → L-17's form
check (`-013`) → **build the plan from the StepPolicy (pure; `-015`)** → **probe the mechanism (cached per box, refreshed by
Check again; `-014`)** → **session cap (`-020`)** → **prepare credentials (`-017`, `-018`)** → budget (E-28) → **write the
plan file; start the outside half of the network (§3.2); run the self-check (`-016`)** → start in the host → return. The
self-check sits inside the 4 s host-start stage of LCH §3.

**Who writes what** (E1-I4): `ILauncher.Launch` appends **nothing**. The engine's launch activity writes
`launch.outcome:<launch>` (one fact key for `launch.started` and `launch.failed`, P8's 2026-10-18 update). The sandbox
summary (mechanism, `planHash`, writable paths, network groups, inner on/off, limits, credentials) is a field of
`StartedLaunch` (an SB-0 type), which the engine copies onto the `launch.started` payload; a refusal is `FailedLaunch`
with its code and reason.

The **self-check** proves the plan, not the box. Inside the planned sandbox, a canary for each denial must fail and one
for each grant must succeed:

| Canary | Proves |
|---|---|
| `fs-deny` | a denied path (`~/.ssh`, the state dir) is absent; write outside the set fails |
| `fs-write` | a write in the worktree, run directory and scratch succeeds |
| `git-ref` | in the real worktree, `git update-ref refs/heads/<ns>/selfcheck HEAD` then `-d` works, and `git update-ref refs/heads/other-selfcheck HEAD` fails; a real throw-away commit in a scratch repo with the same carve-out works unsigned (not `--dry-run`, which writes no ref) |
| `env` | no secret-pattern name in the environment except the allowlist |
| `hooks-run` | the hook canary of §3.1 |
| `net-forward` | a call through the Sindri forwarder reaches the API; the proxy forwarder answers |
| `net-deny` | a connect to a non-allowed host through the proxy is refused; a direct connect fails |

| Code | Kind | Meaning | `reason` / detail | Engine route |
|---|---|---|---|---|
| `SND-LAUNCH-014` | **no runtime** | The sandbox isn't available on this box | `no-bwrap`, `no-user-namespace`, `no-user-manager`, `no-cgroup-controllers` (which), `no-forwarder` (`socat`), `unsupported-platform` | At `OpenRun`: a `deskCapable` Step runs at the desk (P8 D7); any other Step parks with the **existing NoRuntime park** (`SND-WF-010`), whose Resume starts a new run |
| `SND-LAUNCH-015` | **launch park** | The target or its form can't be confined to this policy | `form-none`, `form-reserved`, `tool-not-expressible` (the tool), `bin-denied` (its path), `host-not-tmux`, `worktree-outside-repo`, `no-stored-plan` | New park kind `launch`; Resume starts a **new run** (new token, launch id, worktree `sindri-<instance>-<run+1>`) |
| `SND-LAUNCH-016` | **launch park** | The self-check failed | `canary`, `expected`, `observed`, the failing argv and scrubbed stderr | Same `launch` park |
| `SND-LAUNCH-017` | **no runtime** | The target's model login is missing or expired for Step sessions | `no-login`, `expired` | Same as `-014` |
| `SND-LAUNCH-018` | **step failure** (`step.failed(launch)`) | The agent GitHub App token couldn't be minted (`agent-app` only) | `no-key`, `http-<status>`, `no-network` | The owner retries (a new run) or uses the desk twin |
| `SND-LAUNCH-019` | **step failure** (`step.failed(limit)`) | A sandbox limit ended the session | `kind` `memory` or `wall`, the limit, the peak | **Never auto-retried in the same run.** The owner retries (a new run with a new worktree; the killed run's branch and commits are kept and named in the failure row) or uses the desk twin |
| `SND-LAUNCH-020` | **step failure** (`step.failed(launch)`) | The cap on concurrent confined sessions is reached | `sessions`, the cap | Retry when a session ends |

`-019` rides on `session.ended` and `step.failed`; it is **catalogue-only** and has no HTTP class (E1-S3). The others have
HTTP classes: `-014` Unavailable (503), `-015`, `-016`, `-017` Conflict (409), `-018` and `-020` Unavailable (503). Each gets
a `SindriErrors` row with a golden per `(code, reason)`, in the owner's words (§8). Check again (§8) answers a `launch` or
NoRuntime park when every probe passes. A viewer failure never changes any of this (LCH SL5).

**Engine side** (the engine lane, consulted): the new `launch` park kind with `Fitting → [Resume]` and `ResumeKind.Rerun`;
`-014`/`-017` mapped to the NoRuntime park; the rule `session.ended(limit)` → `step.failed(limit)` carrying `kind`, the limit
and the peak (not `lost`); the rule for a session that ends while the run is suspended (§7). The `Limit` member of
`SessionEndReason` and `SessionEnded`'s `kind`, `limit` and `peak` fields are **SB-0 types**: #295's `SessionEndReason` has
only `Exit` and `Lost` today (E1-I2). Before it sends `session.ended`, `session wrap` (or the launcher) takes **a final
usage sample** for every reason, so a kill doesn't lose the heaviest minute against the budget (E1-I3).

## 7. Takeover and handback

**SB9, recommended: confinement stays exactly as launched.** The engine's behaviour is fixed (engine P8 D11): a park or
takeover suspends the run and keeps the session; the human joins the same tmux session; while taken over only a human
report (`via: desk`) is accepted and the agent's launch secret is refused; handback resumes the watch on the same launch id;
no second session is ever started. The sandbox adds:

| Moment | Confinement | Limits | The session |
|---|---|---|---|
| Takeover (typing in the pane, or `OpenItem` on a live instance) | **Unchanged** | Memory, CPU, pids keep applying. **The wall clock pauses** | Kept |
| A park (a loop cap, a gate refusal, a timeout, a question) | **Unchanged** | The wall clock pauses | Kept (engine P8 D11) |
| Handback or a park answered | **Unchanged**: nothing was loosened | The wall clock resumes with what remained | Same launch id |
| The human needs something the agent can't do (read a secret, push) | Do it in a window of your own: a new tmux window's shell is outside the sandbox and you are the owner. `sindri sandbox exec` reproduces what the agent saw | n/a | Kept |
| The scope is killed or lost while the run is suspended | | | The engine **records it on the suspension**; the answer or handback then becomes a **Rerun** (a new run), because there is no session to hand back to |
| A suspended run's session hits a memory limit | Killed as for any session | | As the row above |

**Who knows the run is suspended** (E1-S1): the source of truth is the engine's suspension state. The heartbeat route is the
launcher's (`LaunchSessionEvents.Beat`), so P8 adds a read, "is the run that owns this launch suspended", which the
heartbeat calls and returns as `paused: true` (an additive field, API §2.5); the API lane doesn't read `views.db`. `session
wrap` counts only unpaused time against the wall limit.

Why not relax on takeover: a live bubblewrap namespace can't be changed, so "loosen" would mean a relaunch, which ends the
conversation the human joined and breaks engine P8 D11. Why not re-tighten on handback: nothing was loosened. The cost: a
human who joins and says "go ahead, push it" sees the agent refuse; they push from their own terminal. Q1 asks the owner.

**What the human sees and says** (R1-10): a banner on the Step sheet and in the pane's tmux status line while the session
is confined: "Confined session. Pushes and secrets are blocked. The clock is paused while you're here." The inner deny rules
return one defined wording to the agent: "Blocked by the Step sandbox (rule `<id>`). Ask the owner to allow it, or use your
own terminal." The escape ("use your own terminal; `sindri sandbox exec` shows what the agent saw") is in the takeover
flow's first screen, not a footnote.

When a run closes (report decided, `step.failed`, cancel, instance ends; engine P8 D11) the host stops the tmux session, the
scope ends, the proxy and forwarders stop, and `session wrap` removes the plan file, token directory and scratch.
**The worktree is left as it was, readable by the engine's process** (engine P8 D10); purging it is the instance's cleanup
(LN-WF2).

## 8. What the owner sees

**Ledger events (additive; API §9 rules).**

| Event | When | Fields |
|---|---|---|
| `launch.started` gains `sandbox` (copied from `StartedLaunch`) | every Step start | `{mechanism, planHash, writable: [path…], network: groups, networkMode: enforce\|learn, inner: on\|off, limits: {memoryMax, cpuQuota, tasksMax, wall}, credentials: {github}}`. Paths are listed, contents never |
| `sandbox.denied` (public) | the 1st, 10th and 100th denial of each (kind, target) per run | `{launchId, kind: network\|policy\|path, target, rule, count}`. The target is a host, a path or a tool name; a denied command line is never recorded whole (it can hold a token): rule id and tool only, redacted and truncated |
| `sandbox.limit-hit` (public) | `TasksMax` reached (once), `MemoryHigh` crossed, disk growth | `{launchId, kind, peak}` |
| `session.ended` gains `reason: limit` | a limit kill | `{kind: memory\|wall, limit, peak}` plus the peaks of every limit (so Q4 is tuned from data); the final usage sample precedes it |

**Owner-voice copy.** One sentence per `(code, reason)`, in the SB-0 goldens (`tests/…/Golden/Sandbox/copy.json`), with the
command as a copyable second line. The card's fix is the same sentence `sindri doctor` prints (sentences below are the
goldens' text):

| Code · reason | What the card says |
|---|---|
| 014 · no-bwrap | "This machine has no sandbox tool, so this Step can't run by itself. Install bubblewrap (`sudo apt install bubblewrap`), then press Check again." |
| 014 · no-user-namespace | "The sandbox tool can't start because the system blocks it. Allow it for bubblewrap in AppArmor, then press Check again." |
| 014 · no-user-manager | "The sandbox needs your user session manager, which isn't running. Run `loginctl enable-linger $USER`, then press Check again." |
| 014 · no-cgroup-controllers | "The system doesn't give your account memory or CPU limits. Enable the missing limit (named here) in systemd, then press Check again." |
| 014 · no-forwarder | "The sandbox needs `socat` to reach Sindri. Install it, then press Check again." |
| 014 · unsupported-platform | "Steps can't run by themselves on macOS yet." Shown gray, as "not yet supported", never red (R1-20). The Step goes to your desk |
| 015 · form-none / form-reserved | "This agent hasn't been taught how to follow a Step's rules, so it can't run by itself. Use Claude Code or Codex, or add its policy setting." |
| 015 · tool-not-expressible | "This Step allows `<tool>`, which this agent can't restrict. Change the Step's tools, or use another agent." |
| 015 · bin-denied | "The agent program lives somewhere the sandbox hides (`<path>`). Move or reinstall it somewhere under your home or `/usr`." |
| 015 · host-not-tmux / worktree-outside-repo | "The Step's workspace isn't set up the way the sandbox needs. This is a Sindri fault: report it, then Resume." |
| 015 · no-stored-plan | "This session's sandbox record is gone, so it can't be reopened safely. Start the Step again." |
| 016 · `<canary>` | "The sandbox didn't behave on this machine: `<what was checked>`. Open the details, fix what they name, then Check again." |
| 017 · no-login / expired | "Claude Code (or Codex) isn't signed in for Steps. Sign in as yourself, then press Check again." |
| 018 | "Sindri couldn't get a short-lived GitHub token for this Step (`<reason>`). Retry the Step, or do it at your desk." |
| 019 · memory | "This Step ran out of memory (`<limit>`) after `<time>`. Its work so far is kept on branch `<name>`. Raise the memory limit in the profile (a re-approval), then run the Step again; or do it at your desk." |
| 019 · wall | "This Step ran out of time after `<time>`. Its work so far is kept on branch `<name>`. Run it again, or do it at your desk." |
| 020 | "Three Steps are already running. This one will need to be started again when one finishes." |

`-019` says plainly that limits are **profile-wide**: raising one is a profile edit and a re-approval, not a per-Step knob.

**Check again runs every probe and shows every blocker on one card** (R1-09), so the first run is one visit, not four. It
re-runs the box probe, the form check and the login check, then a throw-away self-check on a fixture plan. When all
pass, it also **answers the parks**: it sends the Resume answer (as the human; a service never answers) to every instance
parked on a sandbox code. `sindri sandbox check` is the same thing from the terminal and is the **pre-flight**: `sindri
doctor` and `sindri repo onboard` run it and print its remediation block in one list.

**Surfaces.**
- `sindri doctor`, a `sandbox` section in plain terms: `ok  sandbox ready: bubblewrap 0.9.0, limits available, 4 checks passed in 180 ms`, `ok  limits: 3 sessions × 12 GiB = 36 GiB of 62 GiB`, `warn limits: 3 × 12 GiB is more than this machine's memory (the heavy wrapper still gates slot 3)`, `fail sandbox: your user session manager isn't running (run loginctl enable-linger $USER)`, `ok  hooks: 24 of 24 safety hooks run inside a Step`, `info  agents' own sandboxes: off (not needed; Sindri's covers it)` (not a fault), `info  macOS: Steps not yet supported` (gray). A failing line is a `-014`/`-016` reason, so the card and the line say the same sentence.
- `sindri sandbox check|show|denials|explain|exec` (read-only except `exec`, which only reads the plan): `show` prints the mount table (read-only, writable, hidden), the groups, the environment names (never values) and the limits.
- **Health and Settings**: the sandbox state is a row on Health ("Sandbox: ready" or the blocker) and a section in Settings → Agents (the mechanism, the groups, the limits, learn mode with its expiry). The vocabulary is "sandbox", "limit" and "blocked"; "scope", "plan" and "canary" don't appear in the app.
- `sindri status` and `agents`: a Step session reads "sandboxed · mem 2.1/12 GiB"; an attended open reads "not sandboxed (you're at the keyboard)" in the normal ink, not a warning.
- **The app** (SB-6): the Agents sheet "Sandbox" line (writable folder, groups); the park card with the sentences above and a **Check again** button; the "Blocked" list on the item sheet; the limit ending; the takeover banner (§7).

**States** (pinned as SB-0 goldens; `Aw.Sindri.Ui.Tests` reads them): *Check again*: idle → "Checking…" after 300 ms (the UI
spec's rule for a slow action) → still failing (the card lists every blocker, not the first) → all clear ("Ready. Resuming
2 Steps") → resumed. *Park card*: never checked, checked stale (shows the age), checked fresh. *Sandbox line*: sandboxed,
not sandboxed (attended), plan file unreadable ("details unavailable"), serve down (dimmed). *Blocked list*: none, some,
many (top 5 and a link). *Limit ending*: memory, wall, with and without a kept branch.

**Accessibility** (pinned the way `docs/csharp/sindri-ui.md` pins controls; SB-6's done-when includes it):

| Control | Keyboard | Screen reader | Focus | Text scale 1.3 · never colour-only |
|---|---|---|---|---|
| Sandbox line / headroom | in the sheet's tab order | name "Sandbox: confined, writes only to `<folder>`"; headroom as text "memory 2.1 of 12 GiB" | n/a | wraps; state as a word |
| Park card | the card is a landmark region; its fix text before its buttons | name "Needs you: Step `<x>` can't start, `<reason>`" | when the card appears from a user action, focus goes to its heading | wraps; severity as a word and an icon |
| **Check again** | Enter and Space; focus ring | "Check again", then "Checking…", then the result in a live region (polite) | stays on the button while checking; after success moves to the card's result; after "still failing" stays and the live region lists the blockers | the result is text |
| Blocked list | a list; each row's fix button is reachable | each row reads "Blocked `<target>` `<n>` times; allow it for this repo" | n/a | n/a |
| Limit ending | in the failure row | the whole sentence | n/a | n/a |
| Takeover banner | not a trap; dismissible once, back after reattach | role status, read once on appearance | does not take focus | text, not colour |

## 9. Profile keys and environment

Under `sandbox:` in the profile (S1's schema; one section in `docs/csharp/sindri-profile.md`, checked by
`scripts/check-profile-doc.mjs`). A key can only tighten a Step; the profile is the owner's. **An approved profile with no
`sandbox:` section is valid**: it gets the built-in defaults below; tightening needs no re-approval, loosening (a longer
limit, a new group or host, learn mode, `agent-app`) does; `sindri doctor` prints "using the built-in sandbox defaults" and
the profile preview shows the new section on the next approval.

| Key | Default | Meaning |
|---|---|---|
| `sandbox.limits.memory` / `.cpu` / `.tasks` / `.sessions` | `auto` (20% RAM, max 12 GiB) / 50% / 2048 / 3 | Per-session memory, CPU share, processes, concurrent sessions |
| `sandbox.limits.total` | 60% of RAM | The parent slice's `MemoryMax` |
| `sandbox.limits.wallGrace` | `10m` | Added to the Step's `timeout` |
| `sandbox.fs.homeAllow` / `.denyExtra` | detected / `[]` | Home entries bound in (each `ro` or `rw`); more denied paths |
| `sandbox.network.groups` / `.mode` | §3.2's table / `enforce` | Named host groups; `enforce` or `learn` (Q10) |
| `sandbox.repos.<repo>.…` | none | Per-repo `homeAllow`, groups and hosts (§3.7) |
| `sandbox.inner` | `auto` | `auto`, `on`, `off` |
| `sandbox.credentials.github` | `none` | `none` or `agent-app` (SB-7) |
| `sandbox.agentHome` | `owner` | `owner` or `isolated` (Q2; SB-8) |

No environment variable switches the sandbox off. `.env.example` gets only paths a test redirects (`AW_SANDBOX_BWRAP`,
`AW_SANDBOX_SYSTEMD_RUN`, `AW_SANDBOX_SOCAT`), checked by `scripts/check-env-example.sh`. `deps.yaml` declares the new `cli`
edges (`bwrap`, `systemd-run`, `socat`) in SB-0. **Docs the phases update:** `docs/csharp/sindri-sandbox.md` (new, with a
glossary: plan, `planHash`, park, step failure, Check again), `docs/csharp/sindri-launcher.md` (the launch flow gains steps),
`docs/csharp/sindri-profile.md`, `docs/csharp/sindri.md` (codes), `AGENTS.md` and `CLAUDE.md` (Directory Structure, Commands:
`sindri sandbox check|show|denials|explain|exec`), `docs/sindri/README.md`, `.agents/rules` (a note for agents: "what works
inside a Step", §3.5) and `setup.sh`/`scripts/install-deps.sh` (the prerequisite list and the `AW_NO_SYSTEM_INSTALL`
behaviour for `bubblewrap` and `socat`).

## 10. Testing

Per the C# rules (TDD, lanes, fakes with contract tests). Red first; builders on Sonnet.

| What | Cases | Lane |
|---|---|---|
| Plan builder (pure) | `ReadOnly_makes_only_the_run_directory_writable`, `WriteScope_adds_the_worktree_and_the_run_ref_directory`, `Run_branch_is_inside_its_own_directory`, `Home_is_empty_unless_allowlisted`, `Agent_home_config_is_read_only`, `State_dir_is_denied_except_the_hook_inventory`, `Environment_has_no_secret_name_except_the_list`, `A_step_cannot_widen_the_profile_floor`, `Docker_socket_is_always_denied`, `Cpu_quota_is_cores_times_share`, `Resume_of_a_step_session_is_confined`, `Missing_sandbox_section_uses_defaults`; mount-table goldens | unit |
| Contract base | `ISandboxContract` on `PassThroughSandbox`'s documented behaviour and the real `BubblewrapSandbox`: write outside the set fails, denied path absent, `git commit` and `update-ref` on the run ref work, `branch -D other`, `tag`, `stash` and `push` fail, the hook canary passes, `dotnet restore` of a fixture works with the cache paths, a connect to the host's other loopback port fails, the Sindri forwarder works, a non-allowed host is refused, an allowed one relays (local stub) | integration |
| Limits | a memory hog ends `limit/memory`; a sleeper ends `limit/wall`; the wall clock stops while suspended (park and takeover); `TasksMax` gives `EAGAIN` and one event; the stale heavy lock is swept; `RuntimeMaxSec` kills a scope whose wrap was killed | integration |
| Heavy | inside the sandbox the real `heavy` takes and releases a `locks/` slot; `AW_HEAVY_LOCK_HELD=1` runs through; the `dotnet` shim goes through `heavy` | integration |
| Launcher and codes | each `(code, reason)` from a fake with its kind; `Code_kinds_match_the_engine_table` reads one table both sides use; the copy goldens; `Final_usage_sample_precedes_session_ended` | unit |
| Denials | a planted token in a denied command isn't in the event; `egress.log` counts; `explain` answers offline | unit, integration |
| Per target | built-in forms against the SB-S1 record; `cursor-config` parks `-015` | unit |
| UI | view-model tests over the SB-0 state goldens; the accessibility rows | unit |
| Proof | §12, SB-5a, SB-5b, SB-10 | heavy |

Proofs that start tmux use a private socket and unset `TMUX` (`scripts/test-tmux.sh`); no proof touches the real
`$AW_SINDRI_HOME`, state directory or agent home (a canary file under a fixture `HOME` stands in for a secret); none uses a
temp HOME for dotnet (a NuGet cache leaks per run).

## 11. What stays out, and what is accepted

Out: bare attended opens and Meta lanes (Q5); containers, a pid namespace, seccomp and anything aimed at a deliberate escape
(§2); a model proxy with a dummy credential (D §8.2's W1); per-session bandwidth and content inspection; Windows; Cursor
(reserved, `-015`); bounded tool-less jobs (`claude -p`, X56).

Accepted (the LOW findings, recorded rather than fixed): disk is warned, not enforced; a Codex Step loses per-session `heavy`
serialisation; the login is checked at launch only; shared tool caches are shared state (Q3); install scripts are off by
default but a package that needs one fails loudly; the shared `/proc` (§3.1). **Multiplayer** (README X61) changes the agent
home (per user), the credentials (per user), the proofs (owner-run) and shares the profile; `sandbox.agentHome: isolated`
is the multi-user path.

## 12. Phases

Each phase is one PR, driven by /specToProvenPR in its lane, red first. Sizes: S a few files, M one subsystem slice, L more.
**(owner)** steps run on the owner's box. **The critical path to the first agent-run item:** P10a → DT-J2 (#306) → P8 → the
first unattended Step, with the sandbox merged before P8's real-sandbox proof. P8 builds on DT-J2 and needs no P7 (E1-I7).
SB-10 needs #295 (P10a's contract) and P8's U8, not P7.

| Phase | What | Files | Size | Depends on | Done when | Lane |
|---|---|---|---|---|---|---|
| **SB-S1** | **Spike (no product code).** On the owner's box, throw-away scripts outside the repo: (1) interactive Claude Code and Codex under `bwrap` (with and without `--new-session`) in tmux: attach, type, detach; (2) the agent homes and `~/.claude.json` (atomic rename), which paths must be writable; (3) the forwarder plumbing of §3.2 carries Sindri's MCP and the CLI's HTTPS, and `NO_PROXY` works; (4) `systemd-run --user --scope` keeps the tty, and wrap reads `memory.events` and the scope's `Result` after an OOM kill; (5) nested inner sandboxes per pinned CLI; (6) which settings and `-c` keys each pinned CLI honours (§5); (7) which discovery file `sindri` reads; (8) the nested-heavy rule with the real wrapper; (9) **the real commands of §3.5 in the sandbox**: `dotnet build -warnaserror`, the unit lane, `dotnet format`, `npm test` in a TS package, `scripts/dotnet-verify-packages.sh`, one bash test; the writable list is derived from it | scratch dir; findings into §15 and `docs/csharp/sindri-sandbox.md` | S | none | Each of the nine answered with command and version **written into §15**; SB-2 to SB-4 start only when §15 holds the answers they use. A "no" on (1) or (3) goes to the owner first | ideas |
| **SB-0** | Contracts: `docs/csharp/sindri-sandbox.md` and the docs list of §9; the plan-file schema; **the types**: `SessionEndReason.Limit` and `SessionEnded`'s `kind`, `limit`, `peak`, the `StartedLaunch` sandbox summary, `FailedLaunch` reasons; codes `-014` to `-020` with kinds and the engine table; **the copy goldens, the states and the accessibility rows** of §8; profile keys; `deps.yaml` edges; `.env.example`; the README row | `docs/csharp/*`, `SindriErrors.cs`, the launch and session types, `SindriProfile` schema and `ProfileRules`, `deps.yaml`, `tests/…/Golden/Sandbox/*` | M | none (beside the spike) | `check-profile-doc`, `check-deps`, `check-env-example` pass; goldens pin every `(code, reason)`; the engine lane reads the kind table and the types | api |
| **SB-1** | The pure core: `ISandbox` and types, the plan builder, mount stack and deny lists, the hook inventory, the environment allowlist, the per-launch gitconfig, the limits parsing, host-group rules, the per-repo declaration schema, `planHash`, the renderer for `show` and `explain`, `UnavailableSandbox`, `PassThroughSandbox` and `RecordingSandbox` (test-only ones in `Aw.Sindri.Testing`) | `…Core/Sandbox/*`, `…Features/Sandbox/`, `…Testing/*`, tests | M | SB-0 | The plan-builder cases pass; the environment test fails on a planted `GH_TOKEN`; the product Host registers only `UnavailableSandbox` | api |
| **SB-2** | `BubblewrapSandbox`: argv from the plan, the run ref directory, the tmpfs home, the cache paths, the hook spool binds, the canaries incl. `git-ref` and `hooks-run` | `…Infrastructure/Sandbox/Bubblewrap*`, `…Testing/ISandboxContract` | M | SB-1, SB-S1 (1, 2, 9) | The contract base passes on real `bwrap`; a `dotnet restore` fixture works; the hook canary denies `rm -rf /` | api |
| **SB-3** | Limits and the end reason: the scope and slice properties, `RuntimeMaxSec`, the watchdog, the pause on any suspension (the engine's read, E1-S1), `memory.events` polling, `limit` and `-019`, the final usage sample, `-020`, the `dotnet` and `npm` shims, the stale-lock sweep | `…Infrastructure/Sandbox/SystemdScope*`, `…Host/Commands/SessionWrap*`, the heartbeat | M | SB-1, SB-S1 (4, 8); **SL-2/P10a's `session wrap`**; the engine's suspension read | The limits and heavy cases pass; `session.ended` carries `reason: limit` and the kind, never `lost`; the clock stops during a park | api |
| **SB-4** | The egress: the outside `EgressService`, the proxy, `egress.log`, the inside init and forwarders, `NO_PROXY`, learn mode, `sandbox.denied` events, `denials` and `explain` | `…Infrastructure/Sandbox/Egress*` | M | SB-1, SB-S1 (3) | The network cases pass on stubs; the Sindri MCP call works through the forward and not through the proxy | api |
| **SB-5a** | **Launcher integration, ReadOnly first:** the launch flow, probe and Check again (all blockers on one card, and the Resume answer), the `ReadOnly` policy files for `claude-settings-file` and `codex-overrides`, `-014` to `-017`, `-020`, the `StartedLaunch` summary, `sandbox check\|show\|denials\|explain\|exec`, doctor lines, the engine's `launch` park kind and NoRuntime mapping (engine lane, in the same PR or its P8 follow-up). Needs only the run directory, the model host and the wall limit: **merging this puts a real scoping or review Step on the factory** | `…Features/Launch*`, `…Infrastructure/Launch/*`, `…Host/Commands/Sandbox*`, events, goldens (the gate runs `Aw.Sindri.Ui.Tests`) | M | SB-2 (read-only parts), SB-3, SB-4, P10a with SL-2 and L-17 | **Proof:** `sindri-prove.sh` starts a ReadOnly fixture Step under a private tmux socket with a stub CLI that tries each forbidden action: every one is refused, the allowed ones work; with `bwrap` renamed away a deskCapable Step runs at the desk and another parks (NoRuntime); Check again after restoring it resumes | api |
| **SB-5b** | **WriteScope:** the worktree and run-ref-directory binds, the `packages` group, the git canary on the real worktree, the per-launch gitconfig, the `WriteScope` policy files, the engine's D10 git hardening (E1-S2), `-018` stub | the same | M | SB-5a, SB-2 | **Proof:** a WriteScope fixture Step commits on its branch, can't update another ref or push, `dotnet build` works, the engine reads its diff after the session | api |
| **SB-6** | UI and status copy: the Sandbox line, the park card and Check again (every state), the Blocked list, the takeover banner and tmux status line, the limit ending, Health and Settings rows, the fixture sections | `src/Sindri/Aw.Sindri.Ui/Views/Agents*`, `Views/Launch*`, `Views/Health*`, `Settings`, `scripts/sindri-ui-fixture.sh`, `docs/csharp/sindri-ui.md` | **M** | SB-0 (goldens), SB-5a for live data | View-model tests over the SB-0 goldens; the accessibility rows of §8 each have a test; the fixture shows every state | ui |
| **SB-7** | The GitHub credential: `agent-app`, the mint, the token directory, the shims, the 45 min refresh, the `github` group, the branch-only pre-push refuser, `-018` | `…Infrastructure/Sandbox/AgentToken*`, shims, tests with a fake `AGENT_APP_API_URL` | M | SB-5b | With the fake API a Step pushes its run branch, not another; the token is never in `env`, `ps`, a transcript or the plan; the refresh is seen through the directory bind | ideas |
| **SB-8** | Isolated agent home (Q2 B), when its trigger fires: `sandbox login`, hooks into the home, `CLAUDE_CONFIG_DIR`/`CODEX_HOME`, readers and the collector following the directory | `…Infrastructure/Sandbox/AgentHome*`, `providers/*/install-hooks.sh` (a target-dir option), readers | M | SB-5a, the owner's answer | A Step runs with none of the owner's `~/.claude`; its transcript is found by the usage reader and the collector | api, ideas |
| **SB-9** | macOS: `SeatbeltSandbox` from the same plan, the keychain rule, the polling watchdog | `…Infrastructure/Sandbox/Seatbelt*` | M | SB-5a, a Mac | **(owner, on a Mac)** the SB-5a proof passes; a memory `limit` fires within 5 s of the ceiling | ideas |
| **SB-11** | The per-repo declaration: `sindri repo onboard` runs `sandbox check` against the repo, detects toolchains, offers the `sandbox.repos.<repo>` entries; the "allow this for this repo" fix | `…Features/Onboard*`, `sandbox` commands | S | SB-5b | Onboarding a Python and a Go fixture repo offers their entries; approving them lets a Step build | ideas |
| **SB-10** | **(owner) The first unattended run**, real sandbox, on P8's signal: `workflow.step` on a scratch item launches Claude Code confined, commits on its branch, cannot read `~/.ssh` or push, reports; a park keeps the session and pauses the clock; a takeover keeps the same confined session; handback resumes; the worktree is read by the engine afterward; a planted memory hog ends `step.failed(limit)` with its branch kept | owner's box, a scratch repo | S | SB-5b; P8's U8 and #295 | The owner records the run (events, `sandbox show`, the engine's rows) | owner |

### 12.1 Parallelization plan

```
now ──► SB-S1 (ideas) ───────────────────────────┐
now ──► SB-0 (api) ──► SB-1 (api) ──┬──► SB-2 ───┼──► SB-5a ──► SB-5b ──► SB-10 (owner, with P8)
                                    ├──► SB-4 ───┤       │         └──► SB-7, SB-11 (ideas)
P10a with SL-2 ──► SB-3 (api) ──────┘            ┘       └──► SB-9 (ideas, Mac), SB-8 (if triggered)
SB-0 goldens ──► SB-6 (ui) ◄────────────── live data after SB-5a
P8 (engine) proves on PassThroughSandbox (test + fixture only); the product refuses with -014 via UnavailableSandbox until SB-5a
```

| Wave | API lane | Ideas lane | Engine lane | UI lane | Owner |
|---|---|---|---|---|---|
| 0 | SB-0, then SB-1 | SB-S1 | reads SB-0's table and types; P8 on `PassThroughSandbox`; the suspension read (E1-S1) | — | answers Q1, Q2, Q5, Q11, Q12 |
| 1 | SB-2 and SB-4 (no common files), SB-3 once `session wrap` is on `main` | SB-7 tests against a fake token API | maps `-014` to `-020`; the `launch` park kind; the limit rule | SB-6 on the goldens | the spike's "no" answers |
| 2 | SB-5a, then SB-5b | SB-7, then SB-11 | P8 eval cases per code kind | SB-6 live | — |
| 3 | — | SB-9 on a Mac; SB-8 if triggered | P8's real-sandbox proof | — | SB-10 |

- **What runs in parallel so a team stays busy:** the spike with SB-0/SB-1; SB-2, SB-3 and SB-4 are independent files; the
  ideas lane writes SB-7 against a fake while SB-5 lands; the UI lane needs only goldens; the engine lane never waits for the
  real sandbox until its owner proof.
- **ReadOnly first** (SB-5a): a scoping or review Step runs sandboxed before WriteScope plumbing, the git binds and the
  `packages` group exist. Q12 asks which Step is first.
- Hotspots (`SindriErrors.cs`, `EventCatalog.cs`, the command and endpoint tables, the JSON context, `Add…()`, `deps.yaml`):
  each phase appends at its anchor; the second to merge rebases. SB-0, SB-5a, SB-5b and SB-6 change goldens the UI tests
  read, so their gates run `Aw.Sindri.Ui.Tests`.
- Every proof uses `scripts/sindri-prove.sh`, a private tmux socket and a guarded temp `AW_SINDRI_HOME`.
- **P8 is not blocked and nothing runs unconfined in the product:** P8 merges with the product Host registering only
  `UnavailableSandbox`; its launcher tests and its own proof run on `PassThroughSandbox`, registered only in tests and in the
  proof fixture's temp home (SB12). In the product every Step has no agent runtime until SB-5a: `deskCapable` Steps run at
  the desk, others park NoRuntime. SB1 is true by construction.

## 13. Open questions for the owner

Each has a provisional answer, used until the owner says otherwise.

**Q1. When you take over a running Step, should the agent's limits come off, so you can tell it to push or read something
it was denied?**
- *Option A (recommended): no. The session keeps the exact confinement it started with, for its whole life.* It is the same
  running process; changing it means starting a new one, which ends the conversation you just joined. If you want something
  the agent may not do, do it in your own terminal (the banner says so). Only the time limit changes: it pauses while the
  Step is parked or while you are in it.
- *Option B: loosen on takeover and tighten on handback.* The session would have to be relaunched at each change.
- *Option C: leave the sandbox for good once you take over.* Simple, but one stray keystroke ends the protection.

**Q2. Which model login do Step sessions use?** The agent needs your login to talk to the model; it is the one credential of
yours that has to be inside.
- *Option A (recommended for the first runs): your own `~/.claude` and `~/.codex`, with your settings, hooks and skills
  mounted read-only.* Your safety hooks apply to Steps and the readers find the transcripts. A Step can still read your
  login file and other sessions' history (it is told not to print them).
- *Option B (build later, SB-8): a separate login for Steps.* Your history and settings stay out entirely. It needs a second
  login and the hooks installed in that directory. Recommended trigger: the first time a Step is seen reading another
  session's history, or when a second person uses the box.

**Q3. What can a Step reach on the network, and which tool caches can it write?**
- *Recommended:* every Step reaches its model provider; Steps that change code also reach the package servers (`nuget.org`,
  npm). Nothing else. Verifying package signatures and fetching tool binaries happen on the host, not in the Step. The NuGet
  and npm caches under your home are writable by Steps (a restore needs them) and shared with your own builds; npm install
  scripts are off.
- *Stricter:* no package servers; a Step needing a new package parks and you add it.
- *Looser:* signature and release hosts too, so the full gate commands can run inside a Step.

**Q4. How many Steps may run at once, and how big may one get?** *Recommended:* three at once; each up to 20% of the
machine's memory (at most 12 GiB) and half the CPU at lower priority; all Steps together at most 60% of memory. That is
the safe default; real peaks are recorded on every Step so the numbers can be set from data after the first runs. Do you
want a lower ceiling (for example 8 GiB and two at once) until then?

**Q5. Should a session you open yourself (`sindri open`) also be sandboxed?** *Recommended: no.* You are at the keyboard,
and the common reason to open one is to do things the sandbox blocks. A session that belongs to a Step run (including one
you reopen or resume) is always sandboxed. Meta lanes stay as they are.

**Q6. GitHub access for Steps.** *Recommended: none in the first release.* Steps commit to their branch in their workspace;
Sindri pushes and opens pull requests from the host. The one-hour token path (SB-7) comes next, for Steps like the
babysitter that must push, and then only to their own branch. Say now if the first Step you want must push.

**Q7. Three things the sandbox won't stop; do you accept them?** (a) An agent can still `pkill` your other processes (hiding
them would break the heavy-job locks and the session list). (b) It can read other repos on the machine (read-only),
including their `.env` files, and, because process information is shared, a determined look can get around the hidden
folders. (c) It can't fill the disk silently, but nothing stops it; Sindri only warns. Alternative for (b): a list of repos a
Step may read, at the cost of Steps that look at a sibling repo failing.

**Q8. Linux first, macOS second?** *Recommended:* the first unattended run is on this Linux box (SB-10). Until macOS
support (SB-9), Steps on a Mac go to your desk (if the Step can run there) or park with "not yet supported". If a Mac must
run Steps first, SB-9 moves ahead of SB-7, never ahead of SB-5a.

**Q9. Inside the sandbox, should Claude Code ask, deny or just run?** The sandbox already bounds what it can do and nobody
is there to answer a question.
- *Option A (recommended): refuse what isn't allowed* (`dontAsk` with an allow list). Predictable, no stalls; the agent adapts.
- *Option B: run everything* (`bypassPermissions`, as your lanes do). Fewer refusals in the first runs. Your repo's own
  settings forbid this mode, so it needs an exception.

**Q10. Should the first runs of a repo be "log only" on the network?** The allowlist is a guess; the first runs may stall on
a host nobody listed. *Recommended: yes, for the network only.* For a new repo, a Step may reach any site for its first ten
runs or seven days, whichever is first. Sindri records every host it reached and shows them on the Blocked list, with a
button to allow each one. The workspace, secrets and credentials stay fully protected, and the status says "learning"
loudly. *Alternative:* strict from the start, and fix each park as it comes.

**Q11. Where does a Step leave its output files?** The scoping Steps write their results into a folder Sindri makes for
each run (the "run directory"). The engine's rule for a read-only Step says it gets no place to write. *Recommended:* keep
the run directory as a folder the launcher provides for every Step (the engine's rule is about the *repository*, not this
folder), so a read-only Step can still hand back its work. The engine lane confirms this reading before the engine builds on
it. *Alternative:* a read-only Step reports only through the report call, with no files.

**Q12. Which Step do you want to run unattended first?** *Recommended: a read-only one (scoping or a review).* It needs only
the model host and the run folder, so it can be on the factory as soon as SB-5a lands, while the code-changing Steps wait for
SB-5b. If the first one you want edits code, say so and SB-5b moves up.

## 14. Revision 2 (autoplan round 1) and review record

Round 1 (2026-10-18): verdicts, product ITERATE, architecture NEEDS_WORK, design BLOCKED (accessibility rule), devex
NEEDS_WORK, security NEEDS_WORK; no CRITICAL. The engine lane's review of rev 1: 3 blocking, 7 issues, 3 suggestions.

| id | What changed |
|---|---|
| R1-01 | §3.1 inventory of the hook state paths, taken from `~/.claude/hooks` and `config/hooks`; exceptions for read-only `hooks/ bin/ bin-r1b/ toolkit` and writable `judge/ context-guard/ digests/ probe/`; the `hooks-run` canary (§6) and a doctor line |
| R1-02 | SB1 and §6: confinement keys on the Step run, not `Attended`; resume reuses the stored plan, `-015 no-stored-plan`; test `Resume_of_a_step_session_is_confined` |
| R1-03 | §3.1, Q2: agent home writable only where the CLI writes, the owner's settings, hooks, skills read-only; credential reads denied in the inner policy; the SB-8 trigger named |
| R1-04 | §3.2 end to end: both halves, net directory exception, `NO_PROXY`, start order before the self-check, `-014 no-forwarder`, `-016 net-forward` |
| R1-05 | SB15 and §3.6: counts per class, `egress.log`, `denials`, `explain`, `exec`, `AW_SANDBOX`, launch-brief paragraph, per-repo allow edit, weekly roll-up; learn mode as Q10 |
| R1-06 | §3.5 works/fails table; cache paths into scratch, `.local/share/NuGet` and `.npm` logs; the env list extended (§3.4); `signing` and `releases` groups; the run branch told to the agent (`AW_SINDRI_BRANCH`); SB-S1 item 9 |
| R1-07 | §8 accessibility table per control; SB-6 done-when includes it |
| R1-08 | §8 owner-voice sentence per `(code, reason)` as SB-0 goldens; `-019` says limits are profile-wide |
| R1-09 | §8 Check again runs every probe, one card of all blockers, `sandbox check` as a pre-flight from doctor and onboarding |
| R1-10 | §7 banner and tmux status line, the defined refusal wording, the escape in the first screen |
| R1-11 | SB7, §3.3: parent slice with total cap, `sandbox.limits.sessions` and `-020`, `RuntimeMaxSec`, the `dotnet`/`npm` shims through `heavy` |
| R1-12 | Header and §2 refs fixed (SC W9 and W11, not WF); the run directory reconciled with engine P8 D8 as Q11 |
| R1-13 | §5 Codex ReadOnly row corrected; table marked provisional pending SB-S1 item 6 |
| R1-14 | §3.4 token in a bound directory, shims read `<dir>/token` |
| R1-15 | §3.4 per-launch gitconfig; the canary does a real commit |
| R1-16 | SB-5 split into SB-5a (ReadOnly) and SB-5b (WriteScope); Q12 |
| R1-17 | §3.7 and SB-11: per-repo declaration at `repo onboard` |
| R1-18 | `sindri sandbox exec` (§3.6, §8) |
| R1-19 | §9 docs list incl. AGENTS.md, CLAUDE.md, launcher doc; §15 is filled by SB-S1 and gates SB-2 to SB-4; a missing `sandbox:` section uses defaults |
| R1-20 | §8 neutral wording for attended, macOS gray "not yet supported", inner "off" is an info line, Health and Settings placement, vocabulary |
| R1-21 | Accepted or fixed in passing (§3.1, §3.3, §3.4, §11): per-run ref directory, disk warning, install scripts off, redacted events, private-address refusal, `CPUQuota` per CPU, the lock sweep, Codex `heavy` accepted, login check accepted, multiplayer line, peaks recorded |
| R1-22 | §3.1 "what it can't stop": the shared `/proc` |
| E1-B1 | SB11, §6: `-014`/`-017` use the NoRuntime park; `-015`/`-016` a new `launch` park kind whose Resume starts a new run; Check again answers the park when every probe passes |
| E1-B2 | SB7, §7: the wall clock pauses on any suspension; a session lost during a suspension makes the answer a Rerun |
| E1-B3 | SB12, §12: `UnavailableSandbox` is the product default; `PassThroughSandbox` and `RecordingSandbox` are test-only (tests and the proof fixture); P8 proves on the pass-through; the real proof is SB-10 |
| E1-I1 | §6, SB11: a limit kill is never auto-retried in the run; the owner retries (new run, branch kept and named) or uses the desk twin |
| E1-I2 | §6, SB-0: `Limit` and the `session.ended` fields are SB-0 types; the mapping is a new engine rule; citations fixed |
| E1-I3 | §6: a final usage sample precedes `session.ended` for every reason |
| E1-I4 | §6: the engine writes `launch.outcome:<launch>`; the sandbox summary is on `StartedLaunch` |
| E1-I5 | SB11, §6, Q8: an unavailable sandbox is "no agent runtime": `deskCapable` to the desk, otherwise NoRuntime |
| E1-I6 | SB5, §3.1, §6: the run ref `sindri/<item>/<run>/work` in its own writable directory; the canary is a real ref update |
| E1-I7 | §12: P8 builds on DT-J2 (#306), no P7; SB-10 needs #295 and P8's U8 |
| E1-S1 | §7: the engine adds the "is the run suspended" read the heartbeat calls |
| E1-S2 | §3.1 item 5: the engine's git calls use `-c core.hooksPath=/dev/null -c core.fsmonitor= -c safe.directory=` and check the `.git` file; SB-5b |
| E1-S3 | §6: `-019` is catalogue-only, no HTTP class |

**Round 2: pending.**

## 15. Spike record

Filled in by SB-S1. SB-2 to SB-4 start only when this section holds the answers they use.
