#!/usr/bin/env bash
# scripts/ci/bash-tests.sh — run every tracked bash test suite (`git ls-files '*.test.sh'`),
# each in its own process, and fail if any of them fails. Prints one PASS/FAIL line
# per suite and a summary. The list comes from git, so a new suite can't be missed
# by a glob; a suite is skipped only through EXCLUDED below, with its reason.
#
# Usage: scripts/ci/bash-tests.sh              run the suites
#        scripts/ci/bash-tests.sh --list       print the suites it would run
#        scripts/ci/bash-tests.sh --excluded   print "path<TAB>reason" per exclusion
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT" || exit 2

# path<TAB>reason. Keep this short: every entry is a suite CI does not run.
EXCLUDED=(
  "config/tests/statusline-judge.test.sh	stale on main: it extracts only judge_segment() from statusline.sh, which now calls judge_health(), so all 3 cases fail; config/lib/tests/statusline.test.sh covers the judge column"
)

excluded_paths() {
  local entry
  for entry in "${EXCLUDED[@]}"; do printf '%s\n' "${entry%%	*}"; done
}

suites() {
  git ls-files '*.test.sh' | grep -vxF -f <(excluded_paths)
}

case "${1:-}" in
  --list) suites; exit 0 ;;
  --excluded) printf '%s\n' "${EXCLUDED[@]}"; exit 0 ;;
  "") ;;
  *) echo "usage: $0 [--list|--excluded]" >&2; exit 2 ;;
esac

failed=()
count=0
while IFS= read -r suite; do
  count=$((count + 1))
  log="$(mktemp)"
  start=$SECONDS
  if bash "$suite" >"$log" 2>&1 </dev/null; then
    echo "PASS  $suite ($((SECONDS - start))s)"
  else
    rc=$?
    echo "FAIL  $suite (exit $rc, $((SECONDS - start))s)"
    # The failing assertions first (a long suite's tail can hide them), then the end of the log.
    grep -E 'FAIL|not ok' "$log" | sed 's/^/      ! /' | head -20
    sed 's/^/      | /' "$log" | tail -25
    failed+=("$suite")
  fi
  rm -f "$log"
done < <(suites)

echo
echo "bash suites: $count run, ${#failed[@]} failed, ${#EXCLUDED[@]} excluded"
for entry in "${EXCLUDED[@]}"; do printf '  excluded: %s\n' "${entry%%	*}"; done
if [ "${#failed[@]}" -gt 0 ]; then
  printf '  failed: %s\n' "${failed[@]}"
  exit 1
fi
