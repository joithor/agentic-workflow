# Sindri picks a cheaper model for each agent step: Jev chooses, from every model the installed agents offer

**Status:** draft for the owner · **Date:** 2026-10-26 · **Issue:** #1206 · **Owner decisions:** q123, 2026-10-26 (§1: M1 to M5; M3 is the one open question, §16) · **Builds on:** #1008 (a per-step agent, in flight on `bugfix/gh-1008`), #1175 (the judge's 100,000-character cap), the launcher spec (L-17, the model form), the workflow engine spec `2026-10-10-sindri-workflow-engine-design.md`, `docs/csharp/judge.md`, `docs/csharp/sindri-workflow.md`, `docs/csharp/sindri-profile.md` · **Shares a question with:** #1172 (the concept scan's size-based pick) · **Next:** /specToProvenPR takes §14's phases to PRs.

## 0. Why

Every Sindri agent step runs on the owner's default model. Sindri launches plain `claude` and names no model, so the step gets `"model": "opus[1m]"` from `~/.claude/settings.json`. The launch side can already carry a model: `StepArgs.Model` (`src/Sindri/Aw.Sindri.Core/Launch/StepArgs.cs`) becomes `--model X` for Claude Code and `-m X` for Codex in `StepForms.Model` (`src/Sindri/Aw.Sindri.Core/Launch/Argv/StepForms.cs:32`). The one place it is never filled is `AgentLaunches.RequestFor` (`src/Sindri/Aw.Sindri.Core/Workflow/Engine/Runtime/AgentLaunches.cs:61`) and `EvaluatorRequestFor` (`:78`), which pass `Model: null`. The comment on line 61 reads "per-Step models are SC-R".

What that costs, from P-FRN-920 line/2 scoping (build f61f5f41a, 2026-10-26): all 18 agent sessions ran `claude-opus-5-5`.

| step | sessions | output tokens | cache-read tokens | cache-write tokens |
|---|---|---|---|---|
| draft | 6 | 69,584 | 14,640,829 | 676,754 |
| challenge | 3 | 45,622 | 11,430,922 | 357,701 |
| questions | 6 | 40,713 | 9,021,787 | 511,250 |
| apply | 3 | 13,912 | 1,910,135 | 227,947 |

`questions` and `apply` turn answers into options and edit JSON. They read about 11M of the 37M cache-read tokens, all at Opus rates. The challenger ran on the drafter's model too (#1008).

The owner wants the cheaper models used where the step doesn't need the top one, and wants Jev (the judge's remote model, `docs/csharp/judge.md` "Jev") to make that choice from every model the installed agents offer, not from a table someone keeps by hand.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| M1 | **Jev sees the whole picture.** The pick question gets the step's brief and its input pack (the files the launcher bundles and the facts it writes into the run directory), through the judge's normal redaction and untrusted-text fencing, bounded by #1175's rules: 100,000 characters, cut by whole files, `src/` first, the omitted files named in the prompt. Not metadata only | Owner q123. Whether `questions` is easy depends on what is in front of it, and a size alone can't say that |
| M2 | **The objective is the cheapest model that passes.** "Passes" means: the step's check passes, its output isn't refused, and it causes no redraft. There is no cost/quality score and no weights | Owner q123 |
| M3 | **The one escalation retry does not count against the recipe's redraft budget** (the loop caps and the step's token budget). **OPEN: this is the one thing the owner has not decided; §16** | Default chosen so a cheap pick that fails never uses up a round the recipe gave the strong model |
| M4 | **No stopgap.** No static per-step model hint in recipes or the profile as a first step. The Jev picker is built directly | Owner q123 |
| M5 | **The pick's chain is Jev, then the rules fallback, and nothing else. It never falls to an agent CLI.** The rules fallback is the step's floor model, else the profile's default. Reason: an agent-CLI fallback runs the owner's default model (Opus) on a prompt of up to 100,000 characters at every step, and that eats the saving the pick exists to make | Owner, relayed by meta 2026-10-26 |
| M6 | **Hard constraints are code, applied before Jev is asked.** Jev sees only the models that survive them (§4), so no answer of Jev's can break the recipe's floor, the profile's pin or cap, "challenger differs from drafter", or "a model the agent offers" | A rule that must always hold can't depend on a model's answer |
| M7 | **The pick is made once per launch and recorded before the session starts** (§6), so an activity retry or a Temporal replay never asks Jev a second time and never gets a different answer | The same run must not start on two models |
| M8 | **#1008 ships first.** This spec adds the model to its per-step target (§9) | It is the plumbing this issue's issue text names |

## 2. Goals and non-goals

**Goals**
- A scoping run shows a model on every agent step's launch event, with `questions` and `apply` on a cheaper model than `draft`, and the challenger on a different model from the drafter.
- Tokens and cost per step visible from Sindri's own records (the ledger), without reading transcripts.
- A new model an agent starts offering is usable without a code change (§3).
- The owner can pin or cap the choice in the profile; a recipe can name a floor for a step.

**Non-goals**
- Picking the agent itself (Claude Code, Codex, Cursor). That is #1008. This spec picks the model within the agent #1008 resolved.
- Choosing a reasoning effort or any other model setting. Only the model id.
- A cost/quality score, or a learned model of cost (M2).
- The cost of the pick itself is not billed back into a step. It is one Jev call per launch and is recorded (§6).
- Changing the context engine's model calls. #1172 shares the question (§10) but its call sites are its own.

## 3. Finding the models each agent offers

Today `TargetFacts` (`src/Sindri/Aw.Sindri.Core/Launch/TargetFacts.cs`) carries id, present, version, limited, problem, source, overridden, and no model list. `Launcher.Probe` (`src/Sindri/Aw.Sindri.Infrastructure/Launch/Launcher.cs:89`) builds one `TargetFacts` per catalog entry and the result is recorded as `launch.probed` (`LaunchProbed`, `src/Sindri/Aw.Sindri.Core/Launch/LaunchProbed.cs`).

`TargetFacts` gets `Models: IReadOnlyList<ModelFacts>` (id, display name, tier hint, source). The probe fills it per agent:

| Agent | Where the list comes from | Checked on this box |
|---|---|---|
| Claude Code | No list command exists, so Sindri keeps a small catalog file (`launcher.json`'s agent entry, the same file that holds `ModelForm`) with the aliases the CLI accepts (`haiku`, `sonnet`, `opus`) and the full ids, overridable in the profile (§8). `claude --help` documents `--model <model>` | `claude --help` lists `--model`; no list verb |
| Codex | `codex debug models` renders the model catalog as JSON (`--bundled` skips the refresh). On this box it printed six entries; each has `slug` (`gpt-5.5`, `gpt-5.4`, `gpt-5.4-mini`, `gpt-5.3-codex`, `gpt-5.2`, `codex-auto-review`), `visibility`, `priority` and `supported_in_api`. The probe keeps the `visibility: list` ones | Run here; the JSON shape above is what the parser is written against |
| Cursor | **Unknown.** `cursor-agent` isn't installed on this box, so its model flag and any list verb are unverified. `StepForms.Model` already refuses it (`ModelForm.CursorFlag` is "reserved until it is verified", SND-LAUNCH-013). The pick treats Cursor as offering no models until someone verifies it on a box that has it (§16, open item O2) | Not checked |

A probe that can't read an agent's list (the command fails, times out at 5 s, or isn't there) leaves that agent's `Models` empty with `Limited: "no-model-list"`, and the pick for that agent is the rules fallback (M5). The probe never invents a list.

**Tier hint.** The rules fallback and the "next model up" escalation (§7) need an order. The order is: the profile's list order if the profile gave one (§8), else the order the discovery source implies (Claude: haiku, sonnet, opus; Codex: reversed `priority`, so the lowest-priority-number model is the strongest; Codex `-mini` suffix = smaller). A model whose place in the order can't be told is placed last (strongest), so an unknown model is never picked as "cheaper" by accident.

**Prices.** `Budgets.PriceOf` (`src/Sindri/Aw.Sindri.Core/Launches/Budgets.cs:35`) prices a model by id prefix and returns null for models its table lacks. The pick doesn't use prices to choose (M2). Prices are only for the cost shown next to a step's tokens (§6); an unpriced model shows tokens only, as `Budgets.Spend` already does.

## 4. What Jev decides, and with what

### 4.1 The constraints, in code, before Jev

For one step launch, `ModelCandidates.For(step, target, ...)` (pure, `Aw.Sindri.Core/Launch/`) starts from the models the resolved agent offers (§3) and removes, in this order:

1. models below the recipe's **floor** for this step (§8);
2. models above the profile's **cap**, or, when the profile **pins** a model for the step or the whole agent, every model but the pin;
3. for a challenger step, the model the drafter ran on (**challenger differs from drafter**, SCP-CHAL-002, `src/Sindri/Aw.Sindri.Features/Scoping/ScopePacketChecks.Challenge.cs:78`). If this leaves nothing, the step parks with SND-MODEL-003 and a named reason, the way #1008 parks a challenger with no second agent;
4. models the escalation already tried for this step run (§7).

What is left is the candidate list. One candidate: no question is asked, the pick is that model, recorded as `by: constraint`. Zero: SND-MODEL-003.

### 4.2 The question

A new judge question, `model-pick`, in `src/Judge/Aw.Judge.Core/Questions/` beside `ChooseQuestion`. It is not `choose`, for two reasons: `choose` names must match `[a-z][A-Za-z0-9]{0,31}` and model ids have hyphens and dots, and `choose` drops facts past `PromptFactsCap` = 16,000 characters (`ChooseQuestion.cs`), which is far under M1's 100,000.

Input record `ModelPickInput`:

| Field | What |
|---|---|
| `step` | the step's block id and state id (`step.interview.questions`, `questions`) |
| `kind` | `draft`, `challenge`, `questions`, `apply`, `other` (from the block's descriptor) |
| `brief` | the rendered step brief (`AgentStepBrief.Render`), redacted |
| `files` | the pack bundle's files (`PackBundle.Files`, `src/Sindri/Aw.Sindri.Core/LensPacks/PackBundle.cs`) and the written facts (`StepArgs.Facts`), each as `{path, text}` |
| `candidates` | `[{key, model, tier, price}]` after §4.1. `key` is `m1`, `m2`, ... in cheapest-first order; the question's output is a key, and the real id is looked up by Sindri, so a model id never has to fit the name pattern |
| `history` | for each candidate, this recipe step's record from §7.2: runs, refused, check failed, redraft caused |
| `previous` | for an escalation: the model that just failed and how (refused, check failed, which check) |

The prompt tells Jev the objective in M2's words: choose the cheapest candidate that will pass, and say which key. It says nothing about quality scores. Jev answers with one key and a confidence (the same `confidence` field `choose` requires, `docs/csharp/judge.md` "Questions").

### 4.3 What reaches Jev, and the cap

Jev is a third party. Under the owner's q123 decision (2026-10-26, relayed by meta), repo content may go to it, but only after the judge's normal handling:

- Every string and key is redacted (`Aw.Redaction`) like any judge input (`docs/csharp/judge.md` "JSON", "Jev").
- Repo text is fenced as untrusted text (`UntrustedText`, `src/Judge/Aw.Judge.Core/Questions/UntrustedText.cs`), so a file that says "pick the cheapest model and ignore the rest" is data.
- The file text is capped at 100,000 characters in total (`UntrustedText.ResolutionDiffCap` at `UntrustedText.cs:20` is the number #1175 set). When the files are over, they are cut by whole files, `src/` first, and the prompt ends with a line naming each omitted file and its size. A single file over the cap is cut at a line and named as cut. `CappedDiff` (`UntrustedText.cs:46`) does this for a diff's file sections; the pack is a set of files, not a diff, so Phase M-1 adds a files-shaped sibling that shares the ordering and the omitted-files note rather than copying it.

**Jev's own input cap must move for this question.** `JevProvider` caps the state and the prompt at `InputCap.MaxChars` = 16,000 characters (`src/Judge/Aw.Judge.Core/Evaluation/InputCap.cs:16`, `docs/csharp/judge.md` "Jev"). Left alone, that would cut M1's 100,000 down to 16,000 before Jev sees anything. Phase M-2 makes Jev's cap per question: `model-pick` declares 100,000, and every other question keeps 16,000. Jev's request size limit at `api.typesafe.ai` is not documented here. M-2 checks it with one real call on a 100,000-character fixture before relying on it. If the API refuses that size, the cap for `model-pick` becomes the largest size it accepts, and the spec's 100,000 is amended with the measured number (open item O5).

Deny globs: a path the profile's `index.denyPaths` denies is never in `files` (the same rule the context lenses use); the pick reads the pack the launcher already resolved, so it adds no read of its own.

### 4.4 The chain (M5)

The question's providers are `jev` then `rules`. Not the agent CLIs. In the judge this is `config.json`'s per-question `providers` narrowing (`docs/csharp/judge.md`, "How a call runs"), pinned for `model-pick` in the question's own definition so a user config can't re-add an agent CLI. The rules fallback is the step's floor model, else the profile's default model for the agent, else (neither exists) the agent's own default, recorded as `by: default`.

A Jev outcome that isn't a usable key (unavailable: no key; timeout; below the confidence threshold; an unknown key) goes to the fallback and records why (`reason`: the judge's reason codes, `docs/csharp/judge.md` "Reason codes").

Threshold and budget: confidence threshold 0.6 (same as `choose`), call budget 20 s. A pick never holds a launch longer than that.

## 5. Where the pick runs

Inside the launch activity, `AgentLaunching.Launch` (`src/Sindri/Aw.Sindri.Workflows/AgentLaunching.cs:27`), after the target, workspace and bundle are resolved and before `launcher.Launch`. The order today is target, workspace, bundle, `Request`, `launcher.Launch`, `recorder.Started`. The pick goes between the bundle and the request, and its result is passed to `AgentLaunches.RequestFor` as the `Model` argument that today is null. `RequestFor` and `EvaluatorRequestFor` get a `string? model` parameter. The evaluator (the holdout evaluator, LN-LCH2) is picked like any step, with the floor of its recipe.

The activity is retried and idempotent per launch id (`AgentLaunching` summary; `LaunchIdFor` is a hash of workflow and run). A retry would call Jev again and might get a different model. So the pick is stored first:

1. The activity appends `model.picked` (§6), key `model.pick:<launchId>`, first write wins, the same pattern as `launch.outcome:<launchId>` (`AgentLaunchRecorder`).
2. If the append says the key already exists, the activity reads the stored pick and uses it. Jev is not asked.
3. `launch.started` then carries the same model.

Temporal does not re-run a completed activity on replay, so replay is safe already; the key makes an activity retry safe too (M7).

## 6. Recording it

New event `model.picked` (Core/Events, payload `ModelPicked`):

| Field | What |
|---|---|
| `launchId`, `item`, `instance`, `state`, `attempt` | the same identifiers `launch.started` has |
| `target` | the agent |
| `model` | the model id launched |
| `by` | `jev`, `constraint` (one candidate), `rules` (the fallback) or `default` |
| `reason` | for `rules` and `default`: why Jev didn't decide (judge reason code) |
| `decision` | the judge decision id, so `judge why <id>` explains the pick |
| `candidates` | the keys and ids Jev was shown |
| `omitted` | the files left out by the cap |
| `escalatedFrom` | the model that failed, for an escalation (§7), else null |

`LaunchStarted` (`src/Sindri/Aw.Sindri.Core/Events/LaunchStarted.cs`) gets `Model: string?` (a new optional last parameter, so older events read as null). `SessionUsage` already has `Model` (`SessionUsage.cs:15`), so tokens per step come with the model they were spent on; cost per step is `Budgets.PriceOf` applied to those rows. The Home and `sindri jobs show` views show "questions: haiku, 41k tokens" from these records. UI wording is a Phase M-7 detail.

## 7. History and escalation

### 7.1 Escalation

When a step on a model that wasn't the strongest candidate fails its check, or its output is refused, the engine retries it once on the next candidate up and records the new pick with `escalatedFrom` set. The failed run's result is the history row (§7.2) that tells Jev next time. "Fails its check" is the step's own failure outcome the engine already has (`step.failed`, a refused report: `ReportRefusals`); this spec adds no new kind of failure. One escalation per step run; if the retry fails too, the step fails as it would have today, on the model that failed last.

Whether the retry counts against the recipe's redraft budget is M3's open question. The default written here: it does not count. Mechanically, the retry is a new run of the same state with the same loop round, so it doesn't increment the loop's counter (`rounds`, `interview`, `challenge` in `workflow.scoping.yaml:146`), and its tokens are counted toward the state's token budget (budget is spent tokens, and they were spent) but the retry is allowed to start even when that budget is exhausted by the failed cheap run, once.

### 7.2 History

A ledger projection (`views.db`, rebuilt by `sindri views rebuild`) `view_model_history`, one row per (recipe id and version, state, model): `runs`, `refused`, `check_failed`, `redraft_caused`, `last_run_at`. It is built from `model.picked`, `launch.started`, the step's completion or failure events and the loop's transitions. "Redraft caused" counts a run whose step completed but whose check sent the instance back to the draft state (the `gaps` transition `check -> draft` in the scoping recipe). It is a projection, so it can be dropped and rebuilt, and nothing reads the ledger rows around it.

`ModelPickInput.history` is that row for each candidate. Only the last 20 runs per (state, model) count, so an old failure fades.

## 8. Recipe and profile keys

**Recipe: `floor`.** An active state may say `model: { floor: strong }`. A floor is a tier, not a model id, so a recipe doesn't name a model that disappears: `cheap`, `standard`, `strong` (the tier hint of §3, three bands: the bottom third of the agent's ordered list, the middle, the top). No `floor` means no floor. The draft state of the scoping recipe gets `floor: strong`; `questions`, `apply` and `challenge-log` get none; `challenge` gets `floor: standard`. These values are the starting point and the owner can move them. Adding a key to a recipe changes its content, so each recipe that gains one bumps its version (`RCP-VERSION-001`, `docs/csharp/sindri-workflow.md`; the CI check `scripts/ci/recipe-versions.sh`), and the recipe's eval cases are re-run (§12).

**Profile** (`workflow.agent`, today `target` only, `docs/csharp/sindri-profile.md` line 58). New keys, each a re-approval like the existing ones:

| Key | Type | Default | Meaning |
|---|---|---|---|
| `workflow.agent.models` | object: agent id to `{ list: string[], default: string }` | none | Overrides discovery (§3): the models of that agent in cheapest-first order. For Claude Code this is where a new alias goes |
| `workflow.agent.pin` | object: agent id or `agent/stateId` to a model id | none | Every step of that agent (or that state) runs on that model; no Jev call |
| `workflow.agent.cap` | object: agent id to a model id | none | No pick above this model |
| `workflow.agent.pick` | `on` or `off` | `on` | `off` skips Jev: every step gets its floor model, else the default (the rules fallback, always). The owner's kill switch |

A model id takes the id pattern only, never a catalog check, like the existing agent keys (`docs/csharp/sindri-profile.md` line 236). `profile approve` lists the new paths that changed. `scripts/check-profile-doc.mjs` needs every nested key as its own backticked name in the doc (see the code index spec CI-0b for the same trap).

## 9. How #1008 folds in

#1008 (branch `bugfix/gh-1008`) adds `ChallengerPick` (`src/Sindri/Aw.Sindri.Workflows/ChallengerPick.cs` on that branch, constructed over `IAgentCatalog`, `ILaunchViews` and `IApprovedProfile`), a `ChallengerAsk`, and a per-step agent in recipes with `differentFrom`. This spec ships after it merges (M8) and changes it in three ways:

1. The per-step choice becomes a pair, (agent, model). `AgentStepTarget` gets the picked model, and `AgentTargetResolver` keeps resolving the agent only.
2. `ChallengerPick` becomes constraint 3 in §4.1: it supplies "the drafter's agent and model" (read from `launch.started`'s `Target` and the new `Model`), and `ModelCandidates` removes that model when the challenger's agent is the drafter's. When #1008 already put the challenger on a different agent, the model constraint is satisfied by construction and is not applied.
3. SCP-CHAL-002 then compares model as well as agent. Today it only warns, because "no run records are kept" for the model; with `launch.started.Model` the check can pass or fail on evidence.

Until #1008 merges, this spec's Phase M-1 to M-5 don't depend on it (they can be built with a fake per-step target); only M-6 does.

## 10. Sharing the question with #1172

The concept scan (`ScanConceptsService`, `src/Context/Aw.Context.Features/ScanConcepts/ScanConceptsService.cs:78`) calls `IModelPort.Ask(new ModelRequest(...))`; `ModelRequest` (`src/Context/Aw.Context.Core/Models/ModelRequest.cs`) has no model field and `JudgeModelPort` (`src/Sindri/Aw.Sindri.Host/Models/JudgeModelPort.cs`) walks the agent CLIs, each answering on its default model. #1172 wants a size-based pick there.

This spec gives #1172 two pieces and leaves its call site to it:

- `ModelRequest` gets an optional `Model` (string, null means the CLI's own default). `JudgeModelPort` passes it to the agent CLI provider as the CLI's model flag; a provider that can't take it falls back to its default and logs that (#1172's own wording).
- The pick question: #1172 can call `model-pick` with `kind: concept-scan`, its map summary as `files` and its own candidates. Same objective (M2) and same chain (M5) apply to it.

`JudgeModelPort`'s doc comment ("never to the remote 'jev' API, because the prompt names files of the owner's repo") is the rule the owner relaxed in q123 (2026-10-26), for the pick question only. Phase M-2 rewrites that comment and the matching text in `docs/csharp/judge.md`.

Note the chain difference: `JudgeModelPort`'s own calls (the concept scan's actual answers) still go to the agent CLIs, never to Jev. Only the pick question goes to Jev. The pick is a classification of which model to use. The scan's text is not sent to Jev.

## 11. Privacy

Jev (`POST https://api.typesafe.ai/v1/systemone`, `docs/csharp/judge.md` "Jev") is a third party. The owner accepted (q123, 2026-10-26) that a step's brief and input pack go to it. What limits that:

- redaction on every string (`Aw.Redaction`) before it leaves the box, same as every judge input;
- the 100,000-character cap per pick, and `index.denyPaths` files never in the pack;
- the untrusted-text fence on all repo text;
- the pick runs only when `workflow.agent.pick` is `on` (default) and `TYPESAFE_API_KEY` is available; without a key Jev is unavailable and the rules fallback runs, with no network call;
- the event records which files were omitted, not their content; `model.picked` carries no prompt text. The decision's redacted input is kept 30 days like any judge decision (`docs/csharp/judge.md` "Retention").

A repo that must never send content to a third party sets `workflow.agent.pick: off`.

## 12. Errors and tests

### Errors

| Code | When | Kind |
|---|---|---|
| `SND-MODEL-001` | A profile model key (`models`, `pin`, `cap`) is malformed, or a `pin` or `cap` names a model outside that agent's `models` list when the list is given | Invalid |
| `SND-MODEL-002` | A recipe `floor` isn't `cheap`, `standard` or `strong` | Invalid (at `recipe validate`) |
| `SND-MODEL-003` | No candidate is left after the constraints (§4.1); the step parks with the reason named (the floor, the cap, the drafter's model) | Refused |
| `SND-MODEL-004` | The agent's model list couldn't be read at the probe (informational, on `launch.probed`, never fails a launch) | Informational |
| `AW-JUDGE-011` | `model-pick` input is invalid (no candidates, more than 32, a duplicate key) | Invalid |

(The judge's error numbers `AW-JUDGE-005`, `007` and `010` exist; `011` is the next free in `docs/csharp/judge.md`. `SND-MODEL-*` is a new range; the builder confirms nothing else uses it.)

### Test plan

- **Constraints** (unit, pure): a table of cases for `ModelCandidates.For`: floor removes the cheap ones; cap removes the strong ones; pin leaves one; challenger removes the drafter's model; the four together leaving none is SND-MODEL-003; an unknown model ordered last is never "cheaper".
- **Discovery** (unit, with a fake process runner): the Codex JSON fixture (the six slugs, only `visibility: list` kept), a timeout, malformed JSON and a missing binary each leave `Models` empty with `no-model-list`; Claude's list comes from the catalog entry and the profile override.
- **The question** (judge unit lane): `ModelPickQuestion` input validation; the prompt carries the objective line, the fenced files, the omitted-files line when over 100,000 (a fixture of 130,000 characters with `src/` first: the `src/` files are kept, the tail omitted and named); the answer maps `m2` to the second candidate; an unknown key is errored. Contract tests run the same cases against a fake Jev and the rules provider.
- **The chain** (judge): with no key, the rules fallback decides and no agent CLI provider is called (a fake agent CLI that records calls asserts zero); a user `config.json` that lists an agent CLI for `model-pick` is ignored.
- **The pick** (Sindri engine tests, `Aw.Sindri.Engine.Tests`): the launch activity with a fake Jev picks `haiku` for `questions`; the request that reaches the fake launcher carries `--model haiku`; a second call of the activity with the same launch id reads the stored `model.picked` and does not call the fake Jev (M7).
- **Escalation**: a fake launcher whose first run is refused; the second run is on the next model up, `escalatedFrom` is set, the loop counter is unchanged (M3's default), and a second failure fails the step.
- **History projection**: events in, `view_model_history` rows out; `views rebuild` gives the same rows.
- **Recipe eval**: a case file for `workflow.scoping` where `questions` and `apply` launch on a cheaper model than `draft`, a failed cheap run escalates once and the walk continues, and the challenger's model differs from the drafter's (`recipes/sindri/evals/workflow.scoping/`, beside `challenge-spent-same-model-stops.jsonl`).
- **Profile and recipe**: `profile validate` accepts the new keys and rejects a bad one with its key path; `recipe validate` rejects a bad `floor`; the profile doc checker passes.
- **Aot-smoke**: no doctor check changes, so `src/Sindri/aot-smoke/expected.txt` stays; if a phase does add one, it updates that file and the gate runs the smoke (owner's rule 2026-10-26).
- **Gates**: phases that add event types, catalog rows or API goldens run `Aw.Sindri.Ui.Tests` in their own gate (owner's rule).

## 13. Size

The numbers are rough and mean the lines of code and tests together.

| Phase | Rough size |
|---|---|
| M-1 | small: about 300 lines |
| M-2 | medium: about 500 lines |
| M-3 | medium: about 600 lines |
| M-4 | medium: about 500 lines |
| M-5 | medium: about 700 lines |
| M-6 | small: about 300 lines |
| M-7 | small to medium: about 400 lines |
| M-8 | small: docs and the scoping recipe's keys, about 200 lines |

## 14. Phases and the parallelization plan (for /specToProvenPR)

Each phase is one PR, layered commits, red first (`/new-slice`: test, request and response, service stub, then the real thing). Builders run on Sonnet or Haiku, never Opus; builders do one light build of the project they touched; the lane lead runs the gates (owner's standing rules).

| Phase | Unit | Depends on | Observable signal |
|---|---|---|---|
| **M-1** | Pure Core: `ModelFacts`, `ModelTier`, `ModelCandidates.For` and its table of cases, `ModelPicked` and the `LaunchStarted.Model` field, the profile keys' records and validation (`SND-MODEL-001`), `docs/csharp/sindri-profile.md` lines, the recipe `floor` key and `SND-MODEL-002` in the recipe validator | none | unit-lane tables; `sindri profile validate` accepts and rejects the new keys; `sindri recipe validate` rejects a bad `floor` |
| **M-2** | The judge question: `ModelPickInput`, `ModelPickQuestion` (chain pinned to Jev then rules, threshold, budget), the files-shaped cap helper beside `CappedDiff`, Jev's input cap made per question (100,000 for `model-pick`, §4.3) and the one real-size call (O5), `AW-JUDGE-011`, `JudgeModelPort`'s comment and `docs/csharp/judge.md`, `ModelRequest.Model` and `JudgeModelPort` passing it (the #1172 piece) | none | judge unit and contract tests; `judge ask model-pick` on a fixture prints a key |
| **M-3** | Discovery: `TargetFacts.Models`, the Codex reader (`codex debug models`), the Claude catalog entry, the tier hint, `SND-MODEL-004`, `launch.probed` carrying them | M-1 | `sindri launch show` (or `launch probe --json`) lists each agent's models on this box |
| **M-4** | History: `view_model_history` projection, its rebuild, a read for the pick | M-1 | projection tests; `sindri views rebuild` reproduces the rows |
| **M-5** | The pick in the launch activity: the Sindri-side caller of `judge ask model-pick`, the `model.picked` event and its key, the idempotent read-back, `RequestFor` and `EvaluatorRequestFor` taking the model, the rules fallback, the escalation retry | M-1, M-2, M-3 (M-4 for history; a stub that returns no history lets M-5 start before M-4 merges) | **In the running app:** a scoping run's launch events carry a model; `questions` and `apply` below `draft`'s; an induced refusal escalates once |
| **M-6** | #1008 fold-in: `ChallengerPick` supplies constraint 3, the per-step (agent, model) pair, SCP-CHAL-002 compares the recorded models | M-5 and #1008 merged | **In the running app:** SCP-CHAL-002 passes on recorded models in a scoping packet |
| **M-7** | Words and views: tokens and cost per step on the Home and `sindri jobs show`, the pick's reason in the launch row, the settings text for `workflow.agent.pick`; its gate runs `Aw.Sindri.Ui.Tests` | M-5 | **In the running app:** per-step model, tokens and cost shown |
| **M-8** | Recipe keys and proof: `floor` on `workflow.scoping.yaml` (version bump) and the eval cases, the other agent recipes' floors, docs (`docs/csharp/sindri-workflow.md`, `docs/csharp/sindri.md` error table), an `owner-check` step | M-5, M-6 | the Done-when run below |

### 14.1 Parallelization plan

Four lanes, builders always busy.

| Wave | Lane A | Lane B | Lane C | Lane D |
|---|---|---|---|---|
| 0 | **M-1** Core records, constraints, keys | **M-2** judge question and cap helper | (waits one step for M-1's records, or starts the Codex reader against a local copy of the type) | free: reviews, the Cursor investigation (O2) |
| 1 | **M-5** the pick in the launch activity (starts on M-1 and M-2 when they are open, on a fake Jev and a stub history) | **M-3** discovery | **M-4** history projection | free |
| 2 | **M-6** the #1008 fold-in (needs #1008 merged) | **M-7** words and views (stacks on M-5) | **M-8** recipe keys and proof (stacks on M-5; the eval cases need M-6's challenger rule) | free |

What can run in parallel: M-1 and M-2 touch different projects (Sindri Core and the judge) and share nothing, so they start together. M-3 and M-4 need only M-1's types and are independent of each other and of M-2. M-7 and M-8's docs parts are independent of each other once M-5's event shape is fixed.

What is sequential: M-5 needs the types (M-1), the question (M-2) and discovery (M-3) to merge; M-6 needs M-5 and #1008; M-8's eval cases need M-6 because one case checks the challenger. No stack is deeper than three open PRs.

If #1008 is late, M-1 to M-5, M-7 and most of M-8 still ship, and the challenger constraint lands with M-6.

## 15. Done when

From the issue: a scoping run shows per-step model picks on its launch events, with at least `questions` and `apply` on a cheaper model than `draft`, the challenger on a different model from the drafter, and per-step tokens and cost visible from Sindri's own records.

Concretely, on a scratch home (the proof is in the PR body):
1. `sindri launch show` lists the models each installed agent offers.
2. A scoping run on a fixture item ends with `launch.started` rows whose `model` is set; the `questions` and `apply` rows name a cheaper model than `draft`'s.
3. The `challenge` row's model differs from `draft`'s and SCP-CHAL-002 passes on that evidence.
4. An induced refusal on a cheap model shows one `model.picked` with `escalatedFrom`, and the run goes on.
5. With no `TYPESAFE_API_KEY`, the same run picks by the rules fallback, calls no agent CLI for the pick, and says why on `model.picked`.
6. `workflow.agent.pick: off` makes every step use its floor or the default with no Jev call.

## 16. Open questions for the owner

1. **Does the one escalation retry count against the recipe's redraft budget? (M3, the one open decision.)** This spec says no. If a cheap model fails `questions` and the retry on the next model up uses a round, a bad cheap pick costs the recipe a round of its cap (`interview` is capped at 6, `challenge` at 3), and a recipe near its cap would fail after a pick the owner never made. The cost of "no": a step that keeps failing on every model gets one extra run per round. Yes or no?

Open items that are not the owner's call, to be settled by the builder:

- **O2: Cursor.** `cursor-agent` isn't on this box, so its model flag and any list verb are unverified (the catalog says "reserved until it is verified"). Until a box with Cursor confirms both, Cursor offers no models to the pick and its steps run on its default. Lane D's free slot in wave 0 is for this.
- **O3: the Claude list.** Claude Code has no list command, so the aliases live in a catalog file. If a model with an id that isn't an alias should be offered before the file is updated, the profile's `models` list is the way.
- **O5: Jev's request size.** `JevProvider` caps at 16,000 today. Whether `api.typesafe.ai` accepts a 100,000-character state is unverified. M-2 measures it with one real call and, if lower, amends §4.3 with the number.
- **O4: one claim to check at build.** The spec says Temporal doesn't re-run a completed activity on replay and that an activity retry re-runs `AgentLaunching.Launch`. Both are standard Temporal behavior and the existing code comment says the launcher is "idempotent per launch id, so a retry or a replay starts no second session", but this spec's author didn't run it. M-5's test for M7 is what proves it.
