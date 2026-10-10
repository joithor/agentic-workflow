#!/usr/bin/env bash
# Tests for scripts/ci/lane-budgets.mjs — the per-package lane budget check (spec §4b.5,
# .agents/rules/csharp-testing.md "Lane budgets"). It times
# `dotnet test --project <test project> --filter-trait "Lane=<lane>" --ignore-exit-code 8`
# per test project, sums the wall clock per package (the deps.yaml projectPrefix, longest
# wins) and fails a package over its lane budget. A stub `dotnet` stands in for the SDK.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$ROOT/scripts/ci/lane-budgets.mjs"

fail() { echo "FAIL: $*"; exit 1; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# A fixture repo: two packages whose prefixes nest (Aw.Foo, Aw.Foo.Data), a test-support
# project (never run), and a test assembly no package owns (Aw.Architecture.Tests).
make_fixture() {
  local d="$1"
  mkdir -p "$d/tests"
  cat > "$d/deps.yaml" <<'YAML'
version: 1
packages:
  - id: Foo
    kind: package
    tier: 0
    projectPrefix: Aw.Foo
  - id: FooData
    kind: package
    tier: 0
    sublevel: 1
    projectPrefix: Aw.Foo.Data
  - id: Hooks
    kind: shell
YAML
  local p
  for p in Aw.Foo.Tests Aw.Foo.Core.Tests Aw.Foo.Data.Tests Aw.Foo.Testing Aw.Architecture.Tests; do
    mkdir -p "$d/tests/$p"; echo '<Project />' > "$d/tests/$p/$p.csproj"
  done
  cat > "$d/AgenticWorkflow.slnx" <<'XML'
<Solution>
  <Folder Name="/tests/">
    <Project Path="tests/Aw.Foo.Tests/Aw.Foo.Tests.csproj" />
    <Project Path="tests/Aw.Foo.Core.Tests/Aw.Foo.Core.Tests.csproj" />
    <Project Path="tests/Aw.Foo.Data.Tests/Aw.Foo.Data.Tests.csproj" />
    <Project Path="tests/Aw.Foo.Testing/Aw.Foo.Testing.csproj" />
    <Project Path="tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj" />
  </Folder>
</Solution>
XML
}

# The stub logs its arguments, sleeps STUB_<project>_<lane> seconds (dots as _), and exits
# STUB_EXIT_<project>_<lane> (default 0).
cat > "$WORK/dotnet" <<'STUB'
#!/usr/bin/env bash
echo "$*" >> "$STUB_LOG"
proj=""; lane=""
while [ $# -gt 0 ]; do
  case "$1" in
    --project) proj="$(basename "$2" .csproj)"; shift ;;
    --filter-trait) lane="${2#Lane=}"; shift ;;
  esac
  shift
done
key="$(echo "${proj}_${lane}" | tr '.' '_')"
s="STUB_${key}"; e="STUB_EXIT_${key}"
sleep "${!s:-0}"
exit "${!e:-0}"
STUB
chmod +x "$WORK/dotnet"

run_check() { # <fixture> [args...]; sets OUT and RC
  local d="$1"; shift
  RC=0
  OUT="$(STUB_LOG="$WORK/log" node "$CHECK" --root "$d" --dotnet "$WORK/dotnet" "$@" 2>&1)" || RC=$?
}

test_sums_each_package_and_passes_under_budget() {
  local d="$WORK/ok"; make_fixture "$d"; : > "$WORK/log"
  export STUB_Aw_Foo_Tests_unit=0.3 STUB_Aw_Foo_Core_Tests_unit=0.3 STUB_Aw_Foo_Data_Tests_integration=0.2
  run_check "$d" --budget unit=2,integration=2
  unset STUB_Aw_Foo_Tests_unit STUB_Aw_Foo_Core_Tests_unit STUB_Aw_Foo_Data_Tests_integration
  [ "$RC" -eq 0 ] || fail "under budget must exit 0, got $RC:
$OUT"
  # Foo's unit lane is the sum of its two projects (0.6 s); FooData is its own package.
  printf '%s\n' "$OUT" | grep -qE '^ok +Foo +unit +0\.[6-9][0-9]? s +limit 2 s' || fail "Foo unit should sum both projects:
$OUT"
  printf '%s\n' "$OUT" | grep -qE '^ok +FooData +integration +0\.[2-9]' || fail "Aw.Foo.Data.Tests should count for FooData (longest prefix):
$OUT"
  printf '%s\n' "$OUT" | grep -qE '^ok +\(Aw\.Architecture\.Tests\) +unit' || fail "an unowned test assembly is its own row:
$OUT"
  grep -q 'Aw.Foo.Testing' "$WORK/log" && fail "a *.Testing project is not a test project and must not run"
  grep -q 'Lane=heavy' "$WORK/log" && fail "the heavy lane has no budget and must not run"
  grep -qF -- '--project tests/Aw.Foo.Tests/Aw.Foo.Tests.csproj' "$WORK/log" || fail "dotnet test not called per project:
$(cat "$WORK/log")"
  grep -F -- 'Aw.Foo.Tests.csproj' "$WORK/log" | grep -F -- '--filter-trait Lane=unit' | grep -F -- '--ignore-exit-code 8' | grep -qF -- '--no-build' \
    || fail "missing lane filter, exit-8 ignore or --no-build:
$(cat "$WORK/log")"
  echo "PASS: test_sums_each_package_and_passes_under_budget"
}

test_a_package_over_budget_fails_with_the_rule_id() {
  local d="$WORK/over"; make_fixture "$d"; : > "$WORK/log"
  export STUB_Aw_Foo_Tests_unit=0.4 STUB_Aw_Foo_Core_Tests_unit=0.4
  run_check "$d" --budget unit=0.5,integration=5
  unset STUB_Aw_Foo_Tests_unit STUB_Aw_Foo_Core_Tests_unit
  [ "$RC" -eq 1 ] || fail "over budget must exit 1, got $RC:
$OUT"
  printf '%s\n' "$OUT" | grep -E '^LANE-PKG-001' | grep -F 'Foo' | grep -F 'unit' | grep -F 'limit 0.5 s' \
    | grep -qF 'dotnet test --project tests/Aw.Foo.Tests/Aw.Foo.Tests.csproj' \
    || fail "LANE-PKG-001 must name the package, lane, measured s, limit and the fix command:
$OUT"
  printf '%s\n' "$OUT" | grep -qE '^ok +FooData' || fail "a package under budget still reports ok:
$OUT"
  echo "PASS: test_a_package_over_budget_fails_with_the_rule_id"
}

test_a_failing_test_run_fails_the_check() {
  local d="$WORK/red"; make_fixture "$d"; : > "$WORK/log"
  export STUB_EXIT_Aw_Foo_Data_Tests_integration=2
  run_check "$d" --budget unit=5,integration=5
  unset STUB_EXIT_Aw_Foo_Data_Tests_integration
  [ "$RC" -eq 1 ] || fail "a failing dotnet test must exit 1, got $RC:
$OUT"
  printf '%s\n' "$OUT" | grep -E '^LANE-PKG-002' | grep -F 'Aw.Foo.Data.Tests' | grep -qF 'exit 2' \
    || fail "LANE-PKG-002 must name the project and the exit code:
$OUT"
  echo "PASS: test_a_failing_test_run_fails_the_check"
}

test_tool_errors_exit_2() {
  local d="$WORK/bad"; make_fixture "$d"
  run_check "$d" --budget unit=abc
  [ "$RC" -eq 2 ] || fail "a bad --budget must exit 2, got $RC"
  rm "$d/AgenticWorkflow.slnx"
  run_check "$d"
  [ "$RC" -eq 2 ] || fail "a missing solution must exit 2, got $RC"
  run_check "$d" --bogus
  [ "$RC" -eq 2 ] || fail "an unknown flag must exit 2, got $RC"
  echo "PASS: test_tool_errors_exit_2"
}

test_default_budgets_are_the_rule_numbers() {
  local d="$WORK/defaults"; make_fixture "$d"
  run_check "$d"
  [ "$RC" -eq 0 ] || fail "default run must pass on the instant stub, got $RC:
$OUT"
  printf '%s\n' "$OUT" | grep -qE 'unit .*limit 10 s' || fail "the unit budget is 10 s:
$OUT"
  printf '%s\n' "$OUT" | grep -qE 'integration .*limit 60 s' || fail "the integration budget is 60 s:
$OUT"
  echo "PASS: test_default_budgets_are_the_rule_numbers"
}

test_sums_each_package_and_passes_under_budget
test_a_package_over_budget_fails_with_the_rule_id
test_a_failing_test_run_fails_the_check
test_tool_errors_exit_2
test_default_budgets_are_the_rule_numbers
