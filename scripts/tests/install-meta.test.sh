#!/usr/bin/env bash
# Tests for scripts/install-meta.sh on a sandboxed HOME and a temp state directory, without the NativeAOT publish
# (AW_SKIP_BUILD=1 with a stub binary; nothing is built, installed or linked outside the sandbox): for Claude both hooks
# registered once (Stop; PostToolUse matching SendMessage), the hook scripts and the fail-open logger copied, the PATH links
# made, and a rerun idempotent; for Codex and Cursor the lane hooks (session-id-report, meta-lane-gate) registered once through
# the adapters beside the user's own hooks, the Codex /hooks trust step printed, and nothing registered for the other events.
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
# the installer is Linux-only: stub uname to Linux so the suite passes on macOS runners too (the Darwin cases below override it)
mkdir -p "$WORK/linuxbin"
printf '#!/usr/bin/env bash\necho Linux\n' > "$WORK/linuxbin/uname"; chmod +x "$WORK/linuxbin/uname"
export PATH="$WORK/linuxbin:$PATH"
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

# Codex: the lane hooks, through the adapter, beside the user's own hooks; the trust step is printed
export CODEX_HOME="$HOME/.codex"; mkdir -p "$CODEX_HOME"
echo '{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"/x/own-stop.sh # mine"}]}]}}' > "$CODEX_HOME/hooks.json"
C="$CODEX_HOME/hooks.json"
OUT="$(bash "$DIR/install-meta.sh" --provider codex 2>&1)"; RC=$?
check "codex: exit 0" "$RC" "0"
check "codex: the trust step is printed" "$(printf '%s' "$OUT" | grep -c 'run `/hooks` to trust the `aw:\*` hooks')" "1"
check "codex: SessionStart runs session-id-report through the adapter" \
  "$(jq -r '.hooks.SessionStart[].hooks[].command' "$C" | grep -c "adapters/codex.sh $AW_STATE_DIR/hooks/session-id-report.sh # aw:session-id-report")" "1"
check "codex: UserPromptSubmit runs meta-lane-gate through the adapter" \
  "$(jq -r '.hooks.UserPromptSubmit[].hooks[].command' "$C" | grep -c "adapters/codex.sh $AW_STATE_DIR/hooks/meta-lane-gate.sh # aw:meta-lane-gate")" "1"
check "codex: Stop gets meta-lane-gate beside the user's own hook" "$(jq -r '[.hooks.Stop[].hooks[].command] | map(sub(".*# ";"")) | join(",")' "$C")" "mine,aw:meta-lane-gate"
check "codex: the Claude-only hooks are not registered" "$(grep -c 'meta-stop-gate\|meta-outbox' "$C")" "0"
check "codex: scripts, adapters and the fail-open logger staged" \
  "$(test -x "$AW_STATE_DIR/hooks/session-id-report.sh" && test -x "$AW_STATE_DIR/hooks/meta-lane-gate.sh" && test -x "$AW_STATE_DIR/hooks/adapters/codex.sh" && test -f "$AW_STATE_DIR/hooks/lib/failopen.sh" && echo yes)" "yes"
check "codex: nothing written to the Claude settings by this install" "$(cmp -s "$S" "$WORK/before" && echo same)" "same"
cp "$C" "$WORK/codex-before"
bash "$DIR/install-meta.sh" --provider codex > /dev/null 2>&1
check "codex: rerun leaves hooks.json unchanged" "$(cmp -s "$C" "$WORK/codex-before" && echo same)" "same"

# Cursor: flat entries; two scripts at sessionStart
mkdir -p "$HOME/.cursor"; U="$HOME/.cursor/hooks.json"
echo '{"version":1,"hooks":{"stop":[{"command":"/x/own-stop.sh # mine"}]}}' > "$U"
OUT="$(bash "$DIR/install-meta.sh" --provider cursor 2>&1)"; RC=$?
check "cursor: exit 0, no Codex trust step" "$RC:$(printf '%s' "$OUT" | grep -c 'run .\/hooks. to trust')" "0:0"
check "cursor: sessionStart has session-id-report and meta-lane-gate" "$(jq -r '[.hooks.sessionStart[].command] | map(sub(".*# ";"")) | sort | join(",")' "$U")" "aw:meta-lane-gate,aw:session-id-report"
check "cursor: postToolUse and stop run meta-lane-gate; the user's stop hook is kept" \
  "$(jq -r '[.hooks.postToolUse[].command] | map(sub(".*# ";"")) | join(",")' "$U"):$(jq -r '[.hooks.stop[].command] | map(sub(".*# ";"")) | join(",")' "$U")" "aw:meta-lane-gate:mine,aw:meta-lane-gate"
check "cursor: the entries go through the cursor adapter" "$(jq -r '.hooks.stop[].command' "$U" | grep -c "adapters/cursor.sh $AW_STATE_DIR/hooks/meta-lane-gate.sh")" "1"
cp "$U" "$WORK/cursor-before"
bash "$DIR/install-meta.sh" --provider cursor > /dev/null 2>&1
check "cursor: rerun leaves hooks.json unchanged" "$(cmp -s "$U" "$WORK/cursor-before" && echo same)" "same"
check "links on PATH are made for any provider too" "$(readlink "$HOME/.local/bin/aw-meta")" "$AW_STATE_DIR/bin/aw-meta"
OUT="$(AW_DRY_RUN=1 bash "$DIR/install-meta.sh" --provider codex 2>&1)"; RC=$?
check "codex dry-run: says what it would do, still prints the trust step, exit 0" "$RC:$(printf '%s' "$OUT" | grep -c 'dry-run.*lane hooks'):$(printf '%s' "$OUT" | grep -c 'run .\/hooks. to trust')" "0:1:1"
OUT="$(bash "$DIR/install-meta.sh" --provider nope 2>&1)"; RC=$?
check "an unknown provider is refused" "$RC" "1"
rm "$AW_STATE_DIR/bin/aw-meta"
bash "$DIR/install-meta.sh" --provider claude > /dev/null 2>&1; RC=$?
check "AW_SKIP_BUILD with no binary: exit 1" "$RC" "1"

# aw-meta reads Linux /proc (memory, uptime, process table): the installer refuses on macOS, before touching anything
printf '#!/usr/bin/env bash\nexit 0\n' > "$AW_STATE_DIR/bin/aw-meta"; chmod +x "$AW_STATE_DIR/bin/aw-meta"   # a binary is present, so only the refusal can keep the links away
mkdir -p "$WORK/fakebin"
printf '#!/usr/bin/env bash\necho Darwin\n' > "$WORK/fakebin/uname"; chmod +x "$WORK/fakebin/uname"
OUT="$(PATH="$WORK/fakebin:$PATH" CLAUDE_LOCAL_BIN="$WORK/darwin-bin" bash "$DIR/install-meta.sh" --provider claude 2>&1)"; RC=$?
check "Darwin: refused with the Linux-only message, exit 1" "$RC:$(printf '%s' "$OUT" | grep -c 'Linux-only')" "1:1"
check "Darwin: nothing linked" "$(ls "$WORK/darwin-bin" 2>/dev/null | wc -l | tr -d ' ')" "0"
OUT="$(PATH="$WORK/fakebin:$PATH" bash "$DIR/install-meta.sh" --provider codex 2>&1)"; RC=$?
check "Darwin, another provider: refused too, exit 1" "$RC:$(printf '%s' "$OUT" | grep -c 'Linux-only')" "1:1"
exit $fail
