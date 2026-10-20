#!/usr/bin/env bash
# headroom, as a proxy wrapper only (never an MCP server): installs the `headroom` CLI when it is missing and puts
# the `aw-headroom` launcher on PATH. `aw-headroom claude|codex` then runs the tool through a loopback headroom
# proxy chained in front of the upstream the tool already uses (an API gateway such as EasyCLIProxyAPI, or the
# official API); see scripts/aw-headroom.sh for the detection order and the lifecycle.
#
# Nothing in the provider's settings is edited, and no MCP server is registered: headroom stays in setup.sh's
# AW_MCP_RETIRED, so a leftover headroom MCP registration is still removed.
#
# Cursor gets no wrapper (it is not launched through a CLI the wrapper can start behind a loopback proxy), so it gets a
# one-line skip and nothing is written.
#
# Fail-open: a missing Python, a failed package install or an unwritable bin dir warns and exits 0, so setup.sh goes on.
#
# Usage: scripts/install-headroom.sh [--provider claude|codex|cursor] [--dry-run] [--uninstall]
#   AW_DRY_RUN=1 is the same as --dry-run (setup.sh sets it).
#   AW_HEADROOM_BIN_DIR  where the launcher goes (default ~/.local/bin)
#   AW_HEADROOM_PACKAGE  the pip requirement (default 'headroom-ai[proxy]')
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$ROOT/config/hooks/adapters/install-lib.sh"
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}

DRY="${AW_DRY_RUN:-0}"
UNINSTALL=0
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY=1 ;;
    --uninstall) UNINSTALL=1 ;;
    *) echo "usage: install-headroom.sh [--provider claude|codex|cursor] [--dry-run] [--uninstall]" >&2; exit 1 ;;
  esac
done

BIN_DIR="${AW_HEADROOM_BIN_DIR:-$HOME/.local/bin}"
LAUNCHER="$BIN_DIR/aw-headroom"
PACKAGE="${AW_HEADROOM_PACKAGE:-headroom-ai[proxy]}"

if [ "$AW_PROVIDER" = "cursor" ]; then
  aw_hooks_init cursor
  aw_unsupported headroom "Cursor is not launched through a CLI this wrapper can start behind a proxy"
  exit 0
fi

if [ "$UNINSTALL" = "1" ]; then
  if [ "$DRY" = "1" ]; then echo "  [dry-run] would stop the headroom proxies and remove $LAUNCHER"; exit 0; fi
  [ -x "$LAUNCHER" ] && "$LAUNCHER" stop >/dev/null 2>&1 || true
  rm -f "$LAUNCHER"
  echo "  headroom: removed $LAUNCHER (the headroom package is left in place)"
  exit 0
fi

# The newest Python >= 3.10 on PATH (headroom-ai needs it); empty when none.
find_python() {
  local best="" best_minor=-1 py minor
  for py in python3.14 python3.13 python3.12 python3.11 python3.10 python3 python; do
    command -v "$py" >/dev/null 2>&1 || continue
    minor="$("$py" -c 'import sys; print(sys.version_info.minor if sys.version_info.major == 3 else -1)' 2>/dev/null || true)"
    case "$minor" in ''|*[!0-9-]*) continue ;; esac
    if [ "$minor" -ge 10 ] && [ "$minor" -gt "$best_minor" ]; then best="$py"; best_minor="$minor"; fi
  done
  printf '%s' "$best"
}

have_headroom() { command -v headroom >/dev/null 2>&1 || [ -x "$HOME/.local/bin/headroom" ]; }

# Install the package: uv tool, then pipx, then pip --user. Returns non-zero when none worked.
install_package() {
  local py
  if command -v uv >/dev/null 2>&1; then
    echo "  headroom: installing $PACKAGE with uv tool"
    uv tool install "$PACKAGE" && return 0
  fi
  if command -v pipx >/dev/null 2>&1; then
    echo "  headroom: installing $PACKAGE with pipx"
    pipx install "$PACKAGE" && return 0
  fi
  py="$(find_python)"
  [ -n "$py" ] || { echo "  headroom: WARN: Python 3.10+ not found" >&2; return 1; }
  echo "  headroom: installing $PACKAGE with $py -m pip --user"
  "$py" -m pip install --user --break-system-packages "$PACKAGE" 2>/dev/null \
    || "$py" -m pip install --user "$PACKAGE"
}

if have_headroom; then
  echo "  headroom: CLI already installed ($(headroom --version 2>/dev/null || "$HOME/.local/bin/headroom" --version 2>/dev/null || echo 'unknown version'))"
elif [ "$DRY" = "1" ]; then
  echo "  [dry-run] would install $PACKAGE (uv tool, else pipx, else pip --user)"
else
  if ! install_package; then
    echo "  headroom: WARN: could not install $PACKAGE (non-fatal); the aw-headroom launcher will run the tool unwrapped until it is installed"
  fi
fi

if [ "$DRY" = "1" ]; then
  echo "  [dry-run] would install $ROOT/scripts/aw-headroom.sh -> $LAUNCHER ($AW_PROVIDER: aw-headroom $AW_PROVIDER; proxy only, no MCP server registered)"
  exit 0
fi
if mkdir -p "$BIN_DIR" && cp "$ROOT/scripts/aw-headroom.sh" "$LAUNCHER" && chmod +x "$LAUNCHER"; then
  echo "  headroom: launcher at $LAUNCHER (run: aw-headroom $AW_PROVIDER)"
else
  echo "  headroom: WARN: could not write $LAUNCHER (non-fatal)" >&2
fi
exit 0
