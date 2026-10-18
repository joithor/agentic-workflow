#!/usr/bin/env bash
# Tests for scripts/check-fleet.mjs (the fleet state file contract, session collector CO-0): the repo passes, each valid fixture
# validates and each invalid one is refused, and each kind of drift fails.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-fleet.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-fleet-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# Ajv is a dev dependency of scripts/ (scripts/package.json); installed once under a lock, since suites run in parallel.
# shellcheck source=scripts/lib/ensure-scripts-deps.sh
. "$REPO/scripts/lib/ensure-scripts-deps.sh"
ensure_scripts_deps "$REPO/scripts" || exit 1

run() { set +e; OUT="$(node "$CHECK" --root "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -F -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

# A throwaway copy of the files the check reads.
copy() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/schemas" "$d/tests/fixtures"
  cp "$REPO/schemas/fleet-state.schema.json" "$d/schemas/"
  cp -R "$REPO/tests/fixtures/fleet" "$d/tests/fixtures/"
  cp -R "$REPO/tests/fixtures/sessions" "$d/tests/fixtures/"
  echo "$d"
}

echo "repo"
run "$REPO"; expect_rc "the repo's fleet fixtures and sessions tree pass" 0; expect_out "prints ok" "check-fleet: ok"

echo "fixtures"
valid="$(jq -r '[.fixtures[] | select(.schema == "valid")] | length' "$REPO/tests/fixtures/fleet/manifest.json")"
invalid="$(jq -r '[.fixtures[] | select(.schema == "invalid")] | length' "$REPO/tests/fixtures/fleet/manifest.json")"
if [ "$valid" -ge 10 ] && [ "$invalid" -ge 10 ]; then ok "the manifest has $valid valid and $invalid invalid fixtures"; else fail "too few fixtures: $valid valid, $invalid invalid"; fi
if [ -f "$REPO/tests/fixtures/fleet/claude-code/permission-request.json" ] && [ ! -e "$REPO/tests/fixtures/fleet/claude" ]; then ok "Claude fixtures sit under claude-code/, never claude/ (the AgentId mapping)"; else fail "Claude fixtures are not under claude-code/"; fi

echo "drift"
d="$(copy verdict)"
cp "$d/tests/fixtures/fleet/claude-code/stop.json" "$d/tests/fixtures/fleet/invalid/bad-state.json"
run "$d"; expect_rc "an invalid fixture that validates fails" 1; expect_out "names the fixture" "invalid/bad-state.json: expected invalid"

d="$(copy asvalid)"
cp "$d/tests/fixtures/fleet/invalid/bad-state.json" "$d/tests/fixtures/fleet/claude-code/stop.json"
run "$d"; expect_rc "a valid fixture the schema refuses fails" 1; expect_out "names the fixture and the reason" "claude-code/stop.json: expected valid"

d="$(copy unlisted)"
echo '{}' > "$d/tests/fixtures/fleet/stray.json"
run "$d"; expect_rc "a fixture missing from the manifest fails" 1; expect_out "names it" "stray.json: not listed in manifest.json"

d="$(copy missing)"
rm "$d/tests/fixtures/fleet/codex/interrupt.json"
run "$d"; expect_rc "a manifest entry with no file fails" 1; expect_out "names it" "codex/interrupt.json: listed in manifest.json but missing"

d="$(copy dir)"
mkdir -p "$d/tests/fixtures/fleet/claude"
mv "$d/tests/fixtures/fleet/claude-code/stop.json" "$d/tests/fixtures/fleet/claude/stop.json"
perl -pi -e 's#"claude-code/stop.json"#"claude/stop.json"#' "$d/tests/fixtures/fleet/manifest.json"
run "$d"; expect_rc "a valid fixture outside its provider's directory fails" 1; expect_out "names the provider directory" "directory named by its provider (claude-code)"

d="$(copy schema)"
perl -0pi -e 's/"enum": \["running", "asking", "idle", "ended"\]/"enum": ["running", "idle"]/' "$d/schemas/fleet-state.schema.json"
run "$d"; expect_rc "a schema that drops a state fails the valid fixtures" 1; expect_out "names a fixture" "permission-request.json: expected valid"

echo "sessions tree"
d="$(copy proc-unlisted)"
cp -R "$d/tests/fixtures/sessions/proc/211" "$d/tests/fixtures/sessions/proc/4242"
run "$d"; expect_rc "a process directory missing from the manifest fails" 1; expect_out "names it" "proc/4242: not listed in manifest.json"

d="$(copy proc-link)"
ln -sfn / "$d/tests/fixtures/sessions/proc/211/cwd"
run "$d"; expect_rc "a cwd symlink to a real directory fails (tools that glob the repo follow it)" 1; expect_out "names the link" "proc/211/cwd: symlink target / is a directory"

d="$(copy proc-incomplete)"
rm "$d/tests/fixtures/sessions/proc/311/environ"
run "$d"; expect_rc "a process directory without environ fails" 1; expect_out "names the file" "proc/311: no environ file"

d="$(copy proc-link-outside)"
ln -sfn /nonexistent-elsewhere/repo "$d/tests/fixtures/sessions/proc/211/cwd"
run "$d"; expect_rc "a cwd symlink outside the fixture root fails (it may exist on another box)" 1; expect_out "names the link" "proc/211/cwd: symlink target /nonexistent-elsewhere/repo is outside /nonexistent-fixture-root"

echo "shared Ajv install (scripts/lib/ensure-scripts-deps.sh)"
# Two suites that need Ajv at once: a stub npm (slow, counting its calls) in a scratch package dir.
dep="$TMPROOT/deps" && mkdir -p "$dep/bin" "$dep/pkg"
cat > "$dep/bin/npm" <<'STUB'
#!/usr/bin/env bash
echo call >> "$NPM_CALLS"
sleep 1
mkdir -p node_modules/ajv && echo '{"name":"ajv"}' > node_modules/ajv/package.json
STUB
chmod +x "$dep/bin/npm"
( export PATH="$dep/bin:$PATH" NPM_CALLS="$dep/calls"
  ensure_scripts_deps "$dep/pkg" > "$dep/a.out" 2>&1 & a=$!
  ensure_scripts_deps "$dep/pkg" > "$dep/b.out" 2>&1 & b=$!
  wait "$a" && wait "$b" && echo both-ok > "$dep/result" ) || true
if [ "$(cat "$dep/result" 2>/dev/null)" = both-ok ]; then ok "two concurrent callers both succeed"; else fail "two concurrent callers: $(cat "$dep/a.out" "$dep/b.out")"; fi
if [ "$(wc -l < "$dep/calls" | tr -d ' ')" = 1 ]; then ok "npm ci runs once"; else fail "npm ci ran $(wc -l < "$dep/calls" | tr -d ' ') times"; fi
if [ ! -e "$dep/pkg/.npm-ci.lock" ]; then ok "the lock is released"; else fail "the lock is left behind"; fi

echo
echo "check-fleet.test.sh: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
