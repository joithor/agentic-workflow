#!/usr/bin/env bash
# The team's start lock for lanes whose agent reports its own session id (sourced by meta-lane.sh; tested by
# scripts/tests/meta-launch.test.sh). The lock is a symlink, $start_lock, whose target is the owner's pid: `ln -s` is atomic and
# records the owner in one step, on macOS (no flock there) and Linux alike. A lock whose owner pid is dead is taken over, so a
# crashed start never leaves a stale one.
# Needs: start_lock (the symlink path), lane_pid (the owner), lane (a name for messages) and AW_META_ID_WAIT (optional).
# start_lock_hook <point> is a test seam, a no-op here: "checked" runs once a dead owner was seen, "gap" while a taker holds the
# lock's slot empty.
start_lock_hook() { :; }
release_start_lock() { [ "$(readlink "$start_lock" 2> /dev/null)" = "$lane_pid" ] && unlink "$start_lock" 2> /dev/null; return 0; }
acquire_start_lock() { # returns 1 if the holder did not finish in AW_META_ID_WAIT x 10 s
  local deadline=$((SECONDS + ${AW_META_ID_WAIT:-30} * 10)) owner moved
  while ! ln -s "$lane_pid" "$start_lock" 2> /dev/null; do
    owner="$(readlink "$start_lock" 2> /dev/null)"
    if [ -n "$owner" ] && ! kill -0 "$owner" 2> /dev/null; then   # the holder died: take the lock over (the rename wins for one taker)
      start_lock_hook checked
      moved="$start_lock.dead.$$"
      if mv "$start_lock" "$moved" 2> /dev/null; then
        start_lock_hook gap
        # a taker that got in between our check and the rename moved a live lock: put it back
        [ "$(readlink "$moved")" = "$owner" ] || ln -s "$(readlink "$moved")" "$start_lock" 2> /dev/null
        unlink "$moved" 2> /dev/null
      fi
      continue
    fi
    if [ "$SECONDS" -ge "$deadline" ]; then
      echo "meta-lane: another lane's start did not finish; starting $lane anyway" >&2
      return 1
    fi
    sleep 0.2
  done
  return 0
}
