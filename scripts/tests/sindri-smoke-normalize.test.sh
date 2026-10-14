#!/usr/bin/env bash
# The Sindri host smoke's normalizer (src/Sindri/aot-smoke/normalize.sh): a captured transcript normalizes to what
# expected.txt pins, whether or not the heartbeat has ticked before 'sindri status' runs.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
N="$ROOT/src/Sindri/aot-smoke/normalize.sh"
fail=0
check() { # name input expected
  local got; got="$(printf '%s\n' "$2" | bash "$N" /tmp/smoke-work /tmp/smoke-root)"
  if [ "$got" = "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; printf '  expected: %s\n  got:      %s\n' "$3" "$got"; fail=1; fi
}
check "status before the first heartbeat" "  services  heartbeat every 5s" "  services  heartbeat every 5s"
check "status after the first heartbeat (the (last …) suffix is dropped)" "  services  heartbeat every 5s (last 12:34:56Z)" "  services  heartbeat every 5s"
check "the home path is WORK" "  profile   /tmp/smoke-work/home/profile/profile.yaml (approved, hash 90f90d)" "  profile   WORK/home/profile/profile.yaml (approved, hash HASH6)"
check "the pid is a fixed word" "  lease     held by pid 4242, epoch 1" "  lease     held by pid PID, epoch 1"
check "the port is a fixed word" "  api       127.0.0.1:60237" "  api       127.0.0.1:PORT"
check "doctor's claude-code line is dropped" "$(printf 'ok    claude-code re-probed: the markers are set, version 2.1.296\nwarn  cursor-agent pending: owner probe')" "warn  cursor-agent pending: owner probe"
[ "$fail" -eq 0 ] && echo "PASS: sindri-smoke-normalize tests" || { echo "FAIL: sindri-smoke-normalize tests"; exit 1; }
