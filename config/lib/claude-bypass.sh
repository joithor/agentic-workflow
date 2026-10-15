#!/usr/bin/env bash
# claude-bypass.sh — the owner's permission mode (2026-10-14): every Claude Code session starts in bypassPermissions,
# so all sessions on the box run in the same mode and messages between them are never held for approval (a session
# holds a peer's message when the two modes differ). Sourced by setup.sh.
#
#   aw_claude_bypass <settings.json>   set permissions.defaultMode = "bypassPermissions", touching no other key
#   AW_NO_CLAUDE_BYPASS=1              skip (opt out)
#   AW_DRY_RUN=1                       print, write nothing
#
# A fresh install gets the key from config/settings.json; an existing settings.json (which setup.sh otherwise never
# overwrites) gets only this key. A file jq can't parse is left alone with a warning.
aw_claude_bypass() {
  local settings="$1" tmp
  [ "${AW_NO_CLAUDE_BYPASS:-0}" = 1 ] && { echo "  permission mode: skipped (AW_NO_CLAUDE_BYPASS=1)"; return 0; }
  [ -f "$settings" ] || return 0
  if [ "$(jq -r '.permissions.defaultMode // empty' "$settings" 2> /dev/null)" = bypassPermissions ]; then
    echo "  permission mode: bypassPermissions (already set)"
    return 0
  fi
  if [ "${AW_DRY_RUN:-0}" = 1 ]; then
    echo "  [dry-run] would set permissions.defaultMode = bypassPermissions in $settings"
    return 0
  fi
  tmp="$(mktemp "$settings.aw.XXXXXX")" || return 1
  if ! jq '.permissions = ((.permissions // {}) + {defaultMode: "bypassPermissions"})' "$settings" > "$tmp" 2> /dev/null; then
    unlink "$tmp"
    echo "  WARN: $settings isn't valid JSON; permission mode not set" >&2
    return 0
  fi
  cat "$tmp" > "$settings"   # write through, so a symlinked settings file keeps its link
  unlink "$tmp"
  echo "  permission mode: bypassPermissions set in $settings"
}
