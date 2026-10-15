#!/usr/bin/env bash
# Tests for scripts/install-meta.sh on a sandboxed HOME, without the NativeAOT publish (AW_SKIP_BUILD=1 with a stub
# binary): both hooks registered once (Stop; PostToolUse matching SendMessage), the hook scripts and the fail-open
# logger copied, the PATH links made, other providers skipped, and a rerun idempotent.
# Run: bash scripts/tests/install-meta.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done   # the caller may be a meta session: its team config, bin and claude must not reach the stubs
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" AW_SKIP_BUILD=1
unset CLAUDE_DIR CLAUDE_LOCAL_BIN AW_DRY_RUN
mkdir -p "$HOME/.claude" "$AW_STATE_DIR/bin"
printf '#!/usr/bin/env bash\nexit 0\n' > "$AW_STATE_DIR/bin/aw-meta"; chmod +x "$AW_STATE_DIR/bin/aw-meta"
S="$HOME/.claude/settings.json"
echo '{"model":"opus","hooks":{"Stop":[{"hooks":[{"type":"command","command":"/x/done-gate.sh # aw:done-gate"}]}]}}' > "$S"

bash "$DIR/install-meta.sh" --provider claude > "$WORK/out" 2>&1; RC=$?
check "exit 0" "$RC" "0"
check "Stop: the meta gate added beside the existing hook" \
  "$(jq -r '[.hooks.Stop[].hooks[].command] | map(sub(".*# ";"")) | join(",")' "$S")" "aw:done-gate,aw:meta-stop-gate"
check "PostToolUse: the outbox hook, matching SendMessage" \
  "$(jq -r '.hooks.PostToolUse[] | select(.hooks[0].command | endswith("# aw:meta-outbox")) | .matcher' "$S")" "SendMessage"
check "other settings kept" "$(jq -r .model "$S")" "opus"
check "hook scripts and the fail-open logger copied, executable" \
  "$(test -x "$HOME/.claude/hooks/meta-stop-gate.sh" && test -x "$HOME/.claude/hooks/meta-outbox.sh" && test -f "$HOME/.claude/hooks/lib/failopen.sh" && echo yes)" "yes"
check "links on PATH" "$(readlink "$HOME/.local/bin/aw-meta"):$(basename "$(readlink "$HOME/.local/bin/aw-meta-launch")")" \
  "$AW_STATE_DIR/bin/aw-meta:meta-launch.sh"

cp "$S" "$WORK/before"
bash "$DIR/install-meta.sh" --provider claude > /dev/null 2>&1
check "rerun: settings unchanged (no duplicate hooks)" "$(cmp -s "$S" "$WORK/before" && echo same)" "same"

OUT="$(bash "$DIR/install-meta.sh" --provider codex 2>&1)"; RC=$?
check "another provider: skipped, exit 0" "$RC:$(printf '%s' "$OUT" | grep -c 'Claude Code only')" "0:1"
rm "$AW_STATE_DIR/bin/aw-meta"
bash "$DIR/install-meta.sh" --provider claude > /dev/null 2>&1; RC=$?
check "AW_SKIP_BUILD with no binary: exit 1" "$RC" "1"
exit $fail
