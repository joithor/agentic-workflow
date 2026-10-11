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
| `Aw.Judge.Infrastructure` | The SQLite stores over `decisions.sqlite`, the config file, the redactor adapter over `Aw.Redaction`, the `PATH` executable locator, the process runner, the secret source (environment, then keychain), the model providers and their catalog (`Providers/`), random decision ids, and the settings read from the environment |
| `Aw.Judge.Host` | `Program.cs`, `Composition.Compose`, one command per slice in `Commands/`, the command table, `JudgeJsonContext` (the package's one source-generated JSON context) |

Core's namespaces form a graph with no cycle (`ARCH-CYCLE-001`): `Errors` and `Ids` are leaves, `Providers`,
`Config`, `Briefs`, `Decisions`, `Health`, `Questions`, `Processes` and `Secrets` sit on them, `Ports` on those, `Evaluation` on `Ports`, and
the root holds only `AddJudgeCore()`.

### Ports

| Port | Adapter (Infrastructure) | Fake and contract (`tests/Aw.Judge.Testing`) |
|---|---|---|
| `IBriefStore` | `SqliteBriefStore` (`briefs` table) | `InMemoryBriefStore`, `BriefStoreContract` |
| `IDecisionLog` | `SqliteDecisionLog` (`decisions`, `decision_details`, `failures`) | `InMemoryDecisionLog`, `DecisionLogContract` |
| `IJudgeConfigStore` | `FileJudgeConfigStore` (`config.json`) | `InMemoryJudgeConfigStore`, `JudgeConfigStoreContract` |
| `IInputRedactor` | `AwInputRedactor` (over `Aw.Redaction`) | `FakeInputRedactor`, `InputRedactorContract` |
| `IExecutableLocator` | `PathExecutableLocator` (`PATH` lookup for the agent CLIs) | `FakeExecutableLocator`, `ExecutableLocatorContract` |
| `IModelProvider` | `ClaudeCliProvider`, `CodexCliProvider`, `CursorCliProvider`, `JevProvider` (Providers below); rules is the engine's own `RulesProvider` | `ScriptedProvider` |
| `IProviderCatalog` | `ProviderCatalog` (the lineup from `config.json`, `AW_PROVIDER` and `PATH`) | `FakeProviderCatalog`, `ProviderCatalogContract` |
| `IProcessRunner` | `SystemProcessRunner` (`System.Diagnostics.Process`) | `FakeProcessRunner` (runs `FakeProgram`s), `ProcessRunnerContract` |
| `ISecretSource` | `KeychainSecretSource` (the variable, then the macOS keychain) | `FakeSecretSource`, `SecretSourceContract` |

Plus the Kernel's `IIdGenerator` (`RandomIdGenerator`) and the BCL's `TimeProvider`.

## JSON

Everything the judge reads or writes as JSON goes through the Kernel `AwJson` profile and `JudgeJsonContext`
(camelCase unless a record names its fields, required and nullable members enforced on read):

- stdin is deserialized into the command's input record (`BriefInput` for `brief save`); any failure, syntax, shape or
  an id past its 4,096-character cap, is exit 1 with one line `invalid input: <reason>`;
- `config.json` is deserialized into `JudgeConfig` and then checked (`JudgeConfigParser`);
- the JSON columns `decisions.skipped` (a list of `{provider, reason}`) and `decision_details.probabilities` (option
  → probability) are the typed values, serialized through the same options.

The one exception is the providers' wire formats (the agent CLIs' output envelopes, an image input's paths, Jev's request
and response): those records are internal to Infrastructure, which the Host's context can't name, so they have their own
internal `ProviderWireJsonContext`, used only through `AwJson.CreateOptions` (`WireJson.Options`). An answer's free
fields (the decision plus the question's extra properties) are read as a `JsonObject` and checked against the question.

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

## Providers

A question is answered by the first provider in its chain that decides (the evaluation engine, `QuestionEvaluator`).
The providers live in `Aw.Judge.Infrastructure/Providers/`; Composition reaches them through one port.

### The catalog

`IProviderCatalog.Lineup(ProvidersConfig? config, IReadOnlyCollection<ProviderName>? allowed, IModelProvider rules)` returns a
`ProviderLineup(Providers, Chain)`, which is what an `EvaluationContext` takes. It is the one place the inputs meet:

1. the agent CLIs: `config.json`'s `providers.agentClis` when set, else the default order (claude, codex, cursor)
   with the `AW_PROVIDER` host moved first (`AgentCliResolution`); either way only those whose binary is on `PATH`;
2. Jev, unless `providers.jev` is false;
3. the chain: text classes get Jev, the agent CLIs, then rules; image gets the agent CLIs, then rules
   (`ChainSpec.Build`); then the `--providers` / `AW_JUDGE_PROVIDERS` allowlist, which always keeps rules;
4. the providers: one instance of each model provider the chain names, then the `rules` provider the caller passes
   (the engine's own; the catalog doesn't make one).

`AddJudgeInfrastructure` registers it (its `AddModelProviders` part) over one instance of each model provider: Jev
(with its own `HttpClient`) and the three agent CLIs (over `IProcessRunner`).

### The agent CLIs

Each runs headless on a small fast model, with the recursion guard `AW_JUDGE_CHILD=1` (every `aw:*` hook in the child
exits at once), standard input closed, the question's time budget as its timeout, and a fresh empty temp directory as
its working directory, deleted afterwards. The answer schema is the question's outputs as the decision's enum, plus
its extra properties. All three read images, so all three cover every content class.

| Provider | Binary and model | Isolation | Schema and answer |
|---|---|---|---|
| `claude-cli` | `claude -p --model haiku --effort low` | `--no-session-persistence`, no MCP servers, `--setting-sources ""` (no settings files or CLAUDE.md), hooks and slash commands off, thinking off (`MAX_THINKING_TOKENS=0`); text: no tools, the prompt as `--system-prompt`, the input as the message; image: only `Read`, scoped to the evidence directory, which is the working directory | `--json-schema`; the envelope's `structured_output`, else its `result` text parsed as JSON |
| `codex-cli` | `codex exec --model gpt-6-luna -c model_reasoning_effort="low"` | `-C` the workspace, `--ephemeral`, `--ignore-user-config`, `--ignore-rules`, read-only sandbox, the `hooks` feature (and tool-list features) disabled; images as `--image` absolute paths; the prompt after `--` | `--output-schema schema.json` (in the workspace) in OpenAI strict mode: every property required, extras nullable, no others; the last `agent_message` of the `--json` event lines |
| `cursor-cli` | `cursor-agent -p --model gpt-5.6-luna-none-fast` | `--mode ask`, `--sandbox enabled`, `--trust` (a fresh workspace needs it), `--workspace` the temp directory (the evidence directory for images, named by absolute path) | no schema flag: the schema is stated in the prompt; the last JSON object in the envelope's `result` text |

The answer object is read the same way for all three: a string `decision` among the outputs; a `confidence` 0–1 when
present, else **1.0** (the CLIs answer under a schema with no confidence field, so a well-formed answer is taken as
certain); each declared extra checked against its fragment's JSON `type` (and array `items`), a null one absent;
undeclared fields dropped.

### Jev

`POST https://api.typesafe.ai/v1/systemone` with `{state: <input>, model: "jev-1.13.0", questions: {decision:
{type: "choice", instructions: <prompt>, criteria: {<output>: <criterion or the output>}}}}` and `Authorization:
Bearer <key>`; text classes only. The key comes from `ISecretSource` (`TYPESAFE_API_KEY`, else the keychain).
The answer's `choice`, `confidence` (1.0 when absent) and `probabilities` become the decision.

### Rules

`rules` closes every chain but decides nothing itself: pre-rules and fallback rules run in the engine, and the rules
provider is the engine's (`Aw.Judge.Core.Evaluation`), always unavailable, never a failure. The catalog takes it as an
argument.

### Reason codes

What the engine does with each result is the engine's rule: decided → a decision (below threshold: skipped, then the
fallback rule); unavailable → skipped with no failure row, except `timeout`, which is a failure row; errored → a
failure row with the code.

| Result | Code | Providers | When |
|---|---|---|---|
| decided | `model` | all | an answer within the outputs |
| unavailable | `timeout` | agent CLIs, Jev | past the question's budget; a CLI is killed with its whole process tree |
| unavailable | `not-installed` | agent CLIs | the binary isn't on `PATH` (or can't be started) |
| unavailable | `no-api-key` | Jev | no key in the environment or the keychain |
| unavailable | `network-error` | Jev | the API can't be reached (offline, DNS, refused) |
| errored | `exit-N` | agent CLIs | the CLI exited non-zero |
| errored | `unparseable-output` | agent CLIs | stdout isn't the CLI's envelope (or event lines) |
| errored | `unparseable-answer` | all | no JSON answer object with a string decision |
| errored | `out-of-enum` | all | the decision isn't one of the outputs |
| errored | `extra-out-of-schema` | agent CLIs | a declared extra has the wrong JSON type |
| errored | `cli-reported-error` | agent CLIs | `is_error` (claude, cursor) or codex's `turn.failed`/`error` with no answer |
| errored | `invalid-image-input` | agent CLIs | an image question's input has no `afterScreenshot` and `evidenceDir` |
| errored | `http-NNN` | Jev | a non-success status |
| errored | `too-many-options` | Jev | more than 255 outputs |

### Processes and secrets

`SystemProcessRunner` finds a bare program name on the `PATH` the environment gives (a path is run as is), reads both
output streams concurrently from the start and writes the input on its own task (so a full pipe never blocks it), and
on a timeout or a cancellation kills the whole process tree (`Process.Kill(entireProcessTree: true)`), so a child that
inherited the pipes can't hold the run open. A program that can't be started is `NotStarted`, never an exception.

`KeychainSecretSource` reads a secret's variable; when it is unset or empty it runs `security find-generic-password
-s agentic-workflow-typesafe -a TYPESAFE_API_KEY -w` (5 s timeout). A missing entry, a missing `security` (not macOS)
or a hung keychain all read as unset.

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
| `AW_PROVIDER` | `ProviderCatalog` | The host the calling session runs in (`claude`, `codex`, `cursor`), which leads the agent CLI order unless `config.json` sets `providers.agentClis` |
| `AW_JUDGE_PROVIDERS` | the providers allowlist | A comma-separated allowlist of providers when the `--providers` flag is absent |
| `TYPESAFE_API_KEY` | `KeychainSecretSource` | Jev's API key; when unset or empty, the keychain entry `agentic-workflow-typesafe` / `TYPESAFE_API_KEY`; without either Jev is unavailable |
| `AW_LOG_LEVEL` | Aw.Kernel.Telemetry | The stderr log level (default information; the hooks discard stderr) |
