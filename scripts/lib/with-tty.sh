#!/usr/bin/env bash
# scripts/lib/with-tty.sh — with_tty CMD [ARGS…]: run a command with a pseudo-terminal as its stdin and stdout, the
# way a human's terminal would be, and print its output with the CRs the terminal adds and its control sequences
# stripped (.NET's Console writes ESC[?1h ESC= to a terminal before its first output). Source it; it defines
# functions only and sets no options. Used by scripts/sindri-prove.sh (human mode) and src/Sindri/aot-smoke/run.sh.
#
# - The pty comes from script(1): BSD `script -q /dev/null CMD…` (macOS) or util-linux `script -qec "<quoted CMD>"
#   /dev/null` (Linux). stdout and stderr both go to the pty, so they arrive merged on stdout.
# - Exit status: the command's (BSD script passes it through; util-linux needs -e).
# - stdin: when it is a terminal, script(1) forwards it as typed. Otherwise (a pipe, a file, /dev/null) it is
#   written into the pty only once the command has started, because script(1) drops input that arrives before its
#   child is set up (seen on macOS). Echo is off for that input, so the typed text (and the ^D script(1) sends at
#   end of input) doesn't appear in the output: what's printed is the command's own output, in a stable order.

with_tty() {
  if [ $# -eq 0 ]; then
    echo "with_tty: usage: with_tty CMD [ARGS…]" >&2
    return 2
  fi
  local flavour=bsd
  if script --version 2>/dev/null | grep -q util-linux; then flavour=util-linux; fi

  if [ -t 0 ]; then
    _with_tty_script "$flavour" "$@" | _with_tty_clean
    return "${PIPESTATUS[0]}"
  fi

  local tmp rc
  tmp="$(mktemp -d "${TMPDIR:-/tmp}/with-tty.XXXXXX")" || return 2
  if ! mkfifo "$tmp/ready"; then
    rmdir "$tmp" 2>/dev/null
    return 2
  fi
  # The feeder (left) records its pid, waits until the command's shell opens "ready" (it is running inside the pty
  # by then), then copies stdin into script(1)'s stdin and holds it open 0.2 s more: when the input's end follows
  # its last byte at once, macOS script(1) can deliver the ^D first and the command reads nothing (0 of 20 runs
  # without the pause, 20 of 20 with 0.05 s). script(1)'s stdin must be an anonymous pipe: macOS script refuses a
  # FIFO (tcgetattr fails with EOPNOTSUPP, not ENOTTY). Once script(1) returns, the middle stage stops the feeder
  # and its cat, so a stdin that never ends can't hold the pipeline open; the feeder exits 0 on that TERM, so the shell
  # reports no killed job.
  sh -c 'trap "exit 0" TERM; echo $$ > "$0"; : < "$1"; cat; sleep 0.2' "$tmp/pid" "$tmp/ready" 2>/dev/null |
    {
      _with_tty_script "$flavour" sh -c 'stty -echo 2>/dev/null; : > "$0"; exec "$@"' "$tmp/ready" "$@"
      status=$?
      _with_tty_stop_feeder "$tmp"
      exit "$status"
    } |
    _with_tty_clean
  rc=${PIPESTATUS[1]}
  rm -f "$tmp/ready" "$tmp/pid"
  rmdir "$tmp" 2>/dev/null
  return "$rc"
}

# _with_tty_clean — the output without CRs, CSI sequences (ESC [ … letter) or keypad-mode switches (ESC = and ESC >).
_with_tty_clean() {
  local esc
  esc="$(printf '\033')"
  tr -d '\r' | sed -e "s/${esc}\[[0-9;?]*[A-Za-z]//g" -e "s/${esc}[=>]//g"
}

# _with_tty_stop_feeder TMP — stop the feeder once script(1) has returned (it may still be waiting on stdin).
_with_tty_stop_feeder() {
  local tries=0 pid=""
  while [ -z "$pid" ] && [ "$tries" -lt 20 ]; do
    pid="$(cat "$1/pid" 2>/dev/null)"
    if [ -z "$pid" ]; then
      sleep 0.05
      tries=$((tries + 1))
    fi
  done
  if [ -n "$pid" ]; then
    pkill -P "$pid" 2>/dev/null
    kill "$pid" 2>/dev/null
  fi
  return 0
}

# _with_tty_script FLAVOUR CMD… — script(1) with this platform's arguments; output kept raw.
_with_tty_script() {
  local flavour="$1"; shift
  if [ "$flavour" = util-linux ]; then
    local quoted
    quoted="$(printf '%q ' "$@")"
    script -qec "$quoted" /dev/null
  else
    script -q /dev/null "$@"
  fi
}
