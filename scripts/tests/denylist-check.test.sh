#!/usr/bin/env bash
# Tests for scripts/denylist-check.sh: the list is injected through AW_DENYLIST (file:<abs path> or env:<VAR>), hits print
# only file:line and a count, never the matched words; every bad pointer or list fails closed (exit 2). The words here
# are synthetic.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

HERE="$(cd "$(dirname "$0")" && pwd)"
SCRIPT="$HERE/../denylist-check.sh"
PASS=0
FAIL=0
T="$(mktemp -d "${TMPDIR:-/tmp}/denylist-check-test.XXXXXX")"
trap 'rm -rf "$T"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
# run <pointer> <args>... runs with AW_DENYLIST=<pointer>; runu <args>... runs with AW_DENYLIST unset.
run()  { local ptr="$1"; shift; set +e; OUT="$(AW_DENYLIST="$ptr" "$BASH" "$SCRIPT" "$@" 2>&1)"; CODE=$?; set -e; }
runu() { set +e; OUT="$(env -u AW_DENYLIST "$BASH" "$SCRIPT" "$@" 2>&1)"; CODE=$?; set -e; }
expect() { # expect <name> <code> <substring>...
  local name="$1" code="$2" good=1 s; shift 2
  [ "$CODE" = "$code" ] || good=0
  for s in "$@"; do case "$OUT" in *"$s"*) ;; *) good=0 ;; esac; done
  if [ "$good" = 1 ]; then ok "$name"; else fail "$name (exit $CODE, want $code)"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi
}
refuse() { # refuse <name> <substring>... — OUT contains none of them (case-insensitive)
  local name="$1" s; shift
  for s in "$@"; do
    if printf '%s' "$OUT" | has -iF -- "$s"; then fail "$name (printed '$s')"; printf '%s\n' "$OUT" | sed 's/^/       | /'; return; fi
  done
  ok "$name"
}

printf '# team keys and names (synthetic)\n\nZQX-\n  quokka-lattice  \nexample-ws.slack.test\r\n' > "$T/deny.txt"
chmod 600 "$T/deny.txt"
printf 'title: fix the pane split\nbody line two\n' > "$T/clean.md"
printf 'line one\nsee zqx-482 for details\nnothing\nthe Quokka-Lattice project and ZQX-9\nEXAMPLE-WS.slack.test\n' > "$T/hit.md"
F="file:$T/deny.txt"

echo "denylist-check.sh: file: pointer"

run "$F" "$T/clean.md"
expect "a clean file passes with a zero count" 0 "denylist-check: 0 hits in 1 file"

run "$F" "$T/hit.md" "$T/clean.md"
expect "hits print file:line per matching line and the count" 1 "$T/hit.md:2" "$T/hit.md:4" "$T/hit.md:5" "denylist-check: 3 hits in 2 files"
refuse "hits never print the matched words or the line text" "zqx" "quokka" "lattice" "example-ws" "details" "project"
case "$OUT" in *"$T/clean.md"*) fail "a clean file is not listed"; printf '%s\n' "$OUT" | sed 's/^/       | /' ;; *) ok "a clean file is not listed" ;; esac

cp "$T/deny.txt" "$T/deny-0400.txt"; chmod 400 "$T/deny-0400.txt"
run "file:$T/deny-0400.txt" "$T/hit.md"
expect "mode 0400 (stricter than 0600) is accepted" 1 "denylist-check: 3 hits"

echo "denylist-check.sh: env: pointer"

export DL_ENV="$(printf '# synthetic list\r\nZQX-\r\n\r\n   \n  quokka-lattice  \r\nexample-ws.slack.test\r\n')"
run "env:DL_ENV" "$T/hit.md" "$T/clean.md"
expect "env: reads newline-separated entries, with CRLF, comments and blanks" 1 "$T/hit.md:2" "$T/hit.md:4" "$T/hit.md:5" "denylist-check: 3 hits in 2 files"
refuse "env: hits never print the matched words or the line text" "zqx" "quokka" "lattice" "example-ws" "details" "project"
run "env:DL_ENV" "$T/clean.md"
expect "env: a clean file passes" 0 "denylist-check: 0 hits in 1 file"

echo "denylist-check.sh: refusals (all exit 2, never an entry in the output)"

runu "$T/clean.md"
expect "AW_DENYLIST unset fails closed" 2 "AW_DENYLIST"
run "" "$T/clean.md"
expect "AW_DENYLIST empty fails closed" 2 "AW_DENYLIST"

runu "$T/deny.txt" "$T/clean.md"
expect "old form (denylist path first, AW_DENYLIST unset) is refused with a pointer to AW_DENYLIST" 2 "set AW_DENYLIST"
refuse "the old form never reads the list" "zqx" "quokka" "lattice"

run "$T/deny.txt" "$T/clean.md"
expect "a bare path is refused" 2 "file:<absolute path>" "env:<VAR>"
run "https://example.test/list" "$T/clean.md"
expect "an unknown scheme is refused" 2 "file:<absolute path>"
run "file:deny.txt" "$T/clean.md"
expect "file: with a relative path is refused" 2 "absolute"
run "file:" "$T/clean.md"
expect "file: with no path is refused" 2 "absolute"

cp "$T/deny.txt" "$T/deny-0644.txt"; chmod 644 "$T/deny-0644.txt"
run "file:$T/deny-0644.txt" "$T/hit.md"
expect "mode 0644 is refused" 2 "0600"
refuse "a refused mode never reads the list" "zqx" "quokka" "lattice" "hit"
cp "$T/deny.txt" "$T/deny-0640.txt"; chmod 640 "$T/deny-0640.txt"
run "file:$T/deny-0640.txt" "$T/hit.md"
expect "mode 0640 is refused" 2 "0600"
cp "$T/deny.txt" "$T/deny-0700.txt"; chmod 700 "$T/deny-0700.txt"
run "file:$T/deny-0700.txt" "$T/hit.md"
expect "mode 0700 (an execute bit) is refused" 2 "0600"

run "$F" "$T/deny.txt"
expect "the list itself named as an input file is refused" 2 "AW_DENYLIST"
mkdir "$T/fakebin"; printf '#!/bin/sh\necho 99999\n' > "$T/fakebin/id"; chmod 755 "$T/fakebin/id"
set +e; OUT="$(PATH="$T/fakebin:$PATH" AW_DENYLIST="$F" "$BASH" "$SCRIPT" "$T/clean.md" 2>&1)"; CODE=$?; set -e
expect "a file owned by another user is refused" 2 "owned by the current user"

run "file:$T/nope.txt" "$T/clean.md"
expect "a missing file is refused" 2 "denylist" "missing"
mkdir "$T/dir"; chmod 700 "$T/dir"
run "file:$T/dir" "$T/clean.md"
expect "a directory is refused" 2 "denylist" "regular file"

: > "$T/empty.txt"; chmod 600 "$T/empty.txt"
run "file:$T/empty.txt" "$T/clean.md"
expect "an empty file fails closed" 2 "no entries"
printf '# only a comment\n\n   \n' > "$T/comments.txt"; chmod 600 "$T/comments.txt"
run "file:$T/comments.txt" "$T/clean.md"
expect "a file with only comments and blanks fails closed" 2 "no entries"

run "env:DL_UNSET_VAR" "$T/clean.md"
expect "env: an unset variable fails closed" 2 "DL_UNSET_VAR"
export DL_EMPTY=""
run "env:DL_EMPTY" "$T/clean.md"
expect "env: an empty variable fails closed" 2 "DL_EMPTY"
export DL_COMMENTS="$(printf '# nothing\r\n\r\n  \n')"
run "env:DL_COMMENTS" "$T/clean.md"
expect "env: only comments and blanks fails closed" 2 "no entries"
run "env:AW_DENYLIST" "$T/clean.md"
expect "env: naming AW_DENYLIST itself is refused" 2 "AW_DENYLIST"
run "env:ptr" "$T/hit.md"
expect "env: cannot read the script's own variables" 2 "not exported"
DL_LOCAL="ZQX-"
run "env:DL_LOCAL" "$T/hit.md"
expect "env: a variable that is set but not exported fails closed" 2 "not exported"
ln -s "$T/deny.txt" "$T/link-ok.txt"; ln -s "$T/deny-0644.txt" "$T/link-open.txt"
run "file:$T/link-ok.txt" "$T/hit.md"
expect "file: a link to a 0600 file is judged on its target and accepted" 1 "denylist-check: 3 hits"
run "file:$T/link-open.txt" "$T/hit.md"
expect "file: a link to a 0644 file is refused on the target's mode" 2 "0600"
run "env:" "$T/clean.md"
expect "env: with no name is refused" 2 "env:<VAR>"
run "env:not a name" "$T/clean.md"
expect "env: with an invalid variable name is refused" 2 "env:<VAR>"

echo "denylist-check.sh: inputs and entries"

run "$F" "$T/clean.md" "$T/missing.md"
expect "a missing input file is an error, not a pass" 2 "$T/missing.md" "not a readable file"

run "$F"
expect "no input files is a usage error" 2 "usage: scripts/denylist-check.sh <file>..."
runu
expect "no arguments is a usage error" 2 "usage:"

printf 'a\nb line with -e and .* chars\n' > "$T/regex.md"; printf '.*\n-e\n' > "$T/deny-regex.txt"; chmod 600 "$T/deny-regex.txt"
run "file:$T/deny-regex.txt" "$T/regex.md"
expect "entries are fixed strings, not patterns or options" 1 "$T/regex.md:2" "denylist-check: 1 hit in 1 file"

echo
echo "denylist-check: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
