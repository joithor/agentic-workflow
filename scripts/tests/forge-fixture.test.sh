#!/usr/bin/env bash
# Tests for scripts/forge-fixture.sh (PR babysitter spec, PB-X). Run: bash scripts/tests/forge-fixture.test.sh
# Runs the real aw-forge-fake (dotnet build, once) over the file-backed fake forge in a temp home. The --github mode is the
# owner's: only its refusals and its --dry-run are tested here, and no request leaves the box.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FIXTURE="$ROOT/scripts/forge-fixture.sh"
SET="$ROOT/tests/fixtures/forge/pr-set"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { # $1 name, $2 haystack, $3 needle
  case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac
}
lacks() { # $1 name, $2 haystack, $3 needle
  case "$2" in *"$3"*) echo "not ok - $1"; echo "  found: $3"; fail=1 ;; *) echo "ok - $1" ;; esac
}

WORK="$(mktemp -d)"
H="$WORK/tmp/forge-fixture"
cleanup() { FORGE_FIXTURE_HOME="$H" bash "$FIXTURE" down --all >/dev/null 2>&1; rm -r "$WORK"; }
trap cleanup EXIT
export TMPDIR="$WORK/tmp" AW_STATE_DIR="$WORK/state"
mkdir -p "$TMPDIR" "$AW_STATE_DIR"
export FORGE_FIXTURE_HOME="$H"
unset AW_FORGE_FAKE AW_FORGE_ADAPTER AW_FORGE_HOSTS AW_FORGE_TOKEN AW_FORGE_CREDENTIAL FORGE_FIXTURE_GITHUB_SCRATCH

command -v jq >/dev/null 2>&1 || { echo "not ok - jq is needed"; exit 1; }

# --- usage and refusals (nothing is built) ---
out="$(bash "$FIXTURE" 2>&1)"; check "no verb exits 2" "$?" "2"
contains "no verb prints usage" "$out" "forge-fixture.sh up --fake"
bash "$FIXTURE" --help >/dev/null 2>&1; check "--help exits 0" "$?" "0"
out="$(bash "$FIXTURE" sideways 2>&1)"; check "an unknown verb exits 2" "$?" "2"
contains "an unknown verb is named" "$out" "unknown verb: sideways"
out="$(bash "$FIXTURE" up 2>&1)"; check "up needs a mode (exit 2)" "$?" "2"
out="$(bash "$FIXTURE" up --fake nonsense 2>&1)"; check "an unknown variant exits 2" "$?" "2"
contains "an unknown variant is named" "$out" "unknown variant: nonsense"
out="$(FORGE_FIXTURE_HOME=/etc/forge-fixture bash "$FIXTURE" up --fake comments 2>&1)"; check "a home outside a temp folder is refused" "$?" "2"
contains "the guard names the rule" "$out" "must be under"
out="$(FORGE_FIXTURE_HOME="$TMPDIR/../elsewhere" bash "$FIXTURE" down 2>&1)"; check "a dotdot home is refused" "$?" "2"
out="$(bash "$FIXTURE" list 2>&1)"; check "list exits 0" "$?" "0"
for v in comments ciRed ciFlaky conflict stacked noDocs sourceFix foreignPush secret; do
  contains "list has $v" "$out" "$v"
done

# --- the --github mode: refusals and a dry run only ---
out="$(bash "$FIXTURE" up --github 2>&1)"; check "--github needs a repo (exit 2)" "$?" "2"
out="$(bash "$FIXTURE" up --github not-a-slug comments 2>&1)"; check "--github needs owner/name (exit 2)" "$?" "2"
out="$(bash "$FIXTURE" up --github acme/scratch comments 2>&1)"; check "--github without a token is refused (exit 2)" "$?" "2"
contains "--github names the missing token" "$out" "AW_FORGE_TOKEN"
out="$(AW_FORGE_TOKEN=tok-not-printed bash "$FIXTURE" up --github acme/scratch comments 2>&1)"; check "--github without the confirmation is refused (exit 2)" "$?" "2"
contains "--github names the confirmation" "$out" "FORGE_FIXTURE_GITHUB_SCRATCH"
lacks "--github never prints the token" "$out" "tok-not-printed"
out="$(AW_FORGE_TOKEN=tok-not-printed FORGE_FIXTURE_GITHUB_SCRATCH=other/repo bash "$FIXTURE" up --github acme/scratch comments 2>&1)"; check "--github with another confirmation is refused (exit 2)" "$?" "2"

# --- up --fake comments (the first call builds aw-forge-fake) ---
out="$(bash "$FIXTURE" up --fake comments 2>&1)"; rc=$?
check "up --fake comments exits 0" "$rc" "0"
link="$(printf '%s\n' "$out" | sed -n 's/^link: //p' | head -1)"
check "the link is a fixture pull request" "$link" "https://forge.test/fixture/comments/pull/1"
contains "up prints the answer key path" "$out" "expected: $H/comments/expected.json"
contains "up prints the workspace" "$out" "workspace: $H/comments/work"
contains "up prints the hand-off" "$out" "handoff: $H/comments/handoff"

forge() { AW_STATE_DIR="$H/state" dotnet "$H/bin/aw-forge-fake.dll" "$@"; }
threads="$(forge threads "$link" 2>&1)"
check "the threads command works" "$(printf '%s' "$threads" | jq 'length' 2>/dev/null)" "$(jq '.threads | length' "$SET/comments.json")"
exp="$H/comments/expected.json"
check "every judge type is in the answer key" \
  "$(jq -r --slurpfile v "$SET/variants.json" '[.threads[] | select(.expect.queue=="comment") | .expect.type] as $t | ($v[0].judgeTypes - $t) | length' "$exp")" "0"
check "every queue outcome is in the answer key" \
  "$(jq -r --slurpfile v "$SET/variants.json" '[.threads[].expect.queue] as $q | (($v[0].queueOutcomes + $v[0].notQueued) - $q) | length' "$exp")" "0"
check "every answer-key thread is a thread of the link" \
  "$(jq -r --argjson live "$threads" '[.threads[].key] - [$live[].key] | length' "$exp")" "0"
check "an allowlisted bot is a bot thread" "$(printf '%s' "$threads" | jq -r '[.[] | select(.author=="scanbot[bot]" and .isBot)] | length')" "1"
check "an unlisted bot is a second bot author" "$(printf '%s' "$threads" | jq -r '[.[] | select(.author=="relaybot[bot]" and .isBot)] | length')" "1"
check "an untrusted author has no association" "$(printf '%s' "$threads" | jq -r '[.[] | select(.author=="stranger-lou" and .association=="NONE")] | length')" "1"
check "one thread is resolved" "$(printf '%s' "$threads" | jq -r '[.[] | select(.resolved)] | length')" "1"
check "Sindri's own reply carries its marker" "$(printf '%s' "$threads" | jq -r '[.[] | select(.author=="aw-bot" and (.markers | index("fixture-own-reply")))] | length')" "1"
check "the edited comment carries the new text" "$(printf '%s' "$threads" | jq -r '[.[] | select(.body | startswith("Actually, --json"))] | length')" "1"
check "the answer key names the profile's bots" "$(jq -r '.profileBots[0]' "$exp")" "scanbot[bot]"
pull="$(forge pull "$link")"
check "comments: checks are passing" "$(printf '%s' "$pull" | jq -r .checks.state)" "Passing"
check "the head is the workspace's branch tip" "$(printf '%s' "$pull" | jq -r .head.sha)" "$(git -C "$H/comments/work" rev-parse feature/json-flag)"
check "the workspace is a git repo on the head" "$(git -C "$H/comments/work" rev-parse --abbrev-ref HEAD)" "feature/json-flag"
check "the hand-off's proven sha is the head" "$(jq -r .headSha "$H/comments/handoff/pr.json")" "$(printf '%s' "$pull" | jq -r .head.sha)"
check "the hand-off has no placeholder left" "$(grep -rlE '@[A-Z]+@' "$H/comments/handoff" "$H/comments/phase-packet.json" | wc -l | tr -d ' ')" "0"
check "the hand-off's pr.json is JSON" "$(jq -r .headBranch "$H/comments/handoff/pr.json")" "feature/json-flag"
check "the phase packet is JSON" "$(jq -r .id "$H/comments/phase-packet.json")" "fixture-line-P1"
check "docs/usage.md is stale: it lacks --json" "$(grep -c -- '--json' "$H/comments/work/docs/usage.md")" "0"
check "the code has --json" "$(grep -c -- '--json' "$H/comments/work/src/args.js")" "2"

bash "$FIXTURE" status >"$WORK/status" 2>&1; check "status exits 0 while up" "$?" "0"
contains "status lists the link" "$(cat "$WORK/status")" "$link"

# up twice replaces the variant (one pull request, not two)
bash "$FIXTURE" up --fake comments >/dev/null 2>&1
check "a repeat up leaves one pull request" "$(forge pull https://forge.test/fixture/comments/pull/1 | jq -r .status)" "Open"
forge pull https://forge.test/fixture/comments/pull/2 >/dev/null 2>&1; check "a repeat up does not open a second" "$?" "1"

# --- the other variants ---
bash "$FIXTURE" up --fake ciRed >"$WORK/o" 2>&1; check "up ciRed exits 0" "$?" "0"
l="$(sed -n 's/^link: //p' "$WORK/o" | head -1)"; p="$(forge pull "$l")"
check "ciRed: a check fails" "$(printf '%s' "$p" | jq -r .checks.state)" "Failing"
check "ciRed: the failing check is named" "$(printf '%s' "$p" | jq -r '.checks.failing[0]')" "unit"

bash "$FIXTURE" up --fake ciFlaky >"$WORK/o" 2>&1; check "up ciFlaky exits 0" "$?" "0"
l="$(sed -n 's/^link: //p' "$WORK/o" | head -1)"; p="$(forge pull "$l")"
check "ciFlaky: a check fails" "$(printf '%s' "$p" | jq -r .checks.state)" "Failing"
check "ciFlaky: the answer key says flaky" "$(jq -r .flaky "$H/ci-flaky/expected.json")" "true"

bash "$FIXTURE" up --fake conflict >"$WORK/o" 2>&1; check "up conflict exits 0" "$?" "0"
l="$(sed -n 's/^link: //p' "$WORK/o" | head -1)"
check "conflict: the forge reports a conflict" "$(forge pull "$l" | jq -r .mergeable)" "false"

bash "$FIXTURE" up --fake noDocs >"$WORK/o" 2>&1; check "up noDocs exits 0" "$?" "0"
check "noDocs: there is no docs folder" "$(test -e "$H/no-docs/work/docs" && echo yes || echo no)" "no"

bash "$FIXTURE" up --fake sourceFix >"$WORK/o" 2>&1; check "up sourceFix exits 0" "$?" "0"
sf="$H/source-fix/work"
check "sourceFix: the candidate fix branch exists" "$(git -C "$sf" rev-parse --verify -q fixture/source-fix >/dev/null && echo yes)" "yes"
check "sourceFix: the candidate changes source" "$(git -C "$sf" diff --name-only feature/json-flag fixture/source-fix)" "src/count.js"
contains "sourceFix: the candidate carries the trailer" "$(git -C "$sf" log -1 --format=%B fixture/source-fix)" "Sindri-Comment: "

bash "$FIXTURE" up --fake foreignPush >"$WORK/o" 2>&1; check "up foreignPush exits 0" "$?" "0"
l="$(sed -n 's/^link: //p' "$WORK/o" | head -1)"; p="$(forge pull "$l")"
proven="$(jq -r .headSha "$H/foreign-push/handoff/pr.json")"
check "foreignPush: the head moved past the proven sha" "$([ "$(printf '%s' "$p" | jq -r .head.sha)" != "$proven" ] && echo moved)" "moved"
check "foreignPush: the proven sha is in the history" "$(git -C "$H/foreign-push/work" merge-base --is-ancestor "$proven" feature/json-flag && echo yes)" "yes"
check "foreignPush: the pushed commit is a collaborator's" "$(git -C "$H/foreign-push/work" log -1 --format=%an feature/json-flag)" "reviewer-kai"
check "foreignPush: it changes non-test source" "$(git -C "$H/foreign-push/work" diff --name-only "$proven" feature/json-flag)" "src/format.js"

bash "$FIXTURE" up --fake stacked >"$WORK/o" 2>&1; check "up stacked exits 0" "$?" "0"
l="$(sed -n 's/^link: //p' "$WORK/o" | head -1)"
check "stacked: the PR sits on the base branch" "$(forge pull "$l" | jq -r .base.ref)" "feature/json-base"
check "stacked: the base PR is open" "$(forge pull "${l%/*}/1" | jq -r .status)" "Open"
bash "$FIXTURE" event merge-base --variant stacked >/dev/null 2>&1; check "event merge-base exits 0" "$?" "0"
check "stacked: the base PR merged mid-run" "$(forge pull "${l%/*}/1" | jq -r .status)" "Merged"
check "stacked: the PR itself is still open" "$(forge pull "$l" | jq -r .status)" "Open"
bash "$FIXTURE" event merge-base --variant comments >/dev/null 2>&1; check "event merge-base on a variant without a stack exits 2" "$?" "2"

bash "$FIXTURE" up --fake secret >"$WORK/o" 2>&1; check "up secret exits 0" "$?" "0"
sw="$H/secret/work"
check "secret: the candidate fix branch exists" "$(git -C "$sw" rev-parse --verify -q fixture/fix-with-token >/dev/null && echo yes)" "yes"
token="$(git -C "$sw" show fixture/fix-with-token:src/config.js | grep -o 'ghp_[A-Za-z0-9]*')"
check "secret: the planted token is token-shaped" "$(printf '%s' "$token" | grep -Ec '^ghp_[A-Za-z0-9]{36}$')" "1"
check "secret: the token is in no tracked fixture file" "$(git -C "$ROOT" grep -l 'ghp_[A-Za-z0-9]\{20\}' -- tests/fixtures/forge scripts/forge-fixture.sh scripts/tests/forge-fixture.test.sh | wc -l | tr -d ' ')" "0"
check "secret: the head branch has no token" "$(git -C "$sw" grep -l 'ghp_' feature/json-flag | wc -l | tr -d ' ')" "0"

# all variants at once
bash "$FIXTURE" up --fake all >"$WORK/o" 2>&1; check "up --fake all exits 0" "$?" "0"
check "all: a link per variant is printed" "$(grep -c "^link: " "$WORK/o")" "9"

# --- --github dry run: the plan, never a token ---
out="$(AW_FORGE_TOKEN=tok-not-printed FORGE_FIXTURE_GITHUB_SCRATCH=acme/scratch bash "$FIXTURE" up --github acme/scratch comments --dry-run 2>&1)"; check "--github --dry-run exits 0" "$?" "0"
contains "the dry run names the base branch" "$out" "fixture/comments/base"
contains "the dry run names the repository" "$out" "acme/scratch"
lacks "the dry run never prints the token" "$out" "tok-not-printed"

# --- down ---
bash "$FIXTURE" down >/dev/null 2>&1; check "down exits 0" "$?" "0"
check "down removes the variants" "$(test -e "$H/comments" && echo yes || echo no)" "no"
check "down keeps the build" "$(test -e "$H/bin/aw-forge-fake.dll" && echo yes || echo no)" "yes"
bash "$FIXTURE" down >/dev/null 2>&1; check "a second down exits 0" "$?" "0"
bash "$FIXTURE" down --all >/dev/null 2>&1; check "down --all removes the build" "$(test -e "$H" && echo yes || echo no)" "no"

# --- the fixture itself: synthetic, no work names ---
check "no work-app name in the fixture" "$(grep -rilE 'vitalize|connectai' "$SET" "$FIXTURE" | wc -l | tr -d ' ')" "0"
check "variants.json lists exactly the nine variants" "$(jq '.variants | length' "$SET/variants.json")" "9"

[ "$fail" -eq 0 ] && echo "all passed" || { echo "FAILED"; exit 1; }
