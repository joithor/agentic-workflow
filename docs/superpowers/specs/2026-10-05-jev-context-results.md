# Jev context experiment results (Task 5)

Generated 2026-10-05T03:12:10.981Z from t5-eval-*.json.

## Decision rule (verbatim from task-5-brief.md)

> **Decision rule (fixed now, before any data):**
> - **Label sources:** every number is reported twice, once against `outcome` labels (with `--collapse progressing`) and once against `adjudicator` labels. A variant wins only if it wins on **both**. If outcome and adjudicator labels agree on fewer than 70% of shared items (with the adjudicator's labels collapsed the same way), the verdict is "inconclusive": no Task 6, and the doc records the disagreement.
> - **turn-progress (go/no-go for Task 6):** `full` or `problem+diff` must reach **decisive rate ≥ 60% and accuracy-on-decisive ≥ 80%** on at least 60 labeled turns per label source. It must also beat `diff-only` by **≥ 10 points** of decisive rate or accuracy-on-decisive. Otherwise, no-go: the per-turn judge isn't built, and the doc records why.
> - **resolution-check (separate):** if `full` beats `stat` by ≥ 10 points of decisive rate with accuracy-on-decisive not lower, keep Task 1's diff (already shipped). Otherwise open a follow-up to drop the `diff` field again, since it costs tokens for nothing.

## turn-progress vs outcome labels (--collapse progressing, provider jev)

| variant | n | decisive rate | accuracy | accuracy-on-decisive | p50 latency | mean input tokens |
|---|---|---|---|---|---|---|
| diff-only | 420 | 31.4% | 68.6% | 81.8% | 179 ms | n/a |
| problem+diff | 420 | 51.7% | 73.8% | 82.0% | 187 ms | n/a |
| full | 420 | 46.2% | 73.1% | 83.0% | 186 ms | n/a |

## turn-progress vs adjudicator labels (--collapse progressing, provider jev)

| variant | n | decisive rate | accuracy | accuracy-on-decisive | p50 latency | mean input tokens |
|---|---|---|---|---|---|---|
| diff-only | 120 | 34.2% | 82.5% | 92.7% | 182 ms | n/a |
| problem+diff | 120 | 64.2% | 89.2% | 96.1% | 178 ms | n/a |
| full | 120 | 56.7% | 90.0% | 94.1% | 177 ms | n/a |

## Reference: claude-cli, variant full, labels any

| variant | n | decisive rate | accuracy | accuracy-on-decisive | p50 latency | mean input tokens |
|---|---|---|---|---|---|---|
| full (claude-cli) | 428 | 97.9% | 72.0% | 73.5% | 6849 ms | n/a |

claude-cli per source: outcome 75.2% acc-on-decisive (n=112); adjudicator 87.2% (n=112).

## Label-source agreement

Outcome vs adjudicator (collapsed): 93/112 = 83.0%

## Majority-class baseline (information only)

- outcome: progressing 86.2% of 420 (collapsed)
- adjudicator: progressing 92.5% of 120 (collapsed)

## resolution-check

deferred: no stored inputs yet

## Verdict: no-go

- full: outcome: decisive 46.2% < 60%; adjudicator: decisive 56.7% < 60%
- problem+diff: outcome: decisive 51.7% < 60%

Task 6 is not built.

## Notes (not part of the rule)

- More context does help Jev: `problem+diff` raises the decisive rate by about 20–30 points over `diff-only` on both label sources, with accuracy-on-decisive flat or higher. The fixed bar (decisive ≥ 60% on **both** sources) is missed only on outcome labels (51.7%).
- `full` (adding prior stat and signals) is slightly *less* decisive than `problem+diff`; the extra fields look like noise for Jev.
- Labels are heavily imbalanced: progressing is 86% (outcome) and 92% (adjudicator) of items. On outcome labels Jev's overall accuracy (73–74%) is below the majority-class baseline, so a decisive-only gate is the only way this judge could add value.
- claude-cli (`full`) is almost always decisive (98%) but only 73.5% accurate on decisive answers, at about 6.8 s p50 versus about 180 ms for Jev.
- Data: 437 turns imported from 14 days of transcripts, 420 outcome-labeled, 120 adjudicated by Opus (2-of-3, 0 splits, 360 Opus calls). Each Jev variant ran once over each label source; the claude-cli reference ran once.
