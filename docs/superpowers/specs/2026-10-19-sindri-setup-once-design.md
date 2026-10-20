# Sindri setup once: the owner sets up Sindri and Onyx, and nothing else is typed (spec amendment)

**Status:** proposed, rev 1 (2026-10-19). The owner reads it before it is marked ready. It is a docs-only amendment: no
code lands until the owner approves it and answers the open questions in §13. Lanes: api (this spec), engine, context, ui.

**Decision it implements (owner, 2026-10-19, after the demo):** Sindri is set up **once**, then Onyx. Everything else
happens in the background or through the app, never through CLI commands: repo registration, the profile approval
prompt, MCP registration, map builds and recipe registration.

**Amends** (the full list, with the repo docs, is §11; each inline edit is marked **(SO-n)** in the amended text):
- the API surface spec (`2026-10-10-sindri-api-surface-design.md`): SO-1 to SO-9 (the setup route, the setup commands,
  the `setup` needs-you kind, the app as a confirmation channel for three `protected` actions, error codes, events,
  `displayName` on the recipe views);
- the workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`): SO-10 to SO-13 (`displayName`,
  registration at serve start, approve-all, `SND-RECIPE-011`);
- the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`): SO-14 (the map has no manual promote
  step; the `context-map-refresh` service promotes);
- the Onyx spec (`2026-10-16-sindri-onyx-managed-design.md`): SO-15 (the owner's Onyx login may be given in the app);
- `docs/sindri/README.md`: SO-16 (X65).

**Relation to PR #433 (CI apply spec, draft):** §10 folds it in clause by clause. This text wins where they differ, and
only for the clauses §10 marks "supersedes" or "conflicts".

## 1. Goal and the owner's rule

**The rule (owner, 2026-10-19):** the owner types two setup commands, ever: **Sindri setup** and **Onyx setup**. A thing
that is not one of those two is a background job or a button in the app. A new repo is a button. An approval is a typed
or held confirmation in the app. A map builds itself.

**What stays human.** Approvals stay human. The owner's rule removes the *terminal*, not the *person*: profile approval
and the recipe approvals keep a confirmation bound to the content's hash, now given in the app (§4). A session is still
refused `SND-AUTH-002`. Nothing in this spec lets an agent approve anything.

**Success.** A new owner runs `./setup.sh --with-sindri` and then `sindri-app onyx up`. They open the app. Its first
screen is a checklist of six steps (repo, profile, recipes, MCP, map, Onyx). They pick a repo, confirm one profile
approval and one "approve all recipes", and watch the rest finish. The second repo they add is one pick and one
confirmation.

## 2. Today: every step a new owner types

The C# Sindri app, from nothing to a working Sindri with one repo. A numbered row is one command the owner types. A
"typed" row also asks for a typed confirmation. Counts: 26 shipped recipes (`recipes/sindri/*.yaml`).

| # | Step | Exact command today | Typed confirmation |
|---|---|---|---|
| 1 | Install Sindri (builds the TS `sindri`, the deps, the Serena image) | `./setup.sh --providers claude --with-sindri` | no |
| 2 | Onyx (setup.sh runs it at the end, at the terminal): your Onyx account in the browser, then your Onyx admin login and password (prompts, not commands) | `sindri-app onyx up` (run by step 1; run by hand after `--no-onyx` or a failure) | the profile approval, inline |
| 3 | Start the control plane and open the app | `sindri-app` | no |
| 4 | Register a repo: there is no C# verb, so the owner edits `profile.yaml` (`$AW_SINDRI_HOME/profile/profile.yaml`) and adds a `repos:` entry. The TS build has `sindri repo onboard <path>` (add, approve, pre-commit hook, first index) | hand edit; or TS `sindri repo onboard .` | its profile approval |
| 5 | Look at the profile change | `sindri-app profile approve` | no |
| 6 | Approve the profile | `sindri-app profile approve <hash>` | typed: `approve profile <hash6>` |
| 7 | Register Sindri's MCP in the repo (per repo, from inside it) | `sindri-app mcp register --apply --providers all` | no |
| 8-33 | Register each shipped recipe, children first (26 commands) | `sindri-app recipe register recipes/sindri/<id>.yaml` | no |
| 34-59 | Approve each shipped recipe, children first (26 commands) | `sindri-app recipe approve <id>` | typed: `approve <recipe> <version>`, 26 times |
| 60 | Build the map | `sindri-app context map build` | no |
| 61 | Accept what is certain | `sindri-app context map review --accept-deterministic` (and `review accept\|reject …` for the rest) | no |
| 62 | Make the map current | `sindri-app context map promote` | typed: `promote map v<n>` |
| 63 | Check | `sindri-app doctor` | no |

**Count today: 63 commands and 28 typed confirmations** for the first repo (1 setup.sh, 1 Onyx, 1 start, 1 repo
registration, 2 profile, 1 MCP, 52 recipe, 3 map, 1 doctor), plus **7 commands and 2 typed confirmations for each
further repo** (registration, two profile commands, MCP, three map commands). The Onyx command is already part of
setup.sh, so by hand it is 62 for a box where setup.sh did both.

**After this spec: 2 commands, 0 typed commands.** Sindri setup `./setup.sh --with-sindri` (it ends by running Onyx
setup, §9), and Onyx setup `sindri-app onyx up` (run by setup.sh; run again by hand only after `--no-onyx` or a
failure, or replaced by the app's Set up Onyx button). In the app the owner gives **two confirmations** (profile, then
approve-all recipes) and picks the folder of each repo. Starting the app each day is `sindri-app`; that is how you open
the app, not setup, and the checklist is its first screen until every step is done.

## 3. Decisions

| # | Decision | Why |
|---|---|---|
| SO-D1 | The setup is **one visible list of six steps**, served by `GET /sindri/setup`, in the order repo, profile, recipes, mcp, map, onyx. The app draws it; nothing in it is the app's own logic | The UI is a pure client (UI spec §17.27.1); the owner sees one place that says what is done and what needs them |
| SO-D2 | Long work is a **background job** returning 202 and a job id, never a blocking POST; a step that fails or waits on the owner is also a **needs-you row**, kind `setup` | The existing job and needs-you machinery (API §3.6, P6) already gives progress, cancel and events |
| SO-D3 | The app becomes a **second confirmation channel for exactly three `protected` actions**: `ApproveProfile`, `ApproveRecipe`, `ApproveAllRecipes`. Everything else `protected` (grants, accept, revoke, map promote by hand, review-apply, merge, add, delete) stays terminal-only | The owner's rule needs the profile and the recipes in the app. Widening further is a separate decision |
| SO-D4 | The app confirms by **typing** (the profile) or by **holding** (approve-all), against a server challenge bound to the content's hash. Same challenge store, same 5 minutes, same drift refusal as the terminal | The profile widens what Sindri may do (repos, services, effects), so it is typed. Approve-all covers only recipes the installed build ships, whose hashes the build itself carries, so a deliberate hold after reading the list is enough |
| SO-D5 | Recipes are **machine-wide**. `serve` registers every shipped recipe at start, idempotently, and never approves | A shipped recipe is part of the install; registering it is not a decision |
| SO-D6 | **Approve-all** is one human confirmation applied as one approval per recipe, children first, stopping at the first refusal | Keeps `recipe.approved` per recipe, so pins, drift and revoke work unchanged |
| SO-D7 | **MCP registration is done by Sindri** for every repo of the approved profile, local scope only, never user-wide. Approving a profile that adds a repo is the consent | §6.4 says why this is safe and why it is not an agent-triggerable action |
| SO-D8 | The map has **no manual step**: the `context-map-refresh` service builds, cleans up and promotes. The protected manual promote stays | Owner rule, 2026-10-19 (source: this decision, relayed with #433): map promote has no manual step, so the service promote bypasses the protected command by design |
| SO-D9 | Onyx setup stays the **second setup command**, and the same work is a button in the app for an owner who prefers it | The owner said "Sindri once, then Onyx"; the Onyx login is the one thing only the owner can supply |
| SO-D10 | The setup step ids are fixed words (`repo`, `profile`, `recipes`, `mcp`, `map`, `onyx`) and appear in the route, the event and the needs-you id | One vocabulary across lanes |

## 4. The app as a confirmation channel (SO-D3, SO-D4)

Today a `protected` action is refused to the app (API §4.2) and the app shows the CLI to copy. This spec changes that
for three actions, and only them.

1. The app asks the server for a challenge: `POST /sindri/workflow/challenges` (`issue`) with `{action}` as today. The
   server accepts a `human` caller without a terminal **only for** `approve profile <hash6>`, `approve <recipe> <version>`
   (or `approve <recipe>@<hash>`) and `approve-all <digest6>`; any other protected action from the app is
   `SND-AUTH-002`, as today.
2. The answer carries the preview as today, plus `confirm` and, for a hold, `minHoldMs`:
   `{challenge, action, expiresAt, confirm: "typed"|"held", minHoldMs?: 1500, approve?|approveAll?}`.
3. **Typed:** the app shows the preview, then a field that asks for the action text (`approve profile 3f0c9a`). The
   answer is `{challenge, typed}`, compared as the terminal's is. **Held:** the app shows the list and a button that
   fills while pressed; the answer is `{challenge, heldMs}`. The server refuses `heldMs` under `minHoldMs`, and any
   answer less than 2 seconds after the challenge was issued, with `SND-SETUP-003` ("confirmation not given"). The
   hold is a slip guard, not proof of reading; that is the threat model (honest mistakes, not a deliberate agent).
4. Everything else is the terminal's rule: one-time, 5 minutes, bound to the subject, refused when the subject changed
   (`SND-RECIPE-009` for recipes, `SND-PROFILE-003` for a stale profile hash).
5. The terminal path stays and writes the same events; the actor tells them apart (`human:cli`, `human:ui`).

**Catalog.** `GET /commands` rows gain `confirm` (`typed`, `held` or `null`). The three rows' UI status flips from
`cli-only:typed-confirmation` to `ready` (SO-3). The architecture test that says the app and the MCP server reference no
challenge route (engine §4.2 last line) keeps its MCP half and changes its app half to "the app references only the
three allowed actions' challenges" (SO-12).

**What stops a session.** A `session` caller is refused `SND-AUTH-002` for the three actions as for any `protected`
action. The app sends `human` with the per-start token (API §6); the MCP endpoint stamps `session` whatever it is
sent. Reading the owner's token file stays out of scope for v0 (API §6, §11).

## 5. The six steps

Each step: **trigger**, **job or route**, **events**, **needs-you row on failure**, **human-only part**,
**idempotent on re-run**. `service:setup` is a new built-in service (listed in `sindri services`, paused and resumed
like any other, no profile entry, a 30-second tick). It owns the `setup` view's derivation and runs the in-process
parts below as caller `service:setup`. The grants it needs are the existing `ServiceGrants` field on the catalog rows
(`RegisterRecipe`, `RegisterMcp`), as #433 uses them for `ci-apply`.

### 5.1 Step `repo`: add a repo from the app

| | |
|---|---|
| Trigger | The owner picks a folder in the app and presses **Add repo** (`AddRepo`, `desk`). The app sends the absolute path from its folder picker |
| Route and job | `POST /sindri/setup/repos` `{path, name?}` answers **202** `JobStarted(accepted, JobId, links)`. Job kind `add-repo`, key = the resolved path. A second add of the same path while one runs is `SND-JOB-409` |
| What the job does | 1. Validates the path: it exists, is a git checkout, resolves to its **main checkout** (a linked worktree resolves as API §3.1 says), is readable, is not under `$AW_SINDRI_HOME` (`SND-SETUP-001`). 2. Refuses a repo already named in the profile or a name that collides with another repo (`SND-SETUP-002`). 3. Appends a `repos[]` entry (`name` = the folder name unless `name` is given, `path` = the real path) to the **live profile file**, keeping every other key and comment (the editor the Onyx step already uses). That change is a profile drift, so the profile waits for approval (§5.2). 4. Appends `repo.added`. Progress lines: `checking`, `added to the profile`, `waiting for you to approve the profile` |
| After approval | The `setup` service sees `profile.approved` name a repo it has not onboarded and runs, as `service:setup`: the pre-commit scrub hook (when the C# build has an installer; until then the line says `no hook installer yet` and the step still completes), then step `mcp` (§5.4). The first map is the `context-map-refresh` service's own trigger (§5.5), not a step of this job |
| Events | `repo.added {repo, commandId}` (public; the path is in the non-public payload), `job.*`, `setup.step.changed` |
| Failure | State `failed`, `error {SND-SETUP-001/002 or the profile's SND-PROFILE-001/002}`; needs-you `setup:repo` with the sentence and the action `AddRepo` to try another folder |
| Human-only part | The path is the owner's pick, so `AddRepo` is `desk`: a session is refused `SND-AUTH-002`. Approving the entry is the profile approval |
| Re-run | Adding an added path is `SND-SETUP-002` and changes nothing. Onboarding after approval is keyed by `(repo, approved hash)` and does nothing it already did |

### 5.2 Step `profile`: the approval prompt

| | |
|---|---|
| Trigger | The live profile differs from the approved one (`profile.checked drifted:true`, every 30 s), or no profile has ever been approved |
| Route | The existing `GET /profile` and the challenge of §4 (`approve profile <hash6>`, **typed**), then `POST /profile/approve` `{hash, answer}` (unchanged body) |
| Needs-you | The existing `profile:drift` row (kind `profile`); its `primaryAction` becomes `ApproveProfile`, `ready` in the app (SO-5). **There is no `setup:profile` row**: two rows for one thing would be noise |
| Preview | The changed key paths as today, plus one plain sentence for each consent the approval carries: "Sindri will register its MCP server in `<repo>` for Claude Code, Codex and Cursor" (§6.4) and, once the map step is live, "Sindri will keep `<repo>`'s map up to date on its own" |
| Events | `profile.checked`, `profile.approved` (unchanged), `setup.step.changed` |
| Failure | A stale hash is `SND-PROFILE-003` (the app re-reads and asks again); an invalid profile file is `SND-PROFILE-001/002` shown on the step as `failed` with the sentence |
| Human-only part | The whole step. The typed confirmation is bound to the live hash |
| Re-run | Approving an approved hash is a no-op (as today) |

### 5.3 Step `recipes`: registration at start, approve-all in the app (engine lane)

| | |
|---|---|
| Trigger | Registration: every `serve` start, after the control plane listens. Approval: the owner presses **Approve all** |
| Registration | `service:setup` registers each recipe of the installed build (26 today), children first, through the `RegisterRecipe` slice. Idempotent by hash: an already-registered hash is `unchanged`. It **never approves**. It appends `recipe.registration.completed {registered, unchanged}` (counts) once per start, and the `recipe.registered` event per new hash as today |
| Approve all | `GET /sindri/workflow/recipes/approve-all` is the **plan** (a query): every shipped recipe that is registered and not approved at its current hash or is stale, children first, each with its capability summary and eval result. `POST /sindri/workflow/recipes/approve-all` (`protected`, challenge `approve-all <digest6>`, **held**; digest = the plan's digest) applies it: one `ApproveRecipe` per recipe in plan order, one `recipe.approved` each (with `batch: <commandId>`). It **stops at the first refusal** and answers `{approved, refused}` |
| Drift between preview and apply | If the plan changed after the challenge was issued (a recipe re-registered, a pin moved), the POST is `SND-RECIPE-009` and nothing is applied; the app re-reads the plan |
| Only shipped recipes | A recipe whose hash is not the build's shipped hash (an owner-written or generated recipe) is never in the plan; naming one is `SND-RECIPE-011`. Those are approved one at a time (`ApproveRecipe`, typed in the app, §4) |
| Failing evals | A recipe whose evals fail refuses (`SND-RECIPE-008`), exactly as one approval does |
| `displayName` | A new **required** recipe field (SO-10): the name the app shows instead of `workflow.step`. Adding it changes every hash, so **every shipped recipe is re-approved once**, which approve-all makes one hold. Views that name a recipe carry `displayName` beside the id (SO-9) |
| Needs-you | `setup:recipes` while recipes are registered and unapproved ("26 recipes wait for your approval"), primary action `ApproveAllRecipes`; on a failed registration or a refusal, the sentence and the same action |
| Events | `recipe.registration.completed`, `recipe.registered`, `recipe.approved`, `setup.step.changed` |
| Human-only part | Approve-all. A session is refused `SND-AUTH-002`; registration is not a decision |
| Re-run | Registration of known hashes changes nothing. Approve-all on an empty plan answers `{approved: [], refused: []}` (200) and the plan's `empty` sentence says "Every recipe is approved." |
| After a Sindri update | A new build's changed recipes are registered at the next start and appear in the plan as `changed`; the step returns to `needs-you` and one more hold approves them (Q5) |

### 5.4 Step `mcp`: Sindri registers itself in each repo

Today `sindri mcp register --apply` is the human's verb, and `scripts/sindri-app.sh` only prints the command. The owner's
rule moves the doing to Sindri. The mechanism, and why it is safe:

| | |
|---|---|
| Trigger | (a) A repo appears in the **approved** profile with no registration recorded for the current port; (b) every `serve` start, for each approved repo (so a changed port is repaired); (c) the owner presses **Register MCP** (`RegisterMcp`, `desk`) |
| Route and job | (c) `POST /sindri/setup/mcp` `{repo?}` answers **202** `JobStarted`. Job kind `register-mcp`, key = repo. (a) and (b) run the same slice in-process as `service:setup` and show as the same job kind in `GET /jobs` |
| What it writes | Exactly what `mcp register --apply` writes today for the installed providers: Claude Code's **local** scope (`claude mcp add --scope local`, run in the repo's checkout), the checkout's `.cursor/mcp.json` and `.codex/config.toml`, and Codex's project-trust row. Providers are those found on the box; none found is not a failure |
| Never | User-wide scope, any repo not in the approved profile (`SND-SETUP-004`), any content other than Sindri's loopback `/sindri/mcp` URL and the `X-Sindri-Repo` header. Doctor's `context:mcp-scope` still warns on a user-wide entry (`.agents/rules/mcp-servers.md`) |
| Why a background process may do it | The inputs are fixed and come from the approved profile, so the profile approval (typed, human) is the consent and the preview says so (§5.2). The action is idempotent (provider outcome `unchanged`) and affects one repo's local config. No session can start it: `RegisterMcp` is `desk`, and the automatic runs are triggered by `profile.approved`, a human event. The server runs as the owner's OS user, so it writes only what the owner could |
| Where a human is still needed | Codex and Cursor ask for their own trust of a project the first time. Sindri cannot answer that prompt and does not try; the step is `done` with the detail "Codex will ask you to trust this folder once" |
| Needs-you | `setup:mcp` on a failed provider ("Couldn't register with Claude Code: `claude` isn't installed" with the provider's fix line), primary action `RegisterMcp` |
| Events | `mcp.registered {repo, providers: [{provider, outcome}]}` (public), `job.*`, `setup.step.changed` |
| Re-run | Unchanged providers write nothing |
| Script change | `scripts/sindri-app.sh` stops printing the register command (SO-16); `AW_SINDRI_APP_NO_MCP=1` and the profile key `setup.mcp: off` (§7.1) turn the automatic runs off |

### 5.5 Step `map`: the auto map (context lane)

The context lane's final names are used as stated. The trigger and the cleanup agent are the context lane's; this spec
adds only the setup step that reports them.

| | |
|---|---|
| Trigger | The built-in service **`context-map-refresh`** ticks every minute with no profile entry. For each repo of the approved profile it starts a job when the repo's HEAD moved and is reachable from the default branch (`origin/HEAD`, `main`, `master`; no fetch). Backoff 5, 10, 20, 40, 60 minutes after a failure |
| Job | Kind **`RefreshContextMap`** (key `refresh`, one at a time): a draft under the heavy lock, then a cleanup agent, then the promote. The cleanup agent: deterministic proposals are accepted; orphaned overrides are dropped; model proposals at or above 0.75 are accepted; the rest go to the model as keep or remove in batches; if the model is unavailable or over its cap they are rejected. The service then **promotes through the context engine directly**, `Who = service:context-map-refresh` |
| Manual | `sindri context map refresh` and `POST /sindri/context/map/refresh` (class `job`); the context lane owns the catalog row and its goldens |
| The owner rule | **Map promote has no manual step.** The service promote bypasses the protected `context map promote` command by design. The manual protected promote stays for a person who wants it (source: owner decision, 2026-10-19, stated with #433). Every accept the agent makes is a reversible `map_override` row with actor `service:context-map-refresh` and the Undo of API X63 |
| Events | `context.map.built` (existing), **`context.map.refreshed {version, heads{repo: sha}, accepted, removed, dropped, cleanup: "agent"\|"unavailable"}`** (new; the context lane registers it), `context.map.promoted` (existing). There is no repo-registered event: the service reads the approved profile's repos each tick |
| Step | `running` while a `RefreshContextMap` job runs (progress = repos done of repos total); `done` when every approved repo has a current map at its head; `todo` with "waits for a repo" when there is none; `failed` after 3 failures in a row |
| Needs-you | **`failure:service:context-map-refresh`** after 3 failures in a row, cleared on the next success. It is the existing `failure` kind with the service's id, not a `setup` row (so `setup:map` is never emitted) |
| Human-only part | None. A map that needs a decision the agent cannot make is rejected by the rules above, not parked |
| Re-run | A tick with HEAD unchanged starts nothing |

### 5.6 Step `onyx`: the second setup command, and a button

| | |
|---|---|
| Trigger | The owner runs `sindri-app onyx up` (also run by `setup.sh --with-sindri`), or presses **Set up Onyx** in the app |
| Route and job | `POST /sindri/setup/onyx` `{adminEmail, adminPassword}` answers **202** `JobStarted`; job kind `onyx-up`, key `onyx`. It runs the Onyx spec's §4.1 steps 1 to 10 as one job. The terminal prompts become step states |
| The owner's part | (1) **Sign up in the browser**: the job opens Onyx's sign-up page and the step goes `needs-you`, why "Create your Onyx account in the browser, then press Continue", action `SetupOnyx`. (2) The app's form asks for the owner's own Onyx admin login (email, password). The password is in the request body of this one call, held in memory, used for the provisioning requests, never logged, never in the ledger, `command.handled`, a job line or the response, and dropped when the job ends (Onyx spec §5.3). The profile section Onyx writes is a drift: the owner confirms it in step 2 |
| Needs-you | `setup:onyx` while waiting (sign-up, login) or on a failure, with the matching `SND-ONYX-0nn` sentence; `cli` = `sindri-app onyx up` |
| Events | `job.*`, `setup.step.changed`; the existing Onyx state in `control.json` |
| Human-only part | The sign-up and the owner's login. Docker is a prerequisite (`SND-ONYX-001`) |
| Re-run | Onyx's own idempotence (Onyx spec §4.1) |
| Terminal | Unchanged: `sindri-app onyx up` stays, and `setup.sh --with-sindri` still runs it (Onyx spec P5). With the terminal, the profile confirmation is the typed challenge at the terminal, as today |

## 6. Fixed words: the step states

| State | Meaning (plain) |
|---|---|
| `todo` | Not started, or waiting on an earlier step (the `why` says which) |
| `running` | Sindri is working; `progress {done, total}` says how far |
| `needs-you` | Only the owner can go on; `action` is the button |
| `done` | Finished; `detail` says what it did |
| `failed` | It stopped; `error {code, sentence}` and the fix are in plain words |

A step never moves backwards by itself except when its inputs change (a new repo, a new build). The projection is
derived from the ledger and the views (so `views rebuild` rebuilds it); nothing about setup is stored elsewhere.

## 7. The contract

### 7.1 Profile

One new optional key, left out of the canonical JSON when absent (existing approvals keep their hash):
`setup: { mcp: auto | off }` (default `auto`). `off` stops the automatic MCP registration of §5.4; the button still
works. `docs/csharp/sindri-profile.md` documents it (`check-profile-doc.mjs` enforces that).

### 7.2 Routes (SO-1, SO-2)

| Route | Slice | Class | Answer |
|---|---|---|---|
| `GET /setup` | `GetSetup` | query | `SetupView` (below) |
| `POST /setup/repos` | `AddRepo` | desk | 202 `JobStarted(accepted, JobId, links)` |
| `POST /setup/mcp` | `RegisterMcp` | desk | 202 `JobStarted` |
| `POST /setup/onyx` | `SetupOnyx` | desk | 202 `JobStarted` |
| `GET /workflow/recipes/approve-all` | `GetApproveAllPlan` (engine) | query | `ApproveAllPlan` |
| `POST /workflow/recipes/approve-all` | `ApproveAllRecipes` (engine) | **protected** (app: held) | 200 `ApproveAllResult {approved, refused}` |

`approve-all` has no `@`, so it cannot be read as a `{id}@{hash}` route; a route test pins that. All routes sit under
`/sindri`. `JobStarted` is the existing answer (`links {job, cancel, events}`).

**`SetupView`:**

```json
{
  "steps": [
    {
      "id": "repo",
      "title": "Add your repo",
      "state": "todo|running|done|failed|needs-you",
      "why": "plain sentence: why this step is in this state",
      "detail": "plain sentence: what it did or is doing, or null",
      "error": { "code": "SND-SETUP-001", "sentence": "plain sentence with the fix" },
      "action": "AddRepo",
      "progress": { "done": 1, "total": 3 }
    }
  ],
  "asOf": { }
}
```

- `steps` is always six entries in the order `repo`, `profile`, `recipes`, `mcp`, `map`, `onyx`.
- `error` is `null` unless `state` is `failed`, or `needs-you` after a failure. `action` is a catalog action id or
  `null`. `progress` is `null` unless the step counts something.
- `asOf` is the views' `asOf` object (API §2.3), as every view carries.
- Titles are fixed: Add your repo · Approve the profile · Approve the recipes · Connect your agents · Build the map ·
  Set up Onyx. A client may show them or its own.

**`ApproveAllPlan`:** `{recipes: [{id, displayName, version, hash, change: "new"|"changed"|"stale", capabilities:
[string], evals: "pass"|"fail"|"missing", children: [id]}], digest, empty, asOf}`, in the order approval will happen
(children first). `digest` is what the challenge `approve-all <digest6>` is bound to. `empty` is the sentence when the
list is empty, else `null`. **`ApproveAllResult`:** `{approved: [{id, displayName, hash}], refused: [{id, hash, code,
sentence}]}`. `refused` holds the one recipe that stopped the run (it is a list so a later version can hold more), and
the recipes after it are neither approved nor listed; the app re-reads the plan.

### 7.3 Needs-you (SO-5)

A new kind **`setup`**. Its row id is **`setup:<step>`** with `<step>` one of the six step ids. `NeedsYouRowId` accepts
all six; the projection emits `setup:repo`, `setup:recipes`, `setup:mcp` and `setup:onyx` only (the profile uses
`profile:drift`, the map uses `failure:service:context-map-refresh`).

| Field | Value |
|---|---|
| `kind` | `setup` |
| `subject` | `{setup, <step>}` |
| `title`, `reason` | the step's `title` and its `error.sentence` or `why` |
| `urgency` | `pulse` when `needs-you`, `colour` when `failed` |
| `primaryAction` | the step's `action` (`AddRepo`, `ApproveAllRecipes`, `RegisterMcp`, `SetupOnyx`) |
| `cli` | the terminal fallback: `sindri-app onyx up` for Onyx, else the CLI form of the action |
| Cleared when | the step is `done` or `running` |

Rows order by urgency, then oldest first (API §3.6). A `setup` row raises no OS notification (notifications come only
from `notify.desk`, X50).

### 7.4 Catalog action ids (SO-3)

| Action id | Class | Callers | Confirm | Route |
|---|---|---|---|---|
| `AddRepo` | desk | human (cli, app) | — | `POST /setup/repos` |
| `ApproveProfile` | protected | human + terminal, **human app** | typed | `POST /profile/approve` |
| `ApproveRecipe` | protected | human + terminal, **human app** | typed | `POST /workflow/recipes/{id}@{hash}/approve` |
| `ApproveAllRecipes` | protected | human + terminal, **human app** | **held** | `POST /workflow/recipes/approve-all` |
| `RegisterMcp` | desk | human; `service:setup` through `ServiceGrants` | — | `POST /setup/mcp` |
| `SetupOnyx` | desk | human (cli, app) | — | `POST /setup/onyx` |
| `RefreshContextMap` | job | human; `service:context-map-refresh` | — | `POST /context/map/refresh` (context lane) |

CLI forms (the fallback): `sindri repo add <path>`, `sindri profile approve <hash>`, `sindri recipe approve-all`,
`sindri mcp register --apply`, `sindri-app onyx up`. They call the same routes.

### 7.5 Events (SO-4)

| Type | Payload (besides `commandId`) | Key | Public | Writer |
|---|---|---|---|---|
| `setup.step.changed` | `id`, `state`, `code?` | `setup.step.changed:<id>:<state>:<ledger head at the change>` | yes | `service:setup` (only when the derived state differs from the last event of that step) |
| `repo.added` | `repo` (the path is non-public) | `repo.added:<commandId>` | yes | `AddRepo` |
| `recipe.registration.completed` | `registered`, `unchanged` (counts) | `recipe.registration.completed:<epoch>` | yes | `service:setup` |
| `recipe.approved` (existing) | gains `batch?` (the approve-all `commandId`) | as today | yes | `ApproveRecipe`, `ApproveAllRecipes` |
| `mcp.registered` | `repo`, `providers: [{provider, outcome}]` | `mcp.registered:<commandId>` | yes | `RegisterMcp` |
| `context.map.refreshed` | `version`, `heads{repo: sha}`, `accepted`, `removed`, `dropped`, `cleanup` | context lane's | yes | `context-map-refresh` |

The event catalog's `invalidates` lists (UI E-15) add: `setup.step.changed` and `repo.added` re-query `setup`;
`recipe.registration.completed` re-queries `setup` and the recipe list.

### 7.6 Errors (SO-8)

Checked against `main` and PR #433 on 2026-10-19: `SND-SETUP-*` is a new family; `SND-RECIPE-011` is the next free
recipe code; #433 owns `SND-CI-001…010` and `SND-PROFILE-017…019`, which this spec does not use. The phase that
reserves a code re-greps before merging.

| Code | Kind (status) | When | Fix line |
|---|---|---|---|
| `SND-SETUP-001` | Invalid (400) | The folder is missing, is not a git checkout, is unreadable, or is inside the Sindri home | Pick the repo's top folder |
| `SND-SETUP-002` | Conflict (409) | The repo is already in the profile, or its name collides with another repo's | Pick another folder, or give a different name |
| `SND-SETUP-003` | Forbidden (403) | The app's confirmation was not given: a hold shorter than the minimum, or an answer less than 2 seconds after the challenge | Hold the button for the full time |
| `SND-SETUP-004` | Conflict (409) | `RegisterMcp` for a repo the approved profile does not name | Add the repo, approve the profile, then try again |
| `SND-SETUP-005` | Invalid (400) | `SetupOnyx` without the Onyx admin email and password | Enter both; sign up in Onyx first if you have no account |
| `SND-RECIPE-011` | Conflict (409) | Approve-all (or a request for it) names a recipe that this build does not ship | Approve it on its own, after reading its preview |

`SND-RECIPE-009` is reused for a plan that changed between the preview and the approval (engine lane, agreed).
`SND-AUTH-002` is the refusal for a session on every action above.

### 7.7 Views that name a recipe (SO-9)

`displayName` is on `RecipeView`, each `RecipeSummary` row, and wherever an instance or item view names its recipe
(`recipe: {id, version, hash, displayName}`). It is required on a recipe file (`RCP-DISPLAY-001`, rule id chosen
because `RCP-NAME-002…006` already name the instance-name template). It is plain text, at most 60 characters, no
control characters, and is scrubbed like other agent-visible text.

## 8. Golden files and examples

Each is `Golden/Api/<path>`; one body each. Hashes and ids are shortened.

**`setup/all-done.json`**
```json
{"steps":[
 {"id":"repo","title":"Add your repo","state":"done","why":"A repo is in your approved profile.","detail":"agentic-workflow","error":null,"action":"AddRepo","progress":null},
 {"id":"profile","title":"Approve the profile","state":"done","why":"Your profile is approved.","detail":"Approved 3f0c9a.","error":null,"action":null,"progress":null},
 {"id":"recipes","title":"Approve the recipes","state":"done","why":"Every recipe that ships with Sindri is approved.","detail":"26 recipes.","error":null,"action":null,"progress":{"done":26,"total":26}},
 {"id":"mcp","title":"Connect your agents","state":"done","why":"Sindri is registered in your repos.","detail":"Claude Code, Codex and Cursor in agentic-workflow.","error":null,"action":null,"progress":null},
 {"id":"map","title":"Build the map","state":"done","why":"Every repo has a current map.","detail":"Map v3, built at 8597ecc.","error":null,"action":null,"progress":{"done":1,"total":1}},
 {"id":"onyx","title":"Set up Onyx","state":"done","why":"Onyx is running and Sindri can search it.","detail":"Onyx v4.9.0 on 127.0.0.1:3000.","error":null,"action":null,"progress":null}],
 "asOf":{"projectedAt":"2026-10-19T09:12:00Z","cursor":5120,"ledgerHead":5120,"rebuilding":false,"stalled":false,"stalledAt":null,"stalledType":null,"text":null}}
```

**`setup/running-progress.json`** (steps 1, 2 and 4 done and the others shortened with `...` here; the file holds all six)
```json
{"steps":[
 {"id":"repo","state":"done","...":"..."},
 {"id":"profile","state":"done","...":"..."},
 {"id":"recipes","title":"Approve the recipes","state":"running","why":"Sindri is approving your recipes.","detail":"Approving workflow.review-loop.","error":null,"action":null,"progress":{"done":14,"total":26}},
 {"id":"mcp","title":"Connect your agents","state":"todo","why":"Waits for the recipes.","detail":null,"error":null,"action":"RegisterMcp","progress":null},
 {"id":"map","title":"Build the map","state":"running","why":"Sindri is mapping agentic-workflow.","detail":"Reading the code.","error":null,"action":null,"progress":{"done":0,"total":1}},
 {"id":"onyx","state":"todo","...":"..."}],
 "asOf":{"...":"..."}}
```

**`setup/failed.json`** (the `repo` step; the other five follow it)
```json
{"steps":[{"id":"repo","title":"Add your repo","state":"failed","why":"The last folder you picked could not be added.","detail":null,
  "error":{"code":"SND-SETUP-001","sentence":"/home/jdoe/notes is not a git repo. Pick the top folder of a repo."},
  "action":"AddRepo","progress":null},{"id":"profile","...":"..."}],"asOf":{"...":"..."}}
```

**`setup/profile-needs-approval.json`** (the `profile` step)
```json
{"steps":[{"id":"repo","state":"done","...":"..."},
 {"id":"profile","title":"Approve the profile","state":"needs-you","why":"You added agentic-workflow. Sindri waits for you to approve the profile before it uses it.","detail":"1 change: repos[agentic-workflow] added.","error":null,"action":"ApproveProfile","progress":null},
 {"id":"recipes","state":"todo","...":"..."}],"asOf":{"...":"..."}}
```

**`recipes-approve-all/plan.json`**
```json
{"recipes":[
 {"id":"workflow.step","displayName":"One step","version":1,"hash":"1a2b3c4d5e6f","change":"changed","capabilities":["effect.comment"],"evals":"pass","children":[]},
 {"id":"workflow.item","displayName":"Work item","version":2,"hash":"9f8e7d6c5b4a","change":"changed","capabilities":[],"evals":"pass","children":["workflow.step"]}],
 "digest":"c41e07","empty":null,"asOf":{"...":"..."}}
```

**`recipes-approve-all/applied.json`**
```json
{"approved":[{"id":"workflow.step","displayName":"One step","hash":"1a2b3c4d5e6f"},{"id":"workflow.item","displayName":"Work item","hash":"9f8e7d6c5b4a"}],"refused":[]}
```

**`recipes-approve-all/refused.json`**
```json
{"approved":[{"id":"workflow.step","displayName":"One step","hash":"1a2b3c4d5e6f"}],
 "refused":[{"id":"workflow.item","hash":"9f8e7d6c5b4a","code":"SND-RECIPE-008","sentence":"Work item: its checks do not pass. Run sindri recipe eval recipes/sindri/workflow.item.yaml."}]}
```

**`recipes-approve-all/empty.json`** (the plan when nothing waits)
```json
{"recipes":[],"digest":"000000","empty":"Every recipe is approved.","asOf":{"...":"..."}}
```

Each golden is a test input for the API (serialization) and the UI (fixtures); the UI's contract tests read them, and
`Aw.Sindri.Ui.Tests` pins the catalog (the api lane's gate for P1 and P2 runs it, as the UI lane's note requires).

## 9. The two setup commands

| Command | What it does | Changes in this spec |
|---|---|---|
| `./setup.sh --providers … --with-sindri` | Installs Sindri's dependencies and tools, publishes nothing the owner must run by hand, then runs the Onyx command at the end (Onyx spec P5) | Prints, last, "Open the app with `sindri-app`; its first screen finishes the setup." It does not print the MCP register command any more |
| `sindri-app onyx up` | Onyx setup: the stack, your account, Sindri's key, the profile section | Unchanged. Not at a terminal, setup.sh prints it instead of asking; the app's **Set up Onyx** does the same job |

Daily start (`sindri-app`, bare) is not setup. On a box where Onyx is not set up it keeps the Onyx spec's one-time offer
at a terminal and a needs-you row `setup:onyx`.

## 10. Folding in PR #433 (CI apply spec): clause by clause

#433 lets a merged, checked PR approve recipes and promote maps from CI/CD (`ci-apply`, class `ci`). Setup once removes
most of what #433 needed a CI for on a fresh box.

| #433 clause | Verdict | Why |
|---|---|---|
| §1 and §3.2 `ci-apply` service, mirror, cursor, candidates, checks, events (`ci.*`) | **Keeps**, unchanged, for recipes only | They are about recipe files merged in a repo; nothing here touches them. It is not part of the setup: it needs a forge token, mergers and a workflow, which is more setup, not less |
| §3.1 profile `ci:` block, `SND-PROFILE-017…019` | **Keeps** | No collision: this spec's profile key is `setup:` and its codes are `SND-SETUP-*` |
| §3.4 recipe approve with the capability ceiling | **Keeps** for owner-written recipes in a repo. **Conflicts** only where it says a recipe id's first approval is at the terminal: it becomes "in the app (`ApproveRecipe`, typed) or at the terminal". Shipped recipes are not CI's: they come through approve-all (§5.3) | The shipped recipes are the installed build's, approved by the owner's hold, not by a merged PR of this repo's `recipes/sindri/` |
| §3.5 map promote from CI (`promote` entries, build at a commit over the mirror, `SND-CI-008`, CI-P5) | **Supersedes** | `RefreshContextMap` builds and promotes on a default-branch move with no forge, no checks and no mergers (§5.5). CI-P5 is dropped; `promote:` entries are removed from the `ci:` block's schema, and `SND-CI-008` is retired |
| §3.5 "model proposals are left out of a CI promote" (O4) | **Conflicts** | The auto map's cleanup agent decides model proposals (accept at 0.75, ask the model below, reject if it cannot), so a promoted map can hold agent decisions. The owner decides (Q3) |
| §3.6 map import from CI (CI-P8) | **Keeps**, deferred, outside setup | An authored map file is not what an auto map is. Still waits on its own questions (#433 O2) and the switch |
| §4 class `ci`, `ServiceGrants`, separate request types, no route | **Keeps** | `service:setup` uses `ServiceGrants` on `RegisterRecipe` and `RegisterMcp` the same way, with no new class; the auto map's promote is the owner rule of §5.5, not a `ci` command |
| §6.2 needs-you kind `failure` with `failure:ci:*`, `RetryCiApply`, the optional `link` field | **Keeps** | Different ids from `setup:<step>`; the `link` field is also available to setup rows |
| §6.2 `cli` per code (`sindri recipe approve <id@hash>` for `SND-CI-007`) | **Conflicts, narrowly** | A row's `primaryAction` for `007` becomes `ApproveRecipe` (ready in the app); the `cli` stays as the fallback |
| §6.1 and §6.2 System `ci:apply` node and `GET /ci` | **Keeps** | Independent |
| §7 credential | **Keeps** | Independent |
| §8 "what stays human" table | **Amended** | Profile approve and recipe approvals stay human but are now app-confirmable (§4); revoke, grants, accept and manual map promote stay terminal |
| §9 self-hosted runner alternative | **Keeps** (an alternative, unchanged) | |
| §10 edits to API §3.7, §4.1, §4.2, §6 (CI-1…CI-4) | **Keeps; textual overlap** | The two PRs both edit API §4.1/§4.2/§6 and §3.6. Whichever merges second rebases; this PR's edits are additive and do not alter CI-1…CI-4 text |
| §11 O1 to O7 | **O4 interacts** (above); the rest unchanged | |
| §13 CI-P1…CI-P8 | CI-P5 **dropped**; CI-P1…P4, P6, P7 and P8 unchanged | |

#433's own §12 findings (rounds 1 to 3) are about the pull model, the ceiling and the cursor; none is reopened. This PR
should add one line to #433's body: "its map-promote half is replaced by the setup-once spec."

## 11. Edits this amendment makes

| File | Section | Change | Mark |
|---|---|---|---|
| API surface spec | revision note, §3.1 | `GET /setup` row | SO-1 |
| API surface spec | §3.2 | `AddRepo`, `RegisterMcp`, `SetupOnyx` rows | SO-2 |
| API surface spec | §3.3 | the `confirm` column and the three rows' status | SO-3 |
| API surface spec | §3.4 | the events of §7.5 | SO-4 |
| API surface spec | §3.6 | kind `setup`, id `setup:<step>`, the `profile` row's `primaryAction` | SO-5 |
| API surface spec | §4.1, §4.2 | `protected` has a second channel for three actions; `service:setup` and the map service | SO-6 |
| API surface spec | §6 "Protected actions in the UI" | rewritten for §4 | SO-7 |
| API surface spec | §9 | the codes of §7.6 | SO-8 |
| API surface spec | §3.8 | `displayName` on the recipe and instance views | SO-9 |
| Engine spec | §3.2 | `displayName` field and `RCP-DISPLAY-001` | SO-10 |
| Engine spec | §4.1, §4.2 | registration at start, approve-all, the app channel | SO-11 |
| Engine spec | §6.2 | class table: `protected` rows in the app | SO-12 |
| Engine spec | §8 | `SND-RECIPE-011` | SO-13 |
| Context spec | §3 "Decisions stay human" | the service promote | SO-14 |
| Onyx spec | O3, §4.1 step 7 | the owner's login may be given in the app | SO-15 |
| `docs/sindri/README.md` | X65 | the decision | SO-16 |
| `docs/csharp/sindri-profile.md` | `setup` | the key | SO-P1 |
| `docs/csharp/sindri.md` | commands, errors, events | as phases land | each phase |
| `scripts/sindri-app.sh`, `setup.sh` | headers and output | no register-command print; the closing line | SO-P4 |

## 12. Phases, with a parallel plan

Lanes: **api**, **engine**, **context**, **ui**. Up to four builders per lane. A unit is a PR. `->` is "waits for".

| Unit | Lane | What | Waits for |
|---|---|---|---|
| SO-A1 | api | **The contract:** `SetupView` and `ApproveAllPlan` types, the eight goldens, `GetSetup` with the derived projection (all steps `todo` from an empty ledger), the `setup` kind and `NeedsYouRowId`, the events in `EventCatalog`, `SND-SETUP-*` codes, `setup:` profile key, catalog rows (stubs), docs | — |
| SO-A2 | api | App confirmation: the challenge for a `human` app caller on the three actions, `confirm` and `minHoldMs`, `SND-SETUP-003`, the catalog flip and its architecture-test change | A1 |
| SO-A3 | api | `AddRepo`: route, job, path checks, the profile editor, `repo.added`, onboarding after approval (hook slot), `GetSetup` repo and profile steps | A1 |
| SO-A4 | api | MCP: `RegisterMcp` route and job, the automatic runs from `profile.approved` and serve start, `mcp.registered`, `sindri-app.sh` | A1, A3 |
| SO-A5 | api | `SetupOnyx`: route, job, the pause points as step states, the password handling and its tests | A1, A2 |
| SO-A6 | api | Setup service: step derivation, `setup.step.changed`, the needs-you rows, doctor `setup`; the first-run proof in a scratch home | A1, A3, E2, C1 |
| SO-E1 | engine | `displayName`: the field, `RCP-DISPLAY-001`, all 26 recipes get one, evals updated, views carry it | — |
| SO-E2 | engine | Registration at serve start and `recipe.registration.completed`; the shipped-hash source | E1, A1 |
| SO-E3 | engine | Approve-all: the plan query, the protected POST, `batch`, `SND-RECIPE-011`, `SND-RECIPE-009` on drift; terminal verb `sindri recipe approve-all` | E1, A1 (and A2 for the app channel) |
| SO-C1 | context | The `map` step's inputs: `context.map.refreshed` and the failure row as the context lane specified, plus the manual `context map refresh` row; no new rule beyond §5.5 | A1 |
| SO-U1 | ui | The Setup checklist (first screen until done): six steps from `GET /setup`, states, progress, plain words; fixtures are the goldens | goldens in A1 |
| SO-U2 | ui | The two confirmations: typed (profile) and held (approve-all), the preview sheet, `SND-SETUP-003` handling | goldens in A1; A2 to run live |
| SO-U3 | ui | Add repo (folder picker, then the job's progress), Set up Onyx (sign-up wait, login form), Register MCP | A3, A5 to run live |
| SO-U4 | ui | `setup` needs-you rows, `displayName` on recipes and instances, the approve-all list | A1, E1 |

**Waves.** Wave 0: A1 and E1 start together, and U1, U2 and U4 can start from the goldens in this spec before A1
merges (the UI lane builds against fixtures). Wave 1, once A1 merges: A2, A3, E2, E3 (terminal path), C1 and the UI
units against live data run in parallel. Wave 2: A4, A5, U3 against live data. Wave 3: A6 and the proof.

**Contract each unit waits on.** The goldens and types are A1's; no other unit changes them. The challenge shape is A2's;
U2 and E3's app path depend on it, E3's terminal path and its tests do not. `displayName` is E1's; every recipe hash
changes there, so E1 must merge before any shipped recipe is approved in daily use (the engine spec's P2 note says the
same of every hash-changing field).

**Proof (A6):** on a scratch home with the file forge unused: from a fresh `AW_SINDRI_HOME`, start `serve`; the six steps
show; add a repo through the route as the human; the profile step asks; a session's attempt at the same approval is
`SND-AUTH-002`; a typed confirmation approves; the MCP step writes the three providers' files in the scratch repo;
recipes are registered (26, `unchanged` on the second start); a held approve-all approves them in order; a recipe edited
between the plan and the POST gives `SND-RECIPE-009` and nothing applied; the map step runs the refresh job; a recipe with a
failing eval makes the approve-all stop at it with `refused`. **Measure:** commands typed from install
to a working Sindri: 2 (target), against 63 today.

## 13. Open questions for the owner

Each is a real decision with a recommended answer.

- **Q1. Approving in the app instead of the terminal.** The profile and the recipes are approved by typing or holding in
  the app, for those three actions only; revoke, grants, accept and a manual map promote stay terminal. Is that the
  line? *Recommended: yes, exactly these three.*
- **Q2. Registering Sindri in your repos by itself.** When you approve a profile that adds a repo, Sindri registers its
  MCP server in that repo for Claude Code, Codex and Cursor on its own, and repairs it when the port changes. Or should
  that be a button you press? *Recommended: automatic, with the off switch in the profile, because approving the profile
  already said which repos.*
- **Q3. The map without a person.** The cleanup agent decides what stays in the map (including a model's keep or
  remove), and the result becomes the current map with no review. Every choice can be undone in the Context screen. #433
  said model guesses should wait for a person. Which? *Recommended: the agent decides, with Undo and the "cleanup:
  unavailable" case rejecting the guesses, as the context lane built it; it is what "no manual step" means.*
- **Q4. Your Onyx password in the app.** The app's form takes your Onyx admin login for the one setup call and forgets it
  at once; the terminal command stays. Or terminal only? *Recommended: allow the form; the password is never written
  anywhere and only your own user can send it.*
- **Q5. After a Sindri update changes recipes.** You hold the approve-all button once more (the first time after this
  change re-approves all 26, because every recipe gets a display name). Or should an update auto-approve shipped recipes
  whose capabilities did not change? *Recommended: hold again; a recipe can run commands, and one hold is cheap.*
- **Q6. The secret-check hook on a new repo.** The TypeScript build installs a pre-commit hook that refuses
  secret-shaped text. The C# build has no installer yet. Add the repo now without it and say so on the step, or hold
  the repo step until the installer exists? *Recommended: add now, say so, and install the hook when the installer
  ships.*
- **Q7. What happens to #433.** Keep its recipe half as an optional extra for recipes you write in your own repos, and
  drop its map-promote half as replaced here? *Recommended: yes.*
- **Q8. Which folders can be added.** Any git checkout your user can read outside the Sindri home. Or only folders under
  your home? *Recommended: any readable git checkout; the path is checked, never trusted.*

## 14. Not in this spec

Multi-user setup (X61), a Windows setup, installing Docker (Onyx O5), the shipped recipes' own content, importing an
authored map (#433's deferred part), changing the sandbox (SB specs), and the Context screen's design beyond the Undo it
already has.
