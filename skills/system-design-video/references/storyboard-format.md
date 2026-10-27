# storyboard.json

```json
{
  "inks": { "board": "color.surface.board", "chalk": "color.ink.chalk", "primary": "color.accent.primary" },
  "strokeWidth": 6, "lead": 0.4, "tail": 0.9, "font": "optional css font stack",
  "beats": [
    { "id": "base", "cam": [2700, 640, 0.8], "camDur": 1.5, "hold": 1.1,
      "say": "Now there is one base limit for the API: 100 per minute.",
      "items": [ { "type": "box", "x": 2400, "y": 230, "w": 600, "h": 320, "color": "chalk" } ] }
  ]
}
```

`inks` is required to contain `board` (the surface) and `chalk` (the neutral ink); every other name is yours. Values are token paths into `--tokens` (aliases like `{color.brand.500}` are followed) or literal `#rrggbb`. Items use ink names as `color`. `render.ts` writes `resolved-inks.json` with the hex each ink resolved to.

World coordinates are free; `cam` is `[centerX, centerY, zoom]`: the frame shows 1920/zoom by about 980/zoom world units, centered there. A beat's camera eases from the previous beat's over `camDur` (default 1.1 s). Beats run in order; `dur` overrides a beat's length (default: the last item's end plus `hold`, 1.1 s).

## Items

Drawn in order; each starts when the previous ends unless `at` (seconds from the beat start) is set. All items accept `dur` and `at`.

| type | fields | notes |
| --- | --- | --- |
| `box` | `x y w h color [fill] [id]` | `fill: true` adds a faint tint once drawn |
| `text` | `text x y size color [anchor=middle\|start] [bold] [id]` | `y` is the baseline; writes on left to right; duration scales with length |
| `arrow` | `from [x,y] to [x,y] color [via [[x,y]..]]` | the shaft draws, then the head |
| `table` | `x y cols [w..] rows [[..]] [rowH] [size] [color] [colors]` | the first row is the header |
| `cross` | `over id color` | strikes through an earlier box or text and dims it |
| `ring` | `over id color` | circles an earlier box or text |

## Output of render.ts
`footage.mp4` (silent, 30 fps), `layout.json` (each beat's `start`, `land` (its last stroke done) and `end`, plus the caption), `resolved-inks.json`. `--still t1,t2` writes PNGs only; `--range a..b` renders a slice.

## Planning a design into beats
One diagram or concept is a scene of 2 to 4 beats, one spoken sentence each; each scene gets its own region of the world and the camera pans between them. Data model: draw the table, then the key columns, then an example row. A flow: draw the input, one arrow per function, the result inside the receiving box. Rollout: draw the states left to right and move a marker. PR order: draw the boxes, then the dependency arrows.
