#!/usr/bin/env bash
# Check what /system-design-video needs: ffmpeg, bun, playwright-core and its Chromium.
#
#   doctor.sh            # report; exit 0 when everything is ready, 1 otherwise
#
# The install command for a missing browser stack is printed, never run.
set -uo pipefail
ok=0
need() { command -v "$1" >/dev/null 2>&1 && echo "ok    $1" || { echo "MISSING $1: $2"; ok=1; }; }
need ffmpeg "install ffmpeg with libx264 (brew install ffmpeg)"
need ffprobe "ships with ffmpeg"
need bun "install bun (https://bun.sh)"
home=$HOME/.agentic-workflow/system-design-video
core=${PLAYWRIGHT_CORE:-$home/node_modules/playwright-core}
if [ -f "$core/package.json" ]; then
  echo "ok    playwright-core at $core"
else
  echo "MISSING playwright-core: mkdir -p $home && (cd $home && bun add playwright-core && bunx playwright-core install chromium)"
  ok=1
fi
exit $ok
