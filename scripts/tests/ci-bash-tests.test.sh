#!/usr/bin/env bash
# Tests for scripts/ci/bash-tests.sh — the CI bash-suite runner. Its suite list must
# be every tracked *.test.sh, minus excluded ones that each carry a reason, so a new
# suite can't be left out of CI by a glob that didn't match it.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUNNER="$ROOT/scripts/ci/bash-tests.sh"

fail() { echo "FAIL: $*"; exit 1; }

test_lists_every_tracked_suite_except_the_excluded() {
  local listed tracked excluded
  listed="$(bash "$RUNNER" --list)"
  excluded="$(bash "$RUNNER" --excluded | cut -f1)"
  tracked="$(git -C "$ROOT" ls-files '*.test.sh')"
  local want
  want="$(comm -23 <(printf '%s\n' "$tracked" | sort) <(printf '%s\n' "$excluded" | sed '/^$/d' | sort))"
  [ "$(printf '%s\n' "$listed" | sort)" = "$want" ] \
    || fail "runner list differs from tracked suites minus exclusions:
$(diff <(printf '%s\n' "$listed" | sort) <(printf '%s\n' "$want") || true)"
  echo "PASS: test_lists_every_tracked_suite_except_the_excluded"
}

test_suites_outside_the_old_globs_are_listed() {
  bash "$RUNNER" --list | grep -qx 'skills/ui-evidence/scripts/lib/tests/locks.test.sh' \
    || fail "a nested skill suite is not run"
  echo "PASS: test_suites_outside_the_old_globs_are_listed"
}

test_every_exclusion_is_tracked_and_has_a_reason() {
  local path reason
  while IFS=$'\t' read -r path reason; do
    [ -n "$path" ] || continue
    git -C "$ROOT" ls-files --error-unmatch "$path" >/dev/null 2>&1 || fail "excluded suite is not tracked: $path"
    [ "${#reason}" -ge 20 ] || fail "exclusion needs a reason: $path"
  done < <(bash "$RUNNER" --excluded)
  echo "PASS: test_every_exclusion_is_tracked_and_has_a_reason"
}

test_lists_every_tracked_suite_except_the_excluded
test_suites_outside_the_old_globs_are_listed
test_every_exclusion_is_tracked_and_has_a_reason
