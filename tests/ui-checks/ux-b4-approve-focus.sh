#!/usr/bin/env bash
# tests/ui-checks/ux-b4-approve-focus.sh — frozen regression check for UX-pass finding B-4 (ui-ux-pass-2026-10-25).
# Approving the changed settings must leave keyboard focus somewhere: the Approve settings button is removed with focus in it,
# and every shortcut goes dead. Expect, within 2 s of profile.approved by human:ui, that the driver finds a focused element.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh). Takes well under a minute.
#   bash tests/ui-checks/ux-b4-approve-focus.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b4-approve-focus-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b4-approve-focus-driver-XXXXXX")" || exit 2
OUT="${UXCHECK_OUT:-$(mktemp -d "$TMP_ROOT/ux-b4-approve-focus-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b4-approve-focus: setup failed: $*" >&2; exit 2; }
fail() { echo "FAIL: $*" >&2; exit 1; }

cleanup() {
  local app_pid=""
  if [ -f "$AW_UI_DRIVER_DIR/driver.json" ]; then
    app_pid="$(sed -n 's/.*"pid":\([0-9][0-9]*\).*/\1/p' "$AW_UI_DRIVER_DIR/driver.json")"
  fi
  if [ -n "$app_pid" ]; then kill "$app_pid" 2>/dev/null || true; fi
  if [ -n "$APP_WRAPPER_PID" ]; then kill "$APP_WRAPPER_PID" 2>/dev/null || true; fi
  bash "$FIXTURE" down >/dev/null 2>&1 || true
}
trap cleanup EXIT

command -v jq >/dev/null 2>&1 || setup_fail "jq is needed"

# 1. The control plane with an approved profile, then the profile changes so Settings asks for the approval.
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" drift >/dev/null 2>&1 || setup_fail "sindri-ui-fixture drift"

# 2. The real app, drivable, on the fixture's control plane: this checkout's debug build (sindri-app.sh's publish would
#    make the build the owner's current one), unless AW_SINDRI_UI names a binary.
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
if [ -n "${DISPLAY:-}" ]; then
  bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
else
  xvfb-run -a bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
fi
APP_WRAPPER_PID=$!
"$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

approve=(--type Button --name "Approve settings")
open_settings() {
  "$DRIVER" key --chord Ctrl+OemComma >/dev/null 2>&1 || true
}


# 3. Settings shows the approval; click Approve settings (one click, no phrase) and wait for the ledger.
open_settings
"$DRIVER" wait-for "${approve[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "no Approve settings button in Settings (see $OUT/app.log)"
"$DRIVER" click "${approve[@]}" >/dev/null 2>&1 || setup_fail "the Approve settings click was not accepted by the driver"
approved=0
for _ in $(seq 1 20); do
  ledger="$("$PROVE" human ledger --json --max 1000 2>&1 | tr -d "\r")"
  if printf "%s" "$ledger" | grep -q "profile.approved" && printf "%s" "$ledger" | grep -q "human:ui"; then approved=1; break; fi
  sleep 0.5
done
[ "$approved" = 1 ] || setup_fail "no profile.approved by human:ui in the ledger"

# 4. The driver has no focus query: a key with no selector goes to the focused element and fails with AW-UIDRIVER-002
#    "nothing has keyboard focus" when there is none. LeftShift alone changes nothing.
focused=0
for _ in $(seq 1 4); do
  if "$DRIVER" key --chord LeftShift >/dev/null 2>"$OUT/focus.err"; then focused=1; break; fi
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-approve.png" >/dev/null 2>&1
[ "$focused" = 1 ] || fail "nothing has focus after approve (screenshot in $OUT; $(tr "\n" " " <"$OUT/focus.err"))"

echo "PASS: focus is on an element after Approve settings (screenshots in $OUT)"
