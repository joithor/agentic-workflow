#!/usr/bin/env bash
# Build sindri and install the ~/.local/bin/sindri CLI wrapper (matches install-judge.sh).
#
#   install-sindri.sh                 npm ci + build, then write the wrapper
#   AW_DRY_RUN=1 install-sindri.sh    print what would happen, write nothing
#   AW_SKIP_BUILD=1 install-sindri.sh write the wrapper only (tests; dist/ already built)
#   AW_SKIP_LAUNCHD=1 install-sindri.sh skip the hourly observe launchd job (macOS; tests)
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
  echo "  [dry-run] would install launchd job com.agentic-workflow.sindri-observe.plist (macOS)"
  exit 0
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
# Hourly `sindri observe` keeps the ledger's plan-task state current (macOS only;
# AW_SKIP_LAUNCHD=1 skips it, which the tests use).
if [ "$(uname -s)" = "Darwin" ] && [ "${AW_SKIP_LAUNCHD:-0}" != "1" ]; then
  LAUNCH_AGENTS_DIR="$HOME/Library/LaunchAgents"
  NAME=com.agentic-workflow.sindri-observe
  mkdir -p "$LAUNCH_AGENTS_DIR" "${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri"
  sed -e "s|__HOME__|$HOME|g" -e "s|__BIN__|$BIN_DIR|g" "$SCRIPT_DIR/config/launchd/$NAME.plist" > "$LAUNCH_AGENTS_DIR/$NAME.plist"
  launchctl bootout "gui/$(id -u)" "$LAUNCH_AGENTS_DIR/$NAME.plist" 2>/dev/null || true
  launchctl bootstrap "gui/$(id -u)" "$LAUNCH_AGENTS_DIR/$NAME.plist"
  echo "  sindri: hourly observe (launchd $NAME)"
fi
case ":$PATH:" in *":$BIN_DIR:"*) ;; *) echo "  WARN: $BIN_DIR is not on PATH" ;; esac
