#!/usr/bin/env bash
# Tests for scripts/ci/tmux-isolation.sh. Runs no tmux: it only writes fixture files and scans them.
set -u
unset TMUX
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$DIR/scripts/ci/tmux-isolation.sh"
fail=0
check() { if [ "$2" == "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; fi; }
W="$(mktemp -d)"; trap 'rm -rf "$W"' EXIT
mkdir -p "$W/scripts/tests" "$W/tests/Aw.X.Tests"

# scan <name> <body>: a one-file fixture tree; prints "<exit>:<number of violation lines>".
scan() {
  rm -f "$W"/scripts/tests/*.sh "$W"/tests/Aw.X.Tests/*.cs
  case "$1" in
    *.sh) printf '%s\n' "$2" > "$W/scripts/tests/$1" ;;
    *.cs) printf '%s\n' "$2" > "$W/tests/Aw.X.Tests/$1" ;;
  esac
  local out rc
  out="$(bash "$CHECK" --root "$W" 2> /dev/null)"; rc=$?
  echo "$rc:$(grep -c "^[^:]*:[0-9]*: " <<< "$out")"
}

check "a bare tmux call is a violation" "$(scan a.test.sh 'tmux kill-server')" "1:1"
check "tmux after && or a subshell is a violation" "$(scan a.test.sh 'cd x && tmux ls')" "1:1"
check "tmux inside a command substitution is a violation" "$(scan a.test.sh 'x="$(tmux list-sessions)"')" "1:1"
check "env tmux is a violation" "$(scan a.test.sh '  env FOO=1 tmux new-session -d')" "1:1"
check "command tmux is a violation" "$(scan a.test.sh 'command tmux ls')" "1:1"
check "tmux with -L is allowed" "$(scan a.test.sh 'tmux -L t1-private ls')" "0:0"
check "tmux with -S is allowed (a private path the caller owns)" "$(scan a.test.sh 'tmux -S "$W/sock" ls')" "0:0"
check "ttmux is allowed" "$(scan a.test.sh 'ttmux new-session -d -s x')" "0:0"
check "a line with AW_TMUX is allowed" "$(scan a.test.sh 'AW_TMUX="$stub" tmux ls')" "0:0"
check "a waiver comment is allowed" "$(scan a.test.sh 'tmux -V  # tmux-isolation: ok (version only, no server)')" "0:0"
check "a comment line is skipped" "$(scan a.test.sh '# tmux ls would be bad')" "0:0"
check "a package name in a string is not a call" "$(scan a.test.sh 'echo "brew install tmux"')" "0:0"
check "a file that sources scripts/test-tmux.sh may call plain tmux" "$(scan a.test.sh '. "$DIR/test-tmux.sh" || exit 2
tmux new-session -d -s x')" "0:0"
check "the source form of the helper counts too" "$(scan a.test.sh 'source "$DIR/scripts/test-tmux.sh"
tmux ls')" "0:0"
check "a file that only mentions the helper in a comment does not" "$(scan a.test.sh '# see scripts/test-tmux.sh
tmux ls')" "1:1"
check "C# starting tmux is a violation" "$(scan ATests.cs 'var p = Process.Start("tmux", "ls");')" "1:1"
check "C# FileName tmux is a violation" "$(scan ATests.cs 'var i = new ProcessStartInfo { FileName = "tmux" };')" "1:1"
check "C# CommandRequest tmux without -L is a violation" "$(scan ATests.cs 'new CommandRequest(["tmux", "ls"], t)')" "1:1"
check "C# CommandRequest tmux with -L is allowed" "$(scan ATests.cs 'new CommandRequest(["tmux", "-L", "t1", "ls"], t)')" "0:0"
check "C# strings that only mention tmux are fine" "$(scan ATests.cs 'Assert.Equal("tmux send-keys -t x", line);')" "0:0"
check "the C# waiver is allowed" "$(scan ATests.cs 'Process.Start("tmux", "-V"); // tmux-isolation: ok (version only)')" "0:0"
check "the real tree is clean" "$(bash "$CHECK" > /dev/null 2>&1; echo $?)" "0"
check "an unknown flag is a usage error" "$(bash "$CHECK" --nope > /dev/null 2>&1; echo $?)" "2"
exit "$fail"
