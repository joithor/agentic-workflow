#!/usr/bin/env bash
# Tests for scripts/binaries-lock.sh check: the repo's binaries.lock passes, and each malformed fixture fails with its
# code. fetch is not run here (it downloads); its platform lookup is covered through a missing-row case.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

SCRIPT="$(cd "$(dirname "$0")/.." && pwd)/binaries-lock.sh"
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/binaries-lock-test.XXXXXX")"
trap 'find "$TMPROOT" -delete' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

run() { set +e; OUT="$(bash "$SCRIPT" "$@" 2>&1)"; RC=$?; set -e; }
expect() {   # <label> <rc> [<regex>]
  if [ "$RC" -ne "$2" ]; then fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  if [ -n "${3:-}" ] && ! printf '%s\n' "$OUT" | has -E -- "$3"; then fail "$1: output lacks /$3/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  ok "$1"
}

SHA=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
row() {   # <name> <version> <platform> [sha] [url] [path]
  printf '%s %s %s %s %s %s\n' "$1" "$2" "$3" "${4:-$SHA}" \
    "${5:-https://github.com/o/r/releases/download/v$2/$1_$2_$3.tar.gz}" "${6:-$1}"
}
four() { for p in darwin-arm64 darwin-x64 linux-x64 linux-arm64; do row "$1" "$2" "$p"; done; }
lock() { local f="$TMPROOT/$1.lock"; cat >"$f"; echo "$f"; }

echo "binaries-lock check"

run check --file "$REPO/binaries.lock"
expect "the repo's binaries.lock passes" 0

f="$(printf '# comment\n\n' | lock comments-only)"
run check --file "$f"
expect "a file with no rows fails" 1 'BIN-LOCK-005'

f="$(four tool 1.2.3 | lock good)"
run check --file "$f"
expect "four well-formed rows pass" 0

f="$( { four tool 1.2.3; echo "tool 1.2.3 darwin-arm64 $SHA"; } | lock fields)"
run check --file "$f"
expect "a short row fails" 1 'BIN-LOCK-001'

f="$( { four tool 1.2.3 | sed 's/^tool 1.2.3 linux-x64/tool 1.2 linux-x64/'; } | lock version)"
run check --file "$f"
expect "a non x.y.z version fails" 1 'BIN-LOCK-002 .*version "1.2"'

f="$( { row tool 1.2.3 darwin-arm64; row tool 1.2.3 darwin-x64; row tool 1.2.3 linux-x64; row tool 1.2.3 windows-x64; } | lock platform)"
run check --file "$f"
expect "an unknown platform fails" 1 'BIN-LOCK-002 .*platform "windows-x64"'
expect "and the missing platform is named" 1 'BIN-LOCK-004 tool: no linux-arm64 row'

f="$( { row tool 1.2.3 darwin-arm64 ABCDEF; row tool 1.2.3 darwin-x64; row tool 1.2.3 linux-x64; row tool 1.2.3 linux-arm64; } | lock sha)"
run check --file "$f"
expect "a bad sha256 fails" 1 'BIN-LOCK-002 .*sha256'

f="$( { row tool 1.2.3 darwin-arm64 "$SHA" http://example.com/tool_1.2.3.tar.gz; row tool 1.2.3 darwin-x64; row tool 1.2.3 linux-x64; row tool 1.2.3 linux-arm64; } | lock url)"
run check --file "$f"
expect "a non-github url fails" 1 'BIN-LOCK-002 .*url'

f="$( { row tool 1.2.3 darwin-arm64 "$SHA" https://github.com/o/r/releases/download/latest/tool.tar.gz; row tool 1.2.3 darwin-x64; row tool 1.2.3 linux-x64; row tool 1.2.3 linux-arm64; } | lock url-version)"
run check --file "$f"
expect "a url without the version fails" 1 'BIN-LOCK-002 .*carry version'

f="$( { row tool 1.2.3 darwin-arm64 "$SHA" "" ../escape; row tool 1.2.3 darwin-x64; row tool 1.2.3 linux-x64; row tool 1.2.3 linux-arm64; } | lock path)"
run check --file "$f"
expect "a path escaping the archive fails" 1 'BIN-LOCK-002 .*path'

f="$( { four tool 1.2.3 | sed 's/^tool 1.2.3 linux-arm64/tool 1.2.4 linux-arm64/'; } | lock versions)"
run check --file "$f"
expect "two versions of one name fail" 1 'BIN-LOCK-003'

f="$( { four tool 1.2.3; row tool 1.2.3 linux-x64; } | lock dup)"
run check --file "$f"
expect "a duplicated platform fails" 1 'BIN-LOCK-004 .*listed twice'

f="$(four tool 1.2.3 | lock fetch-missing)"
run fetch other --file "$f" --dest "$TMPROOT/bin"
expect "fetch of a name with no row is a usage error" 2 'no row for other'

f="$( { four tool 1.2.3; echo "tool 1.2.3 darwin-arm64"; } | lock fetch-bad)"
run fetch tool --file "$f" --dest "$TMPROOT/bin"
expect "fetch refuses a file that fails its check" 1 'fails its check'

echo
echo "binaries-lock: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
