#!/usr/bin/env bash
# tests/ui-checks/gh-1096-filter-zoom.sh — check for #1096 (half 1): filtering the Context map to a term must zoom and pan to the matches.
# Expected: after 'Pane layout' + Enter in "Ask or look up", the canvas view changes (scale or offset) and the camera is framed on the matches, not left on the whole map.
# The fixture (context-lib.sh: sindri-ui-fixture.sh up + context up) is the tabs map. 'Pane layout' hits the node 'Pane layout' (thing:pane-layout),
# and, if the search matches on part of a label, 'Restore a tab's pane layout' (action:tab-manager.restore-layout), its only neighbour.
# The world is drawn by one custom control, so the matches' rectangles are not in the element tree; what the driver can read is the ZoomBorder's View
# (scale, offset) and WorldBounds. So this asserts what those show: (1) the view moved: its scale or offset differs from the whole-map fit by more than 1 px / 0.01;
# (2) it is framed on a part: the middle of the canvas is NOT the middle of the world (a fit of the whole map keeps them equal; the matches sit in
# one corner of the tabs map's second column), and the scale did not go down. A pass of (1) alone would be a page that merely re-fitted.
# It drives the REAL sindri-ui. Red or green on main is the answer: this check only measures.
#   bash tests/ui-checks/gh-1096-filter-zoom.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init gh-1096-filter-zoom
ctx_start 1280 900
ctx_geometry
S0="$VIEW_S"; X0="$VIEW_X"; Y0="$VIEW_Y"
shot before-filter

term="Pane layout"
"$DRIVER" type --name "Ask or look up" --value "$term" --replace >/dev/null 2>&1 || setup_fail "could not type into the search box"
"$DRIVER" key --chord Enter --name "Ask or look up" >/dev/null 2>&1 || setup_fail "Enter in the search box was not accepted"

# Let the camera's ease finish: the view reads the same twice in a row (at most ~8 s).
prev=""
for _ in $(seq 1 16); do
  sleep 0.5
  ctx_geometry
  cur="$VIEW_S $VIEW_X $VIEW_Y"
  [ "$cur" = "$prev" ] && break
  prev="$cur"
done
shot after-filter

moved="$(awk -v s0="$S0" -v x0="$X0" -v y0="$Y0" -v s="$VIEW_S" -v x="$VIEW_X" -v y="$VIEW_Y" \
  'function a(v) { return v < 0 ? -v : v } BEGIN { print (a(s - s0) > 0.01 || a(x - x0) > 1 || a(y - y0) > 1) ? 1 : 0 }')"
[ "$moved" = 1 ] || fail "filtering to '$term' did not move the camera: view stayed scale $S0 offset ($X0, $Y0)"

lt "$(awk -v s0="$S0" 'BEGIN { print s0 - 0.01 }')" "$VIEW_S" || fail "filtering to '$term' zoomed OUT: scale $S0 -> $VIEW_S"

# The middle of the canvas, in world coordinates, against the middle of the world.
off="$(awk -v cw="$CANVAS_W" -v ch="$CANVAS_H" -v s="$VIEW_S" -v vx="$VIEW_X" -v vy="$VIEW_Y" -v wx="$WB_X" -v wy="$WB_Y" -v ww="$WB_W" -v wh="$WB_H" \
  'function a(v) { return v < 0 ? -v : v }
   BEGIN { mx = (cw / 2 - vx) / s; my = (ch / 2 - vy) / s; print a(mx - (wx + ww / 2)) + a(my - (wy + wh / 2)) }')"
lt 20 "$off" || fail "filtering to '$term' left the camera on the middle of the whole map (centre off by only $off world px, scale $S0 -> $VIEW_S): the matches are not framed"
echo "PASS: filtering to '$term' moved the camera (scale $S0 -> $VIEW_S) and framed a part of the map"
