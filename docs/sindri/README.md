# Sindri

Sindri is a local, always-on harness that picks up work, sizes it, starts agent sessions with cited context, keeps them on track and ships them through a fixed recipe. It asks the human only when it has to. Design: `docs/superpowers/specs/2026-10-07-sindri-design.md`.

This package is being built in plans. What exists today (Plan 2):

| Command | What it does |
|---|---|
| `sindri profile init [--ring0] [--dir DIR]` | Scaffold a profile in `mode: shadow`. `--ring0` makes one for the current repo, using its plan files as the backlog |
| `sindri profile validate \| explain <key> \| migrate \| approve [hash]` | Check the profile, show where a value comes from, upgrade it, approve a change. Changes take effect only once approved, and approving needs you at a terminal |
| `sindri observe [--no-record]` | List open work with sizes and what auto-small would start. Records it in the ledger when the profile is approved and this is the active host |
| `sindri ledger [--item ID] [--since 7d]` | Show recorded events |
| `sindri scrub [--staged] [--install-pre-commit]` | Redact secrets from stdin, check staged changes, or install the pre-commit hook that refuses secret-shaped strings |
| `sindri doctor` | One line per health check, `ok` / `warn` / `fail`, each with a fix |

Every read command takes `--json`. Exit codes: `0` ok, `1` attention needed, `2` error. Errors carry a stable code (`SND-<AREA>-<NNN>`); see `errors.md`.

## Quick start (this repo, ring 0)

```bash
scripts/install-sindri.sh                 # or ./setup.sh --with-sindri
sindri profile init --ring0 --plans '*-sindri-plan-*'   # profile for this repo; backlog = the Sindri plans
sindri profile validate
sindri profile approve                    # shows the hash and what changed
sindri profile approve <hash>             # you, at a terminal: type the first 6 characters to confirm
sindri scrub --install-pre-commit         # refuse secret-shaped strings in commits
sindri doctor                             # expect every line ok
sindri observe                            # the remaining Sindri plan tasks, in order
```

## Where things live

| Path | What |
|---|---|
| `$AW_STATE_DIR/sindri/ledger.db` | The ledger (SQLite, 0600). Schema in `planning/ERD.md` |
| `$AW_STATE_DIR/sindri/sindri.lock/` | The singleton lock (spec §9.1) |
| `$AW_STATE_DIR/sindri/profile-approved/<hash>/` | Snapshots of approved profiles, for diffs |
| `$AW_STATE_DIR/profile` | The profile, or a link to your private profile repo (`--profile` and `AW_PROFILE_DIR` override it) |

`$AW_STATE_DIR` defaults to `~/.agentic-workflow`.

Not to be confused with `./setup.sh --profile <web-app|ios|personal>`, which applies a Claude Code settings profile to a repo. A Sindri profile is the `profile.yaml` described in `profile.md`.

## Plan files as a backlog

The `plan-file` tracker reads each `### Task N: …` heading as one item and its `- [ ]` / `- [x]` steps as progress. A task is done when every step is ticked. Code inside fenced blocks is ignored, so keep shell snippets fenced: an unfenced `# comment` line would end the task early.

## When to use which

| You want to… | Use |
|---|---|
| Fix one bug ticket, by hand, now | `/bugFixOrchestrator <ticket>` |
| Turn an approved spec into proven PRs, by hand | `/specToProvenPR` |
| Review a plan before building it | `/autoplan` |
| See the backlog and what Sindri would start | `sindri observe` |
| Have work picked up, run and shipped for you | `sindri start` / auto-start (rollout step 3a and later; not built yet) |
