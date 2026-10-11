# Evidence — `ui-evidence`

`Aw.Evidence` (`src/Evidence`, tier 2, full shape: `Core`, `Features`, `Infrastructure`, `Host`) drives a web app in a
headless browser and records what it saw: a scripted run's steps, screenshots, lint findings and visual verdict
(`summary.json`), and design parity against exported design frames (`parity.json`). The binary is `ui-evidence`
(`Aw.Evidence.Host`, framework-dependent). Until the switch the installed toolkit keeps running the TypeScript
`skills/ui-evidence`; nothing installs or calls this binary yet ("At the switch" below).

```bash
dotnet run --project src/Evidence/Aw.Evidence.Host -- <command> [arguments]
```

## Commands

Every command binds its arguments, calls one use case and maps the result to an exit code. Arguments are strict: an
unknown flag, a flag without its value (a value never starts with `--`), a repeated flag or the wrong number of
positionals is a usage error. Paths may be relative; the binary makes them absolute against the working directory, and
every path it writes into a run file is absolute.

| Command | Does | Exit |
|---|---|---|
| `run <script.json> <run-dir> [--baseline png] [--app-build sha] [--fixtures id] [--cache manifest.json] [--host url] [--allow-preview-host] [--allow-writes] [--lock-timeout s]` | Runs every step of a UI script in each of its viewports, writes `<run-dir>/summary.json` and prints it | 0 every step passed · 2 a step failed or broke · 1 · 4 · 75 |
| `parity <manifest.json> <run-dir> [--host url] [--allow-preview-host] [--allow-writes] [--app-build sha] [--db-provenance seeded\|unknown] [--lock-timeout s]` | Deletes `<run-dir>/parity.json`, measures each design frame, writes `parity.json` and prints it; each failed box check is also a stderr line `ui-evidence: geometry: <frame> <target>: <failure>` | 0 every geometry check passed · 2 one failed · 1 · 4 · 75 |
| `parity-plan <run-dir> [--reviewed file,file]` | Prints the Linear attachment plan (one side-by-side image per frame) | 0 every image reviewed · 3 an image is unreviewed · 1 provenance unknown (AW-EVIDENCE-016: the evidence stays local) |
| `parity-comment <run-dir> --for linear\|github` | Prints the comment as markdown (Linear: a table with geometry; GitHub: a table, images stay on Linear) | 0 · 1 |
| `parity-stale --attachments <file.json> --keep id,id` | Prints the JSON array of `Pixel diff: ` attachments not kept, from a JSON array of `{id, title}` (other fields ignored) | 0 · 1 |
| `parity-put --upload <file.json> --file <png>` | Prints the JSON argv of the signed PUT (`curl … -H "<name>: <value>"` for every signed header, verbatim; run it within a minute of signing) from `{url, headers}` | 0 · 1 |
| `publish-plan <run-dir> --provenance seeded\|unknown [--linear-issue id]` | Prints the publish plan for `summary.json` (below) | 0 · 1 |
| `doctor` | Prints `{healthy, failures[]}` for the local stack; each failure is also a stderr line `ui-evidence: doctor: <failure>` | 0 healthy · 1 not |
| `provenance` | Prints `{provenance: seeded\|unknown, reason}` for the local database | 0 |
| `install-browser` | Installs the browser the run and parity commands drive | 0 · 4 |

`--lock-timeout` is whole seconds (default 120; 0 tries once). `--host` takes a bare origin: `http://localhost:<port>`
or `http://127.0.0.1:<port>` (local: no extra flag), or `https://pr-<n>.vitalize.build` with `--allow-preview-host`
(the host guard below). Without `--host` the binary drives `http://localhost:3000`.

### Exit codes

One scheme for every command:

| Exit | Meaning | Errors |
|---|---|---|
| 0 | Done; every check passed | — |
| 1 | Usage error, invalid input, or a guard refused | `ui-evidence: usage: ui-evidence <shape>`; AW-EVIDENCE-001…006, 010, 012, 016 |
| 2 | A check failed: a step failed or broke (`run`), a box check failed (`parity`) | the document is still written and printed |
| 3 | Blocked on review (`parity-plan`) | the plan is still printed |
| 4 | The run itself errored: the browser failed to start or crashed, a frame's anchor is missing, a region runs past the viewport, a file couldn't be written | AW-EVIDENCE-011, 013, 014, 015 |
| 75 | A machine lock stayed busy past `--lock-timeout` | AW-EVIDENCE-009 |
| 130 | Interrupted (Ctrl-C, SIGTERM); the locks are released on the way out | — |

The mapping is by error kind: `timeout` → 75, `unavailable` → 4, anything else → 1.

### stdout and stderr

stdout carries exactly one JSON document, written through the Kernel `AwJson` profile (camelCase, UTC `O` timestamps,
nulls written), or markdown for `parity-comment`. stderr carries one-line messages prefixed `ui-evidence: `:
`ui-evidence: <code> <message>` for an error (an invalid script or manifest names the file, the JSON location and
the problem in the input's own terms: `ui-evidence: AW-EVIDENCE-002 script /work/script.json: $.steps[0]: missing
required property "expectedState"`; "Inputs" below), the `geometry:` and `doctor:` lines above, `ui-evidence: waiting on lock:
<dir>` (once, while a lock is busy), and the Kernel telemetry's JSON lines at `AW_LOG_LEVEL`. No message carries a
sign-in credential: a parity failure's text is scrubbed of the email and password before it leaves the use case.

## Inputs

`run` reads a UI script and `parity` reads a design manifest, both written by the planner (or by hand). Each is one
JSON object read through the package's `AwJson` options into the Core records (`UiScript`, `DesignManifest`); a key
marked required must be present and not null, an optional key may be left out (or `null` where the type says so), and
names are camelCase. A document that doesn't read, or breaks a rule below, is refused before any lock is taken:
AW-EVIDENCE-002 for a script, AW-EVIDENCE-003 for a manifest, exit 1. The message names the file, the JSON location
and the problem (`script <path>: $.steps[0]: missing required property "expectedState"`, `manifest <path>:
$.frames[0].zoom: unknown property`, `$.role: "intern" is not a valid value`, `$: not valid JSON (line 1, column 2)`);
a rule broken after reading is named by its place instead (`step 2: …`, `frame "empty-state": …`), and every broken
rule is listed, joined by `; `.

### Run script (`run <script.json>`)

| Key | Type | Required | Holds and rules |
|---|---|---|---|
| `route` | string | yes | the route under test; recorded in `summary.json` (the run navigates only through `goto` steps) |
| `role` | `manager` \| `staff` \| `central-staffer` \| `super-admin` | yes | the app role; recorded in `summary.json` |
| `viewports` | (`desktop` \| `phone`)[] | yes | at least one; every step runs in each (desktop 1280x800, phone 375x812) |
| `steps` | step[] | yes | at least one, run in order (below) |
| `planning` | object \| null | no | the planner's own model call, recorded as the first `invocations[]` entry: `pr` (string \| null, also `summary.json`'s `pr`), `model` (string \| null), `elapsedMs`, `inputTokens`, `outputTokens` (integer \| null each, not negative; null is unknown, never zero) |

A step:

| Key | Type | Required | Holds and rules |
|---|---|---|---|
| `action` | `goto` \| `click` \| `fill` \| `expect-visible` | yes | what the step does |
| `target` | string | yes | `goto`: a path on the host (one leading `/`; the host guard below); `click`, `fill`: a `data-testid`; `expect-visible`: visible text |
| `expectedState` | object | yes | the state that must hold after the action (below) |
| `value` | string \| null | no | the text a `fill` types; null or absent types nothing |

`expectedState` is one of five kinds, chosen by its `kind` key (anywhere in the object):

| `kind` | Other keys (all required) | Holds when |
|---|---|---|
| `text-visible` | `text` (string, not empty) | some visible element shows the text |
| `text-absent` | `text` (string, not empty) | no visible element shows the text |
| `testid-visible` | `testId` (string, not blank) | the element with that `data-testid` is visible |
| `url-path` | `path` (string, not empty) | the page's URL path is `path` |
| `input-value` | `testId` (string, not blank), `value` (string, may be empty) | that input holds exactly `value` |

A missing `kind` is `missing required property "kind"`; another value names the five kinds. Enum values are matched by
name only (a number is refused). Unknown keys in a script are ignored. The script's SHA-256 is `summary.json`'s
`scriptSha256`.

### Design manifest (`parity <manifest.json>`)

Unknown keys are refused in every manifest object (the manifest, `ready`, `viewport`, `neutralClick`, `login`, each
frame and its `designRegion`, `anchor`, `anchorOffset`, `waitFor`, `formValues[]` and `expectBoxes[]` with their
`target`, `relativeTo` and `expected`): a misspelt key is an error, not a silently ignored setting. The one exception
is a frame's `steps[]`, which use the run script's step format above (unknown keys ignored there, as in a script).

| Key | Type | Required | Default | Holds and rules |
|---|---|---|---|---|
| `route` | string | yes | — | the route opened first; starts with `/` |
| `ready` | selector | yes | — | visible once the route has loaded (past any spinner) |
| `frames` | frame[] | yes | — | at least one; names unique (`duplicate frame name "<name>"`) |
| `viewport` | `{w, h}` (integers) | no | 1512x982 | the browser viewport; both positive |
| `timeoutMs` | integer | no | 60000 | the wait for `ready`, a frame's `waitFor` and its anchor, in ms; positive |
| `settleMs` | integer | no | 600 | the settle time before and after capture preparation, in ms; not negative |
| `threshold` | number | no | 0.1 | the per-pixel colour tolerance of the pixel diff; 0 to 1 |
| `includeAA` | boolean | no | false | count anti-aliased pixels as differences |
| `neutralClick` | `{x, y}` (numbers) | no | 22 px in from the viewport's bottom-right corner | where to click to drop focus before capture |
| `skipNeutralClick` | boolean | no | false | skip that click |
| `designNode` | string \| null | no | null | the design file or node every frame comes from; copied to `parity.json` |
| `login` | object \| null | no | null (no sign-in) | how to sign in first (below) |

A selector (`ready`, `anchor`, `waitFor`, a box's `target` and `relativeTo`) is `{testId}` or `{text}`, exactly one:
`testId` a `data-testid` (not blank), `text` visible text (not empty; the first visible match).

`login`:

| Key | Type | Required | Default | Holds and rules |
|---|---|---|---|---|
| `emailEnv`, `passwordEnv` | string | yes | — | the environment variables holding the credentials; UPPER_SNAKE (`^[A-Z][A-Z0-9_]*$`). The values come only from the environment, never the manifest |
| `emailSelector`, `passwordSelector`, `submitSelector` | string (CSS) | yes | — | the form's fields and submit control; not empty |
| `nextSelector` | string (CSS) \| null | no | null | clicked after the email on a two-step form |
| `loginPath` | string | no | `/login` | sign-in is done once the URL path no longer contains this |

A frame:

| Key | Type | Required | Default | Holds and rules |
|---|---|---|---|---|
| `name` | string | yes | — | letters, digits, `_` and `-`, starting with a letter or digit; names the frame's output files |
| `designPng` | string | yes | — | the design export (PNG at 1x); not empty. A relative path resolves against the manifest's directory, an absolute one is used as is |
| `designRegion` | `{x, y, w, h}` (integers) | yes | — | the compared region of the export; `x`, `y` ≥ 0, `w`, `h` > 0 |
| `anchor` | selector | yes | — | the element whose bounding-box origin locates the implementation crop |
| `anchorOffset` | `{x, y}` (numbers) \| null | no | no offset | added to the anchor's origin |
| `waitFor` | selector \| null | no | null | visible before the frame's steps run |
| `steps` | step[] | no | [] | steps that bring the page into this state (run script steps, same rules; the write guard applies on every host) |
| `formValues` | object[] | no | [] | values typed into the form (never submitted): `value` (string, required) and exactly one of `testId` (not blank) and `css` (not empty) |
| `expectBoxes` | object[] | no | [] | geometry checks: `target` (selector, required), `expected` (required: `{w, h, x, y}`, numbers, at least one set), `relativeTo` (selector: measure `x`, `y` from its top-left instead of the page's), `tolerance` (number ≥ 0 px per axis; absent is 0) |
| `knownDifferences` | string[] | no | [] | intentional differences that explain a non-zero diff; no empty entry |
| `designNode` | string \| null | no | null | the design node (id or URL) this frame was exported from |

## Run files

Scorer and bugfix-state read these files after their own ports; the fields they read are marked †.

### `summary.json` (`schema: "ui-evidence.run/1"`)

| Field | Type | Holds |
|---|---|---|
| `schema` | string | `ui-evidence.run/1` |
| `runId` | string | the run directory's name |
| `startedAt`, `finishedAt` † | string (UTC `O`) | when the run started and ended |
| `route` | string | the script's route |
| `role` | `manager` \| `staff` \| `central-staffer` \| `super-admin` | the script's role |
| `host` | string | the origin driven |
| `appBuild` † | string \| null | `--app-build` (the commit under test, when verified) |
| `fixtures` | string \| null | `--fixtures` |
| `scriptSha256` † | string | SHA-256 (hex) of the script file's bytes as read: proves which script a run executed |
| `pr` | string \| null | the script's `planning.pr` |
| `viewports` | (`desktop` \| `phone`)[] | the script's viewports (1280x800, 375x812) |
| `steps[]` † | object[] | viewport by viewport, in order: `viewport`, `index` (1-based), `action` (`goto` \| `click` \| `fill` \| `expect-visible`), `target`, `status` † (`passed` \| `failed` \| `broken`), `screenshot` (`<run-dir>/<viewport>-<index>-<action>.png`) |
| `visual` | object | `verdict` † (`unchecked` \| `unchanged` \| `looks-right` \| `looks-off` \| `sloppy`), `reasons` (string[]), `diffScore` (number \| null: the fraction of pixels differing from the baseline, 1 for a size change), `diffImage` (string \| null: `<run-dir>/visual-diff.png`) |
| `lintFindings[]` | object[] | `viewport`, `rule` (`overlap` \| `misaligned-edge` \| `phone-overflow` \| `truncated-label` \| `clipped` \| `missing-hover-focus` \| `unresolved-placeholder` \| `non-token-color`), `selector`, `detail` |
| `invocations[]` † | object[] | every model call actually made, in order (none when no judge is wired: "Judge" below): `phase` (`planning` \| `selector-repair` \| `visual-critique`), `model`, `elapsedMs`, `ok` (false: the judge was called and failed or answered nothing), `inputTokens`, `outputTokens` (number \| null: null is unknown, never zero), `cacheHit` (a verdict-cache hit stands in for a critique) |
| `planning` | object \| null | the script's planning block: `pr`, `model`, `elapsedMs`, `inputTokens`, `outputTokens` |
| `evidence` | object | `traces` (string[]: `<run-dir>/<viewport>-trace.zip`), `videos` (string[]) |

A step's `status`: `passed` (the action ran and the expected state held), `failed` (the action ran, the expected state
didn't hold: a behaviour failure, never repaired), `broken` (the action itself failed and no repair made it pass). Every
step runs whatever an earlier one did. The visual verdict comes from the last passed step's screenshot: pixel-identical
to `--baseline` is `unchanged` with no model (never for a run with a failed step); otherwise the verdict cache
(`--cache`, default `<run-dir>/verdict-cache.json`; keyed on the pixels' and baseline's hashes, the prompt version,
`AW_VISUAL_MODEL`, the app build, the script's scenario hash, fixtures, viewports and the browser build; an entry without
an app build is trusted only inside the run that wrote it), then one critique of the changed region. No passed step, no
judge wired, or no answer from the judge, is `unchecked`.

### `parity.json` (`schema: "ui-evidence.parity/1"`)

| Field | Type | Holds |
|---|---|---|
| `schema` | string | `ui-evidence.parity/1` |
| `runId` | string | the run directory's name |
| `host` | string | the origin driven |
| `provenance` | `seeded` \| `scrubbed` \| `unknown` | a preview host is `scrubbed` (or `unknown` when `--db-provenance unknown`); a local host is `seeded` only with `--db-provenance seeded` |
| `appBuild`, `designNode` | string \| null | `--app-build`; the manifest's design node |
| `viewport` | `{w, h}` | the manifest's viewport |
| `frames[]` | object[] | one per manifest frame, in order (below) |

Each frame: `name`, `designRegion` `{x, y, w, h}` (in the design export), `implementationOrigin` `{x, y}` (where the
same-size region starts on the page: the anchor plus `anchorOffset`), `anchorBox` `{x, y, w, h}`, `anchorOffset` `{x, y}`,
`sizeDelta` `{w, h}` (anchor size minus design region size), `diffPixels`, `total`, `diffPercent` (two decimals),
`threshold`, `includeAA`, `boxChecks[]` (`target`, `relativeTo`, `expected` `{w, h, x, y}`, `actual` (or null when an
element is missing), `tolerance`, `pass`, `failure`), `knownDifferences[]`, `files` (`design`, `implementation`, `diff`,
`sideBySide`: `<run-dir>/<frame>-{design,implementation,diff,side-by-side}.png`), `designNode`.

A relative `designPng` in the manifest resolves against the manifest's directory. `parity.json` is deleted before the
run, so a failed run never leaves an earlier summary for `parity-plan` to publish.

### `publish-plan` output

`provenance` (`seeded` \| `unknown`: the claim, downgraded to `unknown` for any host that isn't local), `question` (the
ask-first question; it states what will happen: a Linear upload only when seeded with an issue given, otherwise why
there is none), `linearUploadAllowed` (seeded and an issue given), `files` (the step screenshots), `note`, and
`prComment` (the markdown body).

## Locks

`run` and `parity` hold two box-wide locks for the whole browser run, through Evidence's own Core port
`IMachineLocks` (`AcquireAsync(LockKind, timeout) → Result<ILockLease>`), in a fixed order: the QA-stack lock (the wider
one) first, then the heavy-job lock; failing to take the second releases the first. A guard refusal or a bad input is
reported before any lock is taken. The leases are released on every path: success, failure, exception and
interruption.

The adapter (`DirectoryLocks`) is the same lock the shell tools take, so the two exclude each other both ways:

| | |
|---|---|
| QA-stack lock | `$AW_QA_STACK_LOCK`, else `${AW_STATE_DIR:-~/.agentic-workflow}/locks/qa-stack.lock` |
| Heavy-job lock | `$AW_HEAVY_JOB_LOCK`, else `${AW_STATE_DIR:-~/.agentic-workflow}/locks/heavy-job.lock` |
| Acquire | an atomic `mkdir` of the lock directory (libc `mkdir`; `EEXIST` means busy), polled every second until the timeout; `ui-evidence: waiting on lock: <dir>` once |
| Owner | `<dir>/owner`: `<pid> <UTC time> ui-evidence`, the file the `heavy` wrapper writes too |
| Release | delete our `owner`, then `rmdir`; a directory something else wrote into is left alone |
| `AW_HEAVY_LOCK_HELD=1` | set by the `heavy` wrapper: the parent already holds the heavy-job lock, so that lease is a no-op |
| Stale locks | never taken over (the shell primitive doesn't either); remove a dead holder's directory by hand |

`config/lib/locks.sh` (`acquire_lock` / `release_lock`) is the shell side of the same format; the integration tests
source it to prove the exclusion in both directions.

## Judge

Selector repair and visual critique go through the Core port `IJudge` (`IsAvailable`, `RepairSelectorAsync`,
`CritiqueAsync`). `IsAvailable` says whether a judge is wired at all; callers check it first, and with no judge they
don't call it and record nothing in `invocations[]`, because no model ran. A wired judge's null answer means the call
failed (timed out, errored or answered out of contract); that call is recorded with `ok: false`. Either way every caller
fails closed: no repair (the step stays `broken`), and the verdict stays `unchecked`. A recorded call carries null
token counts when the answer reports none. Until `docs/csharp/judge.md` lands, the composition registers
`UnavailableJudge` (`IsAvailable` false, every answer null; it logs once that it isn't wired), so a run records no
`selector-repair` or `visual-critique` invocation; the CLI adapter over the C# `judge` binary is BLOCKED on that
contract.

## Host guard

| Host | Accepted as | Write guard |
|---|---|---|
| none | `http://localhost:3000` (local) | none |
| `http://localhost:<port>`, `http://127.0.0.1:<port>` (bare origin) | local | none |
| `https://pr-<digits>.vitalize.build` (bare origin) with `--allow-preview-host` | preview | write-looking steps refused without `--allow-writes` |
| anything else (dev, staging, prod, a path, a query, credentials in the URL) | refused, AW-EVIDENCE-004 | — |

Every `goto` target must be a path on the host (one leading `/`); a step that leaves the host is refused
(AW-EVIDENCE-005), and the browser aborts any off-origin navigation. A write-looking step is a click (or a `goto`) whose
target has a write verb (`save`, `submit`, `delete`, …, including fused control names such as `saveBtn`). `parity`
applies the write guard on every host, local included. Sign-in credentials come only from the environment variables
the manifest's `login` names (AW-EVIDENCE-006 lists the missing names, never values).

## Environment

| Variable | Read by |
|---|---|
| `UI_EVIDENCE_USER_AGENT` | `run`, `parity`: the user agent to present instead of desktop Chrome at the browser's build |
| `AW_VISUAL_MODEL` | `run`: the model label in the verdict-cache key (default `judge-default`) |
| `UI_EVIDENCE_APP_TITLE`, `UI_EVIDENCE_CONFIG` | `doctor`: the title the app on :3000 must show at `/login`; else the `NAME=value` env file `UI_EVIDENCE_CONFIG` names |
| `UI_EVIDENCE_SEED_DOMAINS`, `UI_EVIDENCE_SEED_EMAIL_PATTERN` | `provenance`: the seed policy (defaults match only `example.com`, so an unconfigured install stays `unknown`) |
| `AW_QA_STACK_LOCK`, `AW_HEAVY_JOB_LOCK`, `AW_HEAVY_LOCK_HELD`, `AW_STATE_DIR` | the lock adapter |
| the manifest's `login.emailEnv` / `login.passwordEnv` | `parity` sign-in |

`doctor` checks `http://localhost:3000/login`'s title, `docker exec postgres_container pg_isready -U postgres`, and a
TCP connect to `localhost:5002`. `provenance` runs `docker exec postgres_container psql` with the seed pattern passed as
a psql variable, never interpolated into the SQL; a failed query is `unknown` with the reason.

## AOT scope

The Host is framework-dependent, not an AOT entry point: Playwright for .NET has no NativeAOT or trimming support.
`Aw.Evidence.Infrastructure` opts out of AOT analysis (`AwAotCompatible=false`) for the same reason. `Core` and
`Features` stay AOT-compatible (no reflection; JSON through the package's source-generated context), so they could
move behind an AOT host if the browser adapter ever allowed it.

## Ports and adapters

| Port (Core) | Adapter (Infrastructure) | Fake (Testing) |
|---|---|---|
| `IBrowser`, `IBrowserSession` | `PlaywrightBrowser` (headless Chromium) | `FakeBrowser`, `FakeBrowserSession`, `FakePage` |
| `IBrowserInstaller` | the Playwright installer (`install chromium`) | `FakeBrowserInstaller` |
| `IRunFiles` | `FileRunFiles` | `InMemoryRunFiles` |
| `IImageCodec` | `PngCodec` | `FakeImageCodec` |
| `IVerdictCacheStore` | `FileVerdictCacheStore` | `InMemoryVerdictCacheStore` |
| `IMachineLocks` | `DirectoryLocks` | `InMemoryMachineLocks` |
| `IJudge` | `UnavailableJudge` (CLI adapter blocked, above) | `FakeJudge` |
| `IStackProbe`, `IProvenanceQuery` | `ProcessStackProbe`, `DockerPsqlProvenance` | `FakeStackProbe`, `FakeProvenanceQuery` |
| `ISecretsSource` | `EnvironmentSecrets` | `FakeSecrets` |

Each port's contract suite lives in `tests/Aw.Evidence.Testing`; the fake and the adapter both pass it.

## At the switch

What changes for the callers when the install moves to the C# build:

- **SKILL.md** (`skills/ui-evidence`): `with_stack_lock_and_heavy_job_lock 120 node dist/bin.js <script> <run-dir> …`
  becomes `ui-evidence run <script> <run-dir> … [--lock-timeout 120]`. The binary takes both locks itself, so the bash
  wrapper goes. The parity, plan, comment, stale, put and publish steps call `ui-evidence parity …`,
  `ui-evidence parity-plan …` and so on; `scripts/doctor.sh` becomes `ui-evidence doctor`; the provenance probe becomes
  `ui-evidence provenance`; `npx playwright install chromium` becomes `ui-evidence install-browser`.
- **bugfix-state** (`run-ui`): runs `ui-evidence run <script> <run-dir> --app-build <sha>` instead of
  `node <sibling>/dist/bin.js`, reads `summary.json` per the table above (`steps[].status`, `appBuild`, `scriptSha256`),
  and treats exit 75 as "stack busy, try again" rather than a failed check.
- **Scorer**: reads `invocations[]`, `visual.verdict` and `finishedAt` from `summary.json`.
- Exit 4 (the run errored) and 75 (a lock stayed busy) are new; a caller that only knew 0, 1 and 2 must treat both as
  "no evidence", never as a pass.
