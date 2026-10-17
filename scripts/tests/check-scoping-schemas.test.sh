#!/usr/bin/env bash
# Tests for scripts/check-scoping-schemas.mjs (the scoping station's output schemas, SC-Y): the repo passes, and each kind of drift fails.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-scoping-schemas.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-scoping-schemas-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# Ajv is a dev dependency of scripts/ (scripts/package.json); installed once under a lock, since suites run in parallel.
# shellcheck source=scripts/lib/ensure-scripts-deps.sh
. "$REPO/scripts/lib/ensure-scripts-deps.sh"
ensure_scripts_deps "$REPO/scripts" || exit 1

run() { set +e; OUT="$(node "$CHECK" --root "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -F -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

# A throwaway copy of the files the check reads.
copy() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/schemas" "$d/recipes/sindri"
  cp -R "$REPO/schemas/scoping" "$d/schemas/"
  cp "$REPO/recipes/sindri/workflow.scoping.yaml" "$d/recipes/sindri/"
  cp -R "$REPO/recipes/sindri/evals" "$d/recipes/sindri/"
  echo "$d"
}

# Rewrites a JSON file in a copy through a jq filter.
edit() { local f="$1"; shift; jq "$@" "$f" > "$f.new"; mv "$f.new" "$f"; }

echo "repo"
run "$REPO"; expect_rc "the repo's scoping schemas pass" 0; expect_out "prints ok" "check-scoping-schemas: ok"

echo "drift"
d="$(copy verdict)"; edit "$d/schemas/scoping/examples/scope-input.brief.valid.json" '.kind = "ticket"'
run "$d"; expect_rc "a valid example the schema refuses fails" 1; expect_out "names the example" "scope-input.brief.valid.json"

d="$(copy relax)"; edit "$d/schemas/scoping/acceptance.json" '."$defs".fact.properties.items.items.properties.status = {"type":"string"} | ."$defs".file.properties.items.items.properties.status = {"type":"string"}'
run "$d"; expect_rc "a schema that accepts an invalid example fails" 1; expect_out "names the invalid example" "acceptance.passing.invalid.json"

d="$(copy unlisted)"; echo '{}' > "$d/schemas/scoping/examples/extra.json"
run "$d"; expect_rc "an example missing from the manifest fails" 1; expect_out "names the file" "extra.json"

d="$(copy broken)"; echo '{"type": 5}' > "$d/schemas/scoping/brief.json"
run "$d"; expect_rc "a schema that does not compile fails" 1; expect_out "names the schema" "brief.json"

d="$(copy seeds)"; edit "$d/schemas/scoping/seeds.json" 'del(."fact:challenge")'
run "$d"; expect_rc "a seed the recipe's intake writes but seeds.json lacks fails" 1; expect_out "names the drift" "seeds.json"

d="$(copy badseed)"; edit "$d/schemas/scoping/seeds.json" '."fact:acceptance" = {"items": [{"id": "x"}]}'
run "$d"; expect_rc "a seed its schema refuses fails" 1; expect_out "names the fact" "fact:acceptance"

d="$(copy payload)"; sed -i 's/"ref":"7"/"pr":7/' "$d/recipes/sindri/evals/workflow.scoping/change-happy.jsonl"
run "$d"; expect_rc "an eval payload its schema refuses fails" 1; expect_out "names the eval and the fact" "change-happy.jsonl:1"

d="$(copy stringpayload)"; sed -i 's/"fact:brief":{.*},"fact:anchors"/"fact:brief":"text","fact:anchors"/' "$d/recipes/sindri/evals/workflow.scoping/brief-happy.jsonl"
run "$d"; expect_rc "a string where the fact has a schema fails" 1; expect_out "names the fact" "fact:brief"

d="$(copy nosource)"; edit "$d/schemas/scoping/examples/manifest.json" '.examples[0].source = ""'
run "$d"; expect_rc "a valid example with no source fails" 1; expect_out "names the requirement" "names its source"

echo
echo "passed $PASS, failed $FAIL"
[ "$FAIL" -eq 0 ]
