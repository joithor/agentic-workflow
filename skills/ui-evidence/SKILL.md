---
name: ui-evidence
description: Generate Playwright-based UI evidence for a web-app PR — plan with qa-runner, run headless with no model, repair broken selectors via judge, lint for sloppiness, and publish (ask-first) to Linear and a PR comment. Local stack only, never dev/prod.
---

# UI evidence

1. Doctor the stack: `bash scripts/doctor.sh`. Refuse to proceed if unhealthy or foreign (RF-1).
2. **Spawn a subagent** — the `qa-runner` custom agent (installed per provider) — with the PR diff and `verify-web-app`'s route map to write a script (`script-schema.ts`'s shape).
3. `with_stack_lock_and_heavy_job_lock 120 node dist/bin.js <script.json> <run-dir> [--baseline main.png] [--app-build <sha>] [--fixtures <id>] [--cache <manifest.json>]` (exit 0 all steps passed, 2 a step failed/broken, 1 bad usage or invalid script; the summary JSON is printed and written to `<run-dir>/summary.json`, including `appBuild` — pass `--app-build $(git rev-parse HEAD)` so the run is tied to a commit).
4. Check DB provenance (`checkDbProvenance`) before deciding whether Linear upload is even offered.
5. Visual verdict, cheapest first (`visual-gate.ts`): (a) deterministic pixel compare of the last passed screenshot against the approved main baseline — identical pixels and all steps passed ⇒ `unchanged`, **no model call**; (b) verdict cache (`verdict-cache.ts`) keyed on the actual after+baseline image hashes plus prompt/model version, app build, script, fixtures, viewport and browser version — a hit reuses the prior critique and is recorded as a cache hit; (c) otherwise one `judge visual-critique` call on the cropped changed region (image-capable CLI only — Jev is text-only and never sees images). Any failure, timeout, or out-of-enum result is `unchecked` — never a default `looks-right`. A run with a failed/broken step always gets a critique. The browser run and every step check always execute; a cached or `unchanged` visual never stands in for a behavior check.
6. `publishEvidence(...)` — every write asks first, per this repo's policy. Artifact URLs (screenshots, `*-trace.zip`, videos, diff overlay) come only from an approved `uploadArtifact` uploader and only when DB provenance is `seeded`; otherwise evidence stays local paths.

## Host override (guard rails)

`localhost:3000` stays the default. `--host https://pr-<n>.vitalize.build --allow-preview-host` runs against a PR living preview and nothing else: the host must be exactly `pr-<digits>.vitalize.build` over https with no port, path or credentials, so dev, staging and prod hosts are refused. A host override is read-only: click steps whose target reads like save/submit/update/delete/create/confirm/publish/send/apply/approve/post/add are refused unless `--allow-writes`. Credentials come only from environment variables (`doppler run --project vitalize --config dev_preview -- …`); they are never printed, and error text is scrubbed of them. A preview is a scrubbed customer extract: its provenance is `scrubbed`, not `seeded`.

## Design-parity mode (Figma pixel diff)

Compares the implementation with a design export instead of with a main-branch screenshot. Existing usage is unchanged.

```bash
with_stack_lock_and_heavy_job_lock 120 node dist/bin.js parity <manifest.json> <run-dir> \
  [--host https://pr-<n>.vitalize.build --allow-preview-host] [--allow-writes] [--app-build $(git rev-parse HEAD)] [--db-provenance seeded|unknown]
```

Exit 0 all geometry checks passed, 1 bad usage/manifest/guard, 2 the run errored or a geometry check failed. `<run-dir>/parity.json` is the summary.

1. **Design PNGs at 1x.** Get each frame at its native size with the Figma MCP `get_screenshot` (frame node, scale 1) and save it under the run, or accept any PNG export. The default viewport is the Figma frame (1512x982, DPR 1) — set `viewport` to match your frame.
2. **Manifest** (`parity-schema.ts`; worked example in `examples/design-parity/example-manifest.json`): `route`, `ready` (an element that must be visible before anything is asserted — a text-absent check straight after `goto` passes vacuously on the loading spinner), optional `login` (env var names + CSS selectors only), `settleMs`, `threshold` (default 0.1), `includeAA` (default false), and `frames[]`. Each frame has `designPng`, `designRegion {x,y,w,h}`, an `anchor` (`{testId}` or `{text}`) whose bbox origin locates the implementation crop (plus optional `anchorOffset`), optional `steps` (same shape as script steps, run in order on the same page so states accumulate), `formValues` (typed to match the design's sample data, never saved), `expectBoxes`, `knownDifferences` and `designNode`.
3. **Output per frame:** `<name>-design.png`, `-implementation.png`, `-diff.png`, `-side-by-side.png` (design | implementation | diff, 12px gaps), `-full.png`, and a `parity.json` entry: `diffPercent`, `diffPixels`, `total`, anchor bbox, `sizeDelta` vs the design region, `boxChecks`, `knownDifferences`. Pure, unit-tested primitives live in `parity.ts` (`cropRegion`, `compareRegion`, `sideBySide`, `checkBoxes`).
4. **Geometry assertions:** `expectBoxes: [{target, relativeTo?, expected:{w,h,x,y}, tolerance?}]` (tolerance default 0; x/y are relative to `relativeTo` when given). A mismatch, or an element that cannot be found, fails the run (exit 2) and is printed.
5. **Known differences:** list intentional differences (copy, data) per frame. They are carried into the report, the Linear subtitle and the PR text, so a non-zero diff is explained, never hidden. Do not list a difference you have not looked at.
6. **Capture hygiene** is built in: `document.fonts.ready`, `settleMs` before each screenshot (a popover mid fade-in gives a see-through shot), blur of the active element plus a click on a neutral spot (a Radix select restores focus after closing and leaves a blue ring), scroll containers reset to the top, and `formValues` applied before capture. Raise `settleMs` for slow animations.

### Publishing parity evidence (images to Linear, text to GitHub)

- **Policy** (`parity-publish.ts`): images go to Linear, GitHub stays text-only. Linear upload is allowed for `seeded` and `scrubbed` provenance after an explicit ask; `unknown` never leaves the machine. A GitHub comment carries image URLs only for `seeded` data with an approved uploader (`renderParityPrComment` has none). Every write still asks first.
- **Review gate:** before any upload, open every image in the plan (the Read tool shows PNGs) and compare it with its `expectation`. Reject mid-animation shots, focus rings and see-through popovers and re-capture. `parity-plan` stays blocked (exit 3) until the files are listed: `node dist/bin.js parity-plan <run-dir> --reviewed <file>,<file>`.
- **Per image, one at a time** (the signed URL lives 60s): `mcp: linear/prepare_attachment_upload` (issue, filename, contentType `image/png`, exact size) → PUT the bytes with every returned header verbatim (`buildPutCommand` gives the argv; `curl --data-binary`) → `mcp: linear/create_attachment_from_upload` with `assetUrl`, the plan's `title` (`Pixel diff: <state> (Figma | implementation | diff)`) and `subtitle` (design node, build, viewport, diff %, known differences). Never pass `--app-build` a commit you did not verify; an unverified build prints as "unverified".
- **Replacing stale evidence:** list the issue's attachments (`get_issue`) before attaching, attach the new ones first, then show the user the IDs of earlier attachments whose title starts with `Pixel diff: ` (`selectStale`) and delete them (`mcp: linear/delete_attachment`) only after the user approves those IDs.
- **Optional summary:** one `mcp: linear/save_comment` built from `renderLinearComment` (regions, percentages, geometry, known differences).

## Step checks and selector repair

Every script step carries a machine-checkable `expectedState` (`text-visible`, `text-absent`, `testid-visible`, `url-path`, `input-value`) asserted after the action. A step whose action works but whose expectation fails is `failed`. A step whose selector is broken (click/fill only) goes to `judge ui-element-repair` with the actual broken target; the judge's chosen index is validated against the currently visible DOM, the same action is executed on that candidate, and the expected state is re-checked — only then is the step `passed`. Two attempts max, then `broken`.

## Cost baseline

Each run writes `summary.json` with every model invocation (planning, selector-repair, visual-critique): elapsed time, and token counts that are `null` (unknown) unless the provider reported them. `scorer` renders per-phase and per-PR/route tables and prints `unknown`, never `0`. To link the planner's single call to its run, add `planning: { pr, model, elapsedMs, inputTokens?, outputTokens? }` to the script. See `docs/ui-evidence-cost.md` for the measurement protocol and status of the planner-model pilot.

## Configuration

These values are deployment-specific and never hardcoded. Set them as
environment variables, or in an uncommitted `.ui-evidence.local.env`
(next to this SKILL.md — see `.gitignore`) that `doctor.sh` sources.

- `UI_EVIDENCE_APP_TITLE` — the app's `<title>` text, used by `doctor.sh` to
  confirm `:3000` is this app and not a foreign process. Required; `doctor.sh`
  fails with a clear message if it's unset and not found in the local config.
- `UI_EVIDENCE_SEED_DOMAINS` — comma-separated list of email domains that
  count as seed/synthetic data for `checkDbProvenance`. Default: `example.com`.
- `UI_EVIDENCE_SEED_EMAIL_PATTERN` — the SQL `LIKE` pattern used to find a
  seed marker row in `public.profile`. Default: a generic example.com pattern.
  Unconfigured, both defaults only match fictional example.com addresses, so
  provenance fails closed ("unknown") against any real database.
- `UI_EVIDENCE_USER_AGENT` — the browser's user agent. Default: desktop Chrome at the
  bundled Chromium's version, because headless Chromium's own UA says "HeadlessChrome"
  and apps that gate on browser support (Vitalize's unsupported-browser page) block it.
