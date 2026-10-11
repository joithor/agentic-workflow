# BugFix — `bugfix-state`

`bugfix-state` is the state helper the `/bugFixOrchestrator` skill drives a bug fix through. It owns the state file and
refuses every step the rules don't allow, so the skill can't skip a gate or edit evidence. It is the C# port of
`skills/bugFixOrchestrator/src` (spec §4a step 1): a reimplementation, not a translation. The TypeScript helper stays
installed until the switch, and nothing in `setup.sh` points at this build.

Source: `src/BugFix` (full shape: `Core`, `Features`, `Infrastructure`, `Host`; NativeAOT binary `bugfix-state`).
Tests: `tests/Aw.BugFix.Tests` (slices, rules, contract bindings, the CLI end to end) and `tests/Aw.BugFix.Testing`
(fakes and contract bases). AOT smoke: `bash src/BugFix/aot-smoke/run.sh`.

## What the skill relies on

The skill text is this CLI's only caller. It keeps working through these points; everything else is this package's own
design.

- Every command takes `--state <dir>`. After the switch the skill passes `bugfix/<repo-slug>/<ticket-slug>` (spec §4c).
- The commands and flags below.
- Exit 0 on success, 3 on a refusal. A refusal prints one stderr line starting with `refused:`, and the skill reports
  the reason.
- `run-test` and `run-ui` print `evidence`, the run record path the skill passes back to `advance reproduce` and
  `record-run`.
- `resume` prints `next`; `status` prints the state with `baseline.commit`.
- `judge` prints `decision`, `reasons` and `status` (`needs-human` after an escalation).

## Commands

| Command | Phase | Does |
|---|---|---|
| `init --state <dir> --ticket <ticket.json>` | (none, or finished) | Reads the ticket (`source` linear or text, `id`, `title`, `brief`, `expected`, `actual`; brief and expected not blank) and starts at `intake` |
| `advance investigate --evidence <handoff.md>` | intake → investigate | Snapshots the `/rootCause` handoff: status `diagnosed`, at least one `confirmed` hypothesis, a `## Root Cause` section |
| `run-test --check <file> [--cwd <dir>] -- <command…>` | any | Runs the test command on a clean tree and registers a run record (with the commit and the working directory) |
| `run-ui --check <script.json> [--cwd <dir>]` | any | Runs the ui-evidence script on a clean tree and registers a run record (blocked: see Ports) |
| `advance reproduce --evidence <run> --check <file> [--cwd <dir>]` | investigate → reproduce | Freezes the check and the failing baseline |
| `start-attempt --mode <A\|B\|C>` | reproduce, evaluate → fix | Opens the next attempt (at most 3) |
| `record-candidate --branch <b> [--cwd <dir>] [--hypothesis <n>] [--allow-test-changes]` | fix, evaluate | Records the worktree's commit as the next candidate (`c1`, `c2`, …) |
| `record-run <candidate> --evidence <run>` | fix, evaluate → evaluate | Records the frozen check's run against the candidate |
| `judge <candidate>` | evaluate | Asks judge resolution-check once and records the verdict (blocked: see Ports) |
| `advance report (--candidate <id> \| --unresolved)` | → report | Finishes resolved (only with a passing run and a `resolved` verdict) or unresolved |
| `status` | any | Prints the whole state |
| `resume` | any | Prints `phase`, `status`, `attempt` and the `next` action |

`--state`, `--ticket`, `--evidence` and `--cwd` resolve against the process's working directory; `--check` resolves
against `--cwd` (the checkout the check lives in). The argv after `--` is kept as given.

## The rules

- **Evidence is ours.** `advance reproduce` and `record-run` accept only run records that `run-test` or `run-ui` wrote for
  this bug fix and that are unchanged since (path and SHA-256 registered in the state).
- **The check reproduces the bug.** The baseline run must fail at the checkout's HEAD, on the committed check file. A
  passing check can never prove a fix, and a **broken** run is not a reproduction: a test command that couldn't start or
  was killed at the time limit, or a ui run with broken steps and no failed one (a bad selector).
- **The check is frozen.** Every later run must execute the same check file (same SHA-256); a test run must use the
  same argv **in the same working directory** (relative to the repository root) as the baseline; its program (argv[0],
  resolved once against the run's directory or PATH and started as exactly that file) must be the same file (same
  SHA-256), and it must run in the same environment (the hash of the allowed variables below). `run-test` refuses a command that doesn't name the check by a
  path that resolves to it from the working directory (a same-named file elsewhere doesn't count; paths resolve like
  realpath(3), links before `..`), a non-runner (`grep`, `cat`, `true`, …), a program that runs another one (`env`,
  `xargs`, `timeout`, `nice`, `nohup`, `sudo`, …), and inline code, by interpreter: shells and python `-c`, node/deno/bun
  `-e`/`-p`/`--eval`/`--print`, perl and ruby `-e`/`-E`, php `-r`, `deno eval` (anywhere in a short-flag cluster, so
  `bash -xec` counts and `bash -e` doesn't; ruby and perl value flags such as `-Itest` don't). A package runner (`npx`,
  `npm exec`, `pnpm exec|dlx`, `yarn exec|dlx|node`, `bun x`, `uv run`, `poetry run`, `bundle exec`) is looked through,
  past its options and their values: the program it runs is classified the same way.
- **The caller's environment can't steer it.** The check runs with only an allowlist of variables from bugfix-state's
  own environment (`PATH`, `HOME`, `USER`, `LOGNAME`, `SHELL`, `TMPDIR`, `TERM`, `CI`, `LANG`, `LANGUAGE`, `LC_*`,
  `TZ`); everything else (`BASH_ENV`, `NODE_OPTIONS`, exported shell functions, `npm_config_*`, `PYTEST_ADDOPTS`, …)
  is dropped, and the allowed values are frozen by hash at the baseline, so a later run with another PATH or HOME is
  refused. Git runs without `GIT_*` variables, and bugfix-state refuses to change state while
  `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` is set. Residual risk: what lives outside the repository isn't frozen beyond
  that: installed tools other than the check's own program, files under `HOME` (`~/.npmrc`, shell startup files), and
  dependencies in gitignored directories (`node_modules`, `.venv`).
- **Candidates are real changes on the baseline:** a clean worktree on the named branch, with commits on top of the
  baseline commit and a tree different from it, not already recorded. A fix that touches the check, its directory,
  tests, fixtures, snapshots, runner config, `package.json`, or a repository file the frozen command names (its runner
  or wrapper script, a `--require=`/`--config=` value, a `-r./x` value, a `-m`/`-p` module's file, and the file a
  loader would open for a specifier without an extension, e.g. `./lib/setup` → `lib/setup.js`), or a runner file the
  command reads without naming it (`Makefile`, `*.mk`, `justfile`, `Rakefile`, `Taskfile.yml`, `noxfile.py`, `.npmrc`,
  `.yarnrc*`, `bunfig.toml`, `deno.json`, `pom.xml`, `build.gradle`, `Cargo.toml`, `Gemfile`, `go.mod`,
  `Directory.Build.*`, …) needs `--allow-test-changes` (the user's approval). A baseline command that names a path
  (a script or config by extension, or anything written with `/` or a leading `.`) that doesn't exist as a file or
  directory is refused (AW-BUGFIX-008): a fix could add it. Globs and package patterns (`./...`) aren't paths.
- **Attempts.** Mode A one candidate, C one candidate (split by area, in sequence), B two candidates on different open
  hypotheses, only after a failed attempt and with at least two hypotheses not ruled out. A new attempt waits until
  every recorded candidate of the current one is evaluated and none resolves the ticket. While a mode-B attempt has
  one candidate, `resume` points at the second (and says to start the next attempt if its implementer produced
  nothing usable).
- **One verdict per candidate.** `judge` builds the input from state (the ticket verbatim, the snapshotted root cause, a
  description of the frozen check, the diff stat and the diff without lock files (`package-lock.json`, `*.lock`,
  `pnpm-lock.yaml`, `packages.lock.json`) and `dist/` output, capped at 200,000 characters), writes it to
  `judge-<candidate>.json`, marks the candidate as asked, asks judge about that file without holding the lock, and
  records the answer only when it names that file's SHA-256 and the bug fix is still in `evaluate`. It never asks
  again: a call interrupted after asking leaves the mark, the next `judge` is refused, and `resume` says to ask the
  user, who may start the next attempt (the candidate counts as evaluated, not resolving) or report unresolved. If judge gives
  no usable verdict (or one about another input) the mark is cleared and nothing is recorded, so a retry is not a
  re-roll; the input file stays as a record of what was asked. An escalation sets the status to `needs-human`; a
  later `resolved` verdict in the same attempt sets it back to `active`, and `resume` points at the resolving candidate.
- **One writer at a time.** Every state change holds the state directory's lock (`state.lock`) from load to save; a
  long `run-test`, `run-ui` or `judge` takes it only to record its result onto the current state. A call that can't get
  it within `AW_BUGFIX_LOCK_WAIT_SECONDS` (default 30) is refused with AW-BUGFIX-021 and changes nothing. Only `init`
  creates the state directory; any other command on a missing one is AW-BUGFIX-002 and creates nothing.

## Output, exit codes and errors

Success prints one JSON document (the Kernel `AwJson` profile: camelCase, enums as their names) on stdout, exit 0.
Everything else is one line on stderr ending with the catalogue code, and nothing on stdout.

| Exit | Line | When |
|---|---|---|
| 0 | (JSON on stdout) | Success |
| 1 | `error: <message> (AW-BUGFIX-NNN)` | Invalid input or not found (`ErrorKind.Invalid`, `NotFound`) |
| 2 | `usage: <problem>` then the usage | Unknown command or option, a missing or empty option value, malformed `--mode`, `--hypothesis` or candidate id |
| 3 | `refused: <reason> (AW-BUGFIX-NNN)` | A rule refuses the step; judge or ui-evidence is unavailable; the state is busy; a file can't be written or read |

| Code | Kind | Meaning |
|---|---|---|
| AW-BUGFIX-001 | invalid | An argument or input file failed validation (a bad ticket, both or neither of `--candidate`/`--unresolved`) |
| AW-BUGFIX-002 | notFound | No state in the state directory: run `init` first |
| AW-BUGFIX-003 | invalid | The state file isn't a BugFix state of this schema |
| AW-BUGFIX-004 | invalid | An input file (ticket, handoff) can't be read |
| AW-BUGFIX-005 | conflict | The step isn't allowed in this phase, or an unfinished bug fix exists at `init` |
| AW-BUGFIX-006 | conflict | The handoff doesn't support a fix |
| AW-BUGFIX-007 | conflict | The baseline run doesn't reproduce the bug |
| AW-BUGFIX-008 | conflict | The check isn't committed, is outside the repository or changed since the baseline |
| AW-BUGFIX-009 | conflict | The evidence isn't a registered, unchanged run record |
| AW-BUGFIX-010 | conflict | No new attempt now |
| AW-BUGFIX-011 | conflict | The candidate can't be recorded |
| AW-BUGFIX-012 | conflict | The test command could pass without running the check |
| AW-BUGFIX-013 | notFound | Unknown candidate |
| AW-BUGFIX-014 | conflict | The run can't be recorded for the candidate |
| AW-BUGFIX-015 | conflict | The candidate can't be judged (no passing run, already judged) |
| AW-BUGFIX-016 | unavailable | judge gave no usable verdict; nothing recorded |
| AW-BUGFIX-017 | unavailable | ui-evidence produced no usable run; nothing registered |
| AW-BUGFIX-018 | invalid | A git command failed (not a repository, unknown revision) |
| AW-BUGFIX-019 | conflict | The report step isn't allowed (already reported, candidate not eligible) |
| AW-BUGFIX-020 | conflict | A working tree has uncommitted or untracked changes |
| AW-BUGFIX-021 | conflict | Another bugfix-state call holds the state directory; retry when it finishes |
| AW-BUGFIX-022 | unavailable | A state, run or log file can't be written or read |

## Files

Everything lives under the `--state` directory; the C# build never reads TS-written state (spec §4c).

- `state.json`: the state (`schemaVersion` 1), written to a temporary file and renamed, under `state.lock`. Fields: `ticket`, `phase`,
  `status`, `attempt`, `mode`, `investigation` (`handoff`, `rootCause`, `hypotheses`), `check` (`kind` test or ui,
  `path`, `sha256`, `command`, `directory`, `programSha256`, `commandFiles`), `baseline` (`evidence`, `commit`), `candidates` (`id`, `attempt`, `mode`, `branch`,
  `worktree`, `commit`, `hypothesis`, `changedFiles`, `run`, `verdict`, `judgeAsked`), `runs` (`evidence`, `sha256`), `resolvedBy`,
  `history`.
- `runs/<stamp>-<test|ui>-<n>.json`: a run record (`kind`, `outcome` passed, failed or broken, `commit`, `directory`,
  `checkSha256`, `command`, `programSha256`, `exitCode`, `output`); a ui run that produced nothing gives its slot back, with its `.log` (and, for ui, the ui-evidence run directory `<stem>/`).
- `judge-<candidate>.json`: the resolution-check input asked about the candidate.

## Ports and adapters

| Port (Core) | Fake (`Aw.BugFix.Testing`) | Adapter (`Infrastructure`) | Contract base |
|---|---|---|---|
| `IStateStore` (load, save, exclusive lock) | `InMemoryStateStore` | `FileStateStore` (`state.lock`, `FileShare.None`) | `StateStoreContract` |
| `IRunLedger` | `InMemoryRunLedger` | `FileRunLedger` | `RunLedgerContract` |
| `IGit`, `IFiles` | `InMemoryRepos` (one fake: git answers about the files the file port reads) | `GitProcess` (git on PATH), `LocalFiles` | `ReposContract` |
| `ICheckRunner` (returns the exit code and whether it ran to a verdict) | `ScriptedCheckRunner` | `ProcessCheckRunner`: output streamed to the log (16 MB cap); killed after `AW_BUGFIX_CHECK_TIMEOUT_SECONDS` (default 1800); a run that exits keeps its exit code even if background children still hold its output (output after the exit is dropped after 5 s, noted in the log; those children aren't killed: they left the process tree); a run killed at the limit or that couldn't start didn't run to a verdict (broken); runs without loader variables | `CheckRunnerContract` |
| `IJudge` (asks about the written input file; the answer names its SHA-256) | `ScriptedJudge` | `UnavailableJudge` (**blocked**) | `JudgeContract` |
| `IUiEvidence` | `ScriptedUiEvidence` | `UnavailableUiEvidence` (**blocked**) | `UiEvidenceContract` |

**Blocked adapters.** judge and ui-evidence are reached through their CLIs (`deps.yaml` cli edges), and both are being
ported to C# too, so `bugfix-state` follows their C# contracts, not the TypeScript ones. Neither contract is published
yet (`docs/csharp/judge.md` comes with the Judge packet; Evidence isn't ported). Until then Composition registers
adapters that answer AW-BUGFIX-016 and AW-BUGFIX-017: `judge` and `run-ui` refuse with exit 3 and record nothing, and
everything else works. The adapter that replaces each one is a new `Infrastructure` class bound to its contract base.
For judge the port already carries the binding: the adapter gets the written input file and its SHA-256 and must
return the SHA-256 of the input judge stored the decision for (the helper refuses any other), so an agent can't forge or
re-roll a verdict.
