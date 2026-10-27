# Gotchas

- **Tokens, not hex.** A token the project lacks falls back to a neutral default for the standard ink names (board, chalk, primary, secondary, tertiary, warning, danger) and `render.ts` prints which; ask the user for the real token or run `/design-language`. Any other ink name must resolve. `render.ts` fails with "token not found" or "not a #rrggbb color" when an ink path is wrong or points at a non-color; fix the path in `inks`. A token with no `#rrggbb` value (a color space object without `hex`) is rejected: use its sRGB hex token.
- **Fonts are system fonts.** On a machine without the listed faces the fallback is `cursive`, which can look very different; read a still before trusting a render, and set `font` in the storyboard for a house hand.
- **`text` y is the baseline and anchor is `middle` by default**: a wide label can run past its box. Read the sheet; shorten it or widen the box.
- **Old content in view.** The camera shows 1920/zoom world units, so an earlier scene within that window shows at the edge. Put scenes farther apart or raise the zoom.
- **Fonts must be loaded before layout.** `render.ts` waits for `document.fonts.ready`; if text boxes look clipped, the font was not ready.
- **playwright-core.** `render.ts` looks at `$PLAYWRIGHT_CORE`, then `~/.agentic-workflow/system-design-video/node_modules/playwright-core`, then `./node_modules/playwright-core`. `scripts/doctor.sh` prints the install command; it never runs it.
- **Frame cost.** A 40 s film is about 1200 frames at 30 fps and takes a couple of minutes; iterate with `--still` and only render the footage once the stills are clean.
- **Colors are verified at the corners** by `deliver.sh` within 2/255 (H.264's floor for flat colors).
