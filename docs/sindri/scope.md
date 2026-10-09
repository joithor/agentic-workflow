# Scoping with Sindri

`sindri scope` turns a brief into a **scope map**: every surface the work touches (UI, API, jobs, data, integrations, permissions, reports, notifications, mobile, flags), the implications, workstreams with dependencies and acceptance checks, and the open product questions. Each surface and implication cites a source (`R1`, `R2`, …), listed in the map's Sources table with its trust label. Spec: §7.5.

```bash
sindri scope docs/briefs/new-thing.md --out ~/notes/scopes               # a brief file
sindri scope docs/spec.md --section 13 --sources file,code --out docs/superpowers/scopes   # one section; safe inside a repo
sindri scope linear:https://linear.app/acme/project/new-thing-abc123 --out ~/notes/scopes  # a Linear project (needs sources.linear)
sindri scope linear:<project> --dry-run          # which sources would be read, with counts; calls no model
sindri scope --backtest linear:<project> [--window 1d] [--with-index]   # how well would scoping have done?
sindri scope runs [--json]                       # the latest runs: mode, status, surfaces, recall, precision, file
```

## What it reads, what it sends, where it writes

| Source | Read when | Notes |
|---|---|---|
| `file` | always (it is the brief, `R1`) | `--section N` scopes one `## N.` section of a long document |
| `linear` | the subject is a Linear project, and `linear` is selected | issues and the first 20 comments of each (later comments are not fetched); descriptions are fetched as they are today, so edits made later are visible |
| `notes` | `sources.notesDir` is set | markdown files with at least two keyword hits; generated `scope-*.md` and `backtest-*.md` maps are skipped |
| `transcripts` | `sources.transcripts.enabled: true` (off by default) | human turns from `~/.claude/projects`, **all projects**, so turns from unrelated work can reach the model |
| `code` | always | the Plan 3 index: symbols whose names match the brief, and what they call |

- `--sources file,code` (a comma-separated list) restricts the run to those sources. Anything the profile doesn't configure is never read.
- Every record is stripped of HTML comments, hidden characters, long encoded blobs and remote images or links, then scrubbed of secrets, before any model sees it. Source text is fenced as untrusted data; instructions in it are never followed.
- What goes to the provider: the brief, the matching source records (trimmed to `scope.maxPackChars`), the map as it evolves and the check reasons. The model runs with no tools. Its child process env is an allowlist: `HOME`, `PATH`, `USER`, `LANG`, `TERM`, `TMPDIR` and the `AW_*` flags (so it uses your `claude login`; an API key in your environment is not forwarded, and `*_BASE_URL` variables are dropped unless `models.allowBaseUrl: true`). Proxy and CA variables such as `HTTPS_PROXY` and `NODE_EXTRA_CA_CERTS` are not forwarded either, so behind a corporate proxy or TLS-inspecting CA the `claude` call may fail (`SND-SCOPE-002`).
- The map is written to `--out` (default: `sources.notesDir`) as `.md` and `.json`, mode 0600, in a 0700 directory. **Inside a git worktree only `--sources file,code` is allowed** (`SND-SCOPE-025`), because a map built from notes, transcripts or tracker text must not land in a public repo. `--sources file` also works inside a git repo that is not in the profile. `code` inside a worktree needs that worktree's top level to be a profile repo (its path in the profile, so a linked worktree of a profile repo is refused for `code`), and then only that repo's code is read; otherwise it is refused. When git itself errors, so the worktree check can't be answered, the guard fails closed: it counts as inside a worktree, and only `--sources file` is accepted, except that when walking up from `--out` finds a `.git` entry, the directory holding it counts as the top level, and `code` is still allowed if that directory is a profile repo. Even then, read the file before you commit it: `code:` references name repositories and paths.
- The scoping job has no read-only code tools in v1 (`--tools ""`): code context comes only from the `code` source in the pack. Acceptance checks are per workstream, not per surface, and the challenger sees the same evidence pack as the drafter.
- Every model call is recorded in the ledger (`model_calls`: run id, role, model, tokens); `sindri scope runs` lists the runs.

## How it works

1. **Gather.** The brief is `R1` and is never trimmed below half the pack budget. Keywords from it query each source.
2. **Draft.** A model (`models.scoping`) drafts the map.
3. **Check.** Deterministic rules: at least one surface, every surface cites a real source and sits in a workstream, workstream dependencies exist and don't cycle, every workstream has acceptance checks. Failures go back to the drafter together with its previous draft, for up to `scope.maxRounds` rounds.
4. **Challenge.** A different model (`models.challenger`) looks for missing surfaces until it finds none that is new (a surface whose title matches an existing one doesn't count). Additions must pass the same checks.
5. **Deliver.** The map is written to `--out`. Creating tracker issues from it is a later, approval-gated step.

A run that hits a token budget, the round cap, or a model failure is written as `incomplete`, with the reasons in the file, and exits 1. Progress lines (`gathering…`, `drafting (round 2)…`) go to stderr.

## Backtest: how to read the numbers

`--backtest` replays a past Linear project. The brief is the project's name and description. Issues filed within `--window` (default 1 day) of its creation reach the full run as as-of Linear records (those sharing the brief's keywords), and the baseline never sees them. The test set is every issue filed after the window. A third model (`models.adjudicator`) judges, twice per batch with the order reversed; only answers on which both runs agree count.

- **Recall** = later issues the map covers / later issues. Did the map anticipate the work?
- **Precision** = map surfaces that some project issue (early or later) supports / surfaces. A map that lists everything gets high recall and low precision.
- **Brief-only baseline** = the same two numbers for the same scoping with no other sources. The difference is what the sources add.
- **Pass bar** (printed in the report): recall >= 0.60, precision >= 0.60, and recall above the baseline's. `PASS`, `NOT PASSED` or `NOT MEASURED` (a number is missing because a step didn't finish; the report says which, and the run exits 1).
- Caveats, also written in every report: Linear text is fetched as it is today, so descriptions edited after the cut can leak later knowledge into the brief and recall is optimistic; the run leaves out the notes dir and (unless `--with-index`, which labels it **leaky**) the code index; later issues include bugs and follow-ups no map could predict; fewer than ten later issues is a small sample. Missed issues are listed first in the report.

Re-run the backtest after any change to the prompts, models or sources; that is the number the self-evolution loop (Plan 5) improves.

## Setup

In `profile.yaml`, then `sindri profile approve`:

```yaml
models:
  scoping: sonnet
  challenger: opus        # must differ from scoping
  adjudicator: opus       # must differ from scoping
  effort: medium          # low | medium | high | xhigh | max
scope:
  maxRounds: 3
  maxTokensPerRun: 600000
  maxTokensPerBacktest: 1500000
sources:
  notesDir: /Users/you/notes        # optional
  transcripts:
    enabled: false                  # opt-in
  linear:
    token: keychain:linear/you      # a secret pointer, never the key itself
```

**A read-only Linear key.** In Linear: Settings, Security & access, Personal API keys, New API key. Name it `sindri-read` and give it **read-only** permission. Store it in the macOS keychain with `security add-generic-password -s linear -a "$USER" -w` (paste the key when asked; the first read pops up a keychain prompt, choose Always Allow). Other pointers: `env:NAME`, `op:vault/item/field`, or `file:/abs/path` with mode 600.

## Errors

`SND-SCOPE-0xx` and `SND-SECRET-0xx` codes are listed with their fixes in [errors.md](errors.md). Common ones: `SND-SCOPE-002` (the `claude` CLI failed; the message carries its first error line, run `claude -p hello`), `SND-SCOPE-005` (the token budget is used up), `SND-SCOPE-011` (Linear's own error text is in the message), `SND-SCOPE-025` (use `--sources file,code`, or write outside the repo).
