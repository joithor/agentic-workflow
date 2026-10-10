#!/usr/bin/env bash
# scripts/ci/bash-tests.sh — run every bash test suite the AGENTS.md merge gate
# lists (providers, scripts, hooks, config/lib), each in its own process, and
# fail if any of them fails. Prints one PASS/FAIL line per suite and a summary.
#
# Usage: scripts/ci/bash-tests.sh            (from anywhere inside the repo)
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT" || exit 2

failed=()
count=0
for suite in providers/tests/*.test.sh scripts/tests/*.test.sh \
             config/hooks/tests/*.test.sh config/lib/tests/*.test.sh; do
  [ -f "$suite" ] || continue
  count=$((count + 1))
  log="$(mktemp)"
  start=$SECONDS
  if bash "$suite" >"$log" 2>&1; then
    echo "PASS  $suite ($((SECONDS - start))s)"
  else
    rc=$?
    echo "FAIL  $suite (exit $rc, $((SECONDS - start))s)"
    # The failing assertions first (a long suite's tail can hide them), then the end of the log.
    grep -E 'FAIL' "$log" | sed 's/^/      ! /' | head -20
    sed 's/^/      | /' "$log" | tail -25
    failed+=("$suite")
  fi
  rm -f "$log"
done

echo
echo "bash suites: $count run, ${#failed[@]} failed"
if [ "${#failed[@]}" -gt 0 ]; then
  printf '  failed: %s\n' "${failed[@]}"
  exit 1
fi
