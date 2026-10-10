#!/usr/bin/env bash
# Tests for scripts/readiness-baseline.mjs (throwaway R0 scorer). Fixture trees only; no network (--no-remote).
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$DIR/../readiness-baseline.mjs"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $*"; exit 1; }

# A minimal tree: README, AGENTS.md, one app with a lockfile, strict tsconfig and a unit test.
make_tree() {
  local root="$1"
  mkdir -p "$root/judge/tests" "$root/judge/src"
  echo "# demo" > "$root/README.md"
  printf '# Agents\n\n```bash\ncd judge && npm run build\n```\n' > "$root/AGENTS.md"
  echo '{"name":"judge","scripts":{"build":"tsc","test":"vitest run"}}' > "$root/judge/package.json"
  echo '{}' > "$root/judge/package-lock.json"
  echo '{"compilerOptions":{"strict": true}}' > "$root/judge/tsconfig.json"
  echo 'test("x", () => {})' > "$root/judge/tests/a.test.ts"
}

row_state() { # row_state <file> <criterion-id> -> state column of that criterion's table row
  awk -F'|' -v id="$2" '$2 ~ "^ *"id" *$" { gsub(/ /, "", $6); print $6; exit }' "$1"
}

test_codeowners_flips_sec3() {
  local t="$TMP/co"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/before.md"
  [ "$(row_state "$t/before.md" SEC-3)" = "fail" ] || fail "SEC-3 should fail without CODEOWNERS"
  mkdir -p "$t/.github"; echo "* @owner" > "$t/.github/CODEOWNERS"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/after.md"
  [ "$(row_state "$t/after.md" SEC-3)" = "pass" ] || fail "SEC-3 should pass with .github/CODEOWNERS"
  echo "PASS: test_codeowners_flips_sec3"
}

test_env_template_flips_de2() {
  local t="$TMP/env"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/a.md"
  [ "$(row_state "$t/a.md" DE-2)" = "fail" ] || fail "DE-2 should fail without .env.example"
  echo "FOO=" > "$t/.env.example"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/b.md"
  [ "$(row_state "$t/b.md" DE-2)" = "pass" ] || fail "DE-2 should pass with .env.example"
  echo "PASS: test_env_template_flips_de2"
}

test_no_remote_marks_remote_unknown() {
  local t="$TMP/nr"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/o.md"
  for id in SEC-1 SEC-2 TD-2 BS-4; do
    [ "$(row_state "$t/o.md" "$id")" = "unknown" ] || fail "$id should be unknown with --no-remote"
  done
  echo "PASS: test_no_remote_marks_remote_unknown"
}

test_deterministic_for_fixed_date() {
  local t="$TMP/det"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/1.md"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/2.md"
  diff -u "$t/1.md" "$t/2.md" >/dev/null || fail "two runs with the same --date differ"
  echo "PASS: test_deterministic_for_fixed_date"
}

test_app_fraction_counts_present_apps() {
  local t="$TMP/frac"; make_tree "$t"
  node "$SCRIPT" --root "$t" --no-remote --date 2026-10-10 --out "$t/o.md"
  # TS-1 is per app over seven apps; only judge exists and has a test.
  grep -E '^\| TS-1 \|.*\| 1 / 7 \|' "$t/o.md" >/dev/null || fail "TS-1 should report 1 / 7"
  echo "PASS: test_app_fraction_counts_present_apps"
}

test_bad_root_exits_2() {
  local rc=0
  node "$SCRIPT" --root "$TMP/does-not-exist" --no-remote >/dev/null 2>"$TMP/err" || rc=$?
  [ "$rc" -eq 2 ] || fail "bad root should exit 2, got $rc"
  grep -q "does-not-exist" "$TMP/err" || fail "error should name the path"
  echo "PASS: test_bad_root_exits_2"
}

test_only_read_only_gh_calls() {
  # The script must never write to GitHub: no method override and no field flags on gh api.
  if grep -nE "'(-X|--method|-f|-F|--field|--raw-field|--input)'" "$SCRIPT"; then
    fail "gh api write flags found"
  fi
  echo "PASS: test_only_read_only_gh_calls"
}

test_codeowners_flips_sec3
test_env_template_flips_de2
test_no_remote_marks_remote_unknown
test_deterministic_for_fixed_date
test_app_fraction_counts_present_apps
test_bad_root_exits_2
test_only_read_only_gh_calls
