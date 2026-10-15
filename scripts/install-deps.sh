#!/usr/bin/env bash
# scripts/install-deps.sh — the external tools the toolkit shells out to, installed or checked once per box.
# Called by setup.sh; also runnable alone. Honors AW_DRY_RUN=1 (prints what it would do, writes nothing).
#
#   user-level, installed here (no root):
#     uv           Linux: astral.sh installer into ~/.local/bin; macOS: brew
#     graphify     the code index's feature-cluster tool, `uv tool install graphifyy==<pin> --exclude-newer <date>`;
#                  the pin is sindri/package.json's "sindri.graphifyPin" (what `sindri index setup` installs)
#     temporal, temporal-test-server   scripts/binaries-lock.sh fetch <name> (sha256-verified, idempotent) into
#                  ${AW_STATE_DIR:-~/.agentic-workflow}/cache/bin/<name>-<pin>/, where sindri serve and the tests look;
#                  `temporal --version` is run afterwards and a failed fetch or a binary that won't run fails the script
#   bwrap is also probed with a no-op sandbox (`bwrap --ro-bind / / true`); when that fails because AppArmor restricts
#   user namespaces (Ubuntu 24.04), the exact /etc/apparmor.d/bwrap profile and `sudo apparmor_parser -r` are printed and the run exits 1.
#   system packages, checked only (installing needs root, so the exact command is printed and this exits 1):
#     Linux: bubblewrap (bwrap: graphify never runs unsandboxed), libsecret-tools (secret-tool: Sindri keychain:
#            pointers), clang + zlib1g-dev (NativeAOT), lsof, tmux, curl, git
#     macOS: tmux (brew)
#
# Exit: 0 everything present or installed, 1 a system package is missing (or a user-level install failed).
# Bash 3.2 compatible (macOS /bin/bash).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dry() { [ "${AW_DRY_RUN:-0}" = "1" ]; }
OS="${AW_DEPS_OS:-$(uname -s)}"
FAILED=0

case ":$PATH:" in *":$HOME/.local/bin:"*) ;; *) export PATH="$HOME/.local/bin:$PATH" ;; esac

echo "=== External tools (uv, graphify, Temporal, system packages) ==="

# --- system packages: check, never sudo ---
MISSING_PKGS=""
BWRAP_BROKEN=0
BWRAP_RESTRICTED=""
need() {   # <command> <apt package> [<what needs it>]
  if command -v "$1" &>/dev/null; then echo "  $1: found"; else
    echo "  $1: MISSING${3:+ ($3)}"; MISSING_PKGS="$MISSING_PKGS $2"
  fi
}
if [ "$OS" = "Linux" ]; then
  need bwrap bubblewrap "network sandbox for graphify; it never runs unsandboxed"
  # Present is not enough: Ubuntu 24.04 (kernel.apparmor_restrict_unprivileged_userns=1, no profile for bwrap) refuses
  # the user namespace, so graphify could never run. Probe a no-op sandbox; AW_DEPS_BWRAP_PROBE replaces the command (tests).
  if command -v bwrap &>/dev/null; then
    if _why="$(eval "${AW_DEPS_BWRAP_PROBE:-bwrap --ro-bind / / true}" 2>&1)"; then echo "  bwrap: sandbox works"; else
      echo "  bwrap: found but cannot create a sandbox (${_why:-no output})"; BWRAP_BROKEN=1
      _restrict=""; read -r _restrict <"${AW_DEPS_USERNS_FILE:-/proc/sys/kernel/apparmor_restrict_unprivileged_userns}" 2>/dev/null || true
      BWRAP_RESTRICTED="$_restrict"
    fi
  fi
  need secret-tool libsecret-tools "Sindri keychain: pointers"
  need clang clang "NativeAOT publish"
  if [ -e "${AW_DEPS_ZLIB_H:-/usr/include/zlib.h}" ]; then echo "  zlib.h: found"; else
    echo "  zlib.h: MISSING (zlib1g-dev, NativeAOT publish)"; MISSING_PKGS="$MISSING_PKGS zlib1g-dev"
  fi
  need lsof lsof "port and lock probes"
  need tmux tmux "Sindri sessions"
  need curl curl "downloads"
  need git git
elif [ "$OS" = "Darwin" ]; then
  if command -v tmux &>/dev/null; then echo "  tmux: found"; else echo "  tmux: MISSING (brew install tmux)"; FAILED=1; fi
fi

# --- uv ---
if command -v uv &>/dev/null; then
  echo "  uv: already installed ($(uv --version 2>/dev/null || echo 'unknown version'))"
elif dry; then
  echo "  [dry-run] would install uv (brew on macOS, astral.sh/uv/install.sh elsewhere)"
else
  echo "  Installing uv..."
  if [ "$OS" = "Darwin" ] && command -v brew &>/dev/null; then brew install uv || true
  elif command -v curl &>/dev/null; then curl -LsSf https://astral.sh/uv/install.sh | sh || true; fi
  if command -v uv &>/dev/null; then echo "  uv: installed"; else
    echo "  WARN: uv install failed; install it (https://docs.astral.sh/uv/getting-started/installation/) and rerun"; FAILED=1
  fi
fi

# --- graphify at the pin sindri/package.json declares ---
PKG="$ROOT/sindri/package.json"
PIN="$(jq -r '.sindri.graphifyPin // empty' "$PKG" 2>/dev/null)"
PIN_DATE="$(jq -r '.sindri.graphifyPinDate // empty' "$PKG" 2>/dev/null)"
if [ -z "$PIN" ] || [ -z "$PIN_DATE" ]; then
  echo "  WARN: sindri/package.json has no sindri.graphifyPin; skipping graphify"; FAILED=1
elif command -v graphify &>/dev/null && [ "$(graphify --version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)" = "$PIN" ]; then
  echo "  graphify: already installed ($PIN)"
elif dry; then
  echo "  [dry-run] would run: uv tool install graphifyy==$PIN --exclude-newer $PIN_DATE"
elif command -v uv &>/dev/null; then
  echo "  Installing graphifyy==$PIN (dependencies age-gated to $PIN_DATE)..."
  if uv tool install "graphifyy==$PIN" --exclude-newer "$PIN_DATE"; then echo "  graphify: installed ($PIN)"; else
    echo "  WARN: graphify install failed; run: uv tool install graphifyy==$PIN --exclude-newer $PIN_DATE"; FAILED=1
  fi
else
  echo "  WARN: graphify not installed (uv missing)"; FAILED=1
fi

# --- Temporal CLI + time-skipping test server (binaries.lock) ---
for _bin in temporal temporal-test-server; do
  if dry; then
    echo "  [dry-run] would run: scripts/binaries-lock.sh fetch $_bin"
  elif _path="$(bash "$ROOT/scripts/binaries-lock.sh" fetch "$_bin")"; then
    # sindri serve and the tests look for the CLI at <AW_STATE_DIR>/cache/bin/temporal-<pin>/temporal (the path fetch printed)
    if [ "$_bin" = "temporal" ]; then
      if _v="$("$_path" --version 2>&1)"; then echo "  $_bin: $_path ($_v)"; else
        echo "  WARN: $_path does not run: $_v"; FAILED=1
      fi
    else
      echo "  $_bin: $_path"
    fi
  else
    echo "  WARN: scripts/binaries-lock.sh fetch $_bin failed"; FAILED=1
  fi
done

# --- missing system packages: the exact command, then fail ---
if [ -n "$MISSING_PKGS" ]; then
  echo ""
  echo "  Missing system packages; root is needed, so this script does not install them. Run:"
  if command -v apt-get &>/dev/null; then echo "    sudo apt-get install -y$MISSING_PKGS"
  else echo "    (install with your package manager:$MISSING_PKGS; Debian/Ubuntu package names)"; fi
  if dry; then echo "  [dry-run] a real run would stop here"; else FAILED=1; fi
fi

# --- bwrap present but unusable: the exact root fix, then fail ---
if [ "$BWRAP_BROKEN" = "1" ]; then
  echo ""
  if [ "$BWRAP_RESTRICTED" = "1" ]; then
    echo "  AppArmor blocks unprivileged user namespaces (kernel.apparmor_restrict_unprivileged_userns=1) and has no profile"
    echo "  for /usr/bin/bwrap, so graphify's sandbox cannot start. Root is needed, so this script does not apply it. Run:"
    cat <<'FIX'
    sudo tee /etc/apparmor.d/bwrap >/dev/null <<'PROFILE'
abi <abi/4.0>,
include <tunables/global>
profile bwrap /usr/bin/bwrap flags=(unconfined) {
  userns,
  include if exists <local/bwrap>
}
PROFILE
    sudo apparmor_parser -r /etc/apparmor.d/bwrap
FIX
  else
    echo "  bwrap cannot create a sandbox and the AppArmor userns restriction is not the cause; run \`bwrap --ro-bind / / true\` and read its error."
  fi
  if dry; then echo "  [dry-run] a real run would stop here"; else FAILED=1; fi
fi

[ "$FAILED" = "0" ]
