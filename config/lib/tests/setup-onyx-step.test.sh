#!/usr/bin/env bash
# aw_setup_onyx_finish (config/lib/setup-onyx-step.sh): 0 and 3 end setup cleanly; every other code fails setup with the message,
# including the codes a script abort gives (2, 126, 127), which an `= 1` check used to swallow.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../setup-onyx-step.sh
source "$DIR/../setup-onyx-step.sh"

for rc in 0 3; do
  out="$(aw_setup_onyx_finish "$rc" "RUN-LATER" 2>&1)" || { echo "FAIL: rc $rc should pass"; exit 1; }
  [ -z "$out" ] || { echo "FAIL: rc $rc printed: $out"; exit 1; }
done
for rc in 1 2 126 127; do
  got=0
  out="$(aw_setup_onyx_finish "$rc" "RUN-LATER" 2>&1)" || got=$?
  [ "$got" = "$rc" ] || { echo "FAIL: rc $rc returned $got"; exit 1; }
  grep -q "exit $rc" <<<"$out" && grep -q "RUN-LATER" <<<"$out" || { echo "FAIL: message for $rc: $out"; exit 1; }
done
echo "setup-onyx-step.test.sh: all passed"
