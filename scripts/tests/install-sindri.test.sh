#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$DIR/../.."
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

test_dry_run_writes_nothing() {
  local out
  out="$(AW_DRY_RUN=1 CLAUDE_LOCAL_BIN="$TMP/bin" bash "$ROOT/scripts/install-sindri.sh")"
  grep -q "\[dry-run\] would write $TMP/bin/sindri" <<<"$out" || { echo "FAIL: dry-run line missing"; exit 1; }
  [ ! -e "$TMP/bin/sindri" ] || { echo "FAIL: dry-run wrote the wrapper"; exit 1; }
  echo "PASS: test_dry_run_writes_nothing"
}

test_wrapper_execs_the_built_cli() {
  AW_SKIP_BUILD=1 AW_SKIP_LAUNCHD=1 CLAUDE_LOCAL_BIN="$TMP/bin" bash "$ROOT/scripts/install-sindri.sh" > /dev/null
  [ -x "$TMP/bin/sindri" ] || { echo "FAIL: wrapper not executable"; exit 1; }
  grep -q 'exec "/.*node" ".*/sindri/dist/cli.js" "\$@"' "$TMP/bin/sindri" || { echo "FAIL: wrapper does not exec dist/cli.js with an absolute node"; exit 1; }
  grep -q "export SINDRI_BIN=\"$TMP/bin/sindri\"" "$TMP/bin/sindri" || { echo "FAIL: wrapper does not export SINDRI_BIN"; exit 1; }
  echo "PASS: test_wrapper_execs_the_built_cli"
}

test_setup_has_opt_in_flag() {
  grep -q -- '--with-sindri) WITH_SINDRI=1' "$ROOT/setup.sh" || { echo "FAIL: setup.sh lacks --with-sindri"; exit 1; }
  grep -q 'would run scripts/install-sindri.sh' "$ROOT/setup.sh" || { echo "FAIL: setup.sh dry-run line missing"; exit 1; }
  echo "PASS: test_setup_has_opt_in_flag"
}

test_observe_job_is_hourly() {
  local plist="$ROOT/config/launchd/com.agentic-workflow.sindri-observe.plist"
  [ -f "$plist" ] || { echo "FAIL: $plist missing"; exit 1; }
  if command -v plutil >/dev/null 2>&1; then plutil -lint "$plist" >/dev/null || { echo "FAIL: plist invalid"; exit 1; }; fi
  grep -q '<string>__BIN__/sindri</string>' "$plist" || { echo "FAIL: observe command missing"; exit 1; }
  grep -q '<integer>3600</integer>' "$plist" || { echo "FAIL: not hourly"; exit 1; }
  grep -q 'com.agentic-workflow.sindri-observe.plist' "$ROOT/scripts/install-sindri.sh" || { echo "FAIL: installer does not install the job"; exit 1; }
  echo "PASS: test_observe_job_is_hourly"
}

test_guard_proof_uses_a_scratch_repo() {
  local proof="$ROOT/scripts/sindri-guard-proof.sh"
  [ -x "$proof" ] || { echo "FAIL: $proof missing or not executable"; exit 1; }
  grep -q 'mktemp -d' "$proof" || { echo "FAIL: proof does not use a scratch repo"; exit 1; }
  grep -q 'GIT_CONFIG_GLOBAL=/dev/null' "$proof" || { echo "FAIL: proof inherits global git config"; exit 1; }
  echo "PASS: test_guard_proof_uses_a_scratch_repo"
}

test_dry_run_writes_nothing
test_wrapper_execs_the_built_cli
test_setup_has_opt_in_flag
test_observe_job_is_hourly
test_guard_proof_uses_a_scratch_repo
