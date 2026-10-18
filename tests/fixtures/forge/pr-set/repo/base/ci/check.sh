#!/usr/bin/env bash
# The CI script: the unit tests, then a smoke run.
set -euo pipefail
cd "$(dirname "$0")/.."
npm test
printf 'one two\n' | node src/index.js /dev/stdin
