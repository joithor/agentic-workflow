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
  # Heavy jobs run at background priority, so they never compete with the interactive session.
  for name in sindri-index sindri-index-quick; do
    plist="$launchd/com.agentic-workflow.$name.plist"
    tr -d ' \n' < "$plist" | grep -q '<key>ProcessType</key><string>Background</string>' || { echo "FAIL: $name is not ProcessType Background"; exit 1; }
    tr -d ' \n' < "$plist" | grep -q '<key>Nice</key><integer>10</integer>' || { echo "FAIL: $name is not Nice 10"; exit 1; }
    tr -d ' \n' < "$plist" | grep -q '<key>LowPriorityIO</key><true/>' || { echo "FAIL: $name is not LowPriorityIO"; exit 1; }
  done
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

test_nudge_hook_per_provider() {
  local out settings home="$TMP/nudge-home"
  mkdir -p "$home"
  out="$(HOME="$home" AW_DRY_RUN=1 bash "$ROOT/scripts/install-sindri.sh" --hook-only --provider codex)"
  grep -q "would install sindri-nudge (SessionStart) for codex" <<<"$out" || { echo "FAIL: codex dry-run line missing"; exit 1; }
  out="$(HOME="$home" AW_DRY_RUN=1 bash "$ROOT/scripts/install-sindri.sh" --hook-only --provider cursor)"
  grep -q "would install sindri-nudge (sessionStart) for cursor" <<<"$out" || { echo "FAIL: cursor dry-run line missing"; exit 1; }
  out="$(HOME="$home" AW_DRY_RUN=1 bash "$ROOT/scripts/install-sindri.sh" --hook-only)"
  grep -q "would install sindri-nudge (SessionStart) for claude" <<<"$out" || { echo "FAIL: claude dry-run line missing"; exit 1; }
  [ -z "$(ls -A "$home")" ] || { echo "FAIL: dry-run wrote under HOME"; exit 1; }
  settings="$TMP/settings.json"; echo '{}' > "$settings"
  HOME="$home" CLAUDE_SETTINGS_FILE="$settings" CLAUDE_HOOKS_DIR="$TMP/hooks" bash "$ROOT/scripts/install-sindri.sh" --hook-only > /dev/null
  [ "$(jq '[.hooks.SessionStart[].hooks[].command | select(test("# aw:sindri-nudge$"))] | length' "$settings")" = "1" ] || { echo "FAIL: claude SessionStart entry missing"; exit 1; }
  [ -x "$TMP/hooks/sindri-nudge.sh" ] || { echo "FAIL: claude hook script not copied"; exit 1; }
  HOME="$home" CLAUDE_SETTINGS_FILE="$settings" CLAUDE_HOOKS_DIR="$TMP/hooks" bash "$ROOT/scripts/install-sindri.sh" --hook-only > /dev/null
  [ "$(jq '[.hooks.SessionStart[].hooks[].command | select(test("# aw:sindri-nudge$"))] | length' "$settings")" = "1" ] || { echo "FAIL: reinstall duplicated the entry"; exit 1; }
  local codex="$TMP/codex-hooks.json" cursor="$TMP/cursor-hooks.json"
  HOME="$home" CODEX_HOOKS_FILE="$codex" AW_HOOKS_DIR="$TMP/aw-hooks" bash "$ROOT/scripts/install-sindri.sh" --hook-only --provider codex > /dev/null
  [ "$(jq '[.hooks.SessionStart[].hooks[].command | select(test("adapters/codex.sh .*sindri-nudge.sh # aw:sindri-nudge$"))] | length' "$codex")" = "1" ] || { echo "FAIL: codex SessionStart entry missing"; exit 1; }
  HOME="$home" CURSOR_HOOKS_FILE="$cursor" AW_HOOKS_DIR="$TMP/aw-hooks" bash "$ROOT/scripts/install-sindri.sh" --hook-only --provider cursor > /dev/null
  [ "$(jq '[.hooks.sessionStart[].command | select(test("adapters/cursor.sh .*sindri-nudge.sh # aw:sindri-nudge$"))] | length' "$cursor")" = "1" ] || { echo "FAIL: cursor sessionStart entry missing"; exit 1; }
  # A plain install prints the hints and installs no hook (only --hook-only does).
  out="$(HOME="$home" AW_SKIP_BUILD=1 AW_SKIP_LAUNCHD=1 CLAUDE_LOCAL_BIN="$TMP/bin" bash "$ROOT/scripts/install-sindri.sh")"
  grep -q "sindri repo onboard --template" <<<"$out" || { echo "FAIL: template hint missing"; exit 1; }
  grep -q "install-sindri.sh --hook-only" <<<"$out" || { echo "FAIL: nudge hint missing"; exit 1; }
  [ ! -e "$home/.claude" ] || { echo "FAIL: plain install wrote ~/.claude"; exit 1; }
  if HOME="$home" bash "$ROOT/scripts/install-sindri.sh" --frob > /dev/null 2>&1; then echo "FAIL: unknown flag accepted"; exit 1; fi
  echo "PASS: test_nudge_hook_per_provider"
}

mode_of() { stat -c '%a' "$1" 2>/dev/null || stat -f '%Lp' "$1"; }

# A scratch source repo with an origin: two merged commits on main and one unmerged commit on a branch.
make_scratch_repo() {
  local work="$TMP/src" origin="$TMP/origin.git"
  rm -rf "$work" "$origin" "$TMP/state" "$TMP/bin" "$TMP/b'in"
  git init -q --bare "$origin"
  git init -q "$work"
  git -C "$work" checkout -q -b main
  mkdir -p "$work/sindri/dist"
  echo '{ "name": "sindri-test" }' > "$work/sindri/package.json"
  echo 'process.stdout.write("channel-ok " + process.argv.slice(2).join(" "));' > "$work/sindri/dist/cli.js"
  git -C "$work" add -A
  git -C "$work" -c user.name=t -c user.email=t@example.com commit -qm "merged one"
  MERGED1="$(git -C "$work" rev-parse HEAD)"
  echo one > "$work/sindri/extra.txt"
  git -C "$work" add -A
  git -C "$work" -c user.name=t -c user.email=t@example.com commit -qm "merged two"
  MERGED2="$(git -C "$work" rev-parse HEAD)"
  git -C "$work" remote add origin "$origin"
  git -C "$work" push -q origin main
  git -C "$work" checkout -q -b feature
  echo two > "$work/sindri/more.txt"
  git -C "$work" add -A
  git -C "$work" -c user.name=t -c user.email=t@example.com commit -qm "unmerged"
  UNMERGED="$(git -C "$work" rev-parse HEAD)"
  SRC="$work"
}

channel_install() { # channel ref [extra env assignments are inherited]
  AW_SINDRI_SRC="$SRC" AW_SKIP_BUILD=1 AW_SKIP_LAUNCHD=1 AW_STATE_DIR="$TMP/state" CLAUDE_LOCAL_BIN="${BIN:-$TMP/bin}" bash "$ROOT/scripts/install-sindri.sh" --channel "$1" --ref "$2"
}

test_channel_dry_run_writes_nothing() {
  make_scratch_repo
  local out
  out="$(AW_DRY_RUN=1 AW_SINDRI_SRC="$SRC" AW_STATE_DIR="$TMP/state" CLAUDE_LOCAL_BIN="$TMP/bin" bash "$ROOT/scripts/install-sindri.sh" --channel next --ref "$MERGED2")"
  grep -q "\[dry-run\] would build sindri at $MERGED2 into $TMP/state/sindri/channels/next/$MERGED2" <<<"$out" || { echo "FAIL: dry-run build line missing"; exit 1; }
  grep -q "\[dry-run\] would write $TMP/bin/sindri-next" <<<"$out" || { echo "FAIL: dry-run wrapper line missing"; exit 1; }
  [ ! -e "$TMP/state/sindri/channels" ] && [ ! -e "$TMP/bin/sindri-next" ] || { echo "FAIL: dry-run wrote something"; exit 1; }
  echo "PASS: test_channel_dry_run_writes_nothing"
}

test_channel_refuses_unmerged_ref() {
  make_scratch_repo
  if out="$(channel_install next "$UNMERGED" 2>&1)"; then echo "FAIL: an unmerged ref was installed"; exit 1; fi
  grep -q "is not an ancestor of refs/remotes/origin/main" <<<"$out" || { echo "FAIL: wrong refusal: $out"; exit 1; }
  [ ! -e "$TMP/state/sindri/channels/next/$UNMERGED" ] || { echo "FAIL: build dir exists"; exit 1; }
  echo "PASS: test_channel_refuses_unmerged_ref"
}

test_channel_install_writes_wrapper_and_state() {
  make_scratch_repo
  channel_install next "$MERGED2" > /dev/null
  [ "$(mode_of "$TMP/state/sindri")" = "700" ] || { echo "FAIL: channel state dir is not 0700"; exit 1; }
  [ "$("$TMP/bin/sindri-next" hi)" = "channel-ok hi" ] || { echo "FAIL: sindri-next did not run the build"; exit 1; }
  node -e 'const c = JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8")); if (c.next.sha !== process.argv[2] || c.stable !== null) process.exit(1);' "$TMP/state/sindri/channels.json" "$MERGED2" || { echo "FAIL: channels.json wrong"; exit 1; }
  if out="$(channel_install next "$MERGED2" 2>&1)"; then echo "FAIL: reinstall over an existing build was allowed"; exit 1; fi
  grep -q "already exists" <<<"$out" || { echo "FAIL: wrong refusal: $out"; exit 1; }
  echo "PASS: test_channel_install_writes_wrapper_and_state"
}

test_channel_stable_bootstraps_once() {
  make_scratch_repo
  channel_install stable "$MERGED1" > /dev/null
  [ "$("$TMP/bin/sindri" yo)" = "channel-ok yo" ] || { echo "FAIL: stable wrapper broken"; exit 1; }
  local before out
  before="$(cat "$TMP/state/sindri/channels.json")"
  if out="$(channel_install stable "$MERGED2" 2>&1)"; then echo "FAIL: --channel stable replaced an existing stable"; exit 1; fi
  grep -q "sindri channel promote" <<<"$out" || { echo "FAIL: refusal does not name sindri channel promote: $out"; exit 1; }
  [ "$(cat "$TMP/state/sindri/channels.json")" = "$before" ] || { echo "FAIL: channels.json changed"; exit 1; }
  [ ! -e "$TMP/state/sindri/channels/stable/$MERGED2" ] || { echo "FAIL: a second stable build was made"; exit 1; }
  # A dry run refuses too.
  if AW_DRY_RUN=1 channel_install stable "$MERGED2" > /dev/null 2>&1; then echo "FAIL: dry run accepted a second stable"; exit 1; fi
  # next is unaffected.
  channel_install next "$MERGED2" > /dev/null || { echo "FAIL: next refused with a stable present"; exit 1; }
  echo "PASS: test_channel_stable_bootstraps_once"
}

test_channel_wrapper_quotes_paths() {
  make_scratch_repo
  BIN="$TMP/b'in" channel_install next "$MERGED1" > /dev/null
  [ "$("$TMP/b'in/sindri-next" quoted)" = "channel-ok quoted" ] || { echo "FAIL: wrapper broke on a quote in the path"; exit 1; }
  echo "PASS: test_channel_wrapper_quotes_paths"
}

# Runs the installer fully sandboxed (dry run, scratch HOME and state); sets RUN_RC and RUN_OUT.
sandboxed() {
  RUN_RC=0
  RUN_OUT="$(HOME="$TMP/sandbox-home" AW_DRY_RUN=1 AW_SKIP_BUILD=1 AW_SKIP_LAUNCHD=1 AW_STATE_DIR="$TMP/sandbox-state" CLAUDE_LOCAL_BIN="$TMP/sandbox-bin" bash "$ROOT/scripts/install-sindri.sh" "$@" 2>&1)" || RUN_RC=$?
}

test_channel_rejects_bad_arguments() {
  mkdir -p "$TMP/sandbox-home"
  local args msg
  for args in "--bogus" "--channel" "--channel next --ref" "--channel next --bogus"; do
    # shellcheck disable=SC2086
    sandboxed $args
    [ "$RUN_RC" = 1 ] || { echo "FAIL: '$args' exited $RUN_RC, want 1"; exit 1; }
    grep -q "usage: install-sindri.sh" <<<"$RUN_OUT" || { echo "FAIL: '$args' printed no usage: $RUN_OUT"; exit 1; }
  done
  sandboxed --channel nope
  [ "$RUN_RC" = 1 ] && grep -q "must be stable or next" <<<"$RUN_OUT" || { echo "FAIL: --channel nope: $RUN_RC $RUN_OUT"; exit 1; }
  sandboxed --ref abc
  [ "$RUN_RC" = 1 ] && grep -q "only makes sense with --channel" <<<"$RUN_OUT" || { echo "FAIL: --ref alone: $RUN_RC $RUN_OUT"; exit 1; }
  sandboxed --hook-only --channel next
  [ "$RUN_RC" = 1 ] && grep -q "usage: install-sindri.sh" <<<"$RUN_OUT" || { echo "FAIL: --hook-only --channel: $RUN_RC $RUN_OUT"; exit 1; }
  for msg in "--provider codex" "--provider=codex"; do
    # shellcheck disable=SC2086
    sandboxed --channel next $msg
    [ "$RUN_RC" = 1 ] && grep -q "provider" <<<"$RUN_OUT" || { echo "FAIL: --channel with $msg: $RUN_RC $RUN_OUT"; exit 1; }
  done
  [ ! -e "$TMP/sandbox-bin" ] && [ ! -e "$TMP/sandbox-state" ] || { echo "FAIL: a rejected invocation wrote something"; exit 1; }
  echo "PASS: test_channel_rejects_bad_arguments"
}

test_channel_ref_must_be_in_remote_tracking_main() {
  make_scratch_repo
  local out
  # A local tag named origin/main, on the unmerged commit, must not stand in for the remote-tracking ref.
  git -C "$SRC" tag origin/main "$UNMERGED"
  if out="$(channel_install next "$UNMERGED" 2>&1)"; then echo "FAIL: a tag named origin/main bypassed the merged-only check"; exit 1; fi
  grep -q "is not an ancestor of refs/remotes/origin/main" <<<"$out" || { echo "FAIL: wrong refusal: $out"; exit 1; }
  [ ! -e "$TMP/state/sindri/channels/next/$UNMERGED" ] || { echo "FAIL: build dir exists"; exit 1; }
  git -C "$SRC" tag -d origin/main > /dev/null
  # Same for a local branch named origin/main.
  git -C "$SRC" branch origin/main "$UNMERGED"
  if out="$(channel_install next "$UNMERGED" 2>&1)"; then echo "FAIL: a branch named origin/main bypassed the check"; exit 1; fi
  git -C "$SRC" branch -q -D origin/main
  # With no remote-tracking ref at all, even a merged sha is refused.
  git -C "$SRC" update-ref -d refs/remotes/origin/main
  if out="$(channel_install next "$MERGED2" 2>&1)"; then echo "FAIL: installed with no refs/remotes/origin/main"; exit 1; fi
  grep -q "no refs/remotes/origin/main" <<<"$out" || { echo "FAIL: wrong refusal: $out"; exit 1; }
  echo "PASS: test_channel_ref_must_be_in_remote_tracking_main"
}

test_channel_look_alike_refs_cannot_stand_in_for_a_missing_remote_ref() {
  make_scratch_repo
  local out
  git -C "$SRC" update-ref -d refs/remotes/origin/main
  # A tag whose name is the full remote ref, on the unmerged commit.
  git -C "$SRC" tag refs/remotes/origin/main "$UNMERGED"
  if out="$(channel_install next "$UNMERGED" 2>&1)"; then echo "FAIL: a tag named refs/remotes/origin/main bypassed the check"; exit 1; fi
  grep -q "no refs/remotes/origin/main" <<<"$out" || { echo "FAIL: wrong refusal (tag): $out"; exit 1; }
  git -C "$SRC" tag -d refs/remotes/origin/main > /dev/null
  # A branch with that name.
  git -C "$SRC" update-ref refs/heads/refs/remotes/origin/main "$UNMERGED"
  if out="$(channel_install next "$UNMERGED" 2>&1)"; then echo "FAIL: a branch named refs/remotes/origin/main bypassed the check"; exit 1; fi
  grep -q "no refs/remotes/origin/main" <<<"$out" || { echo "FAIL: wrong refusal (branch): $out"; exit 1; }
  [ ! -e "$TMP/state/sindri/channels/next/$UNMERGED" ] || { echo "FAIL: build dir exists"; exit 1; }
  echo "PASS: test_channel_look_alike_refs_cannot_stand_in_for_a_missing_remote_ref"
}

test_channel_wrong_shaped_state_refuses_before_the_build() {
  make_scratch_repo
  local shim="$TMP/npm-shim" out content
  mkdir -p "$shim" "$TMP/state/sindri"
  printf '#!/bin/sh\necho built >> "%s/calls"\nexit 0\n' "$shim" > "$shim/npm"
  chmod +x "$shim/npm"
  for content in '{ not json' '[]' '{}' '{"stable": 5, "next": null}' 'null'; do
    for channel in stable next; do
      printf '%s' "$content" > "$TMP/state/sindri/channels.json"
      : > "$shim/calls"
      if out="$(PATH="$shim:$PATH" AW_SINDRI_SRC="$SRC" AW_SKIP_LAUNCHD=1 AW_STATE_DIR="$TMP/state" CLAUDE_LOCAL_BIN="$TMP/bin" bash "$ROOT/scripts/install-sindri.sh" --channel "$channel" --ref "$MERGED2" 2>&1)"; then echo "FAIL: '$content' was accepted on $channel"; exit 1; fi
      grep -q "repair or restore channels.json" <<<"$out" || { echo "FAIL: '$content' ($channel): no repair hint: $out"; exit 1; }
      if grep -qi "delete" <<<"$out"; then echo "FAIL: '$content' ($channel): the message says delete: $out"; exit 1; fi
      [ ! -s "$shim/calls" ] || { echo "FAIL: '$content' ($channel): the build ran before the refusal"; exit 1; }
      [ ! -e "$TMP/state/sindri/channels/$channel/$MERGED2" ] && [ ! -e "$TMP/bin/sindri" ] && [ ! -e "$TMP/bin/sindri-next" ] || { echo "FAIL: '$content' ($channel): something was written"; exit 1; }
      [ "$(cat "$TMP/state/sindri/channels.json")" = "$content" ] || { echo "FAIL: '$content' ($channel): channels.json was changed"; exit 1; }
    done
  done
  echo "PASS: test_channel_wrong_shaped_state_refuses_before_the_build"
}

test_channel_missing_state_is_a_first_install() {
  make_scratch_repo
  channel_install stable "$MERGED1" > /dev/null || { echo "FAIL: a first stable install was refused"; exit 1; }
  [ "$("$TMP/bin/sindri" yo)" = "channel-ok yo" ] || { echo "FAIL: first stable install did not run"; exit 1; }
  echo "PASS: test_channel_missing_state_is_a_first_install"
}

test_channel_record_refuses_state_that_turns_wrong_shaped_during_the_build() {
  make_scratch_repo
  channel_install next "$MERGED1" > /dev/null
  local shim="$TMP/npm-shim" out before
  before="$(cat "$TMP/bin/sindri-next")"
  mkdir -p "$shim"
  # The build "damages" channels.json after the pre-flight check passed.
  printf '#!/bin/sh\nprintf "[]" > "%s/state/sindri/channels.json"\nexit 0\n' "$TMP" > "$shim/npm"
  chmod +x "$shim/npm"
  if out="$(PATH="$shim:$PATH" AW_SINDRI_SRC="$SRC" AW_SKIP_LAUNCHD=1 AW_STATE_DIR="$TMP/state" CLAUDE_LOCAL_BIN="$TMP/bin" bash "$ROOT/scripts/install-sindri.sh" --channel next --ref "$MERGED2" 2>&1)"; then echo "FAIL: record_channel wrote into a wrong-shaped file"; exit 1; fi
  grep -q "repair or restore channels.json" <<<"$out" || { echo "FAIL: wrong refusal: $out"; exit 1; }
  [ "$(cat "$TMP/state/sindri/channels.json")" = "[]" ] || { echo "FAIL: channels.json was rewritten"; exit 1; }
  [ "$(cat "$TMP/bin/sindri-next")" = "$before" ] || { echo "FAIL: the wrapper was switched"; exit 1; }
  echo "PASS: test_channel_record_refuses_state_that_turns_wrong_shaped_during_the_build"
}

test_channel_failed_build_does_not_block_a_retry() {
  make_scratch_repo
  local out shim="$TMP/npm-shim"
  mkdir -p "$shim"
  : > "$shim/calls"
  # A failing npm: it leaves a half-built node_modules, logs the call, and exits 7.
  printf '#!/bin/sh\necho "$@" >> "%s/calls"\nmkdir -p node_modules\nexit 7\n' "$shim" > "$shim/npm"
  chmod +x "$shim/npm"
  if out="$(PATH="$shim:$PATH" AW_SINDRI_SRC="$SRC" AW_SKIP_LAUNCHD=1 AW_STATE_DIR="$TMP/state" CLAUDE_LOCAL_BIN="$TMP/bin" bash "$ROOT/scripts/install-sindri.sh" --channel next --ref "$MERGED2" 2>&1)"; then echo "FAIL: a failing build was accepted"; exit 1; fi
  grep -q "^ci" "$shim/calls" || { echo "FAIL: the npm shim was not the build that ran"; exit 1; }
  [ ! -e "$TMP/state/sindri/channels/next/$MERGED2" ] || { echo "FAIL: the failed build left a directory behind"; exit 1; }
  [ ! -e "$TMP/bin/sindri-next" ] && [ ! -e "$TMP/state/sindri/channels.json" ] || { echo "FAIL: the failed build wrote the wrapper or state"; exit 1; }
  # A retry with a working npm succeeds: nothing blocks the same sha.
  printf '#!/bin/sh\necho "$@" >> "%s/calls-ok"\nexit 0\n' "$shim" > "$shim/npm"
  PATH="$shim:$PATH" AW_SINDRI_SRC="$SRC" AW_SKIP_LAUNCHD=1 AW_STATE_DIR="$TMP/state" CLAUDE_LOCAL_BIN="$TMP/bin" bash "$ROOT/scripts/install-sindri.sh" --channel next --ref "$MERGED2" > /dev/null || { echo "FAIL: the retry was refused"; exit 1; }
  grep -q "^run build" "$shim/calls-ok" || { echo "FAIL: the retry did not run the build"; exit 1; }
  [ "$("$TMP/bin/sindri-next" again)" = "channel-ok again" ] || { echo "FAIL: retry did not install"; exit 1; }
  echo "PASS: test_channel_failed_build_does_not_block_a_retry"
}

test_channel_refuses_a_symlink_in_the_archive() {
  make_scratch_repo
  git -C "$SRC" checkout -q main
  ln -s /etc/hosts "$SRC/sindri/link"
  git -C "$SRC" add -A
  git -C "$SRC" -c user.name=t -c user.email=t@example.com commit -qm "symlink"
  git -C "$SRC" push -q origin main
  local sha out
  sha="$(git -C "$SRC" rev-parse HEAD)"
  if out="$(channel_install next "$sha" 2>&1)"; then echo "FAIL: an archive with a symlink was installed"; exit 1; fi
  grep -q "contains symlinks" <<<"$out" || { echo "FAIL: wrong refusal: $out"; exit 1; }
  [ ! -e "$TMP/state/sindri/channels/next/$sha" ] || { echo "FAIL: build dir exists"; exit 1; }
  echo "PASS: test_channel_refuses_a_symlink_in_the_archive"
}

test_channel_corrupt_state_leaves_the_wrapper_alone() {
  make_scratch_repo
  channel_install next "$MERGED1" > /dev/null
  local before out
  before="$(cat "$TMP/bin/sindri-next")"
  echo '{ not json' > "$TMP/state/sindri/channels.json"
  if out="$(channel_install next "$MERGED2" 2>&1)"; then echo "FAIL: a corrupt channels.json was accepted"; exit 1; fi
  grep -q "channels.json is unreadable" <<<"$out" || { echo "FAIL: wrong refusal: $out"; exit 1; }
  [ "$(cat "$TMP/bin/sindri-next")" = "$before" ] || { echo "FAIL: the wrapper was switched before the state was recorded"; exit 1; }
  [ ! -e "$TMP/state/sindri/channels/next/$MERGED2" ] || { echo "FAIL: the new build was left behind"; exit 1; }
  echo "PASS: test_channel_corrupt_state_leaves_the_wrapper_alone"
}

test_channel_refuses_a_directory_at_the_wrapper_path() {
  make_scratch_repo
  mkdir -p "$TMP/bin/sindri-next"
  local out
  if out="$(channel_install next "$MERGED2" 2>&1)"; then echo "FAIL: a directory at the wrapper path was accepted"; exit 1; fi
  grep -q "is a directory" <<<"$out" || { echo "FAIL: wrong refusal: $out"; exit 1; }
  [ -z "$(ls -A "$TMP/bin/sindri-next")" ] || { echo "FAIL: the wrapper was moved into the directory"; exit 1; }
  [ ! -e "$TMP/state/sindri/channels/next/$MERGED2" ] || { echo "FAIL: a build was made"; exit 1; }
  echo "PASS: test_channel_refuses_a_directory_at_the_wrapper_path"
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
test_nudge_hook_per_provider
test_channel_dry_run_writes_nothing
test_channel_refuses_unmerged_ref
test_channel_install_writes_wrapper_and_state
test_channel_stable_bootstraps_once
test_channel_wrapper_quotes_paths
test_channel_rejects_bad_arguments
test_channel_ref_must_be_in_remote_tracking_main
test_channel_failed_build_does_not_block_a_retry
test_channel_refuses_a_symlink_in_the_archive
test_channel_corrupt_state_leaves_the_wrapper_alone
test_channel_refuses_a_directory_at_the_wrapper_path
test_channel_look_alike_refs_cannot_stand_in_for_a_missing_remote_ref
test_channel_wrong_shaped_state_refuses_before_the_build
test_channel_missing_state_is_a_first_install
test_channel_record_refuses_state_that_turns_wrong_shaped_during_the_build
