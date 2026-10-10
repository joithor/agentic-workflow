#!/usr/bin/env bash
# config/lib/failopen.sh — HOOK-FAILOPEN logging for hooks that fail open
# (spec docs/superpowers/specs/2026-10-09-agent-readiness-design.md §4c, B5).
#
# Sourced, never executed. bash 3.2 compatible. A hook calls
#   aw_failopen <hook> <cause>
# right before it allows (or lets through) because of its own internal error:
# judge missing or erroring, input it can't read, unreadable state. One JSON
# line goes to ${AW_STATE_DIR:-$HOME/.agentic-workflow}/logs/failopen.log:
#   {"ts":"…Z","event":"HOOK-FAILOPEN","hook":"send-gate","cause":"judge-exit-127","provider":"claude"}
# The scorer's daily report lists them (scorer/src/hook-failopen.ts).
#
# `aw_failopen <hook> input <raw-stdin> [jq-predicate]` classifies a payload
# the hook couldn't use: empty-input, jq-missing or input-unparsable; and,
# when the predicate holds (the payload names the hook's own tool), field-missing.
# It logs nothing for any other readable JSON object.
#
# Logging only: it never prints, never fails and never changes the caller's
# decision. Hooks still call it from a subshell (see their failopen() wrapper)
# so the caller's set -e/-u can't reach it. Payload content is never written:
# only the hook name and a cause built from fixed words, both reduced to
# [A-Za-z0-9._:-]. With neither AW_STATE_DIR nor HOME set it logs nothing.
# A symlinked log is never followed; the log rotates to failopen.log.1 past
# AW_FAILOPEN_MAX_BYTES (default 1 MiB).

aw_failopen() {
  local hook="${1:-unknown}" cause="${2:-unknown}" provider="${AW_PROVIDER:-claude}" dir log ts size
  if [ "$cause" = "input" ]; then
    if [ -z "${3-}" ]; then
      cause="empty-input"
    elif ! command -v jq > /dev/null 2>&1; then
      cause="jq-missing"
    elif ! printf '%s' "$3" | jq -e 'type == "object"' > /dev/null 2>&1; then
      cause="input-unparsable"
    elif [ -n "${4-}" ] && printf '%s' "$3" | jq -e "$4" > /dev/null 2>&1; then
      cause="field-missing"
    else
      return 0
    fi
  fi
  if [ -n "${AW_STATE_DIR:-}" ]; then
    dir="$AW_STATE_DIR/logs"
  elif [ -n "${HOME:-}" ]; then
    dir="$HOME/.agentic-workflow/logs"
  else
    return 0
  fi
  log="$dir/failopen.log"
  [ -L "$log" ] && return 0
  hook="${hook//[^A-Za-z0-9._:-]/_}"
  cause="${cause//[^A-Za-z0-9._:-]/_}"
  cause="${cause:0:80}"
  provider="${provider//[^A-Za-z0-9._:-]/_}"
  ts="$(date -u +%Y-%m-%dT%H:%M:%SZ 2> /dev/null)" || ts=""
  (
    umask 077
    mkdir -p "$dir" || exit 0
    size="$(wc -c < "$log" 2> /dev/null)" || size=0
    [ "${size// /}" -gt "${AW_FAILOPEN_MAX_BYTES:-1048576}" ] 2> /dev/null && mv -f "$log" "$log.1"
    printf '{"ts":"%s","event":"HOOK-FAILOPEN","hook":"%s","cause":"%s","provider":"%s"}\n' \
      "$ts" "$hook" "$cause" "$provider" >> "$log"
  ) > /dev/null 2>&1 || true
  return 0
}
