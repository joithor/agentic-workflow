# Context whole map on the UI fixture (whole-map spec section 8, W3 and W4)

The real app, not a test host: `sindri serve` (the fixture's guarded temp home, `scripts/sindri-ui-fixture.sh up`, `context up`,
`context whole over|under`) and `sindri-ui` (the Avalonia desktop app, a debug build of main at a78148552) on an Xvfb display,
driven with XTEST key and mouse events by `scripts/sindri-ui-capture.py`. The app is launched with
`sindri://context?repo=aw-split-panes`; the frame run is Home, click Context, type a query, Enter, Esc.

| Frame | State |
|---|---|
| `over-<size>-1-whole.png` | 612 nodes, over the 500-node budget: the whole map by feature (12 areas) with its one line |
| `over-<size>-2-query-zoomed.png` | the query `area 3` zooms the camera to that feature, the rest stays dimmed behind |
| `over-<size>-3-after-esc.png` | Esc goes back to the whole map |
| `under-<size>-1-whole.png` | 14 nodes, under budget: every node drawn |
| `under-<size>-2-query-zoomed.png` | the query `tab groups` |
| `under-<size>-3-after-esc.png` | Esc goes back |

`<size>` is `1440x900` or `900x800` (the Xvfb screen; the window is resized to fill it).

## First-frame time (click on Context to the first changed frame below the header; 33 ms polling)

| Map | Size | Cold (fresh app) first frame / settled | Warm first frame / settled |
|---|---|---|---|
| over (612 nodes, by feature) | 1440x900 | 0.095 s / 0.72 s | 0.14 s / 0.55 s |
| over | 900x800 | 0.09 s / 0.62 s | 0.09 s / 0.09 s |
| under (14 nodes) | 1440x900 | 0.08 s / 0.36 s | 0.12 s / 0.95 s |
| under | 900x800 | 0.07 s / 0.33 s | 0.06 s / 0.06 s |

Software-rendered Xvfb on a shared box, one run each: a rough figure, not a benchmark.

## Finding

Entering Context from Home in a fresh app launched without the deep link (no repo yet) stayed on the empty page (header and
anchor box only) for 8 s and longer; with `sindri://context?repo=...` or on a second visit the map opens. Not a regression of this PR.
