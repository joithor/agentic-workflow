# Judge — `judge`

`Aw.Judge` (`src/Judge`, tier 1, four projects) is the C# judge: the cheap-agent harness's decision CLI that the bash
hooks call (send-gate, scope-gate, done-gate, subagent-start-map, judge-health, prompt-sort). It is a reimplementation,
not a translation of the TS judge (`judge/src/**`): the TS package is a guide to what the judge does, never to how.
Its contracts are the C# tests in `tests/Aw.Judge.Tests`, except the **hook contract** below, which stays frozen
until the switch (spec §4a step 1, §4c). Before the switch nothing installs it (spec §4a: the installed toolkit stays
TypeScript); the switch puts the NativeAOT `judge` binary where the hooks find `judge` on `PATH`.

## Package shape

```
Aw.Judge.Host ──► Aw.Judge.Features ──► Aw.Judge.Core ◄── Aw.Judge.Infrastructure
```

| Project | Holds |
|---|---|
| `Aw.Judge.Core` | Typed ids (`Ids`: `DecisionId`, `ToolUseId`, `SessionId`, `PromptId`, `AgentId`), the domain records (`Briefs`, `Decisions`, `Health`, `Config`, `Providers`), the eight questions with their input records (`Questions`), the evaluation engine with the question catalog, the rules provider and `ProviderSet` (`Evaluation`), the error catalogue (`Errors.JudgeErrors`, AW-JUDGE-NNN) and the ports (`Ports`) |
| `Aw.Judge.Features` | One slice per command: `AskQuestion` (every `judge <question>`), `SaveBrief`, `GetBrief`, `MapBriefByName`, `MapBriefByDispatch`, `SetBriefAgentId`, `ExplainDecision` (`why`), `UndoDecision`, `ApproveDecision`, `GetHealth`, `GetConfig`, `SetConfig` |
| `Aw.Judge.Infrastructure` | The SQLite stores over `decisions.sqlite`, the config file, the redactor adapter over `Aw.Redaction`, the `PATH` executable locator, random decision ids, and the settings read from the environment |
| `Aw.Judge.Host` | `Program.cs`, `Composition.Compose`, one command per slice in `Commands/`, the command table, `JudgeJsonContext` (the package's one source-generated JSON context) |

Core's namespaces form a graph with no cycle (`ARCH-CYCLE-001`): `Errors` and `Ids` are leaves, `Providers`,
`Config`, `Briefs`, `Decisions`, `Health` and `Questions` sit on them, `Ports` on those, `Evaluation` on `Ports`, and
the root holds only `AddJudgeCore()`.

### Ports

| Port | Adapter (Infrastructure) | Fake and contract (`tests/Aw.Judge.Testing`) |
|---|---|---|
| `IBriefStore` | `SqliteBriefStore` (`briefs` table) | `InMemoryBriefStore`, `BriefStoreContract` |
| `IDecisionLog` | `SqliteDecisionLog` (`decisions`, `decision_details`, `failures`) | `InMemoryDecisionLog`, `DecisionLogContract` |
| `IJudgeConfigStore` | `FileJudgeConfigStore` (`config.json`) | `InMemoryJudgeConfigStore`, `JudgeConfigStoreContract` |
| `IInputRedactor` | `AwInputRedactor` (over `Aw.Redaction`) | `FakeInputRedactor`, `InputRedactorContract` |
| `IExecutableLocator` | `PathExecutableLocator` (`PATH` lookup for the agent CLIs) | `FakeExecutableLocator`, `ExecutableLocatorContract` |
| `IModelProvider` | `RulesProvider` (in Core: deterministic, always unavailable); the agent CLIs and Jev in Infrastructure (Task 7) | `ScriptedProvider` |

Plus the Kernel's `IIdGenerator` (`RandomIdGenerator`) and the BCL's `TimeProvider`.

## JSON

Everything the judge reads or writes as JSON goes through the Kernel `AwJson` profile and `JudgeJsonContext`
(camelCase unless a record names its fields, required and nullable members enforced on read):

- stdin is deserialized into the command's input record (`BriefInput` for `brief save`); any failure, syntax, shape or
  an id past its 4,096-character cap, is exit 1 with one line `invalid input: <reason>`. A question's stdin is
  deserialized by the evaluator into the question's input record (Questions below); any failure, or a required text
  that is blank, is the `invalid-input` escalation, recorded like any other;
- `config.json` is deserialized into `JudgeConfig` and then checked (`JudgeConfigParser`);
- the JSON columns `decisions.skipped` (a list of `{provider, reason}`) and `decision_details.probabilities` (option
  → probability) are the typed values, serialized through the same options.

## CLI contract

`judge <command> [args]`, or `judge <question> < input.json` for any first word that isn't a command.

### Exit codes and streams

| Exit | Meaning | Streams |
|---|---|---|
| 0 | Ok | The result on stdout (JSON or a bare id), nothing on stderr |
| 1 | Error: not found, invalid input, an invalid stored config, a store that can't be used | Nothing on stdout; one stderr line, the error's message |
| 2 | Escalated: a question that couldn't decide; for `ask-check`, "continue" (Questions below) | The question's envelope on stdout |
| 64 | Usage: no command, an unknown subcommand or question, a missing or extra argument, an argument that doesn't parse | One stderr line starting `usage: judge ` (the synopsis, then the problem in parentheses) |

Usage lines name the command's synopsis, e.g. `usage: judge why <decision-id>`, `usage: judge brief map-by-dispatch
<session-id> <prompt-id> <subagent-type>`, `usage: judge config set <question> enabled true|false | threshold <0-1>
(unknown field 'thresh')`; with no arguments at all, `usage: judge <command> [args] | judge <question> < input.json`
(`CliUsageTests`).

Every stream that has text ends with one `\n`. Log lines (Aw.Kernel.Telemetry JSON lines, `AW_LOG_LEVEL`) also go to
stderr; the hooks discard stderr except where the hook contract says.

### Commands

| Command | Input | Output (exit 0) | Errors |
|---|---|---|---|
| `health` | — | `HealthOutput`: `{"status": "ok"\|"degraded"\|"down", "failures24h": n}` | store error → 1 |
| `brief save` | stdin `BriefInput`: `toolUseId`, `sessionId`, `promptId`, `dispatchName` (string or null), `subagentType`, `goal`, `acceptanceCriteria`, `proofCommand`, all required, strings may be empty, other keys ignored | nothing | `invalid input: …` → 1; `brief already saved: <toolUseId>` → 1 |
| `brief get <toolUseId>` | — | `BriefOutput`: `toolUseId`, `agentId`, `sessionId`, `promptId`, `dispatchName`, `subagentType`, `goal`, `acceptanceCriteria`, `proofCommand`, `savedAt` | `no brief for tool_use_id: <id>` → 1 |
| `brief map-by-name <name>` | — | the oldest unmapped brief's tool use id with that dispatch name, or nothing | store error → 1 |
| `brief map-by-dispatch <sessionId> <promptId> <subagentType>` | — | the oldest unmapped matching brief's tool use id, or nothing | store error → 1 |
| `brief set-agent-id <toolUseId> <agentId>` | — | nothing (an unknown tool use id changes nothing) | store error → 1 |
| `why <decisionId>` | — | `DecisionOutput` (below) | `unknown decision: <id>` → 1 |
| `undo <decisionId>` | — | `DecisionOutput` with `undone_at` set | `unknown decision: <id>` → 1 |
| `approve <decisionId>` | — | `DecisionOutput` with `approved_at` set; approving again keeps the first time | `unknown decision: <id>` → 1 |
| `config get` | — | the effective `JudgeConfig` (an invalid `config.json` prints the defaults and logs one warning) | store error → 1 |
| `config set <question> enabled true\|false` | — | the saved `JudgeConfig` | usage → 64; an invalid `config.json`: `invalid config <file>: <reason>; fix or delete it, then set again` → 1 |
| `config set <question> threshold <number>` | — | the saved `JudgeConfig` | not a number → 64; outside 0–1 → 1; an invalid `config.json` → 1 as above |
| `<question>` | stdin: the question's input record (Questions below) | the decided envelope (exit 0) or the escalated one (exit 2) | an unknown question or any argument → 64 |

`DecisionOutput` is the decision row with the `decisions` table's column names: `id`, `ts`, `question`,
`content_class`, `provider`, `decision`, `confidence`, `reason_code`, `latency_ms`, `input_digest`, `undone_at`,
`chain_position`, `skipped` (`[{provider, reason}]`, reason `unavailable|failed|timeout|below_threshold`), `outcome`
(`decided|escalated|failed`), `approved_at`. Timestamps are UTC ISO-8601 (`AwJson`); `undone_at` and `approved_at`
are null until set.

`JudgeConfig` (`config.json`, written indented): `questions` (required: name → `{enabled, threshold, providers?}`,
`threshold` 0–1, `providers` from `rules`, `jev`, `claude-cli`, `codex-cli`, `cursor-cli`), `providers?`
(`agentClis?`: agent CLI names in priority order, `jev?`: false drops Jev), `promptSort?` (`enabled?`, `budgetMs?`
200–1050, `cooldownPrompts?` a whole number ≥ 0, `scaffolds?`: `brief`, `bugfix`, `ui-evidence`, `plan-first` → on).
Unknown keys are ignored; a wrong type, a missing required field, an unknown name or a value out of range makes the
whole file invalid (AW-JUDGE-007): readers use the defaults (wake-gate enabled at 0.7) and log one warning, and
`config set` refuses to overwrite it. A valid file without wake-gate gets the default one.

### Questions

`judge <question> < input.json` is one command for all eight questions (the `AskQuestion` slice). It reads stdin, takes
no arguments, and prints one envelope on stdout:

| Outcome | Exit | Stdout |
|---|---|---|
| Decided | 0 | `{"decision", "confidence", "model", "reasonCode", "id"}` plus the question's declared extra fields (`chosenIndex`, `reasons`) when the deciding provider sent them; a provider's undeclared fields are dropped |
| Escalated | 2 | `{"escalated": true, "reasonCode"}`, reason `question-disabled`, `invalid-input`, `below-threshold` or `no-provider-decided` |

`model` is the provider that decided (`rules` for a pre-rule or a fallback rule), `id` the decision's id for `why`,
`undo` and `approve`. Every call writes one decision row, escalations included. **Hook-contract exception:**
`ask-check` exits **0 for `ask` and for any escalation** (an unsure judge asks) and **2 for `continue`**, because
`done-gate.sh` branches on those codes; its envelope is the same as every other question's. The hooks fail open on any
other non-zero exit.

How a call runs: the question's `config.json` entry (`enabled`, `threshold`, `providers`; no entry means the
question's own threshold) becomes the evaluator's override, and a disabled question escalates `question-disabled`; an
unreadable or invalid `config.json` runs on the defaults and logs one warning. The chain is the one `Composition` builds
(`ProviderSet`), restricted by the allowlist when one is given. **Providers today:** `Composition.ModelProviders()` is
the one place the list is built, and it is `rules` alone, so the commands run offline and any input no pre-rule settles
escalates `no-provider-decided` (exit 2; `ask-check` asks). The agent-CLI and Jev providers, the chain built from
`config.json` and `AW_PROVIDER`, and the `--providers` allowlist (`AskQuestionRequest.AllowedProviders`) plug in there.

| Question | Input record (stdin) | Outputs | Class | Threshold | Budget | Pre-rules (decided by `rules` without a model) | Fallback after an unsure model |
|---|---|---|---|---|---|---|---|
| `wake-gate` | `{text, senderKind}`, `senderKind` one of `teammate`, `subagent`, `main`, `background` | `send`, `batch`, `drop` | message-meta | 0.7 | 10 s | a `{"type":"ack"\|"idle_notification"…}` message → `drop`; an urgent word (blocked, blocker, failed, error, question, approve, approval; any case) → `send`; trimmed text of 12 characters or fewer → `drop` | `send` |
| `brief-scope` | `{agentType, goal, acceptanceCriteria, proofCommand, skillInternal?}` | `ready`, `missing`, `needs_design` | brief | 0.6 | 10 s | `agentType` `Explore`, `claude-code-guide` or `lean-researcher`, or `skillInternal: true` → `ready`; a blank goal or acceptance criteria → `missing` | none (escalates) |
| `ask-check` | `{transcriptTail}` | `continue`, `ask` | transcript | 0.6 | 10 s | the deny list → `ask`: merge, delete, force push (`force-push`, `force pushed`, …), or a push with `-f`, `--force`, `--force-with-lease` or a `+refspec`; a plain push isn't listed | `ask` |
| `rule-check` | `{rule, hunk}` | `violated`, `fine`, `n/a` | diff | 0.6 | 10 s | a blank hunk → `n/a` | none |
| `resolution-check` | `{brief, expected, actual, rootCause, checkKind, checkSummary, beforePassed, afterPassed, diffStat, diff?}`, `checkKind` `ui-evidence` or `test`; a blank `brief` is invalid input | `resolved`, `partial`, `unresolved` (+ `reasons`) | brief | 0.8 | 15 s | the check fails after the fix, or already passed before it → `unresolved` | none |
| `turn-progress` | `{problem, turnDiff, acceptanceCriteria?, priorDiffStat?, signals?}` (the last three default to empty); a blank `problem` is invalid input | `progressing`, `stalled`, `regressing`, `off-target` | diff | 0.7 | 10 s | a blank `turnDiff` → `stalled` | none |
| `ui-element-repair` | `{brokenSelector, step, candidates: [{index, role, accessibleName, testId, text}]}` (the four strings may be null) | `repaired` (+ `chosenIndex`), `no-good-candidate` | code | 0.6 | 10 s | no candidates → `no-good-candidate` | none |
| `visual-critique` | `{afterScreenshot, evidenceDir, baselineScreenshot?}` | `looks-right`, `looks-off`, `sloppy` (+ `reasons`) | image | 0.5 | 20 s | none: judging the screenshot is the question | none |

The input names of `wake-gate`, `brief-scope` and `ask-check` are hook contract; the other five are the C# judge's own
design (their tier-2 callers are ported to this contract). Prompts carry untrusted text (a ticket, a diff, a problem
statement) inside tagged blocks with the tags inside it defused, capped (`UntrustedText`). Pinned by the
`Questions/*Tests` (each question's rules, offline with `ScriptedProvider`), `AskQuestionTests` (config, allowlist,
session) and `AskQuestionCliTests` (envelopes, exit codes, the ask-check exception, invalid input, unknown question).

### Errors (AW-JUDGE-NNN)

| Code | Kind | When |
|---|---|---|
| AW-JUDGE-001 | Invalid | Input failed validation (an id past its cap, a threshold outside 0–1) |
| AW-JUDGE-002 | NotFound | `why`, `undo`, `approve` of an unknown decision |
| AW-JUDGE-003 | Conflict | A decision id already recorded |
| AW-JUDGE-004 | Conflict | A brief already saved for the tool use id |
| AW-JUDGE-005 | NotFound | `brief get` of a tool use id with no brief |
| AW-JUDGE-007 | Invalid | `config.json` can't be read as a judge config |
| AW-JUDGE-010 | Invalid | The `--providers` flag or `AW_JUDGE_PROVIDERS` is malformed (a usage error, 64) |

## Hook contract

What the hooks that keep running read, and nothing else. It stays frozen until the switch; Task 9's real-hook check
runs the hook scripts against the C# binary.

| Hook | Calls | Reads | Pinned by |
|---|---|---|---|
| `scope-gate.sh` | `judge brief save` < `{toolUseId, sessionId, promptId, dispatchName, subagentType, goal, acceptanceCriteria, proofCommand}` (`dispatchName` null when empty) | nothing (`> /dev/null 2>&1 \|\| true`) | `SaveBriefCliTests.Brief_save_stores_the_payload_and_prints_nothing` |
| `scope-gate.sh` | `judge brief-scope` < `{agentType, goal, acceptanceCriteria, proofCommand, skillInternal?}` (`AW_SESSION_ID` set) | `.decision` of stdout; a non-zero exit fails open | `AskQuestionCliTests.Brief_scope_prints_its_decision`, `BriefScopeTests` |
| `send-gate.sh` | `judge wake-gate` < `{text, senderKind}` (`AW_SESSION_ID` set) | `.decision` of stdout; a non-zero exit fails open | `AskQuestionCliTests.A_decided_question_prints_the_decided_envelope_and_exits_0`, `WakeGateTests` |
| `done-gate.sh` | `judge ask-check` < `{transcriptTail}` | the exit code: 0 = ask (an escalation asks too), 2 = continue, anything else fails open | `AskQuestionCliTests.Ask_check_exits_0_for_ask_or_an_escalation_and_2_for_continue`, `AskCheckTests` |
| `done-gate.sh` | `judge brief get "task:<session>"` | stdout as the brief JSON; on a non-zero exit, stderr starting `no brief for` means "no brief", anything else fails open | `GetBriefCliTests` (the `no brief for` prefix, exit 1) |
| `done-gate-annotate.sh` | `judge brief get <toolUseId>` | `.acceptanceCriteria` | `GetBriefCliTests` |
| `subagent-start-map.sh` | `judge brief map-by-name <agentType>`, `judge brief map-by-dispatch <session> <prompt> <agentType>` | stdout: the tool use id or nothing, exit 0 | `MapBriefByNameCliTests`, `MapBriefByDispatchCliTests` |
| `subagent-start-map.sh` | `judge brief set-agent-id <toolUseId> <agentId>` | nothing (`\|\| true`) | `SetBriefAgentIdCliTests` |
| `judge-health.sh` | `judge health` | `.status` (`ok`, `degraded`, `down`) and `.failures24h`; a non-zero exit is "health check failed" | `GetHealthCliTests` |
| `prompt-sort.sh` | `judge prompt-sort` < `{prompt, sessionId}` | stdout (J2) | J2 |

The hooks also export `AW_STATE_DIR`, `AW_SESSION_ID`, `AW_PROVIDER` and `AW_JUDGE_PROVIDERS` to the judge (below),
and write their own files under `$AW_STATE_DIR/judge/` (`sessions/`, `outbox`, `teammate-names.jsonl`), which the
judge doesn't own.

## State files

All under `${AW_STATE_DIR:-~/.agentic-workflow}/judge/`. The C# build starts with fresh state (spec §4c); it never
reads a TS-written file.

| File | Holds |
|---|---|
| `decisions.sqlite` | WAL SQLite (`Aw.Kernel.Data`), migrations embedded in `Aw.Judge.Infrastructure/Sqlite/Migrations/` (`0001_judge.sql`, `0002_approved_at.sql`; a migration is never edited once merged): `decisions` (one row per decision, `approved_at` from 0002), `decision_details` (the redacted input and the provider's detail, pruned after 30 days), `failures` (provider failures, for `health`), `briefs` (the dispatch briefs), `schema_version` |
| `config.json` | `JudgeConfig`, written by `config set`; absent means the defaults |

## Environment

| Variable | Read by | Meaning |
|---|---|---|
| `AW_STATE_DIR` | Infrastructure settings | The state root; the judge's files are under `<root>/judge/`. Default `~/.agentic-workflow` |
| `AW_SESSION_ID` | the question commands (`Composition`, once) | The calling session, stored with the decision's detail row; a value past the session id's cap is ignored |
| `AW_PROVIDER` | the provider chain (Task 7) | The host the calling session runs in (`claude`, `codex`, `cursor`), which leads the agent CLI order unless `config.json` sets `providers.agentClis` |
| `AW_JUDGE_PROVIDERS` | the providers allowlist | A comma-separated allowlist of providers when the `--providers` flag is absent |
| `TYPESAFE_API_KEY` | the Jev provider (Task 7) | Jev's API key; without it Jev is unavailable |
| `AW_LOG_LEVEL` | Aw.Kernel.Telemetry | The stderr log level (default information; the hooks discard stderr) |
