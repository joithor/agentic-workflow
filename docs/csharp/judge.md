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
| `Aw.Judge.Core` | Typed ids (`Ids`: `DecisionId`, `ToolUseId`, `SessionId`, `PromptId`, `AgentId`), the domain records (`Briefs`, `Decisions`, `Health`, `Config`, `Questions`, `Providers`), the evaluation engine (`Evaluation`), the error catalogue (`Errors.JudgeErrors`, AW-JUDGE-NNN) and the ports (`Ports`) |
| `Aw.Judge.Features` | One slice per command: `SaveBrief`, `GetBrief`, `MapBriefByName`, `MapBriefByDispatch`, `SetBriefAgentId`, `ExplainDecision` (`why`), `UndoDecision`, `ApproveDecision`, `GetHealth`, `GetConfig`, `SetConfig` |
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
| `IModelProvider` | (Task 7: the agent CLIs and Jev) | `ScriptedProvider` |

Plus the Kernel's `IIdGenerator` (`RandomIdGenerator`) and the BCL's `TimeProvider`.

## JSON

Everything the judge reads or writes as JSON goes through the Kernel `AwJson` profile and `JudgeJsonContext`
(camelCase unless a record names its fields, required and nullable members enforced on read):

- stdin is deserialized into the command's input record (`BriefInput` for `brief save`); any failure, syntax, shape or
  an id past its 4,096-character cap, is exit 1 with one line `invalid input: <reason>`;
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
| 2 | Escalated: a question that couldn't decide (Task 6); for `ask-check`, "continue" | The question's output |
| 64 | Usage: no command, an unknown subcommand, a missing or extra argument, an argument that doesn't parse | One stderr line starting `usage: judge ` (the synopsis, then the problem in parentheses) |

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
| `<question>` | stdin: the question's input record (Task 6) | the question's output | `unknown question: <name>` → 1 |

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
| `scope-gate.sh` | `judge brief-scope` < input (`AW_SESSION_ID` set) | `.decision` of stdout; a non-zero exit fails open | Task 6 |
| `send-gate.sh` | `judge wake-gate` < `{text, senderKind}` (`AW_SESSION_ID` set) | `.decision` of stdout; a non-zero exit fails open | Task 6 |
| `done-gate.sh` | `judge ask-check` < `{transcriptTail}` | the exit code: 0 = ask, 2 = continue, anything else fails open | Task 6 |
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
| `AW_SESSION_ID` | the question commands (Task 6) | The calling session, stored with the decision's detail row |
| `AW_PROVIDER` | the provider chain (Task 7) | The host the calling session runs in (`claude`, `codex`, `cursor`), which leads the agent CLI order unless `config.json` sets `providers.agentClis` |
| `AW_JUDGE_PROVIDERS` | the providers allowlist | A comma-separated allowlist of providers when the `--providers` flag is absent |
| `TYPESAFE_API_KEY` | the Jev provider (Task 7) | Jev's API key; without it Jev is unavailable |
| `AW_LOG_LEVEL` | Aw.Kernel.Telemetry | The stderr log level (default information; the hooks discard stderr) |
