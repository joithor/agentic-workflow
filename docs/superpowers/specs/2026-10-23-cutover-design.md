# The cutover: the install moves from TypeScript to C# in one switch (spec)

**Status:** draft for owner review, rev 2 (2026-10-23). Docs only. The code lands in the phases of §12.

**Rev 2 (2026-10-23), /autoplan round 1.** Fixed: the install and the removal script now run in one safe order
(publish, then clean up and delete the TS judge state, then link; §2, §7.1, §8), so no window has the C# `judge` reading the
TS database or `setup.sh` stopping half switched; the v3 hook bakes the stable `~/.local/bin/sindri`, not a stamped
directory (§6.2); the TS repo list is read from `$AW_STATE_DIR/profile/repos/*.yaml`, not a path that does not exist (§6.2, §8);
the C# `scrub --staged` also serves v2 and v1 hooks unchanged, so a missed repo costs one stderr line, not a refused commit
(§6.2); §9 no longer says the PR comes after the flip; the switch edits to `setup.sh` land in P3, not P1; the post-switch
profile and onboarding step and the printed record of what was deleted are specified (§8, P4); installed `prompt-sort` and
`trait-check` registrations are removed (§7.4); `reflect` and `correct` go on `DEPRECATED_SKILLS` (§9, §15); the statusline keeps
its judge segment without the scorer (§10); `ui-evidence` is granted per verb, not as a whole (§3.1); script exit codes and
flags are defined (§7.1, §8.1); more consumers of `sindri-app` and the TS packages are listed (§9).

**Decision it implements (owner, 2026-10-20 and 2026-10-23):** the toolkit's install becomes the C# build. Plain `sindri`
is the C# app (today `scripts/sindri-app.sh` and the name `sindri-app`). `setup.sh` and the provider installers install
the C# binaries and **remove** the TypeScript packages (`sindri/`, `judge/`, `scorer/`, the TS of `skills/ui-evidence`,
the TS of `skills/bugFixOrchestrator`) and their installs. The switch is one-way: no rollback, no `--runtime` selector, no
state archive tooling. The hook contract stays green. The scorer and the TS Sindri commands that have no C# version are
**cut**; they come back later as C# recipes built from scratch (§14).

**The owner's main care (2026-10-23):** the `ui-evidence` skill and the Sindri recipe's evidence steps run **the same
code**. §3 specifies one C# Evidence path and maps every SKILL.md command to its C# verb, with the gaps.

**Supersedes:** `2026-10-09-agent-readiness-design.md` §4c (the reversible switch, `--runtime cs|ts`, the TS state
archive, the 14-day TS tag, "the always-on box first, the laptop a day later"). **Amends:** see §15.

**Evidence.** Every file:line below was read on `origin/main` (75428215d) on 2026-10-23. `EV` is `docs/csharp/evidence.md`.
Where a claim is not verified in code, the text says so.

## 1. Goal, and what is cut

**Goal.** After the switch a box has no Node-built toolkit package, no `scorer`, no TS `sindri`, and one install path:
the C# binaries published by one script. Nothing the hooks, skills or recipes call is missing.

| Item | Decision | Why |
|---|---|---|
| `scorer` (reports, `live`, `probe`, `audit`, launchd jobs) | CUT | owner 2026-10-23; rebuilt inside Sindri later (§14) |
| TS `sindri evolve`, `scope`, `observe`, `shape`, `profile explain` | CUT | owner 2026-10-20 (`project_evolve_scope_observe_recipes`); recipes later |
| TS `sindri profile migrate`, `repo onboard --template`, `channel`, `--schema-version` | DROP | no C# profile migration (no TS state is migrated); channels were the TS installer's; sindri-cmds.md rows 17, 27, 32, 34 |
| skills `correct`, `reflect` | REMOVED | they only drive `sindri evolve` (`skills/correct/SKILL.md:5,28`, `skills/reflect/SKILL.md:5,29`) |
| judge `prompt-sort`, `trait-check`, `label`, `adjudicate`, `eval`, `import-turns` | CUT unless Q2 says otherwise | no C# verb; §5 |
| `mods/aw-live` (the /live pane) | UNINSTALLED at the switch unless Q4 says otherwise | it only reads `scorer live` (`mods/aw-live/hooks/register.tsx:39-72`) |
| Evidence script features (scroll, drag, press, ...) | PORTED unless Q1 says otherwise | §3.3 |

## 2. What the switch is

One-way, in this order, each step proven before the next phase (§12):

1. Close the C# gaps (§3 to §6). Nothing installs yet.
2. Land `scripts/install-cs.sh` (§7) and `scripts/cutover-clean-ts.sh` (§8) as standalone scripts, proven on a scratch
   box (P2). Nothing calls them yet: until the switch, nothing in `setup.sh`, the providers, MCP registration or launchd
   points at the C# build (AGENTS.md, "C# build"). Unit 5.1 writes the scripts and their test only; the `setup.sh` and
   provider edits that call them are part of the switch PR.
3. **The switch PR** (§9): `setup.sh` and the provider installers call the two scripts, and the same PR deletes the TS
   directories, CI jobs, gates, suites and docs. Merging it changes no box by itself; any checkout that pulls it and runs
   `./setup.sh` flips that box (the owner's boxes included: P4 is the planned run).
4. **The flip:** the owner pulls main and runs `./setup.sh` once on each box, right after the pull. That run publishes the
   C# binaries, cleans up the TS install and links the C# binaries (§8). Until it runs, the installed hooks (copied files)
   and the TS wrappers keep working, but the skills are linked from the checkout, so a pulled `ui-evidence` or
   `bugFixOrchestrator` skill already names binaries that exist only after the run.

There is no step that restores TS. A box that needs TS again checks out an older commit and runs that commit's
`setup.sh`; this spec builds nothing for it. The C# build keeps no byte or state compatibility with TS (AGENTS.md,
"C# build"); the exception stays the hook contract (§10).

## 3. Evidence: one code path

### 3.1 The design

The only implementation of UI, API and DB evidence is `Aw.Evidence.Features` (with `Core` and `Infrastructure`). It is
reached **only** through the `ui-evidence` binary (`Aw.Evidence.Host`, AssemblyName `ui-evidence`, framework-dependent;
evidence.md:7, :90).

- **The skill** runs `ui-evidence <verb>` from a Bash call. SKILL.md is its instructions.
- **The recipe's evidence agent steps** run the same binary. `step.evidence.ui-run`, `api-run` and `db-run` are Agent
  runtime steps (`EvidenceSteps.cs:21,24,27`); the plan steps are too (`:12,15,18`). The engine launches an agent session
  with a brief (`AgentLaunches.RequestFor`), and the agent runs the CLI.
- **The permission.** `AgentLaunches.cs:12` defines `_evidence = ["Bash(ui-evidence:*)"]` but adds it only in
  `EvaluatorRequestFor` (`:74`). `RequestFor` (`:57`) gives a normal agent step `_report` and, for write scope, `_commit`.
  The cutover adds grants to `RequestFor` **per verb**, not `Bash(ui-evidence:*)`: a step gets the verbs its brief names
  (`ui-evidence run`, `parity`, `api-run`, `db-run`, `provenance`, `doctor` for the run and plan steps; `gh-publish` only
  for a publish step). A blanket grant would also give `--allow-writes`, a non-local `--host` and `gh-publish` to every
  step. Without the grants the recipe's run step cannot run (evidence.md:64). The skill's own `allowed-tools` (§3.5) keeps
  the blanket `Bash(ui-evidence:*)`, since the human drives that session.
- **The brief.** The step brief says "follow the ui-evidence skill" and names the verb and its arguments. The skill text
  is the step's instructions, so one text describes both.
- **The deterministic steps.** `step.evidence.none` and `step.evidence.combine` are Block runtime with no `IBlockStep`
  class today (evidence.md:60). They become `IBlockStep` implementations that read summary files by the file contract:
  schemas `ui-evidence.run/1`, `evidence.api.summary/1` and `evidence.db.summary/1`. `ProveCollectStep`
  (`ProveCollectStep.cs:25-27`) already reads `fact:evidenceSummary` that way.
- **No project edge.** Sindri gets no `compose` or `project` edge to `Aw.Evidence.*`. Reasons: it would pull Playwright and
  Npgsql into the Sindri host (and block an AOT split of other heads, evidence.md:75); a second composition of
  `IMachineLocks` and `IJudge`; and the Block runtime's 2-minute `MaxDuration` cannot hold a browser run that
  "outlasts 10 minutes" (evidence.md:83). The declared `Sindri -> Evidence` edge stays `kind: file` (heavy-job lock slot,
  `deps.yaml:~342`).
- **Rejected for now:** an `IUiEvidence` port in Sindri Core, a `UiEvidenceTool : IUiEvidence` adapter in Sindri
  Infrastructure that spawns `ui-evidence` (the `AwPacketsTool` pattern, `AwPacketsTool.cs:17`), and a `Sindri -> Evidence
  kind: cli` edge. It would also keep one implementation. It is worth building only when a non-agent Block step must run a
  check. Exit mapping for that day: 0 pass, 2 check failed (a result), 3 review pending, 4 run errored, 75 lock busy
  (park), 1 refused (evidence.md:82).

**Q6. How do the recipe's evidence run steps reach Evidence?**
Recommended: agent steps run the `ui-evidence` binary, briefed from the skill text (the descriptors already say Agent).
Alternative: Block steps through an `IUiEvidence` port and CLI adapter (one implementation either way; costs a `cli` edge and
a port, and the 2-minute block bound has to be lifted for it).

### 3.2 Command map: skill command to C# verb

SKILL.md = `skills/ui-evidence/SKILL.md`. Line numbers are SKILL.md unless marked. "Verdict" is equivalent, partial,
missing, or C# only. The C# verb table is `CommandTable.cs:15-18,54-70`.

| # | Skill says (line) | TS entry | C# verb | Verdict |
|---|---|---|---|---|
| 1 | `bash scripts/doctor.sh` (L10) | `scripts/doctor.sh`, `settings-files.ts` | `ui-evidence doctor` (`DoctorCommand.cs`; EV L31, L534-560) | partial: no worktree-slot checks (G-d) |
| 2 | spawn `qa-runner` to write a script (L11) | `script-schema.ts` | none; input is `UiScript` (EV L82-115) | the C# schema is a strict subset (§3.3) |
| 3 | `with_stack_lock_and_heavy_job_lock 120 node dist/bin.js <script> <run-dir> [--baseline] [--app-build] [--fixtures] [--cache]` (L12) | `cli.ts`, `run-script.ts` | `ui-evidence run <script.json> <run-dir> [--baseline] [--app-build] [--fixtures] [--cache] [--host] [--allow-preview-host] [--allow-writes] [--lock-timeout s]` (`RunScriptCommand.cs:12-14`) | equivalent; takes both locks itself (EV L469-495), so the bash wrapper goes; exits add 4, 75, 130 |
| 4 | `checkDbProvenance` before Linear upload (L13) | `db-provenance.ts` | `ui-evidence provenance` (`CheckProvenanceCommand.cs`) | equivalent; SKILL must name the verb, not an internal function |
| 5 | visual verdict, cache, `judge visual-critique` (L14) | `visual-gate.ts`, `verdict-cache.ts`, `visual-critique.ts` | inside `run` (`Core/Visual/VisualGate.cs`, `VerdictCache.cs`) | partial: judge is `UnavailableJudge`, verdict stays `unchecked` (G-c) |
| 6 | `node dist/bin.js gh-publish <run-dir> --repo --pr --db-provenance seeded --reviewed f,f` (L23) | `gh-publish-cli.ts`, `github-publish.ts` | **none** | missing (G-b): no hit for `gh-publish`, `gh api` or `ui-evidence/pr` under `src/Evidence` or EV |
| 7 | text-only PR comment via `renderPrComment` from `dist/publish.js` (L24) | `publish.ts` | `ui-evidence publish-plan <run-dir> --provenance seeded\|unknown [--linear-issue id]` (`PublishPlanCommand.cs:12`) | equivalent; flag name differs (G-g) |
| 8 | `parity <manifest> <run-dir> [--host --allow-preview-host] [--allow-writes] [--app-build] [--db-provenance ...]` (L56-60) | `parity-cli.ts`, `parity-run.ts` | `ui-evidence parity` (EV L25) | equivalent; exits add 75, 130 |
| 9 | `parity-plan <run-dir> --reviewed f,f` (L72) | `parity-cli.ts` | `ui-evidence parity-plan` (EV L26), exit 0/3/1 | equivalent |
| 10 | helpers `buildPutCommand`, `selectStale`, `renderLinearComment`, `renderParityPrComment` (L73) | `parity-publish.ts` | `parity-put --upload f --file png`, `parity-stale --attachments f --keep ids`, `parity-comment <run-dir> --for linear\|github` (EV L27-29) | C# is better: real verbs; L73-76 are rewritten |
| 11 | Linear MCP attachments (L74-76), `gh pr comment` (L19, L24), `gh api -X DELETE` of the evidence branch (L26) | agent tools | n/a | unchanged; the agent's, not the binary's |
| 12 | selector repair via `judge ui-element-repair` (L90) | `repair.ts`, `judge-exec.ts` | inside `run` through `IJudge.RepairSelectorAsync`; rules in `Core/Steps/RepairRules.cs` | partial: judge not wired (G-c) |
| 13 | lint for sloppiness (frontmatter L3) | `lint-page.ts` | `Core/Lint/PageLint.cs`, inside `run` | equivalent |
| 14 | `npx playwright install chromium` (implicit) | node playwright | `ui-evidence install-browser` (`PlaywrightInstaller.cs:20-28`) | equivalent |
| 15 | locks: `with_stack_lock_and_heavy_job_lock 120` (L12, L56) | `scripts/lib/locks.sh` | built into `run`, `parity`, `api-run`, `db-run` (`DirectoryLocks.cs`); interoperates with `config/lib/locks.sh` | equivalent |
| 16 | (no text) | none | `api-run`, `db-run` (EV L23-24, L167-240) | C# only; the skill needs text (G-f) |

Also changed and not a verb: `summary.json` has a new schema (EV L626-640: `startedAt`/`finishedAt` for `ts`, `visual`
is an object, no `steps[].name`). The run dir must be `$AW_STATE_DIR/ui-evidence/runs/<id>`; a stable `--cache` with
`--app-build` gives cache hits (EV L601-607).

### 3.3 The gaps, and what closes each

| Gap | What is missing | Closed by |
|---|---|---|
| G-a | script features: actions `scroll`, `drag`, `press`; step `index`, `to`, `grabOffset`, `dropOffset`, `holdAlt`, `key`; target prefixes `role:<role>\|<name>`, `text:<text>`, a trailing `#<n>`; `expectedState` kinds `url-search`, `testid-absent`; script-level `login` and `localStorage`; the write-guard keyword rules for the new actions. `StepAction` has only `goto\|click\|fill\|expect-visible`; unknown keys are refused (evidence.md:30-35; `Core/Scripts/StepAction.cs`, `Browser/Locator.cs`, `Guard/StepGuard.cs`; `LoginSpec` exists only in the parity manifest, `Core/Design/LoginSpec.cs`) | Q1; unit 1.1 |
| G-b | `gh-publish`: push PNGs to the branch `ui-evidence/pr-<n>`, write `pr-comment.md`, exit 0/1/3/4; refuses without `--reviewed` and a provenance value, as the TS verb did | Q7; unit 1.2 |
| G-c | `JudgeCliJudge : IJudge` in `Aw.Evidence.Infrastructure/Judging`, replacing `UnavailableJudge` in `Composition.cs`: spawn `judge <question>` with JSON on stdin, parse the envelope. The contract is on main (`docs/csharp/judge.md:176-177`: `ui-element-repair` input `{brokenSelector, step, candidates:[...]}`, outputs `repaired(+chosenIndex)\|no-good-candidate`, threshold 0.6, 10 s; `visual-critique` input `{afterScreenshot, evidenceDir, baselineScreenshot?}`) | unit 1.3 |
| G-d | `doctor` lacks the slot checks (`UI_EVIDENCE_SLOT_*`, `UI_EVIDENCE_WORKTREE`, foreign-checkout refusal, printing `--host` and the slot DB; SKILL L28-36); `run` and `parity` read no env file, so the SKILL "Configuration" section (L102-139) is wrong for C# (evidence.md:558-560) | unit 1.4: a loader for `deployment.env` and `.ui-evidence.local.env` in `run` and `parity` (plain `KEY=value` lines parsed, never sourced; an allowlist of keys; a variable already set in the environment is never overridden), and the doctor checks |
| G-e | per-artifact `commit` and `at` (DTC9, `EvidenceSteps.cs:7`): the C# summary has `appBuild` and `finishedAt` but not clearly a per-artifact `commit`. **Not verified in code** | unit 1.4 first checks, then adds if missing |
| G-f | SKILL.md rewrite (§3.5) and the text for `api-run` and `db-run` | unit 1.5 |
| G-g | `--db-provenance` (gh-publish, parity) against `--provenance` (publish-plan) | one name across the verbs. Recommended: `--provenance` everywhere, with `--db-provenance` accepted on `parity` for one release only if a caller needs it; decided in unit 1.4 |

**Q1. The script features of G-a.**
Recommended: port them to C#. They were added for a real app's flows, and the skill text and the live qa-runner depend on them.
Alternative: cut them from SKILL.md at the switch and tell qa-runner the C# subset (four actions, three target kinds).

**Q7. How does `gh-publish` talk to GitHub?**
Recommended: through the `gh` CLI, with the runner style Evidence already has (`ProcessCliRunner`), behind a port with a
fake (git data API calls: blob, tree, commit, ref on `ui-evidence/pr-<n>`).
Alternative: `Aw.Forge`'s GitHub adapter. Forge's adapter is documented read-side (evidence.md gap list), so this needs
write methods and Forge's credential rules first.

### 3.4 The skill and the recipe stay one path

Two rules keep it so, both checked by a test in phase 1:

1. **No second implementation.** A repo check (a bash test in `scripts/tests/`) fails when any file outside
   `src/Evidence/` spawns Playwright, or when SKILL.md or a recipe brief names a command other than a `ui-evidence` verb for
   running a check.
2. **One text.** The brief of each evidence agent step is generated from, or quotes by file path, the skill's verb lines.
   Unit 1.5 adds a test that the verbs named in the briefs are exactly verbs in `CommandTable.cs`.

### 3.5 SKILL.md rewrite: lines to change

| Where | Change |
|---|---|
| L10 | `bash scripts/doctor.sh` becomes `ui-evidence doctor` |
| L12, L56 | drop `with_stack_lock_and_heavy_job_lock 120 node dist/bin.js`; run `ui-evidence run ...` and `ui-evidence parity ...`; exits 0/1/2/3/4/75/130 |
| L13 | name `ui-evidence provenance` instead of `checkDbProvenance` |
| L14 | verdict text matches `visual` as an object and the `unchecked` rule |
| L23 | `gh-publish` becomes `ui-evidence gh-publish` (after G-b), flag names per G-g |
| L24, L73 | no `node` import of `dist/publish.js` or `parity-publish`; use `publish-plan`, `parity-put`, `parity-stale`, `parity-comment` |
| L42-96 | the script schema section follows the C# schema (G-a, or the trimmed subset per Q1) |
| L90 | repair through `run` (the judge adapter, G-c) |
| L102-139 | "Configuration": the env-file loader (G-d) or the plain environment |
| new | `api-run` and `db-run` sections (`--at base\|head`, `--host`, targets from `<repo>/.agentic-workflow/evidence.json`, `summary.json` on stdout) |
| install | `npx playwright install chromium` becomes `ui-evidence install-browser`, run once by the installer (§7) |
| run dir | pass `$AW_STATE_DIR/ui-evidence/runs/<id>` |
| frontmatter | `allowed-tools` loses `node`, `npm`, `npx`; gains `Bash(ui-evidence:*)` |

`skills/ui-evidence/scripts/` (`doctor.sh`, `lib/locks.sh`) is deleted when nothing references it (EV "At the switch",
L599-625); `config/lib/locks.sh` stays.

## 4. BugFix: the skill onto `src/BugFix`

### 4.1 The state of C# today

C# `bugfix-state` cannot finish a ticket. Its `judge` and `run-ui` verbs are wired to stubs that always refuse with exit 3:
`src/BugFix/Aw.BugFix.Infrastructure/UnavailableJudge.cs:12-17` (AW-BUGFIX-016) and `UnavailableUiEvidence.cs:12-17`
(AW-BUGFIX-017). `src/BugFix/aot-smoke/expected.txt:18` pins the refusal. Only `--unresolved` reports work (bugfix-judge.md
§0.1). The judge contract now exists (`docs/csharp/judge.md`), and so does the Evidence CLI; the adapters were not written.

### 4.2 Command map

Line numbers are `skills/bugFixOrchestrator/SKILL.md`. TS is `src/cli.ts` (dispatch :74-121) and `commands.ts`; C# is
`src/BugFix/Aw.BugFix.Host/Commands/*` (`CommandTable.cs:11-26`).

| Skill step (line) | Command | C# verb | Verdict |
|---|---|---|---|
| Resume (46) | `resume --state` | `ResumeCommand` | equivalent |
| Intake (70) | `init --state --ticket` | `InitCommand`; also makes `digest.key` | equivalent |
| Investigate (76) | `advance investigate --evidence` | `AdvanceInvestigateCommand` | equivalent |
| Reproduce test (114) | `run-test --state --check <f> --cwd <d> -- <cmd...>` | `RunTestCommand`; stdout adds `outcome` (passed/failed/broken); stricter command rules (runner allowlist, env freeze, clean tree) | equivalent, stricter |
| Reproduce ui (101) | `run-ui --state --check <script.json> --cwd` | `RunUiCommand` into `UnavailableUiEvidence` | **missing** until §4.3 (a) |
| Reproduce (119) | `advance reproduce --evidence --check --cwd` | `AdvanceReproduceCommand` | equivalent |
| Fix (131) | `start-attempt --mode A\|B\|C` | `StartAttemptCommand`; adds `--abandon-judge` | equivalent, one extra flag |
| Fix (167) | `record-candidate --branch --cwd [--hypothesis n] [--allow-test-changes]` | `RecordCandidateCommand` | equivalent (mode B needs `--hypothesis`) |
| Evaluate (139, 185) | `status --state`; `record-run <cand> --evidence` | `StatusCommand`, `RecordRunCommand` | equivalent |
| Evaluate (187) | `judge <cand> --state` | `JudgeCandidateCommand`; `{candidate,decision,decisionId,reasonCode,reasons,status}` | partial: verb present, adapter is a stub |
| Report (209) | `advance report (--candidate <id> \| --unresolved)` | `AdvanceReportCommand` | equivalent; the resolved path needs the judge adapter |

### 4.3 What closes the stubs

(a) **UI evidence adapter.** BugFix's own Infrastructure gets an adapter for its UI-evidence port that spawns the
`ui-evidence` binary (`ui-evidence run <script> <run-dir> --app-build <commit>`, the C# verb of §3.2 row 3), resolves it by
`AW_UI_EVIDENCE_BIN` else PATH, and maps exits (0 pass, 2 check failed, 4 errored to `broken`, others refused). So bugfix,
the skill and the recipe reach the same Evidence code.

(b) **Judge adapter.** BugFix's own Infrastructure gets an adapter for its judge port that spawns the `judge` binary
(`judge resolution-check` with JSON on stdin, then `judge why <id>`), bounded to 5 minutes as TS did
(`deps.ts:43-45,63-74`). It resolves by `AW_JUDGE_BIN` else PATH. It reads the same judge state as the hooks, so
`AW_STATE_DIR` must match (or `AW_JUDGE_STATE_DIR`).

(c) **Two mismatches, resolved here.**
- *Input digest.* The port expects "the SHA-256 of the input file" (`Core/Model/JudgeAnswer.cs:12`; bugfix.md:236-239). The
  judge stores `input_digest` as the first 16 hex of SHA-256 over its canonical re-serialization (`Aw.Judge.Core/Evaluation/InputJson.cs:74-75`).
  Resolved: the adapter writes `judge-<cand>.json` in the judge's canonical form and compares the 16-hex prefix; the
  port's doc and `JudgeAnswer` wording change to "the judge's input digest (16 hex)". The TS helper did the same with a
  16-char digest (`commands.ts:442`).
- *Check kind.* BugFix serializes `CheckKind` as `test` or `ui` (`Core/Model/CheckKind.cs`); the judge's `resolution-check`
  takes `ui-evidence`, `api-evidence`, `db-evidence`, `test`. The adapter maps `ui` to `ui-evidence`. BugFix has no verb for
  api or db checks, so those kinds stay unreachable from the skill; adding `run-api`/`run-db` is out of scope (§14).

(d) **Install and the skill.** `bugfix-state` (NativeAOT, no SQLite) goes on PATH (§7). The skill drops
`BFS_JS="$TK/skills/bugFixOrchestrator/dist/bin.js"` and every `node "$BFS_JS"` (SKILL.md:31-44), the `npm ci && npm run build`
fallback, and `allowed-tools` `Bash(node *)`, `Bash(npm *)`, `Bash(npx *)`, `Bash(BFS_JS=*)`, `Bash(TK=*)` (L5); it gains
`Bash(bugfix-state:*)`. `STATE` becomes `$AW_STATE_DIR/bugfix/<repo-slug>/<ticket-slug>` (bugfix.md:17, `deps.yaml:224`).
The Setup block's `locks.sh` and `doctor.sh` lines follow §3.5. Sindri already finds the binary by `AW_BUGFIX_BIN` else
`bugfix-state` on PATH (`BugFixStateTool.cs:11`).

(e) **Text the skill must absorb.** Exit codes: C# adds 2 (usage) beside TS's 0, 1, 3 (the skill branches only on 3, L20).
New refusals the skill does not mention: a dirty tree or untracked files on `run-test`, `run-ui`, `record-candidate`,
`record-run` (AW-BUGFIX-020); command classification (AW-BUGFIX-012); the env freeze (`AW_BUGFIX_CHECK_ENV`); a busy
`state.lock` (AW-BUGFIX-021, `AW_BUGFIX_LOCK_WAIT_SECONDS`); refusal while `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` is set. The
run timeout stays 30 minutes (`AW_BUGFIX_CHECK_TIMEOUT_SECONDS`). Open bug fixes restart at the switch (C# never reads TS
state, bugfix.md:205).

## 5. Judge: every caller onto `src/Judge`

### 5.1 Callers and coverage

| Caller | Call | C# |
|---|---|---|
| `config/hooks/send-gate.sh:114-116` | `judge wake-gate` | covered |
| `config/hooks/scope-gate.sh:63-65,74` | `judge brief-scope`, `judge brief save` | covered |
| `config/hooks/done-gate.sh:148-152,182-184`, `done-gate-annotate.sh:16` | `judge ask-check` (exit only), `judge brief get` | covered (documented exception: exit 0 = ask) |
| `config/hooks/subagent-start-map.sh:25-30` | `brief map-by-name`, `map-by-dispatch`, `set-agent-id` | covered |
| `config/hooks/judge-health.sh:6-14`, `config/statusline.sh:116-132` | `judge health` (`find_bin`: PATH, else `~/.local/bin/judge`) | covered |
| `config/hooks/prompt-sort.sh:38,53` | `judge prompt-sort` | **GAP** (J2 not built; unknown question exits 64, the hook prints nothing) |
| `config/hooks/trait-check.sh:62-75` | `${AW_JUDGE_BIN:-judge} trait-check` | **GAP** (no verb) |
| `skills/judge/SKILL.md` | `health`, `why`, `undo`, `approve`, `config get`, `config set <q> enabled\|threshold` | covered |
| same skill | `label import\|outcomes\|set\|status`, `adjudicate`, `prompt-sort why\|scaffold\|promote`, `config set prompt-sort ...` | **GAP**; `config set prompt-sort` fails `unknown question` |
| `src/Evidence` (was `skills/ui-evidence/src/judge-exec.ts`) | `ui-element-repair`, `visual-critique` | questions exist; adapter §3.3 G-c |
| `src/BugFix` (was `skills/bugFixOrchestrator/src/deps.ts:63-74`) | `resolution-check`, `why` | questions exist; adapter §4.3 |
| `src/Sindri/Aw.Sindri.Infrastructure/Judge/JudgeCliChoice.cs:24` | `judge choose`; `AW_SINDRI_JUDGE` else PATH | covered (C# only) |
| `scripts/install-{done-gate,scope-gate,trait-check,wake-gating,external-write-guard}.sh` | `command -v ${AW_JUDGE_BIN:-judge}`; refuse when missing | works with any `judge` on PATH; the message text names `install-judge.sh` |
| hook-written files (`judge/sessions/<sid>.json`, `judge/outbox/<sid>.jsonl`, `judge/teammate-names.jsonl`) | written by bash hooks, not the judge | frozen (§10) |

Judge verbs in C#: `health`, `brief save|get|map-by-name|map-by-dispatch|set-agent-id`, `why`, `undo`, `approve`,
`config get|set`, and nine questions (`QuestionCatalog.cs:11-19`). Exits: 0 ok, 1 error, 2 escalated (and `ask-check`
continue), 64 usage.

### 5.2 The cut

**Q2. `prompt-sort`, `trait-check`, `label`, `adjudicate`, `eval`.**
Recommended: cut them from the cutover. The installers stop installing the `prompt-sort` UserPromptSubmit hook and the
`trait-check` lever; `install-prompt-sort.test.sh` and `install-trait-check.test.sh` go; the `/judge` skill text loses
the `label`, `adjudicate`, `prompt-sort` and `config set prompt-sort` sections and its pointers to `judge/src/questions/`
and `scripts/install-judge.sh`; `config/hooks/prompt-sort.sh` and `trait-check.sh` are deleted. Rebuilt later if wanted. Boxes that already have them
installed keep a registered `aw:prompt-sort` / `trait-check` entry (Claude settings, Codex and Cursor hooks files) and a hook
copy that calls a script that no longer exists: the provider installers remove those registrations and copies when they run
(unit 3.1; §7.4). No unmerge helper for hook entries exists on main today (`providers/lib.sh` only removes retired dirs), so
unit 3.1 writes it and tests it per provider.
Alternative: port `prompt-sort` before the switch (J2: a question, its config block `promptSort`, the `prompt_sort_*`
tables, `why|scaffold|promote`), and cut only `trait-check`, `label`, `adjudicate`, `eval`.

### 5.3 Judge state

The C# judge uses `$AW_STATE_DIR/judge/decisions.sqlite`, the same path the TS judge writes. The C# migration runner
refuses a file that has tables but no matching `schema_version` (AW-DATA-009, `Aw.Kernel.Data/MigrationChecks.cs:45`; the TS
file has `decisions`, `failures`, `briefs`, `decision_details`, `eval_items`, `labels`, `prompt_sort_*`). Every hook would
see judge exit 1 and fail open. So the switch **deletes** `judge/decisions.sqlite{,-shm,-wal}` and `judge/config.json`
(one-way, no archive) and **keeps** `judge/sessions/`, `judge/outbox/` and `judge/teammate-names.jsonl`. The deletion runs
**before** the C# `judge` is linked (§8 step 5, then the link at step 6), so no hook ever sees the C# `judge` over the TS database.
The only guard beyond that order is the generic migration refusal (AW-DATA-009); the readiness spec's "refuse to start,
excluding the three hook files" check is **not found in `src`** (verified), so it is not a net this spec relies on.

The `scorer` read of `judge/decisions.sqlite` (`scorer/src/live-judge.ts`) goes with the scorer.

## 6. Sindri: the TS commands against the C# app

### 6.1 What must exist in C# before the switch (KEEP-needed)

| TS verb | C# today | Needed |
|---|---|---|
| `profile init`, `validate`, `approve` | ported (`CommandTable.Groups.cs:11-13`); no `--ring0/--plans/--dir/--force` | docs change (`docs/sindri/README.md:263`); nothing to build |
| `doctor` | ported, new checks | per-repo `pre-commit:<repo>` line (§6.2) |
| `index build\|status\|query\|setup` | service `code-index-main-move` (no verb by design, code-index spec CI1); `code-index status\|symbols\|callers\|callees` | message text only; the graphify pin moves (§6.3) |
| `repo add`, `repo onboard`, `repo status [--nudge]` | ported; `onboard` hook slot is `NoSecretCheckHook` (installs nothing) | the real `IRepoHooks` (§6.2) |
| `ledger` | ported (`--after`, no `--since`) | none |
| `scrub` (stdin, `--staged`, `--install-pre-commit [--repo]`) | **none** (`Aw.Redaction` is the engine; no verb) | build it (§6.2) |

### 6.2 `scrub` and the pre-commit hook (the blocker)

**The hazard.** Repos onboarded by TS have `.git/hooks/pre-commit` with marker `# sindri-pre-commit v2` (or legacy
`# sindri-scrub-pre-commit v1`) and an absolute `SINDRI='<~/.local/bin/sindri>'` baked in (`sindri/src/scrub/commands.ts:31-65`).
It runs `"$SINDRI" scrub --staged || exit 1`, then `"$SINDRI" shape --record --staged || true`. Once `sindri` is the C#
binary and has no `scrub`, the verb is unknown, exits 2, and the hook refuses **every commit** in each onboarded repo. With
`scrub` built (item 1), the v2 and v1 hooks work unchanged: `scrub --staged` answers them, and the `shape --record` line fails
as an unknown verb but is ignored (`|| true`), costing one stderr line per commit. So a repo the removal script cannot reach
is noisy, not blocked.

**Build in C# (a slice in `src/Sindri`, with `Aw.Redaction` as the engine):**
1. `sindri scrub` reads stdin and writes the redacted text; `sindri scrub --staged` reads `git diff --cached --unified=0`
   (added lines only, as `parseAddedLines` did, `commands.ts:80-120`) and exits 1 on a finding; `sindri scrub
   --install-pre-commit [--repo PATH]` installs the hook. Keep the error codes: `SND-SCRUB-002` (a staged secret, exit 1;
   `scripts/sindri-guard-proof.sh` greps it), `SND-SCRUB-003` (a foreign hook is present), `SND-SCRUB-004` (not a repo),
   `SND-SCRUB-005` (git diff failed). It resolves the hook path with `git rev-parse --git-path hooks/pre-commit` and respects `core.hooksPath`.
2. A real `IRepoHooks` replaces `NoSecretCheckHook`; `repo onboard` calls it only after approval, as before, and the
   `setup:repo-hook` doctor warning (`SetupDoctor.cs:24`) turns into a real per-repo check.
3. **Hook body v3** (marker `# sindri-pre-commit v3`): no `shape` line; the **stable** path `~/.local/bin/sindri` (the
   wrapper of §7.1, never a stamped build directory, which pruning deletes) baked in at install; fails closed when the
   binary is missing (exit 1, `sindri-scrub: ... not found`), as v2 did, with the fix text naming `./setup.sh` (not the
   deleted `install-sindri.sh`). `isSindriHook`
   recognises v2 and v1 so the installer replaces them.
4. `scripts/sindri-guard-proof.sh` keeps working against the C# verb (it expects `SND-SCRUB-002`).

**Order.** The removal script (§8) reinstalls the v3 hook in every profile repo **before** it deletes TS state. The TS
profile is the only list of those repos; it is resolved as the TS loader does (`sindri/src/profile/load.ts:32-37`: `--profile`,
else `AW_PROFILE_DIR`, else `$AW_STATE_DIR/profile`, a directory or a link), and each repo is a `repos/<name>.yaml` with an
absolute `path` (`sindri/src/profile/schema.ts:241`). After its deletion they cannot be found, so the script also prints the
list (name and path) in its report (§8 step 10).

### 6.3 Other Sindri changes

- **graphify pin.** It lives in `sindri/package.json` (`sindri.graphifyPin`, `graphifyPinDate`) and is read by
  `scripts/install-deps.sh:106-111`, `scripts/code-index-lock.sh:33,128-131` (rule `CODEIDX-LOCK-007`), the tests
  `scripts/tests/install-deps.test.sh:39-40,262` and `code-index-lock.test.sh:124-126`, and a comment in
  `GraphifyOptions.cs:4`. It moves before `sindri/` is deleted. Recommended: `binaries.lock` style (one pinned row), with
  `install-deps.sh` reading it; `context/code-index.lock` becomes consistent with it and rule 007 changes to compare those two.
  Alternative: a small `context/graphify.pin` file.
- **Nudge text.** `config/lib/tests/sindri-cs-hook-surface.test.sh:70` pins `sindri: <repo> is not onboarded (no secret
  scan, shape signals or index); run: sindri repo onboard`.
- **launchd.** The six plists (`config/launchd/com.agentic-workflow.{scorer,scorer-audit,sindri-observe,sindri-index-quick,sindri-index,sindri-evolve}.plist`)
  are removed with `config/launchd/`. The code index runs as the `code-index-main-move` service; nothing replaces the rest.
- **`sindri live`.** Not built (§14).

**Q3. The nudge line text.**
Recommended: amend the frozen hook-contract line to drop "shape signals" (a cut feature), with the test updated in the same PR.
Alternative: keep the text as is (a line that advertises a feature that no longer exists).

## 7. The install

### 7.1 One C# install path

A new script, `scripts/install-cs.sh`, is the only install of the toolkit's binaries. `setup.sh` and the provider
installers call it. It follows `scripts/install-meta.sh:101-140` and `scripts/sindri-app.sh`:

1. Pin and install the .NET SDK from `global.json` into `$DOTNET_ROOT` when missing; fetch the pinned `temporal` binaries
   (`scripts/binaries-lock.sh fetch`).
2. Copy the needed source tree to a scratch copy (the same set each `aot-smoke/run.sh` copies), under the box-wide heavy
   lock, and publish into `$AW_STATE_DIR/bin/cs/<stamp>/` (its own subdirectory: `bin/` also holds `aw-meta`, and the old `bin/sindri-app/<stamp>/` is retired by §8), stamped with the commit (`SourceRevisionId`, `AwBuildTime`). Like `sindri-app.sh` today, it stages the shipped recipes and runs their eval gate, and it never replaces or prunes a build a running `serve` executes (it prints `run sindri stop, then re-run` instead):

| Binary | Project | Publish | Notes |
|---|---|---|---|
| `sindri` | `src/Sindri/Aw.Sindri.Host` | framework-dependent | Temporal worker cannot be trimmed (README X54) |
| `sindri-ui` | `src/Sindri/Aw.Sindri.Ui.Desktop` | NativeAOT | found next to `sindri` |
| `judge` | `src/Judge/Aw.Judge.Host` | NativeAOT, ~11 MB | with `libe_sqlite3.*` beside it |
| `aw-packets` | `src/Packets/Aw.Packets.App` | NativeAOT | |
| `bugfix-state` | `src/BugFix/Aw.BugFix.Host` | NativeAOT | no SQLite |
| `ui-evidence` | `src/Evidence/Aw.Evidence.Host` | framework-dependent | Playwright has no AOT (EV L562-567) |
| `aw-readiness` | `src/Readiness/Aw.Readiness.App` | NativeAOT | |

   `aw-meta` stays opt-in through `--with-meta` and keeps `install-meta.sh`.
3. Verify each binary answers `version` or `help` ("install didn't take" check as `install-meta.sh` does); keep the newest
   two stamped dirs; mark one `current` (a symlink `bin/cs/current`, switched atomically).
4. **Link** (a separate step, §8 for where it runs): write the wrappers below to `~/.local/bin/{sindri,judge,bugfix-state,ui-evidence,aw-packets,aw-readiness}`
   (`CLAUDE_LOCAL_BIN`), each replacing any TS wrapper of the same name atomically (write a temp file, then `mv`). `judge`
   and `sindri` must be linked **before** the lever installers run, because each refuses to install without `judge` on PATH
   (`scripts/install-done-gate.sh:28,46`, `install-wake-gating.sh:68`, and the others in §5.1).
5. `ui-evidence install-browser`, once per box (it needs the .NET runtime, not Node; it downloads Chromium, so it needs
   network, and fails with a one-line message offline; the SDK and `temporal` downloads likewise).

Flags: `install-cs.sh [--publish | --link] [--rebuild] [--dry-run]`; with neither of `--publish` and `--link` it does both.
`--publish` is A7a's `--publish-only`. Exit 0 done or current, 1 failed (nothing on PATH changed by a failed `--publish`),
2 usage. Output lines start `install-cs:`, no colour.

Every `~/.local/bin` entry is a **small wrapper**, never a bare link (a link would break when pruning removes a stamp, and
NativeAOT finding `libe_sqlite3` through a symlink on macOS is unverified, so P2/P4 prove the resolved-path run). The wrapper
exports `DOTNET_ROOT` and execs `bin/cs/current/<name>` by its resolved path. The `sindri` wrapper also exports
`AW_SINDRI_JUDGE=<current>/judge` and `AW_PACKETS_BIN=<current>/aw-packets` (what `sindri-app.sh:22-23` injected), then `exec`s the real `sindri` by its
resolved path, so bare `sindri` finds `sindri-ui` next to it (`UiLauncher.Locate` is called with `AppContext.BaseDirectory`,
`CommandTable.cs:241`, which is the real binary's directory when the wrapper execs it by its resolved path). The wrapper does not publish; publishing is the installer's job, run by `setup.sh` and by hand as `scripts/install-cs.sh --publish` (§7.3).

### 7.2 What `setup.sh` and the providers change

| Where | Change |
|---|---|
| `setup.sh:254-260` | delete the `install-scorer.sh` call |
| `setup.sh:256-259` | `install-judge.sh --build-only` becomes the `install-cs.sh` call |
| `setup.sh:262-268` | the `--with-sindri` block (npm build, wrapper, four launchd jobs) is deleted; the C# install is not optional |
| `setup.sh:269-283` | the uv step for the TS index goes; `install-deps.sh` (`:287`) already installs uv and graphify |
| `setup.sh:293`; `providers/lib.sh:236-262` | delete `aw_build_skill_packages` and `AW_SKILL_PACKAGES`; delete the case in `providers/tests/install.test.sh:293-305` |
| `setup.sh:15,697`, messages naming `sindri index setup` | text |
| `setup.sh:622-635,717`; `config/lib/setup-onyx-step.sh:6`; `scripts/install-onyx.sh` | `sindri-app` becomes `sindri` |
| `providers/{claude,codex,cursor}/install.sh` | hook installs move out of `install-judge.sh` and `install-sindri.sh` (§7.4) |
| `providers/lib.sh:411` | drop `install-live-pane` from the lever list unless Q4 says otherwise |
| `setup.sh:229,235` | remove `reflect` and `correct` from `MANAGED_SKILLS`; add them to `DEPRECATED_SKILLS`; the skill-count messages follow |

All edits in this table are made in the switch PR (P3), not in P1.

`--with-sindri` is no longer a switch. **Q8. Keep `--with-sindri` accepted?** Recommended: accept it as a no-op that prints one
line ("the C# Sindri is always installed"), for one release, so owner scripts do not break. Alternative: remove it and let
an unknown flag fail.

### 7.3 Order against setup-once A7a/A7b

Setup-once §9 (A7a) has `setup.sh` run `sindri-app --publish-only` first, and §5.3 and SO-D5 define how shipped recipes are
embedded and approved at publish. A7a is **not on main** (`scripts/sindri-app.sh:8-12` has the flag and its header says `setup.sh` calls it, but `setup.sh`
has no call; install.md §1.1 row 33). This spec reuses A7a's order and its exit codes (0 published or current, 1 failed; a failed
publish stops `setup.sh` with exit 1; re-running is safe by the stamp check and never replaces a running `serve`). The
`install-cs.sh` publish **is** A7a's publish, extended to the seven binaries, and the TS builds it ordered itself before no
longer exist. Whichever of A7a and the cutover's phase 1.6 merges first owns the `setup.sh` call; the other rebases. The
Onyx default (`--no-onyx` today, `--with-onyx` after A7b) is not decided here.

### 7.4 The hook installers keep their hooks

The hooks stay bash and stay installed (`judge-health` for all providers; `sindri-nudge`; the opt-in levers). What goes is
the TS half of the two installers:

- `scripts/install-judge.sh`: drop the `npm install && npm run build` and the wrapper (`:35-57`, `package.json` guard
  `:41-44`). The hook half (`--hook-only`) is renamed `scripts/install-judge-hooks.sh`; its callers are
  `providers/claude/install.sh:185-189`, `providers/codex/install.sh:48-49`, `providers/cursor/install.sh:84-85`, and four
  tests (§9). `AW_NO_PROMPT_SORT` and the `prompt-sort` install go with Q2, and the installers unmerge the old `prompt-sort`
  and `trait-check` registrations and hook copies from each provider's settings (§5.2).
- `scripts/install-sindri.sh`: drop channels, the wrapper, the four launchd jobs. The `--hook-only` nudge install (`:57-77`)
  moves to `scripts/install-sindri-hooks.sh`; callers `providers/claude/install.sh:191-196`, `codex/install.sh:50-52`,
  `cursor/install.sh:86-88`. With the C# install always present, the nudge is installed whenever the provider is (Q8 aside).

### 7.5 The `sindri-app` name

**Q5. What happens to `sindri-app`?**
Recommended: retire it at the switch. The switch is one-way and the name existed only for the side-by-side period; the
removal script deletes the `~/.local/bin/sindri-app` link and `$AW_STATE_DIR/bin/sindri-app/`, and `scripts/sindri-app.sh`'s
publish logic moves to `scripts/install-cs.sh` (its run mode is just `sindri`) and the old file goes. Every `sindri-app <verb>` in docs becomes `sindri <verb>`
(§9).
Alternative: keep `sindri-app` as an alias to `sindri` for one release.

## 8. The removal script

`scripts/cutover-clean-ts.sh`, idempotent, with `--dry-run` (the `aw_dry` style; the `aw_remove_retired_dir` pattern of
`providers/lib.sh:391-406`). `setup.sh` calls it **after** `install-cs.sh` has published, linked and verified; never before.
It removes by shape, never by name list alone. In this order:

Order in `setup.sh` (one sequence, so no window has the C# `judge` over the TS database or `setup.sh` stopped half
switched): `install-cs.sh --publish` (nothing on PATH changes; a failure stops with the box untouched), then
`cutover-clean-ts.sh --pre-link` (steps 1 to 5), then `install-cs.sh --link` (step 6), then `cutover-clean-ts.sh --post-link`
(steps 7 to 10), then the lever installers. With neither flag the script runs all of its steps. Step 2 runs the C# `sindri`
by its stamped path, because nothing is linked yet.

| Step | Action | Why this position |
|---|---|---|
| 1 | Refuse (exit 1, **nothing changed yet**) if a TS job is mid-run: on macOS a loaded `com.agentic-workflow.*` launchd job that `launchctl print` shows running; on any OS the TS ledger (`$AW_STATE_DIR/sindri/ledger.db`) answering busy to a `BEGIN IMMEDIATE` probe. The TS code writes no pid file (verified: no `control.json` in `sindri/src`), and there is no `pkill` or `pgrep -f` | first, so a refusal leaves the box exactly as it was |
| 2 | For every repo of the TS profile (`$AW_STATE_DIR/profile/repos/*.yaml`, or `AW_PROFILE_DIR`; §6.2), install hook v3 with the stamped C# `sindri scrub --install-pre-commit --repo <path>` (replaces markers v2 and v1); a repo that is gone or unreadable is reported, not fatal | the TS profile is the only list; step 8 deletes it |
| 3 | macOS: `launchctl bootout gui/$(id -u)/<label>` and delete the six plists (`scorer`, `scorer-audit`, `sindri-observe`, `sindri-index-quick`, `sindri-index`, `sindri-evolve`) in `${AW_LAUNCH_AGENTS_DIR:-~/Library/LaunchAgents}`; Linux: print "remove any cron line you added by hand" | nothing is running when state goes |
| 4 | `git config --global --get init.templateDir`; unset only when it equals `$AW_STATE_DIR/sindri/git-template` | the template copy lives in the TS state |
| 5 | Delete `judge/decisions.sqlite{,-shm,-wal}` and `judge/config.json` (after printing the non-default values of the config for step 10) | before the link: no C# `judge` ever sees it (§5.3) |
| 6 | `install-cs.sh --link`: the C# wrappers replace the TS `judge` and `sindri` in `~/.local/bin` atomically | the C# state is clean, hook v3 points at this path |
| 7 | Remove the remaining TS wrappers when the file matches the TS shape (`exec <node> .../dist/cli.js`; `install-sindri.sh:97` refuses a symlink target): `scorer`, `sindri-next`, and the `sindri-app` link with `$AW_STATE_DIR/bin/sindri-app/`. Anything else (for example `sindri-view`, which no script in the repo creates) is left alone and reported | the links of step 6 already replaced `judge` and `sindri` |
| 8 | Delete TS state: `$AW_STATE_DIR/sindri/` (ledger, index, channels, git-template, spool, logs), `$AW_STATE_DIR/profile` (the profile directory or link), `$AW_STATE_DIR/scorer/`, `$AW_STATE_DIR/audit/`, `<repo-slug>/bugfix/` (open bug fixes restart, §4.3 e) | one-way; no archive. Every delete is guarded: `AW_STATE_DIR` must be absolute and non-empty (the guard of `aw_remove_retired_dir`, `providers/lib.sh:391-406`) |
| 9 | Run `scripts/install-live-pane.sh --uninstall` once (Claude marketplace `agentic-workflow-mods`, plugin `aw-live`), unless Q4 keeps the mod; `aw_remove_retired_dir` on `judge/`, `scorer/`, `sindri/` of an updated checkout for ignored build output (`dist/`, `node_modules/`) only | the mod's backend is gone; tracked content stays |
| 10 | Print, and write to `$AW_STATE_DIR/logs/cutover-<timestamp>.log`: what was removed, what was kept, what was left alone, the repos found (name and path), the judge config values that were deleted, and the **next steps**: `sindri profile init` then the typed `sindri profile approve`, then `sindri repo onboard <path>` for each listed repo (the C# profile is new and starts empty; no TS profile is migrated) | the owner needs the list, and the C# side starts unapproved |

### 8.1 Script contract

`cutover-clean-ts.sh [--pre-link | --post-link] [--dry-run]`: exit 0 done or nothing to do, 1 refused at step 1 (nothing
changed), 3 stopped partway (re-run `./setup.sh`; every step is idempotent and `setup.sh` prints that line and exits 1, with
no lever installer run), 2 usage. Lines start `cutover:`, no colour. A step that deletes prints one line with a count; with
`--dry-run` the same lines are prefixed `would`. The unique irreversible step is 5 and 8: before P4 the owner runs
`cutover-clean-ts.sh --dry-run` against the **real** state (it is read-only), and the report is read before the flip.

**Kept, never touched:** `judge/sessions/`, `judge/outbox/`, `judge/teammate-names.jsonl` (hook-written);
`context-guard/`, `locks/` (heavy-job, qa-stack, `heavy-slots-cap`, `heavy-meta-reserve`), `hooks/`, `fleet/`;
`ui-evidence/runs/` (old TS summaries stay unread, EV L619-625); `<repo-slug>/{plans,reviews,proof,verification,design}`,
`digests/`, `toolkit/`, `logs/`, `meta/`, `bin/`, `cache/`; `$AW_STATE_DIR/sindri-app` (the C# Sindri's home, never touched
by this script; its name is the state dir, not the command); per-repo MCP entries (`X-Sindri-Repo`; the C# app re-registers
per setup-once §5.4 when the port differs).

If step 5 is skipped, the generic migration refusal (AW-DATA-009) makes the C# `judge` exit 1 and every gate fail open; the
order above is what prevents it, and `judge health` in the P4 exit would show it.

## 9. The switch PR

One PR, before the flip (§12 P3; the flip is P4). Every item is a deletion or a text edit, plus the `setup.sh` and provider edits of §7.2 and §7.4.

**Directories:** `scorer/`, `judge/`, `sindri/` (121, 88 and 226 tracked files); the TS of `skills/ui-evidence/`
(`src/`, `package.json`, `scripts/` once unreferenced) and `skills/bugFixOrchestrator/` (`src/`, `package.json`); `config/launchd/`;
`mods/aw-live` and its marketplace entry unless Q4 keeps it; `skills/correct/`, `skills/reflect/`; `scripts/install-scorer.sh`,
`scripts/transcript-audit/`; the `ts-scorer` gate step of `skills/meta/examples/agentic-workflow.team.json:48-50`; `deps.yaml`
`binPaths` naming TS packages; `config/rules/web-app-rules.json`, `scripts/install-live-pane.sh`, `scripts/probe.sh:125`,
`skills/judge/SKILL.md:17` and `scripts/readiness-baseline.mjs` where they name TS packages (re-grep `scorer/`, `judge/`,
`sindri/`, `dist/cli.js` on the PR's tree); `.github/ci-tools/` and `scripts/ci/no-any.mjs` with `scripts/ci/no-any-baseline.json` (holds only
`{"judge/src/commands.ts":2}`) unless aw-live stays TS.

**CI and gates.** `.github/workflows/ci.yml`: the `ts` job (`:48-115`); the npm cache and "Build the CLIs the tests call"
in `bash-tests` (`:117-160`), replaced by the dotnet publish of `judge` and `sindri` the suites call; in `gate` the
coverage-ignore grep, the ci-tools install, the no-any gate and `claude plugin validate|test mods/aw-live` (`:162-226`);
add an "install end to end" step running the phase 2 proof in the .NET job. `scripts/ci/changed-areas.sh:17-35`: drop
`TS_PACKAGES` and `dependents_of`, and the `packages` output the workflow reads (`ci.yml:36,58`).
`.github/dependabot.yml:20-34`: trim the npm directories. `.github/labels.yml:37`: drop `area:scorer`.
`deps.yaml`: delete the `Scorer` package and every edge naming it (`:197-202,264-270,310-320,360,380-390,483,563`); drop the
`ts:` and `scan:` fields and `db: [sindri/ledger.db]`; update `schemas/deps.schema.json` and the scan path in
`scripts/check-deps.mjs` and `scripts/deps/scan.mjs`. `.env.example`: drop variables only TS reads (`AW_SKIP_LAUNCHD`, `AW_SINDRI_SRC`, `AW_SCORER_BIN`, the scorer audit block, `CLAUDE_LOCAL_BIN` text); keep `AW_SKIP_BUILD` (read by `install-meta.sh` and `config/lib/setup-meta-step.sh:12`) and
`AW_LAUNCH_AGENTS_DIR` (read by the removal script); add the new variables of §7 and §8; the `sindri-app` build-path
defaults point at `bin/cs/`; `check-env-example.sh` fails otherwise.
**`sindri-app` consumers:** `scripts/context-first-run.sh` (16 hits), `scripts/owner-check.sh` (8), `scripts/sindri-ui-fixture.sh`,
`config/lib/setup-onyx-step.sh`, `scripts/install-onyx.sh`, `.env.example`, and 28 hits under `src/` (messages, comments and
docs strings; `git grep -n sindri-app` on the PR's tree is the checklist), all read `sindri`; `scripts/tests/sindri-app.test.sh`
becomes the install test. `scripts/readiness-baseline.mjs:17` `APPS` drops `scorer`, `judge`, `sindri`,
`skills/*`; `readiness-baseline.test.sh:14-18,251` fixtures are rewritten.

**AGENTS.md** (`CLAUDE.md` is a symlink): the tagline; the "C# build — in progress, not installed until the cutover"
paragraph becomes a short statement of the installed build; the Tech Stack TS rows; Directory Structure `judge/`, `scorer/`,
`sindri/`, `mods/`; the Commands block (TS packages, `scorer ...`, `sindri doctor|index|evolve|scope|shape|observe`,
`install-scorer.sh`, `install-sindri.sh`, the `sindri-app` lines); the bash test list; **Merge Gate** items 1, 2 (TS parts and
`claude plugin ...`), 6 and 7 (item 8 becomes the gate); Required Context's `CODE_STYLE.md` note. Then `scripts/sync-rules.sh`.
Rules: `.agents/rules/testing.md` (Vitest policy), `.agents/rules/hooks.md:171` (sindri-nudge installer).

**Bash suites deleted:** `scripts/tests/install-sindri.test.sh`, `scripts/tests/install-scorer-audit.test.sh`,
`config/lib/tests/install-live-pane.test.sh` (unless Q4), `config/lib/tests/judge-brief-real-cli.test.sh`,
`config/lib/tests/send-gate-real-judge.test.sh` (both run `node judge/dist/cli.js`; the C# hook-surface test covers the
contract; retarget at the C# binary if the owner wants them), `config/lib/tests/install-prompt-sort.test.sh` and
`install-trait-check.test.sh` (with Q2), the case in `providers/tests/install.test.sh:293-305`.
**Retargeted:** `scripts/tests/install-deps.test.sh` and `code-index-lock.test.sh` (pin source); `scripts/tests/ci-changed-areas.test.sh`,
`ci-bash-tests.test.sh:28,69-80` (the `install-judge.sh` build guard is moot); `scripts/tests/sindri-app.test.sh` (the
rename; becomes the install test); `config/lib/tests/install-wake-gating.test.sh:23`, `installed-hooks-smoke.test.sh`,
`aw-state-dir-isolation.test.sh:59-68,114`, `config/hooks/tests/provider-install-hooks.test.sh:123-126` (they call
`install-judge.sh` and expect a `judge` wrapper in `CLAUDE_LOCAL_BIN`); `config/lib/tests/statusline.test.sh` (fake scorer,
`:27,186-195`: keep the "missing scorer shows `--`" case, delete the refresh cases). `scripts/ci/bash-tests.sh:84-106` lists.

**Docs.** Edit: `README.md` (`:3,5,199,237-238,264`), `ONBOARDING.md` (`:11,28,241,253-257`), `planning/ARCHITECTURE.md`
(`:10,116,210,355-361` and the TS Sindri text), `planning/PROVIDERS.md` (`:4,39-40,64,72`), `planning/TESTING.md`
(`:13,22,27`), `planning/LOCAL_DEV.md` (`:7,45,57,90`), `planning/CODE_STYLE.md:9`, `planning/ERD.md` (`scorer.sqlite`),
`planning/DEPENDENCY_GRAPH.md:15`, `planning/PR_GUIDE.md`, `planning/COMMIT_STRATEGY.md`; `docs/sindri/README.md`
(`:25,35-36,108,229-262`), `docs/sindri/SETUP.md` (`:20,52,114-120,407`), `docs/sindri/OPERATIONS.md:7` (the "read `sindri` as
`sindri-app`" notices go), and the TS command docs `index.md`, `evolve.md`, `scope.md`, `profile.md`, `linear.md`,
`onyx.md`, `context.md`; `docs/csharp/{judge,bugfix,evidence,sindri,sindri-ui,sindri-profile,sindri-context}.md` (every
"until the switch" sentence; evidence.md "At the switch" is adopted wholesale); `skills/_preamble.md:5,17,18,102`, the toolkit counts ("53 native skills" in AGENTS.md and READMEs), `.agents/rules/skills.md:148`,
`.agents/rules/csharp.md:42`, `.agents/rules/mcp-servers.md`, `docs/devcontainer.md`, `docs/ui-evidence-cost.md`,
`docs/dispatch-conventions.md`, `docs/readiness/*`. Comment-only citations: `config/hooks/context-guard.sh:9`,
`probe-log.sh:6`, `done-gate.sh:65`, `config/lib/failopen.sh:11`. Historical specs under `docs/superpowers/` stay.

**Stays Node** (so Node stays a setup prerequisite): `dembrandt` (`setup.sh:461`), `prism-mcp` and `xcodebuildmcp` through `npx`
(`setup.sh:566-577`), `scripts/package.json` (Ajv for `check-launcher.mjs`, `check-fleet.mjs`, `check-scoping-schemas.mjs`),
`mods/aw-live` if kept, `.nvmrc`.

## 10. The hook contract stays green

The contract (readiness spec §4c, frozen): the commands and arguments the bash hooks and statusline call, the JSON fields
they `jq`, the exit codes and stderr prefixes they branch on, and the files hooks write.

- `config/lib/tests/judge-cs-hook-surface.test.sh` becomes C#-only: delete `SIDES`, `TS_CLI`, `shim-ts`, `same()` and every
  `same <case>` call and every `if [ "$SIDES" = "cs ts" ]` block (lines 8, 33-35, 38, 40, 89-97, 132-136, 152-157, 174, 184, 218,
  246-251), the "(sides: ...)" suffix (253-254) and the "TS" wording in messages (93-96, 134-135, 155, 249). Binary
  resolution: `AW_JUDGE_CS_BIN` first (CI), then the installed `judge` on PATH, then the build-it fallback for local work.
  The shim keeps the hooks calling bare `judge` as installed hooks do.
- `config/lib/tests/sindri-cs-hook-surface.test.sh` (the nudge) stays; it resolves the installed `sindri`. With Q3 its pinned
  line changes in the same PR.
- A new case set for the pre-commit hook (§6.2): a staged secret is refused with `SND-SCRUB-002`; v2 and v1 hooks are
  replaced; a foreign hook is refused `SND-SCRUB-003`.
- `statusline.sh` keeps its `find_bin scorer` guard: with no scorer the Live column prints `--` and no refresh is scheduled
  (`config/statusline.sh:155,184`). The scorer refresh branch and the Live cache code are removed in the same PR as the
  scorer. `judge_health` (`config/statusline.sh:125`) is called today only from `live_refresh` (`:159`) and the segment returns `--`
  when no scorer exists (`:184`), so removing the scorer would drop the judge segment. The PR therefore moves the
  `judge_health` call into the render path (cached, as the Live cache was), and `statusline.test.sh` keeps a judge-health case.
- `config/hooks/tests/judge-health.test.sh` and `failopen.test.sh` use stubs and do not change.

## 11. The scorer cut, in detail

| Consumer | Action |
|---|---|
| `scripts/install-scorer.sh`, plists `scorer`, `scorer-audit`, `$AW_STATE_DIR/scorer/` | removed (§8) |
| `config/statusline.sh:155,184` | guard stays, refresh goes (§10) |
| `mods/aw-live` | Q4 |
| `deps.yaml` Scorer package and edges | deleted (§9). The Evidence `files` edge for `ui-evidence/runs` and the `context-guard` fires-log file need a new owner or removal; unit 3.2 decides, and `check-deps.mjs` is the check |
| CI, dependabot, labels, `.env.example`, readiness-baseline | §9 |
| `scorer probe` and `scripts/probe.sh` docs | the reader is gone; `probe.sh` keeps writing, docs say so |
| daily cost report | none until Sindri rebuilds it (§14) |

**Q4. The `aw-live` mod.**
Recommended: uninstall it at the switch (`install-live-pane.sh --uninstall`) and delete it from the repo until Sindri has a
`sindri live`.
Alternative: keep the `scorer` package installed only for the pane until then. That contradicts the owner's cut and keeps
Node and the TS build in the install.

## 12. Phases

Each phase has exit criteria and a proof. Contract units come first (§13).

### P0. Contracts

This spec's tables become test lists, red first (`/new-slice` style). Exit: every §3.2, §4.2, §5.1 and §6.1 row marked
equivalent has a named test or an existing one; every gap row has a red test or a stub. Proof: `dotnet build -warnaserror`
zero warnings; the red tests are listed in the PR. Owner decisions Q1 to Q8 are answered before P1 opens.

### P1. Close the gaps (parallel waves)

| Unit | Package | Work | Proof |
|---|---|---|---|
| 1.1 | Evidence | G-a script features (or the trim, per Q1) with guard rules | unit lane; heavy `BrowserSessionContract` run through the heavy lock |
| 1.2 | Evidence | G-b `gh-publish` (port, gh runner, fake) | unit lane with the fake; owner's scratch-repo check, skipped without a token |
| 1.3 | Evidence | G-c `JudgeCliJudge`; registered in `Composition.cs` | contract test with a stub `judge`; one run with the real C# judge offline (`AW_JUDGE_PROVIDERS=rules`) |
| 1.4 | Evidence | G-d doctor slots and env loader; G-e per-artifact `commit` and `at`; G-g flag name | unit lane |
| 1.5 | Evidence + skills | G-f SKILL.md rewrite; the brief text; `step.evidence.none` and `combine` as `IBlockStep`; `Bash(ui-evidence:*)` in `AgentLaunches.RequestFor`; §3.4 checks | Sindri unit lane; the Sindri Ui tests in their own gate if a catalog row changes (feedback_lane_gate_ui_tests) |
| 2.1 | BugFix | §4.3 (a) and (b): both adapters, the digest and check-kind fixes, `aot-smoke` and `expected.txt:18` updated | unit lane; `bash src/BugFix/aot-smoke/run.sh` with a fake `judge` and `ui-evidence` |
| 2.2 | BugFix skill | §4.3 (d) text | the skill's commands run once in a temp repo through the smoke |
| 3.1 | Judge | Q2 cuts: installers, hooks, skill text, tests; the hook-registration unmerge for provider settings (§5.2); the statusline judge segment (§10) | `judge-cs-hook-surface` and `install-*` tests |
| 3.2 | deps/CI | Scorer package out of `deps.yaml`; graphify pin moved (§6.3) | `node scripts/check-deps.mjs`; `install-deps.test.sh`; `code-index-lock.test.sh` |
| 4.1 | Sindri | `scrub` verbs, `IRepoHooks`, hook v3, `doctor` per-repo line | unit and integration lanes; `scripts/sindri-guard-proof.sh` |
| 4.2 | Sindri | Q3 text and test; `--with-sindri` Q8; text of `sindri index setup` messages | `sindri-cs-hook-surface` |
| 5.1 | Install | `scripts/install-cs.sh` (`--publish`, `--link`), the wrappers and their test; `cutover-clean-ts.sh` is P2. The `setup.sh` and provider edits of §7.2 and §7.4 wait for P3 | the install proof of P2, dry run |

Exit: every gap row of §3.3 and every KEEP-needed row of §6.1 is closed or cut by an owner answer; `dotnet build
-warnaserror`, both lanes, `dotnet format --verify-no-changes`, `scripts/dotnet-verify-packages.sh` pass.

### P2. The install switch and the removal script, on a scratch box

Write `scripts/cutover-clean-ts.sh` and its test; prove the whole switch **without the real HOME**. Isolation follows
`scripts/sindri-prove.sh`: a guarded temp state dir (`AW_STATE_DIR`, `CLAUDE_LOCAL_BIN`, `AW_LAUNCH_AGENTS_DIR`,
`GIT_CONFIG_GLOBAL` set to temp paths, plus the provider settings and hook paths and the Playwright browser cache, a fake TS
install seeded in it: wrappers, state, plists, a profile with one repo, one onboarded repo with a v2 hook, a TS-shaped
`decisions.sqlite`, and registered `prompt-sort`/`trait-check` entries). Never `HOME=$(mktemp -d)` for dotnet or heavy work (each leaks a 3.3 GB NuGet cache; `feedback_no_temp_home_dotnet`).
Exit and proof: on the scratch box, `install-cs.sh` publishes, links and verifies; the cleanup removes exactly the §8
list and leaves the §8 "kept" list byte-identical; the v2 hook is now v3 and refuses a staged secret and passes a clean
commit; the real hooks, run against the installed binaries, pass the §10 suites; `./setup.sh --providers claude,codex,cursor
--dry-run` is clean; a second run changes nothing; a cold `sindri repo status --nudge` through the wrapper stays inside the nudge hook's time
budget (measured, not assumed; the framework-dependent `sindri` is the slowest binary); a failed `--publish` leaves the seeded
TS install untouched, and a step-1 refusal changes nothing. The step runs in CI (§9). The macOS case (a wrapper exec of a
NativeAOT `judge` finding `libe_sqlite3`) cannot run in Linux CI; it is the P4 check on the owner's Mac.

### P3. The switch PR

§9 in one PR: `setup.sh` and the provider installers call `install-cs.sh` then `cutover-clean-ts.sh`, and the TS
directories, CI jobs, gates, suites and docs are deleted. Merging it changes no box (the flip is P4). Exit: the Merge
Gate with item 8 as the gate passes; `scripts/sync-rules.sh --check`, `check-deps.mjs`, `check-env-example.sh`,
`check-rules.mjs` pass; no tracked file under the deleted directories; no `npm` in any install path; the P2 scratch-box
proof re-run on the PR's tree; the owner has read the output of `cutover-clean-ts.sh --dry-run` against the real state.
Proof: the CI run.

### P4. The owner's flip on the real box

The owner pulls main and runs `./setup.sh` once per box. Exit: `sindri doctor` is ok; `judge health` answers `ok`; a real hook fires (a session
start prints no `judge: not found`); a real `ui-evidence run` produces a summary with the judge adapter wired; one
`bugfix-state` run reaches a resolved report; `git commit` in an onboarded repo is checked by v3; `sindri profile init`, the typed `profile approve` and `repo onboard` for
each repo the report listed have been run (the C# profile starts empty); bare `sindri` opens the app, or on a box with no
display prints the status and says how to open it. No rollback is built; if
a step fails, the owner reports it and the fix goes forward.

## 13. Parallel execution with an agent team

**Dependency graph.**

```
P0 contracts (Wave 0)
 |
 +-- 1.1  Evidence script features ----+
 +-- 1.2  Evidence gh-publish ---------+
 +-- 1.3  Evidence JudgeCliJudge ---+---+--> 1.5 skill + steps + AgentLaunches  --+
 +-- 1.4  Evidence doctor/env/DTC9 -+                                              |
 +-- 2.1  BugFix adapters  --(needs the 1.3 judge contract, not its code)--> 2.2 ---+
 +-- 3.1  Judge cuts ------------------------------------------------------------+
 +-- 3.2  deps/CI + graphify pin ------------------------------------------------+
 +-- 4.1  Sindri scrub + IRepoHooks + hook v3 --> 4.2 nudge text -----------------+
 +-- 5.1  install-cs.sh (needs the binary list, not the gaps) -------------------+
                                                                                  v
                                            P2 switch proof --> P3 switch PR --> P4 flip
```

**Waves.**

| Wave | Units | Rule |
|---|---|---|
| 0 | P0 contracts | one unit per package, merged first; later units build on red tests |
| 1 | 1.1, 1.2, 1.3, 1.4, 2.1, 3.1, 3.2, 4.1, 5.1 | disjoint write scopes; all start at once |
| 2 | 1.5, 2.2, 4.2 | wait for their wave-1 inputs |
| 3 | P2 proof, then P3 | one integrator unit each |
| 4 | P4 | the owner |

**Contract unit first, then disjoint scopes.** In each package the first unit writes the failing tests and the stubs
(request, response, port, adapter with no constructor parameters that throws `NotSupportedException`) and merges red; the
implementers then fill disjoint write scopes: 1.1 owns `Core/Scripts`, `Core/Guard`, `Browser/Locator.cs`; 1.2 owns a new
`Features/GhPublish`; 1.3 owns `Infrastructure/Judging`; 1.4 owns `Features/Doctor` and a new env loader; 2.1 owns
`src/BugFix/Aw.BugFix.Infrastructure`; 4.1 owns a new `Aw.Sindri.Features/Scrub` and `OnboardRepo/IRepoHooks`.

**Hotspot files owned by the integrator only.** `deps.yaml`, `AgenticWorkflow.slnx`, `Directory.Packages.props`,
`packages.lock.json`, `PublicAPI.*.txt`, `CommandTable.cs` of each host, `Composition.cs` / the package's `Add...()` and its
JSON context, `.env.example`, `.editorconfig` baseline block, `scripts/ci/bash-tests.sh`, `.github/workflows/ci.yml`,
`AGENTS.md`, `setup.sh`, `providers/lib.sh`. An implementer asks the integrator for an edit to one of these; it does not
make it. The integrator serializes them so two units never conflict on a hotspot.

**Roles.**

| Role | Does | Never does |
|---|---|---|
| Orchestrator | holds the graph, assigns units, tracks the heavy lock queue, answers the owner through the lead | codes |
| Implementers | one unit each, in their own git worktree, red then green, formatting only the `.cs` they touched | touch another unit's scope or a hotspot; run a full-solution format; run pattern kills |
| Integrator | owns hotspots, merges units in dependency order, runs the batch gate (build `-warnaserror`, both lanes, full `dotnet format --verify-no-changes`, `check-deps`, `check-env-example`, `check-rules`, `sync-rules --check`) | write feature code |
| Prover | runs the P2 scratch-box proof and the real-binary smokes (`aot-smoke` scripts, `sindri-guard-proof.sh`, the hook-surface suites) and reports exit codes and output | fix what it finds; it reports to the owning implementer |
| Reviewers | read each unit's diff against this spec's tables, not a summary of them; fix-only findings loop, suggestions answered in writing (`feedback_review_loop_severity`) | block on nits |

Builders run on Sonnet or Haiku, never Opus (`feedback_builders_on_sonnet`). Worktrees are removed by the meta, never by hand
(`feedback_worktree_removal_via_meta`). A lane never asks the owner a blocking question; it routes it through the meta
(`feedback_no_blocking_questions_in_lanes`). Each PR worktree gets `heavy dotnet clean AgenticWorkflow.slnx` right after its
`ready` (`feedback_clean_after_ready`). No process is killed by pattern, ever: recorded PIDs only.

**Heavy jobs.** Builds, test lanes, NativeAOT publishes, browser runs and the P2 proof take slots on the box-wide heavy lock
(`config/lib/locks.sh`; cap and graded memory guard, `project_heavy_lock_two_slots`). The number of agents is not the limit;
the lock is. A unit that cannot get a slot waits in the queue and does other non-heavy work (tests written red, docs) in the
meantime. Lanes and the meta are the only ones that run heavy jobs in demo mode; outside it, any unit does, through the lock.
Scoped lane gates: a lane runs build and the unit lane of its touched projects only; the full-solution format and the
integration lane run once, in the integrator's batch (`project_scoped_lane_gates`, `feedback_scoped_dotnet_format`). A PR
that touches a Sindri catalog row, action id or API golden also runs `Aw.Sindri.Ui.Tests` in its own gate.

## 14. Out of scope

- The scorer rebuild, the daily cost report and `scorer audit`.
- Recipes for `evolve`, `scope`, `observe`, `shape` and `profile explain` (rebuilt from scratch in C#, owner 2026-10-20).
- `sindri live` (the replacement for `scorer live` and the `aw-live` pane's numbers).
- `bugfix-state run-api` and `run-db` verbs (the judge's `api-evidence` and `db-evidence` kinds stay unreachable from the skill).
- `prompt-sort`, `trait-check`, `label`, `adjudicate`, `eval` (unless Q2's alternative).
- An `IUiEvidence` port and a `Sindri -> Evidence cli` edge (§3.1, Q6 alternative).
- Windows; a second user; any rollback tooling.

## 15. Risks

| Risk | Effect | Mitigation |
|---|---|---|
| Stale per-repo hooks (v2, v1) in repos the TS profile does not list, or a repo moved since onboarding | the v2 `shape` line fails (one stderr line per commit); `scrub --staged` still answers, so no commit is refused | the C# `scrub --staged` serves v2 and v1 hooks unchanged (§6.2); v3 recognises v1 and v2; doctor reports `pre-commit:<repo>` for profile repos; the removal script prints repos it could not reach and how to reinstall (`sindri scrub --install-pre-commit --repo <path>`); the fail-closed message names the fix |
| Judge state refusal (AW-DATA-009) if `decisions.sqlite` survives | every hook sees judge exit 1; fail-open logs; judge health `down` | §5.3 and §8: step 5 deletes it before the link; the P2 proof seeds a TS-shaped file |
| .NET runtime missing for the framework-dependent `sindri` and `ui-evidence` | the wrappers fail | `install-cs.sh` installs the pinned SDK into `$DOTNET_ROOT`; the wrappers export it; the install verifies each binary answers |
| Node stays required (dembrandt, prism-mcp, xcodebuildmcp, Ajv scripts, aw-live if kept) | "no TS" is read as "no Node" | §9 lists what stays Node; `setup.sh` keeps its Node check |
| Heavy lock contention (P1 builds, AOT publishes, the browser) | waves stall, a gate starves lanes | §13: queue by depth, the meta reserves and clears its slot (`feedback_meta_clear_gate_reserve`); no per-agent cap |
| The statusline's Live column shows `--` forever | looks broken | the guard is intended (§10); the README says why |
| `reflect` and `correct` skills removed | users type a removed command | `setup.sh` removes their links: they move from `MANAGED_SKILLS` (`setup.sh:229`) to `DEPRECATED_SKILLS` (`:235`); removing a name from `MANAGED_SKILLS` alone leaves the link (the prompt answers no when not interactive) |
| Evidence summary schema change (EV L626-640) | old `summary.json` files unread, bugfix and scorer readers differ | old runs stay on disk; BugFix reads only the C# schema; no migration |
| The cleanup stops partway (a step fails) | a half-switched box | the order of §8 puts the only refusal first, every step is idempotent, and `setup.sh` stops with exit 1 and "re-run `./setup.sh`" before any lever installer runs |
| A7a not on main | the `setup.sh` call lands twice | §7.3: whichever merges first owns it |
| Judge adapters in BugFix and Evidence read the judge's store | wrong `AW_STATE_DIR` gives empty `why` rows | both adapters take the same env as the hooks; `AW_JUDGE_STATE_DIR` for Sindri's separate store is not reused (judge.md; DT spec §8.3) |

## 16. Amends

- `2026-10-09-agent-readiness-design.md` §4c: **superseded** (see the header). §4a step 3 ("one reversible switch"), the
  `--runtime` and `--prefix` flags, the archive and the 14-day tag are replaced by §2 and §8. Kept from §4c: the hook
  contract, "no data compatibility", the refuse-to-start check, the bugfix namespace move.
- `2026-10-19-sindri-setup-once-design.md`: the command name (`sindri-app` is read as `sindri` from the switch on; §7.5), the
  statement that `sindri` is the TypeScript build, and the A7a order sentence naming the scorer, judge and TS builds
  (§7.3). Not amended: SO-D5, §5.3, §5.4, the A7a exit codes, §13.1's proof shape.
- `docs/csharp/evidence.md` "At the switch": adopted; G-a to G-g added.
- `docs/csharp/bugfix.md`: the digest wording (§4.3 c) and the adapters; `docs/csharp/judge.md`: hook table row `prompt-sort`
  per Q2.
- `AGENTS.md` and `.agents/rules/{hooks,testing}.md`: §9.

## 17. Questions for the owner

| # | Question | Recommended | Alternative |
|---|---|---|---|
| Q1 | The Evidence script features (G-a) | Port them | Cut them from SKILL.md; qa-runner uses the C# subset |
| Q2 | `prompt-sort`, `trait-check`, `label`, `adjudicate`, `eval` | Cut from the cutover; rebuild later | Port `prompt-sort` first (J2) |
| Q3 | The nudge line text | Amend the frozen line to drop "shape signals" | Keep the text |
| Q4 | The `aw-live` mod | Uninstall and delete until `sindri live` | Keep `scorer` installed only for the pane |
| Q5 | `sindri-app` | Retire at the switch | Alias for one release |
| Q6 | How the recipe's evidence run steps reach Evidence | Agent steps run the `ui-evidence` binary | Block steps through an `IUiEvidence` port and CLI adapter |
| Q7 | `gh-publish` transport | The `gh` CLI runner Evidence already styles | `Aw.Forge`'s GitHub adapter (needs write methods) |
| Q8 | `--with-sindri` after the switch | Accept as a no-op for one release, print one line | Remove; an unknown flag fails |
