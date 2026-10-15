# Sindri UI screens: build and check tools

Everything here works on `mockups/*.html` (the eleven mockups), `fixture.md` and `prototype.html`. Nothing here touches the
mockups, the fixture, the fix list or the spec; the tools write `prototype.html`, `prototype-*.png` and temp files only.

## Serve

```bash
# mockups on 8765 (dom-check.cjs reads these)
cd docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/mockups && python3 -m http.server 8765 --bind 127.0.0.1 &
# the spec dir on 8766 (the prototype, and the mockups again under /mockups/ for css-equiv and text-parity)
cd docs/superpowers/specs/2026-10-13-sindri-ui-screens-design && python3 -m http.server 8766 --bind 127.0.0.1 &
```

Override the URLs with `PROTO_URL` (default `http://127.0.0.1:8766/prototype.html`) and `MOCK_URL`
(default `http://127.0.0.1:8766/mockups/`).

Playwright lives in `skills/ui-evidence`, so run every `.mjs` script from there (they resolve `playwright` from the current
directory, not from this folder):

```bash
cd skills/ui-evidence
T=../../docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/tools
```

## Check the mockups

```bash
NODE_PATH="$PWD/node_modules" node $T/dom-check.cjs [home,agents,...]   # the 11 mockups: width, pill, fonts, dashes, borders, chips
```

## Build the prototype

```bash
python3 docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/tools/build-prototype.py            # writes prototype.html
python3 .../tools/build-prototype.py --check                                                          # exit 1 if prototype.html is stale
python3 .../tools/build-prototype.py --ref 9f20ed53 --out /tmp/old.html                                # build from the mockups at a git ref
python3 .../tools/build-prototype.py --css-only /tmp/merged.css                                        # just the merged mockup CSS
```

`prototype.html` = `prototype-shell.html` (head, body skeleton, controls panel)
+ CSS (the mockups' stylesheets: rules every mockup has stay unscoped once; every other rule is scoped to the pages that have it with a
zero-specificity `:where(.v-page)` prefix and the per-page rule order is kept, so each page's cascade is the mockup's)
+ `prototype-only.css` (sheets, menus, palette, banners, controls panel: not from the mockups)
+ `TPL` (the SVG fragments of `context.html` and `system.html`, nodes made focusable)
+ `prototype-runtime.js` (helpers, the fixture and `CHIP_TABLE`, page renderers, hash router, the `ACTS` data-action table,
sheets, menus, confirms, palette, controls panel, announcements).

The fixture and the renderers live in `prototype-runtime.js`: edit that file (and `prototype-only.css`) for behaviour, the
mockups for look, then rebuild. Building twice gives the same bytes.

## Check the prototype

```bash
node $T/css-equiv.mjs /tmp/merged.css      # after --css-only: every element's computed style = the mockup's, 11 pages
node $T/walk.mjs                           # the main click-through (needs-you, sheets, G chords, Context, Agents, System, palette, conn states)
node $T/more.mjs                           # decisions, answers, seen/undo, Workflows, Work, Recipes, inspector, System, Context
node $T/extra.mjs                          # connection states, deep links, keyboard, layout limits, whole-page audits
node $T/proto-dom.mjs                      # 37 states x (scrollWidth, pill, fonts, dashes, borders, band) + the chip audit vs fixture.md
node $T/text-parity.mjs [pages]            # visible text of each mockup vs the same prototype view (M = only mockup, P = only prototype)
```

Each script prints `PASS`/`FAIL` per step and exits 1 on any failure or page error. `proto-dom.mjs` reads the chip table out of
`fixture.md` ("State chips"), so a new word or class there is enforced without touching the script.

## Re-capture the PNGs

```bash
node $T/capture.mjs [home,agents,...]      # prototype-{home,workflows,workflow-project,item-sheet,recipe-inspector,agents,context,system,palette,stalled}.png
```

1280 wide, the controls panel hidden, each at its page height (at least 900). Compare each with `mockups/<page>.png`
(palette and stalled have no mockup; they compare with `mockups/home.png`). Context is captured in the prototype's first state
(46 pending, the live "Accept all deterministic"); the mockup draws the state right after that action.

## Files

| File | Role |
|---|---|
| `build-prototype.py` | the generator |
| `prototype-shell.html`, `prototype-only.css`, `prototype-runtime.js` | build inputs (everything not taken from the mockups) |
| `lib.mjs` | shared: Playwright from cwd, the PASS/FAIL runner, helpers |
| `walk.mjs`, `more.mjs`, `extra.mjs` | scripted click-throughs |
| `proto-dom.mjs` | DOM state checks and the chip audit |
| `css-equiv.mjs` | proves the CSS merge changes nothing |
| `text-parity.mjs` | mockup vs prototype text |
| `capture.mjs` | PNG re-capture |
| `dom-check.cjs` | the same DOM checks on the mockups themselves |
