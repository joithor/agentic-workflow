# schemas/line

JSON Schemas for the line's step reports (line spec `docs/superpowers/specs/2026-10-15-sindri-spec-to-proven-pr-line-design.md`, Appendix A and PR-R R9). Each schema's `description` quotes the spec text it follows; `examples/manifest.json` gives every example its verdict and its source. `node scripts/check-line-schemas.mjs` checks them.

| Schema | Fact | Report of |
|---|---|---|
| `design.json` | `fact:design` | A.1, A.2 |
| `changed-sections.json` | `fact:changedSections` | A.2 |
| `lens-report.json` | `fact:lensReport` | A.3 (a finding may carry a typed `escalate`) |
| `escalation.json` | `fact:escalation` | BS-12: the one typed way a lens loop or its fixer asks a human |
| `plan.json` | `fact:plan` | A.4 (PKT's `TaskPlan`) |
| `findings.json` | `fact:findings` | A.5, A.8 and the checks |
| `holdout-sets.json` | `fact:holdoutSets` | A.6 |
| `red-tests.json` | `fact:redTests` | A.7 |
| `built.json` | `fact:built` | A.8 |
| `holdout-result.json` | `fact:holdoutResult` | A.9, 9.3 |

Where the spec names a field list only in prose, the schema follows the prose (`holdout-sets.json` and `holdout-result.json` forbid extra fields so no scenario text can ride along). Element shapes the spec leaves open are chosen in the schema and noted in its `description`.
