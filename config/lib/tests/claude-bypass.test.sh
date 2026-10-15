#!/usr/bin/env bash
# Tests for config/lib/claude-bypass.sh. Run: bash config/lib/tests/claude-bypass.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=../claude-bypass.sh
source "$DIR/claude-bypass.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
unset AW_NO_CLAUDE_BYPASS AW_DRY_RUN
S="$WORK/settings.json"

echo '{"model":"opus","permissions":{"allow":["Bash(ls)"]}}' > "$S"
aw_claude_bypass "$S" > /dev/null
check "sets defaultMode" "$(jq -r .permissions.defaultMode "$S")" "bypassPermissions"
check "keeps the other keys" "$(jq -c '[.model, .permissions.allow]' "$S")" '["opus",["Bash(ls)"]]'
cp "$S" "$WORK/before"
OUT="$(aw_claude_bypass "$S")"
check "rerun: unchanged" "$(cmp -s "$S" "$WORK/before" && echo same)" "same"
check "rerun: says already set" "$OUT" "  permission mode: bypassPermissions (already set)"

echo '{"model":"opus"}' > "$S"
aw_claude_bypass "$S" > /dev/null
check "no permissions object: created" "$(jq -c .permissions "$S")" '{"defaultMode":"bypassPermissions"}'

echo '{"permissions":{"defaultMode":"acceptEdits"}}' > "$S"
aw_claude_bypass "$S" > /dev/null
check "another mode: replaced" "$(jq -r .permissions.defaultMode "$S")" "bypassPermissions"

echo '{"model":"opus"}' > "$S"; cp "$S" "$WORK/before"
AW_NO_CLAUDE_BYPASS=1 aw_claude_bypass "$S" > /dev/null
check "opt out: untouched" "$(cmp -s "$S" "$WORK/before" && echo same)" "same"
AW_DRY_RUN=1 aw_claude_bypass "$S" > /dev/null
check "dry run: untouched" "$(cmp -s "$S" "$WORK/before" && echo same)" "same"

echo 'not json' > "$S"
aw_claude_bypass "$S" > /dev/null 2>&1; RC=$?
check "invalid JSON: left alone" "$RC:$(cat "$S")" "0:not json"
check "invalid JSON: no temp file left" "$(ls "$WORK" | grep -c 'settings.json.aw')" "0"

ln -s "$WORK/real.json" "$WORK/link.json"; echo '{}' > "$WORK/real.json"
aw_claude_bypass "$WORK/link.json" > /dev/null
check "symlinked settings: link kept, target written" "$(test -L "$WORK/link.json" && jq -r .permissions.defaultMode "$WORK/real.json")" "bypassPermissions"
aw_claude_bypass "$WORK/absent.json"; RC=$?
check "no settings file: nothing to do" "$RC:$(test -e "$WORK/absent.json" && echo made)" "0:"
exit $fail
