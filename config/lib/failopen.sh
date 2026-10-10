#!/usr/bin/env bash
# config/lib/failopen.sh — HOOK-FAILOPEN logging for hooks that fail open
# (spec docs/superpowers/specs/2026-10-09-agent-readiness-design.md §4c, B5).
#
# Sourced, never executed. bash 3.2 compatible. A hook calls
#   aw_failopen <hook> <cause>
# right before it allows (or lets through) because of its own internal error:
# judge missing or erroring, input it can't read, unreadable state. One JSON
# line goes to ${AW_STATE_DIR:-$HOME/.agentic-workflow}/hooks/failopen.log:
#   {"ts":"…Z","event":"HOOK-FAILOPEN","hook":"send-gate","cause":"judge-exit-127","provider":"claude"}
# The scorer's daily report lists them (scorer/src/hook-failopen.ts).
#
# `aw_failopen <hook> input <raw-stdin>` classifies an unreadable payload as
# empty-input, jq-missing or input-unparsable, and logs nothing for a readable
# JSON object (a valid payload that simply has nothing to check).
#
# Logging only: it never prints, never fails and never changes the caller's
# decision. Payload content is never written — only the hook name and a cause
# built from fixed words, both reduced to [A-Za-z0-9._:-].

aw_failopen() {
  local hook="${1:-unknown}" cause="${2:-unknown}" provider="${AW_PROVIDER:-claude}" dir ts
  if [ "$cause" = "input" ]; then
    if [ -z "${3-}" ]; then
      cause="empty-input"
    elif ! command -v jq > /dev/null 2>&1; then
      cause="jq-missing"
    elif ! printf '%s' "$3" | jq -e 'type == "object"' > /dev/null 2>&1; then
      cause="input-unparsable"
    else
      return 0
    fi
  fi
  hook="${hook//[^A-Za-z0-9._:-]/_}"
  cause="${cause//[^A-Za-z0-9._:-]/_}"
  cause="${cause:0:80}"
  provider="${provider//[^A-Za-z0-9._:-]/_}"
  dir="${AW_STATE_DIR:-$HOME/.agentic-workflow}/hooks"
  ts="$(date -u +%Y-%m-%dT%H:%M:%SZ 2> /dev/null)"
  {
    mkdir -p "$dir" &&
      printf '{"ts":"%s","event":"HOOK-FAILOPEN","hook":"%s","cause":"%s","provider":"%s"}\n' \
        "$ts" "$hook" "$cause" "$provider" >> "$dir/failopen.log"
  } 2> /dev/null || true
  return 0
}
