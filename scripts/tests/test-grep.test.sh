#!/usr/bin/env bash
# Tests for scripts/lib/test-grep.sh: has() reads all its input, so a producer never gets SIGPIPE under pipefail.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

FAIL=0
# The first line matches and the stream is far larger than a pipe buffer: `seq 1000000 | grep -q -x 1` exits at once,
# seq gets SIGPIPE (141) and the pipeline fails under pipefail. has() keeps reading, so seq always finishes.
for i in $(seq 1 20); do
  if ! seq 1000000 | has -x 1; then echo "FAIL: has -x 1 failed on run $i"; FAIL=1; fi
  if ! seq 1000000 | has -E '^1$'; then echo "FAIL: has -E failed on run $i"; FAIL=1; fi
  if seq 1000000 | has -x nomatch; then echo "FAIL: has matched a missing pattern on run $i"; FAIL=1; fi
done

# A match is reported by status and nothing is printed.
out="$(printf 'a\nb\n' | has b && echo yes)"
[ "$out" = "yes" ] || { echo "FAIL: has printed or failed: '$out'"; FAIL=1; }

[ "$FAIL" = 0 ] || exit 1
echo "PASS: has() reads all input (20 runs)"
