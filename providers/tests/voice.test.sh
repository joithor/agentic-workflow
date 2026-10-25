#!/usr/bin/env bash
# Tests for the reply voice: config/voice/*.md and the managed block setup.sh writes into each
# provider's global instruction file (providers/lib.sh aw_write_managed_block / aw_install_voice).
# Runs entirely inside a throwaway $HOME; never touches the real one.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"

fail() { echo "FAIL: $*"; exit 1; }

setup_env() {
  SANDBOX="$(mktemp -d)"
  export HOME="$SANDBOX/home"
  mkdir -p "$HOME"
  export TOOLKIT_DIR="$ROOT"
  export AW_DRY_RUN=0
  unset AW_STATE_ROOT AW_STATE_DIR CODEX_HOME CLAUDE_DIR CURSOR_DIR
  # shellcheck source=../lib.sh
  source "$ROOT/providers/lib.sh"
  SRC="$SANDBOX/src.md"
  printf 'line one\nline two\n' > "$SRC"
}
teardown_env() { rm -rf "$SANDBOX"; }

count() { grep -c -F -- "$1" "$2" || true; }

test_inline_voice_stays_under_budget() {
  local size
  size="$(wc -c < "$ROOT/config/voice/joi.md" | tr -d ' ')"
  [ "$size" -le 4000 ] || fail "config/voice/joi.md is $size bytes; it is inlined every turn, keep it <= 4000"
  [ -s "$ROOT/config/voice/joi-examples.md" ] || fail "config/voice/joi-examples.md is missing"
  has "config/voice/joi-examples.md" < "$ROOT/config/voice/joi.md" || fail "joi.md should point at the examples file"
  echo "PASS: test_inline_voice_stays_under_budget"
}

test_block_created_in_missing_file() {
  setup_env
  local f="$HOME/.codex/AGENTS.md"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  [ -f "$f" ] || fail "file not created"
  [ "$(sed -n 1p "$f")" = "<!-- aw:voice:start -->" ] || fail "first line should be the start marker"
  has "line two" < "$f" || fail "content missing"
  [ "$(tail -n 1 "$f")" = "<!-- aw:voice:end -->" ] || fail "last line should be the end marker"
  teardown_env
  echo "PASS: test_block_created_in_missing_file"
}

test_block_appended_and_outside_text_kept() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md"
  mkdir -p "$(dirname "$f")"
  printf '## Mine\nkeep me\n' > "$f"
  chmod 600 "$f"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  [ "$(sed -n 1,2p "$f")" = "$(printf '## Mine\nkeep me')" ] || fail "text before the block changed"
  has "line one" < "$f" || fail "content missing"
  local mode
  mode="$(stat -f %Lp "$f" 2>/dev/null || stat -c %a "$f")"
  [ "$mode" = "600" ] || fail "file mode changed to $mode"
  teardown_env
  echo "PASS: test_block_appended_and_outside_text_kept"
}

test_rerun_replaces_in_place_without_duplicating() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" first
  mkdir -p "$(dirname "$f")"
  printf 'before\n' > "$f"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  printf 'after\n' >> "$f"
  first="$(cat "$f")"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  [ "$(cat "$f")" = "$first" ] || fail "rerun with the same content changed the file"
  printf 'new content\n' > "$SRC"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  [ "$(count '<!-- aw:voice:start -->' "$f")" = 1 ] || fail "start marker duplicated"
  has "new content" < "$f" || fail "block content not replaced"
  ! has "line one" < "$f" || fail "old block content left behind"
  [ "$(tail -n 1 "$f")" = "after" ] || fail "text after the block moved or was lost"
  [ "$(head -n 1 "$f")" = "before" ] || fail "text before the block moved or was lost"
  teardown_env
  echo "PASS: test_rerun_replaces_in_place_without_duplicating"
}

test_unclosed_block_is_refused() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" before rc=0
  mkdir -p "$(dirname "$f")"
  printf 'a\n<!-- aw:voice:start -->\nold\nmine, not the toolkit'"'"'s\n' > "$f"
  before="$(cat "$f")"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null 2>&1 || rc=$?
  [ "$rc" -ne 0 ] || fail "an unclosed block should be refused"
  [ "$(cat "$f")" = "$before" ] || fail "an unclosed block must leave the file untouched"
  teardown_env
  echo "PASS: test_unclosed_block_is_refused"
}

test_file_without_trailing_newline_stays_idempotent() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" first
  mkdir -p "$(dirname "$f")"
  printf 'no newline at the end' > "$f"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  first="$(cat "$f")"
  [ "$(sed -n 1p "$f")" = "no newline at the end" ] || fail "the last user line was glued to the start marker"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null
  [ "$(cat "$f")" = "$first" ] || fail "rerun changed the file"
  [ "$(count '<!-- aw:voice:start -->' "$f")" = 1 ] || fail "start marker duplicated"
  teardown_env
  echo "PASS: test_file_without_trailing_newline_stays_idempotent"
}

test_end_marker_before_start_is_refused() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" before rc=0
  mkdir -p "$(dirname "$f")"
  printf '<!-- aw:voice:end -->\nmine\n<!-- aw:voice:start -->\nmore of mine\n' > "$f"
  before="$(cat "$f")"
  aw_write_managed_block "$f" voice "$SRC" >/dev/null 2>&1 || rc=$?
  [ "$rc" -ne 0 ] || fail "an end marker before the start should be refused"
  [ "$(cat "$f")" = "$before" ] || fail "the file must stay untouched"
  teardown_env
  echo "PASS: test_end_marker_before_start_is_refused"
}

test_dry_run_writes_nothing() {
  setup_env
  local f="$HOME/.claude/CLAUDE.md" out
  out="$(AW_DRY_RUN=1 aw_write_managed_block "$f" voice "$SRC")"
  [ ! -e "$f" ] || fail "dry run created the file"
  echo "$out" | has "dry-run" || fail "dry run should say what it would do"
  teardown_env
  echo "PASS: test_dry_run_writes_nothing"
}

test_providers_install_the_voice() {
  setup_env
  source "$ROOT/providers/claude/install.sh"
  source "$ROOT/providers/codex/install.sh"
  source "$ROOT/providers/cursor/install.sh"
  claude_install_voice >/dev/null
  codex_install_voice >/dev/null
  has "# Talking to Joi" < "$HOME/.claude/CLAUDE.md" || fail "claude: voice not in CLAUDE.md"
  has "# Talking to Joi" < "$HOME/.codex/AGENTS.md" || fail "codex: voice not in AGENTS.md"
  cursor_install_voice | has "User Rules" || fail "cursor: should print the paste note"
  teardown_env
  echo "PASS: test_providers_install_the_voice"
}

test_skills_and_agents_read_the_voice() {
  has "config/voice/joi.md" < "$ROOT/skills/_preamble.md" || fail "the preamble should read the voice"
  local a
  # qa-runner is left out: its output is a Playwright plan a no-model runner executes, not a report.
  for a in "$ROOT"/config/agents/lean-*.md; do
    has "config/voice/joi.md" < "$a" || fail "$(basename "$a") should read the voice"
  done
  echo "PASS: test_skills_and_agents_read_the_voice"
}

test_inline_voice_stays_under_budget
test_block_created_in_missing_file
test_block_appended_and_outside_text_kept
test_rerun_replaces_in_place_without_duplicating
test_unclosed_block_is_refused
test_file_without_trailing_newline_stays_idempotent
test_end_marker_before_start_is_refused
test_dry_run_writes_nothing
test_providers_install_the_voice
test_skills_and_agents_read_the_voice
echo "ALL PASS"
