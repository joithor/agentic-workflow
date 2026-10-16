#!/usr/bin/env bash
# scripts/ci/tmux-isolation.sh — static check: no test runs a tmux that could reach the box's live server.
#
# A test shell that runs inside a tmux client has $TMUX set, and tmux follows $TMUX before anything else, so a bare `tmux ...` in
# a test once killed the live /meta team. The rule: a test reaches a real tmux only through scripts/test-tmux.sh (ttmux, or the
# PATH shim it installs), or names an explicit private socket with -L / -S, or goes through the AW_TMUX stub.
#
# Scanned: shell tests (scripts/tests, config/**/tests, providers/tests, skills/**/tests, any *.test.sh) and C# tests under tests/.
#   shell: a `tmux` word at command position (line start, or after ; && || | ( $( ` env command exec sudo xargs time) is a violation
#          (also after if/while/until/then/do/else/!/{ and after timeout, env, nohup and VAR=value prefixes) unless the line has `-L `, `-S `, `AW_TMUX`, or the waiver comment `# tmux-isolation: ok`, or the file sources
#          scripts/test-tmux.sh (`. ".../test-tmux.sh"` or `source ...test-tmux.sh`): its shim then makes plain tmux private.
#          Comment lines are skipped. A tmux-isolation waiver needs a reason on the same line.
#   C#:    a Process start of the program "tmux" (FileName = "tmux", ProcessStartInfo("tmux"), Process.Start("tmux", CommandRequest(["tmux")
#          is a violation unless the line has `"-L"` or the waiver comment `// tmux-isolation: ok`.
# Usage: scripts/ci/tmux-isolation.sh [--root DIR]   prints file:line: text for each violation; exit 0 clean, 1 violations, 2 usage.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
while [ $# -gt 0 ]; do
  case "$1" in
    --root) [ $# -ge 2 ] || { echo "tmux-isolation: --root needs a directory" >&2; exit 2; }; ROOT="$(cd "$2" && pwd)" || exit 2; shift 2 ;;
    *) echo "usage: tmux-isolation.sh [--root DIR]" >&2; exit 2 ;;
  esac
done
cd "$ROOT" || exit 2

SELF="scripts/ci/tmux-isolation.sh"
SELF_TEST="scripts/tests/tmux-isolation.test.sh"   # its fixtures are the violations
# The file list: inside a git checkout, what git tracks or would track (so ignored directories and, from the main checkout, other
# worktrees' files are never scanned); outside one, a find that skips .git, .claude/worktrees, node_modules, obj and bin.
if [ "$(git -C "$ROOT" rev-parse --show-toplevel 2> /dev/null)" = "$ROOT" ]; then
  listing="$(git ls-files --cached --others --exclude-standard 2> /dev/null | sort -u)"
else
  listing="$(find . \( -path ./.git -o -path ./.claude/worktrees -o -name node_modules -o -name obj -o -name bin \) -prune -o -type f \( -name '*.sh' -o -name '*.cs' \) -print 2> /dev/null | sed 's|^\./||' | sort -u)"
fi
files="$(grep -E '(\.test\.sh$|^tests/.*\.sh$)' <<< "$listing")"
cs_files="$(grep -E '^tests/.*\.cs$' <<< "$listing")"

violations=0
report() { printf '%s:%s: %s\n' "$1" "$2" "$3"; violations=$((violations + 1)); }

# A command position: line start or after ; & | ( ` $( or { ; then an optional keyword (if while until ! then do else elif time), wrappers
# (env command exec sudo xargs nohup, timeout with its duration), and VAR=value prefixes; then tmux itself.
CMDPOS='(^|[;&|(`{]|\$\()[[:space:]]*((if|while|until|elif|then|do|else|!|time)[[:space:]]+)*((env|command|exec|xargs|nohup|sudo([[:space:]]+-[A-Za-z-]+([[:space:]]+[A-Za-z0-9_.-]+)?)*|timeout([[:space:]]+(-[A-Za-z-]+|[0-9][0-9a-z.]*))*)[[:space:]]+)*([A-Za-z_][A-Za-z0-9_]*=("[^"]*"|'"'"'[^'"'"']*'"'"'|[^[:space:]]*)[[:space:]]+)*tmux([[:space:]]|$)'

while IFS= read -r f; do
  [ -n "$f" ] && [ -f "$f" ] && [ "$f" != "$SELF" ] && [ "$f" != "$SELF_TEST" ] || continue
  # A file that sources the helper gets the shim: plain tmux in it is private by construction.
  if grep -Eq '^[[:space:]]*(\.|source)[[:space:]]+.*test-tmux\.sh' "$f"; then continue; fi
  n=0
  while IFS= read -r line || [ -n "$line" ]; do
    n=$((n + 1))
    case "$line" in [[:space:]]*'#'*|'#'*) continue ;; esac
    [[ "$line" =~ $CMDPOS ]] || continue
    case "$line" in *'tmux-isolation: ok'*|*' -L '*|*' -S '*|*AW_TMUX*) continue ;; esac
    report "$f" "$n" "$line"
  done < "$f"
done <<< "$files"

while IFS= read -r f; do
  [ -n "$f" ] && [ -f "$f" ] || continue
  n=0
  while IFS= read -r line || [ -n "$line" ]; do
    n=$((n + 1))
    [[ "$line" =~ (FileName[[:space:]]*=[[:space:]]*\"tmux\"|ProcessStartInfo\(\"tmux\"|Process\.Start\(\"tmux\"|CommandRequest\(\[\"tmux\") ]] || continue
    case "$line" in *'tmux-isolation: ok'*|*'"-L"'*) continue ;; esac
    report "$f" "$n" "$line"
  done < "$f"
done <<< "$cs_files"

if [ "$violations" -gt 0 ]; then
  echo "tmux-isolation: $violations tmux call(s) in tests could reach the live server: source scripts/test-tmux.sh and use ttmux, name a private socket with -L, or go through AW_TMUX" >&2
  exit 1
fi
echo "tmux-isolation: ok"
