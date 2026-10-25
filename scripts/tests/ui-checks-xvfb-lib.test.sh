#!/usr/bin/env bash
# Tests for tests/ui-checks/xvfb-lib.sh with a FAKE Xvfb on PATH (a script that writes the display's lock file and sleeps): a shell that
# starts the display and exits leaves no Xvfb behind, and an EXIT trap the shell already had still runs.
set -u
unset TMUX
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LIB="$DIR/tests/ui-checks/xvfb-lib.sh"
fail=0
check() { if [ "$2" == "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; fi; }
W="$(mktemp -d)"; trap 'rm -rf "$W"' EXIT
mkdir -p "$W/bin" "$W/locks"
# The fake: Xvfb :<n> ... writes the lock file the real one writes, then sleeps until it is terminated.
cat > "$W/bin/Xvfb" <<'FAKE'
#!/usr/bin/env bash
n="${1#:}"
echo $$ > "$XVFB_LOCK_DIR/.X${n}-lock"
trap 'rm -f "$XVFB_LOCK_DIR/.X${n}-lock"; exit 0' TERM
while :; do sleep 0.2; done
FAKE
chmod +x "$W/bin/Xvfb"
export PATH="$W/bin:$PATH" XVFB_LOCK_DIR="$W/locks"

alive() { if kill -0 "$1" 2>/dev/null; then echo alive; else echo gone; fi; }

# 1. A shell that starts the display and exits stops that Xvfb, and an EXIT trap it already had still runs.
(
  trap 'echo ran > "$W/earlier-trap"' EXIT
  source "$LIB"
  xvfb_start 800x600x24 || exit 3
  echo "$XVFB_PID" > "$W/pid"
  echo "$DISPLAY" > "$W/display"
)
pid="$(cat "$W/pid" 2>/dev/null)"
check "the display was started and exported" "$(cut -c1-2 "$W/display" 2>/dev/null)" ":9"
check "the fake Xvfb is gone after the shell exited" "$(alive "$pid")" "gone"
check "the EXIT trap the shell already had still ran" "$(cat "$W/earlier-trap" 2>/dev/null)" "ran"

# 2. xvfb_stop kills by pid and is safe twice.
( source "$LIB"; xvfb_start || exit 3; echo "$XVFB_PID" > "$W/pid2"; xvfb_stop; xvfb_stop; echo done > "$W/stopped" )
check "xvfb_stop twice is harmless" "$(cat "$W/stopped" 2>/dev/null)" "done"
check "xvfb_stop stopped the Xvfb" "$(alive "$(cat "$W/pid2")")" "gone"

# 3. A display with a lock file is skipped.
echo 1 > "$W/locks/.X90-lock"
( source "$LIB"; xvfb_start || exit 3; echo "$DISPLAY" > "$W/display3" )
check "a locked display is skipped" "$(cat "$W/display3" 2>/dev/null)" ":91"

exit "$fail"
