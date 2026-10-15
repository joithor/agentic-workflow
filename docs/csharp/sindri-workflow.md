# Sindri workflow engine (C#) — recipes and blocks

A **recipe** is a YAML state machine that Sindri's workflow engine runs for an item; a **block** is the C# code a
recipe names (a gate, a Step, an effect, an evidence provider or a Step input). This page is the reference for writing
both. The design and its reasons are in the engine spec,
`docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (§2 blocks, §3 the schema, §9 the validator); where
this page summarizes, the spec wins. The app shell (the `sindri` binary, its home, the error catalogue) is in
`docs/csharp/sindri.md`.

This page covers reading, normalizing, hashing and validating recipes in-process (P1) and the pure engine's eval verb,
`recipe eval` (P3). Registering and approving recipes (P2) and running instances on Temporal (the recipe interpreter,
desk Steps and the bridge, P4; engine spec §0 and §5, revision 8) come in later phases.

## `sindri recipe validate`

```
sindri recipe validate <file> [--json]
```

Local: it runs in the calling process with this build's block catalog, and never starts or contacts `sindri serve`. It
reads the file and its child recipes, and writes nothing.

- **Plain output:** one line per finding, then `hash sha256:<64 hex>` when the recipe bound (it parsed and its fields
  have the right types). The hash is printed in full, not as §3.1's 12-character display, because it is the value a
  `hash:` field takes. Errors come first, as `RCP-…: <what> at <file>:<line>:<col> (<path>); <fix>`. Information and
  skipped findings follow, with `information: ` or `skipped: ` in front of the same line, so a line that starts with
  `RCP-` is always an error. Output is ASCII as long as the recipe's own names are.
- **`--json`:** one JSON object on one line: `passed`, `hash` (null when the recipe didn't bind), `findings` (in the
  plain order; each has `code`, `severity` (`error`, `information` or `skipped`), `message`, `file`, `line`, `column`,
  `path` and `fix`) and `normalized`. `normalized` is the normalized long form without `hash`; SHA-256 over its RFC 8785
  canonical JSON is `hash`.
- **Child recipes** resolve from the recipe file's directory (`<child id>.yaml`), so `recipes/sindri/workflow.item.yaml`
  finds `workflow.step.yaml`. Once the registry exists (P2), a served validation resolves them from the registered recipes.
- **Hand-written only:** `recipe validate` passes no base recipe, so it checks every recipe as hand-written: a `base`
  field is `RCP-CAP-004`, and the generated-recipe rules (`RCP-CAP-001`, `-002`, `-005`, `-006`) don't run. Those will run
  when Sindri's generator checks a generated recipe against its base (the packet freeze, W9); no command runs them in this build.
- **The profile:** there is no effect allowlist in the profile yet (`workflow.effects`, P6), so `RCP-EFFECT-001` is
  reported as `skipped`.

| Exit | When |
|---|---|
| 0 | No finding is an error (information and skipped findings don't count) |
| 1 | One or more errors |
| 2 | A usage error (no file: `SND-CLI-003`; an unknown `recipe` verb: `SND-CLI-001`), or a tool error: the file doesn't exist, is a directory or can't be read (`SND-RECIPE-010 <path> doesn't exist`, `… is a directory, not a recipe file` or `… can't be read: <reason>`, each with its fix), or a bug inside `sindri` (`SND-API-005 recipe validate failed inside sindri: …`) |

Errors go to stderr as `CODE message (fix: …)`, or `CODE message (usage: …)` for a usage error, like every `sindri` verb.


```
$ sindri recipe validate tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/RCP-REACH-001.yaml
RCP-REACH-001: state 'orphan' is unreachable from 'produce' at RCP-REACH-001.yaml:14:3 (states.orphan); add a transition into it or remove it
hash sha256:a8c2b5e8be5a2d5877545ffbb7d8a3f6d82cd2b01254f2825e5b323d62e99f3c
$ echo $?
1
```

The golden files under `tests/Aw.Sindri.Tests/Workflow/Cli/Golden/` pin this output. After an intended change, re-pin
them with `AW_GOLDEN_UPDATE=1 dotnet test --project tests/Aw.Sindri.Tests --filter-namespace Aw.Sindri.Tests.Workflow.Cli`
(the run rewrites them and fails), review the diff, then run again without the variable.

## recipe eval

`sindri recipe eval <file> [--json]` runs a recipe's eval cases (spec §9.2) in this process, never contacting the
control plane. It reads the recipe and its child recipes beside it, then folds the pure engine's decisions (P3) over
each case listed in `evals:`, in order, with fake gates; the decisions read no clock. From P4 the same cases also run
through the interpreter on Temporal's time-skipping test server, and the two must agree (spec Phase 3). Each case's path is relative to the recipe
and stays inside its directory (`evals/<recipe id>/<case>.jsonl`).

A case file is JSON lines, LF or CRLF:
- line 1: `{"start":{"inputs":{"<fact>":"<value>",...}}}` (`"scaffold": true` marks a generated case);
- each later line: `{"trigger":{"kind","outcome"?,"outputs"?,"park"?,"variant"?,"amount"?,"reason"?},"gates"?:{"<block>":"pass|fail|uncertain"},"expect":{"state","park"?,"outcome"?}}`.

`kind` is a recipe trigger (`step.completed`, `step.failed`, `wait.satisfied`, `command.<verb>`), a child's change
(`child.parked` with `park`, `child.resumed`, `child.escalated`; a child's end is `wait.satisfied` with its outcome),
or an engine trigger: `answer` with a `variant` (`grant`, `grant-tokens`, `accept`, `reject`, `resume`, `text`; the
grants take `amount`, a `text` answer's text is `outputs` `fact:answer`), `takeover`, `handback`, `cancel` and
`deadline`. An answer wait is satisfied by an `answer` line with `variant` `text` (outcome `answered`), not by
`wait.satisfied`. A `deadline` line fails until timers land (P5).

A `step.failed` line takes an optional `reason`, what the Step reported as its failure. Left out, it is the constant
`the Step failed`, the same on every line, so two `step.failed` lines in a loop are the same failure and park
`no-progress` when the loop names `sameFailure` (spec §3.7). Give different reasons to make a round count as progress.
`reason` on any other kind fails its line. `--record` writes each failure's reason from the event, so a recorded
`sameFailure` park replays. `reason` isn't in the spec's §9.2 field list yet; the owner has been asked to add it there,
and this page documents it in the meantime.

Output, one line per case:

```
$ sindri recipe eval recipes/sindri/workflow.step.yaml
happy pass
no-progress pass
rounds pass
$ sindri recipe eval <copy with line 3 of happy.jsonl expecting checks>
happy fail at evals/workflow.step/happy.jsonl:3: expected checks, got verify
no-progress pass
rounds pass
```

A line the engine rejects (an answer that doesn't fit the park, a grant of 0 or above the loop's cap, an undeclared
outcome) or one out of the format (not JSON, an unknown `kind`, a `variant` that doesn't go with its kind) fails its case
at that line, saying why. A recipe without `evals:` prints `no evals declared` and exits 0: nothing failed. Approving
such a recipe is refused (`SND-RECIPE-008`, see `sindri recipe approve` below). `--json` prints `{"cases":[{"case","passed","line","detail"}],"scaffold":null,"passed"}`, where `"case"` is the eval file's path as `evals:` lists it (`evals/workflow.step/happy.jsonl`); the text output shows the bare name (`happy`).
The same eval run twice prints the same output.

`--scaffold <file>` prints a happy-path case for the recipe on stdout, marked `"scaffold": true` on line 1: the start
with a placeholder for each input, then, from the initial state, the first of each state's declared outcomes whose
transition moves into a state not yet visited, until a terminal state. Every gate passes, and each line expects what the
engine did, so it passes once it is in the recipe's `evals:`. `--record <address> --out <file>` writes a live instance's
history in the same format; it needs the server's instances (P4) and exits 2 until then.

| Exit | When |
|---|---|
| 0 | Every case passes, or the recipe declares none |
| 1 | A case fails (`<case> fail at <file>:<line>: <detail>`) |
| 2 | A usage error (no file, `--record` without an address and `--out`: `SND-CLI-003`; an unknown argument: `SND-CLI-001`; `--record` itself until P4: `SND-CLI-001 … needs the server (P4)`), or a tool error: the recipe can't be read (`SND-RECIPE-010`) or doesn't bind (`SND-RECIPE-001`, fix: `sindri recipe validate`), a listed eval file is missing, unreadable or outside the recipe's directory (`SND-RECIPE-008`), or a bug inside `sindri` (`SND-API-005 recipe eval failed inside sindri: …`) |

## `sindri recipe register`, `show` and `list`

`sindri recipe register <file>` (`desk`; starts the control plane if it may) validates the recipe on the server with the
registry's view (`RCP-VERSION-001/002`, and `RCP-CAP-003` for a generated recipe's base) and stores its normalized long
form under its hash: `registered <id>@<hash12> (unapproved)`, or `already registered …` for a hash it has (one
`recipe.registered` event per hash). Other content under a registered id and version is `SND-RECIPE-007`; any other
failing rule is `SND-RECIPE-001`, each followed by its rule lines.

`sindri recipe show <id@hash>` (a query; the hash by 8 or more digits) prints the recipe's line, then `urgency: <urgency>`, one
`input: <key> type=<type> kind=<kind|->` line per input and one `output: <key> type=<type> from=<state>` line per output, then the approval preview;
an unknown hash is `SND-RECIPE-002`. `sindri recipe list [--stale] [--json]` prints one line per registered recipe,
`<id>@<hash12> <standing> (<via>)[; stale: …]`, where standing is `approved`, `unapproved` or `revoked` and stale names
`pin-drift` (a pinned block's descriptor changed in this build) or `child-stale` (a pinned child was revoked);
`--stale` keeps only the approved ones that are stale. `--json` prints the server's body,
`{"recipes":[{id, version, hash, standing, via, stale, inputs, urgency, outputs}]}`, where an input is `{key, label, type, kind}`
(type and kind `null` when not declared) and an output is `{key, label, type, from}`. A row registered before `urgency`, `outputs` and
`inputs[].kind` existed keeps its stored text and hash and lists with the defaults (`work`, no outputs, no kinds); registering the same
file again hashes differently and is `SND-RECIPE-007` naming `RCP-VERSION-001` until the author raises `version`. The queries never start the control plane: when it
isn't running they print `SND-API-001 not running: …` and exit 1.

## `sindri recipe approve` and `sindri recipe revoke`

Both are `protected` (engine spec §4.2, §6.2, §6.3): only a human at a terminal gets past the command chain; a session
or a CLI without a terminal gets `SND-AUTH-002` naming the agent variable or process it saw, and nothing is recorded but
the audit row.

`sindri recipe approve <file|id@hash>`:

1. With a file, the CLI validates it in-process (`SND-RECIPE-001` if it fails) and checks every eval file its `evals:`
   names is present beside it (`SND-RECIPE-008` otherwise; P4 runs them at approve). It registers the file if the server
   doesn't know it yet.
2. It asks `POST /sindri/workflow/challenges` for `approve <id> <version>` and prints the preview: id, version, hash and
   base; the diff against the last approved version of the id (`nothing` for a first approval); the capability uses
   (block, `from -> to`, capabilities and capability parameters); the effect kinds and their allowlist status; the pinned
   blocks with their descriptor hashes and the pinned children; the eval files. A re-approval of an approved hash also
   shows the blocks whose descriptor changed and the drift-parked instances it resumes.
3. You type the action text, `approve workflow.step 1`. With a file, the CLI re-reads it first and refuses with
   `SND-RECIPE-009` if it changed while you read.
4. It prints `approved workflow.step@<hash12>`.

The server refuses before any challenge when the hash is revoked (`SND-RECIPE-005`), the recipe declares no evals
(`SND-RECIPE-008`), a child it starts has no approved hash to pin (`SND-RECIPE-003`), or it is already approved with the
same pins (`SND-CLI-003`). A wrong typed text, a challenge for another action, an expired one (5 minutes) or one whose
subject changed (another hash, or other pins: a child approved while you read) is `SND-RECIPE-009` and records nothing.
A generated recipe whose base was revoked after it was registered is refused too (`SND-RECIPE-005`). With a file whose
id and version are registered with other content (edited without raising its version), the CLI refuses before the preview
with the register call's `SND-RECIPE-007` and its `RCP-VERSION-001` line.

`sindri recipe revoke <id@hash>` (the hash by 8 or more digits) previews the instances that will park, the takeovers it
ends, the generated recipes approved from it (revoked with it, recursively) and the parents pinning it as a child
(marked `child-stale`), asks for `revoke <id>@<hash12>`, and prints `revoked …; N instances parked` (N is 0 until P4's
wake reaches instances). Revoking an already revoked hash is `SND-RECIPE-005`, unless a revoke stopped partway through
its cascade: then the same command finishes it (each step is keyed by its hash, so nothing is recorded twice).

Each approval appends `recipe.approved` (keyed `recipe.approved:<hash>:after:<seq>`) and an approval row keyed by
(id, version, hash) with its pins; each revoked hash appends `recipe.revoked` (keyed `recipe.revoked:<hash>`) and its row,
and is sent to `IRecipeWake`. Challenges live in the server process and die on restart.

## Recipe reference (schema 1)

JSON Schemas: `schemas/recipes/recipe.schema.json` is the long form, generated from the C# records by
`RecipeSchemaTests`. `schemas/recipes/recipe.editor.schema.json` also accepts the short forms; point an editor at it with
`# yaml-language-server: $schema=<path>/recipe.editor.schema.json`.

### Reading

- **Refused YAML** (`RCP-SCHEMA-003`): duplicate keys, anchors, aliases, merge keys, explicit tags, a file over 256 KiB,
  or nesting deeper than 32.
- **Scalars** follow the YAML 1.2 core schema, so `on` stays a string.
- **All schema errors** are reported with their line and column, not just the first. When the reader's only findings
  are unknown fields, the schema rules still run on the rest of the recipe, so a misspelt field doesn't hide a missing
  `next` or an undeclared state. After any other reader finding (a wrong type, a missing or empty value, refused YAML)
  they wait, since the recipe without that node would make them report on something the author didn't write. A recipe
  with any reader finding has no hash, and the other rule groups wait until it is fixed.
- **Short forms become long forms** before typed binding, which refuses unknown fields (`RCP-SCHEMA-001`). A short form
  and its long form bind to the same record and hash the same. `tests/Aw.Sindri.Testing/Workflow/Recipes/valid/` has
  `short-forms.yaml` and the same recipe written out as `long-forms.yaml`.

| Short form | Long form |
|---|---|
| `step: step.produce` | `step: { use: step.produce, with: {}, inputs: [] }` |
| `inputs: [fact:item]` | `inputs: [{ key: fact:item, label: null, type: null }]` |
| `from: produce` | `from: [produce]` |
| `gates: [gate.budget.available]` | `gates: [{ use: gate.budget.available, threshold: null, with: {} }]` |
| `effects: [effect.notify.desk]` | `effects: [{ use: effect.notify.desk, with: {} }]` |
| `with: { strict: true }` | `with: { strict: { literal: true } }` |
| `tokens: 200k`, `2M` | `tokens: 200000`, `2000000` |
| `loops: { r: { cap: 3 } }` | `loops: { r: { cap: 3, noProgress: [findings, emptyDiff, sameFailure] } }` |

**Hash:** `sha256:` over the RFC 8785 (JCS) canonical JSON of the normalized long form without `hash`. A `hash` written in
the file is checked (`RCP-HASH-001`). Listings show the first 12 hex digits. The long form writes every absent optional
value as `null` and every absent list or map as empty (owner decision, 2026-10-11), so adding an optional field to the format re-hashes every recipe, and each
needs re-approval.

### Top level

| Field | Required | Notes |
|---|---|---|
| `schema` | yes | `1` |
| `id` | yes | `workflow.<name>` |
| `version` | yes | An integer. Same id and version with another hash, or a version not above the highest registered, is refused once the registry exists (`RCP-VERSION-001`/`-002`, P2) |
| `description` | no | |
| `base` | generated recipes only | Written by Sindri's generator; on a hand-written recipe it is `RCP-CAP-004`, and `recipe validate` treats every recipe as hand-written |
| `urgency` | no | `incident`, `triage` or `work` (default `work`; UI E-21). The app orders its Workflows strip by it; the engine never reads it. The normalizer writes `work` when it is absent, so an absent urgency and `urgency: work` hash the same. Any other value is `RCP-URG-001` |
| `inputs` | no | The facts a start supplies: `{ key, label?, type?, kind? }`. `type` is a fact type (default `text`), `kind` a subject kind; the first input is the instance's subject (UI E-22) |
| `outputs` | no | What the recipe produces for a human: `{ key, label?, type?, from }`. `from` names the state that produces the fact. Declarations only: they add no wiring (UI E-22) |
| `initial` | yes | An `active` or `wait` state (`RCP-INIT-001`) |
| `states` | yes | At least one non-terminal and one terminal state. `cancelled`, `escalated`, `parked` and `taken-over` are reserved (`RCP-STATE-001`) |
| `transitions` | yes | |
| `loops`, `budget` | no | |
| `evals` | to approve (P2) | Paths of eval files, relative to the recipe's directory (`evals/<id>/<case>.jsonl`, spec §9.2). The paths' form isn't checked yet |
| `hash` | no | Checked when present |

### Fact types and subject kinds

Fact type: `text`, `file`, `item`, `link`, `scope-map`, `packet`, `evidence`, `decision`, `payload` (`FactTypes`). It picks a viewer in
the app and nothing else. Subject kind: `item`, `linear-project`, `webhook`, `file`, `text` (`SubjectKinds`); `item`, `file` and `text`
pair with the type of the same name, `linear-project` and `webhook` with `payload`. Both are strings in the long form, so a bad value
reaches its rule (`RCP-TYPE-001` to `-003`, `RCP-OUT-001` to `-003`, `RCP-URG-001`) instead of a reader error. Those rules are
checked from P2b part B; until then a value of another spelling is read and hashed as written.

### Value grammars

| Value | Form |
|---|---|
| Fact key | `fact:<name>` or `evidence:<kind>@<base\|head>` |
| Block id | `<gate\|step\|effect\|evidence\|input>.[<area>.]<name>`, kebab-case, at most 64 characters (`step.produce`, `gate.budget.available`; spec §3.3) |
| Event kind | `<area>.<name>`, registered with `AddEvent` (this build: `claim.created`, `item.routed`, `notify.delivered`) |
| Duration | `<n>m`, `<n>h` or `<n>d`, at least `1m` (`RCP-DUR-001`) |
| Tokens | An integer, or with a `k` or `M` suffix |
| `with:` value | A literal (string, number, bool, or a list of these), `{ fact: <key> }` or `{ profile: <key> }`. Capability parameters take only literals or `{ profile }` (`RCP-CAP-007`) |

### States

Every state has `kind: active | wait | terminal`. State, loop and parameter (`with:` key) names match
`[a-z][a-z0-9-]{0,63}` (`RCP-NAME-001`).

| Field | `active` | `wait` | `terminal` |
|---|---|---|---|
| `step` | required: `{ use, with?, inputs? }`; `inputs` are `input.…` blocks run before the Step starts | — | — |
| `waitFor` | — | required | — |
| `timeout` | required: `{ after, then: park \| escalate \| retry }` | required | — |
| `loop` | optional: a declared loop's name | optional | — |
| `budget` | optional: `{ tokens }` | — | — |
| `next` | required | required | optional |
| `outcome` | — | — | required: `resolved`, `unresolved`, `alreadySatisfied` or `failed` |

`waitFor` is one of `{ event: <kind> }`, `{ answer: { id, question } }`, `{ resource: heavy | slot }` or
`{ child: { recipe, inputs, onParked: propagate | ignore } }`. No resource has an allocator in this build, so a resource
wait is reported as information (`RCP-RES-001`); at run time it parks with `SND-WF-010` until one lands. `then: retry`
re-arms the deadline in the same frame and re-sends the entering transition's effects; it isn't allowed on a child wait
(`RCP-TIME-002`).

### Transitions

| Field | Required | Notes |
|---|---|---|
| `from` | yes | A non-terminal state, or a list of them. A name that isn't a state of the recipe is `RCP-SCHEMA-002` |
| `to` | yes | A state of the recipe; any other name is `RCP-SCHEMA-002` |
| `on` | yes | `step.completed` or `step.failed` from an active state; `wait.satisfied` from a wait state; `command.<verb>` from any non-terminal state (`RCP-TRIG-001`). Verbs match `[a-z][a-z-]{1,31}` (`RCP-TRIG-001`) and may not be one of `sindri workflow`'s own (`RCP-TRIG-002`) |
| `when` | no | Below |
| `gates` | no | `{ use, threshold?, with? }`, run in order. A judge or model gate needs a threshold above 0 and at most 1; a deterministic gate takes none (`RCP-THRESH-001`/`-002`) |
| `evidence` | no | `{ kind, at, expect: pass \| fail }` |
| `effects` | no | `{ use, with? }` |
| `produces` | no | Facts the transition records; each must be an output of the trigger's source (`RCP-WIRE-002`) |

The first transition whose `when` holds is the one taken. If its gates refuse, that is a refusal, not a fall-through to
the next transition.

### `when`

A closed set: `{ outcome }`, `{ fact, is: present | absent }`, `{ loop, below: cap }`, `{ all: [...] }`, `{ any: [...] }`
and `{ not: ... }`. Outcomes come from the trigger's source: the Step's declared outcomes, the event's, or a child's
terminal outcomes plus `cancelled` (`RCP-WHEN-001`). A loop must be declared (`RCP-WHEN-002`).

### Loops, budgets and `next`

- **Loops:** `loops: { <name>: { cap ≥ 1, noProgress } }`. `noProgress` defaults to `[findings, emptyDiff, sameFailure]`
  and can't be empty (`RCP-LOOP-003`). A looped state needs a transition back into it (`RCP-LOOP-002`).
  **Until P8 (spec §3.7, owner decision 2026-10-11):** the engine sees a round's `fact:findings` only as a reference
  and its SHA-256, and a Step report carries no diff, so P3–P7 detect no progress only from identical findings (the
  digest of the whole findings value; a superset, keyed by file, check and normalized message, isn't detected) and a
  repeated failure reason (`sameFailure`: a `step.failed`, or a gate refusal, repeats its reason). `emptyDiff` never
  fires, so a loop that names only `emptyDiff` never parks early, and the validator doesn't say so. The agent runtime
  (P8) adds finding keys and a diff signal to Step reports, and the full A §17.23.13 rule lands there.
- **Budgets:** `budget: { tokens }` on an active state and on the recipe. Exhausting one parks the instance with `budget`.
- **`next`** tells whoever reads the status what happens or what to do. Its variables are `{item}`, `{instance}`,
  `{state}`, `{user}`, `{child}`, `{loop.<name>}`, `{loop.<name>.cap}` and `{budget.tokens.left}` (`RCP-NEXT-001`).
  It never tells the reader to run an approval-tier command such as `--grant` (`RCP-NEXT-002`). Waits are descriptions
  ("Waiting for …"); states a human acts in are instructions.

### Engine-owned states

The engine adds `parked`, `escalated`, `taken-over` and `cancelled` to every recipe, with their own `next` texts and ways
out. A recipe can't declare them. In short: loop caps, no progress, budgets, refusals, timeouts, revoked or drifted
recipes and missing runtimes park; a top-rung gate below its threshold, an `escalate` timeout or an escalated child
escalates; `takeover` hands the instance to a human until `handback`; `cancel` ends it. Spec §3.8 has every row with its
`next` text and resume action.

## Rules

Every rule `recipe validate` runs has an invalid fixture, `tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/<RULE-ID>.yaml`,
and a test that pins its exact line (the P2 rows have none yet). All are errors except where the severity says otherwise.

| Rule | Checks | Fix |
|---|---|---|
| `RCP-SCHEMA-001` | No unknown or missing field, the fields a state's kind needs and no others (a missing `timeout` is `RCP-TIME-001`'s), and one form where a value takes exactly one (`waitFor`, `with:` values, `when`) | Remove the field, or add the missing one |
| `RCP-SCHEMA-002` | Each value has the right type and form (an enum is exactly one of its names), and a transition's `from` and `to` name states of the recipe | Write the form the message names |
| `RCP-SCHEMA-003` | No refused YAML (see Reading) | Write the value out in full |
| `RCP-STATE-001` | No state uses a reserved name, and the recipe has an active or wait state and a terminal state | Rename the state |
| `RCP-NAME-001` | Every state, loop and parameter (`with:` key) name matches `[a-z][a-z0-9-]{0,63}`; each bad name is reported once, where it is written | Rename it |
| `RCP-INIT-001` | `initial` names an active or wait state | Point `initial` at one |
| `RCP-DUR-001` | Durations are `<n>m/h/d`, at least `1m` | Write a whole number and m, h or d |
| `RCP-BLOCK-001` | Every block a recipe names is registered | Name a registered block, or fix the id |
| `RCP-BLOCK-002` | A block is used where its kind fits (gates under `gates`, Steps under `step`, …) | Name a block of the right kind |
| `RCP-BLOCK-003` | `with` names only the block's parameters, with the right types, and every required one | Remove, retype or add the parameter |
| `RCP-EVID-001` | An evidence kind has a registered provider (a bad `at` or `expect` is `RCP-SCHEMA-002`) | Name a provided kind, or register the provider |
| `RCP-EVENT-001` | A waited-for event is registered | Wait for a registered event |
| `RCP-WIRE-001` | A block's inputs are produced on every path into its state, or are recipe inputs | Produce it on every path, or add it to `inputs` |
| `RCP-WIRE-002` | `produces` lists only what the trigger's source records | List only its outputs |
| `RCP-WHEN-001` | `when: { outcome }` names an outcome the source reports | Test one of its outcomes |
| `RCP-WHEN-002` | `when: { loop }` names a declared loop, and `when: { outcome }` isn't used on a trigger that reports no outcome | Declare the loop |
| `RCP-OUTCOME-001` | Every outcome a Step, event or child reports is handled by a transition, and terminal outcomes are `resolved`, `unresolved`, `alreadySatisfied` or `failed` | Add a transition for it, or use one of those outcomes |
| `RCP-CHILD-001` | A child recipe resolves (registered, or next to the recipe) and gets its inputs | Register it, or put `<id>.yaml` beside the recipe; pass the inputs it declares |
| `RCP-CHILD-002` | Child recipes form no cycle | Remove a child wait |
| `RCP-CHILD-003` | Children nest at most 4 deep | Flatten the chain |
| `RCP-CAP-001` | **Generated recipes only** (run against the base; `recipe validate` doesn't): a generated recipe adds no capability use its base doesn't have | Remove it, or generate from a base that has it |
| `RCP-CAP-002` | **Generated recipes only:** a generated recipe keeps its base's capability parameters | Set them to the base's values |
| `RCP-CAP-003` | The recipe's hash is approved and its pinned blocks match the build (registry, P2) | Approve it |
| `RCP-CAP-004` | A hand-written recipe has no `base` | Remove `base` |
| `RCP-CAP-005` | **Generated recipes only:** a generated recipe keeps every guard its base puts on a capability use | Restore the guard |
| `RCP-CAP-006` | **Generated recipes only:** a generated recipe raises no loop cap or budget above its base | Lower it to the base's |
| `RCP-CAP-007` | A capability parameter is a literal or `{ profile }`, never a fact | Give a literal or `{ profile: <key> }` |
| `RCP-REACH-001` | Every state is reachable from `initial` | Add a transition into it, or remove it |
| `RCP-EXIT-001` | Every non-terminal state has a path to a terminal state | Add a transition towards a terminal state |
| `RCP-TIME-001` | Every active and wait state has a timeout | Add `timeout: { after, then }` |
| `RCP-TIME-002` | A child wait doesn't `retry` | Use `park` or `escalate` |
| `RCP-LOOP-001` | A state's `loop` is declared, every loop's `cap` is at least 1, and every cycle of step and wait transitions passes through a state with a `loop` (a cycle closed by a `command.<verb>` or by the answer to an `answer` wait is a human's, and a cycle with no way out is `RCP-EXIT-001`'s) | Declare it under `loops`, or add `loop` to a state on the cycle |
| `RCP-LOOP-002` | A looped state has a transition back into it | Add it, or remove `loop` |
| `RCP-LOOP-003` | `noProgress` isn't empty | Name a signal, or leave it out |
| `RCP-TRIG-001` | `on` is a known trigger, a command verb matches the pattern, and `on` fits the `from` state's kind | Use the trigger the message lists |
| `RCP-TRIG-002` | A `command.<verb>` isn't a reserved verb | Choose another verb |
| `RCP-THRESH-001` | A judge or model gate has a threshold | Add `threshold` in (0, 1] |
| `RCP-THRESH-002` | A threshold is in (0, 1], and a deterministic gate has none | Fix or remove it |
| `RCP-NEXT-001` | `next` uses only recipe variables | Use only the listed variables |
| `RCP-NEXT-002` | `next` names no approval-tier command | Leave it out |
| `RCP-EFFECT-001` | Every effect is on the profile's effect allowlist. **Skipped** while the profile has no `workflow.effects` (P6) | Add it to `workflow.effects` |
| `RCP-RES-001` | **Information:** a resource wait has no allocator in this build | None needed; it parks with `SND-WF-010` until one lands |
| `RCP-HASH-001` | A `hash` in the file matches the content | Set it to the hash the message names, or remove it |
| `RCP-VERSION-001` | Same id and version, same hash (registry, P2) | Raise `version` |
| `RCP-VERSION-002` | `version` is above the highest registered (registry, P2) | Raise `version` |

Rows marked P2 need the registry and aren't checked by `recipe validate` yet; neither are the generated-recipes-only rows,
which need the base recipe. Each message names the node and the
concrete fix, so the table's Fix column is a summary.

## How to add a block

A block is a class with a static descriptor, registered explicitly (no assembly scanning).

1. **Write the test first** in `tests/Aw.Sindri.Tests/Workflow/` (by feature): what the block decides, for each outcome.
   Its contract tests derive from `BlockContract` in `Aw.Sindri.Testing`, which runs once per block; that base arrives
   with the engine (P3/P4), so until then test the block directly.
2. **Write the block.** Pure blocks go in `src/Sindri/Aw.Sindri.Features/Blocks/<Kind>/`, blocks that do I/O in
   `Aw.Sindri.Infrastructure/Blocks/<Kind>/`. It implements its kind's interface (`IGate`, `IStep`, `IEffectKind`,
   `IEvidenceProvider` or `IStepInput`, in `Aw.Sindri.Core.Workflow.Blocks`) and `IDescribedBlock`:

   ```csharp
   internal sealed class BudgetAvailableGate : IGate, IDescribedBlock
   {
       public static BlockDescriptor Descriptor { get; } = new(
           BlockId.From("gate.budget.available"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
           Capability.None, Parameters: [], Rung.Deterministic, DeskCapable: false);

       public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken) => …;
   }
   ```

   The descriptor is what the validator checks recipes against: the facts it needs (`Inputs`), what it records and on
   which outcomes (`Outputs`), its outcomes, its capabilities and parameters, a gate's rung, and whether a Step may run
   at the desk. A block over a lower-tier package (Judge, Evidence, Packets, BugFix) reaches it through that package's
   `Core` or CLI, under a declared `deps.yaml` edge.
3. **Register it** in `BlockRegistrations.AddRecipeBlocks` (`Aw.Sindri.Features/Blocks/`) with `AddGate<T>()`,
   `AddStep<T>()`, `AddEffect<T>()`, `AddEvidence<T>()` or `AddStepInput<T>()`; an I/O block is registered by
   `AddSindriInfrastructure`. An event a wait can wait for is `AddEvent(descriptor)`. A block whose runtime comes in a
   later phase can ship as `AddBlockDescriptor(descriptor)`, so recipes that name it validate; at run time it is
   unavailable. Add it to `ShippedBlocksTests`, which pins the catalog.
4. **Use it in a recipe** and run `sindri recipe validate` on it.
5. **A behaviour change bumps `Version`** in the descriptor, in the reviewed change that makes it. Once approvals pin
   blocks (P2), a changed descriptor parks the instances of every recipe approved against the old one until it is
   re-approved (`SND-RECIPE-006`).

## Temporal foundation

Engine rev 8 runs recipe instances on Temporal (engine spec §0, §5; README X53). Temporal is decided (owner,
2026-10-12), and **P4a, the Temporal foundation** (engine spec rev 8.1, "Phase 4a"), lands the code that measured it:
the `Temporalio` pin, `binaries.lock`, `Aw.Sindri.Workflows`, the dev server under `sindri serve` and `doctor`, the
worker, a first `RecipeWorkflow`, `workflow complete` and `workflow list`, and the ledger mirror. P4's units rebuild or
extend each of these to the spec. This section is P4a's acceptance record: each of the spec's five "Acceptance
(Temporal)" checks below with its measurement and how it was taken. The checks were written before the code and are not
tuned; the verification plan is `~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-wf-p0/verification-plan.md`.
The measurements were taken while the work was still called Phase 0, so the units (a)–(d), the "P0 scratch dir" and
the `proof/sindri-wf-p0/` paths below keep that name.

**Pins.** `Temporalio` 1.19.0 (`Directory.Packages.props`), referenced only by `src/Sindri/Aw.Sindri.Workflows`
(`AwAotCompatible=false`, temporalio/sdk-dotnet#286). `binaries.lock` pins the `temporal` CLI (1.9.1, the dev server)
and the time-skipping `temporal-test-server` (1.39.0) for darwin-arm64, darwin-x64, linux-x64 and linux-arm64.
`scripts/binaries-lock.sh fetch <name>` installs one for this platform under
`${AW_STATE_DIR:-~/.agentic-workflow}/cache/bin/<name>-<version>/` after checking its sha256;
`scripts/binaries-lock.sh check` validates the file (`scripts/tests/binaries-lock.test.sh`).

**Status:** done (2026-10-12). Units (a)–(d) merged; the prover re-proved every check on the merged branch (head `1e586964`), through `sindri serve`'s own worker; that is the branch the spec's numbers come from. After merging main (P4a), journeys 1, 3, 4, 5 and 7, the eval and timer tests and the merge gate were re-run; journeys 2 (loopback, 0600), 6 (the pause) and 9 (footprint, check 4) and the bad-query `workflow list` case were not (proof: `~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-wf-p0/evidence.md`, "After merging main (P4a)").

**On the merged head** (2026-10-12): `sindri stop` answered in 1.33 s, waiting for the worker to drain and then the dev server to exit before the release (both pids gone; the restart reopened the same `temporal.db` inode); after a `kill -9` of serve the next serve adopted the server and `complete --run 2.1` took 0.39 s wall (CLI start included); the ledger held 1 + 3 rows with 4 distinct keys after a stop, a serve and a full replay; the evals took 52 ms with the desk Update and 24 ms with the desk activity, and the `4h` timeout parked in 8 ms of wall time.

| # | Acceptance check | Measurement | Result |
|---|---|---|---|
| 1 | The server starts and stops with `sindri serve`, loopback only, its file under `$AW_SINDRI_HOME`, and survives a host sleep with the instance intact | listen addresses: all seven of the server's listeners on `127.0.0.1` (gRPC frontend, Web UI and five internal ports), none elsewhere (`lsof -nP -iTCP -sTCP:LISTEN -a -p <pid>`) · db path and owning pid: `$AW_SINDRI_HOME/temporal.db` (0600) open by the temporal pid (`lsof -p <pid>`), output in `logs/temporal.log` · pid in `control.json`: `"temporal":{"pid":…,"port":…,"uiPort":…}` beside serve's own `url`, `pid`, `epoch`; `sindri doctor` prints `ok temporal 127.0.0.1:<port> answers (pid <pid>), version 1.9.1 as pinned, namespace sindri present` · serve's start-to-`control.json`: 1.03 s with a fresh database, 0.47 s on the existing one · stop takes the server down: `sindri stop` exits 0 and the temporal pid is gone before the lease is released; a restart starts a new pid on the same `temporal.db` with the namespace and the seven search attributes (`Item`, `Recipe`, `State`, `NeedsYou`, `Repo`, `Tenant`, `Ring`) intact; `--namespace sindri` and `--search-attribute` on an existing database are no-ops, and serve also registers missing attributes through the OperatorService on every start · unclean exit: after `kill -9` of serve the server keeps running (it runs detached, in its own session, never as serve's child) and the next `sindri serve` adopts it (same pid); a server that doesn't answer within 3 s (SIGSTOPped, after `kill -9` of serve) is killed and replaced (new pid, 3.5 s to `control.json`) · sleep (simulated with `kill -STOP`/`-CONT` of serve and the server together, 2 min; a stand-in for host sleep, which isn't used on the owner's box): an open `RecipeWorkflow` instance (no worker yet, started with `NeedsYou=true`) kept its run id, history (2 events) and search attributes; `workflow list --query 'NeedsYou = true'` still found it; serve kept its lease (epoch 4) and `doctor` was green for `lease`, `api` and `temporal` 2 s after the resume. How: the built apphost (`artifacts/bin/Aw.Sindri.Host/debug/sindri`, `DOTNET_ROOT=~/.dotnet`), `AW_SINDRI_HOME=$TMPDIR/sindri-dev-p0`, `AW_SINDRI_PORT=0`, pinned `temporal` 1.9.1 (archive sha256 checked by `scripts/binaries-lock.sh fetch`; the extracted binary is `33c298d4…`); raw outputs in `p0-a/hand/` (`j123.out`, `j6.out`) in the P0 scratch dir. Tests: `TemporalDevServerTests`, `DevServerTemporalContractTests`, `DevServerTemporalTests` (integration, the real CLI), `TemporalCheckTests` and the slice tests (unit) · **Prover, merged build (journeys 1, 2, 3, 6):** doctor `ok temporal … version 1.9.1 as pinned, namespace sindri present`; seven listeners on `127.0.0.1` and none elsewhere; `temporal.db` open by the temporal pid; `sindri stop` takes it down and a restart opens the same file (same inode); the 2-min SIGSTOP stand-in (not a real sleep) left `J-6/step/1` with the same run id, history length 8, state `produce` run `1.1`, `workflow list` still finding it and its one ledger row, and doctor green 2 s after the resume | Pass |
| 2 | Async completion, timers and the kill-and-resume case work as stated; the eval cases pass on the time-skipping server in under 10 s total | `complete --run 1.1` moves `produce -> checks`: yes, `RecipeWorkflowTests` (integration, pinned dev server 1.9.1 via `ExistingPath`): run 1.1 completed through its task token, the instance is in `checks` with run 2.1, a second `complete --run 1.1` is `SND-WF-005`, the history replays clean; a run completed with no worker running moves on when a worker starts (both tests 2.4 s) (P0 (b)); the same completion through the task token moves it in the test interpreter (`SpikeEvalTests`, every eval line) · `4h` timeout wall time: **3 ms** with the desk report as the `complete` Update; **never fires** while the desk Step is an open async-completion activity (2 s asserted, see finding F1) · worker restart mid-Step, same run token after restart: **yes**, `1.1` scheduled once and completed by the new worker, next run `2.1` opened 26 ms (time-skipping, first worker uncached) / 15 ms (dev server); a real `kill -9` is journey 5 · eval cases (`happy`, `no-progress`, `rounds`) total: **0.06 s** (desk activity), **0.52 s** (desk Update), **0.54 s** with PR #180's real `TransitionDecision` (local merge, not pushed) · **Prover, merged build, through `sindri serve`'s own worker** (journeys 4, 5, 8): `sindri workflow complete J-4/step/1 --run 1.1 --outcome done --output fact:output=…`, with the frontend read from `control.json` (no `--temporal`), printed `-> checks`; the history shows `RunStep` `run:1.1:1` completed by the CLI's identity · a real `kill -9` of serve with `checks` open: the dev server kept running, the next serve adopted it, `GetInstance` still `checks` run `2.1`, and `complete --run 2.1` moved it to `verify` (activity `run:2.1:2` scheduled once) · the 19 probe tests (`Workflows/Spike/`) pass in 3.8 s on the merged build (evals 46 ms with the desk Update, 19 ms with the desk activity; 4 h timeout 3 ms wall with the Update, no skip with the activity open) | Pass, with F1: engine spec rev 8.1 §5.3 (desk Steps wait for the `complete` Update; P4 builds it) |
| 3 | The ledger holds one `workflow.transition` per transition after a replay (no duplicates) | transitions in history: **3** (+1 `workflow.started`; 4 `MarkerRecorded` local-activity markers) · ledger rows before replay: **2** (worker A, sticky cache, stopped at `verify`) · after replay: **3** `workflow.transition` + 1 `workflow.started`, no key twice (`sqlite3`), after worker B with `MaxCachedWorkflows = 0`, the SDK `WorkflowReplayer` over the fetched history, and a direct re-run of event 2 (`AlreadyRecorded`) · **Prover:** the key is now `workflow:{workflow id}:{run id}:{activity id}` (rev 8.1 §5.6; the test above re-proven with it, and a re-execution under the same activity id is `AlreadyRecorded`) · **through `sindri serve`** (journey 7): `J-4/step/1` after a `kill -9`, a `sindri stop` + `sindri serve` (empty sticky cache) and a full replay by `GetInstance`: 1 `workflow.started` + 2 `workflow.transition`, 3 distinct keys; after the next transition on the new worker, 1 + 3, 4 distinct keys (`…:2`, `…:4`, `…:6`, `…:8`), equal to the 3 `RunStep` transitions in history (`sqlite3 ledger.db`) | Pass |
| 4 | Dev server RSS ≤ 300 MB idle and ≤ 500 MB with ten instances; worker overhead inside `sindri serve` ≤ 150 MB; server ready in ≤ 5 s | **Prover re-measure (2026-10-12), macOS physical footprint** (`footprint -p <pid>`, cross-checked with `vmmap --summary` "Physical footprint" at start and end) beside `ps -o rss=`, because RSS leaves out compressed and swapped pages and the box was under memory pressure (unit (d)'s RSS also went *down* under load): the real `sindri serve` with its Temporal worker (`SindriWorkerHost`) and its dev server, 15 min at 60 s, minutes 0–5 idle (no open instance), 10 `RecipeWorkflow` instances started after minute 5, each holding an open desk activity (`temporal workflow count` = 10 to the end), Ollama running · dev server idle: footprint median 129, max 137 MB (RSS median 164, max 171) · with 10 instances: footprint median 133, max 134 MB (RSS median 181, max 182) · `sindri serve` (whole process, worker included): footprint idle median 106, max 106 MB; with 10, median 109, max 115 MB (RSS 73/76 idle, 108/111 with 10) · worker overhead, serve minus unit (d)'s no-Temporal probe host as the baseline (an upper bound: serve also carries the ledger, lease and API): footprint median 50, max 59 MB (RSS median 72, max 75 MB with 10) · memory pressure: swap used 6983 MB at the start, 6759 MB at the end (of 8192), `memory_pressure` free 59% → 63%, compressor pages 226440 → 187362 · samples: `p0-prover/footprint/samples.csv`, `pressure.txt`, `run.sh` · ready ≤ 5 s: not re-measured (unit (d): 0.66 s max headless; unit (a): serve start to `control.json` 1.03 s fresh) · **Unit (d)'s hour (RSS only, a probe worker):** dev server RSS idle: median 133, max 164 MB (11 samples, minutes 0–10) · with 10 instances: median 116, max 165 MB (50 samples, minutes 11–60; `temporal workflow count` = 10 throughout) · `sindri serve` RSS minus no-worker baseline: median 8, max 16 MB (worker process max 48 MB, baseline max 40 MB) · start-to-ready ×5: 0.66 s max headless (0.22–0.66), 1.10 s max with the web UI (0.21–1.10) · samples: 60 min at 60 s, Ollama running (`ollama serve` up in every sample, no model loaded), CSV at `p0-d/footprint/hour.csv` in the P0 scratch dir. How: pinned `temporal` 1.9.1 `server start-dev --db-filename <tmp>/temporal.db --ip 127.0.0.1 --port <free> --ui-port <free> --namespace sindri` (web UI on for the hour; ready = `temporal operator namespace describe sindri` succeeds, measured from launch, fresh db each run, `ready.csv`); RSS = `ps -o rss=` on macOS arm64. The worker is a stand-alone probe that mirrors serve's shape, not `sindri serve` itself (unit (a) owns that wiring): an ASP.NET Core slim host on 127.0.0.1:0 with a hosted service, in `worker` mode a `TemporalClient` plus a `TemporalWorker` for a workflow that waits on a signal, in `baseline` mode the same host without Temporal; both ran side by side under `dotnet` for the hour, and 10 executions were started at minute 10 | Pass: every number is under its threshold in both measures (footprint max 137 of 300 idle, 134 of 500 with 10; overhead max 59 of 150) |
| 5 | The AOT split builds with zero warnings and the host publishes framework-dependent; the Avalonia app still publishes NativeAOT | `dotnet build AgenticWorkflow.slnx -warnaserror`: 0 warnings, `IL*` outside `Aw.Sindri.Workflows`: 0 (grep of the normal-verbosity log: no `IL` codes at all; `IsAotCompatible=true` on Sindri Core, Features and Infrastructure, unset on Workflows, by `dotnet msbuild -getProperty`) · host framework-dependent publish: passes (`src/Sindri/aot-smoke/run.sh`: `dotnet publish -c Release --no-self-contained -warnaserror`, runtimeconfig names `Microsoft.AspNetCore.App`, no runtime beside it, `Temporalio.dll` and its native core bridge present, the published apphost matches `expected.txt`; output 271 MB because the portable publish carries the Temporal bridge for every RID) · Avalonia app NativeAOT publish: passes on a throwaway merge (never pushed, removed afterwards) of `feat/sindri-wf-p0-d` 5bb297ef with `origin/feat/sindri-s1b-ui-shell` 3e5eb8a2 (U0, PR #168), `AgenticWorkflow.slnx` and `Directory.Packages.props` union-resolved (Temporalio and Avalonia pins side by side), the other conflicts (docs, deps tooling, architecture tests) not touched by the app: `src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh` published `sindri-ui` NativeAOT for osx-arm64, 19 MB Mach-O, 0 warnings, no `IL` codes. The full merged solution wasn't built (those conflicts are U0's to resolve on rebase) · **Prover, merged build (2026-10-12):** `dotnet build AgenticWorkflow.slnx -warnaserror` 0 warnings, 0 errors (Debug and Release); `src/Sindri/aot-smoke/run.sh` ok (framework-dependent publish, 272 MB); the Avalonia app is still not on main (U0 open), so unit (d)'s throwaway-merge NativeAOT publish is the evidence for that clause | Pass |

### What each server supports, the evals and the ledger mirror

Tests: `tests/Aw.Sindri.Tests/Workflows/Spike/` (integration lane, about 4 s for the 19 tests, both servers started
from `binaries.lock` with `ExistingPath`; the SDK never downloads). The interpreter there (`SpikeStepWorkflow`) is
test-only, the eval harness: it walks `recipes/sindri/workflow.step.yaml` read by P1's `YamlRecipeReader`, with a decision
stub (`SpikeDecision`: first matching transition, identical findings park `no-progress`, a loop past its cap parks
`rounds`). Unit (b) built `RecipeWorkflow`, the foundation's interpreter, in parallel. The same evals also ran with PR #180's
`TransitionDecision` folded inside a Temporal workflow, on a local branch with #180 merged
(`throwaway/p0-c-real-decisions`, never pushed): all three pass, 0.54 s.

**Support.** Each row is one test that runs on both servers (`TemporalProbes`, bound by `TimeSkippingProbeTests` and
`LocalProbeTests`), or the named class.

| Feature (rev 8 §, §10) | Test | `temporal-test-server` 1.39.0 (time-skipping) | `temporal` 1.9.1 dev server |
|---|---|---|---|
| Update with a validator; a rejected Update records nothing; the same update id twice returns the first result (§5.2) | `Update_validator_rejects_and_a_repeated_update_id_applies_once` | yes | yes |
| Async activity completion through the task token, from outside the worker (§5.3) | `Async_completion_completes_the_activity_through_its_task_token` | yes | yes |
| Signals, including ones sent before the wait (§5.4) | `Signals_reach_the_workflow_and_wait_until_it_waits` | yes | yes |
| Child workflow, id `{parent}/child.1`, `ParentClosePolicy.Terminate` (§5.8) | `Child_workflow_returns_its_result_to_the_parent` | yes | yes |
| `DelayAsync` with time skipping (§5.5) | `DelayAsync_skips_time_only_on_the_time_skipping_server` | yes: 4 h in 74 ms | no skipping (by design): 1 s took 1.07 s |
| Timer fires while an activity is scheduled or open | `Four_hour_timeout_does_not_skip_while_the_desk_activity_is_open` (time-skipping) | **no**: time stays still while any activity is scheduled or open (F1) | real time only |
| Worker restart mid-Step resumes with the same run token (§10) | `Worker_restart_mid_step_resumes_with_the_same_run_token` | yes, first worker without a sticky cache | yes |
| Restart from a worker that had the instance in its sticky cache | `Worker_restart_from_a_sticky_cache_resumes_only_on_the_dev_server` | **no**: the task stays on the dead sticky queue (F2) | yes, 15 ms |
| Complete an activity by id (workflow id, activity id) | exploratory, not kept | **no**: the server fails the call (`missing method "getNamespace"`) | not tried; Sindri uses task tokens |

**Findings for P4.**
- **F1. A desk Step held open as an activity blocks its own timeout on the time-skipping server.** The Java test server
  doesn't skip time while any activity is scheduled or open, so §5.3's async-completion `RunStep` and §5.5's timer can't
  be tested together there (4 h timer still pending after 2 s; an unpolled activity blocks it too). What works, measured:
  `RunStep` records the desk task and returns at once, and the report arrives as the `complete` Update (§5.2 already
  lists `complete` as an Update) with a validator that refuses a stale run token; the 4 h timeout then parks in 3 ms of
  wall time, and the evals pass that way too (0.52 s in unit (c)'s run, 46 ms in the prover's). Also measured: an auto-skipping result wait begun while an
  activity was open didn't resume skipping after it closed (the test timed out at 30 s), so a test waits until the
  timer is in history before awaiting the result. The prover also found that an activity left open in an instance
  that was then terminated keeps time skipping off for the rest of that server's life (the shared-fixture `DelayAsync`
  probe timed out after the restart probes; it now starts a server of its own). **Owner decision (2026-10-12), engine
  spec rev 8.1 §5.3:** a desk Step is a short activity that records the open task, and the workflow waits for the
  `complete` Update, validated synchronously; agent Steps (P8) keep a heartbeating activity, and their timeout tests
  run on the dev server. The foundation's `RecipeWorkflow` keeps unit (b)'s async-completion desk activity; P4 builds the
  Update model.
- **F2. The time-skipping server never moves a task off a dead worker's sticky queue** (no fallback after 20 s with a
  1 s `StickyQueueScheduleToStartTimeout`). Restart tests there run the first worker with `MaxCachedWorkflows = 0`, or
  run on `StartLocalAsync`; the dev server resumes on a new worker in 15 ms after a graceful stop. After a real crash
  (journey 5, `kill -9` of `sindri serve` with `checks` open), the new serve's worker resumed the instance: a
  `GetInstance` query (not timed) answered first, and `complete --run 2.1` then returned `-> verify` in 0.16 s, so any
  sticky-queue wait fell on that first query. (Engine spec rev 8.1 §11.)
- **F3. Workflow code and `CA2007`.** Library code must write `ConfigureAwait(false)`, which leaves the workflow's
  deterministic scheduler; the probe workflows (`Workflows/Spike/`) live in the test project, where `CA2007` is off. Workflow code
  in `Aw.Sindri.Workflows` writes `ConfigureAwait(true)` instead (the foundation's `RecipeWorkflow` does; P4's keeps
  it). `CancellationTokenSource.CancelAsync` (`MA0042`) has the same problem; workflow code calls
  `Cancel`. (Engine spec rev 8.1 §11.)

**The ledger mirror.** `RecordEventActivities.RecordAsync` (`src/Sindri/Aw.Sindri.Workflows`) appends through S1's
`ILedgerStore` under the held lease, with the idempotency key `workflow:{workflow id}:{run id}:{activity id}` (engine
spec rev 8.1 §5.6, owner decision 2026-10-12), read inside the activity from its own `ActivityInfo`. Every activity,
local or regular, has a deterministic `ActivityId`, the same on every replay; no activity sees a history event id
(`ActivityInfo` has no scheduled-event-id field). Unit (c) first keyed it by a workflow-side counter; the prover changed
it test-first (`Replay_leaves_one_ledger_row_per_transition_keyed_by_the_activity_id`, red 98c8ec60, green 0395ca65).
The local activity ids are the SDK's activity sequence, shared with regular activities, so in the test interpreter the
four mirrored events are `1`, `3`, `5`, `7`. It is a
**local** activity, as §5.6 says: its result is a marker written with the workflow task, so a replay reads the marker
and never runs it again, and it costs no task-queue round trip or extra history events. The one case that runs it
twice, a worker that dies after the append but before the workflow task completes, is absorbed by S1's `Append`, which
looks the key up and inserts in one write transaction and returns `AlreadyRecorded`. A regular activity would give the
same at-least-once execution with three history events per ledger row, so exactly-once comes from the key either way
and the local activity is the cheaper of the two. Its retry policy is unbounded with backoff, so the workflow waits for
the ledger.

**How it was measured.** `AW_SPIKE_LOG=<file> dotnet test --project tests/Aw.Sindri.Tests -- --filter-namespace
Aw.Sindri.Tests.Workflows.Spike` appends each measurement line to the file; `AW_SPIKE_LEDGER_COPY=<dir>` keeps the
ledger replay test's `ledger.db`. Counted with `sqlite3 ledger.db "select type, count(*) from events group by type"`
(`workflow.started|1`, `workflow.transition|3`) and `select idempotency_key, count(*) c from events group by
idempotency_key having c > 1` (no rows). Wall times are `Stopwatch` around the awaited call, on the owner's Mac (arm64),
2026-10-11. The raw logs and CTRF reports are in the unit's scratch directory (`p0-c/final-spike.log`,
`p0-c/ledger-sqlite3.txt`, `p0-c/ctrf-final/`).

**Verdict: pass.** All five pass lines hold on the merged build, line 4 in both physical footprint and RSS. Temporal stays the substrate (rev 8); P4 starts from rev 8.1: desk Steps wait for the `complete` Update (F1), the ledger key is the RecordEvent activity id, and time-skipping tests follow F1–F3 (spec §11).
