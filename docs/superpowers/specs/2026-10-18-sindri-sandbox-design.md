# Sindri sandbox phase: confining an unattended Step session on the host

**Status:** design, revision 1 (draft; /autoplan rounds pending, §14) · **Date:** 2026-10-18 · **Owner decisions:** none
yet; §13 asks for them. Inherited: the threat model is honest mistakes, not a deliberate bad actor (README "Gates
guard against…", A §17.28.3, `docs/csharp/bugfix.md` "Threat model"); a Step only tightens its policy (owner, 2026-10-17;
engine P8 D8); Steps run in tmux on the host (README X55) · **Closes:** README X59(a) · **Builds on:** the session launcher
spec `2026-10-15-sindri-session-launcher-design.md` (LCH), SL4, SL8, §3, L-6, and `docs/csharp/sindri-launcher.md`
"Model and policy forms" (the scoping-station spec's L-17); the workflow engine spec
`2026-10-10-sindri-workflow-engine-design.md` (WF), §5.5, §6.5, P8; the blocks-over-MCP spec
`2026-10-18-sindri-blocks-over-mcp-design.md` (BM), the Step session and plain session; the session collector spec
`2026-10-16-sindri-session-collector-design.md` (CO); the first Sindri design `2026-10-07-sindri-design.md` (D), §8.2 and
§13.1, which this replaces for the host (§4); the line spec `2026-10-15-sindri-spec-to-proven-pr-line-design.md` (LN),
LN-WF3, LN-WF6, LN-LCH1; the engine lane's binding constraints for P8 (cited **engine P8 D3, D8, D9, D10, D11**) ·
**Lanes:** the API lane builds the sandbox inside the launcher (SB-1…SB-5); the ideas lane runs the spike and builds the
macOS adapter and the GitHub credential path; the engine lane (P8) consumes the contract and was consulted on it; the
UI lane writes copy (SB-6) · **Next:** /autoplan, then the owner reads §13, then /specToProvenPR takes each phase to PRs.

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
| SB1 | **Every unattended Step session runs confined, or does not run.** There is no "off" switch for a Step launch. A launch whose confinement can't be built or can't be proven parks or fails with a `SND-LAUNCH-0nn` code (§6); it never falls back to running unconfined. Attended opens (`sindri open`), `resume` and Meta lanes are not confined in this spec (Q5) | The owner wants Steps to run unattended; the cost of one unconfined run is the exact thing the spec exists to prevent. SL4 already makes "unattended" one code path |
| SB2 | **Sindri owns the boundary; the agent CLI's own policy is the second layer.** The outer layer is a Sindri-built sandbox around the CLI's whole process tree (§4, bubblewrap on Linux, Seatbelt on macOS) plus a cgroup scope for limits. The inner layer is L-17's per-launch policy file (permission mode, allow and deny rules, the Codex overrides), which gives the agent clear tool-level refusals instead of `EACCES` and keeps working on a target Sindri hasn't taught the outer layer. The outer layer is the guarantee; the inner one is advice the CLI follows (§5) | Cursor, and any added entry, has no usable inner sandbox. A guarantee that depends on three CLIs' sandbox behaviour (which changes by version) isn't one. But the CLIs' rules are cheap and already needed by L-17 |
| SB3 | **Filesystem: read-only everywhere, writable in one place.** The only writable paths are the run's worktree, the carve-out of the repo's git directory that the worktree's commits need (SB5), the Step directory (WF W11) and a private scratch directory. `$HOME` is replaced by an empty tmpfs and rebuilt from an allowlist; the secret stores and Sindri's own state are denied (§3.1). The worktree stays an ordinary directory on the host, readable by the engine after the session ends (engine P8 D10) | Matches the assignment and `StepPolicy`: with `WriteScope` the worktree is the one writable path; without it the Step directory only (engine P8 D8) |
| SB4 | **Network: default deny, one allowlisting proxy, loopback to Sindri.** The session has no network of its own. It reaches (a) Sindri's `/sindri/*` API and `/sindri/mcp` at the same loopback port the launch config names, and (b) the egress proxy, which allows hosts by name from the approved profile (§3.2). Anything not proxy-aware fails closed | The honest mistakes are `curl | sh`, a package install from a typo, a `git push` to a real remote. A per-process netns plus a name-checking proxy is the one mechanism that works the same on Linux and (with Seatbelt's localhost rule) macOS |
| SB5 | **The repo's git directory is mounted read-only, with a carve-out for the run's branch.** A worktree's `.git` is a file pointing into `<repo>/.git/worktrees/<name>`; commits write objects, the branch ref and the worktree's index and HEAD there. The sandbox binds `<repo>/.git` read-only and binds read-write only `objects/`, `worktrees/<name>/`, and `refs/heads/<ns>/` and `logs/refs/heads/<ns>/` for the run's own branch namespace (the launcher creates the directories). `config`, `hooks/`, `packed-refs`, other branches and `refs/remotes` are read-only | Without it, "writable: the worktree" is false, because every `git commit` needs the shared git directory; with the whole `.git` writable, one `git branch -D` or `git update-ref` rewrites the owner's branches. LN-LCH1's `pre-push` hook still refuses pushes (§3.4) |
| SB6 | **No process-ID namespace** (accepted limit, Q7). The heavy-job wrapper finds a session by walking to the nearest `claude` ancestor (`~/.agentic-workflow/bin-r1b/heavy`) and the collector discovers sessions from the process table (CO §4). A pid namespace breaks both. The cost: an agent can still `pkill` a process of the same user. The safety hooks (`block-destructive.sh`) and the deny rules in the inner policy cover the common forms | Honest-mistake model; the lock design is the owner's and stays as is |
| SB7 | **Limits are a cgroup scope plus a watchdog, and they end the session with a named reason.** CPU and memory and pids come from `systemd-run --user --scope` properties; wall time from the `session wrap` watchdog. A kill ends in `session.ended` with `reason: limit` and `detail.kind` `memory` or `wall` (§3.3), which the engine maps to `step.failed(limit)` (engine P8 D3, D9). Wall time pauses while the instance is taken over (§7) | A runaway session must hit its own limit, not take the box down (D §8.2's goal, kept) |
| SB8 | **The engine passes a policy; the launcher maps it.** The engine gives `Attended: false`, `Mode: Open`, the worktree as cwd, `StepRef` and a `StepPolicy` (`WriteScope` → the worktree is the only writable path, otherwise ReadOnly) and **never names a host, argv, bin, environment variable or mount** (engine P8 D8). The sandbox owns every other environment variable and the policy → confinement mapping (§5). `AW_SINDRI_LAUNCH_SECRET` is set by `session wrap` and survives the environment scrub; it is the one `AW_*` credential an agent holds | Keeps the engine out of per-CLI detail; matches L-17's split |
| SB9 | **Confinement is fixed at launch and does not change for the life of the session.** Takeover and handback don't loosen or re-tighten it, and never relaunch (§7; engine P8 D11) | The session is one live process tree; changing a bwrap mount namespace after the fact isn't possible, and relaunching on takeover would end the conversation the human joined. Put to the owner as Q1 |
| SB10 | **No owner credentials in the session, with one named exception: the target's own model login.** The environment is built from an allowlist, not scrubbed from the owner's (§3.4). No `GH_TOKEN`, cloud, registry or ssh credential is passed or mounted. GitHub access is `none` by default; the 1 h agent GitHub App token (`scripts/agent-token.sh`) is minted by the launcher outside the session and delivered through a file only when the profile says `agent-app` (SB-7). The CLI's model login (`~/.claude`, `~/.codex`) is mounted because the CLI can't run without it (Q2) | The assignment's "no owner tokens", and the honest-mistake leak (a token printed into a transcript or log) needs the token to be reachable first |
| SB11 | **Each refusal is its own `SND-LAUNCH` code with a kind.** `-014` to `-019` (§6). Each is labelled **park** (the engine parks the Step with `spec-conflict` carrying the code; the owner fixes the cause) or **step failure** (the engine ends the run `step.failed`, retryable, or sends the Step to its desk twin). The engine routes on the code, never the text (engine P8 D8) | Engine contract |
| SB12 | **The mechanism is a port.** `ISandbox` in `Aw.Sindri.Core/Sandbox/` with `Probe()`, `Plan(SandboxRequest) → Result<SandboxPlan>` (pure), `SelfCheck(SandboxPlan)` and `Wrap(SandboxPlan, argv) → argv`; adapters `BubblewrapSandbox` (Linux), `SeatbeltSandbox` (macOS) and a test-only `RecordingSandbox`. The plan is data (`launches/<id>.sandbox.json`, 0600) that `session wrap` reads; `sindri sandbox show <launch>` prints it | Same shape as the launcher's three ports; a new mechanism is an adapter, never a change to the engine or to `OpenItem` |
| SB13 | **`.devcontainer/` is not the sandbox** (docs/devcontainer.md, readiness spec §4d). Neither is Docker: the Docker socket is denied inside the session (§3.1). Containers stay an option for the "container's tmux later" step of WF §6.5, behind the same `ISandbox` port (§4) | Repo rule; Docker-in-the-agent is root on the host |
| SB14 | **Tool use that needs the box's heavy capacity goes through Sindri's blocks, not through the session.** BM's `sindri_execute_tool_call` runs heavy gates, tests and evidence host-side under the Step's policy and the heavy-job slots. The session's own `dotnet build` still works (it calls the shared `heavy` wrapper, §3.3), but the browser, container and index builds aren't expected to run inside it | One owner of the box-wide cap; keeps the Docker and browser sockets out of the session |

## 2. Terms

- **Confined session:** an agent CLI process tree started by `session wrap` inside the sandbox of §3, inside a cgroup
  scope, in the tmux session `snd-<run>` (LCH SL4, WF §6.5). `session wrap` itself, tmux and the launcher run outside it.
- **StepPolicy:** the engine's per-launch policy (L-17, WF W9): mode, allowed tools, writable paths. This spec fixes the
  two values the engine sends (SB8) and what each means in the sandbox.
- **Sandbox plan:** the data the pure builder produces from the StepPolicy, the launch and the profile: mounts (read-only,
  read-write, denied), the network rules, the environment allowlist, the limits and the credentials to deliver. It is
  hashed (`planHash`) and recorded on `launch.started` (§8).
- **Agent home:** the directory a CLI keeps its login, settings and session records in: `~/.claude` and `~/.claude.json`
  for Claude Code, `~/.codex` for Codex. Today it is the owner's own (Q2).
- **Run area, Step directory:** WF W11 (`$AW_SINDRI_HOME/workflow/runs/<instance>/<step>/`, 0700).
- **Park / step failure:** engine P8 D8's two outcomes of a failed launch. A park is a `spec-conflict` the owner fixes; a
  step failure is `step.failed(launch)` or `step.failed(limit)`.
- **Threat model.** The sandbox stops the honest mistakes: `rm -rf` or a redirect in the wrong directory, a `git push` or
  `gh pr create` from a session that shouldn't, a read of a secret by accident and the leak of it into a transcript, a
  `curl | sh`, a build storm or a runaway test that takes the box down, and an agent that edits another repo or the
  owner's dotfiles. It does **not** try to contain an agent that deliberately probes for an escape: no seccomp filter, no
  pid or ipc isolation, no attempt to hide that a sandbox exists, nothing against a kernel exploit. A review finding that
  needs a crafted escape is answered against this paragraph (the same stance as `docs/csharp/bugfix.md`).

## 3. What the sandbox does

### 3.1 Filesystem

The plan is built as a stack of bind mounts over the host root, bottom to top:

1. **Root, read-only.** `/` is bound read-only (`--ro-bind / /`), with `/dev`, `/proc` fresh and `/tmp` replaced by a
   private directory (below). The agent can read the system, the toolchains and the other repos on the box. It can't
   write any of it.
2. **`$HOME` replaced.** An empty tmpfs over `$HOME`; then the **home allowlist** is bound back: read-only unless noted.
   Defaults, detected by `sindri sandbox check` and listed in the profile (`sandbox.fs.homeAllow`): `.dotnet`, `.nvm`,
   `.cargo`, `.rustup`, `.local/bin`, `.local/share/NuGet` and `.gitconfig` (read-only); `.nuget/packages` and `.npm/_cacache`
   (read-write: restores need them; the tool caches are the one shared mutable state between sessions and the owner,
   Q3); the agent home of the target (read-write, Q2). Everything else in `$HOME`, including `.ssh`, `.aws`, `.config`,
   `.gnupg`, shell histories and the browsers' profiles, doesn't exist for the session. An allowlist, not a deny list, is
   the default for `$HOME` because the deny list can only name what someone thought of.
3. **Outside `$HOME`, a short deny list** over the read-only root, as empty tmpfs or `/dev/null` binds:

   | Denied | Why |
   |---|---|
   | `$AW_STATE_DIR` (default `~/.agentic-workflow`) and `$AW_SINDRI_HOME`, **except** the entries below | The ledger, the profile, the human token, every launch secret, judge and scorer stores, other runs' areas |
   | `/var/run/docker.sock`, `/run/podman`, `/run/user/<uid>` (including the ssh-agent, gpg-agent and dbus sockets) | The Docker socket is root on the host (SB13); the agent sockets sign as the owner |
   | `/etc/ssh` host keys, `/root`, other users' homes | Not the session's |
   | The holdout store and other runs' worktrees (LN-LCH1) | Added by the line's capability policy through the same plan |
   | The macOS keychain files (`~/Library/Keychains`) | The agent App's key and the owner's logins live there (`scripts/agent-token.sh`) |

   The allowed exceptions inside the denied state directories, each exactly one path: `$AW_STATE_DIR/locks/` (read-write,
   the heavy wrapper's `mkdir` mutexes), `$AW_STATE_DIR/heavy-slots-cap` and `heavy-meta-reserve` (read-only),
   `$AW_STATE_DIR/bin-r1b/heavy` and the other wrapper files it names (read-only), the Step directory (read-write), and the
   one discovery file the `sindri` CLI reads to find the loopback port (read-only; the lane settles which, SB-S1). No ledger,
   token or secret file is ever among them.
4. **Writable.**
   - **The worktree** (`<repo>/.claude/worktrees/sindri-<instance>-<run>`, the cwd; LN-WF2's phase worktree when the line
     owns one) when the policy has `WriteScope`; read-only otherwise (engine P8 D8).
   - **The git carve-out** (SB5), same condition.
   - **The Step directory**, always: a ReadOnly Step still writes its outputs there (WF W9, W11). It is the only writable
     path of a ReadOnly Step.
   - **A private scratch directory** `$run-area/tmp`, mounted as `/tmp` and `$TMPDIR`, removed when the run closes. Not
     a RAM tmpfs: it would count against the cgroup and fill silently.
   - **The tool caches** of step 2, and the agent home.
5. **Not unshared:** user, pid and ipc namespaces (SB6, the user namespace is bubblewrap's own). The session runs as the
   owner, so files it creates in the worktree are the owner's, with the owner's umask, and the engine reads the worktree's
   `HEAD` and `git status` from outside after the session ends with no permission change (engine P8 D10). The sandbox
   never rewrites ownership, uses an overlay or copies the worktree; the end of a session removes only the scratch directory.

What it can't stop, said plainly: reading another repo's `.env` (the root is readable), `pkill` (SB6), a write through a
tool cache to poison a later build, filling the worktree's disk. The profile's `sandbox.fs.denyExtra` takes more paths.

### 3.2 Network

On Linux the session runs in its own **network namespace** with only a loopback interface. Three listeners are made
reachable inside it, each a unix socket bound into the sandbox and a forwarder (`socat`, or a tiny helper if it is absent;
SB-S1 decides) listening on `127.0.0.1:<port>` in the namespace:

| Inside | Outside | Allowed |
|---|---|---|
| `127.0.0.1:<sindri port>` (the same number the per-launch MCP config and `AW_SINDRI_*` name, so nothing is rewritten) | Sindri's API and `/sindri/mcp` on the host loopback | The Sindri port only. Nothing else on the host loopback (the Prism dashboard, Ollama, a dev server) is reachable |
| `127.0.0.1:<proxy port>`, exported as `HTTPS_PROXY`/`HTTP_PROXY` (and lower-case) | The egress proxy, a Sindri service in `session wrap`'s process tree | `CONNECT host:port` where the host (by name; Sindri resolves it, the session has no DNS) is on the allowlist |
| nothing else | | |

The proxy does no TLS interception: it checks the requested host name and port, then relays bytes. Every refused request is
counted; the first per run is a ledger event (§8), the rest a counter. The allowlist is a set of named host groups in the
approved profile (`sandbox.network.groups`), each Step capability picking groups (defaults, Q3):

| Group | Hosts | Given to |
|---|---|---|
| `model` | the target's provider: `api.anthropic.com` for `claude-code`; `api.openai.com` and `chatgpt.com` for `codex`; a catalog entry names its own | every Step |
| `packages` | `api.nuget.org`, `*.nuget.org`, `registry.npmjs.org` | `WriteScope` Steps (a restore on a changed lockfile) |
| `github` | `github.com`, `api.github.com` | only with `credentials.github: agent-app` (SB-7) |

The targets' telemetry and update checks are turned off in the inner policy (`DISABLE_TELEMETRY`,
`CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`, Codex's equivalents), so a denied telemetry host isn't noise. A tool that
ignores the proxy variables has no network, which is the intended failure.

On macOS there is no network namespace. Seatbelt denies all network and allows `network-outbound` to `localhost:<proxy
port>` and `localhost:<sindri port>` only; the same proxy applies (SB-9).

### 3.3 Process limits, and how they compose with the heavy-job wrapper

`session wrap` starts the agent as `systemd-run --user --scope -p … -- bwrap … -- <agent argv>`; the tty is the tmux
pane's, so attach works as before (WF §6.5). Limits, with defaults the profile may change (`sandbox.limits.*`, Q4):

| Limit | Mechanism | Default | When hit |
|---|---|---|---|
| Memory | `MemoryHigh` (80% of max: reclaim and slow down), `MemoryMax`, `MemorySwapMax=0`, `OOMPolicy=kill` (one OOM kill takes the whole scope) | `auto`: 20% of the box's RAM, at most 12 GiB | The scope is killed; `session.ended reason: limit`, `kind: memory` |
| CPU | `CPUWeight=50` (below an interactive shell's 100) and `CPUQuota` of 50% of the cores | 50% | Throttled only; never a kill |
| Processes | `TasksMax` | 2048 | New forks fail with `EAGAIN`; recorded once as `sandbox.limit-hit {kind: pids}`; no kill |
| Wall time | the `session wrap` watchdog: SIGTERM, 10 s, then `systemctl --user kill --signal=SIGKILL --kill-whom=all` | the Step's `timeout` plus 10 minutes; 4 h when the Step has none; at most `workflow.steps.longMax` plus 10 minutes (LN-WF7) | `session.ended reason: limit`, `kind: wall`. The engine's own `timeout` fires first and parks or fails the Step; this is the backstop for a wrap the engine lost |
| Disk | not limited | n/a | Said under "can't stop" in §3.1 |

**The heavy-job wrapper stays the owner of box-wide concurrency; the cgroup only caps one session's size.** The two answer
different questions: the wrapper decides how many heavy jobs run at once on the box (`AW_HEAVY_SLOTS`, the owner's
`heavy-slots-cap`, the memory guard, the meta reservation), the scope decides how big one session may get. Rules:

1. A session is not a heavy job. It spends most of its time waiting on a model; it takes no slot of its own.
2. A command the agent runs through the `heavy` wrapper (`dotnet build`, tests) takes its slots as today: the wrapper's
   locks are on `$AW_STATE_DIR/locks/` (read-write, §3.1), it finds the session by walking to the `claude` ancestor
   (SB6), and it reads the cap files (read-only). Its children are inside the scope, so they count against the memory
   ceiling.
3. **A Step that holds a heavy slot already** (LN-WF3: the engine took one for `waitFor: { resource: heavy }`) gets
   `AW_HEAVY_LOCK_HELD=1` in its environment from the launcher, so the agent's own `heavy` call runs straight through
   instead of waiting for a second slot (the wrapper's nested-call rule). A Step that doesn't hold one gets nothing, and
   its `heavy` calls take slots as a lane session's would.
4. The default memory ceiling is chosen so that the box-wide cap times the ceiling is a number the owner can see: the doctor
   line prints `slots x ceiling` against RAM and warns when it is over 100% (the ceiling is not a reservation; the
   wrapper's memory guard still gates the third slot).
5. The cgroup and the wrapper never see each other's state. Killing a session for `limit` doesn't release a slot by magic:
   the wrapper's own session lock is a `mkdir` the dead holder's `trap` would have removed, and a SIGKILL skips it. The
   launcher's session-end sweep removes `locks/heavy-session/<pid>.lock` of the dead scope's ancestor pid (a rule the
   wrapper already tolerates for stale locks; SB-3 adds the test).

### 3.4 Credentials and environment

**Environment from an allowlist.** `session wrap` starts the CLI with `env -i`-style construction from the plan:
`PATH` (the toolchain dirs plus the shim dir), `HOME`, `LANG`, `TERM`, `TMPDIR`, `USER`, `SHELL`, the `DOTNET_*` and
`NUGET_*` variables the home allowlist implies, the proxy variables, `AW_SINDRI_INSTANCE`, `AW_SINDRI_RUN_DIR`,
`AW_SINDRI_WORKTREE`, `AW_SINDRI_CHILD`, `AW_SINDRI_ITEM`, `AW_SINDRI_LAUNCH`, `AW_SINDRI_HOME` only when it isn't the
default (LCH §3 "The agent's environment") and `AW_SINDRI_LAUNCH_SECRET` (engine P8 D8, SB8). It is one list in code with a
test that fails on any name matching `*_TOKEN`, `*_KEY`, `*_SECRET`, `*PASSWORD*`, `AWS_*`, `GH_*`, `GITHUB_*` or
`AGENT_APP_*` unless it is the launch secret or the target's model credential (below). LN-LCH1's scrub (unset
`GH_TOKEN`, `GITHUB_TOKEN`, `GH_ENTERPRISE_TOKEN`, empty `GH_CONFIG_DIR`) is subsumed: the allowlist never carried them.
`AW_SINDRI_LAUNCH_SECRET` is already in a 0600 file for the launcher (API §8.1) and in the process environment only,
never on argv; the agent's transcript scrubber (`Aw.Redaction`) treats it as a secret.

**Model login.** The one owner credential in the session is the target's own: Claude Code's `~/.claude/.credentials.json`
or the macOS keychain item it reads, Codex's `~/.codex/auth.json`, or `ANTHROPIC_API_KEY`/`OPENAI_API_KEY` for a box that
uses API-key mode. The CLI cannot run without it. Two ways to hold it, Q2: (A) mount the owner's own agent home
read-write, which also gives Step sessions the owner's hooks and settings (the toolkit's safety hooks keep working, the usage
readers and the collector find the transcripts where they look today); (B) a separate agent home under
`$AW_SINDRI_HOME/agent-home/<target>/` that the owner logs in to once (`sindri sandbox login <target>`), which keeps the
owner's settings, history and memory out of Step sessions but needs the readers to follow `CLAUDE_CONFIG_DIR` and the
hooks to be installed into it. **Recommendation: A for the first unattended run, B as SB-8 once the readers can follow
the directory.** On macOS the keychain item is readable to the CLI by Seatbelt rule for that one service name only.

**GitHub.** `credentials.github: none` is the default and the only value v1 builds before SB-7. No `gh`, no push credential:
a Step that needs a push or a PR emits an effect (WF §5.7, P6) that Sindri sends host-side; this matches LN-WF6's `build`
capability ("no push, no `gh`, no forge credential"). The worktree gets LN-LCH1's `pre-push` hook that refuses, which the
git carve-out leaves intact (`hooks/` is read-only). With `agent-app` (SB-7):

1. The launcher, running as the owner outside the session, runs `scripts/agent-token.sh --repo <name>` (the script is
   "owner/session launcher only", spec §4d) and writes the token to `$AW_SINDRI_HOME/launches/<launchId>.gh-token` (0600),
   bound read-only at a fixed path inside the sandbox. It is never in the environment, an argument or tmux's environment.
2. Two shims first on `PATH` (`gh`, and a git credential helper) read that file at each call. `session wrap` mints a fresh
   token every 45 minutes and replaces the file atomically, since a Step may run longer than the token's hour
   (LN-WF7's `longMax` is 2 h).
3. The `github` host group opens in the proxy; the pre-push hook is replaced by one that allows only the run's branch.
4. A mint failure (key missing, no network, App uninstalled) is `SND-LAUNCH-018`, a step failure.

The App's private key never enters the session: it lives in the keychain or `AGENT_APP_KEY_FILE`, outside the home
allowlist (§3.1).

## 4. Mechanism options

| Option | Filesystem | Network | Limits | Verdict |
|---|---|---|---|---|
| **bubblewrap** (`bwrap`; installed on the owner's box, 0.9.0; works under the box's AppArmor user-namespace restriction, verified 2026-10-18) | Full: read-only root, tmpfs over `$HOME`, per-path binds, deny by overmount | Own net namespace: none, plus unix-socket forwarders (§3.2) | None; pair with a cgroup scope | **Linux primary.** It is the only unprivileged tool that can do "read-only root with a rebuilt home" and a per-session network namespace, it is what Claude Code's own sandbox uses on Linux, and it is already on the box |
| **systemd-run `--user --scope`** (cgroup v2; `cpu memory pids` are delegated on the owner's box, verified) | None | None (`IPAddressDeny` needs BPF a user manager can't load) | Memory, CPU, tasks, with kill and accounting | **Linux limits.** Used with bubblewrap, not instead of it. No user manager (an ssh session without lingering) is `-014` |
| **Landlock** (in the kernel's LSM list here) | Allowlist only: it can grant, it can't carve a deny out of a granted tree, and it can't hide a path (a denied path is `EACCES`, not absent) | TCP port rules only (ABI 4+); no name filtering | None | **Fallback adapter, not built in v1.** Fits where `bwrap` is blocked (a distro without unprivileged user namespaces); the home-allowlist design in §3.1 happens to suit it. Codex uses it internally |
| **Containers** (rootless podman, Docker) | Full, but the worktree needs a user-namespace UID mapping, so files the engine reads from outside are owned by a subuid, against engine P8 D10; the image must carry the toolchain | Full | Full | **Not now.** D §8.2's design; it is the "container's tmux later" of WF §6.5, a heavier change (image per repo, the mirror and bundle flow) that this spec neither blocks nor needs. Same port, new adapter |
| **Devcontainer** | n/a | n/a | n/a | **No.** `.devcontainer/` is for humans, never the agent sandbox (docs/devcontainer.md, SB13) |
| **Firejail** and similar setuid sandboxes | Good | Good | Some | **No.** Not installed, setuid on the owner's box, and a second mechanism for no gain over `bwrap` |
| **sandbox-exec** (Seatbelt, macOS) | Full by path and regex, deny rules; `file-read*` can name the keychain files | `network-outbound` by `localhost:port` or IP, no names | None (the watchdog does memory by polling, §3.3) | **macOS primary** (SB-9). Deprecated by Apple but what Claude Code's and Codex's own macOS sandboxes use; the profile is generated from the same plan |

**Lean on, wrap, or both: both, with Sindri's layer the guarantee (SB2).**

| Question | Claude Code's built-in sandbox | Codex's built-in sandbox | Recommendation |
|---|---|---|---|
| What it gives | Settings `sandbox` key (bubblewrap on Linux, Seatbelt on macOS): Bash-tool commands confined to the cwd, a network allowlist by domain, `autoAllowBashIfSandboxed` | `--sandbox read-only\|workspace-write\|danger-full-access`, `sandbox_workspace_write.writable_roots`/`network_access` (`-c`), `--ask-for-approval`; Landlock/bubblewrap with seccomp | Real, but each covers the CLI's own tool calls only, differs by version, and is absent in Cursor |
| Does it cover the whole session tree? | Only Bash-tool children; the CLI process, its MCP servers and hooks run outside it | Same for commands the model runs | No: that's why the outer layer wraps the CLI itself |
| Nested inside Sindri's bwrap? | Needs a nested user namespace, which an AppArmor policy may refuse | Same | **Off by default** (`sandbox.inner: auto`): `SelfCheck` tries the inner one once per box and keeps it only if it starts; with it off, the outer layer is the only sandbox and the inner policy is permissions only (SB-S1 records the result per pinned version) |
| Use it for | The permission rules (`permissions.allow`/`deny`), mode, and `sandbox.network` allowlist as the inner copy of §3.2 | `approval_policy="never"` plus the overrides | Always: the inner policy file is the L-17 `policy` form (§5). It is advice with a good error message, not the boundary |

## 5. Per-target forms

The engine's `StepPolicy` is mapped by the launcher in two places: the **outer plan** (§3, the same for every target) and
the **inner policy** (L-17's `policy` form, one per catalog entry). Because there are no prompts to answer when no human is
attached, the inner policy never relies on one: a call outside the policy is refused, not asked about.

| Engine input | Outer plan | Claude Code (`claude-settings-file`, `--settings <path>`) | Codex (`codex-overrides`, `-c`) | Cursor (`cursor-config`) |
|---|---|---|---|---|
| `Mode: Default`, `WriteScope` | worktree and carve-out writable, Step dir writable | `permissions.defaultMode: "dontAsk"` with `allow` for `Read`, `Edit`, `Write` under the worktree and Step dir, `Bash` (the outer layer is what bounds it), `mcp__sindri-step__*`; `deny` for `Read` of the denied paths, `Bash(git push:*)`, `Bash(gh:*)`, `Bash(rm -rf /*)`-style rules | `sandbox_mode="workspace-write"` and `sandbox_workspace_write.writable_roots=[<worktree>, <step dir>]`, `network_access=false`, `approval_policy="never"` (or `danger-full-access` inside Sindri's layer when the inner one can't start, §4) | **Reserved.** Parks `-015` until a box with the CLI verifies a config argument (as L-17) |
| `ReadOnly` (no `WriteScope`; AllowedTools empty = the CLI's defaults for that mode) | Only the Step dir writable | `dontAsk`, `allow` `Read`, `Glob`, `Grep`, `Edit`/`Write` under the Step dir only, `mcp__sindri-step__*` and the read-only `Bash` set (`git status`, `git diff`, `git log`, `git show`, `sindri`); `deny` `Edit`/`Write` elsewhere | `sandbox_mode="read-only"` with the Step dir as the one writable root, `approval_policy="never"` | Reserved |
| `AllowedTools` non-empty | no change | the list replaces `allow`'s tool entries; the deny rules stay | maps to the nearest `-c` set; a tool Codex can't express is `-015` | Reserved |
| A Step only tightens (owner, 2026-10-17) | The profile's deny list and network groups are the floor; a Step can remove a group from itself, never add one | Same for rules | Same | Same |

The exact setting names are **pinned by a probe**, not assumed: SB-S1 runs each pinned CLI version under the plan and
records which keys the CLI honours, and the built-in forms are tested against that record (as the launcher probes the
agent variables, WF §6.2). The Claude settings file also sets `sandbox.enabled: false` unless the box's `sandbox.inner` is on,
sets `disableBypassPermissionsMode: "disable"`, and carries the toolkit's safety hooks only when the agent home is the
owner's (Q2). Whether `dontAsk` or `bypassPermissions` is better inside the sandbox is Q9.

**Targets that can't be confined.** A catalog entry whose `policy` form is `none` or reserved, whose `open` template the
plan can't wrap (a GUI app that owns its process, the `none` host), or whose `bin` the plan can't make runnable inside the
sandbox (a binary under a denied path) parks with `-015`. This extends `SND-LAUNCH-013`: `-013` is "the entry has no form
for what the Step asked"; `-015` is "the entry has the form but the sandbox can't confine what the form lets through".
`argvHash` (SL8) is unchanged: the plan and the policy file are per-launch values, not hashed, so one approval holds
across runs. The sandbox's argv wrapper (`systemd-run … bwrap …`) is built by Sindri and is not part of any entry.

## 6. Launch flow, refusals and codes

`ILauncher.Launch` for `Attended: false` becomes (LCH §3's order, with the new steps in bold): resolve the agent → SL8
(`-012`) → L-17's form check (`-013`) → **build the plan from the StepPolicy (pure; `-015`)** → **probe the mechanism
(cached per box, re-run by Check again; `-014`)** → **prepare credentials (`-017`, `-018`)** → budget (E-28) → **write the
plan file** → **self-check (`-016`)** → start in the host (`session wrap` reads the plan, starts the scope and the
sandbox, starts the proxy and forwarders) → return. The self-check and the plan build sit inside the 4 s host-start stage
of LCH §3 (the self-check is one throw-away `bwrap … true` plus four path probes, under 400 ms).

The self-check proves the plan, not the box: inside the planned sandbox, a canary for each class of denial must fail
(read of a denied path, write outside the writable set, connect to a non-allowed host) and a canary for each grant must
succeed (write in the worktree and the Step dir, `git -C <worktree> commit --dry-run`, connect to the Sindri port).

| Code | Kind | Meaning | Detail | Fix text |
|---|---|---|---|---|
| `SND-LAUNCH-014` | **park** (`spec-conflict`) | The sandbox isn't available on this box | `reason`: `no-bwrap`, `no-user-namespace`, `no-user-manager`, `no-cgroup-controllers` (which of `cpu memory pids` is missing), `unsupported-platform` (macOS before SB-9) | "Install `bubblewrap`, or enable unprivileged user namespaces for it; run `loginctl enable-linger $USER`; then **Check again**. Run `sindri sandbox check`" |
| `SND-LAUNCH-015` | **park** (`spec-conflict`) | The target or its form can't be confined to this policy | `reason`: `form-none`, `form-reserved`, `tool-not-expressible` (names the tool), `bin-denied` (its path), `host-not-tmux`, `worktree-outside-repo` | "Use a built-in entry, or add the form the entry lacks; or change the Step's policy (`sindri launch show <id>` prints what it can express)" |
| `SND-LAUNCH-016` | **park** (`spec-conflict`) | The self-check failed: the sandbox didn't deny or allow what the plan says | `canary` (which), `expected`, `observed` | "This is a box or profile fault, not the Step's. Run `sindri sandbox check --launch <id>`; fix the path named; resume" |
| `SND-LAUNCH-017` | **park** (`spec-conflict`) | The target's model login is missing or expired for Step sessions | `target`, `agentHome`, `reason`: `no-login`, `expired` | "Log in to `<target>` as yourself and **Check again**" (with Q2 B: `sindri sandbox login <target>`) |
| `SND-LAUNCH-018` | **step failure** (`step.failed(launch)`; the Step may retry or use its desk twin) | The agent GitHub App token couldn't be minted (only with `credentials.github: agent-app`) | `reason`: `no-key`, `http-<status>`, `no-network`; the message is `agent-token.sh`'s own, scrubbed | "Check the App key and installation (`scripts/agent-token.sh --verbose` at a terminal); retry" |
| `SND-LAUNCH-019` | **step failure** (`step.failed(limit)`, never auto-retried) | A sandbox limit ended the session | `kind`: `memory` or `wall`; `limit`; `peak`; the session id | "The Step used its ceiling. Raise `sandbox.limits.*` in the profile for this Step, or split the Step" |

`-014` to `-017` are launch refusals: the session never starts, the launcher writes `launch.failed` with the code, and the
engine parks with `spec-conflict` carrying it (engine P8 D8). `-018` is also before the start. `-019` is not a refusal at
launch: it is the code on `session.ended` and on the `step.failed(limit)` the engine writes (§8), so the same catalogue
names it and the same route resolves it. HTTP classes: `-014` Unavailable (503), `-015` and `-016` Conflict (409),
`-017` Conflict (409), `-018` Unavailable (503), `-019` Conflict (409). Each gets a catalogue row in `SindriErrors` with
golden messages. A viewer failure never changes any of this (LCH SL5).

## 7. Takeover and handback

**Decision SB9, recommended: confinement stays exactly as launched.** What the engine does is fixed (engine P8 D11):
a park or takeover suspends the run but keeps the session; the human joins the same tmux session; the agent's launch
secret is refused for `complete` while taken over, so only a human report (`via: desk`) is accepted; handback resumes the
watch on the same launch id; no second session is ever started. The sandbox adds:

| Moment | Confinement | Limits | The session |
|---|---|---|---|
| Takeover (the human types in the pane, or `OpenItem` with a live instance) | **Unchanged.** The agent process tree is the same confined tree; the human is talking to it | Memory, CPU and pids keep applying. The **wall clock pauses**: `session wrap` learns the state from the heartbeat reply (`paused: true`, an additive field on the existing route, API §2.5) and counts only unpaused time, so a kill never lands in the middle of a conversation the human joined; the engine's `takeoverIdle` (WF §5.5) is the human's reminder | Kept |
| Handback | **Unchanged.** Nothing is re-tightened because nothing was loosened | The wall clock resumes with what remained | Kept; the same launch id |
| The human needs to do something the agent can't (read a secret, push) | Do it in another terminal or tmux window of your own: a new window's shell is outside the sandbox and you are the owner. The sandbox is the agent's, not yours | n/a | Kept |
| A taken-over session hits a limit | The scope is killed as for any session | | `session.ended reason: limit`; the engine, which owns the suspension, applies its rule for a session that ends under a takeover (WF §5.5/§5.8; the park's resume action) |

Why not relax on takeover: a live bubblewrap mount namespace can't be changed, so "loosen" would mean a relaunch, which
would end the conversation the human joined and break engine P8 D11. Why not re-tighten on handback: nothing to
re-tighten. The cost: a human who joins and tells the agent "go ahead, push it" finds the agent refused. That is the
sandbox working; the human pushes from their own terminal or hands back with a different instruction. Q1 asks the owner to confirm.

When a run closes (report decided, `step.failed`, cancel, instance ends, engine P8 D11) the host stops the tmux session,
the scope ends with its last process, the proxy and forwarders stop, and `session wrap` removes the plan file, the
token file and the scratch directory. **The worktree is left as it was, readable by the engine's process**
(engine P8 D10); purging it is the instance's own cleanup (LN-WF2), never the sandbox's.

## 8. What the owner sees

**Ledger events (additive; API §9 rules; key patterns follow LCH L-10).**

| Event | When | Fields |
|---|---|---|
| `launch.started` gains `sandbox` | every unattended start | `{mechanism: bwrap|seatbelt, planHash, writable: [path…], network: [group…], inner: on|off, limits: {memoryMax, cpuQuota, tasksMax, wall}, credentials: {github: none|agent-app}}`. Paths are listed, contents never |
| `sandbox.denied` (public) | the first refused request or denied path the proxy and the inner policy report per run | `{launchId, kind: network|policy, target, rule}`; later denials are counted on `session.ended` |
| `sandbox.limit-hit` (public) | `TasksMax` reached (once per run) or `MemoryHigh` crossed | `{launchId, kind: pids|memory-high, peak}` |
| `session.ended` gains `reason: limit` | a limit kill | `{kind: memory|wall, limit, peak}`; with `exit` and `lost` this is the closed set the engine maps (engine P8 D3, D9: `step.failed(exit\|lost\|limit)`) |

`launch.failed` carries `-014` to `-018` as for any code. A sandbox kill is **always** `session.ended` with a reason, never
a bare disappearance, so the engine's 120 s silence rule (`lost`) doesn't mislabel it: `session wrap` sends the exit call
with the reason before it exits.

**Surfaces.**
- `sindri doctor`: a `sandbox` section, e.g. `ok  sandbox: bubblewrap 0.9.0 · user namespaces ok · cgroup cpu memory pids · self-test 180 ms`, `ok  limits: 3 slots x 12 GiB = 36 GiB of 62 GiB`, `warn limits: 3 slots x 12 GiB exceeds RAM (the wrapper's memory guard gates slot 3)`, `fail sandbox: no user manager (loginctl enable-linger $USER)`, `ok  inner sandbox: claude-code 2.1.302 off (nested user namespace refused); codex 0.139.0 off`. A failing check is the `-014` reason, so the park's fix text and the doctor fix are the same sentence.
- `sindri sandbox check [--launch <id>] [--json]`: runs the probe and the self-check on a throw-away plan; `--launch` replays a run's plan. `sindri sandbox show <launch>` prints the plan: the mount table (ro, rw, denied), the network groups, the environment names (never values), the limits. Both are `query`-class and read-only.
- `sindri status` and `sindri agents`: a confined session shows `confined` and its limits headroom (`mem 2.1/12 GiB`); a session that isn't (an attended open) shows `unconfined`. The collector's row reads the plan file's presence.
- The app (copy for the UI lane, SB-6): the Agents sheet gets a "Confined" line with the writable path and the network groups; the park card for `-014` to `-017` shows the fix text with a **Check again** button (`ProbeLaunchTargets`, LCH §6.6); a `limit` ending reads "Ran out of memory (12 GiB) after 41 min" with the Step's retry or desk twin. Nothing new on Home.
- A first refusal reaches the owner as the Step's existing park row in Needs you (WF §6.4); the sandbox adds no notifier (X50).

## 9. Profile keys and environment

Under `sandbox:` in the profile (S1's schema; each key is a re-approval; one new `docs/csharp/sindri-profile.md` section,
checked by `scripts/check-profile-doc.mjs`). A key can only tighten a Step; the profile as a whole is the owner's.

| Key | Default | Meaning |
|---|---|---|
| `sandbox.limits.memory` | `auto` (20% of RAM, max 12 GiB) | `MemoryMax` per session; `MemoryHigh` is 80% of it |
| `sandbox.limits.cpu` | `50%` | `CPUQuota` as a share of all cores; `CPUWeight` stays 50 |
| `sandbox.limits.tasks` | 2048 | `TasksMax` |
| `sandbox.limits.wallGrace` | `10m` | Added to the Step's `timeout` for the watchdog |
| `sandbox.fs.homeAllow` | the detected list of §3.1 | Entries under `$HOME` bound into the session, each `ro` or `rw` |
| `sandbox.fs.denyExtra` | `[]` | More denied paths, appended to the built-in list |
| `sandbox.network.groups` | the table of §3.2 | Named host groups and which Step capability gets them |
| `sandbox.inner` | `auto` | `auto`, `on`, `off`: the CLI's own sandbox inside Sindri's (§4) |
| `sandbox.credentials.github` | `none` | `none` or `agent-app` (SB-7) |
| `sandbox.agentHome` | `owner` | `owner` or `isolated` (Q2; `isolated` is SB-8) |

No environment variable switches the sandbox off. `.env.example` gets only paths a test needs to redirect
(`AW_SANDBOX_BWRAP`, `AW_SANDBOX_SYSTEMD_RUN`, for the fake binaries in tests), checked by `scripts/check-env-example.sh`.
`deps.yaml` declares the new `cli` edges (`bwrap`, `systemd-run`, `socat` where used; DEPS undeclared-edge check) in SB-0.

## 10. Testing

Per the C# rules (TDD, lanes, fakes with contract tests). Red first; builders on Sonnet.

| What | Cases | Lane |
|---|---|---|
| Plan builder (pure, `unit`) | `ReadOnly_policy_makes_only_the_step_dir_writable`, `WriteScope_adds_the_worktree_and_the_git_carve_out`, `Home_is_empty_unless_allowlisted`, `State_dir_is_denied_except_the_locks_and_cap_files`, `Environment_has_no_name_matching_a_secret_pattern_except_the_launch_secret`, `A_step_cannot_widen_the_profile_floor`, `Docker_socket_is_always_denied`, goldens of the mount table per policy | unit |
| Contract base | `ISandboxContract` run against `RecordingSandbox` and `BubblewrapSandbox` (the real one in `integration`, skipped with a named reason where `bwrap` is absent): write outside the writable set fails, a denied path is absent, `git commit` in the worktree works, `git branch -D other` and `git push` fail, a connect to the host's other loopback port fails, to the Sindri port succeeds, to a non-allowed host through the proxy is refused, to an allowed one is relayed (against a local stub, never the internet) | integration |
| Limits | A memory hog under a 256 MiB scope ends `session.ended reason: limit kind: memory`; a sleeper under a 2 s wall ends `kind: wall`; a pause during takeover stops the wall clock; `TasksMax` shows `EAGAIN` and one `sandbox.limit-hit`; the stale `heavy-session` lock is swept | integration |
| Heavy composition | Inside the sandbox, the real `heavy` wrapper (a stub command) takes and releases a `locks/` slot; with `AW_HEAVY_LOCK_HELD=1` it runs straight through | integration |
| Launcher | each of `-014` to `-018` from a fake `ISandbox`/probe, each with its kind in the golden, the engine's routing table (`park` vs `step failure`) covered by a `Code_kinds_match_the_engine_table` test that reads one table both sides use | unit |
| Per target | the built-in forms (`claude-settings-file`, `codex-overrides`) against the SB-S1 record; `cursor-config` parks `-015` | unit |
| Proof (owner-run, SB-10) | §12 | heavy |

Proofs that start tmux use a private socket and unset `TMUX` (the team-socket hazard, `scripts/test-tmux.sh`); no proof
touches the real `$AW_SINDRI_HOME` or the real agent home (they use a throw-away `HOME` with a copy of only the
login file; never a temp HOME for dotnet, which leaks a NuGet cache per run).

## 11. What stays out

- Attended opens and Meta lanes (Q5); the sandbox is for the unattended factory.
- Containers, a pid namespace, seccomp and anything aimed at a deliberate escape (§2).
- A model proxy with a dummy credential (D §8.2's W1): the login is mounted instead (§3.4); revisit with containers.
- Disk quotas, per-session network bandwidth, and content inspection of allowed traffic.
- Bounded tool-less jobs (triage, labeling, judge questions): they use `claude -p` (X56) and aren't Steps.
- Windows.
- Cursor, until a box with the CLI verifies a config argument (reserved, `-015`).

## 12. Phases

Each phase is one PR, driven by /specToProvenPR in its lane, built red first. Sizes: S a few files, M one subsystem
slice, L more. **(owner)** steps run on the owner's box. The API and engine lanes' phases are those named in the README
build order. The critical path the lead named is P10a → P8 → P7 → DT-J2 → P10b/U5'/U6; the sandbox must be on `main`
before P8's unattended proof, and nothing in it waits on anything but P10a's ports.

| Phase | What | Files | Size | Depends on | Done when | Lane |
|---|---|---|---|---|---|---|
| **SB-S1** | **Spike (no product code).** On the owner's box, with throw-away scripts outside the repo: (1) interactive Claude Code and Codex under `bwrap` with `--new-session` or without, inside tmux, then attach, type, and detach work; (2) `~/.claude.json` and the agent homes: which paths must be rw for a full session (the atomic-rename problem on a bound single file); (3) the unix-socket forwarder (`socat` or a helper) carries Sindri's MCP and the CLI's HTTPS through the proxy; (4) `systemd-run --user --scope` under tmux keeps the tty, and the wrap can read `memory.events` and the scope's `Result` after an OOM kill; (5) nested inner sandboxes for each pinned CLI (§4); (6) which settings keys and `-c` keys each pinned CLI honours (§5); (7) which discovery file `sindri` reads; (8) the nested-heavy rule with the real wrapper | scratch dir; findings into this spec's §15 and `docs/csharp/sindri-sandbox.md` (SB-0) | S | none | Each of the eight answered with the command and the CLI version; a "no" on (1) or (3) goes back to the owner before SB-2 starts | ideas |
| **SB-0** | Contracts: `docs/csharp/sindri-sandbox.md` (plan file schema, profile keys, codes `-014` to `-019` with kinds, events, doctor lines), the catalogue rows and goldens, `deps.yaml` `cli` edges, `docs/csharp/sindri-profile.md` and the profile schema keys (`sandbox:`), `.env.example`, the README X59(a) row, the engine's code-kind table as one shared data file | `docs/csharp/sindri-sandbox.md`, `SindriErrors.cs` rows, `SindriProfile` schema + `ProfileRules`, `deps.yaml`, `.env.example`, `docs/csharp/sindri-profile.md`, `tests/…/Golden/…/errors` | S | none (starts beside the spike) | `check-profile-doc`, `check-deps`, `check-env-example` pass; the error goldens pin all six codes with their kind; the engine lane reads the kind table | api |
| **SB-1** | The pure core: `ISandbox` and the types, the plan builder, the mount stack and deny lists, the environment allowlist, the limits parsing (`auto`), the host-group rules, `planHash`, the plan-file schema, `sandbox show`'s renderer | `src/Sindri/Aw.Sindri.Core/Sandbox/*`, `…Features/Sandbox/`, `tests/Aw.Sindri.Tests/Sandbox/*` (goldens), `tests/Aw.Architecture.Tests` | M | SB-0 | The plan-builder cases of §10 pass; the mount-table goldens for `ReadOnly` and `WriteScope`; the environment test fails when a `GH_TOKEN` is planted | api |
| **SB-2** | `BubblewrapSandbox`: argv builder from the plan, the git carve-out, the tmpfs home, the self-check canaries, the contract base | `…Infrastructure/Sandbox/Bubblewrap*`, `…Testing/ISandboxContract` | M | SB-1, SB-S1 | The contract base passes on the real `bwrap` (§10): a commit works, `git branch -D other` and `git push` fail, `~/.ssh` is absent; the self-check refuses a plan whose canary fails | api |
| **SB-3** | Limits and the end reason: the scope properties, `session wrap`'s watchdog and the pause on takeover, `memory.events` polling, the `limit` end reason and `-019`, the stale heavy-lock sweep, the additive `paused` heartbeat field | `…Infrastructure/Sandbox/SystemdScope*`, `…Host/Commands/SessionWrap*`, `…Features/` (heartbeat), goldens | M | SB-1, SB-S1; **SL-2/P10a's `session wrap`** | The limits cases of §10 pass; `session.ended` carries `reason: limit` with the kind, never `lost`; the heavy-composition case passes against the real wrapper | api |
| **SB-4** | The egress proxy and forwarders: the CONNECT proxy with host groups, the Sindri-port forward, the unix-socket plumbing, the denial counters and `sandbox.denied` | `…Infrastructure/Sandbox/Egress*`, the forwarder helper if `socat` is absent | M | SB-1, SB-S1 (3) | The network cases of §10 pass on stubs; a tool that ignores the proxy variables has no network; the Sindri MCP call works through the forward | api |
| **SB-5** | Integration into `ILauncher`: the new launch flow, the probe and **Check again**, the policy files for `claude-settings-file` and `codex-overrides` from the engine's `StepPolicy`, `-014` to `-018`, `launch.started.sandbox`, `sandbox.denied`/`limit-hit` events, `sindri sandbox check\|show`, doctor lines. **Merging this is "the sandbox is ready"** | `…Features/Launch*`, `…Infrastructure/Launch/*`, `…Host/Commands/Sandbox*`, `EventCatalog.cs`, `PendingRows`, goldens (the UI tests read the events catalog: the gate runs `Aw.Sindri.Ui.Tests`) | L | SB-2, SB-3, SB-4, P10a with SL-2 and L-17 | **Proof:** `sindri-prove.sh` starts a ReadOnly and a WriteScope fixture Step under a private tmux socket with a stub CLI that tries each forbidden action; every one is refused, the allowed ones work, `launch.started.sandbox` lists the writable paths; with `bwrap` renamed away the Step parks `-014` and Check again after restoring it launches | api |
| **SB-6** | UI and status copy: the Confined line, the park cards and fix text for `-014` to `-017`, the `limit` ending, the fixture sections in `sindri-ui-fixture.sh` | `src/Sindri/Aw.Sindri.Ui/Views/Agents*`, `Views/Launch*`, `scripts/sindri-ui-fixture.sh`, `docs/csharp/sindri-ui.md` | S | SB-0 (goldens), SB-5 for the live data | View-model tests over the SB-0 goldens; the fixture shows each state | ui |
| **SB-7** | The GitHub credential: `credentials.github: agent-app`, the mint in the launcher, the token file, the shims, the 45 min refresh, the `github` group, the run-branch pre-push hook, `-018` | `…Infrastructure/Sandbox/AgentToken*`, shims under `config/`, tests with a fake `AGENT_APP_API_URL` loopback server | M | SB-5 | With the fake API: a Step pushes its run branch, not another; the token is never in `env`, `ps`, the transcript or the plan file; the refresh replaces the file; a failed mint is `-018` | ideas |
| **SB-8** | Isolated agent home (Q2 B), if the owner picks it: `sindri sandbox login`, hooks installed into the home, `CLAUDE_CONFIG_DIR`/`CODEX_HOME`, and the usage readers and collector following the directory | `…Infrastructure/Sandbox/AgentHome*`, `providers/*/install-hooks.sh` (a target-dir option), the readers | M | SB-5, the owner's answer | A Step session runs with no file of the owner's `~/.claude`; its transcript is found by the usage reader and the collector | api, ideas |
| **SB-9** | macOS: `SeatbeltSandbox` from the same plan, the keychain rule, the polling watchdog for memory and tasks, Linux-only checks marked | `…Infrastructure/Sandbox/Seatbelt*` | M | SB-5, a Mac | **(owner, on a Mac)** the SB-5 proof passes; memory `limit` fires within 5 s of the ceiling | ideas |
| **SB-10** | **(owner) The first unattended run:** engine P8's signal with the real sandbox: `workflow.step` on a scratch item launches Claude Code confined, edits and commits in its worktree, cannot read `~/.ssh` or push, reports with `complete`; a takeover keeps the same confined session and pauses the wall clock; handback resumes; the worktree is read by the engine afterward; a planted memory hog ends `step.failed(limit)` | owner's box, a scratch repo | S | SB-5, engine P8 | The owner records the run (ledger events, `sindri sandbox show`, the engine's park and failure rows) | owner |

### 12.1 Parallelization plan

```
now ──► SB-S1 (ideas) ─────────────┐
now ──► SB-0 (api) ──► SB-1 (api) ─┼──► SB-2 (api) ─┐
                                   ├──► SB-4 (api) ─┼──► SB-5 (api) ──► SB-10 (owner, with engine P8)
P10a with SL-2 ──► SB-3 (api) ─────┘                ┘        │
SB-0 goldens ──► SB-6 copy (ui) ◄──────────────── live data ─┤
                                                             └──► SB-7 (ideas) ──┐
                                                             └──► SB-9 (ideas, on a Mac)
                                                             └──► SB-8 (if Q2 = B)
engine P8 builds against RecordingSandbox and the SB-0 code-kind table from now; its unattended proof waits for SB-5
```

| Wave | API lane | Ideas lane | Engine lane | UI lane | Owner |
|---|---|---|---|---|---|
| 0 | SB-0, then SB-1 | SB-S1 | reads SB-0's kind table; P8 against `RecordingSandbox` | — | answers Q1, Q2, Q5 |
| 1 | SB-2 and SB-4 (two builders; no common files), SB-3 once SL-2's `session wrap` is on `main` | writes SB-7's tests against a fake token API | maps `-014` to `-019` | SB-6 against the goldens | the spike's "no" answers, if any |
| 2 | SB-5 | SB-7 | P8 eval cases for each code kind | SB-6 live | — |
| 3 | — | SB-9 when a Mac is free; SB-8 if Q2 = B | P8's unattended proof | — | SB-10 |

- **What runs in parallel so a team stays busy:** SB-S1 and SB-0/SB-1 start together; SB-2 and SB-4 are independent
  files and run side by side; SB-3 runs beside them as soon as `session wrap` exists; the ideas lane writes SB-7 against a
  fake while the API lane lands SB-5; the UI lane's copy needs only goldens; the engine lane never waits for the real
  sandbox until its owner proof.
- SB-2, SB-3 and SB-5 touch the shared hotspots (`SindriErrors.cs`, `EventCatalog.cs`, the command tables, the Sindri JSON
  context, `Add…()` methods, `deps.yaml`): each appends at its anchor and the second to merge rebases (API "Shared files").
  SB-5 and SB-6 change goldens the UI tests read, so their gates run `Aw.Sindri.Ui.Tests`.
- Every proof uses `scripts/sindri-prove.sh`, a private tmux socket and a guarded temp `AW_SINDRI_HOME`; none touches the owner's
  `~/.ssh`, state or agent home (a canary file under a fixture `HOME`, not the real directory, stands in for a secret).
- P8's work is not blocked: it merges with `RecordingSandbox` as the only registered `ISandbox`, which makes every unattended
  launch park `-014`; the real adapter registers in SB-5. A Step therefore never runs unconfined between P8 and SB-5, which
  is SB1 enforced by construction.

## 13. Open questions for the owner

Each has a provisional answer, used until the owner says otherwise. Written in plain words.

**Q1. When you take over a running Step, should the agent's limits come off, so you can tell it to push or read something
it was denied?**
- *Option A (recommended): no. The session keeps the exact confinement it started with, for its whole life.* It is the same
  running process; changing it means starting a new one, which ends the conversation you just joined. If you want to do
  something the agent may not, do it in your own terminal. The only thing that changes at takeover is that the time limit
  pauses until you hand back.
- *Option B: loosen on takeover and tighten on handback.* The session would have to be relaunched at each change.
- *Option C: take the session out of the sandbox for good once you take over.* Simple, but a single stray keystroke would
  end the protection, and the engine would no longer know what state the session is in.

**Q2. Which model login do Step sessions use?** The agent CLI needs your login to talk to the model; this is the one
credential of yours that has to be inside the sandbox.
- *Option A (recommended for the first unattended run): your own `~/.claude` and `~/.codex`.* Least work; your safety
  hooks and settings apply to Steps; the usage and session readers find the transcripts where they look today. The
  session can read your login file and your history.
- *Option B (build later, SB-8): a separate login kept for Steps* (`sindri sandbox login claude-code`), in a directory under
  Sindri's home. Your history and settings stay out of Step sessions. It needs a second login and the hooks installed in
  that directory.

**Q3. What can a Step reach on the network, and which tool caches can it write?**
- *Recommended:* every Step reaches only its model provider. Steps that change code also reach the package servers
  (`nuget.org`, `npmjs`) so a restore works. Nothing else: no GitHub, no other site. The NuGet and npm caches under your
  home are writable by Steps, because a restore needs them; they are shared with your own builds.
- *Stricter:* no package servers; a Step that needs a new package parks and you add it. Safer, and a restore on a changed
  lock file will stop the Step.
- *Looser:* an "allow web search" group per Step. Not recommended before a first run.

**Q4. How big may one Step session get?** Recommended defaults: memory 20% of the machine up to 12 GiB, half of the CPU
(and a lower priority than your own work), 2048 processes, the Step's own time limit plus ten minutes. With three heavy
slots that is up to 36 GiB if all three grew at once, which the heavy-job wrapper's memory guard already prevents for the
third slot. Do you want a lower ceiling (for example 8 GiB) until you have seen what Steps use?

**Q5. Should attended opens (you running `sindri open`) be confined too?** *Recommended: no, not in this spec.* You are
there, you are the owner, and the common reason to open a session is to do things the sandbox would block. A later flag
(`sandbox.attended`) could change it. Meta lanes stay unconfined for the same reason.

**Q6. GitHub access for Steps.** *Recommended: none in the first release.* Steps commit to their branch in their
worktree; Sindri's own effects push and open pull requests from the host, as the line spec already says. The 1-hour App token
path (SB-7) is built next, for Steps (the babysitter, a CI fixer) that must push themselves, and then only to their own
run branch. Say now if the first Step you want to run needs to push.

**Q7. Two things the sandbox won't stop; do you accept them?** (a) An agent can still `pkill` or `kill` other processes of
yours, because hiding them would break the heavy-job locks and the session list. (b) It can read other repos on the machine
(read-only), including their `.env` files. Both match "honest mistakes". Alternatives: hide other repos too (a list of repos
the Step may read), at the cost of Steps that look at a sibling repo failing.

**Q8. Linux first, macOS second?** *Recommended:* the first unattended run is on this Linux box (SB-10); macOS follows
(SB-9) and until then a Step on a Mac parks `-014` (`unsupported-platform`) and uses its desk twin. If a Mac must run
Steps first, SB-9 jumps ahead of SB-7 but not ahead of SB-5.

**Q9. Inside the sandbox, should Claude Code ask, deny or just run?** The sandbox already bounds what it can do, and no one
is there to answer a question.
- *Option A (recommended): "deny what isn't allowed"* (`dontAsk` with an allow list). A tool outside the list is refused
  with a message and the agent adapts. Predictable, no stalls.
- *Option B: "run everything"* (`bypassPermissions`, as your lanes do). Fewer refusals in the first runs, and the sandbox is
  the only limit. Your repo's own settings forbid this mode, so it would need an exception.

## 14. /autoplan rounds

Round 1 pending.

## 15. Spike record

Filled in by SB-S1.
