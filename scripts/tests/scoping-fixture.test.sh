#!/usr/bin/env bash
# Tests for the scoping fixture (tests/fixtures/scoping, spec 10.3, phase SC-F). Run: bash scripts/tests/scoping-fixture.test.sh
# Needs git and node. No dotnet, no sindri, no network: it builds the fixture's git history twice in temp dirs and runs
# the fixture's own checks (tests/fixtures/scoping/selfcheck.mjs). The map import and the probe run need a Sindri
# serve and a human at a terminal; they are the owner proof in the PR (README.md in the fixture).
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FX="$ROOT/tests/fixtures/scoping"
fail=0

# The fake token, in two parts so this file holds no secret-shaped string (the same rule as history/build.sh).
FAKE="$(printf '%s%s' 'ghp_' 'FIXTURE0FAKE0TOKEN0NOT0REAL0DONTUSE0')"

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
mkdir -p "$WORK/a" "$WORK/b" "$WORK/c"

# --- the fixture's own checks (gold shape and keys, answers, briefs, planted items, no secret-shaped file, gold scoring) ---
out="$(node "$FX/selfcheck.mjs" 2>&1)"; rc=$?
check "selfcheck passes" "$rc" "0"
[ "$rc" = 0 ] || echo "$out"
node "$FX/selfcheck.mjs" extra >/dev/null 2>&1; check "selfcheck: an unknown argument exits 2" "$?" "2"
# A loaded gate gives new pipes one 4 KB page (fs.pipe-user-pages-soft): the selfcheck must still print every line, not
# drop what it hasn't flushed when it exits. Linux only (F_SETPIPE_SZ), and needs python3.
if [ "$(uname -s)" = Linux ] && command -v python3 >/dev/null; then
  tail1="$(python3 -I -c '
import fcntl, os, subprocess, sys, time
r, w = os.pipe(); fcntl.fcntl(w, 1031, 4096)
p = subprocess.Popen(["node", sys.argv[1]], stdout=w, stderr=w); os.close(w); time.sleep(1)
out = b"".join(iter(lambda: os.read(r, 65536), b"")); p.wait()
print(out.decode().rstrip().splitlines()[-1])' "$FX/selfcheck.mjs")"
  if [[ "$tail1" =~ ^[0-9]+/[0-9]+\ checks\ pass$ ]]; then echo "ok - through a 4 KB pipe with a slow reader the selfcheck prints every line"; else
    echo "not ok - through a 4 KB pipe the selfcheck's output was cut: last line '$tail1'"; fail=1; fi
fi
printf '["surface:route/links-create"]' > "$WORK/scope.json"
out="$(node "$FX/selfcheck.mjs" score "$WORK/scope.json" expiry --json 2>&1)"; check "score exits 0" "$?" "0"
case "$out" in *'"precision":1'*) echo "ok - score: one gold key alone has precision 1" ;; *) echo "not ok - score output: $out"; fail=1 ;; esac

# --- the history: reproducible, with the planted items ---
h1="$(bash "$FX/history/build.sh" "$WORK/a")"; check "build.sh exits 0 (first)" "$?" "0"
h2="$(bash "$FX/history/build.sh" "$WORK/b")"; check "build.sh exits 0 (second)" "$?" "0"
check "the same head sha twice" "$h1" "$h2"
check "the head is the pinned one (history/golden-head.txt)" "$h1" "$(sed -n 1p "$FX/history/golden-head.txt")"
R="$WORK/a/fixture-notes"
check "the expiry branch is the pinned one" "$(git -C "$R" rev-parse feat/link-expiry)" "$(sed -n 2p "$FX/history/golden-head.txt")"
check "about thirty commits on all branches" "$(git -C "$R" rev-list --all --count)" "28"
check "five merged pull requests" "$(git -C "$R" log main --merges --format=%s | grep -c '^Merge pull request #')" "5"
check "the past PR adds link revocation" "$(git -C "$R" log main --merges --format=%s | grep -c 'nts-130-link-revocation')" "1"
check "the revocation PR changes the link code" "$(git -C "$R" diff --name-only 'main~1^1' 'main~1^2' | grep -c -E 'src/sharing/(links|routes).ts|migrations/005_link_revocation.sql')" "3"
check "the head tree is repo/ (build.sh checks it byte for byte)" "$(diff -r -x .git "$FX/repo" "$R" >/dev/null 2>&1; echo $?)" "0"
check "the working tree is clean" "$(git -C "$R" status --porcelain | wc -l | tr -d ' ')" "0"
check "main is checked out" "$(git -C "$R" rev-parse --abbrev-ref HEAD)" "main"

# the planted fake token: in one commit message and one diff hunk, in no file at the head, and in no fixture file
check "the fake token is in exactly one commit message" "$(git -C "$R" log main --format=%B | grep -c -F "$FAKE")" "1"
check "the fake token is in exactly one added diff line" "$(git -C "$R" log -p main | grep -c -F "+const DEBUG_KEY = \"$FAKE\"")" "1"
check "the fake token is in no file at the head" "$(git -C "$R" grep -c -F 'FIXTURE0FAKE0TOKEN' main -- . | wc -l | tr -d ' ')" "0"
check "no fixture file holds a GitHub-shaped token" "$(grep -rE 'gh[posur]_[A-Za-z0-9_]{36,}' "$FX" | wc -l | tr -d ' ')" "0"

# the partial diff: only the column and the type
check "the expiry branch changes two files" "$(git -C "$R" diff --name-only main feat/link-expiry | sort | tr '\n' ' ')" "migrations/006_link_expiry.sql src/sharing/links.ts "
check "the expiry branch is one commit off main" "$(git -C "$R" rev-list --count main..feat/link-expiry)" "1"

# usage
bash "$FX/history/build.sh" "$WORK/a" >/dev/null 2>&1; check "build.sh refuses an existing destination (exit 2)" "$?" "2"
bash "$FX/history/build.sh" >/dev/null 2>&1; check "build.sh with no argument exits 2" "$?" "2"

# a caller's GIT_ variables do not change the sha
h3="$(GIT_AUTHOR_NAME=someone GIT_COMMITTER_DATE=2001-01-01T00:00:00Z GIT_CONFIG_COUNT=1 GIT_CONFIG_KEY_0=core.autocrlf GIT_CONFIG_VALUE_0=true bash "$FX/history/build.sh" "$WORK/c")"
check "the caller's GIT_* variables do not change the head" "$h3" "$h1"

[ "$fail" = 0 ] && echo "all passed" || echo "FAILED"
exit "$fail"
