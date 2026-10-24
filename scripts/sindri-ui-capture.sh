#!/usr/bin/env bash
# scripts/sindri-ui-capture.sh — runs the REAL sindri-ui in capture mode (desktop UI evidence): publishes it when the source changed
# (scripts/sindri-app.sh --publish-only), then launches it with the arguments given, which open its window at a fixed size, go to
# each screen through the app's own navigation, save the window as PNGs and exit:
#
#   sindri-ui-capture.sh --size WxH --shot <route> <png path> [--shot <route> <png path> ...]
#
# AW_SINDRI_HOME must point at the control plane to show (desktop-run sets it to its seeded fixture home). The display is the user's
# (DISPLAY set) or a virtual X server (xvfb-run); which one is said on stderr as a line 'display: ...', and desktop-run records it in
# the pack's notes. Exit: sindri-ui's (0 every frame written, 4 a screen could not be captured), 1 usage, 3 no display available.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
[ "$#" -ge 5 ] || { echo "usage: sindri-ui-capture.sh --size WxH --shot <route> <png path> [--shot <route> <png path> ...]" >&2; exit 1; }
[ -n "${AW_SINDRI_HOME:-}" ] || { echo "sindri-ui-capture: AW_SINDRI_HOME is not set" >&2; exit 1; }

"$root/scripts/sindri-app.sh" --publish-only >&2 || { echo "sindri-ui-capture: publishing sindri-ui failed" >&2; exit 1; }

state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
ui=""
for dir in $(ls -td "$state"/bin/sindri-app/*/ 2>/dev/null); do
  if [ -x "${dir}sindri-ui" ]; then ui="${dir}sindri-ui"; break; fi
done
[ -n "$ui" ] || { echo "sindri-ui-capture: no published sindri-ui under $state/bin/sindri-app" >&2; exit 1; }

if [ -n "${DISPLAY:-}" ]; then
  echo "display: DISPLAY=$DISPLAY (the user's)" >&2
  exec "$ui" --capture "$@"
fi
if command -v xvfb-run >/dev/null 2>&1; then
  echo "display: xvfb-run (virtual X server 1920x1200x24)" >&2
  exec xvfb-run -a -s "-screen 0 1920x1200x24" "$ui" --capture "$@"
fi
echo "sindri-ui-capture: no DISPLAY and no xvfb-run (install xvfb)" >&2
exit 3
