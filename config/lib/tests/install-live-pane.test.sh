#!/usr/bin/env bash
# Tests for scripts/install-live-pane.sh. A stub `claude` records every call and keeps a
# tiny marketplace/plugin state, so nothing here touches a real Claude Code config.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"
INSTALL="$ROOT/scripts/install-live-pane.sh"
ID="aw-live@agentic-workflow-mods"

fail() { echo "FAIL: $*"; exit 1; }

# new_world sets BIN (stub dir), STATE, LOG, HOME. Every case gets its own.
new_world() {
  local w; w="$(mktemp -d)"
  BIN="$w/bin"; STATE="$w/state"; LOG="$w/claude.log"; mkdir -p "$BIN" "$STATE" "$w/home"
  : > "$LOG"
  export HOME="$w/home" AW_STUB_STATE="$STATE" AW_STUB_LOG="$LOG" AW_DRY_RUN=0
  unset CLAUDE_LOCAL_BIN
  cat > "$BIN/claude" <<'STUB'
#!/usr/bin/env bash
echo "$*" >> "$AW_STUB_LOG"
state="$AW_STUB_STATE"
case "$1 $2 $3" in
  "plugin marketplace list") cat "$state/marketplaces.json" 2>/dev/null || echo '[]' ;;
  "plugin marketplace add") jq -n --arg l "$4" '[{name: "agentic-workflow-mods", source: "directory", path: $l, installLocation: $l}]' > "$state/marketplaces.json" ;;
  "plugin marketplace remove") echo '[]' > "$state/marketplaces.json"; echo '[]' > "$state/plugins.json" ;;
  "plugin list --json") cat "$state/plugins.json" 2>/dev/null || echo '[]' ;;
  "plugin install aw-live@agentic-workflow-mods") echo '[{"id": "aw-live@agentic-workflow-mods"}]' > "$state/plugins.json" ;;
  "plugin uninstall aw-live@agentic-workflow-mods") echo '[]' > "$state/plugins.json" ;;
esac
STUB
  chmod +x "$BIN/claude"
  export PATH="$BIN:$PATH"
}

writes() { grep -cE '^plugin (marketplace (add|remove)|install|uninstall)' "$LOG" || true; }

test_fresh_install_adds_the_marketplace_then_the_plugin_at_user_scope() {
  new_world
  bash "$INSTALL" > /dev/null
  grep -qx "plugin marketplace add $ROOT/mods --scope user" "$LOG" || fail "marketplace not added from this checkout: $(cat "$LOG")"
  grep -qx "plugin install $ID --scope user" "$LOG" || fail "plugin not installed: $(cat "$LOG")"
  echo "PASS: test_fresh_install_adds_the_marketplace_then_the_plugin_at_user_scope"
}

test_second_run_changes_nothing() {
  new_world
  bash "$INSTALL" > /dev/null
  local before; before="$(writes)"
  local out; out="$(bash "$INSTALL")"
  [ "$(writes)" = "$before" ] || fail "second run wrote again: $(cat "$LOG")"
  echo "$out" | grep -q "already installed" || fail "expected an already-installed note, got: $out"
  echo "PASS: test_second_run_changes_nothing"
}

test_dry_run_writes_nothing_and_says_what_it_would_do() {
  new_world
  local out; out="$(bash "$INSTALL" --dry-run)"
  [ "$(writes)" = "0" ] || fail "dry-run wrote: $(cat "$LOG")"
  echo "$out" | grep -q "\[dry-run\] claude plugin marketplace add" || fail "dry-run did not print the add: $out"
  echo "$out" | grep -q "would be installed" || fail "dry-run did not say it would install: $out"
  new_world
  out="$(AW_DRY_RUN=1 bash "$INSTALL")"
  [ "$(writes)" = "0" ] || fail "AW_DRY_RUN=1 wrote: $(cat "$LOG")"
  echo "PASS: test_dry_run_writes_nothing_and_says_what_it_would_do"
}

test_a_marketplace_registered_from_another_checkout_is_moved_here() {
  new_world
  jq -n '[{name: "agentic-workflow-mods", source: "directory", path: "/old/checkout/mods", installLocation: "/old/checkout/mods"}]' > "$STATE/marketplaces.json"
  echo "[{\"id\": \"$ID\"}]" > "$STATE/plugins.json"
  bash "$INSTALL" > /dev/null
  grep -qx "plugin marketplace remove agentic-workflow-mods" "$LOG" || fail "old marketplace not removed: $(cat "$LOG")"
  grep -qx "plugin marketplace add $ROOT/mods --scope user" "$LOG" || fail "this checkout not added: $(cat "$LOG")"
  grep -qx "plugin install $ID --scope user" "$LOG" || fail "plugin not reinstalled after the move: $(cat "$LOG")"
  echo "PASS: test_a_marketplace_registered_from_another_checkout_is_moved_here"
}

test_codex_and_cursor_are_skipped_with_a_reason_and_no_claude_calls() {
  local p
  for p in codex cursor; do
    new_world
    local out; out="$(bash "$INSTALL" --provider "$p")"
    echo "$out" | grep -q "skipped for $p" || fail "$p: no skip note: $out"
    echo "$out" | grep -q "Claude Code only" || fail "$p: skip note does not say why: $out"
    [ ! -s "$LOG" ] || fail "$p: claude was called: $(cat "$LOG")"
  done
  echo "PASS: test_codex_and_cursor_are_skipped_with_a_reason_and_no_claude_calls"
}

test_uninstall_removes_exactly_our_two_ids_and_is_safe_when_absent() {
  new_world
  bash "$INSTALL" > /dev/null
  : > "$LOG"
  bash "$INSTALL" --uninstall > /dev/null
  grep -qx "plugin uninstall $ID --scope user" "$LOG" || fail "plugin not uninstalled: $(cat "$LOG")"
  grep -qx "plugin marketplace remove agentic-workflow-mods" "$LOG" || fail "marketplace not removed: $(cat "$LOG")"
  : > "$LOG"
  bash "$INSTALL" --uninstall > /dev/null
  [ "$(writes)" = "0" ] || fail "uninstall with nothing installed wrote: $(cat "$LOG")"
  echo "PASS: test_uninstall_removes_exactly_our_two_ids_and_is_safe_when_absent"
}

test_a_missing_claude_cli_is_a_clean_skip() {
  new_world
  rm "$BIN/claude"
  if PATH="/usr/bin:/bin" command -v claude > /dev/null; then echo "SKIP: test_a_missing_claude_cli_is_a_clean_skip (claude lives in /usr/bin here)"; return; fi
  local out; out="$(PATH="/usr/bin:/bin" bash "$INSTALL")" || fail "exited non-zero without claude"
  echo "$out" | grep -q "the claude CLI is not on PATH" || fail "no skip note: $out"
  echo "PASS: test_a_missing_claude_cli_is_a_clean_skip"
}

test_warns_when_scorer_has_no_live_command_and_stays_quiet_when_it_does() {
  new_world
  mkdir -p "$HOME/bin2"
  printf '#!/usr/bin/env bash\nexit 1\n' > "$HOME/bin2/scorer"; chmod +x "$HOME/bin2/scorer"
  local out; out="$(CLAUDE_LOCAL_BIN="$HOME/bin2" bash "$INSTALL")"
  echo "$out" | grep -q "WARN: .* has no 'live' command" || fail "expected a scorer warning: $out"
  printf '#!/usr/bin/env bash\necho "{\\"v\\":1}"\n' > "$HOME/bin2/scorer"
  out="$(CLAUDE_LOCAL_BIN="$HOME/bin2" bash "$INSTALL")"
  echo "$out" | grep -q "WARN" && fail "unexpected warning with a live-capable scorer: $out"
  echo "PASS: test_warns_when_scorer_has_no_live_command_and_stays_quiet_when_it_does"
}

test_fresh_install_adds_the_marketplace_then_the_plugin_at_user_scope
test_second_run_changes_nothing
test_dry_run_writes_nothing_and_says_what_it_would_do
test_a_marketplace_registered_from_another_checkout_is_moved_here
test_codex_and_cursor_are_skipped_with_a_reason_and_no_claude_calls
test_uninstall_removes_exactly_our_two_ids_and_is_safe_when_absent
test_a_missing_claude_cli_is_a_clean_skip
test_warns_when_scorer_has_no_live_command_and_stays_quiet_when_it_does
echo "All install-live-pane tests passed."
