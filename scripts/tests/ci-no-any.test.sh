#!/usr/bin/env bash
# Tests for scripts/ci/no-any.mjs — merge gate 7: no `any` type in TypeScript source. It parses TypeScript (so strings and comments never count) and holds
# existing violations to a ratchet baseline that may only shrink.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$ROOT/scripts/ci/no-any.mjs"

fail() { echo "FAIL: $*"; exit 1; }

repo() {
  local r; r="$(mktemp -d)"
  git -C "$r" init -q
  mkdir -p "$r/pkg/src"
  printf 'export const s = "x: any";\n// note: any caller\nexport const company = 1;\n' > "$r/pkg/src/clean.ts"
  printf '{}\n' > "$r/baseline.json"
  echo "$r"
}

run() { # repo [extra args...] -> sets out, rc
  local r="$1"; shift
  rc=0; out="$(node "$CHECK" --root "$r" --baseline "$r/baseline.json" "$@" 2>&1)" || rc=$?
}

test_strings_and_comments_pass() {
  local r; r="$(repo)"; git -C "$r" add -A
  run "$r"; rm -rf "$r"
  [ "$rc" = "0" ] || fail "clean tree failed ($rc): $out"
  echo "PASS: test_strings_and_comments_pass"
}

test_any_in_type_positions_fails_with_location_and_fix() {
  local r; r="$(repo)"
  printf 'export const a: any = 1;\nexport type C = Record<string, any>;\nexport const d: any[] = [];\n' > "$r/pkg/src/bad.ts"
  git -C "$r" add -A
  run "$r"; rm -rf "$r"
  [ "$rc" = "1" ] || fail "expected exit 1, got $rc: $out"
  echo "$out" | has 'pkg/src/bad.ts:1:' || fail "no location for line 1: $out"
  echo "$out" | has 'pkg/src/bad.ts:3:' || fail "no location for line 3: $out"
  echo "$out" | has 'AW-GATE-7' || fail "no rule id: $out"
  echo "$out" | has 'unknown' || fail "no fix hint: $out"
  echo "PASS: test_any_in_type_positions_fails_with_location_and_fix"
}

test_baseline_holds_existing_count_and_fails_growth() {
  local r; r="$(repo)"
  printf 'export const m: Map<any, any> = new Map();\n' > "$r/pkg/src/legacy.ts"
  printf '{"pkg/src/legacy.ts": 2}\n' > "$r/baseline.json"
  git -C "$r" add -A
  run "$r"
  [ "$rc" = "0" ] || { rm -rf "$r"; fail "baselined file failed: $out"; }
  printf 'export const m: Map<any, any> = new Map();\nexport const n: any = 1;\n' > "$r/pkg/src/legacy.ts"
  run "$r"; rm -rf "$r"
  [ "$rc" = "1" ] || fail "growth over the baseline passed: $out"
  echo "$out" | has 'baseline 2' || fail "no baseline in message: $out"
  echo "PASS: test_baseline_holds_existing_count_and_fails_growth"
}

test_a_shrunk_but_unlowered_baseline_fails() {
  local r; r="$(repo)"
  printf 'export const m: Map<string, any> = new Map();\n' > "$r/pkg/src/legacy.ts"
  printf '{"pkg/src/legacy.ts": 2}\n' > "$r/baseline.json"
  git -C "$r" add -A
  run "$r"; rm -rf "$r"
  [ "$rc" = "1" ] || fail "a stale baseline passed ($rc): $out"
  echo "$out" | has 'lower the baseline' || fail "no lower-the-baseline fix: $out"
  echo "PASS: test_a_shrunk_but_unlowered_baseline_fails"
}

test_the_baseline_may_not_rise_above_the_base_branch() {
  local r; r="$(repo)"
  printf 'export const m: Map<any, any> = new Map();\nexport const n: any = 1;\n' > "$r/pkg/src/legacy.ts"
  printf '{"pkg/src/legacy.ts": 3}\n' > "$r/baseline.json"
  printf '{"pkg/src/legacy.ts": 2}\n' > "$r/base.json"
  git -C "$r" add -A
  run "$r" --base-baseline "$r/base.json"
  [ "$rc" = "1" ] || { rm -rf "$r"; fail "a raised baseline passed: $out"; }
  echo "$out" | has 'raises the baseline' || { rm -rf "$r"; fail "no raise message: $out"; }
  printf 'export const m: Map<any, any> = new Map();\n' > "$r/pkg/src/legacy.ts"
  printf '{"pkg/src/legacy.ts": 2}\n' > "$r/baseline.json"
  run "$r" --base-baseline "$r/base.json"; rm -rf "$r"
  [ "$rc" = "0" ] || fail "an unchanged baseline failed: $out"
  echo "PASS: test_the_baseline_may_not_rise_above_the_base_branch"
}

test_a_new_baseline_key_fails() {
  local r; r="$(repo)"
  printf 'export const n: any = 1;\n' > "$r/pkg/src/fresh.ts"
  printf '{"pkg/src/fresh.ts": 1}\n' > "$r/baseline.json"
  printf '{}\n' > "$r/base.json"
  git -C "$r" add -A
  run "$r" --base-baseline "$r/base.json"; rm -rf "$r"
  [ "$rc" = "1" ] || fail "a new baseline key passed: $out"
  echo "$out" | has 'pkg/src/fresh.ts' || fail "the new key isn't named: $out"
  echo "PASS: test_a_new_baseline_key_fails"
}

test_a_baseline_key_for_a_deleted_file_fails() {
  local r; r="$(repo)"
  printf '{"pkg/src/gone.ts": 1}\n' > "$r/baseline.json"
  git -C "$r" add -A
  run "$r"; rm -rf "$r"
  [ "$rc" = "1" ] || fail "a baseline key for a deleted file passed: $out"
  echo "$out" | has 'pkg/src/gone.ts: not checked any more' || fail "the stale key isn't named: $out"
  echo "PASS: test_a_baseline_key_for_a_deleted_file_fails"
}

test_declaration_files_count() {
  local r; r="$(repo)"
  printf 'declare module "shim" {\n  const v: any;\n  export default v;\n}\n' > "$r/pkg/src/shim.d.ts"
  git -C "$r" add -A
  run "$r"; rm -rf "$r"
  [ "$rc" = "1" ] || fail "any in a .d.ts passed: $out"
  echo "$out" | has 'pkg/src/shim.d.ts:2:' || fail "no .d.ts location: $out"
  echo "PASS: test_declaration_files_count"
}

test_the_repo_passes_with_its_baseline() {
  local rc=0 out
  out="$(node "$CHECK" --root "$ROOT" --baseline "$ROOT/scripts/ci/no-any-baseline.json" 2>&1)" || rc=$?
  [ "$rc" = "0" ] || fail "the repo fails gate 7: $out"
  echo "PASS: test_the_repo_passes_with_its_baseline"
}

test_strings_and_comments_pass
test_any_in_type_positions_fails_with_location_and_fix
test_baseline_holds_existing_count_and_fails_growth
test_a_shrunk_but_unlowered_baseline_fails
test_the_baseline_may_not_rise_above_the_base_branch
test_a_new_baseline_key_fails
test_declaration_files_count
test_a_baseline_key_for_a_deleted_file_fails
test_the_repo_passes_with_its_baseline
