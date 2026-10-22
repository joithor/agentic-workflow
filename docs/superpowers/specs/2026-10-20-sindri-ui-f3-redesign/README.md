# F3 UI redesign prototype (mockups)

> Owner approved 2026-10-20. `specs/<screen>.md` are the build specs; the ui lane's slices follow §8 of each.

Copied from `~/.agentic-workflow/mockups/f3/` on the old box, the redesign the ui lane builds from (goal 3: every demo screen).

- `screens/index.html`: open this first. It links every screen: `needs-you.html`, `workflows.html`, `run-canvas.html`, `context.html`, `setup.html`, `questions.html`, `not-mocked.html`, each with PNG captures at 1440 and 900 and `*.notes.md`. Added in the all-screens pass: `home.html`, `work.html`, `recipes.html`, `agents.html`, `system.html`, `jobs.html` (listed under "Every other screen"), and a "This app" section on `setup.html`. `not-mocked.html` is no longer linked from the rail.
- `specs/`: per-screen specs (needs-you, workflows, run-canvas, context, setup, home, work, recipes, agents, system, jobs).
- `BRIEF.md`, `BRIEF-integration.md`, `BRIEF-setup-questions.md`, `SPEC-BRIEF.md`: the briefs the mockups were made from.
- `data/`: `DATA-MODEL.md`, `PLAIN-WORDS.md`, `AVALONIA-NOTES.md`, `CURRENT-UI.md`, `sample.json` and `steps-catalog.json` (the screens' sample data), and `tools/` (the scripts that built them; `gap_common.py`, `pg_*.py` and `build_gap_screens.py` build the six added pages from the workflows page's CSS, `shots_*.sh` render their PNGs with headless Chrome).
- `_setupq/`: the HTML parts the setup and questions screens were assembled from.

Left out: `ref/` (20 MB of third-party reference-site captures) and `data/_src/` (35 MB, a snapshot of this repo's docs, recipes and source; rebuild it from the repo if a tool needs it).
