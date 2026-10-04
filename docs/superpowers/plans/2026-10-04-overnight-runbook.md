# Overnight runbook: Plans A, D, C, B

An unattended orchestrator session follows this runbook to implement four plans in sequence, open one PR per plan, and leave a morning report. It never merges, never installs into Joi's live config, and never asks Joi anything. When it is blocked, it records why and moves on (or stops, where a later plan depends on the blocked one).

| Order | Plan | File | Depends on |
|---|---|---|---|
| 1 | A: judge calibration | `2026-10-04-judge-calibration.md` | — |
| 2 | D: live scorer pane | `2026-10-04-live-scorer-pane.md` | A Task 1 (`decision_details.session_id`) |
| 3 | C: prompt sorter | `2026-10-04-prompt-sorter.md` | A (`callJev`, labels, adjudicate, eval). **Overnight scope is Tasks 1–9.** Task 10 needs ≥ 7 days of shadow data after the hook is installed; list it under "Joi to do" with its date. |
| 4 | B: Jev context experiment | `2026-10-04-jev-context-experiment.md` | A; B Task 6 only on a "go" from B Task 5 |

D runs before C because it's independent UI work: if C stalls, D still ships. B runs last because its Task 5 spends the most model tokens.

## Kickoff (Joi pastes this into a fresh Claude Code session in the repo)

```
Execute docs/superpowers/plans/2026-10-04-overnight-runbook.md exactly. Use superpowers:subagent-driven-development for each plan. Do not ask me anything; follow the runbook's blocked/stop rules.
```

## Setup (orchestrator, once)

1. **Use a worktree. The main checkout has Joi's uncommitted work** (`config/hooks/prism-context.sh`, `mcp-bridge/package*.json`, `setup.sh`, `skills/prismStatus/SKILL.md`), which must not be touched, staged or stashed.
   ```bash
   git -C /Users/joi/personal/agentic-workflow fetch origin
   git -C /Users/joi/personal/agentic-workflow worktree add ../agentic-workflow-overnight -b feat/judge-calibration origin/main
   cd ../agentic-workflow-overnight
   ```
2. **Copy the plan files in** from the main checkout's `docs/superpowers/plans/2026-10-04-*.md` (they're untracked there), then commit them as the first commit on `feat/judge-calibration`:
   `docs: overnight plans A–D and runbook`.
3. **Install dependencies once per package that needs it,** one at a time, only if `node_modules` is missing: `judge`, `scorer`, `skills/bugFixOrchestrator`.
4. **Build `judge/dist` in the worktree** (`cd judge && npm run build`) before any bash test runs. The real-judge bash tests need it. Always run the worktree build as `node <worktree>/judge/dist/cli.js`. Never use the `judge` on PATH: it wraps the main checkout's old build, and never rewrite that wrapper. `config/lib/tests/aw-state-dir-isolation.test.sh` runs its own install and build: run it alone.
5. **Preflight checks.** Record each result in the morning report; none of them stops the run:
   - `judge health`, plus whether `TYPESAFE_API_KEY` or the keychain key is readable (`node -e` via `readApiKey`)
   - `claude --version`
   - `command -v claude codex cursor-agent`

## Branches and PRs

| Plan | Branch | Branched from |
|---|---|---|
| A | `feat/judge-calibration` | `origin/main` |
| D | `feat/live-scorer-pane` | `feat/judge-calibration` |
| C | `feat/prompt-sorter` | `feat/live-scorer-pane` (linear stack; C doesn't need D's code, but a linear stack avoids conflicts) |
| B | `feat/jev-context-experiment` | `feat/prompt-sorter` |

- **Push to the `fork` remote** (`joithor/agentic-workflow`); pushing to `origin` returns 403. Open each PR against `joi-fairshare/agentic-workflow` `main`:
  ```bash
  git push -u fork <branch>
  gh pr create --repo joi-fairshare/agentic-workflow --base main --head joithor:<branch> --title "<type>: <plan title>" --body-file <file>
  ```
  The `block-push-main` hook pattern-matches the word `main` anywhere in a `git push` command line, so never put `git push` and `gh pr create … --base main` in the same Bash call.
- Each PR body:
  - starts with **"Stacked: merge after #<previous PR>"** (except A)
  - lists the plan's tasks with ✅ / ⚠️ blocked
  - includes the eval tables the plan asks for
  - ends with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`
- Open the PR when a plan's last executable task is committed. **Never merge.**

## Execution rules

- **Subagent-driven, strictly serial.** One implementer subagent per task, then one fresh reviewer subagent per task (spec compliance + code quality, against that task's text and the plan's Global Constraints), then the next task. Never two implementers at once.
- **One heavy job at a time** (Joi's crash rule). Heavy means `npm install`, `npm test`, `npm run test:coverage`, `tsc`/`typecheck`, builds, and `judge eval` / `judge adjudicate` runs. The implementer runs the full suite once per commit. The reviewer re-runs it only if the implementer's output doesn't show it, and only after the implementer has finished.
- **Task briefs** given to implementers carry: the task text verbatim, the plan's Global Constraints, the Interfaces of earlier tasks they consume, and the worktree path. Goal / acceptance criteria / proof command are filled from the task so `scope-gate` passes.
- **Never install into Joi's live environment.** Steps that would run `scripts/install-*.sh`, `setup.sh` (non-dry-run), or otherwise write `~/.claude/settings.json`, `~/.claude/hooks/`, `~/.local/bin` or launchd are **deferred**. Run their bash tests (which use temp HOMEs) and `./setup.sh --dry-run`, and list the real install under "Joi to do in the morning". This also covers end-to-end steps that need an installed hook (B Task 6 Step 6, D's real-session proof if it needs an install).
- **Plan D Task 9 "Proof E"** starts a real interactive Claude session with its working directory set to the main checkout (an already-trusted folder). It is read-only there and edits nothing. That is the one allowed use of the main checkout. If it fails or hangs past 5 minutes, use the plan's fallback (mount tests) and mark Proof E ⚠️.
- **Model-calling evals are allowed** (Jev via `TYPESAFE_API_KEY`, Opus via the logged-in `claude`), with these caps:
  - `judge adjudicate … --limit 60` per question (`--limit 120` only for B's turn-progress, as that plan specifies)
  - at most 2 eval rounds per question per plan
  - `AW_STATE_DIR` is **not** overridden for evals: they need the real `decisions.sqlite` history. Evals never write `decisions` (Plan A Task 4 guarantees that).
- **Commit trailer** on every commit:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC
  ```

## Blocked / stop rules

- **Task fails review or tests:** the same implementer gets up to 2 fix rounds. Still failing → mark the task ⚠️ blocked with the failing output, commit nothing broken (set the broken work aside with `git stash push -u -m "blocked: <plan> task <n>"` **in the worktree only**; `reset --hard`/`clean` are blocked by Joi's `block-destructive` hook, and stashing keeps the work for the morning), and apply:
  - **A blocked at Task 1:** stop the whole run (D, C and B all depend on it).
  - **A blocked at a later task:** finish the A tasks that don't depend on it. Continue to D. Run C and B only if the tasks they consume (named in each plan's Interfaces) are done.
  - **D / C blocked:** open the PR with what's done; continue to the next plan.
  - **B blocked before Task 5:** open the PR; stop.
- **Plan text is wrong** (a path or signature that doesn't exist and isn't created earlier): the implementer makes the smallest change consistent with the plan's intent, and the PR lists the deviation under "Plan deviations". Never change a plan's decision rules, thresholds or Global Constraints.
- **External dependency down** (Jev returns `no-api-key` / `http-5xx`, or `claude` isn't logged in): skip only the eval/adjudicate steps, mark them ⚠️ with the error, and continue. Code tasks don't depend on them.
- **Context:** the orchestrator writes `~/.agentic-workflow/digests/overnight-2026-10-04.md` after **every** task, not only when context grows. A crash or compaction resumes from it: re-read the runbook and the digest, check `git log` and `git status` in the worktree, and re-dispatch only unfinished work, one at a time.

## Morning report

Keep `~/.agentic-workflow/digests/overnight-2026-10-04.md` current. When done, it holds:

1. **PRs opened**, with links and merge order.
2. **Per plan:** tasks ✅ / ⚠️ with one line each, and any plan deviations.
3. **Eval results:**
   - Plan A baseline and before/after tables
   - ask-check outcome/adjudicator agreement
   - Plan C per-axis results and which scaffolds the decision rule enabled
   - Plan B results doc verdict (go / no-go / inconclusive) and whether Task 6 was built
4. **Joi to do in the morning:**
   - review and merge the PRs in order
   - then run the deferred installs: `scripts/install-judge.sh --provider claude` and the D install command named in its plan
   - then restart sessions so the new hooks and mod load
5. **Token spend:** Opus adjudication calls and Jev calls, counted from the eval/adjudicate outputs.

Finally, remove the worktree only if every PR is open and its branch is pushed:
`git worktree remove ../agentic-workflow-overnight`
