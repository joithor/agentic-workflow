#!/usr/bin/env bash
# aw:meta-outbox — PostToolUse hook, matcher SendMessage (Claude Code only). Hands every sent message to
# `aw-meta outbox-record`, which closes the obligations it answers (a reply to a peer, a lane-map broadcast, a "main
# moved" announcement, an owner answer). aw-meta ignores sessions other than the registered meta. Never blocks:
# always exits 0 and prints nothing; a failure is logged as a fail-open.
set -uo pipefail
failopen() { :; }   # baseline: the hook without its fail-open logging
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0
META="${AW_META_BIN:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/bin/aw-meta}"
[ -x "$META" ] || exit 0
INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }
printf '%s' "$INPUT" | AW_LOG_LEVEL=none "$META" outbox-record > /dev/null 2>&1
rc=$?
[ "$rc" = 0 ] || failopen "meta-exit-$rc"
exit 0
