#!/usr/bin/env bash
# tldraw offline (https://github.com/tldraw/tldraw-offline), the desktop whiteboard agents can drive to draw diagrams:
# installs the app when it is missing and reports whether its agent skills are in place.
#
#   macOS  brew install --cask tldraw (the cask keeps its own checksum; the app updates itself afterwards)
#   Linux  the pinned AppImage for this arch from the GitHub release, sha256-checked, at
#          $AW_TLDRAW_DIR/tldraw-offline.AppImage, linked as $AW_TLDRAW_BIN_DIR/tldraw-offline
#   other  a skip line
#
# The agent side is the app's own: Develop → Install Agent Skills writes the `tldraw-offline` skill to ~/skills and to
# each provider's skills dir (~/.claude, ~/.codex, ~/.cursor, ...), plus a SubagentStart hook and the `tq.mjs` helper that
# call the app's loopback agent API. Only the app can do that (there is no command line for it), so this script never
# writes those files; it looks for the app's install marker and, when no provider has it, prints the step to take.
# setup.sh's skill cleanup only touches links into this toolkit, so a re-run leaves the app's skills alone.
#
# Fail-open: a failed download, checksum or install warns and exits 0, so setup.sh goes on.
#
# Usage: scripts/install-tldraw.sh [--dry-run]
#   AW_DRY_RUN=1 is the same as --dry-run (setup.sh sets it).
#   AW_TLDRAW_DIR       where the Linux AppImage goes (default ~/.local/share/tldraw-offline)
#   AW_TLDRAW_BIN_DIR   where the Linux launcher link goes (default ~/.local/bin)
#   AW_TLDRAW_APP       the macOS app bundle (default "/Applications/tldraw offline.app")
#   AW_TLDRAW_URL_BASE  the release download base (default the pinned GitHub release; tests point it at a file:// dir)
#   AW_TLDRAW_SHA256    overrides this arch's pinned sha256 (tests, which serve a stand-in AppImage)
#   AW_TLDRAW_ARCH      overrides `uname -m` (tests); AW_DEPS_OS overrides `uname -s`, as in config/lib/sys-pkg.sh
set -euo pipefail

DRY="${AW_DRY_RUN:-0}"
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY=1 ;;
    *) echo "usage: install-tldraw.sh [--dry-run]" >&2; exit 1 ;;
  esac
done

# The Linux pin. The app updates itself after the first install, so this only fixes what the first download is.
VERSION="1.22.0"
SHA256_X86_64="a0daca48f5bbfbcfb85c2b2d4a0e7ad97e75710497a5ef1bb12e6de1bb2e6978"
SHA256_ARM64="07af6c84bd722423f1b4c7e775b82e616c13cc36c54ba6e8518e06de642116b1"

OS="${AW_DEPS_OS:-$(uname -s)}"
ARCH="${AW_TLDRAW_ARCH:-$(uname -m)}"
APP="${AW_TLDRAW_APP:-/Applications/tldraw offline.app}"
DIR="${AW_TLDRAW_DIR:-$HOME/.local/share/tldraw-offline}"
BIN_DIR="${AW_TLDRAW_BIN_DIR:-$HOME/.local/bin}"
URL_BASE="${AW_TLDRAW_URL_BASE:-https://github.com/tldraw/tldraw-offline/releases/download/v$VERSION}"
APPIMAGE="$DIR/tldraw-offline.AppImage"
SKILL_MARKER="installed-by:tldraw-desktop-agent-skills"

sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi
}

# Download the AppImage for this arch, check it against the pin, move it into place and link the launcher.
install_appimage() {
  local asset sum tmp
  case "$ARCH" in
    x86_64|amd64) asset="tldraw-offline-linux-x86_64.AppImage"; sum="$SHA256_X86_64" ;;
    aarch64|arm64) asset="tldraw-offline-linux-arm64.AppImage"; sum="$SHA256_ARM64" ;;
    *) echo "  tldraw: WARN: no AppImage for $ARCH (non-fatal); see https://offline.tldraw.com"; return 1 ;;
  esac
  sum="${AW_TLDRAW_SHA256:-$sum}"
  if [ "$DRY" = "1" ]; then
    echo "  [dry-run] would download $asset (v$VERSION, sha256 checked) to $APPIMAGE and link $BIN_DIR/tldraw-offline"
    return 0
  fi
  mkdir -p "$DIR" "$BIN_DIR" || { echo "  tldraw: WARN: could not create $DIR or $BIN_DIR (non-fatal)"; return 1; }
  tmp="$DIR/.download.$$"
  echo "  tldraw: downloading $asset (v$VERSION)"
  if ! curl -fsSL --retry 2 -o "$tmp" "$URL_BASE/$asset"; then
    rm -f "$tmp"; echo "  tldraw: WARN: download failed: $URL_BASE/$asset (non-fatal)"; return 1
  fi
  if [ "$(sha256_of "$tmp")" != "$sum" ]; then
    rm -f "$tmp"; echo "  tldraw: WARN: $asset does not match its pinned sha256; not installed (non-fatal)"; return 1
  fi
  chmod +x "$tmp" && mv -f "$tmp" "$APPIMAGE" && ln -sfn "$APPIMAGE" "$BIN_DIR/tldraw-offline" \
    || { rm -f "$tmp"; echo "  tldraw: WARN: could not install $APPIMAGE (non-fatal)"; return 1; }
  echo "  tldraw: installed $APPIMAGE (run: tldraw-offline; an AppImage needs FUSE 2, e.g. libfuse2)"
}

app_ok=1
case "$OS" in
  Darwin)
    if [ -d "$APP" ] || { command -v brew >/dev/null 2>&1 && brew list --cask tldraw >/dev/null 2>&1; }; then
      echo "  tldraw: app already installed"
    elif [ "$DRY" = "1" ]; then
      echo "  [dry-run] would run: brew install --cask tldraw"
    elif ! command -v brew >/dev/null 2>&1; then
      echo "  tldraw: WARN: Homebrew not found; install the app from https://offline.tldraw.com (non-fatal)"; app_ok=0
    elif brew install --cask tldraw; then
      echo "  tldraw: installed with brew install --cask tldraw"
    else
      echo "  tldraw: WARN: brew install --cask tldraw failed (non-fatal)"; app_ok=0
    fi ;;
  Linux)
    if [ -x "$APPIMAGE" ] || command -v tldraw-offline >/dev/null 2>&1; then
      echo "  tldraw: app already installed"
    else
      install_appimage || app_ok=0
    fi ;;
  *)
    echo "  tldraw: skipped on $OS (macOS and Linux only; other builds at https://offline.tldraw.com)"
    exit 0 ;;
esac
[ "$app_ok" = "1" ] || exit 0

# The agent skills: installed by the app, so only checked here.
skills=""
for d in "$HOME/.claude" "$HOME/.codex" "$HOME/.cursor"; do
  if grep -qs "$SKILL_MARKER" "$d/skills/tldraw-offline/SKILL.md"; then skills="$skills ${d##*/.}"; fi
done
if [ -n "$skills" ]; then
  echo "  tldraw: agent skills installed for:$skills"
else
  echo "  tldraw: next step: open tldraw offline and choose Develop → Install Agent Skills, then restart your agent tool"
  echo "          (agents reach the canvas only while the app is open)"
fi
exit 0
