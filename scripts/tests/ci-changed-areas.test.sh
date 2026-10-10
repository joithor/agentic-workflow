#!/usr/bin/env bash
# Tests for scripts/ci/changed-areas.sh — the CI path filter. A wrong answer here
# either skips a check that should run or runs everything on a docs change.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DIR/../ci/changed-areas.sh"

expect() {
  local name="$1" files="$2" want="$3" got
  got="$(printf '%s' "$files" | classify_changes)"
  if [ "$got" != "$want" ]; then
    printf 'FAIL: %s\n--- want\n%s\n--- got\n%s\n' "$name" "$want" "$got"
    exit 1
  fi
  echo "PASS: $name"
}

ALL='["mcp-bridge","scorer","judge","sindri","skills/ui-evidence","skills/bugFixOrchestrator"]'

expect "docs-only change runs nothing area-specific" \
  "$(printf 'docs/readme.md\nplanning/TESTING.md\n')" \
  "$(printf 'ts=false\nbash=false\ndotnet=false\npackages=[]')"

expect "one package change runs that package and the bash tests" \
  "$(printf 'judge/src/cli.ts\n')" \
  "$(printf 'ts=true\nbash=true\ndotnet=false\npackages=["judge"]')"

expect "nested skill package is matched by its full path" \
  "$(printf 'skills/ui-evidence/src/run.ts\nskills/review/SKILL.md\n')" \
  "$(printf 'ts=true\nbash=true\ndotnet=false\npackages=["skills/ui-evidence"]')"

expect "a judge change also runs scorer, whose tests import judge's source" \
  "$(printf 'judge/src/db.ts\n')" \
  "$(printf 'ts=true\nbash=true\ndotnet=false\npackages=["judge","scorer"]')"

expect "a hook change runs bash tests only" \
  "$(printf 'config/hooks/send-gate.sh\n')" \
  "$(printf 'ts=false\nbash=true\ndotnet=false\npackages=[]')"

expect "a C# change runs dotnet only" \
  "$(printf 'src/Aw.Kernel/Result.cs\nDirectory.Packages.props\n')" \
  "$(printf 'ts=false\nbash=false\ndotnet=true\npackages=[]')"

expect "a workflow change runs everything" \
  "$(printf '.github/workflows/ci.yml\n')" \
  "$(printf 'ts=true\nbash=true\ndotnet=true\npackages=%s' "$ALL")"

expect "a CI script change runs everything" \
  "$(printf 'scripts/ci/bash-tests.sh\n')" \
  "$(printf 'ts=true\nbash=true\ndotnet=true\npackages=%s' "$ALL")"

expect "a lookalike prefix is not a package" \
  "$(printf 'judgement/notes.md\n')" \
  "$(printf 'ts=false\nbash=false\ndotnet=false\npackages=[]')"

test_non_pr_event_runs_everything() {
  local got
  got="$(main push "")"
  [ "$got" = "$(printf 'ts=true\nbash=true\ndotnet=true\npackages=%s' "$ALL")" ] \
    || { echo "FAIL: push event should run everything, got: $got"; exit 1; }
  echo "PASS: test_non_pr_event_runs_everything"
}

test_pr_without_base_fails_closed() {
  local got
  got="$(main pull_request "")"
  [ "$got" = "$(printf 'ts=true\nbash=true\ndotnet=true\npackages=%s' "$ALL")" ] \
    || { echo "FAIL: a PR with no base sha should run everything, got: $got"; exit 1; }
  echo "PASS: test_pr_without_base_fails_closed"
}

test_non_pr_event_runs_everything
test_pr_without_base_fails_closed
