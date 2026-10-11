# R2 estimate: the stop-rule baseline

Spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md` §10 sets the R1b exit: "the R2 estimate is
fixed here as the stop-rule baseline". This doc fixes it. It measures the packages R1b built from the template,
derives a throughput from them, and applies it to each R2 port packet. The numbers here are the ones the §10 stop
rules compare against. They change only through a re-plan with the human, never by editing this file to fit.

Spec §10's R2 row gives "6–10 weeks". That figure is calendar time and includes waiting for merges. This
baseline is agent-hours, and the stop rules compare against these agent-hours, not the weeks.

## The stop rules (spec §10, quoted)

> - A package that overruns its packet's estimate by 2× is reviewed with the human before more work goes in.
> - **R2 as a whole** overrunning 1.5× its summed estimate triggers a re-plan with the human: reorder, cut scope, or
>   run more packets in parallel. The port itself is decided and isn't reopened.

The fixed thresholds below come from the "Expected" column. They're computed from the unrounded estimates, so a
threshold can differ by 0.1 h from twice the rounded figure. Elapsed times are exact to the second:

| Packet | Estimate (expected) | Stop or re-plan at |
|---|---|---|
| Judge | 8.9 h | 17.8 h |
| Scorer | 7.3 h | 14.7 h |
| Evidence | 3.7 h | 7.4 h |
| BugFix | 2.0 h | 4.1 h |
| Sindri (one packet, six modules) | 29.5 h | 59.0 h |
| **R2 summed (excluding Redaction; Bridge retired)** | **51.5 h** | **77.2 h** (1.5×) |

**Re-plan with the owner, 2026-10-10:** the MCP bridge is retired instead of ported (it was never used: its database
held no message or task, and nothing called it), so the Bridge packet (2.5 h expected, 5.1 h review point) leaves
R2. The R2 total drops from 54.0 h (band 19.3–118.4 h, re-plan at 81.1 h) to 51.5 h (band 18.4–112.9 h, re-plan at
77.2 h), each recomputed from the unrounded figures without Bridge's lines. The per-packet estimates don't change.

**The unit is agent elapsed hours.** A packet's clock starts when its verification plan is created (the
`created` line of `verification-plan.md` in the packet's proof directory). It stops at the packet's last commit
at the time it was marked ready or blocked at the review cap. That commit is the packet's measured head, and
every measurement is pinned to it, not to the branch. The clock runs while the agent waits on the heavy-job lock,
because the baseline counted that time too. Time spent waiting for the human to merge, and the switch itself
(§10 R2), are not part of a packet and are not in these numbers.

## Re-plan 2026-10-11: build the Sindri app, stop porting

The owner re-planned R2 (readiness design §4a step 2 and §10, factory addendum §17.29, decision Q7):
- `BugFix` is done (#155: 3.9 h against 2.0 h expected, under its 4.1 h review point). `Judge` (#156) and `Evidence`
  (#158) finish against their estimates above.
- `Scorer` and `Sindri` are no longer ported, so their rows here stop applying. Sindri is built as a new C#
  application (app shell → workflow engine → context retrieval → blocks → workflows); scorer comes back as a block.
- New Sindri work is new code, not a port, so this baseline doesn't size it. Each build plan gets its own estimate,
  measured the same way (agent hours from the verification plan's `created` line), using the throughput below.

## The baseline: what R1b measured

Three packages were built from the template in R1b. Readiness (T7) and Packets (T8) are the two §10 names, and
Redaction (T1) is the one package ported during R1b. It was measured in R1b (#130) and isn't merged yet. Any
rework #130 still needs before it merges counts toward R1b, not R2. Each package is measured against its PR base.
The raw output is in the T12 proof, which lives in the owner's local state directory
(`proof/readiness-t12-r2-estimate/`), not in the repo. PR #144's Evidence section carries the cross-checks.

| | T1 Redaction (#130) | T7 Readiness (#141) | T8 Packets (#142) |
|---|---|---|---|
| Base | `main` | T6 branch (#139) | T6 branch (#139) |
| Head measured | `4cbd044` | `734c9ae` | `de98629` |
| Commits | 15 | 17 | 28 |
| Files changed | 38 | 129 | 157 |
| Lines added, all files | 1,577 | 6,256 | 6,807 |
| Generated lines (lock files, PublicAPI, golden output, AOT `expected.txt`) | 266 | 1,888 | 1,091 |
| Hand-written lines added | 1,311 | 4,368 | 5,716 |
| C# source lines (`src/**/*.cs`) | 598 | 2,120 | 2,589 |
| C# test lines (`tests/**/*.cs`) | 510 | 1,883 | 2,325 |
| Plan created → measured head (UTC) | 09:13:14 → 10:31:01 | 12:09:34 → 13:31:01 | 12:10:14 → 16:52:41 |
| Elapsed | 4,667 s (77.78 min) | 4,887 s (81.45 min) | 16,947 s (282.45 min) |
| Gaps over 30 min | none | none | one, 166.9 min (13:12 → 15:59) |
| Review runs | 5 (cap) | 5 (cap) | 5 (cap) |
| Findings per run | 12, 12, 7, 7, 5 | 20, 11, 6, 7, 2 | 18, 16, 12, 10, 12 |
| Commits after review run 1 | 10 of 15 (67%) | 9 of 17 (53%) | 13 of 28 (46%) |
| Lines added after review run 1 | 455 (29%) | 1,149 (18%) | 1,926 (28%) |

The rework rows measure the review loop. They show how much of each package changed after the first review, and
none of the three reached zero findings within five runs. T8's long gap ends at the template fix it took from T7
(`743d120` in #142, which applies #141's machine-independent AOT build). The record doesn't say what filled the
gap. It stays in the elapsed time, because a port packet can hit the same kind of wait.

PR #142's branch now ends at `dc93fc9` (17:00:38Z). The orchestrator made that one-word docs edit to
`docs/csharp/packets.md` after T8 was blocked at the cap, so it isn't part of the packet and isn't counted.

**The template's fixed cost.** `/new-package` (#139) with the small shape generates 235 lines of C# source and 81
lines of C# tests (the scaffold commits `f897d52` in #141 and `571d8ef` in #142 are identical in size). Every new
package pays this once.

## Throughput

Throughput is C# lines (source plus tests) per elapsed hour:

| Stage | C# lines | Elapsed | Lines per hour |
|---|---|---|---|
| T1 Redaction | 1,108 | 1.296 h | 855 |
| T7 Readiness | 4,003 | 1.357 h | 2,949 |
| T8 Packets | 4,914 | 4.707 h | 1,044 |
| **Pooled** | 10,025 | 7.361 h | **1,362** |

The expected estimate uses the pooled rate. The fast end of the band uses T7's rate, and the slow end uses T1's.

## From TS size to C# size

Nothing in the baseline is a like-for-like port. Readiness and Packets are new code. Redaction merged two TS
pattern lists (`judge/src/redact.ts` and `sindri/src/scrub/patterns.ts`, 128 non-blank lines in all) into a
package with its own corpus and contract tests (1,108 C# lines, 8.7×). That's a merge, not a port, and it isn't
used as a ratio. The ratio is therefore a stated assumption, with a band around it:

- **C# lines = 316 (template, once per package) + k × TS lines**, where TS lines are non-blank lines of source plus
  tests, and C# lines are added `.cs` lines as `git diff --numstat` counts them, blank lines included. k absorbs
  that difference.
- **k = 1.0 / 1.3 / 1.8 (low / expected / high).** k above 1 is expected because the template requires typed
  records instead of inline object literals, a port and a contract-tested fake for every external technology,
  `Result` mapping at each edge, and explicit registration. Many TS tests translate close to 1:1, which keeps k
  from going much higher.
- **Hours = C# lines ÷ throughput.** Low uses k = 1.0 at the fast rate, expected uses k = 1.3 at the pooled rate,
  and high uses k = 1.8 at the slow rate.

## TS size on main

Sizes are taken on `main` at `1c18a76`. Each counts non-blank lines of `.ts`, `.mts`, `.mjs`, `.js` and `.sh` files
(fixture data is excluded). The source paths are each package's `scan` entries in `deps.yaml`, which cover source
only. The test paths are this doc's own mapping to the matching `tests/` directories. Sindri's modules follow
spec §4a step 2: profile and ledger, scrub, observe, index, scope, evolve. Each sindri test file is assigned to the
module it tests. For example, `sindri/tests/heavy/claude-cli.heavy.test.ts` tests scope's model adapter, so it
counts under scope. The CLI, errors, ids, output, lock, doctor and docs code goes with profile and ledger, the first
module ported, which also builds Sindri's host.

| Packet | TS paths | TS source | TS tests |
|---|---|---|---|
| Judge | `judge/src`, `judge/tests` | 4,136 | 4,942 |
| Scorer | `scorer/src`, `scorer/tests` | 3,468 | 3,986 |
| Evidence | `skills/ui-evidence/src`, `skills/ui-evidence/scripts`, `skills/ui-evidence/tests` | 1,862 | 1,788 |
| BugFix | `skills/bugFixOrchestrator/src`, `skills/bugFixOrchestrator/tests` | 888 | 999 |
| Sindri: core, profile, ledger | `sindri/src/{profile,ledger,lock,doctor,docs}`, top-level `sindri/src/*.ts` except `secrets.ts` | 2,180 | 2,347 |
| Sindri: scrub | `sindri/src/scrub`, `sindri/src/secrets.ts` | 500 | 570 |
| Sindri: observe | `sindri/src/observe`, `sindri/src/adapters` | 615 | 742 |
| Sindri: index | `sindri/src/index` (includes shape, signals, spool) | 3,045 | 4,549 |
| Sindri: scope | `sindri/src/scope` | 1,889 | 2,516 |
| Sindri: evolve | `sindri/src/evolve` | 4,112 | 7,596 |

Cross-check: the six sindri modules sum to the whole of `sindri/src` and `sindri/tests`: 115 source files with
12,341 non-blank lines, and 99 test files with 18,320 (13,428 and 20,001 counting blank lines).

## The estimate per packet

| Packet | TS lines (source + tests) | C# lines (low / expected / high) | Hours (low / expected / high) | 2× review at |
|---|---|---|---|---|
| Redaction | measured in R1b (#130), not yet merged | 1,108 measured | 1.3 measured | — |
| Judge | 9,078 | 9,394 / 12,117 / 16,656 | 3.2 / 8.9 / 19.5 | 17.8 h |
| Scorer | 7,454 | 7,770 / 10,006 / 13,733 | 2.6 / 7.3 / 16.1 | 14.7 h |
| Evidence | 3,650 | 3,966 / 5,061 / 6,886 | 1.3 / 3.7 / 8.1 | 7.4 h |
| BugFix | 1,887 | 2,203 / 2,769 / 3,713 | 0.7 / 2.0 / 4.3 | 4.1 h |
| Sindri: core, profile, ledger | 4,527 | 4,843 / 6,201 / 8,465 | 1.6 / 4.6 / 9.9 | — |
| Sindri: scrub | 1,070 | 1,070 / 1,391 / 1,926 | 0.4 / 1.0 / 2.3 | — |
| Sindri: observe | 1,357 | 1,357 / 1,764 / 2,443 | 0.5 / 1.3 / 2.9 | — |
| Sindri: index | 7,594 | 7,594 / 9,872 / 13,669 | 2.6 / 7.2 / 16.0 | — |
| Sindri: scope | 4,405 | 4,405 / 5,726 / 7,929 | 1.5 / 4.2 / 9.3 | — |
| Sindri: evolve | 11,708 | 11,708 / 15,220 / 21,074 | 4.0 / 11.2 / 24.7 | — |
| **Sindri packet** | 30,661 | 30,977 / 40,175 / 55,506 | 10.5 / 29.5 / 64.9 | 59.0 h |
| **R2 (excluding Redaction; Bridge retired)** | 52,730 | 54,310 / 70,129 / 96,494 | 18.4 / 51.5 / 112.9 | 77.2 h (1.5×) |

Sindri is one packet with six modules (§10 R2: "by module inside its packet"), so the 2× rule applies to the
Sindri total. The module rows are for tracking progress inside it and aren't separate stop points. The template's
316 lines count once, on its first module. Every total is summed from unrounded values, and then rounded.

## Port costs the baseline didn't have

The baseline packages had no existing behavior to match. Some ports have costs that new code didn't, and they're
the likeliest reasons for a packet to land near its high end:
- **Judge:** the hook contract (only what the hooks read: the commands they call, the fields they `jq`, the exit codes
  they branch on, the files they write) is frozen until the switch, and CI runs the real hook scripts against the C#
  binary (§4a step 3, §4c). The rest of the judge is a fresh idiomatic design, not a match of the TS (§4a step 1).
- **Evidence:** Playwright for .NET and its browser tests.
- **Sindri: index:** a pinned Node sidecar for indexing TS repos and graphify behind its adapter (§4a).

A packet with one of these costs may land between expected and high. Its estimate still doesn't move.

## How a packet is measured

Use the same commands that produced the baseline. The full scripts are in the T12 proof directory (see above),
and the core of each is below.

```bash
# Size and commits of a packet's PR against its base, pinned to the measured head <sha>.
mb=$(git merge-base refs/remotes/origin/<base> <sha>)
git rev-list --count "$mb..<sha>"
git diff --numstat "$mb" <sha> | awk '
  { a=$1; f=$3; tot+=a
    if (f ~ /packages\.lock\.json$/ || f ~ /PublicAPI\.(Shipped|Unshipped)\.txt$/ || f ~ /\/Golden\// || f ~ /aot-smoke\/expected\.txt$/) g+=a
    if (f ~ /^src\/.*\.cs$/) src+=a; else if (f ~ /^tests\/.*\.cs$/) tst+=a }
  END { print "added", tot, "generated", g, "src.cs", src, "tests.cs", tst }'

# Elapsed seconds: from the verification plan's created line to the measured head's commit time.
# BSD/macOS date shown; on GNU/Linux use: date -u -d "<created>" +%s
start=$(date -u -j -f '%Y-%m-%dT%H:%M:%SZ' "$(grep -m1 '^created' <proof-dir>/verification-plan.md | cut -d' ' -f2)" +%s)
echo $(( $(git log -1 --format=%ct <sha>) - start ))
```
