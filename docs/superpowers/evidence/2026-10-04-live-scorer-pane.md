# Evidence: live scorer pane (Plan D)

- Claude Code: `claude --version` = `2.1.289 (Claude Code)`. Date: 2026-10-04.
- Proof A (validate + test): pass. `claude plugin validate mods` and `claude plugin validate mods/aw-live` both print `Validation passed`, with no symlink warning. `claude plugin test mods/aw-live` = 28 pass, 0 fail.
- Proof B (latency of `scorer live`, 20,000-line synthetic transcript, `bench.mjs` from the brief): cold 405 ms (`calls=20000 newLines=20000`), warm median 69 ms (max 75 ms) over 15 runs each ingesting one line. Target < 200 ms warm: met.
- Proof C (`/live status` through `claude -p --resume --plugin-dir`, one haiku turn): output below. `calls` (3) and `tokens` (125k in, 1k out; window 44.1k of 200k, 22%) matched the direct `scorer live` output: yes. The judge card reads `not linked yet` because the installed judge predates Plan A (expected).
- Proof D (judge scoping, three seeded decisions for the session): output below. Rows from other sessions excluded: yes. I then seeded two more rows (one for another session id, one with a null session id); the `judge` and `scope-gate` rows did not change (`calls 3 ...`, `scope-gate 1 · ready 1 · p50 400 ms`).
- Proof E (pane drawn in a real interactive session, pty, cwd = main checkout, read-only): obtained. Harness: `captured 18017 chars; missing: none; window meter shown: True`, `exit=0`. The harness asserts the literal strings `Toggle the live scorer pane`, `cost`, `calls`, `over 200k`, `queued now`, `context guard`, and a `window NN%` + bar-glyph match. I read the capture and saw the lines quoted below.
- Findings: hook latency is not observable from a mod in this build (classic hooks did not fire under `claude -p --plugin-dir`; `tool.call` times tool and hooks together). Shown instead: per-gate p50 from `decisions.latency_ms`. The override-label hotkeys were dropped by ruling, so nothing here expects them.
- Not proven here: the install into Joi's real `~/.claude` (run `scripts/install-live-pane.sh --provider claude` from the main checkout after merge).

## Proof C output

```
aw-live: live · ctx 22% · $0.07 · 3 calls
context
  window        22% ▓▓░░░░░░░░
  tokens        44k of 200k
session
  cost          $0.07
  tokens        125k in · 1k out
  calls         3 (0 subagent)
  over 200k     0
judge
  sessions      not linked yet (judge predates decision_details.session_id)
wakes
  queued now    0
  context guard 0 fires
```

Direct `scorer live --session <id> --cwd <dir> --projects-dir ~/.claude/projects`:

```
context   44.1k of 200.0k (22%)
session   3 calls (0 subagent) · 125.3k in · 1.1k out · 0 over 200k
judge     not linked to sessions yet (judge predates decision_details.session_id)
wakes     0 queued · context-guard 0 fires
```

## Proof D output

```
aw-live: live · ctx 22% · $0.07 · 3 calls · judge 3 (1 unsure) · gates 3
...
judge
  calls         3 · jev 67% · rules 33%
  vs rules      agreed 1 · overrode 1 · unsure 1
  latency       p50 350 ms · p95 400 ms
gates
  scope-gate    1 · ready 1 · p50 400 ms
  done-gate     1 · continue 1 · p50 350 ms
  send-gate     1 · send 1 · p50 0 ms
wakes
  queued now    0
  context guard 0 fires
```

## Proof E lines from the capture

Slash menu entry:

```
/live        Toggle the live scorer pane (context, cost, judge, gates)
```

Pane (box drawing, from the final redraw):

```
│context│
│window        30% ▓▓▓░░░░░░░│
│tokens        59k of 200k│
│session│
│cost          $0.09│
│tokens        174k in · 900 out│
│calls         3 (0 subagent)│
│over 200k     0│
│judge│
│sessions      not linked yet (judge predates decision_details.sessi…│
│wakes│
│queued now    0│
│context guard 0 fires│
 r:refresh
```
