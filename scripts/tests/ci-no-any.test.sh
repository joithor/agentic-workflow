#!/usr/bin/env bash
# Tests for scripts/ci/no-any.mjs — merge gate 6: no `any` type outside the Fastify
# boundary. It parses TypeScript (so strings and comments never count) and holds
# existing violations to a ratchet baseline that may only shrink.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$ROOT/scripts/ci/no-any.mjs"

fail() { echo "FAIL: $*"; exit 1; }

repo() {
  local r; r="$(mktemp -d)"
  git -C "$r" init -q
  mkdir -p "$r/pkg/src" "$r/mcp-bridge/src/routes"
  printf 'export const s = "x: any";\n// note: any caller\nexport const company = 1;\n' > "$r/pkg/src/clean.ts"
  printf 'export const r: any = 1;\n' > "$r/mcp-bridge/src/routes/boundary.ts"
  printf '{}\n' > "$r/baseline.json"
  echo "$r"
}

run() { # repo -> sets out, rc
  rc=0; out="$(node "$CHECK" --root "$1" --baseline "$1/baseline.json" 2>&1)" || rc=$?
}

test_strings_comments_and_the_boundary_pass() {
  local r; r="$(repo)"; git -C "$r" add -A
  run "$r"; rm -rf "$r"
  [ "$rc" = "0" ] || fail "clean tree failed ($rc): $out"
  echo "PASS: test_strings_comments_and_the_boundary_pass"
}

test_any_in_type_positions_fails_with_location_and_fix() {
  local r; r="$(repo)"
  printf 'export const a: any = 1;\nexport type C = Record<string, any>;\nexport const d: any[] = [];\n' > "$r/pkg/src/bad.ts"
  git -C "$r" add -A
  run "$r"; rm -rf "$r"
  [ "$rc" = "1" ] || fail "expected exit 1, got $rc: $out"
  echo "$out" | grep -q 'pkg/src/bad.ts:1:' || fail "no location for line 1: $out"
  echo "$out" | grep -q 'pkg/src/bad.ts:3:' || fail "no location for line 3: $out"
  echo "$out" | grep -q 'AW-GATE-6' || fail "no rule id: $out"
  echo "$out" | grep -q 'unknown' || fail "no fix hint: $out"
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
  echo "$out" | grep -q 'baseline 2' || fail "no baseline in message: $out"
  echo "PASS: test_baseline_holds_existing_count_and_fails_growth"
}

test_a_shrunk_baseline_is_reported() {
  local r; r="$(repo)"
  printf 'export const m: Map<string, any> = new Map();\n' > "$r/pkg/src/legacy.ts"
  printf '{"pkg/src/legacy.ts": 2}\n' > "$r/baseline.json"
  git -C "$r" add -A
  run "$r"; rm -rf "$r"
  [ "$rc" = "0" ] || fail "a shrinking file failed: $out"
  echo "$out" | grep -q 'lower the baseline' || fail "no shrink note: $out"
  echo "PASS: test_a_shrunk_baseline_is_reported"
}

test_the_repo_passes_with_its_baseline() {
  local rc=0 out
  out="$(node "$CHECK" --root "$ROOT" --baseline "$ROOT/scripts/ci/no-any-baseline.json" 2>&1)" || rc=$?
  [ "$rc" = "0" ] || fail "the repo fails gate 6: $out"
  echo "PASS: test_the_repo_passes_with_its_baseline"
}

test_strings_comments_and_the_boundary_pass
test_any_in_type_positions_fails_with_location_and_fix
test_baseline_holds_existing_count_and_fails_growth
test_a_shrunk_baseline_is_reported
test_the_repo_passes_with_its_baseline
