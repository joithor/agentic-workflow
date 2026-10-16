#!/usr/bin/env bash
# aw:meta-outbox — PostToolUse hook, matcher SendMessage (Claude Code only). Hands every sent message to
# `aw-meta outbox-record`, which closes the obligations it answers (a reply to a peer, a lane-map broadcast, a "main
# moved" announcement, an owner answer). aw-meta ignores sessions other than the registered meta. Never blocks:
# always exits 0 and prints nothing; a failure is logged as a fail-open.
set -uo pipefail
failopen() { :; }   # baseline: the hook without its fail-open logging
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0
# The team: AW_META_TEAM when the lane launcher set it; else the tmux socket the session runs on (the launcher names it after the
# team), taken only when that team exists on disk (a named team, or the legacy config's own team). Anything else: no team selected.
if [ -z "${AW_META_TEAM:-}" ] && [ -n "${TMUX:-}" ]; then
  sock="${TMUX%%,*}"; sock="${sock##*/}"
  mroot="${AW_STATE_DIR:-$HOME/.agentic-workflow}/meta"
  if [[ "$sock" =~ ^[a-z0-9][a-z0-9-]*$ ]] && { [ -d "$mroot/teams/$sock" ] || [ "$(jq -r '.team // empty' "$mroot/team.json" 2> /dev/null)" = "$sock" ]; }; then
    export AW_META_TEAM="$sock"
  fi
fi
META="${AW_META_BIN:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/bin/aw-meta}"
[ -x "$META" ] || exit 0
INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }
printf '%s' "$INPUT" | AW_LOG_LEVEL=none "$META" outbox-record > /dev/null 2>&1
rc=$?
[ "$rc" = 0 ] || failopen "meta-exit-$rc"
exit 0
