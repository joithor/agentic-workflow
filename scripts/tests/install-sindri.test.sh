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

# Runs the launchd branch for real against a scratch HOME and a stub launchctl.
# $1 = stub mode (ok|fail), $2 = bin dir. Sets RUN_OUT, RUN_RC, STUB_LOG, SCRATCH.
run_launchd_install() {
  SCRATCH="$(mktemp -d "$TMP/launchd.XXXXXX")"
  mkdir -p "$SCRATCH/home" "$SCRATCH/stub"
  STUB_LOG="$SCRATCH/launchctl.log"
  cat > "$SCRATCH/stub/launchctl" <<STUB
#!/usr/bin/env bash
echo "\$*" >> "$STUB_LOG"
if [ "$1" = "fail" ] && [ "\$1" = "bootstrap" ]; then exit 5; fi
exit 0
STUB
  chmod +x "$SCRATCH/stub/launchctl"
  [ "$(PATH="$SCRATCH/stub:$PATH" command -v launchctl)" = "$SCRATCH/stub/launchctl" ] || { echo "FAIL: stub launchctl is not first on PATH"; exit 1; }
  RUN_RC=0
  RUN_OUT="$(AW_SKIP_LAUNCHD=0 HOME="$SCRATCH/home" PATH="$SCRATCH/stub:$PATH" AW_SKIP_BUILD=1 CLAUDE_LOCAL_BIN="${2:-$SCRATCH/bin}" bash "$ROOT/scripts/install-sindri.sh" 2>&1)" || RUN_RC=$?
}

skip_unless_darwin() {
  if [ "$(uname -s)" != "Darwin" ]; then echo "SKIP: $1 (launchd branch is macOS only)"; return 0; fi
  return 1
}

test_launchd_install_writes_and_loads_plist() {
  skip_unless_darwin test_launchd_install_writes_and_loads_plist && return 0
  run_launchd_install ok
  [ "$RUN_RC" = 0 ] || { echo "FAIL: installer exited $RUN_RC: $RUN_OUT"; exit 1; }
  local plist="$SCRATCH/home/Library/LaunchAgents/com.agentic-workflow.sindri-observe.plist"
  [ -f "$plist" ] || { echo "FAIL: plist not written under scratch HOME"; exit 1; }
  grep -q "$SCRATCH/bin/sindri" "$plist" || { echo "FAIL: plist lacks scratch bin path"; exit 1; }
  ! grep -qE '__HOME__|__BIN__' "$plist" || { echo "FAIL: plist has unsubstituted placeholders"; exit 1; }
  if command -v plutil >/dev/null 2>&1; then plutil -lint "$plist" >/dev/null || { echo "FAIL: generated plist invalid"; exit 1; }; fi
  [ "$(stat -f %Lp "$SCRATCH/home/.agentic-workflow/sindri")" = 700 ] || { echo "FAIL: state dir not 0700"; exit 1; }
  local first second
  first="$(sed -n 1p "$STUB_LOG")"; second="$(sed -n 2p "$STUB_LOG")"
  [[ "$first" == bootout* ]] || { echo "FAIL: first launchctl call was '$first', want bootout"; exit 1; }
  [[ "$second" == "bootstrap gui/"*"$plist" ]] || { echo "FAIL: second launchctl call was '$second', want bootstrap"; exit 1; }
  echo "PASS: test_launchd_install_writes_and_loads_plist"
}

test_launchd_bootstrap_failure_warns_and_continues() {
  skip_unless_darwin test_launchd_bootstrap_failure_warns_and_continues && return 0
  run_launchd_install fail
  [ "$RUN_RC" = 0 ] || { echo "FAIL: installer exited $RUN_RC on bootstrap failure: $RUN_OUT"; exit 1; }
  local name
  for name in sindri-observe sindri-index-quick sindri-index; do
    grep -q "WARN: could not load com.agentic-workflow.$name; run: launchctl bootstrap gui/" <<<"$RUN_OUT" || { echo "FAIL: WARN line missing for $name: $RUN_OUT"; exit 1; }
  done
  ! grep -q 'hourly observe (launchd' <<<"$RUN_OUT" || { echo "FAIL: success line printed after a failed load"; exit 1; }
  # Three jobs (observe, quick index, full index), each tried twice.
  [ "$(grep -c '^bootstrap' "$STUB_LOG")" = 6 ] || { echo "FAIL: bootstrap not retried once per job"; exit 1; }
  echo "PASS: test_launchd_bootstrap_failure_warns_and_continues"
}

test_launchd_refuses_unsafe_bin_path() {
  skip_unless_darwin test_launchd_refuses_unsafe_bin_path && return 0
  run_launchd_install ok "$TMP/un&safe/bin"
  [ "$RUN_RC" != 0 ] || { echo "FAIL: installer accepted a bin dir containing &"; exit 1; }
  [ ! -e "$TMP/un&safe/bin/sindri" ] || { echo "FAIL: wrapper written for unsafe path"; exit 1; }
  [ ! -e "$SCRATCH/home/Library/LaunchAgents/com.agentic-workflow.sindri-observe.plist" ] || { echo "FAIL: plist written for unsafe path"; exit 1; }
  echo "PASS: test_launchd_refuses_unsafe_bin_path"
}

test_index_jobs() {
  local launchd="$ROOT/config/launchd"
  local name plist
  for name in sindri-observe sindri-index sindri-index-quick; do
    plist="$launchd/com.agentic-workflow.$name.plist"
    [ -f "$plist" ] || { echo "FAIL: $plist missing"; exit 1; }
    if command -v plutil >/dev/null 2>&1; then plutil -lint "$plist" >/dev/null || { echo "FAIL: $name plist invalid"; exit 1; }; fi
    grep -q '<string>__BIN__/sindri</string>' "$plist" || { echo "FAIL: $name command missing"; exit 1; }
    grep -q '<key>EnvironmentVariables</key>' "$plist" && grep -q '<string>__BIN__:__HOME__/.local/bin:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin</string>' "$plist" || { echo "FAIL: $name has no PATH (launchd's default has no uv or Homebrew)"; exit 1; }
    # Exact: "sindri-index" must not be satisfied by "sindri-index-quick".
    grep -qE "com\.agentic-workflow\.$name(\||\.plist)" "$ROOT/scripts/install-sindri.sh" || { echo "FAIL: installer does not install $name"; exit 1; }
    sed -e "s|__HOME__|/h|g" -e "s|__BIN__|/b|g" "$plist" | grep -q '__' && { echo "FAIL: $name has a placeholder the installer does not substitute"; exit 1; }
  done
  grep -q '<string>--quick</string>' "$launchd/com.agentic-workflow.sindri-index-quick.plist" && grep -q '<integer>3600</integer>' "$launchd/com.agentic-workflow.sindri-index-quick.plist" || { echo "FAIL: the quick job is not an hourly --quick build"; exit 1; }
  grep -q '<key>Hour</key>' "$launchd/com.agentic-workflow.sindri-index.plist" || { echo "FAIL: the full build is not nightly"; exit 1; }
  if grep -q -- '--quick' "$launchd/com.agentic-workflow.sindri-index.plist"; then echo "FAIL: the nightly build is quick"; exit 1; fi
  # Each index job has its own log, so one job's failure isn't read as the other's.
  [ "$(grep -c '<string>__HOME__/.agentic-workflow/sindri/index-quick-launchd.log</string>' "$launchd/com.agentic-workflow.sindri-index-quick.plist")" = 2 ] || { echo "FAIL: the quick job does not log to index-quick-launchd.log"; exit 1; }
  [ "$(grep -c '<string>__HOME__/.agentic-workflow/sindri/index-launchd.log</string>' "$launchd/com.agentic-workflow.sindri-index.plist")" = 2 ] || { echo "FAIL: the nightly job does not log to index-launchd.log"; exit 1; }
  echo "PASS: test_index_jobs"
}

test_launchd_installs_every_job() {
  skip_unless_darwin test_launchd_installs_every_job && return 0
  run_launchd_install ok
  [ "$RUN_RC" = 0 ] || { echo "FAIL: installer exited $RUN_RC: $RUN_OUT"; exit 1; }
  local name plist
  for name in sindri-observe sindri-index-quick sindri-index; do
    plist="$SCRATCH/home/Library/LaunchAgents/com.agentic-workflow.$name.plist"
    [ -f "$plist" ] || { echo "FAIL: $name plist not written under scratch HOME"; exit 1; }
    ! grep -qE '__HOME__|__BIN__' "$plist" || { echo "FAIL: $name plist has unsubstituted placeholders"; exit 1; }
    grep -q "<string>$SCRATCH/bin:$SCRATCH/home/.local/bin:" "$plist" || { echo "FAIL: $name plist PATH not substituted"; exit 1; }
    grep -q "^bootstrap gui/.*$plist\$" "$STUB_LOG" || { echo "FAIL: $name not bootstrapped"; exit 1; }
    grep -q "launchd com.agentic-workflow.$name)" <<<"$RUN_OUT" || { echo "FAIL: no success line for $name"; exit 1; }
  done
  echo "PASS: test_launchd_installs_every_job"
}

test_dry_run_writes_nothing
test_wrapper_execs_the_built_cli
test_setup_has_opt_in_flag
test_observe_job_is_hourly
test_guard_proof_uses_a_scratch_repo
test_launchd_install_writes_and_loads_plist
test_launchd_bootstrap_failure_warns_and_continues
test_launchd_refuses_unsafe_bin_path
test_index_jobs
test_launchd_installs_every_job
