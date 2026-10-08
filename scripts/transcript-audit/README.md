# Transcript audit (Sindri rollout step 0)

Measures where human turns go across Claude Code transcripts and sets the baselines for Sindri's
success metrics (spec §2, §13 step 0).

```bash
(cd scorer && npm run build)
node scorer/dist/cli.js audit --since 60d                 # → ~/.agentic-workflow/audit/
node scorer/dist/cli.js audit --since 60d --items items.json --max-size XS
```

`--since` defaults to `1d`; pass `60d` (or an ISO date) for a baseline window.

Outputs in `--out` (default `~/.agentic-workflow/audit/`):

| File | Content |
|---|---|
| `human-turns.jsonl` | One record per human turn (`HumanTurn`): text, active skills, guard/compaction state, whether code was edited earlier in the session (`editsBefore`), context tokens, the preceding assistant message tail |
| `summary.json` | Session/turn counts, copied turns skipped (`duplicates`), per-pattern floor counts, fresh and cache-read tokens per item, auto-start share |
| `baseline.md` | Human-readable baseline table |

`items.json` (optional) is an array of `{ id, size?, ambiguous?, authorsTrusted? }`, from any tracker export or
triage output. Missing fields count as not eligible.

Resumed and forked sessions copy earlier human turns into the new transcript with the same timestamp. The audit
keeps one set of `(timestamp, text)` keys across all files and skips repeats (reported as `duplicates`). Turns with
no timestamp are never deduped.

Tokens per item split each session's usage evenly across the items it mentions. The primary figure is fresh
tokens (input + cache writes + output); cache-read tokens are reported separately because they grow with session
length. Both are a proxy for subscription quota, not the quota itself.

Patterns are deterministic floor counts, not labels, until calibrated with `--label` (below, added by the
calibration task). `image_turn` counts `[Image #N]` attachments, not defects.

## Calibrating the patterns (`--label`)

The regex patterns are floor counts until they are checked against labels from a model adjudicator. On a second
user's transcripts a correction pattern caught 5 of 45 wrong-approach corrections, and 22 of 80 image turns were
real defects. So no metric may use a pattern until calibration marks it `metric-grade`.

```bash
node scorer/dist/cli.js audit --since 60d --label 400                 # 20 labeler calls + 3 repeat calls
node scorer/dist/cli.js audit --since 60d --label 400 --label-repeat 50 --label-model sonnet
```

- **Sample.** `--label n` takes n deduped typed turns, ordered by sha256 of `session:index` (uniform and reproducible).
  `--label 0` (the default) skips labeling, so the plain audit stays offline and free.
- **Labeler.** `claude -p --safe-mode` with no tools, no MCP servers and no session file, in batches of 20 turns.
  `--safe-mode` means no hooks, CLAUDE.md, MCP servers or plugins see the turn text. The child gets only PATH, HOME, USER
  and the Claude auth variables; proxy and custom-CA variables (`HTTPS_PROXY`, `NODE_EXTRA_CA_CERTS` and the like) are
  not passed, so behind a proxy or custom CA the calls fail. Turn text is fenced as untrusted data (tag spellings with
  spaces, zero-width characters, fullwidth brackets and html entities are neutralized, and the rule is repeated after
  the last fence), the output is schema-validated, and a bad batch is retried once, then counted in `labelErrors`. If
  the first two batches both fail (missing CLI, not logged in), the run aborts with that error. Calibration tests the
  patterns against the same first 1,500 characters the labeler saw.
  Labels: `wrong_approach_design`, `wrong_approach_process`, `defect_report`, `restate`, `rigor`, `scope_surface`,
  `ship_recipe`, `handoff`, `none` (exclusive).
- **Repeat.** `--label-repeat k` (default 50) relabels the first k sampled turns with batches in reverse order and reports
  raw agreement per label.
- **Calibration.** Each pattern is compared with its label (`restate`, `rigor`, `scope_surface`, `ship_recipe`, `handoff`,
  and `image_turn` against `defect_report`): TP, FP, FN, precision and recall with Wilson 95% lower bounds. `metric-grade`
  needs both lower bounds at least 0.6 and at least 10 positives; anything else is `floor only`.
- **Wrong-approach corrections.** Design and process counts in the sample, their share of turns with a Wilson interval,
  scaled to all deduped turns, restricted to turns after code was written (`editsBefore`), and per 30 days. The spec's
  step-0 decision rule reads the design number: under 8 per 30 days after code means Approach and Drift direction checks
  are not built in 3a (not even in shadow).

Extra outputs: `labels.jsonl` (turn key, labels, model, pass) and `calibration.json`; `baseline.md` gains the
"Pattern calibration" and "Wrong-approach corrections" sections. Without `--label`, `baseline.md` says
"Patterns are uncalibrated floor counts; run with --label 400 to calibrate."

**Privacy.** Labeling sends the text of the sampled turns, and the tail of the preceding assistant message, to the
model provider Claude Code already uses (your local `claude` login). The same notice is in `scorer audit --help`.
All outputs stay under `~/.agentic-workflow/audit`. Never commit `labels.jsonl` or `human-turns.jsonl`; PR comments
carry aggregate numbers only.

**Cost.** n = 400 is 20 labeler calls (400 / 20) plus 3 repeat calls (50 / 20, rounded up), 23 in all (up to 2x with retries).
