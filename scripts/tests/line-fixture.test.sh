#!/usr/bin/env bash
# Tests for the line fixture (tests/fixtures/line, line spec 10.3, phase LN-F). Run: bash scripts/tests/line-fixture.test.sh
# Needs git and node. No dotnet, no sindri, no network: it runs the fixture's own checks (selfcheck.mjs), proves each
# check can fail by breaking a copy of the fixture, and builds the fixture's git history twice in temp dirs. The checks
# that need the later phases (design check --facts, plan check --facts, aw-packets validate of the gold plan) are the
# remaining owner proof (tests/fixtures/line/README.md).
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FX="$ROOT/tests/fixtures/line"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'find "$WORK" -delete 2>/dev/null' EXIT
mkdir -p "$WORK/a" "$WORK/b"
# The canary phrase and a scenario's text are read from the hidden sets, never written here (they must appear nowhere else).
PHRASE="$(sed -n 's/.*"phrase": "\([^"]*\)".*/\1/p' "$FX/holdout/expiry/P1.set.json")"
LEAK_TEXT="$(node -p 'JSON.parse(require("fs").readFileSync(process.argv[1],"utf8")).scenarios.find(s=>s.id==="P2-H1").expected' "$FX/holdout/expiry/P2.set.json")"

# --- the fixture's own checks ---
out="$(node "$FX/selfcheck.mjs" 2>&1)"; rc=$?
check "selfcheck passes" "$rc" "0"
[ "$rc" = 0 ] || echo "$out"
node "$FX/selfcheck.mjs" extra >/dev/null 2>&1; check "selfcheck: an unknown argument exits 2" "$?" "2"
node "$FX/selfcheck.mjs" diff-scope >/dev/null 2>&1; check "selfcheck diff-scope with no arguments exits 2" "$?" "2"

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

# --- each check can fail: break a copy of the fixture (line and scoping side by side) ---
# sedi <script> <file>...: an in-place sed that works with GNU and BSD sed alike (CI runs on macOS).
sedi() { local script="$1" f; shift; for f in "$@"; do sed "$script" "$f" > "$f.sedi" && mv "$f.sedi" "$f" || return 1; done; }
mutate() { # mutate <name> <expect-text> <command run inside the copy of tests/fixtures/line>
  local name="$1" expect="$2" cmd="$3" copy="$WORK/m-$1"
  mkdir -p "$copy" && cp -R "$ROOT/tests/fixtures/line" "$copy/line" && cp -R "$ROOT/tests/fixtures/scoping" "$copy/scoping"
  ( cd "$copy/line" && eval "$cmd" ) >/dev/null 2>&1
  # a mutation that edits nothing proves nothing (BSD sed accepts GNU-only scripts and changes nothing)
  if diff -rq "$ROOT/tests/fixtures/line" "$copy/line" >/dev/null && diff -rq "$ROOT/tests/fixtures/scoping" "$copy/scoping" >/dev/null; then
    echo "not ok - the mutation $name changed no file"; fail=1; return
  fi
  local got rc; got="$(node "$copy/line/selfcheck.mjs" 2>&1)"; rc=$?
  check "a broken fixture fails selfcheck: $name" "$rc" "1"
  if printf '%s\n' "$got" | grep '^not ok - ' | grep -qF -- "$expect"; then echo "ok - ...and a failing check names: $expect"; else echo "not ok - expected a failing check to name: $expect"; printf '%s\n' "$got" | grep -A1 '^not ok' | head -6; fail=1; fi
}
# an unmutated copy passes clean, so a mutation failing means something
mkdir -p "$WORK/clean" && cp -R "$FX" "$WORK/clean/line" && cp -R "$ROOT/tests/fixtures/scoping" "$WORK/clean/scoping"
clean_out="$(node "$WORK/clean/line/selfcheck.mjs" 2>&1)"; check "an unmutated copy of the fixture passes selfcheck (exit 0)" "$?" "0"
# and it really ran: a selfcheck that never reaches main also exits 0, with no output
if [[ "${clean_out##*$'\n'}" =~ ^[0-9]+/[0-9]+\ checks\ pass$ ]]; then echo "ok - ...and it ran its checks"; else echo "not ok - an unmutated copy printed no checks: '$clean_out'"; fail=1; fi
mutate hops-integer "lenses.json agrees with coverage.json" "sedi 's/\"hops\": \\[\\]/\"hops\": 0/' gold/scope/expiry/lenses.json"
mutate p2-p3-share-a-file "PKT-SCOPE-002" "sedi 's|\"web/api-client.ts\"|\"src/sharing/routes.ts\"|' gold/plan/expiry/tasks.json"
mutate canary-leaked-to-readme "canary phrase appears" "echo '$PHRASE' >> README.md"
mutate hidden-text-leaked "no hidden scenario text" "echo '$LEAK_TEXT' >> gold/design/expiry/spec.md"
mutate formal-pack-copies-plain "two style packs really differ" "cp packs/style/plain/pack.json packs/style/formal/pack.json && sedi 's/\"id\": \"plain\"/\"id\": \"formal\"/' packs/style/formal/pack.json"
mutate planted-red-passes-removed "PL-RED" "sedi 's/\"outcome\": \"passed\"/\"outcome\": \"failed\"/' gold/build/red.json"
mutate secret-shaped-file "secret-shaped" "printf 'k = \"ghp_%s\"\\n' abcdefghijklmnopqrstuvwxyz0123456789 > notes.txt"
mutate pl-lens-defect-not-in-spec "PL-LENS" "sedi 's/UTC instant/UTC moment/' gold/lens/design-defect.json"
mutate pl-scope-inside-allowed "PL-SCOPE" "sedi 's|\"src/digest/digest.ts\"|\"src/sharing/links.ts\"|' planted.json"
mutate pl-canary-not-in-message "PL-CANARY" "sedi 's/see \$CANARY/see nothing/' history/build.sh"
mutate pl-leak-report-clean "PL-LEAK" "node -e 'const f=\"gold/prove/holdout-result.leaky.json\",fs=require(\"fs\"),j=JSON.parse(fs.readFileSync(f,\"utf8\"));j.results[0].observed=\"Status 410.\";fs.writeFileSync(f,JSON.stringify(j,null,2))'"
mutate related-path-not-a-cascade-path "related is a cascade lens" "sedi 's/reads action:audit.report/reads action:export.run/' gold/scope-trace/expiry.related-trace.json"
mutate hops-not-hop-kinds "hop-kind arrays" "sedi 's/\"dependency\"/\"walk\"/' gold/scope/expiry/lenses.json gold/scope/expiry/coverage.json"
mutate malformed-gold-is-a-check "crashed" "echo '{' > gold/design/expiry/signals.json"
mutate design-parent-not-the-frozen-scope "index.json: kind design, parent is the frozen" "node -e 'const f=process.argv[1],fs=require(\"fs\");fs.writeFileSync(f,fs.readFileSync(f,\"utf8\").replace(/sha256:[0-9a-f]{64}/,\"sha256:\"+\"0\".repeat(64)))' gold/design/expiry/index.json"
mutate plan-index-self-set "index.json: kind plan" "sedi 's/\"self\": null/\"self\": \"sha256:0000000000000000000000000000000000000000000000000000000000000000\"/' gold/plan/expiry/index.json"

# --- the history: reproducible, with the planted items ---
h1="$(bash "$FX/history/build.sh" "$WORK/a")"; check "build.sh exits 0 (first)" "$?" "0"
h2="$(bash "$FX/history/build.sh" "$WORK/b")"; check "build.sh exits 0 (second)" "$?" "0"
check "the same head sha twice" "$h1" "$h2"
GOLD="$FX/history/golden-head.txt"
check "main is the scoping fixture's pinned head (history/golden-head.txt line 1)" "$h1" "$(sed -n 1p "$GOLD")"
R="$WORK/a/fixture-notes"
n=2
for t in p1-red p1-green p1-out-of-scope p1-canary; do
  check "tag line/$t is the pinned commit" "$(git -C "$R" rev-parse "line/$t")" "$(sed -n "${n}p" "$GOLD")"
  n=$((n + 1))
done
check "the branch line/p1 ends at the canary commit" "$(git -C "$R" rev-parse line/p1)" "$(sed -n 5p "$GOLD")"
check "the branch is four commits off main" "$(git -C "$R" rev-list --count main..line/p1)" "4"
check "main is checked out and the tree is clean" "$(git -C "$R" rev-parse --abbrev-ref HEAD) $(git -C "$R" status --porcelain | wc -l | tr -d ' ')" "main 0"
bash "$FX/history/build.sh" "$WORK/a" >/dev/null 2>&1; check "build.sh refuses an existing destination (exit 2)" "$?" "2"
bash "$FX/history/build.sh" >/dev/null 2>&1; check "build.sh with no argument exits 2" "$?" "2"

# PL-SCOPE: the build is inside allowedFiles up to the green commit, and one file outside it afterwards
node "$FX/selfcheck.mjs" diff-scope "$R" main line/p1-green P1 >/dev/null; check "P1's build to the green commit stays inside allowedFiles" "$?" "0"
out="$(node "$FX/selfcheck.mjs" diff-scope "$R" main line/p1-out-of-scope P1)"; rc=$?
check "the out-of-scope range is caught: exit 1" "$rc" "1"
check "...and it names src/digest/digest.ts only" "$out" "src/digest/digest.ts"
out="$(node "$FX/selfcheck.mjs" diff-scope "$R" main line/p1-out-of-scope P4)"
check "for P4 (which owns the digest) the same range names the P1 files instead" "$(printf '%s' "$out" | tr '\n' ' ')" "migrations/006_link_expiry.sql src/sharing/links.ts src/sharing/routes.ts"

# PL-RED: the planted test is in the red commit and the red commit changes tests only
check "the red commit changes only test files" "$(git -C "$R" diff --name-only main line/p1-red)" "tests/expiry.test.ts"
check "the red commit holds the tagged test" "$(git -C "$R" show line/p1-red:tests/expiry.test.ts | grep -c 'PLANTED PL-RED')" "1"

# PL-CANARY: the P1 phrase is in exactly one commit message, and in no diff
check "the P1 canary phrase is in exactly one commit message" "$(git -C "$R" log --all --format=%B | grep -c -F "$PHRASE")" "1"
check "...the last commit of line/p1" "$(git -C "$R" log -1 --format=%s line/p1 | grep -c -F "$PHRASE")" "1"
check "the phrase is in no diff of the branch" "$(git -C "$R" log -p main..line/p1 | grep -c -F "+$PHRASE")" "0"
others=0
for p in P2 P3 P4; do
  x="$(sed -n 's/.*"phrase": "\([^"]*\)".*/\1/p' "$FX/holdout/expiry/$p.set.json")"
  others=$((others + $(git -C "$R" log --all --format=%B | grep -c -F "$x")))
done
check "no other phase's phrase is in the history" "$others" "0"

# PL-LEAK through the CLI
node "$FX/selfcheck.mjs" leak-check "$FX/gold/prove/holdout-result.leaky.json" P2 >/dev/null; check "leak-check: the leaky report exits 1" "$?" "1"
node "$FX/selfcheck.mjs" leak-check "$FX/gold/prove/holdout-result.clean.json" P2 >/dev/null; check "leak-check: the clean report exits 0" "$?" "0"

# DSN-STYLE-001 through the CLI, both ways
node "$FX/selfcheck.mjs" style-check "$FX/gold/design/expiry/spec.md" plain >/dev/null; check "style-check: the gold spec passes plain" "$?" "0"
node "$FX/selfcheck.mjs" style-check "$FX/gold/design/expiry/spec.md" formal >/dev/null; check "style-check: the gold spec fails formal" "$?" "1"
node "$FX/selfcheck.mjs" style-check "$FX/gold/design/expiry.formal.spec.md" formal >/dev/null; check "style-check: the formal rendering passes formal" "$?" "0"
node "$FX/selfcheck.mjs" style-check "$FX/gold/design/expiry.formal.spec.md" plain >/dev/null; check "style-check: the formal rendering fails plain" "$?" "1"

check "no fixture file holds a GitHub-shaped token" "$(grep -rE 'gh[posur]_[A-Za-z0-9_]{36,}' "$FX" | wc -l | tr -d ' ')" "0"

[ "$fail" = 0 ] && echo "all passed" || echo "FAILED"
exit "$fail"
