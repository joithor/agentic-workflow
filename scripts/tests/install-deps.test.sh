#!/usr/bin/env bash
# Tests for scripts/install-deps.sh: the dry run prints each new step and writes nothing, a missing system package is
# named with its exact sudo command (exit 1), and with everything present the checks pass. Runs on a PATH of symlinks to
# just the core tools plus fakes, so the host's real bwrap/uv/graphify never leak in.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
SCRIPT="$REPO/scripts/install-deps.sh"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/install-deps-test.XXXXXX")"
trap 'find "$TMPROOT" -delete' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

BIN="$TMPROOT/bin"; mkdir -p "$BIN"
for t in bash sh jq grep sed awk head cat dirname basename uname mktemp find tr cut; do
  p="$(command -v "$t")" && ln -s "$p" "$BIN/$t"
done
fake() { printf '#!/bin/sh\n%s\n' "${2:-exit 0}" >"$BIN/$1"; chmod +x "$BIN/$1"; }

HOME_DIR="$TMPROOT/home"; mkdir -p "$HOME_DIR"
run() {   # <env assignments...> -- runs the script on the sandboxed PATH
  set +e; OUT="$(env -i PATH="$BIN" HOME="$HOME_DIR" AW_DEPS_ZLIB_H="$TMPROOT/zlib.h" "$@" bash "$SCRIPT" 2>&1)"; RC=$?; set -e
}
expect() {   # <label> <rc> [<regex>]...
  local label="$1" rc="$2"; shift 2
  if [ "$RC" -ne "$rc" ]; then fail "$label: exit $RC, want $rc"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  local re
  for re in "$@"; do
    printf '%s\n' "$OUT" | grep -qE -- "$re" || { fail "$label: output lacks /$re/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; }
  done
  ok "$label"
}

PIN="$(jq -r '.sindri.graphifyPin' "$REPO/sindri/package.json")"
DATE="$(jq -r '.sindri.graphifyPinDate' "$REPO/sindri/package.json")"

echo "install-deps"

fake apt-get
# 1. Dry run on a bare Linux box: every step printed, the sudo line names every missing package, nothing is written.
run AW_DEPS_OS=Linux AW_DRY_RUN=1
expect "dry run prints the new steps" 0 \
  "would install uv" \
  "would run: uv tool install graphifyy==$PIN --exclude-newer $DATE" \
  "would run: scripts/binaries-lock.sh fetch temporal\$" \
  "would run: scripts/binaries-lock.sh fetch temporal-test-server" \
  "bwrap: MISSING" "secret-tool: MISSING" \
  "sudo apt-get install -y .*bubblewrap.*libsecret-tools.*clang.*zlib1g-dev"
[ -z "$(ls -A "$HOME_DIR")" ] && ok "dry run wrote nothing into HOME" || fail "dry run wrote: $(ls -A "$HOME_DIR")"

# 2. uv and graphify at the pin are recognised.
fake uv 'echo "uv 0.0.0"'
fake graphify "echo graphify $PIN"
run AW_DEPS_OS=Linux AW_DRY_RUN=1
expect "installed uv and graphify at the pin are recognised" 0 "uv: already installed" "graphify: already installed \\($PIN\\)"

# 3. A graphify at another version is reinstalled.
fake graphify "echo graphify 0.0.1"
run AW_DEPS_OS=Linux AW_DRY_RUN=1
expect "a graphify off the pin is reinstalled" 0 "would run: uv tool install graphifyy==$PIN"

# 4. Everything present: exit 0, no sudo line.
fake graphify "echo graphify $PIN"
for t in bwrap secret-tool clang lsof tmux curl git; do fake "$t"; done
: >"$TMPROOT/zlib.h"
run AW_DEPS_OS=Linux AW_DRY_RUN=1
expect "all system packages present" 0 "bwrap: found" "secret-tool: found"
printf '%s\n' "$OUT" | grep -q "sudo apt-get" && fail "no sudo line when nothing is missing" || ok "no sudo line when nothing is missing"

# 4b. bwrap present but its sandbox fails with the AppArmor restriction on: root fix printed, exit 1 (real run), note in dry run.
fake bwrap
echo 1 >"$TMPROOT/userns"
run AW_DEPS_OS=Linux AW_DEPS_BWRAP_PROBE='echo "setting up uid map: Permission denied" >&2; false' AW_DEPS_USERNS_FILE="$TMPROOT/userns"
expect "a bwrap that cannot sandbox under AppArmor exits 1 with the fix" 1 "cannot create a sandbox \\(setting up uid map" "profile bwrap /usr/bin/bwrap flags=\\(unconfined\\)" "sudo apparmor_parser -r /etc/apparmor.d/bwrap"
run AW_DEPS_OS=Linux AW_DRY_RUN=1 AW_DEPS_BWRAP_PROBE=false AW_DEPS_USERNS_FILE="$TMPROOT/userns"
expect "dry run shows the fix and notes a real run would stop" 0 "apparmor_parser -r" "a real run would stop here"
echo 0 >"$TMPROOT/userns"
run AW_DEPS_OS=Linux AW_DEPS_BWRAP_PROBE=false AW_DEPS_USERNS_FILE="$TMPROOT/userns"
expect "a failing bwrap with the restriction off still fails, without the AppArmor advice" 1 "cannot create a sandbox"
printf '%s\n' "$OUT" | grep -q "apparmor_parser" && fail "no AppArmor advice when the sysctl is 0" || ok "no AppArmor advice when the sysctl is 0"
run AW_DEPS_OS=Linux AW_DEPS_BWRAP_PROBE=true AW_DRY_RUN=1
expect "a working bwrap probe passes" 0 "bwrap: sandbox works"

# 5. Real run with a package missing: exit 1 and the sudo command.
rm "$BIN/bwrap"
run AW_DEPS_OS=Linux
expect "a real run with bwrap missing exits 1 and names the command" 1 "sudo apt-get install -y bubblewrap"

# 6. macOS: no Linux-only packages are required.
run AW_DEPS_OS=Darwin AW_DRY_RUN=1
expect "macOS does not ask for bwrap" 0 "tmux: found"
printf '%s\n' "$OUT" | grep -q "bwrap" && fail "macOS must not mention bwrap" || ok "macOS must not mention bwrap"

# 7. Temporal: a cached fetch is found in the cache layout sindri serve reads, and `temporal --version` is run.
STATE="$TMPROOT/state"
V="$(awk '$1=="temporal"{print $2; exit}' "$REPO/binaries.lock")"; TV="$(awk '$1=="temporal-test-server"{print $2; exit}' "$REPO/binaries.lock")"
mkdir -p "$STATE/cache/bin/temporal-$V" "$STATE/cache/bin/temporal-test-server-$TV"
printf '#!/bin/sh\necho "temporal version %s"\n' "$V" >"$STATE/cache/bin/temporal-$V/temporal"
: >"$STATE/cache/bin/temporal-test-server-$TV/temporal-test-server"
chmod +x "$STATE"/cache/bin/*/*
fake bwrap
run AW_DEPS_OS=Linux AW_STATE_DIR="$STATE"
expect "temporal is found at cache/bin/temporal-<pin>/ and runs" 0 "temporal: .*/cache/bin/temporal-$V/temporal \\(temporal version $V\\)" "temporal-test-server: .*temporal-test-server-$TV"

# 8. A temporal that won't run is reported and fails the run.
printf '#!/bin/sh\necho broken >&2; exit 3\n' >"$STATE/cache/bin/temporal-$V/temporal"
run AW_DEPS_OS=Linux AW_STATE_DIR="$STATE"
expect "a temporal that won't run fails clearly" 1 "WARN: .*temporal does not run: broken"

echo ""
echo "$PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
