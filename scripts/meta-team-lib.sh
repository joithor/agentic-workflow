#!/usr/bin/env bash
# meta-team-lib.sh — sourced by meta-launch.sh, meta-lane.sh and install-meta.sh: which team's config and state directory a script
# works on. Named teams live in $AW_STATE_DIR/meta/teams/<team>/ (team.json, meta.db, lane-sessions/, gates/); the legacy flat layout
# ($AW_STATE_DIR/meta/team.json) keeps working when no team is selected, or when the selected name is the legacy config's own team.
# The same rules as aw-meta's own (src/Meta/Aw.Meta.Infrastructure/TeamResolver.cs).
#   meta_resolve <state_dir> [team]   sets META_DIR (the team's state directory) and META_CONFIG (its team.json), and returns 0;
#                                     returns 2 with a message on stderr when several teams exist and none is selected, or the name is bad.
# AW_META_CONFIG, when set, still overrides META_CONFIG (the callers apply it); META_DIR is unaffected by it.
meta_resolve() {
  local state="$1" team="${2:-}" root legacy_team named found=() d
  root="$state/meta"
  META_DIR="$root"; META_CONFIG="$root/team.json"
  if [ -n "$team" ]; then
    case "$team" in *[!a-z0-9-]*|-*) echo "meta: bad team name '$team' (lowercase letters, digits, dashes)" >&2; return 2 ;; esac
    named="$root/teams/$team"
    if [ ! -d "$named" ] && [ -f "$root/team.json" ]; then
      legacy_team="$(jq -r 'if (.team | type) == "string" then .team else empty end' "$root/team.json" 2> /dev/null || true)"
      [ "$legacy_team" = "$team" ] && return 0
    fi
    META_DIR="$named"; META_CONFIG="$named/team.json"; return 0
  fi
  [ -f "$root/team.json" ] && return 0
  # only valid names count, in byte order, as TeamResolver.Named does
  for d in $(LC_ALL=C ls -1 "$root/teams" 2> /dev/null); do
    case "$d" in *[!a-z0-9-]*|-*|"") continue ;; esac
    [ -f "$root/teams/$d/team.json" ] && found+=("$d")
  done
  case "${#found[@]}" in
    0) return 0 ;;
    1) META_DIR="$root/teams/${found[0]}"; META_CONFIG="$META_DIR/team.json"; return 0 ;;
    *) echo "meta: several teams exist (${found[*]}) and none is selected; pass --team <name> or set AW_META_TEAM" >&2; return 2 ;;
  esac
}
