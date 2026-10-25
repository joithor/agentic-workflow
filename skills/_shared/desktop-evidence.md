# Desktop Evidence (shared)

How verify-desktop, design-verify-desktop and ui-evidence (desktop mode) prove a desktop UI change: the **real app**, launched on **seeded data**, driven through its own UI, each screen captured and pixel-compared with its mockup. Nothing here names an app: the repo's own config does.
Referenced via: SHARED_DIR pattern (CD2).

## Config (read first)

`<repo>/.agentic-workflow/evidence.json`, its `desktop` section: the screens manifest (`manifest`), the seeded-data fixture, the command that starts the app, the UI driver's command, the window `sizes` and the pixel `threshold`. Read it and the screens manifest it names; both belong to the repo.

- No `evidence.json`, no `desktop` section or no manifest: **stop**. Report `desktop evidence is not configured for this repo` and treat the run as FAIL for gating. Never invent a fixture, an app command or a mockup.
- A requested screen the manifest does not list: **stop** and name it. Mockups come from the manifest's `designRoot`; never substitute a screenshot of the app for a mockup.
- Never edit the config, the manifest or a mockup to make a run pass. Differences the owner accepted are listed per screen in the manifest (`accepted`); nothing else is excused.

## Running it

Two routes to the same pack; take the first that applies:

1. **The workflow engine, when present.** The repo's agent instructions name a workflow engine CLI and it is on the PATH, and the `workflow.evidence-desktop` recipe is registered there. Start it with `workflow start workflow.evidence-desktop --item <id>` plus the inputs the recipe declares (`recipe show` lists them), then follow it with `workflow status`. The recipe runs the check, has an agent review each screen's frames against its mockup, and repairs a broken run at most twice. Its result is the pack below.
2. **Directly.** The Evidence `desktop-run` verb (the `ui-evidence` binary):

```bash
ui-evidence desktop-run <manifest> <run-dir> --repo "$(git rev-parse --show-toplevel)" --app-build "$(git rev-parse HEAD)" [--screens a,b]
```

`<run-dir>` is a new directory under `$AW_DIR` (the caller names the domain: `verification/` or `design/verify/`). Exit 0 every screen passed, 2 a screen failed, 1 usage or a refusal, 4 the run errored (the app would not start, the driver did not answer). `desktop-run` takes its own locks and starts the display; do not wrap it.

Pass `--screens` with the screens the diff touches (names from the manifest); with no argument or `auto`, infer them from the changed view, style and resource files and the manifest's screen names, and when none can be inferred run all.

## The pack

`<run-dir>/summary.json` (`schema: "ui-evidence.desktop/1"`): `outcome` passed | failed | refused | errored, `screens[]` each with `name`, `size`, `diffPercent`, `threshold`, `passed`, `problem`, `files` (`app`, `design`, `diff`, `sideBySide`), `accepted`, `masked`; plus `report.md` and `<screen>-<WxH>/` with `app.png`, `design.png`, `diff.png`, `side-by-side.png`.

## Judging it

The pack is judged **against the mockups**: the owner expects the UI to match them.

1. Open every screen's `side-by-side.png` (and `diff.png` where `diffPercent` is above 0) and list each concrete mismatch: layout, a missing element, the wrong band, colour, a label. Skip only what that screen's `accepted` lists.
2. A screen with `passed: false`, a `problem`, or an unaccepted mismatch is a **FAIL**. Any unexpected mismatch fails the run, even inside the pixel threshold: the threshold absorbs rendering noise, not a wrong element.
3. `outcome` `refused` or `errored` is not a pass and not a mismatch finding: report it as FAIL with the `refusal`/`error` code and message.
4. Findings are region-level (where and what you see); do not attribute them to a token or style value you did not read.

Do not modify code, config, manifest or mockups while verifying; fixes go back to the implementer and the run repeats on the fixed code.
