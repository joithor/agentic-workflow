#!/usr/bin/env bash
# Build sindri and install the ~/.local/bin/sindri CLI wrapper (matches install-judge.sh).
#
#   install-sindri.sh                 npm ci + build, then write the wrapper
#   AW_DRY_RUN=1 install-sindri.sh    print what would happen, write nothing
#   AW_SKIP_BUILD=1 install-sindri.sh write the wrapper only (tests; dist/ already built)
#   AW_SKIP_LAUNCHD=1 install-sindri.sh skip the observe and index launchd jobs (macOS; tests)
#   CLAUDE_LOCAL_BIN=DIR             where the wrapper goes (default ~/.local/bin)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SINDRI_DIR="$SCRIPT_DIR/sindri"
BIN_DIR="${CLAUDE_LOCAL_BIN:-$HOME/.local/bin}"

echo ""
echo "Installing sindri..."

if [ "${AW_DRY_RUN:-0}" = "1" ]; then
  echo "  [dry-run] would run npm ci && npm run build in $SINDRI_DIR"
  echo "  [dry-run] would write $BIN_DIR/sindri"
  for PLIST_FILE in com.agentic-workflow.sindri-observe.plist com.agentic-workflow.sindri-index-quick.plist com.agentic-workflow.sindri-index.plist; do
    echo "  [dry-run] would install launchd job $PLIST_FILE (macOS)"
  done
  exit 0
fi

USE_LAUNCHD=0
if [ "$(uname -s)" = "Darwin" ] && [ "${AW_SKIP_LAUNCHD:-0}" != "1" ]; then USE_LAUNCHD=1; fi
if [ "$USE_LAUNCHD" = "1" ]; then
  # The substituted values land in a sed script and an XML plist; refuse characters that break either.
  case "$HOME$BIN_DIR" in
    *[\|\&\\\<\>]*) echo "ERROR: \$HOME and the bin dir must not contain | & \\ < > (got HOME=$HOME, bin=$BIN_DIR)" >&2; exit 2 ;;
  esac
fi

if [ "${AW_SKIP_BUILD:-0}" != "1" ]; then
  (cd "$SINDRI_DIR" && npm ci && npm run build)
fi
# Absolute node path: launchd and GUI git clients don't load shell profiles (nvm, Homebrew).
NODE_BIN="$(command -v node)"
mkdir -p "$BIN_DIR"
cat > "$BIN_DIR/sindri" <<EOF
#!/usr/bin/env bash
# SINDRI_BIN lets \`sindri scrub --install-pre-commit\` write this absolute path into the hook.
export SINDRI_BIN="$BIN_DIR/sindri"
exec "$NODE_BIN" "$SINDRI_DIR/dist/cli.js" "\$@"
EOF
chmod +x "$BIN_DIR/sindri"
echo "  sindri: CLI at $BIN_DIR/sindri"
# launchd jobs (macOS only; AW_SKIP_LAUNCHD=1 skips them, which the tests use): the hourly observe
# keeps the ledger's plan-task state current, the hourly quick index build keeps structure, clones and
# deps fresh, and the nightly full build refreshes everything. Each plist sets PATH, because launchd's
# default has neither ~/.local/bin (uv tools) nor Homebrew.
if [ "$USE_LAUNCHD" = "1" ]; then
  LAUNCH_AGENTS_DIR="$HOME/Library/LaunchAgents"
  # launchd does not pass AW_STATE_DIR, so the jobs and their logs use the default state dir.
  SINDRI_STATE="$HOME/.agentic-workflow/sindri"
  mkdir -p "$LAUNCH_AGENTS_DIR"
  mkdir -p -m 700 "$SINDRI_STATE"
  chmod 700 "$SINDRI_STATE"
  for JOB in "com.agentic-workflow.sindri-observe|hourly observe" \
             "com.agentic-workflow.sindri-index-quick|hourly quick index build" \
             "com.agentic-workflow.sindri-index|nightly full index build at 03:15"; do
    NAME="${JOB%%|*}"
    WHAT="${JOB#*|}"
    PLIST="$LAUNCH_AGENTS_DIR/$NAME.plist"
    sed -e "s|__HOME__|$HOME|g" -e "s|__BIN__|$BIN_DIR|g" "$SCRIPT_DIR/config/launchd/$NAME.plist" > "$PLIST"
    launchctl bootout "gui/$(id -u)/$NAME" 2>/dev/null || true
    if launchctl bootstrap "gui/$(id -u)" "$PLIST" 2>/dev/null; then
      LOADED=1
    else
      sleep 1
      if launchctl bootstrap "gui/$(id -u)" "$PLIST"; then
        LOADED=1
      else
        LOADED=0
        echo "  WARN: could not load $NAME; run: launchctl bootstrap gui/$(id -u) $PLIST"
      fi
    fi
    if [ "$LOADED" = "1" ]; then echo "  sindri: $WHAT (launchd $NAME)"; fi
  done
fi
case ":$PATH:" in *":$BIN_DIR:"*) ;; *) echo "  WARN: $BIN_DIR is not on PATH" ;; esac
