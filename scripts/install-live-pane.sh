#!/usr/bin/env bash
# Install the aw-live Claude Code mod: a /live pane that shows the
# scorer's numbers for the current session (context, cost, judge, gates, wakes).
#
# Claude Code only. Mods are in-process function hooks of Claude Code; Codex and Cursor have
# no equivalent, so they keep the statusline segments (judge, rtk, headroom, prism) and the
# daily scorer report, and this installer says so and writes nothing for them.
#
# Owner tag: the marketplace is named "agentic-workflow-mods" and the plugin "aw-live"; this
# script only ever touches those two ids, and --uninstall removes exactly them.
#
# Usage: scripts/install-live-pane.sh [--provider claude|codex|cursor] [--dry-run] [--uninstall]
#   AW_DRY_RUN=1 is the same as --dry-run (setup.sh sets it).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$ROOT/config/hooks/adapters/install-lib.sh"
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}

MARKETPLACE="agentic-workflow-mods"
PLUGIN="aw-live"
PLUGIN_ID="$PLUGIN@$MARKETPLACE"
MODS_DIR="$ROOT/mods"
DRY="${AW_DRY_RUN:-0}"
UNINSTALL=0
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY=1 ;;
    --uninstall) UNINSTALL=1 ;;
    *) echo "usage: install-live-pane.sh [--provider claude|codex|cursor] [--dry-run] [--uninstall]" >&2; exit 1 ;;
  esac
done

if [ "$AW_PROVIDER" != "claude" ]; then
  aw_hooks_init "$AW_PROVIDER"
  aw_unsupported live-pane "mods are Claude Code only; $AW_PROVIDER keeps the statusline segments and the daily scorer report"
  exit 0
fi

# Run a state-changing command, or print it under --dry-run.
run() {
  if [ "$DRY" = "1" ]; then
    printf '  [dry-run]'
    printf ' %q' "$@"
    printf '\n'
  else
    "$@"
  fi
}

# This lever is optional: a failed claude call must never abort setup.sh for every provider.
soft_fail() { echo "  WARN: live-pane: claude $1 failed (non-fatal); live pane not installed"; exit 0; }

if ! command -v claude >/dev/null 2>&1; then
  echo "  live-pane: skipped, the claude CLI is not on PATH"
  exit 0
fi

# The marketplace/plugin state is read with jq; without it nothing below can be decided safely.
if ! command -v jq >/dev/null 2>&1; then
  echo "  live-pane: skipped, jq is not on PATH (install jq, then re-run scripts/install-live-pane.sh)"
  exit 0
fi

# Field names observed from claude 2.1.289: `plugin list --json` rows have `.id`;
# `plugin marketplace list --json` rows have `.name` and `.installLocation`.
marketplaces="$(claude plugin marketplace list --json 2>/dev/null || echo '[]')"
plugins="$(claude plugin list --json 2>/dev/null || echo '[]')"
has_plugin() { printf '%s' "$plugins" | jq -e --arg id "$PLUGIN_ID" 'any(.[]?; .id == $id)' >/dev/null 2>&1 || return 1; }
# Non-JSON output (e.g. "No marketplaces configured") reads as "none"; never a non-zero exit.
marketplace_location() {
  printf '%s' "$marketplaces" | jq -r --arg n "$MARKETPLACE" '[.[]? | select(.name == $n) | (.installLocation // .path // "")] | first // empty' 2>/dev/null || true
}

if [ "$UNINSTALL" = "1" ]; then
  if has_plugin; then
    run claude plugin uninstall "$PLUGIN_ID" --scope user || soft_fail "plugin uninstall"
    echo "  live-pane: $PLUGIN_ID uninstalled"
  else
    echo "  live-pane: $PLUGIN_ID was not installed"
  fi
  if [ -n "$(marketplace_location)" ]; then
    run claude plugin marketplace remove "$MARKETPLACE" || soft_fail "marketplace remove"
    echo "  live-pane: marketplace $MARKETPLACE removed"
  fi
  exit 0
fi

if [ ! -f "$MODS_DIR/aw-live/.claude-plugin/plugin.json" ]; then
  echo "  live-pane: skipped, $MODS_DIR/aw-live is missing"
  exit 0
fi

location="$(marketplace_location)"
if [ "$location" = "$MODS_DIR" ]; then
  echo "  live-pane: marketplace $MARKETPLACE up to date"
else
  # Absent, or registered from another checkout: (re)register this one.
  if [ -n "$location" ]; then
    run claude plugin marketplace remove "$MARKETPLACE" || soft_fail "marketplace remove"
    plugins='[]' # removing a marketplace takes its plugins with it
  fi
  run claude plugin marketplace add "$MODS_DIR" --scope user || soft_fail "marketplace add"
  echo "  live-pane: marketplace $MARKETPLACE -> $MODS_DIR"
fi

if has_plugin; then
  echo "  live-pane: $PLUGIN_ID already installed (read from $MODS_DIR/aw-live; /reload-plugins picks up edits)"
else
  run claude plugin install "$PLUGIN_ID" --scope user || soft_fail "plugin install"
  if [ "$DRY" = "1" ]; then echo "  live-pane: $PLUGIN_ID would be installed"; else echo "  live-pane: $PLUGIN_ID installed"; fi
fi

# The mod shells out to `scorer live`; a scorer built before that command exists cannot feed it.
BIN_DIR="${CLAUDE_LOCAL_BIN:-$HOME/.local/bin}"
if [ "$DRY" != "1" ] && ! "$BIN_DIR/scorer" live --session aw-install-check --json 2>/dev/null | jq -e '.v == 1' >/dev/null 2>&1; then
  echo "  WARN: $BIN_DIR/scorer has no 'live' command yet; run scripts/install-scorer.sh so the pane has numbers"
fi
echo "  live-pane: open a session and type /live (pane) or /live status (text)"
