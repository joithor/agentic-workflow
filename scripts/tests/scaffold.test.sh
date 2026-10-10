#!/usr/bin/env bash
# Tests for the C# scaffold generators (scripts/scaffold/new-package.mjs, new-slice.mjs): inputs, refusals, the
# files and edits they write, and revert on failure. No dotnet here; scripts/scaffold/e2e.sh builds and tests a
# scaffolded package (CI's scaffold job).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
node "$HERE/scaffold.unit.mjs"
