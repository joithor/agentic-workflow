# Transcript audit (Sindri rollout step 0)

Measures where human turns go across Claude Code transcripts and sets the baselines for Sindri's
success metrics (spec §2, §13 step 0).

```bash
(cd scorer && npm run build)
node scorer/dist/cli.js audit --since 60d                 # → ~/.agentic-workflow/audit/
node scorer/dist/cli.js audit --since 60d --items items.json --max-size XS
```

Outputs in `--out` (default `~/.agentic-workflow/audit/`):

| File | Content |
|---|---|
| `human-turns.jsonl` | One record per human turn (`HumanTurn`): text, active skills, guard/compaction state, whether code was edited earlier in the session (`editsBefore`), context tokens, the preceding assistant message tail |
| `summary.json` | Session/turn counts, copied turns skipped (`duplicates`), per-pattern floor counts, tokens per item, auto-start share |
| `baseline.md` | Human-readable baseline table |

`items.json` (optional) is an array of `{ id, size?, ambiguous?, authorsTrusted? }`, from any tracker export or
triage output. Missing fields count as not eligible.

Resumed and forked sessions copy earlier human turns into the new transcript with the same timestamp. The audit
keeps one set of `(timestamp, text)` keys across all files and skips repeats (reported as `duplicates`). Turns with
no timestamp are never deduped.

Patterns are deterministic floor counts, not labels, until calibrated with `--label` (below, added by the
calibration task). `image_turn` counts `[Image #N]` attachments, not defects.
