#!/usr/bin/env bash
# Tests for scripts/ci/bash-tests.sh — the CI bash-suite runner. Its suite list must
# be every tracked *.test.sh, minus excluded ones that each carry a reason, so a new
# suite can't be left out of CI by a glob that didn't match it. Independent suites run
# in a parallel pool; the timing-sensitive ones (SERIAL, each with a reason) run one at
# a time after the pool, alone on the machine.
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

test_every_serial_suite_is_listed_and_has_a_reason() {
  local path reason listed n=0
  listed="$(bash "$RUNNER" --list)"
  while IFS=$'\t' read -r path reason; do
    [ -n "$path" ] || continue
    n=$((n + 1))
    printf '%s\n' "$listed" | grep -qxF "$path" || fail "serial suite is not one the runner runs: $path"
    [ "${#reason}" -ge 20 ] || fail "serial suite needs a reason: $path"
  done < <(bash "$RUNNER" --serial)
  [ "$n" -gt 0 ] || fail "no serial suites declared"
  echo "PASS: test_every_serial_suite_is_listed_and_has_a_reason"
}

test_the_timing_sensitive_suites_are_serial() {
  local serial s
  serial="$(bash "$RUNNER" --serial | cut -f1)"
  for s in scripts/tests/agent-token.test.sh config/hooks/tests/failopen.test.sh config/lib/tests/failopen.test.sh \
           config/lib/tests/statusline.test.sh config/hooks/tests/sindri-nudge.test.sh config/lib/tests/prompt-sort.test.sh \
           scripts/tests/readiness-baseline.test.sh; do
    printf '%s\n' "$serial" | grep -qxF "$s" || fail "timing-sensitive suite is not serial: $s"
  done
  echo "PASS: test_the_timing_sensitive_suites_are_serial"
}

# A fixture repo whose suites record when they run. "active-<name>" marker dirs show which
# suites overlap: a parallel suite waits (bounded) until its partner is active too, which
# proves the pool runs them at the same time; a serial suite records any other active
# suite as an overlap.
make_fixture() {
  local d="$1"
  git -C "$d" init -q
  local par='set -u; mkdir "$MARK/active-$1"; i=0
while [ ! -d "$MARK/active-$2" ] && [ ! -e "$MARK/done-$2" ] && [ $i -lt 100 ]; do sleep 0.05; i=$((i + 1)); done
[ -d "$MARK/active-$2" ] && touch "$MARK/saw-$1-$2"
sleep 0.2; touch "$MARK/done-$1"; rmdir "$MARK/active-$1"'
  local ser='set -u; mkdir "$MARK/active-$1"
for a in "$MARK"/active-*; do [ "$a" = "$MARK/active-$1" ] || echo "$1 overlapped ${a##*/active-}" >> "$MARK/overlaps"; done
sleep 0.3
for a in "$MARK"/active-*; do [ "$a" = "$MARK/active-$1" ] || echo "$1 overlapped ${a##*/active-}" >> "$MARK/overlaps"; done
rmdir "$MARK/active-$1"'
  mkdir -p "$d/t"
  printf '%s\n' "set -- p1 p2" "$par" > "$d/t/p1.test.sh"
  printf '%s\n' "set -- p2 p1" "$par" > "$d/t/p2.test.sh"
  printf '%s\n' "set -- s1" "$ser" > "$d/t/s1.test.sh"
  printf '%s\n' "set -- s2" "$ser" > "$d/t/s2.test.sh"
  printf '%s\n' 'echo "should never run"; exit 1' > "$d/t/skipped.test.sh"
  git -C "$d" add t
}

FIXTURE_SERIAL=$'t/s1.test.sh\tfixture: serial because it records overlaps with any other suite\nt/s2.test.sh\tfixture: serial because it records overlaps with any other suite'
FIXTURE_EXCLUDED=$'t/skipped.test.sh\tfixture: excluded, and it fails if it ever runs at all'

run_fixture() { # <fixture dir> <mark dir> [runner args...]
  local d="$1" m="$2"; shift 2
  MARK="$m" BASH_TESTS_ROOT="$d" BASH_TESTS_JOBS=4 \
    BASH_TESTS_SERIAL="$FIXTURE_SERIAL" BASH_TESTS_EXCLUDED="$FIXTURE_EXCLUDED" \
    bash "$RUNNER" "$@"
}

test_pool_runs_in_parallel_and_serial_suites_run_alone() {
  local d m out rc=0
  d="$(mktemp -d)"; m="$(mktemp -d)"
  make_fixture "$d"
  out="$(run_fixture "$d" "$m" 2>&1)" || rc=$?
  [ "$rc" -eq 0 ] || fail "fixture run exited $rc:
$out"
  [ -e "$m/saw-p1-p2" ] && [ -e "$m/saw-p2-p1" ] || fail "parallel suites did not run at the same time:
$out"
  [ ! -e "$m/overlaps" ] || fail "a serial suite overlapped another suite: $(cat "$m/overlaps")"
  printf '%s\n' "$out" | grep -qE '^PASS +par +t/p1\.test\.sh +[0-9]+s$' || fail "summary lacks a par row with a duration:
$out"
  printf '%s\n' "$out" | grep -qE '^PASS +ser +t/s2\.test\.sh +[0-9]+s$' || fail "summary lacks a ser row with a duration:
$out"
  if printf '%s\n' "$out" | grep -q 'should never run'; then fail "an excluded suite ran"; fi
  printf '%s\n' "$out" | grep -q '4 run, 0 failed, 1 excluded' || fail "summary counts are wrong:
$out"
  rm -rf "$d" "$m"
  echo "PASS: test_pool_runs_in_parallel_and_serial_suites_run_alone"
}

test_a_failing_suite_fails_the_run_and_shows_its_log() {
  local d m out rc=0
  d="$(mktemp -d)"; m="$(mktemp -d)"
  make_fixture "$d"
  printf '%s\n' 'echo "ok - first"; echo "not ok - boom happened"; exit 3' > "$d/t/p1.test.sh"
  printf '%s\n' 'echo "not ok - serial boom"; exit 4' > "$d/t/s1.test.sh"
  out="$(run_fixture "$d" "$m" 2>&1)" || rc=$?
  [ "$rc" -eq 1 ] || fail "a failing suite must exit 1, got $rc:
$out"
  printf '%s\n' "$out" | grep -qE '^FAIL +par +t/p1\.test\.sh' || fail "the failing par suite is not reported:
$out"
  printf '%s\n' "$out" | grep -qE '^FAIL +ser +t/s1\.test\.sh' || fail "the failing ser suite is not reported:
$out"
  printf '%s\n' "$out" | grep -qF 'not ok - boom happened' || fail "the failing suite's log is not printed"
  printf '%s\n' "$out" | grep -qF 'exit 3' || fail "the failing suite's exit code is not printed"
  printf '%s\n' "$out" | grep -q '2 failed' || fail "summary does not count the failures:
$out"
  rm -rf "$d" "$m"
  echo "PASS: test_a_failing_suite_fails_the_run_and_shows_its_log"
}

test_bad_configuration_is_a_tool_error() {
  local d m rc
  d="$(mktemp -d)"; m="$(mktemp -d)"
  make_fixture "$d"
  rc=0; MARK="$m" BASH_TESTS_ROOT="$d" BASH_TESTS_JOBS=0 BASH_TESTS_SERIAL="$FIXTURE_SERIAL" \
    BASH_TESTS_EXCLUDED="$FIXTURE_EXCLUDED" bash "$RUNNER" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 2 ] || fail "BASH_TESTS_JOBS=0 must exit 2, got $rc"
  rc=0; MARK="$m" BASH_TESTS_ROOT="$d" BASH_TESTS_EXCLUDED="$FIXTURE_EXCLUDED" \
    BASH_TESTS_SERIAL=$'t/nope.test.sh\tfixture: a serial path that is not tracked at all' \
    bash "$RUNNER" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 2 ] || fail "an untracked serial suite must exit 2, got $rc"
  rc=0; bash "$RUNNER" --bogus >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 2 ] || fail "an unknown flag must exit 2, got $rc"
  rm -rf "$d" "$m"
  echo "PASS: test_bad_configuration_is_a_tool_error"
}

test_lists_every_tracked_suite_except_the_excluded
test_suites_outside_the_old_globs_are_listed
test_every_exclusion_is_tracked_and_has_a_reason
test_every_serial_suite_is_listed_and_has_a_reason
test_the_timing_sensitive_suites_are_serial
test_pool_runs_in_parallel_and_serial_suites_run_alone
test_a_failing_suite_fails_the_run_and_shows_its_log
test_bad_configuration_is_a_tool_error
