# Sindri setup once: the owner sets up Sindri and Onyx, and nothing else is typed (spec amendment)

**Status:** proposed, rev 2 (2026-10-19). The owner reads it before it is marked ready. It is a docs-only amendment: no
code lands until the owner approves it and answers the open questions in §14. Lanes: api (this spec), engine, context, ui.

**Rev 2 (2026-10-19), /autoplan round 1 (§12):** one honest first-run order (setup.sh installs and opens the app; Onyx is
a step in the app; one table counts every prompt); approve-all is a job with a plan digest, an internal approval path and a
shipped-recipe manifest written from the committed tree; recipe versions bump with `displayName`; job kinds and keys match
the id types; Onyx setup is two calls and the password never reaches a job row; approve-all also takes a typed answer;
Setup is a Home card, not a first screen; the UI specs are amended; consent is in plain words; background failures are
visible and retryable.

**Decision it implements (owner, 2026-10-19, after the demo):** Sindri is set up **once**, then Onyx. Everything else
happens in the background or through the app, never through CLI commands: repo registration, the profile approval
prompt, MCP registration, map builds and recipe registration.

**Amends** (the full list, with the repo docs, is §11; each inline edit is marked **(SO-n)** in the amended text):
- the API surface spec (`2026-10-10-sindri-api-surface-design.md`): SO-1 to SO-9 and SO-18 (the setup route, the setup
  commands, the `setup` needs-you kind, the app as a confirmation channel for three `protected` actions, error codes,
  events, `displayName` on the recipe views, the `sindri takeover` short form);
- the workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`): SO-10 to SO-13 and SO-17 (`displayName` and the
  version rule, registration at serve start, approve-all, `SND-RECIPE-011`, the `sindri takeover` short form);
- the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`): SO-14 (the map has no manual promote
  step; the `context-map-refresh` service promotes);
- the Onyx spec (`2026-10-16-sindri-onyx-managed-design.md`): SO-15 (the owner's Onyx login may be given in the app);
- the UI specs (`2026-10-11-sindri-ui-design.md`, `2026-10-13-sindri-ui-screens-design.md`): SO-19 to SO-21 (Setup on
  Home, three protected actions become app confirmations);
- `.agents/rules/mcp-servers.md`: SO-22 (Sindri may register its own MCP after the profile is approved);
- `docs/sindri/README.md`: SO-16 (X65).

**Relation to PR #433 (CI apply spec, draft):** §10 folds it in clause by clause. This text wins where they differ, and
only for the clauses §10 marks "supersedes" or "conflicts".

## 1. Goal and the owner's rule

**The rule (owner, 2026-10-19):** the owner sets up **Sindri** once, then **Onyx**. Sindri setup is one command in a
terminal. Onyx setup is a step in the app (with `sindri-app onyx up` kept as the terminal fallback). A thing that is not
one of those is a background job or a button in the app. A new repo is a button. An approval is a typed or held
confirmation in the app. A map builds itself.

**What stays human.** Approvals stay human. The rule removes the *terminal*, not the *person*: the profile approval and
the recipe approvals keep a confirmation bound to the content's hash, now given in the app (§4). A session is still
refused `SND-AUTH-002`. Nothing in this spec lets an agent approve anything.

**Success.** A new owner runs `./setup.sh --with-sindri`. It installs, publishes the app and opens it. Home shows a
Setup card of six steps (repo, profile, recipes, MCP, map, Onyx). They pick a repo, confirm one profile approval, hold
one "approve all recipes", sign up to Onyx in the browser and enter their Onyx login in a form, and watch the rest finish.
The second repo they add is one pick and one typed confirmation. Success is measured two ways: the prompts in the table
of §2, and **time to first value**, the time from the app's first open to all six steps `done`, recorded from
`setup.step.changed` (§7.5). When the six are done, the card's last line says what to do next ("Open a repo's map" or
"Start a work item").

## 2. Today, and after: every prompt a new owner meets

The C# Sindri app, from nothing to a working Sindri with one repo. A numbered row is one command the owner types. A
"typed" row also asks for a typed confirmation. Counts: 26 shipped recipes (`recipes/sindri/*.yaml`).

| # | Step | Exact command today | Typed confirmation |
|---|---|---|---|
| 1 | Install Sindri (builds the TS `sindri`, the deps, the Serena image), publish `sindri-app`, then stand up Onyx at the terminal: your Onyx account in the browser, then your Onyx admin login and password (prompts, not commands) | `./setup.sh --providers claude --with-sindri` (it runs `sindri-app onyx up` itself unless `--no-onyx`) | the profile approval, inline |
| 2 | Start the control plane and open the app | `sindri-app` | no |
| 3 | Register a repo: there is no C# verb, so the owner edits `profile.yaml` (`$AW_SINDRI_HOME/profile/profile.yaml`) and adds a `repos:` entry. The TS build has `sindri repo onboard <path>` (add, approve, pre-commit hook, first index) | hand edit; or TS `sindri repo onboard .` | its profile approval |
| 4 | Look at the profile change | `sindri-app profile approve` | no |
| 5 | Approve the profile | `sindri-app profile approve <hash>` | typed: `approve profile <hash6>` |
| 6 | Register Sindri's MCP in the repo (per repo, from inside it) | `sindri-app mcp register --apply --providers all` | no |
| 7-32 | Register each shipped recipe, children first (26 commands) | `sindri-app recipe register recipes/sindri/<id>.yaml` | no |
| 33-58 | Approve each shipped recipe, children first (26 commands) | `sindri-app recipe approve <id>` | typed: `approve <recipe> <version>`, 26 times |
| 59 | Build the map | `sindri-app context map build` | no |
| 60 | Accept what is certain | `sindri-app context map review --accept-deterministic` (and `review accept\|reject …` for the rest) | no |
| 61 | Make the map current | `sindri-app context map promote` | typed: `promote map v<n>` |
| 62 | Check | `sindri-app doctor` | no |

Today is **62 commands and 28 typed confirmations** for the first repo (1 setup.sh, which also runs Onyx, 1 start, 1 repo
registration, 2 profile, 1 MCP, 52 recipe, 3 map, 1 doctor), plus **7 commands and 2 typed confirmations for each further
repo** (registration, two profile commands, MCP, three map commands). Rev 1 counted the Onyx command separately (63); it is
part of setup.sh, so 62 is the honest figure.

**The new first run, in order** (one sequence, no step twice):

1. `./setup.sh --providers … --with-sindri` installs the dependencies and tools, **publishes `sindri-app` as its own named
   step** (§9) and **opens the app** (or, with no desktop session, prints the launcher's absolute path). It no longer runs
   `onyx up`; `--with-onyx` keeps the old terminal behaviour (Q9).
2. In the app, Home shows the Setup card. The steps that can start do: the folder pick, registration of the shipped
   recipes (at start), Set up Onyx (any time Docker is ready). The profile step is `todo` ("Waits for your first repo")
   until a repo exists (§5.2).
3. The owner picks a repo, confirms the profile once, holds approve-all, signs up in the browser for Onyx and enters the
   login in the form. MCP and the map finish by themselves.

| | Today | After (rev 2) |
|---|---|---|
| Commands typed in a terminal | 62 | **1** (`./setup.sh --with-sindri`); a daily start is `sindri-app`, not setup |
| Typed confirmations at a terminal | 28 | **0** |
| Typed confirmations in the app | 0 | **1** (the profile, bound to its hash); **2** when Onyx is set up after the profile was approved (its section is one more profile change; Q10) |
| Held confirmations in the app | 0 | **1** (approve-all; a typed answer is accepted instead) |
| Browser steps | 1 (Onyx sign-up) | 1 (Onyx sign-up) |
| Forms | 2 prompts at the terminal (Onyx email, password) | 1 (Onyx login) |
| Folder picks | 0 | 1 |
| Per further repo | 7 commands, 2 typed | 0 commands, 1 pick, **1** typed |

The terminal-only fallback finishes the same setup with the CLI forms (§7.4), which ship with their units (A3, E3): a box
with no desktop can complete it, and the app is still the owner's rule, not a requirement.

**Time.** About 45 to 75 minutes on a fresh box, mostly unattended builds (the Serena image, the TS `sindri` build that
setup.sh still does until the switch, the .NET SDK, the publish of `sindri-app`); about 5 minutes of the owner's time.
Each long stretch is a named setup.sh step with a time hint (§9).

**A box that already has Sindri.** The checklist reads what is there: `repo` and `profile` are `done` for repos already in
the approved profile; `recipes` is `needs-you` once (the version bump of §5.3 means one approve-all); `mcp` is `done`
(entries made by hand are `unchanged`, never overwritten, and doctor still warns on a user-wide one); `map` and `onyx` are
`done` or run. The card says why `recipes` came back ("Sindri updated 26 recipes").

## 3. Decisions

| # | Decision | Why |
|---|---|---|
| SO-D1 | The setup is **one visible list of six steps**, served by `GET /sindri/setup`, in the order repo, profile, recipes, mcp, map, onyx. The app draws it as a **Setup card on Home** (§5.7); nothing in it is the app's own logic | The UI is a pure client (UI spec §17.27.1); the owner sees one place that says what is done and what needs them, and Home stays the default page |
| SO-D2 | Long work is a **background job** returning 202 and a job id, never a blocking POST; a step that fails or waits on the owner is also a **needs-you row**. This includes approve-all | The existing job and needs-you machinery (API §3.6, P6) already gives progress, cancel and events |
| SO-D3 | The app becomes a **second confirmation channel for exactly three `protected` actions**: `ApproveProfile`, `ApproveRecipe`, `ApproveAllRecipes`. Everything else `protected` (grants, accept, revoke, map promote by hand, review-apply, merge, add, delete) stays terminal-only | The owner's rule needs the profile and the recipes in the app. Widening further is a separate decision |
| SO-D4 | The app confirms by **typing** (the profile, a recipe) or by **holding or typing** (approve-all), against a server challenge bound to the content's hash. Same challenge store, same 5 minutes, same drift refusal as the terminal | The profile widens what Sindri may do (repos, services, effects), so it is typed. Approve-all covers only recipes whose hashes the installed build carries in a manifest (§5.3), so a deliberate hold after reading the list is enough, and the typed answer keeps it reachable by keyboard and assistive tools |
| SO-D5 | Recipes are **machine-wide**. `serve` registers every shipped recipe at start, idempotently, from the build's embedded copies, and never approves | A shipped recipe is part of the install; registering it is not a decision |
| SO-D6 | **Approve-all** is one human confirmation applied as one approval per recipe, children first, stopping at the first refusal, run as a job. A recipe that gains a capability is never in it | Keeps `recipe.approved` per recipe, so pins, drift and revoke work unchanged; a gain is a decision of its own |
| SO-D7 | **MCP registration is done by Sindri** for every repo of the **approved profile snapshot**, local scope only, never user-wide. Approving a profile that adds a repo is the consent | §5.4 says why this is safe and why it is not an agent-triggerable action |
| SO-D8 | The map has **no manual step**: the `context-map-refresh` service builds, cleans up and promotes. The protected manual promote stays | Owner rule, 2026-10-19 (source: this decision, relayed with #433): map promote has no manual step, so the service promote bypasses the protected command by design |
| SO-D9 | Onyx setup is the **second setup**, and it is a **step in the app**. `sindri-app onyx up` stays as the terminal fallback; setup.sh runs it only with `--with-onyx` | The owner said "Sindri once, then Onyx"; the Onyx login is the one thing only the owner can supply. Running it at the terminal first made the first run contradict itself (rev 1) |
| SO-D10 | The setup step ids are fixed words (`repo`, `profile`, `recipes`, `mcp`, `map`, `onyx`) and appear in the route, the event and the needs-you id | One vocabulary across lanes |
| SO-D11 | The `setup` view is a **read model over stored state** (the ledger, the views, the live and approved profile, git heads, provider config, `control.json`), computed on read; the service only emits the change events (§6) | Rev 1 said "derived from the ledger"; several steps need more than the ledger |

## 4. The app as a confirmation channel (SO-D3, SO-D4)

Today a `protected` action is refused to the app (API §4.2) and the app shows the CLI to copy. This spec changes that
for three actions, and only them. The change is made by **adding a path, not by loosening the old one**: the existing
challenge route keeps its `NeedsTerminal` rule, so a session still cannot issue a challenge for any protected action.

1. **Issue.** The app asks `POST /sindri/workflow/challenges/app` with `{action}`. It is a separate route that accepts the
   `human` token only (a `session` is `SND-AUTH-002`) and mints a challenge only for `approve profile <hash6>`,
   `approve <recipe> <version>` (or `approve <recipe>@<hash>`) and `approve-all <digest6>`; any other text is
   `SND-AUTH-002`. The challenge records its mode and the time it was issued.
2. The answer carries the preview as today, plus `confirm`:
   `{challenge, action, expiresAt, confirm: "typed"|"held", minHoldMs?: 1500, typedAlternative?, approve?|approveAll?}`.
   For approve-all, `confirm` is `held` and `typedAlternative` is the text (`approve-all c41e07`), so either answer redeems.
3. **Typed:** the app shows the preview, then a field that asks for the action text (`approve profile 3f0c9a`). The
   answer is `{challenge, typed}`, compared as the terminal's is. **Held:** the app shows the list and a button that
   fills while pressed (or Space or Enter held); the answer is `{challenge, heldMs}`. A typed challenge cannot be redeemed
   by `heldMs`, and a held one is redeemed by `heldMs` or by its `typedAlternative` only; the held answer has its own redeem
   path in `ProcessChallenges`. The server refuses `heldMs` under `minHoldMs`, and any answer less than 2 seconds after the
   challenge was issued, with `SND-SETUP-003` ("confirmation not given"). The hold is a slip guard, not proof of reading;
   that is the threat model (honest mistakes, not a deliberate agent).
4. **The hold, in full.** It fills over 1.5 seconds with a text count ("Hold to approve, 1.5 seconds"). Releasing early
   cancels, resets the fill and announces "Not approved, hold again". After the 5 minutes the app asks again with the same
   list. With reduced motion the fill is a stepped counter. By keyboard, Space or Enter held for the same time works, and the
   typed alternative is always offered under the button.
5. Everything else is the terminal's rule: one-time, 5 minutes, bound to the subject, refused when the subject changed
   (`SND-RECIPE-009` for recipes, `SND-PROFILE-003` for a stale profile hash). A second open challenge for the same action
   replaces the first.
6. The terminal path stays and writes the same events; the actor tells them apart (`human:cli`, `human:ui`). The prover
   (`human:prover`) is accepted for these actions only in a scratch home.

**Catalog.** `GET /commands` rows gain `confirm` (`typed`, `held` or `null`). The three rows' UI status flips from
`cli-only:typed-confirmation` to `ready` (SO-3). The architecture test that says the app and the MCP server reference no
challenge route (engine §4.2 last line) keeps its MCP half and changes its app half to "the app references only the app
challenge route, and only for the three allowed actions" (SO-12).

**What stops a session.** A `session` caller is refused `SND-AUTH-002` for the three actions as for any `protected`
action. The app sends `human` with the per-start token (API §6); the MCP endpoint stamps `session` whatever it is
sent. Reading the owner's token file stays out of scope for v0 (API §6, §11): **the token file is a v0 trust boundary**
(Q1 says so), and doctor's `setup` check warns when it is group- or world-readable. This channel is desktop-only: a
browser head cannot hold the token.

## 5. The six steps

Each step: **trigger**, **job or route**, **events**, **needs-you row on failure**, **human-only part**,
**idempotent on re-run**. `service:setup` is a new **built-in service** (listed in `sindri services`, **not pausable**, no
profile entry). It runs the in-process parts below as caller `service:setup`, is woken by events (`profile.approved`,
`recipe.approved`, `repo.added`, `mcp.registered`, `job.finished`) and has a 60-second backstop tick, so a step changes
within a second or two of what caused it. The grants it needs are the existing `ServiceGrants` field on the catalog rows
(`RegisterRecipe`, `RegisterMcp`), as #433 uses them for `ci-apply`.

### 5.1 Step `repo`: add a repo from the app

| | |
|---|---|
| Trigger | The owner picks a folder in the app and presses **Add repo** (`AddRepo`, `desk`), or types a path beside the picker (the picker is desktop-only; other heads show only the field). The app sends the absolute path |
| Route and job | `POST /sindri/setup/repos` `{path, name?}`. The cheap checks run **before** the job is claimed, so their codes are real HTTP errors: the path exists, is a git checkout, resolves to its **main checkout** (a linked worktree resolves as API §3.1 says), is readable, is not under `$AW_SINDRI_HOME`, is **not `/`, not the user's home folder and not a parent of `$AW_SINDRI_HOME`** (`SND-SETUP-001`); the repo is not already in the profile and its name does not collide (`SND-SETUP-002`). Then **202** `JobStarted(accepted, JobId, links)`. Job kind **`AddRepo`**, key = **the first 16 hex of sha256 of the resolved path**. The path is never in a job event; events carry the repo name only. A second add of the same path while one runs is `SND-JOB-409` |
| What the job does | 1. Runs `git` with `-c core.fsmonitor= -c core.hooksPath=/dev/null` and `GIT_CONFIG_NOSYSTEM=1`, arguments as an argv, never a shell string. 2. Appends a `repos[]` entry (`name` = the folder name unless `name` is given, `path` = the real path) to the **live profile file**, keeping every other key and comment (a general `repos[]` appender on the profile writer the Onyx step uses). That change is a profile drift, so the profile waits for approval (§5.2). 3. Appends `repo.added`. Progress lines: `checking`, `added to the profile`. **The job ends there**; waiting for the profile is the `profile` step, not a job line, so the job key is free again |
| After approval | The `setup` service sees `profile.approved` name a repo it has not onboarded and runs, as `service:setup`: the pre-commit scrub hook (when the C# build has an installer; until then the repo step shows `done` **with the detail "No secret-check hook yet: add it when the installer ships"**, also a doctor line, never a silent green), then step `mcp` (§5.4). It appends `repo.onboarded {repo, approvedHash}`. The first map is the `context-map-refresh` service's own trigger (§5.5) |
| Events | `repo.added {repo, commandId}` (public; the path is in the non-public payload), `repo.onboarded`, `job.*`, `setup.step.changed` |
| Failure | Checks: `SND-SETUP-001/002` (400/409). Job: the profile's `SND-PROFILE-001/002` as the job's failure code. Needs-you `setup:repo` with the sentence and the action `AddRepo` to try another folder |
| Human-only part | The path is the owner's pick, so `AddRepo` is `desk`: a session is refused `SND-AUTH-002`. Approving the entry is the profile approval |
| Re-run | Adding an added path is `SND-SETUP-002` and changes nothing, except that a profile entry whose `repo.added` is missing (a crash between the two) gets the event appended. Onboarding after approval is keyed by `(repo, approved hash)` through `repo.onboarded` |

### 5.2 Step `profile`: the approval prompt

| | |
|---|---|
| States | **`todo`**, "Waits for your first repo", until the profile names a repo: on a fresh box there is nothing to approve, so there is no `needs-you` row and no preview. The service writes the first profile file (the defaults plus an empty `repos`) when none exists, as `service:setup`, unapproved. It becomes `needs-you` when the live profile differs from the approved one (`profile.checked drifted:true`, every 30 s) |
| Route | The existing `GET /profile` and the app challenge of §4 (`approve profile <hash6>`, **typed**), then `POST /profile/approve` `{hash, answer}` (unchanged body) |
| Needs-you | The existing `profile:drift` row (kind `profile`); its `primaryAction` becomes `ApproveProfile`, `ready` in the app (SO-5). **There is no `setup:profile` row**: the Setup card links the same row, so the card and Needs you show one approval, not two |
| Preview | In plain words, first: for each added repo, **its resolved real path and its size (files and megabytes)** and the sentences the approval carries: "Sindri will read `<path>` and send what it reads to your Onyx and to the map cleanup agent", "Sindri will write its MCP settings into `<repo>` for Claude Code, Codex and Cursor" (§5.4) and "Sindri will keep `<repo>`'s map up to date on its own". Then widened services and effects in words, then the changed key paths. The typed text is a slip guard; the sentences are the consent. Every pending change (the repo and, if Onyx ran first, the Onyx section) is in this one preview |
| Events | `profile.checked`, `profile.approved` (unchanged), `setup.step.changed` |
| Failure | A stale hash is `SND-PROFILE-003` (the app re-reads and asks again); an invalid profile file is `SND-PROFILE-001/002` shown on the step as `failed` with the sentence |
| Human-only part | The whole step. The typed confirmation is bound to the live hash |
| Re-run | Approving an approved hash is a no-op (as today) |

### 5.3 Step `recipes`: registration at start, approve-all in the app (engine lane)

**The shipped set.** At **publish time** (`scripts/sindri-app.sh`), the committed tree decides what ships: the script reads
`recipes/sindri/*.yaml` from **git `HEAD`, not the working tree**, copies those files beside the binary and writes
`recipes.manifest.json` (`[{id, version, hash}]`). The binary embeds both. At runtime nothing reads `recipes/sindri/` on
disk. A stray edit to a working-tree recipe therefore cannot become "shipped". Publishing from a dirty tree is allowed and
says so; the manifest is still HEAD's.

| | |
|---|---|
| Trigger | Registration: every `serve` start, after the control plane listens. Approval: the owner presses **Approve all** |
| Registration | `service:setup` registers each embedded copy that **matches the manifest** (26 today), children first, through the `RegisterRecipe` slice. Idempotent by hash: an already-registered hash is `unchanged`. It **never approves**. Each recipe registers on its own: **one that fails shows on the `recipes` step with its sentence and the other 25 still register** (a recipe whose child failed waits, with `why` naming the child). It appends `recipe.registration.completed {registered, unchanged, failed}` (counts) once per start, and the `recipe.registered` event per new hash as today. A copy whose hash differs from the manifest is `SND-RECIPE-011` for that recipe |
| Approve-all: the plan | `GET /sindri/workflow/recipes/approve-all` is the **plan** (a query): every shipped recipe that is registered and not approved at its current hash, or whose approval is stale (an approved recipe whose child's approved hash moved), children first. Each row has its plain-words capabilities (§5.3.1), the hash last approved and what changed since (a capability delta in words and the changed blocks), and its recorded eval result. A recipe that **gains a capability** since its last approval, or that waits on a child that does, is **not in the plan's list**; it is in `ownApproval` and is approved one at a time, typed, with `ApproveRecipe`. The plan never runs evals: it reads the results recorded when a hash was first registered (`missing` refuses with `SND-RECIPE-008`, as one approval does) |
| The digest | `digest` = sha256 over the ordered list of `id@hash` plus each recipe's capability set (sorted ids). `approve-all <digest6>` is its first six hex characters. The challenge is bound to it |
| Approve-all: apply | `POST /sindri/workflow/recipes/approve-all` (`protected`, app challenge `approve-all <digest6>`, held or typed) answers **202 `JobStarted`**: job kind **`ApproveAllRecipes`**, key `approve-all:<first 16 hex of the digest>`. **One challenge covers the plan.** Once it is redeemed, the job approves each recipe through an **internal approval path** (`ApproveShippedRecipe`) that takes the redeemed plan and runs the same checks as `ApproveRecipe` (pins, evals, the capability ceiling, the approved profile's allowlist) without minting a challenge per recipe, because each recipe's own challenge binds a pins digest that changes as its children are approved. One `recipe.approved` each, with `batch: <commandId>`, the plan digest and the hold time. It **stops at the first refusal**. **Progress** is the job's `{done, total}` and its lines (the `displayName` of the recipe in hand); the **result** is on the job: `{approved, refused}` |
| Drift between preview and apply | If the plan changed after the challenge was issued (a recipe re-registered, a pin moved), the POST is `SND-RECIPE-009` and nothing is applied; the app re-reads the plan |
| Only shipped recipes | A recipe that is not in the manifest (an owner-written or generated recipe) is never in the plan; naming one is `SND-RECIPE-011`. Those are approved one at a time (`ApproveRecipe`, typed in the app, §4) |
| Failing evals | A recipe whose evals fail refuses (`SND-RECIPE-008`), exactly as one approval does. Its `sentence` carries no command: the fix is in the row's `cli` field. The step says "N approved, 1 needs a look" (Q11) |
| `displayName` and versions | `displayName` is a new **required** recipe field (SO-10): the name the app shows instead of `workflow.step`. It changes every hash, and the registry refuses a different hash at the same version (`RecipeVersionTaken`), so **the same change bumps every shipped recipe's version**. The rule is general: **a content change bumps the version**, and `recipe validate` checks it against the previous manifest. Instances already started keep the hash they pinned; only new starts use the new version. Every shipped recipe is re-approved once, which approve-all makes one hold. Views that name a recipe carry `displayName` beside the id (SO-9) |
| Needs-you | `setup:recipes` while recipes are registered and unapproved ("26 recipes wait for your approval") or on a failed registration or refusal, primary action `ApproveAllRecipes` |
| Events | `recipe.registration.completed`, `recipe.registered`, `recipe.approved`, `job.*`, `setup.step.changed` |
| Human-only part | Approve-all. A session is refused `SND-AUTH-002`; registration is not a decision |
| Re-run | Registration of known hashes changes nothing. Approve-all on an empty plan answers 200 `{approved: [], refused: []}` without a job and the plan's `empty` sentence says "Every recipe is approved." |
| After a Sindri update | A new build's changed recipes are registered at the next start and appear in the plan as `Updated`; the step returns to `needs-you` with the reason, and one more hold approves them (Q5) |
| Order | The step does not wait for the profile to be approved, but the plan's previews use the approved profile's allowlist; before any profile is approved the plan says so and the hold is not offered |

**5.3.1 Capabilities in plain words.** The consent surface never shows a machine name first. Each block descriptor carries a
plain sentence (a descriptor without one fails the block catalog test), and the plan groups the recipes by capability. The
machine id sits in a small secondary line.

| Capability (family) | Sentence shown |
|---|---|
| `effect.claim.ref` | Reserves a work item so no other run starts on it |
| `effect.notify.desk` | Puts a question or notice in your Needs you list |
| `effect.line.report` | Writes progress lines into the workflow log |
| a step that starts an agent (`step.produce`, `step.review.*`, `step.rootcause.*` and the like) | Starts an AI agent session in a repo you added |
| `step.checks`, `step.verify*`, `step.reproduce.*` | Runs your repo's tests and build commands |
| `step.ship.*` | Opens or updates a pull request and reads its checks |
| anything the table has no words for | Does something this list has no words for yet: `<id>`. Never part of approve-all |

The change words are **New**, **Updated** and **Needs a second look** (a part it uses changed). A capability that was added
is written "Newly allowed: …" and puts the recipe in `ownApproval`.

### 5.4 Step `mcp`: Sindri registers itself in each repo

Today `sindri mcp register --apply` is the human's verb, and `scripts/sindri-app.sh` only prints the command. The owner's
rule moves the doing to Sindri. The mechanism, and why it is safe:

| | |
|---|---|
| Trigger | (a) A repo appears in the **approved profile snapshot** with no registration recorded for the current port; (b) every `serve` start, for each approved repo (so a changed port is repaired); (c) the owner presses **Register MCP** (`RegisterMcp`, `desk`) |
| Which profile | The service reads **the approved snapshot by hash** (the content of the ledger's `profile.approved`), never the live file. A repo only in a drifted live file is not registered (`SND-SETUP-004`). A test starts `serve` with a drifted live profile and checks that nothing is written |
| Route and job | (c) `POST /sindri/setup/mcp` `{repo?}`; the cheap check (the repo is named by the approved snapshot, `SND-SETUP-004`) runs first, then **202** `JobStarted`. Job kind **`RegisterMcp`**, key = the repo name. (a) and (b) run the same slice in-process as `service:setup` and show as the same job kind in `GET /jobs` |
| What it writes | Claude Code's **local** scope (`claude mcp add --scope local`, run in the repo's main checkout), the checkout's `.cursor/mcp.json` and `.codex/config.toml`. Providers are those found on the box; none found is not a failure. It writes only when the content differs, never touches a key that is not Sindri's, and adds the two files to `.git/info/exclude` (not `.gitignore`). **It does not write Codex's project-trust row**: Codex asks the owner once, and the detail says "Codex will ask you to trust this folder once". The terminal verb `mcp register --apply` is unchanged and still writes the row |
| Never | User-wide scope, any repo not in the approved snapshot (`SND-SETUP-004`), any content other than Sindri's loopback `/sindri/mcp` URL and the `X-Sindri-Repo` header. Doctor's `context:mcp-scope` still warns on a user-wide entry (`.agents/rules/mcp-servers.md`, SO-22). A checkout that **tracks** `.cursor/mcp.json` or `.codex/config.toml` in git is left alone, and the step says which file and why (`needs-you`, `RegisterMcp` after the owner decides). Only the main checkout is registered; a lane's linked worktree is not |
| Why a background process may do it | The inputs are fixed and come from the approved profile, so the profile approval (typed, human) is the consent and the preview says so (§5.2). The action is idempotent (provider outcome `unchanged`) and affects one repo's local config. No session can start it: `RegisterMcp` is `desk`, and the automatic runs are triggered by `profile.approved`, a human event. The server runs as the owner's OS user, so it writes only what the owner could |
| Needs-you | `setup:mcp` on a failed provider ("Couldn't register with Claude Code: `claude` isn't installed" with the provider's fix line), primary action `RegisterMcp` |
| Events | `mcp.registered {repo, port, providers: [{provider, outcome}]}` (public), `job.*`, `setup.step.changed` |
| Re-run | Unchanged providers write nothing |
| Off switches | The profile key `setup.mcp: off` and `AW_SINDRI_APP_NO_MCP=1` turn the automatic runs off; the step's detail then reads "Automatic registration is off" (the button still works). `scripts/sindri-app.sh` stops printing the register command (SO-16) |

### 5.5 Step `map`: the auto map (context lane)

The context lane's final names are used as stated. The trigger and the cleanup agent are the context lane's; this spec
adds only the setup step that reports them.

| | |
|---|---|
| Trigger | The built-in service **`context-map-refresh`** ticks every minute with no profile entry. For each repo of the approved profile it starts a job when the repo's HEAD moved and is reachable from the default branch (`origin/HEAD`, `main`, `master`; no fetch). A repo with none of those refs makes the step `todo` with "Waits for a default branch". Backoff 5, 10, 20, 40, 60 minutes after a failure |
| Job | Kind **`RefreshContextMap`** (key `refresh`, one at a time): a draft under the heavy lock, then a cleanup agent, then the promote. The cleanup agent: deterministic proposals are accepted; orphaned overrides are dropped; model proposals at or above 0.75 are accepted; the rest go to the model as keep or remove in batches; if the model is unavailable or **over its daily cap** they are rejected. The service then **promotes through the context engine directly**, `Who = service:context-map-refresh` |
| The cleanup agent's limits | It runs **read-only with no tools**; its input is scrubbed by `Aw.Redaction` and its output is validated against a schema. A **per-run removal ceiling** (more than 30% of the map's entries removed, the context lane may tune the number) parks the run and raises the failure row instead of promoting. The previous map version stays one click away (a promote is a pointer move). `context.map.promoted` carries `cleanup` and the counts too |
| Cost | The cleanup's model calls have a **daily cap** (the context lane sets the number; Q3 asks the owner for it). The step's detail shows the day's use ("cleanup: 12 of 40 calls today"); over the cap the agent is `unavailable` and rejects the guesses |
| Manual | `sindri context map refresh` and `POST /sindri/context/map/refresh` (class `job`; **action `RefreshContextMap`, "Retry now"**); the context lane owns the catalog row and its goldens |
| The owner rule | **Map promote has no manual step.** The service promote bypasses the protected `context map promote` command by design. The manual protected promote stays for a person who wants it (source: owner decision, 2026-10-19, stated with #433). Every accept the agent makes is a reversible `map_override` row with actor `service:context-map-refresh` and the Undo of API X63; the Context screen shows "changed by the agent" next to them |
| Events | `context.map.built` (existing), **`context.map.refreshed {version, heads{repo: sha}, accepted, removed, dropped, cleanup: "agent"\|"unavailable"}`** (new; the context lane registers it), `context.map.promoted` (existing). There is no repo-registered event: the service reads the approved profile's repos each tick |
| Step | `running` while a `RefreshContextMap` job runs (progress = repos done of repos total); **after the first failure it stays `running` and the detail reads "Retrying in N minutes: `<sentence>`"**; `failed` after 3 failures in a row, with the action `RefreshContextMap`; `done` when every approved repo has a current map at its head; `todo` with "Waits for a repo" when there is none |
| Needs-you | **`failure:service:context-map-refresh`** after 3 failures in a row, cleared on the next success. The service counts them by reading the last `RefreshContextMap` jobs and returning an error on the third; it is the existing `failure` kind with the service's id, not a `setup` row (so `setup:map` is never emitted) |
| Human-only part | None. A map that needs a decision the agent cannot make is rejected by the rules above, not parked |
| Re-run | A tick with HEAD unchanged starts nothing |

### 5.6 Step `onyx`: a step in the app, in two calls

| | |
|---|---|
| Trigger | The owner presses **Set up Onyx** in the app (any time Docker is ready), or runs `sindri-app onyx up` at a terminal (or `setup.sh --with-onyx`) |
| Call 1: start | `POST /sindri/onyx/setup` (`SetupOnyx`, `desk`, no body) answers **202** `JobStarted`; job kind **`SetupOnyx`**, key `onyx`. It runs the Onyx spec's §4.1 steps 1 to 6 (the stack up) and then **waits**: the step is `needs-you`, why "Create your Onyx account in the browser, then enter your login here", and the detail always holds the sign-up address (`http://127.0.0.1:3000/auth/signup`) so a box with no browser opener still works. The job opens the page when it can. The wait can be cancelled (`CancelJob`); the step goes back to `todo` |
| Call 2: the login | `POST /sindri/onyx/account` `{adminEmail, adminPassword}` (`ConnectOnyx`, `desk`) answers **202** with the same job id and resumes it (steps 7 to 10). Without a waiting job it is `SND-SETUP-006`; without both fields `SND-SETUP-005` |
| The password | It is carried by a **dedicated secret type**: no `ToString`, not serializable, excluded from `AwJson` and from `Aw.Redaction`'s output by construction, and not a field of the command record or the job's stored request. It is **never** in the job row, the ledger, `command.handled`, a job line, a log, a span, a response or the UI's own state; it is used for the provisioning requests once and dropped. A job does not survive a restart with a login pending: on restart the step goes back to `needs-you` and asks again. A test sends a sentinel password and greps logs, the ledger, jobs and `command.handled` for it |
| The profile section | Onyx writes its section into the profile, a change like a repo (Q10). If the profile has not been approved yet, that change is in the same preview and the same one approval; if it has, it is a second typed approval. The card says which |
| Needs-you | The existing **`failure:service:onyx`** row (`NeedsYouRows.ForOnyx`), whose reason carries the wait or failure sentence (`SND-ONYX-0nn`) and whose primary action is `SetupOnyx` (or `ConnectOnyx` once waiting for the login); **no `setup:onyx` row**. `cli` = `sindri-app onyx up` |
| Events | `job.*`, `setup.step.changed`; the existing Onyx state in `control.json` |
| Human-only part | The sign-up and the owner's login. Docker is a prerequisite (`SND-ONYX-001`), shown on the step on first open when it is missing |
| Re-run | Onyx's own idempotence (Onyx spec §4.1) |
| Terminal | Unchanged: `sindri-app onyx up` stays (Onyx spec P5). `setup.sh` runs it only with `--with-onyx`; its profile confirmation is then the typed challenge at the terminal, as today |

### 5.7 The Setup card on Home, and its states

The app shows the six steps as a **Setup card on Home**, above Needs you, while any step is not `done`. Home stays the
default page (screens spec R8-1; rev 7 §3.3). The card has a **nav entry (Setup under Home), the chord `G U` and the deep link `sindri://setup`**,
and after completion it collapses to one line with a link to Health; a step that reopens (a new repo, a new build) brings
the card back. It links the `profile:drift` row rather than copying it.

- **Loading:** a six-row skeleton. **Rebuilding or stalled views:** the existing `asOf` banners. **Sindri is restarting:**
  "Sindri is starting" until the next answer.
- **Sindri isn't running:** derived client-side when `GET /setup` fails to connect: the card says "Sindri isn't running",
  with a **Start Sindri** button (the same start the bare `sindri` command does). No server means no `setup` events, so
  nothing else is shown.
- **Several repos:** a step that counts repos carries `items` (one line per repo with its own state), so "which repo failed"
  is answered; an `action` on a `done` step is labelled "again" or "another".
- **Accessibility:** UI §11 applies. One polite live region announces a change ("Recipes: done"); after a step completes,
  focus stays where it was; the hold and the forms follow §4 and UI §11. A `setup` row raises no OS notification, on
  purpose (only `notify.desk` does, X50); the tray shows a count badge only.

## 6. Fixed words: the step states

| State | Meaning (plain) |
|---|---|
| `todo` | Not started, or waiting on an earlier step (the `why` says which). A step that waits has `action: null`, so no button fails when pressed |
| `running` | Sindri is working; `progress {done, total}` says how far |
| `needs-you` | Only the owner can go on; `action` is the button |
| `done` | Finished; `detail` says what it did |
| `failed` | It stopped; `error {code, sentence}` and the fix are in plain words |

A step never moves backwards by itself except when its inputs change (a new repo, a new build).

**The inputs of each step** (the view reads them on request; the service only watches them and emits `setup.step.changed`):

| Step | Reads |
|---|---|
| `repo` | the approved profile snapshot and the live profile file; `repo.added`, `repo.onboarded` |
| `profile` | the live and approved profile hashes; `profile.checked` |
| `recipes` | the build's manifest, the registry, the approvals (`recipe.registered`, `recipe.approved`), the running `ApproveAllRecipes` job |
| `mcp` | `mcp.registered` (with its port), the running port, the approved snapshot |
| `map` | `context.map.*`, the `RefreshContextMap` jobs, each repo's git heads |
| `onyx` | `control.json`, the running `SetupOnyx` job, the Onyx service state |

`views rebuild` therefore rebuilds the event-fed parts; the rest is read live, so a rebuild cannot make a step flicker.
Dependencies, stated once: `repo` has none; `profile` waits for a repo; `recipes` does not wait but its previews use the
approved profile; `mcp` waits for an approved profile that names the repo; `map` waits for the same; `onyx` waits for
nothing but Docker.

## 7. The contract

### 7.1 Profile

One new optional key, left out of the canonical JSON when absent (existing approvals keep their hash):
`setup: { mcp: auto | off }` (default `auto`). `off` stops the automatic MCP registration of §5.4; the button still
works. `docs/csharp/sindri-profile.md` documents it (`check-profile-doc.mjs` enforces that).

### 7.2 Routes (SO-1, SO-2)

| Route | Slice | Class | Answer |
|---|---|---|---|
| `GET /setup` | `GetSetup` | query | `SetupView` (below) |
| `POST /setup/repos` | `AddRepo` | desk | checks 400/409, then 202 `JobStarted(accepted, JobId, links)` |
| `POST /setup/mcp` | `RegisterMcp` | desk | checks 409, then 202 `JobStarted` |
| `POST /onyx/setup` | `SetupOnyx` | desk | 202 `JobStarted` |
| `POST /onyx/account` | `ConnectOnyx` | desk | 202 (the same job); the password is a secret type |
| `POST /workflow/challenges/app` | `IssueAppChallenge` (api) | desk, `human` token only | the challenge of §4 |
| `GET /workflow/recipes/approve-all` | `GetApproveAllPlan` (engine) | query | `ApproveAllPlan` |
| `POST /workflow/recipes/approve-all` | `ApproveAllRecipes` (engine) | **protected** (app: held or typed) | 202 `JobStarted`; the job's result is `ApproveAllResult` |

`approve-all` has no `@`, so it cannot be read as a `{id}@{hash}` route; a route test pins that. All routes sit under
`/sindri`. `JobStarted` is the existing answer (`links {job, cancel, events}`). A code raised inside a job is the job's
failure code, not an HTTP status; only the checks that run before the job is claimed answer 400/409.

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
      "progress": { "done": 1, "total": 3 },
      "items": [ { "repo": "agentic-workflow", "state": "done", "detail": "Claude Code, Codex" } ]
    }
  ],
  "asOf": { }
}
```

- `steps` is always six entries in the order `repo`, `profile`, `recipes`, `mcp`, `map`, `onyx`.
- `error` is `null` unless `state` is `failed`, or `needs-you` after a failure. `action` is a catalog action id or
  `null` (it is `null` while the step waits on another). `progress` is `null` unless the step counts something. `items` is
  `[]` unless the step counts repos.
- `asOf` is the views' `asOf` object (API §2.3), as every view carries.
- Titles are fixed: Add your repo · Approve the profile · Approve the recipes · Connect your agents · Build the map ·
  Set up Onyx. A client may show them or its own. Each has a one-line gloss in the card: "Set up your repos", "Allow
  Sindri's workflows" and so on (the glosses are the app's text, UI §12.4); paths go in a secondary line and commands only in
  `cli`.

**`ApproveAllPlan`:** `{recipes: [{id, displayName, version, hash, previousApprovedHash, change: "new"|"updated"|"second-look",
changeWord, capabilities: [{id, sentence}], delta: {added: [sentence], removed: [sentence]}, changedBlocks: [id], evals:
"pass"|"fail"|"missing", children: [id]}], ownApproval: [{id, displayName, reason: "gains-capability"|"waits-for-child",
sentence, added: [sentence]}], digest, empty, asOf}`, in the order approval will happen (children first). `recipes` is
empty-by-design for anything in `ownApproval`. `digest` is the value of §5.3; the challenge is `approve-all <digest6>`.
`empty` is the sentence when `recipes` is empty and `ownApproval` too, else `null`. **`ApproveAllResult`** (the job's result):
`{approved: [{id, displayName, hash}], refused: [{id, hash, code, sentence, cli}]}`. `refused` holds the one recipe that
stopped the run (it is a list so a later version can hold more), and the recipes after it are neither approved nor listed;
the app re-reads the plan.

### 7.3 Needs-you (SO-5)

A new kind **`setup`**. Its row id is **`setup:<step>`**. `NeedsYouRowId` accepts `setup:repo`, `setup:recipes` and
`setup:mcp` only: the profile uses `profile:drift`, the map uses `failure:service:context-map-refresh`, and Onyx uses the
existing `failure:service:onyx`.

| Field | Value |
|---|---|
| `kind` | `setup` |
| `subject` | `{setup, <step>}` |
| `title`, `reason` | the step's `title` and its `error.sentence` or `why` |
| `urgency` | `pulse` when `needs-you`, `colour` when `failed` |
| `primaryAction` | the step's `action` (`AddRepo`, `ApproveAllRecipes`, `RegisterMcp`) |
| `cli` | the terminal fallback: the CLI form of the action |
| Cleared when | the step is `done` or `running` |

Rows order by urgency, then oldest first (API §3.6). A `setup` row raises no OS notification (notifications come only
from `notify.desk`, X50).

### 7.4 Catalog action ids (SO-3)

| Action id | Class | Callers | Confirm | Route |
|---|---|---|---|---|
| `AddRepo` | desk | human (cli, app) | — | `POST /setup/repos` |
| `ApproveProfile` | protected | human + terminal, **human app** | typed | `POST /profile/approve` |
| `ApproveRecipe` | protected | human + terminal, **human app** | typed | `POST /workflow/recipes/{id}@{hash}/approve` |
| `ApproveAllRecipes` | protected | human + terminal, **human app** | **held** (typed accepted) | `POST /workflow/recipes/approve-all` |
| `RegisterMcp` | desk | human; `service:setup` through `ServiceGrants` | — | `POST /setup/mcp` |
| `SetupOnyx` | desk | human (cli, app) | — | `POST /onyx/setup` |
| `ConnectOnyx` | desk | human (cli, app) | — | `POST /onyx/account` |
| `RefreshContextMap` | job | human; `service:context-map-refresh` | — | `POST /context/map/refresh` (context lane) |

Job kinds are the action ids (`AddRepo`, `RegisterMcp`, `SetupOnyx`, `ApproveAllRecipes`, `RefreshContextMap`), PascalCase
as `JobKind` requires.

CLI forms (the fallback; they ship with their units, A3, A4, A5, E3): `sindri repo add <path>`,
`sindri profile approve <hash>`, `sindri recipe approve-all`, `sindri mcp register --apply`, `sindri-app onyx up`. They
call the same routes. `sindri recipe approve-all` asks the typed `approve-all <digest6>` at the terminal.

### 7.5 Events (SO-4)

| Type | Payload (besides `commandId`) | Key | Public | Writer |
|---|---|---|---|---|
| `setup.step.changed` | `id`, `state`, `code?` | `setup.step.changed:<id>:<state>:<n>`, n = the step's count of earlier change events | yes | `service:setup` (only when the derived state differs from the last event of that step) |
| `repo.added` | `repo` (the path is non-public) | `repo.added:<commandId>` | yes | `AddRepo` |
| `repo.onboarded` | `repo`, `approvedHash` | `repo.onboarded:<repo>:<hash>` | yes | `service:setup` |
| `recipe.registration.completed` | `registered`, `unchanged`, `failed` (counts) | `recipe.registration.completed:<epoch>` | yes | `service:setup` |
| `recipe.approved` (existing) | gains `batch?` (the approve-all `commandId`), `planDigest?`, `heldMs?` | as today | yes | `ApproveRecipe`, `ApproveAllRecipes` |
| `mcp.registered` | `repo`, `port`, `providers: [{provider, outcome}]` | `mcp.registered:<commandId>` | yes | `RegisterMcp` |
| `context.map.refreshed` | `version`, `heads{repo: sha}`, `accepted`, `removed`, `dropped`, `cleanup` | context lane's | yes | `context-map-refresh` |

The event catalog's `invalidates` lists (UI E-15) add: `setup.step.changed`, `repo.added` and `mcp.registered` re-query
`setup`; `recipe.registration.completed` re-queries `setup` and the recipe list. Step durations come from
`setup.step.changed`, so the proof and the card can report time to done.

### 7.6 Errors (SO-8)

Checked against `main` and PR #433's branch on 2026-10-19: `SND-SETUP-*` is a new family; `SND-RECIPE-011` is the next free
recipe code (both have `SND-RECIPE-010` as the last); #433 owns `SND-CI-001…010` and `SND-PROFILE-017…019`, which this spec
does not use. The phase that reserves a code re-greps before merging.

| Code | Kind (status) | When | Fix line |
|---|---|---|---|
| `SND-SETUP-001` | Invalid (400) | The folder is missing, is not a git checkout, is unreadable, is inside the Sindri home, is `/`, is your home folder, or contains the Sindri home | Pick the repo's top folder |
| `SND-SETUP-002` | Conflict (409) | The repo is already in the profile, or its name collides with another repo's | Pick another folder, or give a different name |
| `SND-SETUP-003` | Forbidden (403) | The app's confirmation was not given: a hold shorter than the minimum, an answer less than 2 seconds after the challenge, or the wrong kind of answer | Hold the button for the full time, or type the text |
| `SND-SETUP-004` | Conflict (409) | `RegisterMcp` for a repo the approved profile does not name | Add the repo, approve the profile, then try again |
| `SND-SETUP-005` | Invalid (400) | `ConnectOnyx` without the Onyx admin email and password | Enter both; sign up in Onyx first if you have no account |
| `SND-SETUP-006` | Conflict (409) | `ConnectOnyx` while no Onyx setup is waiting for the login | Press Set up Onyx first |
| `SND-RECIPE-011` | Conflict (409) | A recipe is not one this build ships: approve-all names it, or a copy on disk does not match the manifest | Approve it on its own, after reading its preview |

`SND-RECIPE-009` is reused for a plan that changed between the preview and the approval (engine lane, agreed).
`SND-AUTH-002` is the refusal for a session on every action above.

### 7.7 Views that name a recipe (SO-9)

`displayName` is on `RecipeView`, each `RecipeSummary` row, and wherever an instance or item view names its recipe
(`recipe: {id, version, hash, displayName}`). It is required on a recipe file (`RCP-DISPLAY-001`, rule id chosen
because `RCP-NAME-002…006` already name the instance-name template). It is plain text, at most 60 characters, no
control characters, and is scrubbed like other agent-visible text.

## 8. Golden files and examples

Each is `Golden/Api/<path>`; one body each. Hashes and ids are shortened. **Ten bodies:** six `setup/…` and four
`recipes-approve-all/…`.

**`setup/all-done.json`**
```json
{"steps":[
 {"id":"repo","title":"Add your repo","state":"done","why":"A repo is in your approved profile.","detail":"agentic-workflow","error":null,"action":"AddRepo","progress":null,"items":[{"repo":"agentic-workflow","state":"done","detail":"Claude Code, Codex and Cursor"}]},
 {"id":"profile","title":"Approve the profile","state":"done","why":"Your profile is approved.","detail":"Approved 3f0c9a.","error":null,"action":null,"progress":null,"items":[]},
 {"id":"recipes","title":"Approve the recipes","state":"done","why":"Every recipe that ships with Sindri is approved.","detail":"26 recipes.","error":null,"action":null,"progress":{"done":26,"total":26},"items":[]},
 {"id":"mcp","title":"Connect your agents","state":"done","why":"Sindri is registered in your repos.","detail":"Claude Code, Codex and Cursor in agentic-workflow. Codex will ask you to trust this folder once.","error":null,"action":null,"progress":null,"items":[]},
 {"id":"map","title":"Build the map","state":"done","why":"Every repo has a current map.","detail":"Map v3, built at 8597ecc.","error":null,"action":null,"progress":{"done":1,"total":1},"items":[]},
 {"id":"onyx","title":"Set up Onyx","state":"done","why":"Onyx is running and Sindri can search it.","detail":"Onyx v4.9.0 on 127.0.0.1:3000.","error":null,"action":null,"progress":null,"items":[]}],
 "asOf":{"projectedAt":"2026-10-19T09:12:00Z","cursor":5120,"ledgerHead":5120,"rebuilding":false,"stalled":false,"stalledAt":null,"stalledType":null,"text":null}}
```

**`setup/first-run.json`** (a fresh box: the app has just opened)
```json
{"steps":[
 {"id":"repo","title":"Add your repo","state":"needs-you","why":"Pick the folder of a repo to start.","detail":null,"error":null,"action":"AddRepo","progress":null,"items":[]},
 {"id":"profile","title":"Approve the profile","state":"todo","why":"Waits for your first repo.","detail":null,"error":null,"action":null,"progress":null,"items":[]},
 {"id":"recipes","title":"Approve the recipes","state":"needs-you","why":"26 recipes wait for your approval.","detail":"Registered 26 of 26.","error":null,"action":"ApproveAllRecipes","progress":null,"items":[]},
 {"id":"mcp","title":"Connect your agents","state":"todo","why":"Waits for an approved profile with a repo.","detail":null,"error":null,"action":null,"progress":null,"items":[]},
 {"id":"map","title":"Build the map","state":"todo","why":"Waits for a repo.","detail":null,"error":null,"action":null,"progress":null,"items":[]},
 {"id":"onyx","title":"Set up Onyx","state":"needs-you","why":"Docker is ready. Set up Onyx when you want search.","detail":null,"error":null,"action":"SetupOnyx","progress":null,"items":[]}],
 "asOf":{"...":"..."}}
```

**`setup/running-progress.json`** (the recipes step while the `ApproveAllRecipes` job runs; the others shortened with `...` here; the file holds all six)
```json
{"steps":[
 {"id":"repo","state":"done","...":"..."},
 {"id":"profile","state":"done","...":"..."},
 {"id":"recipes","title":"Approve the recipes","state":"running","why":"Sindri is approving your recipes.","detail":"Approving Review loop.","error":null,"action":null,"progress":{"done":14,"total":26},"items":[]},
 {"id":"mcp","title":"Connect your agents","state":"running","why":"Sindri is registering itself in agentic-workflow.","detail":null,"error":null,"action":null,"progress":{"done":0,"total":1},"items":[]},
 {"id":"map","title":"Build the map","state":"running","why":"Sindri is mapping agentic-workflow.","detail":"Reading the code.","error":null,"action":null,"progress":{"done":0,"total":1},"items":[]},
 {"id":"onyx","state":"todo","...":"..."}],
 "asOf":{"...":"..."}}
```

**`setup/map-retrying.json`** (the `map` step after its first failure)
```json
{"steps":[{"id":"repo","state":"done","...":"..."},{"id":"profile","state":"done","...":"..."},{"id":"recipes","state":"done","...":"..."},{"id":"mcp","state":"done","...":"..."},
 {"id":"map","title":"Build the map","state":"running","why":"Sindri is mapping agentic-workflow.","detail":"Retrying in 5 minutes: the code index was busy.","error":null,"action":"RefreshContextMap","progress":{"done":0,"total":1},"items":[]},
 {"id":"onyx","state":"todo","...":"..."}],"asOf":{"...":"..."}}
```

**`setup/failed.json`** (the `repo` step; the other five follow it)
```json
{"steps":[{"id":"repo","title":"Add your repo","state":"failed","why":"The last folder you picked could not be added.","detail":null,
  "error":{"code":"SND-SETUP-001","sentence":"That folder is not a git repo. Pick the top folder of a repo."},
  "action":"AddRepo","progress":null,"items":[]},{"id":"profile","...":"..."}],"asOf":{"...":"..."}}
```

**`setup/profile-needs-approval.json`** (the `profile` step)
```json
{"steps":[{"id":"repo","state":"done","...":"..."},
 {"id":"profile","title":"Approve the profile","state":"needs-you","why":"You added agentic-workflow. Sindri waits for you to approve the profile before it uses it.","detail":"1 change: repos[agentic-workflow] added.","error":null,"action":"ApproveProfile","progress":null,"items":[]},
 {"id":"recipes","state":"todo","...":"..."}],"asOf":{"...":"..."}}
```

**`recipes-approve-all/plan.json`**
```json
{"recipes":[
 {"id":"workflow.step","displayName":"One step","version":2,"hash":"1a2b3c4d5e6f","previousApprovedHash":"0f1e2d3c4b5a","change":"updated","changeWord":"Updated","capabilities":[{"id":"effect.notify.desk","sentence":"Puts a question or notice in your Needs you list"}],"delta":{"added":[],"removed":[]},"changedBlocks":["step.run"],"evals":"pass","children":[]},
 {"id":"workflow.item","displayName":"Work item","version":3,"hash":"9f8e7d6c5b4a","previousApprovedHash":null,"change":"new","changeWord":"New","capabilities":[],"delta":{"added":[],"removed":[]},"changedBlocks":[],"evals":"pass","children":["workflow.step"]}],
 "ownApproval":[{"id":"workflow.ship","displayName":"Ship a change","reason":"gains-capability","sentence":"Ship a change can now open a pull request. Approve it on its own after reading it.","added":["Opens or updates a pull request and reads its checks"]}],
 "digest":"c41e07","empty":null,"asOf":{"...":"..."}}
```

**`recipes-approve-all/applied.json`** (the finished job's result)
```json
{"approved":[{"id":"workflow.step","displayName":"One step","hash":"1a2b3c4d5e6f"},{"id":"workflow.item","displayName":"Work item","hash":"9f8e7d6c5b4a"}],"refused":[]}
```

**`recipes-approve-all/refused.json`**
```json
{"approved":[{"id":"workflow.step","displayName":"One step","hash":"1a2b3c4d5e6f"}],
 "refused":[{"id":"workflow.item","hash":"9f8e7d6c5b4a","code":"SND-RECIPE-008","sentence":"Work item: its checks do not pass.","cli":"sindri recipe eval workflow.item"}]}
```

**`recipes-approve-all/empty.json`** (the plan when nothing waits)
```json
{"recipes":[],"ownApproval":[],"digest":"000000","empty":"Every recipe is approved.","asOf":{"...":"..."}}
```

Each golden is a test input for the API (serialization) and the UI (fixtures); the UI's contract tests read them, and
`Aw.Sindri.Ui.Tests` pins the catalog (the api lane's gate for P1 and P2 runs it, as the UI lane's note requires).

## 9. The setup commands

| Command | What it does | Changes in this spec |
|---|---|---|
| `./setup.sh --providers … --with-sindri` | Installs Sindri's dependencies and tools, publishes `sindri-app` **as its own named step**, then **opens the app** | The publish is reported on its own line with a time hint ("Publishing the Sindri app, about 5 to 10 minutes; it may download the .NET SDK") and its own failure sentence ("The Sindri app could not be published; see `<cache>/publish.log`"), separate from Onyx. It no longer runs Onyx by default; `--with-onyx` restores that (Q9) and `--no-onyx` is accepted and does nothing. It ends with "Still to do in the app: Set up Onyx" and opens the app (with no desktop session it prints the absolute path of the launcher, not a bare `sindri-app` that may not be on `PATH`). It does not print the MCP register command any more. It still builds the TypeScript `sindri` until the switch; the time estimate says so |
| `sindri-app onyx up` | Onyx setup at a terminal: the stack, your account, Sindri's key, the profile section | Unchanged: the terminal fallback. Not at a terminal, it prints the command instead of asking; the app's **Set up Onyx** does the same job |

**Re-running `setup.sh` is safe.** It re-checks the installs (the npm builds and the Serena image no-op when current),
re-publishes `sindri-app` only when the source changed (the stamp check), never replaces a running `serve` (it declines)
and runs Onyx only with `--with-onyx`. Its summary lists what is still to do and what failed, separately.

Daily start (`sindri-app`, bare) is not setup. On a box where Onyx is not set up it shows the Onyx step on the Setup card
and the existing `failure:service:onyx` row.

## 10. Folding in PR #433 (CI apply spec): clause by clause

#433 lets a merged, checked PR approve recipes and promote maps from CI/CD (`ci-apply`, class `ci`). Setup once removes
most of what #433 needed a CI for on a fresh box.

| #433 clause | Verdict | Why |
|---|---|---|
| §1 and §3.2 `ci-apply` service, mirror, cursor, candidates, checks, events (`ci.*`) | **Keeps**, unchanged, for recipes only | They are about recipe files merged in a repo; nothing here touches them. It is not part of the setup: it needs a forge token, mergers and a workflow, which is more setup, not less |
| §3.1 profile `ci:` block, `SND-PROFILE-017…019` | **Keeps** | No collision: this spec's profile key is `setup:` and its codes are `SND-SETUP-*` |
| §3.4 recipe approve with the capability ceiling | **Keeps** for owner-written recipes in a repo. **Conflicts** only where it says a recipe id's first approval is at the terminal: it becomes "in the app (`ApproveRecipe`, typed) or at the terminal". Shipped recipes are not CI's: they come through approve-all (§5.3) | The shipped recipes are the installed build's, approved by the owner's hold, not by a merged PR of this repo's `recipes/sindri/` |
| §3.5 map promote from CI (`promote` entries, build at a commit over the mirror, `SND-CI-008`, CI-P5) | **Supersedes** | `RefreshContextMap` builds and promotes on a default-branch move with no forge, no checks and no mergers (§5.5). CI-P5 is dropped; `promote:` entries are removed from the `ci:` block's schema, and `SND-CI-008` is retired |
| §3.5 "model proposals are left out of a CI promote" (O4) | **Conflicts** | The auto map's cleanup agent decides model proposals (accept at 0.75, ask the model below, reject if it cannot), so a promoted map can hold agent decisions. The agent runs read-only with a removal ceiling (§5.5). The owner decides (Q3) |
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
| API surface spec | §3.2 | `AddRepo`, `RegisterMcp`, `SetupOnyx`, `ConnectOnyx`, `ApproveAllRecipes` rows and the app challenge route | SO-2 |
| API surface spec | §3.3 | the `confirm` column and the three rows' status | SO-3 |
| API surface spec | §3.4 | the events of §7.5 | SO-4 |
| API surface spec | §3.6 | kind `setup`, id `setup:<step>`, the `profile` row's `primaryAction` | SO-5 |
| API surface spec | §4.1, §4.2 | `protected` has a second channel for three actions; `service:setup` and the map service | SO-6 |
| API surface spec | §6 "Protected actions in the UI" | rewritten for §4 | SO-7 |
| API surface spec | §9 | the codes of §7.6 | SO-8 |
| API surface spec | §3.8 | `displayName` on the recipe and instance views | SO-9 |
| Engine spec | §3.2 | `displayName` field, `RCP-DISPLAY-001` and the version rule | SO-10 |
| Engine spec | §4.1, §4.2 | registration at start from the manifest, approve-all as a job, the app channel | SO-11 |
| Engine spec | §6.2 | class table: `protected` rows in the app | SO-12 |
| Engine spec | §8 | `SND-RECIPE-011` | SO-13 |
| Context spec | §3 "Decisions stay human" | the service promote | SO-14 |
| Onyx spec | O3, §4.1 step 7 | the owner's login may be given in the app, in two calls | SO-15 |
| `docs/sindri/README.md` | X65 | the decision | SO-16 |
| Engine spec | §6.3 (the `recipe`/`workflow` CLI table) | the short form `sindri takeover <item-or-address>` beside `sindri handback`, as added by #448 (open at rev 2; whichever merges second keeps the line) | SO-17 |
| API surface spec | §3.2 and the engine-owned list | the same short form beside `sindri handback` | SO-18 |
| UI screens spec (rev 8) | §3 (R8-1, the page table), §4.1 Home | Home stays the default; the Setup card above Needs you, a nav entry under Home, `G U`, `sindri://setup`. Rev 7 §3.3 gets the same one-line note | SO-19 |
| UI spec (rev 7) | §6.5 (what the app never does), §7 (the protected rows and the closing paragraph), §18.1 and the E-9/E-13 row | `ApproveProfile`, `ApproveRecipe` and `ApproveAllRecipes` become app confirmations (typed or held); the rest stay `cli-only` | SO-20 |
| UI screens spec (rev 8) | the recipes page ("Re-approval is `protected`: the CLI to copy") | re-approval of one recipe is typed in the app | SO-21 |
| `.agents/rules/mcp-servers.md` | per-repo servers, the `sindri` row | the owner runs `sindri mcp register`, or Sindri registers itself after the owner approves a profile naming the repo; never user-wide, never by `setup.sh` or a provider installer | SO-22 |
| `docs/csharp/sindri-profile.md` | `setup` | the key | SO-P1 |
| `docs/csharp/sindri.md` | commands, errors, events | as phases land | each phase |
| `scripts/sindri-app.sh`, `setup.sh`, `config/lib/setup-onyx-step.sh` | headers and output | no register-command print; the publish step; the closing line; `--with-onyx` | SO-P4 |
| `deps.yaml`, `.env.example` | as phases land | the new cli edges (`claude`, `codex`, `git`) and `AW_SINDRI_APP_NO_MCP` (`check-deps.mjs`, `check-env-example.sh`) | SO-P5 |

(The review's "SO-U*" UI edits are numbered SO-19 to SO-21 here, because SO-U1 to SO-U4 are the UI lane's phase units.)

## 12. Review findings

/autoplan round 1 (2026-10-19): five lenses, all NEEDS_WORK, **19 HIGH** (product 1, arch 6, design 7, devex 4, security 1).
Resolutions are rev 2. "Open" means the owner decides (a question in §14) or a later round re-checks it.

| Lens | Verdict | HIGH |
|---|---|---|
| product | NEEDS_WORK | 1 |
| arch | NEEDS_WORK | 6 |
| design | NEEDS_WORK | 7 |
| devex | NEEDS_WORK | 4 |
| security | NEEDS_WORK | 1 |

| id | lens | severity | finding | resolution (rev 2) |
|---|---|---|---|---|
| R1-1 (C1, H1, H2, D4) | product, devex, design | HIGH | The first run contradicted itself: setup.sh ran `onyx up` and approved the profile in the terminal before the app existed; "2 commands, 0 typed" was not true; the profile step read needs-you before any repo existed | One order: setup.sh installs and opens the app; Onyx is a step in the app (`--with-onyx` keeps the terminal run); the profile step is `todo` until a repo exists; one table counts every prompt (§1, §2, §5.2, §5.6, §9); Q9, Q10 |
| R1-2 (F3, D2) | arch, design | HIGH | Approve-all was a blocking POST yet showed 14/26 | 202 + job `ApproveAllRecipes`, progress and result on the job (§5.3, §7.2, §8) |
| R1-3 (F4, F5, SS-1) | arch, security | HIGH | No plan digest; per-recipe challenges; no source for "shipped"; no capability delta | Digest defined; one challenge and an internal approval path; manifest from git HEAD embedded with the build; previous hash and capability delta per row; a recipe that gains a capability is left out (§5.3) |
| R1-4 (F2) | arch | HIGH | `displayName` re-hashes at the same version: `RecipeVersionTaken` on every existing box | The same change bumps every version; "a content change bumps the version", checked by `recipe validate` (§5.3, SO-E1) |
| R1-5 (F1) | arch | HIGH | Job kinds and keys failed `JobKind` and `JobKey`; the path leaked into events | PascalCase kinds; key = first 16 hex of sha256 of the path; the path is never in events (§5.1, §7.4) |
| R1-6 (F6, D3, SS-2) | arch, design, security | HIGH | One POST with the password could not wait for the browser sign-up; the password lived in the job record | Two calls, `POST /onyx/setup` then `POST /onyx/account`; a secret type, never stored (§5.6) |
| R1-7 (D1) | design | HIGH | Approve-all had no keyboard or assistive path | Typed `approve-all <digest6>` accepted; the key-hold specified (§4) |
| R1-8 (D5) | design | HIGH | "First screen" broke "Home is always the default"; no nav, key or deep link | A Setup card on Home; nav entry, `G U`, `sindri://setup` (§5.7, SO-19) |
| R1-9 (D6) | design | HIGH | The UI specs still said protected actions never become app buttons | UI spec edits SO-19 to SO-21 (§11) |
| R1-10 (D7) | design | HIGH | The consent list showed machine words | Plain sentences per capability, change words, grouping (§5.3.1) |
| R1-11 (H3) | devex | HIGH | The first `sindri-app` publish was unreported and hidden in the Onyx step | Its own setup.sh step with a time hint and failure sentence (§9) |
| R1-12 (H4) | devex | HIGH | Background failures silent up to ~35 minutes; no server-down state; recipe registration failure unspecified; no map retry | "Retrying in N minutes" after the first failure, `failed` after 3, `RefreshContextMap`; "Sindri isn't running" with a start action; per-recipe registration with a `failed` count (§5.3, §5.5, §5.7) |
| C2 | product | MEDIUM | Auto MCP changes `.agents/rules/mcp-servers.md`, not in the edit list | SO-22; excluded files; the off switch shown in the detail (§5.4, §11) |
| C3 | product | MEDIUM | The hold is a slip guard; the token file is a trust boundary | Capability sentences on the hold screen; Q1 says so; doctor warns on a readable token file (§4) |
| C4 | product | MEDIUM | Success was commands, not first value | Time to first value from `setup.step.changed`; a "what now" last line (§1, §7.5) |
| C5 | product | MEDIUM | A typed profile approval for something the owner just picked | Open, Q10 (recommended: keep it typed) |
| C6 | product | MEDIUM | Auto map cost uncapped | A daily cap on cleanup calls, shown in the detail; Q3 asks the number (§5.5) |
| C7 | product | MEDIUM | `displayName` forces a full re-approval | Kept: one hold, versions bumped, the card says why (§5.3); Q5 |
| C8, C9 | product | LOW | Headless boxes; the missing hook hidden behind a green step | CLI forms ship with their units (§2); a visible detail and a doctor line (§5.1) |
| F7 | arch | MEDIUM | "Derived from the ledger" was not true | Read model over named inputs; `port` on `mcp.registered`; `repo.onboarded` (SO-D11, §6) |
| F8 | arch | MEDIUM | The confirmation channel changed more than said | A separate app challenge route, `NeedsTerminal` unchanged, held answers redeem separately (§4) |
| F9 | arch | MEDIUM | Two needs-you rows for Onyx | Reuse `failure:service:onyx`, no `setup:onyx` (§5.6, §7.3) |
| F10 | arch | MEDIUM | Job-time codes listed as HTTP statuses | Cheap checks before the job; job codes are job failure codes (§5.1, §7.2) |
| F11 | arch | MEDIUM | MCP auto-registration vs the rule; tracked files; worktrees | SO-22; tracked files and worktrees stated (§5.4) |
| F12 | arch | MEDIUM | Tick-only derivation; pausable setup service; failure counting | Event-driven plus a 60 s backstop; not pausable; the service counts the last jobs (§5, §5.5) |
| F13, F14, F15, F16 | arch | LOW | Golden inconsistencies; missing edits; unattended map decisions; client-asserted hold | Dependencies stated in §6 and goldens fixed; §11 completed (deps, env); the agent's limits (§5.5); §4 keeps the honest language |
| D8 | design | MEDIUM | The hold's lifecycle | Specified (§4 item 4) |
| D9 | design | MEDIUM | One failing recipe blocks all later ones; a CLI string in `sentence` | The sentence carries no command (`cli` field); continuing past a refusal is open, Q11 |
| D10, D11, D12 | design | MEDIUM | One error for many repos; `todo` with a failing button; no checklist states | `items`; `action: null` while waiting; loading, rebuilding, restarting and server-down states (§5.7, §6, §7.2) |
| D13, D14, D15 | design | MEDIUM | No typed-path fallback; the profile preview as a bare name; undefined nouns | A typed path field; the preview in plain words with path and size; glosses under the titles (§5.1, §5.2, §7.2) |
| D16, D17 | design | LOW | Announcements and focus; no OS notification | Stated (§5.7) |
| dx-M1 | devex | MEDIUM | The link to `sindri-app` may not be on `PATH` | setup.sh prints the absolute launcher path (§9) |
| dx-M2 | devex | MEDIUM | setup.sh still builds the TS `sindri` | Said in the time note; skipping it is out of scope until the switch (§2) |
| dx-M3 | devex | MEDIUM | Pre-app exit codes; exit 3 treated as success | The summary lists "Still to do"; the Onyx step shows `SND-ONYX-001` on first open (§9, §5.6) |
| dx-M4 | devex | MEDIUM | Headless sign-up; the AddRepo job waiting on a human | The address is always in `detail`; the job ends at "added to the profile" (§5.6, §5.1) |
| dx-M5 | devex | MEDIUM | Boxes that already have Sindri; re-running setup.sh | "A box that already has Sindri" and the re-run paragraph (§2, §9) |
| dx-L1..L3 | devex | LOW | Approve-all slip guard; no quickstart; Q6 as a safety regression | Capability sentences above the hold; a quickstart is a doc task in SO-P1's PR; Q6 shows a visible detail |
| SS-2 | security | MEDIUM | The password's 202 lifetime | Secret type, no restart persistence, a sentinel test (§5.6) |
| SS-3 | security | LOW | `git` in an owner-picked folder | Fixed git flags, argv only (§5.1) |
| SS-4 | security | MEDIUM | The preview hides the path; broad folders | Real path and size in the preview; `/`, home and a parent of the Sindri home refused (§5.1, §5.2); Q8 |
| SS-5 | security | MEDIUM | The cleanup agent's authority and input | Read-only, no tools, scrubbed input, schema output, a removal ceiling (§5.5) |
| SS-6 | security | MEDIUM | The Codex trust row contradicted "Codex will ask you" | Sindri does not write the row (§5.4) |
| SS-7 | security | LOW | MCP files committed or churned | `.git/info/exclude`; write only when different (§5.4) |
| SS-8 | security | MEDIUM | The hold is client-asserted | Residual accepted and written (§4); the prover only in a scratch home; one open challenge per action; the `challenge.shown` ack is open for the owner's later round |
| SS-9 | security | MEDIUM | Which profile the MCP service reads | The approved snapshot by hash; a test with a drifted file (§5.4) |
| SS-10 | security | LOW | Approve-all audit and the service promote's attribution | `planDigest` and `heldMs` on `recipe.approved`; the Context screen shows "changed by the agent" (§7.5, §5.5) |
| X-1 | #448 review | LOW | `sindri takeover` short form missing in the specs | SO-17 and SO-18 (§11) |

## 13. Phases, with a parallel plan

Lanes: **api**, **engine**, **context**, **ui**. Up to four builders per lane. A unit is a PR. `->` is "waits for".

| Unit | Lane | What | Waits for |
|---|---|---|---|
| SO-A1 | api | **The contract:** `SetupView` and `ApproveAllPlan` types, the ten goldens, `GetSetup` with the derived projection (all steps `todo` from an empty ledger), the `setup` kind and `NeedsYouRowId` (`setup:repo`, `setup:recipes`, `setup:mcp`), the events in `EventCatalog`, `SND-SETUP-*` codes, `setup:` profile key, catalog rows (stubs), docs | — |
| SO-A2 | api | App confirmation: `IssueAppChallenge`, `confirm`, `minHoldMs` and `typedAlternative`, the held redeem, `SND-SETUP-003`, the catalog flip and its architecture-test change, doctor's token-file check | A1 |
| SO-A3 | api | `AddRepo`: the checks, route, job, the `repos[]` appender, `repo.added`, `repo.onboarded`, onboarding after approval (hook slot), `GetSetup` repo and profile steps; the CLI form `sindri repo add` | A1 |
| SO-A4 | api | MCP: `RegisterMcp` route and job, the automatic runs from `profile.approved` (snapshot) and serve start, `mcp.registered`; `sindri mcp register` keeps its flags | A1, A3 |
| SO-A5 | api | `SetupOnyx` and `ConnectOnyx`: two routes, the job's wait, the secret type and its tests; `sindri-app onyx up` unchanged | A1, A2 |
| SO-A6 | api | Setup service: the read model, `setup.step.changed`, the needs-you rows, doctor `setup`; the first-run proof in a scratch home | A1, A3, E2, C1 |
| SO-A7 | api | Publish and install: `scripts/sindri-app.sh` writes `recipes.manifest.json` and the recipe copies from git `HEAD`; `setup.sh` reports the publish as its own step, adds `--with-onyx`, opens the app, no register-command print; `.agents/rules/mcp-servers.md`; deps and env entries | — |
| SO-E1 | engine | `displayName`: the field, `RCP-DISPLAY-001`, the version bump of all 26 recipes, the rule "a content change bumps the version" in `recipe validate`, evals updated, views carry it, a plain sentence on every block descriptor | — |
| SO-E2 | engine | Registration at serve start from the manifest and `recipe.registration.completed {…, failed}`; the shipped-hash source is A7's manifest | E1, A1, A7 |
| SO-E3 | engine | Approve-all: the plan (digest, delta, `ownApproval`), the protected POST as a job, `ApproveShippedRecipe`, `batch`, `SND-RECIPE-011`, `SND-RECIPE-009` on drift; terminal verb `sindri recipe approve-all`; the `sindri takeover` short form lines | E1, A1 (and A2 for the app channel) |
| SO-C1 | context | The `map` step's inputs: `context.map.refreshed` and the failure row as the context lane specified, the retry action, the cleanup agent's limits and daily cap, the manual `context map refresh` row | A1 |
| SO-U1 | ui | The Setup card on Home with its nav entry, `G U` and deep link: six steps from `GET /setup`, states, progress, items, plain words, loading, rebuilding and "Sindri isn't running" with Start Sindri; fixtures are the goldens | goldens in A1 |
| SO-U2 | ui | The confirmations: typed (profile, one recipe) and held-or-typed (approve-all), the preview sheet in plain words, the hold's lifecycle, `SND-SETUP-003` handling | goldens in A1; A2 to run live |
| SO-U3 | ui | Add repo (folder picker and typed path, then the job's progress), Set up Onyx (the wait, the login form), Register MCP | A3, A5 to run live |
| SO-U4 | ui | `setup` needs-you rows, `displayName` on recipes and instances, the approve-all list and its plain capabilities | A1, E1 |

**Waves.** Wave 0: A1, A7 and E1 start together, and U1, U2 and U4 can start from the goldens in this spec before A1
merges (the UI lane builds against fixtures). Wave 1, once A1 merges: A2, A3, E2, E3 (terminal path), C1 and the UI
units against live data run in parallel. Wave 2: A4, A5, U3 against live data. Wave 3: A6 and the proof.

**Contract each unit waits on.** The goldens and types are A1's; no other unit changes them. The challenge shape is A2's;
U2 and E3's app path depend on it, E3's terminal path and its tests do not. `displayName` and the version bump are E1's;
every recipe hash changes there, so E1 must merge before any shipped recipe is approved in daily use (the engine spec's P2
note says the same of every hash-changing field). The manifest is A7's; E2 and E3 read it.

**Proof (A6):** on a scratch `AW_SINDRI_HOME` and `AW_STATE_DIR` (never a temp `HOME`), through the real `setup.sh` order up
to the app start: from a fresh home, start `serve`; the six steps show (`setup/first-run`); `profile` is `todo` until a repo
is added; add a repo through the route as the human; the profile step asks; a session's attempt at the same approval is
`SND-AUTH-002`; a typed confirmation approves; the MCP step writes the providers' files in the scratch repo and not the
Codex trust row; recipes are registered (26, `unchanged` on the second start); a recipe file edited on disk is not
registered and not in the plan; a held approve-all runs as a job and approves them in order (the typed answer works too); a
recipe edited between the plan and the POST gives `SND-RECIPE-009` and nothing applied; a recipe that gains a capability is
in `ownApproval`; the map step runs the refresh job and shows "Retrying" after a forced failure; a recipe with a failing eval
makes the approve-all stop at it with `refused`; the Onyx password sentinel appears nowhere. **Measure:** commands typed
from install to a working Sindri: 1 (target), against 62 today, and the time from the first app open to all six `done`.

## 14. Open questions for the owner

Each is a real decision with a recommended answer.

- **Q1. Approving in the app instead of the terminal.** The profile and the recipes are approved by typing or holding in
  the app, for those three actions only; revoke, grants, accept and a manual map promote stay terminal. The app holds the
  per-start token in a file that any program running as you can read, so this is a trust boundary for now. Is that the
  line? *Recommended: yes, exactly these three.*
- **Q2. Registering Sindri in your repos by itself.** When you approve a profile that adds a repo, Sindri registers its
  MCP server in that repo for Claude Code, Codex and Cursor on its own, and repairs it when the port changes. It does not
  trust the folder for Codex; Codex asks you once. Or should that be a button you press? *Recommended: automatic, with the
  off switch in the profile, because approving the profile already said which repos.*
- **Q3. The map without a person.** The cleanup agent decides what stays in the map (including a model's keep or
  remove), and the result becomes the current map with no review. It works read-only, never removes more than a third of
  the map in one run, and every choice can be undone in the Context screen. #433 said model guesses should wait for a person.
  Which? And what daily limit on its model calls (the step shows the day's use)? *Recommended: the agent decides, with Undo,
  the removal limit and the "cleanup: unavailable" case rejecting the guesses, as the context lane built it; it is what "no
  manual step" means. Limit: 40 calls a day to start.*
- **Q4. Your Onyx password in the app.** The app's form takes your Onyx admin login after you sign up in the browser, uses
  it once and forgets it; it is never written anywhere, and a restart asks again. The terminal command stays. Or terminal
  only? *Recommended: allow the form.*
- **Q5. After a Sindri update changes recipes.** You hold the approve-all button once more (the first time after this
  change re-approves all 26, because every recipe gets a display name and a new version). Or should an update auto-approve
  shipped recipes whose capabilities did not change? A recipe that gains a capability always waits for you on its own.
  *Recommended: hold again; a recipe can run commands, and one hold is cheap.*
- **Q6. The secret-check hook on a new repo.** The TypeScript build installs a pre-commit hook that refuses
  secret-shaped text. The C# build has no installer yet. Add the repo now without it and say so on the step (and in the
  health check), or hold the repo step until the installer exists? *Recommended: add now, say so, and install the hook when
  the installer ships.*
- **Q7. What happens to #433.** Keep its recipe half as an optional extra for recipes you write in your own repos, and
  drop its map-promote half as replaced here? *Recommended: yes.*
- **Q8. Which folders can be added.** Any git checkout your user can read, except `/`, your home folder itself, and the
  Sindri home and its parents; the app shows the real folder and its size before you approve. Or only folders under your
  home? *Recommended: any readable git checkout with those exceptions.*
- **Q9. Does setup.sh still set up Onyx at the terminal?** In rev 2 it does not: it installs, publishes the app and opens
  it, and Onyx is a step in the app. A flag (`--with-onyx`) keeps the old terminal run for a box with no desktop. Or keep
  the old default and run Onyx at the terminal first? *Recommended: Onyx in the app by default, the flag for terminal-only
  boxes. The old default made you type a login and a profile approval before the app existed.*
- **Q10. Approving a profile change you just made yourself.** Picking a folder, or setting up Onyx, changes the profile,
  and you then type the approval. That is one typed approval if Onyx runs before you approve (the preview holds both), two
  if after. Should a change you made by your own pick or button be approved by that act, or by the hold, instead of typing?
  *Recommended: keep it typed; the profile is what widens what Sindri may do, and one typed line per new repo is cheap.*
- **Q11. When one shipped recipe fails its checks.** Approve-all now stops at it, and the step says "N approved, 1 needs a
  look". Or should it carry on with the recipes that do not depend on the failing one? *Recommended: carry on with the
  unaffected ones, and show the failed one as a Sindri problem with its command to run.*

Dropped: none. Q4, Q5, Q8 are reworded for the changes above.

## 15. Not in this spec

Multi-user setup (X61), a Windows setup, installing Docker (Onyx O5), the shipped recipes' own content, importing an
authored map (#433's deferred part), changing the sandbox (SB specs), the Context screen's design beyond the Undo and the
"changed by the agent" label it already has, a `challenge.shown` acknowledgement before a held answer (a later round), and
a browser head for approvals (this channel is desktop-only).
