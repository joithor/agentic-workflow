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
check "tmux after if is a violation" "$(scan a.test.sh 'if tmux has-session -t x; then echo y; fi')" "1:1"
check "tmux after while is a violation" "$(scan a.test.sh 'while tmux ls; do sleep 1; done')" "1:1"
check "tmux after until is a violation" "$(scan a.test.sh 'until tmux ls; do sleep 1; done')" "1:1"
check "tmux after ! is a violation" "$(scan a.test.sh '! tmux ls')" "1:1"
check "tmux after then is a violation" "$(scan a.test.sh 'then tmux ls')" "1:1"
check "tmux after do is a violation" "$(scan a.test.sh 'do tmux ls')" "1:1"
check "tmux after else is a violation" "$(scan a.test.sh 'else tmux ls')" "1:1"
check "tmux inside braces is a violation" "$(scan a.test.sh '{ tmux ls; }')" "1:1"
check "timeout tmux is a violation" "$(scan a.test.sh 'timeout 5 tmux ls')" "1:1"
check "timeout with a flag and tmux is a violation" "$(scan a.test.sh 'timeout -k 1 5 tmux ls')" "1:1"
check "a VAR= prefix does not hide tmux" "$(scan a.test.sh 'TMUX= tmux ls')" "1:1"
check "several prefixes do not hide tmux" "$(scan a.test.sh 'A=1 B=2 tmux ls')" "1:1"
check "if with -L is allowed" "$(scan a.test.sh 'if tmux -L t1-private has-session -t x; then :; fi')" "0:0"
check "tmux mentioned as an argument is not a call" "$(scan a.test.sh 'if command -v tmux > /dev/null; then :; fi')" "0:0"
check "a quoted VAR=value prefix does not hide tmux" "$(scan a.test.sh 'X="$(date)" tmux ls')" "1:1"
check "a single-quoted VAR prefix does not hide tmux" "$(scan a.test.sh "X='a b' tmux ls")" "1:1"
check "sudo -u x tmux is a violation" "$(scan a.test.sh 'sudo -u x tmux ls')" "1:1"
check "sudo -E tmux is a violation" "$(scan a.test.sh 'sudo -E tmux ls')" "1:1"
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
# an ignored nested directory is not scanned inside a git checkout; outside one, .claude/worktrees is skipped
G="$(mktemp -d)"; N="$(mktemp -d)"; trap 'rm -rf "$W" "$G" "$N"' EXIT
git -C "$G" init -q 2> /dev/null && mkdir -p "$G/scripts/tests" "$G/other/scripts/tests" && printf 'other/\n' > "$G/.gitignore" \
  && printf 'tmux ls\n' > "$G/other/scripts/tests/bad.test.sh" && printf 'echo ok\n' > "$G/scripts/tests/good.test.sh"
check "a nested ignored directory with a bad tmux line is not flagged (git)" "$(bash "$CHECK" --root "$G" > /dev/null 2>&1; echo $?)" "0"
printf 'tmux ls\n' > "$G/scripts/tests/bad.test.sh"
check "an untracked, unignored bad file is flagged (git)" "$(bash "$CHECK" --root "$G" > /dev/null 2>&1; echo $?)" "1"
mkdir -p "$N/.claude/worktrees/lane/scripts/tests" && printf 'tmux ls\n' > "$N/.claude/worktrees/lane/scripts/tests/bad.test.sh"
check "outside git, files under .claude/worktrees are skipped" "$(bash "$CHECK" --root "$N" > /dev/null 2>&1; echo $?)" "0"
check "the real tree is clean" "$(bash "$CHECK" > /dev/null 2>&1; echo $?)" "0"
check "an unknown flag is a usage error" "$(bash "$CHECK" --nope > /dev/null 2>&1; echo $?)" "2"
exit "$fail"
