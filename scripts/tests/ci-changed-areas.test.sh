#!/usr/bin/env bash
# Tests for scripts/ci/changed-areas.sh — the CI path filter. A wrong answer here
# either skips a check that should run or runs everything on a docs change.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DIR/../ci/changed-areas.sh"

expect() {
  local name="$1" files="$2" want="$3" got
  # classify_changes reads git's -z output: NUL-separated paths.
  got="$(printf '%s' "$files" | tr '\n' '\0' | classify_changes)"
  if [ "$got" != "$want" ]; then
    printf 'FAIL: %s\n--- want\n%s\n--- got\n%s\n' "$name" "$want" "$got"
    exit 1
  fi
  echo "PASS: $name"
}

ALL='["mcp-bridge","scorer","judge","sindri","skills/ui-evidence","skills/bugFixOrchestrator"]'

expect "docs-only change runs nothing area-specific" \
  "$(printf 'docs/readme.md\nplanning/TESTING.md\n')" \
  "$(printf 'bash=false\ndotnet=false\npackages=[]')"

expect "one package change runs that package and the bash tests" \
  "$(printf 'mcp-bridge/src/index.ts\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=["mcp-bridge"]')"

expect "nested skill package is matched by its full path" \
  "$(printf 'skills/ui-evidence/src/run.ts\nskills/review/SKILL.md\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=["skills/ui-evidence"]')"

expect "a judge change also runs scorer, whose tests import judge's source" \
  "$(printf 'judge/src/db.ts\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=["judge","scorer"]')"

expect "a hook change runs bash tests only" \
  "$(printf 'config/hooks/send-gate.sh\n')" \
  "$(printf 'bash=true\ndotnet=false\npackages=[]')"

expect "a C# change runs dotnet only" \
  "$(printf 'src/Aw.Kernel/Result.cs\nDirectory.Packages.props\n')" \
  "$(printf 'bash=false\ndotnet=true\npackages=[]')"

expect "an AOT smoke script or its expected output runs dotnet" \
  "$(printf 'src/Aw.Kernel.AotSmoke/run.sh\nsrc/Aw.Kernel.Data.AotSmoke/expected.txt\n')" \
  "$(printf 'bash=false\ndotnet=true\npackages=[]')"

expect "a packet JSON Schema change runs dotnet (its golden test compares it with the records)" \
  "$(printf 'schemas/packets/tasks.schema.json\n')" \
  "$(printf 'bash=false\ndotnet=true\npackages=[]')"

expect "a .NET tool manifest change runs dotnet" \
  "$(printf '.config/dotnet-tools.json\n')" \
  "$(printf 'bash=false\ndotnet=true\npackages=[]')"

expect "the NuGet supply-chain script runs dotnet (where CI runs it) and bash (its test)" \
  "$(printf 'scripts/dotnet-verify-packages.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=[]')"

expect "spike projects never run dotnet" \
  "$(printf 'spikes/aot/Spike.csproj\nspikes/aot/Program.cs\nspikes/Directory.Build.props\n')" \
  "$(printf 'bash=false\ndotnet=false\npackages=[]')"

expect "a workflow change runs everything" \
  "$(printf '.github/workflows/ci.yml\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")"

expect "a Node version change runs everything" \
  "$(printf '.nvmrc\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")"

expect "a CI script change runs everything" \
  "$(printf 'scripts/ci/bash-tests.sh\n')" \
  "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")"

expect "a lookalike prefix is not a package" \
  "$(printf 'judgement/notes.md\n')" \
  "$(printf 'bash=false\ndotnet=false\npackages=[]')"

# A throwaway git repo with git's defaults pinned on (rename detection, quoted
# paths), so the real `main` diff is exercised, not just classify_changes.
scratch_repo() {
  local repo; repo="$(mktemp -d)"
  git -C "$repo" init -q
  git -C "$repo" config user.email ci@example.invalid
  git -C "$repo" config user.name ci
  git -C "$repo" config diff.renames true
  git -C "$repo" config core.quotePath true
  mkdir -p "$repo/mcp-bridge/src" "$repo/docs"
  printf 'export const db = 1;\n' > "$repo/mcp-bridge/src/db.ts"
  printf '# readme\n' > "$repo/docs/readme.md"
  git -C "$repo" add -A && git -C "$repo" commit -qm base
  echo "$repo"
}

test_rename_out_of_a_package_selects_the_package() {
  local repo base got
  repo="$(scratch_repo)"; base="$(git -C "$repo" rev-parse HEAD)"
  git -C "$repo" mv mcp-bridge/src/db.ts docs/db.ts && git -C "$repo" commit -qm move
  got="$(cd "$repo" && main pull_request "$base" trunk trunk)"
  rm -rf "$repo"
  [ "$got" = "$(printf 'bash=true\ndotnet=false\npackages=["mcp-bridge"]')" ] \
    || { echo "FAIL: a rename out of mcp-bridge must select it, got: $got"; exit 1; }
  echo "PASS: test_rename_out_of_a_package_selects_the_package"
}

test_non_ascii_filename_in_a_package_selects_the_package() {
  local repo base got
  repo="$(scratch_repo)"; base="$(git -C "$repo" rev-parse HEAD)"
  printf 'export const e = 1;\n' > "$repo/mcp-bridge/src/$(printf '\303\251').ts"
  git -C "$repo" add -A && git -C "$repo" commit -qm accent
  got="$(cd "$repo" && main pull_request "$base" trunk trunk)"
  rm -rf "$repo"
  [ "$got" = "$(printf 'bash=true\ndotnet=false\npackages=["mcp-bridge"]')" ] \
    || { echo "FAIL: a non-ASCII file in mcp-bridge must select it, got: $got"; exit 1; }
  echo "PASS: test_non_ascii_filename_in_a_package_selects_the_package"
}

test_a_stacked_pr_runs_everything() {
  local repo base got
  repo="$(scratch_repo)"; base="$(git -C "$repo" rev-parse HEAD)"
  printf '# more\n' >> "$repo/docs/readme.md"
  git -C "$repo" add -A && git -C "$repo" commit -qm docs
  # Based on another feature branch, not the default one: after the parent merges,
  # GitHub retargets the PR without a new run, so the filter must not narrow here.
  got="$(cd "$repo" && main pull_request "$base" feat/parent trunk)"
  rm -rf "$repo"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")" ] \
    || { echo "FAIL: a PR into a non-default branch must run everything, got: $got"; exit 1; }
  echo "PASS: test_a_stacked_pr_runs_everything"
}

test_a_pr_with_an_unknown_default_branch_runs_everything() {
  local got
  got="$(main pull_request deadbeef feat/x "")"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")" ] \
    || { echo "FAIL: an unknown default branch must fail closed, got: $got"; exit 1; }
  echo "PASS: test_a_pr_with_an_unknown_default_branch_runs_everything"
}

test_non_pr_event_runs_everything() {
  local got
  got="$(main push "")"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")" ] \
    || { echo "FAIL: push event should run everything, got: $got"; exit 1; }
  echo "PASS: test_non_pr_event_runs_everything"
}

test_pr_without_base_fails_closed() {
  local got
  got="$(main pull_request "")"
  [ "$got" = "$(printf 'bash=true\ndotnet=true\npackages=%s' "$ALL")" ] \
    || { echo "FAIL: a PR with no base sha should run everything, got: $got"; exit 1; }
  echo "PASS: test_pr_without_base_fails_closed"
}

test_non_pr_event_runs_everything
test_pr_without_base_fails_closed
test_rename_out_of_a_package_selects_the_package
test_non_ascii_filename_in_a_package_selects_the_package
test_a_stacked_pr_runs_everything
test_a_pr_with_an_unknown_default_branch_runs_everything
