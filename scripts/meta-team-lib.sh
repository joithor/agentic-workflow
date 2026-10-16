#!/usr/bin/env bash
# meta-team-lib.sh — sourced by meta-launch.sh, meta-lane.sh and install-meta.sh: which team's config and state directory a script
# works on. Named teams live in $AW_STATE_DIR/meta/teams/<team>/ (team.json, meta.db, lane-sessions/, gates/); the legacy flat layout
# ($AW_STATE_DIR/meta/team.json) keeps working when no team is selected, or when the selected name is the legacy config's own team.
# The same rules as aw-meta's own (src/Meta/Aw.Meta.Infrastructure/TeamResolver.cs).
#   meta_resolve <state_dir> [team]   sets META_DIR (the team's state directory) and META_CONFIG (its team.json), and returns 0;
#                                     returns 2 with a message on stderr when several teams exist and none is selected, or the name is bad.
# AW_META_CONFIG, when set, still overrides META_CONFIG through meta_pick_config; META_DIR is unaffected by it.
meta_resolve() {
  local state="$1" team="${2:-}" root legacy_team named found=() d
  root="$state/meta"
  META_DIR="$root"; META_CONFIG="$root/team.json"
  if [ -n "$team" ]; then
    case "$team" in *[!a-z0-9-]*|-*) echo "meta: bad team name '$team' (lowercase letters, digits, dashes)" >&2; return 2 ;; esac
    named="$root/teams/$team"
    if [ ! -f "$named/team.json" ] && [ -f "$root/team.json" ]; then
      legacy_team="$(jq -r 'if (.team | type) == "string" then .team else empty end' "$root/team.json" 2> /dev/null || true)"
      [ "$legacy_team" = "$team" ] && return 0
    fi
    META_DIR="$named"; META_CONFIG="$named/team.json"; return 0
  fi
  [ -f "$root/team.json" ] && return 0
  # only valid names count, in byte order (the glob is sorted by the C locale), as TeamResolver.Named does
  local p LC_COLLATE=C
  for p in "$root"/teams/*/; do
    d="${p%/}"; d="${d##*/}"
    case "$d" in *[!a-z0-9-]*|-*|"") continue ;; esac
    [ -f "$p/team.json" ] && found+=("$d")
  done
  case "${#found[@]}" in
    0) return 0 ;;
    1) META_DIR="$root/teams/${found[0]}"; META_CONFIG="$META_DIR/team.json"; return 0 ;;
    *) echo "meta: several teams exist (${found[*]}) and none is selected; pass --team <name> or set AW_META_TEAM" >&2; return 2 ;;
  esac
}

# meta_pick_config <team>   prints the team config path: AW_META_CONFIG when set and it is for this team (its .team), else META_CONFIG.
# A lane's own AW_META_CONFIG must not carry over when the script is run for another team (same rule as aw-meta's settings).
meta_pick_config() {
  local team="${1:-}" override="${AW_META_CONFIG:-}" cfg_team
  if [ -n "$override" ]; then
    cfg_team="$(jq -r 'if (.team | type) == "string" then .team else empty end' "$override" 2> /dev/null || true)"
    if [ -z "$team" ] || [ "$cfg_team" = "$team" ]; then printf '%s' "$override"; return 0; fi
  fi
  printf '%s' "$META_CONFIG"
}
