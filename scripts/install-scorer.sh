#!/usr/bin/env bash
# Build the scorer, install the ~/.local/bin/scorer CLI wrapper, and (on Darwin)
# install and load the daily launchd report job.
#
# This is split out of setup.sh so it can be run on its own without pulling in
# the rest of setup.sh's install steps.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SCORER_DIR="$SCRIPT_DIR/scorer"

echo ""
echo "Installing scorer..."

if [ -f "$SCORER_DIR/package.json" ]; then
  (cd "$SCORER_DIR" && npm install && npm run build)
  BIN_DIR="${CLAUDE_LOCAL_BIN:-$HOME/.local/bin}"
  mkdir -p "$BIN_DIR"
  cat > "$BIN_DIR/scorer" <<EOF
#!/usr/bin/env bash
exec node "$SCORER_DIR/dist/cli.js" "\$@"
EOF
  chmod +x "$BIN_DIR/scorer"
  echo "  scorer: built, CLI at $BIN_DIR/scorer"
  case ":$PATH:" in *":$BIN_DIR:"*) ;; *) echo "  WARN: $BIN_DIR is not on PATH" ;; esac

  if [ "$(uname)" = "Darwin" ] && [ "${AW_SKIP_LAUNCHD:-}" != "1" ]; then
    LAUNCH_AGENTS_DIR="${AW_LAUNCH_AGENTS_DIR:-$HOME/Library/LaunchAgents}"
    mkdir -p "$LAUNCH_AGENTS_DIR" "${AW_STATE_DIR:-$HOME/.agentic-workflow}/scorer"
    for PLIST_NAME in com.agentic-workflow.scorer.plist com.agentic-workflow.scorer-audit.plist; do
      PLIST_SRC="$SCRIPT_DIR/config/launchd/$PLIST_NAME"
      PLIST_DST="$LAUNCH_AGENTS_DIR/$PLIST_NAME"
      sed "s|__HOME__|$HOME|g" "$PLIST_SRC" > "$PLIST_DST"
      launchctl bootout "gui/$(id -u)" "$PLIST_DST" 2>/dev/null || true
      launchctl bootstrap "gui/$(id -u)" "$PLIST_DST"
    done
    echo "  scorer: daily report at 08:30 and weekly audit Mondays 08:45 (launchd)"
  elif [ "${AW_SKIP_LAUNCHD:-}" = "1" ]; then
    echo "  scorer: AW_SKIP_LAUNCHD=1, skipping launchd registration (sandbox/test mode)"
  else
    echo "  scorer: add a daily cron entry yourself: 30 8 * * * $BIN_DIR/scorer"
    echo "  scorer: add a weekly cron entry yourself: 45 8 * * 1 $BIN_DIR/scorer audit --since 7d --label 0 --out $HOME/.agentic-workflow/audit/weekly/\$(date +%F)"
  fi
else
  echo "  scorer: package.json not found, skipping"
fi
