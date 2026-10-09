# Scope map: Sindri design §13 rollout: steps 0–6, container spike, and ring-0/ring-1 bootstrapping ladder

Status: complete · rounds: 4 · tokens: 91195 · generated 2026-10-09T15:29:17.607Z

Surfaces, details and workstreams are model-drafted; check them against the cited sources. Sources marked untrusted came from comments, issues, transcripts or code.

Surfaces added by the challenger: 5

## Surfaces

- **S1** (job) Transcript audit scripts and scorer audit command: Turn audit extraction into scripts/transcript-audit/. Dedupe resumed/forked turns keyed on (timestamp,text). Label calibration on a 400-turn sample (Wilson bound &gt;=0.6, &gt;=10 positives). Measure design wrong-approach corrections per 30 days with a 95% interval. Weekly unlabeled launchd job and monthly manual labeled re-check. Existing scorer audit code and tests already exist.. Sources: R1, R11, R12, R13.
- **S2** (data) Baselines, targets, XS-share and quota-per-item measurements: Record baselines (\~/.agentic-workflow/audit/baseline.md, weekly summary.json), set §2 targets, measure share of last 60 days of in-scope items that would be XS/clear/trusted (10% threshold decides autoStartMaxSize), and subscription quota per item on manual sessions.. Sources: R1, R13.
- **S3** (other) Host foundation: profile schema/tooling, lock and fencing, scrubber, done-gate fix: Profile schema and CLI skeleton (sindri profile init --ring0, doctor), ledger, heavy-job lock with fencing, scrubber with pre-commit install (v1, v2 records shape signals), done-gate claim-detection fix reinstalled via scripts/install-done-gate.sh.. Sources: R1, R32, R38, R39, R36.
- **S4** (api) judge --providers flag: Callers pin providers (Anthropic + Jev) for direction checks and verifiers; existing judge providers, evaluate and adjudicate modules and tests.. Sources: R1, R2, R6, R4.
- **S5** (integration) plan-file Tracker adapter and sindri observe: Generic adapter reading docs/superpowers/plans/\*.md tasks as work items; observe hourly via launchd; rule-based sizes at first, model triage from step 2. Error codes SND-TRACKER-001/002 and size cap exist.. Sources: R1, R15, R16.
- **S6** (data) Code index (structure, clones, deps, embeddings, graphify) and mirror: Per-repo sqlite index with layers, schema versioning, degraded layers allowed, quick hourly and nightly full builds under heavy lock, git mirror, sindri index setup/build/status, repo add.. Sources: R1, R18, R19, R20, R21, R22, R23, R24, R25, R26, R27, R28, R31, R33.
- **S7** (data) Shape signals spool, ledger ingest and reconciliation: Record-only shape signals in 3a, ingested from spool into ledger, linked to runs and labeled; precision &gt;=0.7 over &gt;=30 signals per layer gates enforcement in 3b.. Sources: R1, R34, R35, R38.
- **S8** (other) Reuse ports phase 1 and scoping harness with --backtest: Port skills needed for scoping/evidence; sindri scope command with --section, --sources, --out docs/superpowers/scopes/; backtest recall on the motivating project; first value is scope maps plus recall.. Sources: R1, R40, R41.
- **S9** (job) Self-evolution loop and artifact registry: Offline blinded eval (step 1), online shadow A/B (3a), self-adopt tier (6). Registry over toolkit repo: module eval suites wired to existing tests, hook false-positive telemetry, stable/next channels, reflect/correct/eval ports, sindri evolve init/resume, auto-revert.. Sources: R1.
- **S10** (ui) Dashboard (Sessions, Waiting) and SwiftBar badge: sindri dashboard showing ring-0 items and what Sindri would do; SwiftBar badge install; step 2.. Sources: R1.
- **S11** (job) Shadow triage, packs, historical replay: Model triage of items, packs, replay; exit when per-class routing agreement &gt;=90% over &gt;=30 items and spike verdict in.. Sources: R1.
- **S12** (integration) Container spike: model proxy, lock broker sidecars, attach: Interactive Claude Code in container with dummy credential and ANTHROPIC\_BASE\_URL proxy swapping subscription bearer; proxy and lock broker as sidecars over one authenticated TCP channel (no bind-mounted unix socket); Warp attach; bundle export and two-phase verify; warm start &lt;60s; 4-session fleet 24h; quota effect under quota.reserveForHuman. Runs as one heavy job.. Sources: R1, R32.
- **S13** (api) sindri start, packs and re-injection, worker→ship Steps, write shim, two-phase verify: Step 3a assist: dispatch replaces pasted prompts, ship Step opens PRs, handoffs removed; containers if spike passed else host with no auto-start.. Sources: R1.
- **S14** (other) Turn classification, tool gate, Stop pattern gate: Pattern-based gates in 3a, using metric-grade patterns only where calibrated.. Sources: R1.
- **S15** (other) Direction checks (Approach, Drift, Shape, Scoping, Scope expansion): Per §13.2 matrix: Approach/Drift conditional on step-0 rule (shadow, built only if design corrections &gt;=8/30d); Shape enforce in 3b; Scoping and Scope expansion shadow then enforce in step 5; sindri-side verifier authoritative in 3b.. Sources: R1.
- **S16** (notification) Notifications: Step 3a notifications for handoffs/waiting; SwiftBar badge related.. Sources: R1.
- **S17** (flag) Profile mode and enforcement flags: mode: shadow/assist/auto-small, autoStartMaxSize, shape.enforce, quota.reserveForHuman, evolve tier self-adopt; per-ring switches.. Sources: R1.
- **S18** (permission) Auto-small unattended execution and credentials: Requires container isolation pass, doctor passing §8, step-2 threshold, and confirmed plan terms for unattended subscription use or API-key mode; no real token in container.. Sources: R1.
- **S19** (integration) Issue creation from scope maps: Moves to self-adopt once a project type meets its backtest bar; step 5.. Sources: R1.
- **S20** (other) Bootstrapping ladder switch-on tasks: Every plan ends with a Turn it on task (command, PR evidence, use on next plan); ring 0 then ring 1 gating (one plan's worth, no open defects); one-working-day rule.. Sources: R1.
- **S21** (report) Steering turns per merged Sindri PR metric: The ladder names this as the first metric it has to bring down. The scorer audit should measure it on the build itself and report it in the weekly summary.json, so the build can be tracked from P1 onward. The map only mentions it as a reporting implication. No surface or workstream acceptance criterion produces it.. Sources: R1.
- **S22** (data) Session-to-item linkage in the ledger: Each observe run records every plan task, and each change to it, in the ledger. From rollout step 2, sessions are also linked to items. Quota per item, routing agreement and steering-per-PR all depend on that link. No surface in the map covers how sessions are joined to items.. Sources: R1, R13.
- **S23** (report) UI-evidence run cost records: The scorer already tracks a cost record for each ui-evidence run: broken steps, visual change, PR, route and invocations. This sits alongside the ported ui-evidence parity publishing. The map covers parity publishing under reuse ports (S8) but not cost and telemetry reporting for the ported evidence skill.. Sources: R14, R40, R41, R1.
- **S24** (integration) Real (workplace) tracker adapter for ring-1 observe and shadow triage: The ladder's ring-1 column says \`observe\` and shadow triage run "on the real tracker" for workplace tickets, and assist mode then runs on workplace tickets. The map only covers the generic plan-file Tracker adapter (S5). The map doesn't cover a second Tracker implementation that reads workplace issues as work items, with sizes, change recording in the ledger and session linkage. Belongs with the ring-1 switch-on work (W11).. Sources: R1, R16.
- **S25** (flag) Live-view off switch enforced in host-mode fallback: If the container spike fails, shadow and assist are only cleared on the host provided the live view stays off and the sindri never fetches bundles on the host. The map has no flag, setting or doctor check that turns the live view off, and nothing that blocks bundle fetches when running on the host without isolation. Belongs with the spike and fallback (W7), and it gates W8.. Sources: R1.

## Implications

- **migration:** Index sqlite schema is versioned by user\_version and is dropped and rebuilt on mismatch; layer stamps trigger table wipes. Any schema or parser change causes a full rebuild. Ledger and profile schema need migration plans as plans P2–P5 land.. Sources: R28, R22, R24.
- **migration:** Done-gate hook copy in \~/.claude/hooks/ must be reinstalled on merge; scrubber pre-commit upgrades from v1 to v2 for shape signals.. Sources: R1.
- **permissions:** Auto-small requires confirmed subscription terms for unattended use or API-key mode; containers hold no real token; no host unix socket bind-mounts; sindri never fetches bundles on host if spike fails; live view off.. Sources: R1.
- **permissions:** Index and mirror directories use 0700/0600 modes and a deny-path list; scrubber guards this public repo from secrets and identifiers.. Sources: R28, R33, R1.
- **reporting:** Weekly audit summary.json, baseline.md, monthly labeled re-check, steering turns per merged Sindri PR as first ladder metric; Wilson-bound metric-grade vs floor-count reporting; routing agreement and per-layer precision reporting.. Sources: R1, R12.
- **notifications:** Notifications arrive in 3a with the dashboard badge; false 'Claiming done' blocks should drop to \~0.. Sources: R1.
- **mobile:** No mobile surface is named in the brief; only Warp attach and a macOS SwiftBar badge appear.. Sources: R1.
- **flags:** Rollout is gated by flags and thresholds: autoStartMaxSize XS vs S (10% XS share), shape.enforce per ring, mode per ring, Approach/Drift conditional on the step-0 8/30d rule with shadow-first, evolve tier.. Sources: R1.
- **other:** Spike failure fallback: 3a and earlier run on host with no auto-start; step 4 waits for another runtime or Linux cloud box. Heavy jobs serialize through one lock, so spike, index builds and audits compete.. Sources: R1, R32.

## Workstreams

- **W1** Step 0: measure and decide
  - surfaces: S1, S2, S21, S22, S23
  - depends on: none
  - acceptance:
    - scripts/transcript-audit/ exists and dedupes forked/resumed turns
    - baseline.md recorded and §2 targets set
    - Approach/Drift decision recorded with 95% interval
    - XS share and quota per item measured
- **W2** Step 1a: host foundation and judge flag
  - surfaces: S3, S4, S5, S17
  - depends on: W1
  - acceptance:
    - doctor all ok and ledger exists
    - false done-gate block rate \~0 in next weekly audit
    - judge --providers jev why &lt;id&gt; works
    - fixture secret refused by pre-commit
    - plan-file observe lists remaining plan tasks
- **W3** Step 1b: code index
  - surfaces: S6, S7
  - depends on: W2
  - acceptance:
    - index status fresh
    - layers down are listed in PR evidence
    - shape signals appear in ledger for builder commits
- **W4** Step 1c: scoping harness and reuse ports
  - surfaces: S8
  - depends on: W2, W3
  - acceptance:
    - Scope map file produced for the design §13
    - --backtest recall number reported on motivating project
- **W5** Self-evolution and artifact registry
  - surfaces: S9
  - depends on: W2
  - acceptance:
    - Registry lists every module
    - first reflect proposal recorded
    - offline blinded eval runs
- **W6** Step 2: shadow triage, dashboard, badge
  - surfaces: S10, S11
  - depends on: W3, W4
  - acceptance:
    - Dashboard shows ring-0 items
    - routing agreement &gt;=90% over &gt;=30 items
- **W7** Step 2: container spike
  - surfaces: S12
  - depends on: W2
  - acceptance:
    - All 7 spike criteria pass or documented fail
    - verdict recorded
- **W8** Step 3a: assist patterns
  - surfaces: S13, S14, S16
  - depends on: W6, W7
  - acceptance:
    - Next Sindri PR opened by ship Step
    - runs in containers if spike passed else host without auto-start
    - notifications delivered
- **W9** Direction checks across steps 3a–5
  - surfaces: S15, S19
  - depends on: W8
  - acceptance:
    - Approach/Drift shadow only if step-0 rule met
    - Shape enforces once per-layer precision &gt;=0.7 over &gt;=30 signals
    - Scoping and scope-expansion enforce at step 5
    - Issue creation self-adopt after backtest bar
- **W10** Step 4: auto-small
  - surfaces: S18
  - depends on: W7, W8, W1
  - acceptance:
    - Isolation pass and doctor §8 pass
    - plan terms confirmed or API-key mode
    - XS ring-0 task goes to draft PR unattended
- **W11** Bootstrapping ladder switch-on
  - surfaces: S20
  - depends on: W2
  - acceptance:
    - Each plan ends with Turn it on task with PR evidence
    - ring-1 only after ring 0 runs a plan with no open defects
- **W12** Missing surfaces
  - surfaces: S24, S25
  - depends on: none
  - acceptance:
    - each listed surface is scoped before work starts

## Open questions

- What are the §2 targets, given they are set from measured baselines? (options: open-ended; sources: R1)
- If the XS share is under 10%, pilot S with 100% verification or skip auto-small and stay in assist? (options: Pilot S / Stay in assist; sources: R1)
- If subscription auth cannot work via base-URL proxy, accept API-key mode for containers and its cost? (options: API-key mode / Block auto-small; sources: R1)
- Are the plan terms for unattended subscription use confirmed, and who confirms? (options: open-ended; sources: R1)
- What backtest bar per project type allows issue creation to move to self-adopt? (options: open-ended; sources: R1)
- Is mobile in scope beyond Warp attach and SwiftBar? (options: No / Yes; sources: R1)
- Which notification channels and events are included in 3a? (options: open-ended; sources: R1)
- What is the Linux cloud box or alternative runtime if the spike fails? (options: open-ended; sources: R1)

## Sources

| Ref | Kind | Trust | Author | Title | Reference | Excerpt |
|---|---|---|---|---|---|---|
| R1 | brief | trusted | unknown | 13. Rollout | file:2026-10-07-sindri-design.md | ## 13. Rollout 0. \*\*Measure and decide\*\* (M3, M7, M14): - Turn the audit extraction into \`scripts/transcript-audit/\` |
| R2 | code | untrusted | unknown | run(args: string\[\], cwd: string, budgetMs: number): Promise&lt;ProviderResult&lt;O&gt;&gt; | code:agentic-workflow/judge/src/providers/claude-cli.ts:50 | run = async &lt;O extends string&gt;(args: string\[\], cwd: string, budgetMs: number): Promise&lt;ProviderResult&lt;O&gt;&gt; =&gt; { let r |
| R3 | code | untrusted | unknown | now() | code:agentic-workflow/judge/tests/adjudicate.test.ts:9 | now = () =&gt; new Date("2026-10-04T00:00:00.000Z") |
| R4 | code | untrusted | unknown | run(m: QuestionModule&lt;unknown, string&gt;): Promise&lt;string&gt; | code:agentic-workflow/judge/tests/adjudicate.test.ts:71 | run = async (m: QuestionModule&lt;unknown, string&gt;): Promise&lt;string&gt; =&gt; { const db = openDb(":memory:"); upsert |
| R5 | code | untrusted | unknown | all(): boolean | code:agentic-workflow/judge/tests/detect.test.ts:67 | all = (): boolean =&gt; true |
| R6 | code | untrusted | unknown | run(fallbackRules: QuestionModule&lt;Input, Output&gt;\["fallbackRules"\], answer: Output, id: string) | code:agentic-workflow/judge/tests/evaluate.test.ts:333 | run = async (fallbackRules: QuestionModule&lt;Input, Output&gt;\["fallbackRules"\], answer: Output, id: string) =&gt; { const |
| R7 | code | untrusted | unknown | now() | code:agentic-workflow/judge/tests/label.test.ts:6 | now = () =&gt; new Date("2026-10-04T00:00:00.000Z") |
| R8 | code | untrusted | unknown | now() | code:agentic-workflow/judge/tests/outcomes.test.ts:86 | now = () =&gt; new Date("2026-10-04T00:00:00.000Z") |
| R9 | code | untrusted | unknown | now() | code:agentic-workflow/judge/tests/prompt-sort/adjudicate.test.ts:8 | now = () =&gt; new Date("2026-10-04T00:00:00.000Z") |
| R10 | code | untrusted | unknown | now() | code:agentic-workflow/judge/tests/turn-import.test.ts:10 | now = () =&gt; new Date("2026-10-04T00:00:00.000Z") |
| R11 | code | untrusted | unknown | now(): string | code:agentic-workflow/scorer/src/transcript/cursor.ts:70 | now = (): string =&gt; { if (lastTs \!== null) return lastTs; mtime ??= fs.statSync(file.path).mtime.toISOString(); |
| R12 | code | untrusted | unknown | run(): Promise&lt;string\[\]&gt; | code:agentic-workflow/scorer/tests/audit-labeling.test.ts:94 | run = async (): Promise&lt;string\[\]&gt; =&gt; { const out = fs.mkdtempSync(path.join(os.tmpdir(), "label-")); const r |
| R13 | code | untrusted | unknown | run(c: { projects: string; out: string }) | code:agentic-workflow/scorer/tests/audit-run.test.ts:21 | run = (c: { projects: string; out: string }) =&gt; runAudit({ projectsDir: c.projects, since: new Date("2026-10-01T00:00: |
| R14 | code | untrusted | unknown | run(over: Partial&lt;UiEvidenceRunRecord&gt;): UiEvidenceRunRecord | code:agentic-workflow/scorer/tests/ui-evidence-cost.test.ts:7 | run = (over: Partial&lt;UiEvidenceRunRecord&gt;): UiEvidenceRunRecord =&gt; ({ ts: "t", brokenSteps: 0, visual: "unchanged", pr: |
| R15 | code | untrusted | unknown | close(): void | code:agentic-workflow/sindri/src/adapters/plan-file/parse.ts:29 | close = (): void =&gt; { if (cur \!== null) tasks.push({ ...cur.task, body: cur.lines.join("\\n").trim() }); cur = nu |
| R16 | code | untrusted | unknown | all(): Promise&lt;Result&lt;Entry\[\]&gt;&gt; | code:agentic-workflow/sindri/src/adapters/plan-file/tracker.ts:71 | async function all(): Promise&lt;Result&lt;Entry\[\]&gt;&gt; { if (\!fs.existsSync(dir)) { const what = fs.existsSync(o.repoP |
| R17 | code | untrusted | unknown | ulid(now: Date = new Date()): string | code:agentic-workflow/sindri/src/ids.ts:7 | export function ulid(now: Date = new Date()): string { let t = now.getTime(); let time = ""; for (let i = 0; i &lt; 1 |
| R18 | code | untrusted | unknown | structureStamp(utilityGlobs: readonly string\[\], extraPatterns: readonly { kind: string; regex: string }\[\]): string | code:agentic-workflow/sindri/src/index/build.ts:28 | structureStamp = (utilityGlobs: readonly string\[\], extraPatterns: readonly { kind: string; regex: string }\[\]): string =&gt; |
| R19 | code | untrusted | unknown | setLayer(db: IndexDb, layer: Layer, stamp: string, status: LayerStatus, detail: string, now: Date): void | code:agentic-workflow/sindri/src/index/build.ts:48 | function setLayer(db: IndexDb, layer: Layer, stamp: string, status: LayerStatus, detail: string, now: Date): void { db |
| R20 | code | untrusted | unknown | stampOf(db: IndexDb, layer: Layer): string \| null | code:agentic-workflow/sindri/src/index/build.ts:54 | function stampOf(db: IndexDb, layer: Layer): string \| null { return (db.prepare("SELECT stamp FROM layers WHERE layer |
| R21 | code | untrusted | unknown | sweepTmp(deps: Deps, live: string): void | code:agentic-workflow/sindri/src/index/build.ts:60 | function sweepTmp(deps: Deps, live: string): void { const dir = path.dirname(live); for (const name of fs.readdirSyn |
| R22 | code | untrusted | unknown | writeStructure(db: IndexDb, files: IndexedFile\[\], utilityGlobs: readonly string\[\], stamp: string, scrubber: Scrubber): { changed: number; removed: number } | code:agentic-workflow/sindri/src/index/build.ts:69 | function writeStructure(db: IndexDb, files: IndexedFile\[\], utilityGlobs: readonly string\[\], stamp: string, scrubber: Scr |
| R23 | code | untrusted | unknown | writeDeps(db: IndexDb, files: IndexedFile\[\]): void | code:agentic-workflow/sindri/src/index/build.ts:101 | function writeDeps(db: IndexDb, files: IndexedFile\[\]): void { const rows = files.filter((f) =&gt; f.path === "package.jso |
| R24 | code | untrusted | unknown | embedLayer(db: IndexDb, embedder: Embedder \| null, now: Date, limit: number): Promise&lt;void&gt; | code:agentic-workflow/sindri/src/index/build.ts:113 | async function embedLayer(db: IndexDb, embedder: Embedder \| null, now: Date, limit: number): Promise&lt;void&gt; { if (embed |
| R25 | code | untrusted | unknown | graphLayer(db: IndexDb, provider: GraphProvider \| null, deps: Deps, repoPath: string, deny: string\[\], ix: LoadedProfile\["profile"\]\["index"\], now: Date): Promise&lt;void&gt; | code:agentic-workflow/sindri/src/index/build.ts:148 | async function graphLayer( db: IndexDb, provider: GraphProvider \| null, deps: Deps, repoPath: string, deny: string\[\], |
| R26 | code | untrusted | unknown | buildIndex(deps: Deps, loaded: LoadedProfile, repo: string, o: { full: boolean; quick?: boolean; mirror?: boolean; embedLimit?: number; lockTimeoutMs?: number }, providers: Providers): Promise&lt;BuildReport&gt; | code:agentic-workflow/sindri/src/index/build.ts:190 | export async function buildIndex( deps: Deps, loaded: LoadedProfile, repo: string, o: { full: boolean; quick?: boolean |
| R27 | code | untrusted | unknown | indexPath(deps: Deps, repo: string): string | code:agentic-workflow/sindri/src/index/db.ts:40 | export function indexPath(deps: Deps, repo: string): string { return path.join(stateDir(deps), "index", \`${repo}.db\`); |
| R28 | code | untrusted | unknown | openIndex(file: string): IndexDb | code:agentic-workflow/sindri/src/index/db.ts:44 | export function openIndex(file: string): IndexDb { fs.mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 }); |
| R29 | code | untrusted | unknown | get(k: string) | code:agentic-workflow/sindri/src/index/db.ts:140 | get = (k: string) =&gt; (db.prepare("SELECT value FROM meta WHERE key = ?").get(k) as { value: string } \| undefined)?.value |
| R30 | code | untrusted | unknown | isSourcePath(p: string): boolean | code:agentic-workflow/sindri/src/index/files.ts:20 | export function isSourcePath(p: string): boolean { return SOURCE.test(p) && \!p.endsWith(".d.ts"); } |
| R31 | code | untrusted | unknown | inventory(git: GitRunner, repoPath: string, o: { denyPaths: readonly string\[\]; maxFileKB: number; maxTotalMB: number; select: (p: string) =&gt; boolean }): Promise&lt;{ files: IndexedFile\[\]; skipped: { path: string; reason: SkipReason }\[\] }&gt; | code:agentic-workflow/sindri/src/index/files.ts:33 | export async function inventory( git: GitRunner, repoPath: string, o: { denyPaths: readonly string\[\]; maxFileKB: n |
| R32 | code | untrusted | unknown | withHeavyLock(deps: Deps, kind: string, timeoutMs: number, fn: () =&gt; Promise&lt;T&gt;): Promise&lt;T&gt; | code:agentic-workflow/sindri/src/index/heavy-lock.ts:149 | export async function withHeavyLock&lt;T&gt;(deps: Deps, kind: string, timeoutMs: number, fn: () =&gt; Promise&lt;T&gt;): Promise&lt;T&gt; { |
| R33 | code | untrusted | unknown | refreshMirror(deps: Deps, name: string, repoPath: string): Promise&lt;string&gt; | code:agentic-workflow/sindri/src/index/mirror.ts:11 | export async function refreshMirror(deps: Deps, name: string, repoPath: string): Promise&lt;string&gt; { const mirror = mirr |
| R34 | code | untrusted | unknown | reconcileShape(db: Ledger, deps: Deps, loaded: LoadedProfile, epoch: number): Promise&lt;{ linked: number; labeled: number }&gt; | code:agentic-workflow/sindri/src/index/reconcile.ts:205 | export async function reconcileShape(db: Ledger, deps: Deps, loaded: LoadedProfile, epoch: number): Promise&lt;{ linked: nu |
| R35 | code | untrusted | unknown | shapeSideSteps(db: Ledger, deps: Deps, loaded: LoadedProfile, epoch: number): Promise&lt;SideSteps&gt; | code:agentic-workflow/sindri/src/index/reconcile.ts:221 | export async function shapeSideSteps(db: Ledger, deps: Deps, loaded: LoadedProfile, epoch: number): Promise&lt;SideSteps&gt; { |
| R36 | code | untrusted | unknown | why(e: unknown): string | code:agentic-workflow/sindri/src/index/reconcile.ts:223 | why = (e: unknown): string =&gt; makeScrubber().scrub(e instanceof Error ? e.message : String(e)).text.split("\\n")\[0\] |
| R37 | code | untrusted | unknown | now(): number | code:agentic-workflow/sindri/src/index/shape.ts:89 | now = (): number =&gt; deps.now().getTime() |
| R38 | code | untrusted | unknown | ingestSpool(db: Ledger, deps: Deps, epoch: number): { runs: number; signals: number; quarantined: number } | code:agentic-workflow/sindri/src/index/spool.ts:102 | export function ingestSpool(db: Ledger, deps: Deps, epoch: number): { runs: number; signals: number; quarantined: number |
| R39 | code | untrusted | unknown | profileDir(d: Deps) | code:agentic-workflow/sindri/tests/profile-commands.test.ts:19 | profileDir = (d: Deps) =&gt; path.join(d.env.AW\_STATE\_DIR as string, "profile") |
| R40 | code | untrusted | unknown | canEmbedImagesInPr(p: Provenance, hasApprovedUploader: boolean): boolean | code:agentic-workflow/skills/ui-evidence/src/parity-publish.ts:18 | export function canEmbedImagesInPr(p: Provenance, hasApprovedUploader: boolean): boolean { return p === "seeded" && ha |
| R41 | code | untrusted | unknown | buildAttachmentPlan(s: ParitySummary, runDir: string, sizeOf: (file: string) =&gt; number): PlannedAttachment\[\] | code:agentic-workflow/skills/ui-evidence/src/parity-publish.ts:63 | export function buildAttachmentPlan(s: ParitySummary, runDir: string, sizeOf: (file: string) =&gt; number): PlannedAttachme |
