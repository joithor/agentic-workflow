#!/usr/bin/env bash
# Tests for scripts/agent-token.sh. Run: bash scripts/tests/agent-token.test.sh
# Uses a fixture RSA key generated here and a fake GitHub endpoint
# (fixtures/agent-token/fake-github.py) — never the real App key or API.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
SCRIPT="$ROOT/scripts/agent-token.sh"
FAKE="$DIR/fixtures/agent-token/fake-github.py"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
SERVER_PID=""
stop_server() {
  if [ -n "$SERVER_PID" ]; then kill "$SERVER_PID" 2>/dev/null || true; wait "$SERVER_PID" 2>/dev/null || true; fi
  SERVER_PID=""
}
trap 'stop_server; rm -rf "$WORK"' EXIT

openssl genrsa -out "$WORK/key.pem" 2048 2>/dev/null
openssl rsa -in "$WORK/key.pem" -pubout -out "$WORK/pub.pem" 2>/dev/null
KEY_LINE="$(sed -n 2p "$WORK/key.pem")"   # a line of key material to grep for
CANNED="ghs_fixtureToken0123456789abcdefABCDEF"
LEAK="ghs_leakyBodyToken9876543210zyxwvuZYXW"

# run_tty <outfile> <cmd...>: run cmd with stdout+stderr on a fresh pty (stdin
# /dev/null), copy what it printed to <outfile>, return its exit code. Unlike
# script(1), it waits for the child however stdin behaves.
run_tty() {
  python3 - "$@" <<'PY'
import os, subprocess, sys
out, cmd = sys.argv[1], sys.argv[2:]
m, s = os.openpty()
p = subprocess.Popen(cmd, stdin=subprocess.DEVNULL, stdout=s, stderr=s)
os.close(s)
buf = b""
while True:
    try:
        chunk = os.read(m, 4096)
    except OSError:
        break
    if not chunk:
        break
    buf += chunk
open(out, "wb").write(buf)
sys.exit(p.wait())
PY
}

start_server() { # $1 = mode (ok|err)
  stop_server
  SRV="$WORK/srv-$1-$RANDOM"; mkdir -p "$SRV"
  python3 "$FAKE" "$SRV" "$WORK/pub.pem" "$1" &
  SERVER_PID=$!
  for _ in $(seq 1 50); do [ -s "$SRV/port" ] && break; sleep 0.1; done
  export AGENT_APP_API_URL="http://127.0.0.1:$(cat "$SRV/port")"
}

export AGENT_APP_KEY_FILE="$WORK/key.pem"
export TMPDIR="$WORK/tmp"; mkdir -p "$TMPDIR"

# --- happy path: token on stdout only, JWT valid ---
start_server ok
rc=0; out="$(bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "exits 0" "$rc" "0"
check "stdout is exactly the token" "$out" "$CANNED"
check "stderr is empty without --verbose" "$(cat "$WORK/err")" ""
req="$(head -1 "$SRV/requests.jsonl")"
check "POSTs the default installation" "$(jq -r .path <<<"$req")" "/app/installations/169843852/access_tokens"
check "asks for the v3 JSON media type" "$(jq -r .accept <<<"$req")" "application/vnd.github+json"
check "JWT header is RS256" "$(jq -c .header <<<"$req")" '{"alg":"RS256","typ":"JWT"}'
check "JWT iss is the default App id" "$(jq -r .claims.iss <<<"$req")" "5259848"
check "JWT lifetime is 600s (iat-60 .. exp+540)" "$(jq '.claims.exp - .claims.iat' <<<"$req")" "600"
now="$(date +%s)"; iat="$(jq .claims.iat <<<"$req")"
check "JWT iat is ~60s in the past" "$(( now - iat >= 55 && now - iat <= 75 ))" "1"
check "JWT signature verifies with the public key" "$(jq -r .verify <<<"$req")" "Verified OK"
check "no request body without --repo" "$(jq -r .body <<<"$req")" ""
check "Authorization header never in any process argv" "$(jq -r .argv_has_bearer <<<"$req")" "false"
check "JWT never in any process argv" "$(jq -r .argv_has_jwt <<<"$req")" "false"
check "private temp dir removed after the run" "$(find "$TMPDIR" -mindepth 1 | wc -l | tr -d ' ')" "0"

# --- flags: --app-id, --installation, --repo, --verbose ---
rc=0; out="$(bash "$SCRIPT" --app-id 42 --installation 7 --repo agentic-workflow --verbose 2>"$WORK/err")" || rc=$?
req="$(tail -1 "$SRV/requests.jsonl")"
check "--verbose: exits 0" "$rc" "0"
check "--verbose: stdout is still only the token" "$out" "$CANNED"
check "--verbose: expiry on stderr" "$(cat "$WORK/err")" "agent-token: expires_at 2099-01-01T00:00:00Z"
check "--installation picks the path" "$(jq -r .path <<<"$req")" "/app/installations/7/access_tokens"
check "--app-id sets iss" "$(jq -r .claims.iss <<<"$req")" "42"
check "--repo narrows the token" "$(jq -c '.body|fromjson' <<<"$req")" '{"repositories":["agentic-workflow"]}'

# --- env overrides for ids ---
AGENT_APP_ID=99 AGENT_APP_INSTALLATION_ID=11 bash "$SCRIPT" >/dev/null 2>&1
req="$(tail -1 "$SRV/requests.jsonl")"
check "AGENT_APP_INSTALLATION_ID env picks the path" "$(jq -r .path <<<"$req")" "/app/installations/11/access_tokens"
check "AGENT_APP_ID env sets iss" "$(jq -r .claims.iss <<<"$req")" "99"

# --- refuses a terminal on stdout ---
rc=0; run_tty "$WORK/tty.out" bash "$SCRIPT" || rc=$?
check "TTY stdout: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "TTY stdout: says why" "$(grep -c 'refusing to print a token to a terminal' "$WORK/tty.out")" "1"
check "TTY stdout: token never reaches the terminal" "$(grep -c "$CANNED" "$WORK/tty.out" || true)" "0"
n_before="$(wc -l <"$SRV/requests.jsonl" | tr -d ' ')"
check "TTY stdout: refused before calling the API" "$n_before" "3"
run_tty "$WORK/tty2.out" bash "$SCRIPT" --force-tty || true
check "--force-tty prints to the terminal" "$(grep -c "$CANNED" "$WORK/tty2.out")" "1"

# --- API error: status + message only, body redacted ---
start_server err
rc=0; out="$(bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "API 401: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "API 401: stdout empty" "$out" ""
check "API 401: stderr names status and message" "$(cat "$WORK/err")" "agent-token: GitHub returned HTTP 401: Bad credentials"
check "API 401: token-shaped body value not echoed" "$(grep -c "$LEAK" "$WORK/err" || true)" "0"

# --- missing key: clear error, no output, no API call ---
start_server ok
rc=0; out="$(AGENT_APP_KEY_FILE="$WORK/nope.pem" bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "missing key: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "missing key: stdout empty" "$out" ""
check "missing key: says which file" "$(grep -c "key file not readable: $WORK/nope.pem" "$WORK/err")" "1"
check "missing key: API not called" "$([ -e "$SRV/requests.jsonl" ] && echo called || echo none)" "none"

# --- not a key: openssl failure surfaces without key bytes ---
echo "not a key" >"$WORK/bad.pem"
rc=0; out="$(AGENT_APP_KEY_FILE="$WORK/bad.pem" bash "$SCRIPT" 2>"$WORK/err")" || rc=$?
check "bad key: exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
check "bad key: stdout empty" "$out" ""
check "bad key: says signing failed" "$(grep -c 'could not sign the JWT' "$WORK/err")" "1"

# --- unknown flag ---
rc=0; bash "$SCRIPT" --bogus >/dev/null 2>"$WORK/err" || rc=$?
check "unknown flag: exit 2" "$rc" "2"
check "unknown flag: usage on stderr" "$(grep -c '^usage: agent-token.sh' "$WORK/err")" "1"

# --- key material never in any output across all runs ---
leaks="$(cat "$WORK"/err "$WORK"/tty.out "$WORK"/tty2.out | grep -c -e "$KEY_LINE" -e 'PRIVATE KEY' || true)"
check "key material never printed" "$leaks" "0"

exit "$fail"
