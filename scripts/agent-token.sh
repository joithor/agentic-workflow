#!/usr/bin/env bash
# Mints a short-lived installation token for the agent GitHub App, so agent
# sessions push and open PRs as the App instead of the owner (agent readiness
# spec §4d). The token lasts at most an hour.
#
# OWNER / SESSION-LAUNCHER ONLY. Run it before an agent session starts and hand
# the session the token (GH_TOKEN); never run it inside an agent session. It
# reads the App's private key from the owner's keychain, which agent sessions
# must not reach (spec §4d B7: the planned deny hook covers this script and
# AGENT_APP_* in agent sessions).
#
# Usage: GH_TOKEN="$(scripts/agent-token.sh [--repo NAME] [--verbose])"
#   --app-id ID         App id           (env AGENT_APP_ID, default 5259848)
#   --installation ID   installation id  (env AGENT_APP_INSTALLATION_ID, default 169843852)
#   --repo NAME         narrow the token to one repository (repeatable)
#   --verbose           print the token's expiry to stderr
#   --force-tty         allow printing the token to a terminal
#
# The private key comes from the macOS keychain (service
# agentic-workflow-agent-app, account private-key; stored multi-line, so
# `security -w` returns hex) or from AGENT_APP_KEY_FILE (a PEM path; tests).
# AGENT_APP_API_URL overrides the API base for tests only: it must be
# https://api.github.com or a loopback http://127.0.0.1:<port> /
# http://localhost:<port>. Whatever host it names receives the App JWT, which
# for its 10-minute life can mint tokens for every installation of the App.
#
# Secrets: only the token is ever printed, and only to stdout. The key is the
# one secret that touches disk: a mode-600 file inside a private temp dir,
# deleted as soon as the JWT is signed (before any network call) and again on
# exit. The JWT and the Authorization header reach curl on its stdin
# (--config -), and the response (with the token) comes back on curl's stdout
# and stays in memory, so neither is ever in argv or on disk. curl runs with
# -q (no ~/.curlrc) and noproxy "*". On an API error only the HTTP status and
# GitHub's message are shown, with token shapes and control characters removed.
#
# Trust boundary: the URL allowlist and these checks stop mistakes, not a
# hostile caller. The caller's environment (PATH, exported functions, BASH_ENV,
# proxies, CA bundle) is trusted, which is why only the owner or the launcher
# runs this.
set -euo pipefail
set +x # an inherited xtrace (bash -x, SHELLOPTS) would trace the JWT and the token
unset BASH_XTRACEFD

APP_ID="${AGENT_APP_ID:-5259848}"
INSTALLATION_ID="${AGENT_APP_INSTALLATION_ID:-169843852}"
API_URL="${AGENT_APP_API_URL:-https://api.github.com}"
KC_SERVICE="agentic-workflow-agent-app"
KC_ACCOUNT="private-key"
REPOS=()
VERBOSE=0
FORCE_TTY=0

die() { echo "agent-token: $1" >&2; exit "${2:-1}"; }
usage() {
  echo "usage: agent-token.sh [--app-id ID] [--installation ID] [--repo NAME]... [--verbose] [--force-tty]" >&2
  exit 2
}

while [ $# -gt 0 ]; do
  case "$1" in
    --app-id) [ $# -ge 2 ] || usage; APP_ID="$2"; shift 2 ;;
    --installation) [ $# -ge 2 ] || usage; INSTALLATION_ID="$2"; shift 2 ;;
    --repo) [ $# -ge 2 ] || usage; REPOS+=("$2"); shift 2 ;;
    --verbose) VERBOSE=1; shift ;;
    --force-tty) FORCE_TTY=1; shift ;;
    -h|--help) usage ;;
    *) echo "agent-token: unknown argument: $1" >&2; usage ;;
  esac
done
[[ "$APP_ID" =~ ^[0-9]+$ ]] || die "App id must be numeric" 2
[[ "$INSTALLATION_ID" =~ ^[0-9]+$ ]] || die "installation id must be numeric" 2
# A strict allowlist also keeps quotes and newlines out of the curl config.
[[ "$API_URL" =~ ^(https://api\.github\.com|http://(127\.0\.0\.1|localhost):[0-9]{1,5})$ ]] \
  || die "AGENT_APP_API_URL must be https://api.github.com, or http://127.0.0.1:<port> or http://localhost:<port> for tests" 2
for r in ${REPOS[@]+"${REPOS[@]}"}; do
  [[ "$r" =~ ^[A-Za-z0-9._-]{1,100}$ ]] || die "invalid repository name (letters, digits, . _ - only)" 2
done
for c in curl jq openssl pgrep; do
  command -v "$c" >/dev/null || die "$c is required"
done

if [ -t 1 ] && [ "$FORCE_TTY" -eq 0 ]; then
  die "refusing to print a token to a terminal; capture it (GH_TOKEN=\"\$(scripts/agent-token.sh)\") or pass --force-tty"
fi

umask 077
WORK="$(mktemp -d "${TMPDIR:-/tmp}/agent-token.XXXXXX")"
# kill_tree PID: snapshot PID's children, stop PID first (so the request job
# can't go on to print), then its children (the command substitution, curl).
kill_tree() {
  local kids child
  kids="$(pgrep -P "$1" 2>/dev/null || true)"
  kill -TERM "$1" 2>/dev/null || true
  for child in $kids; do kill_tree "$child"; done
}
# On every exit — normal, `exit` from a trap, or a fatal signal bash doesn't
# trap (USR1, ALRM, …) — stop any request job still running, found as this
# shell's children so it doesn't matter whether `$!` was recorded yet.
cleanup() {
  local p
  for p in $(pgrep -P $$ 2>/dev/null || true); do kill_tree "$p"; done
  rm -rf "$WORK"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP
KEY="$WORK/key.pem"

if [ -n "${AGENT_APP_KEY_FILE:-}" ]; then
  [ -r "$AGENT_APP_KEY_FILE" ] || die "key file not readable: $AGENT_APP_KEY_FILE"
  cat "$AGENT_APP_KEY_FILE" >"$KEY"
else
  command -v security >/dev/null || die "no AGENT_APP_KEY_FILE and no macOS keychain (security) on this machine"
  command -v xxd >/dev/null || die "xxd is required to decode the keychain item"
  security find-generic-password -s "$KC_SERVICE" -a "$KC_ACCOUNT" -w >"$WORK/kc" 2>/dev/null \
    || die "could not read the App key from the keychain (service $KC_SERVICE, account $KC_ACCOUNT)"
  # A multi-line secret comes back hex-encoded; a single-line one comes back as is.
  if LC_ALL=C grep -Eq '^[0-9A-Fa-f]+$' "$WORK/kc"; then
    xxd -r -p "$WORK/kc" >"$KEY"
  else
    cat "$WORK/kc" >"$KEY"
  fi
  rm -f "$WORK/kc"
fi

b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }

now="$(date +%s)"
header="$(printf '%s' '{"alg":"RS256","typ":"JWT"}' | b64url)"
claims="$(printf '{"iat":%d,"exp":%d,"iss":"%s"}' "$((now - 60))" "$((now + 540))" "$APP_ID" | b64url)"
sig="$(printf '%s' "$header.$claims" | openssl dgst -sha256 -sign "$KEY" -binary 2>/dev/null | b64url)" || sig=""
[ -n "$sig" ] || die "could not sign the JWT (is the key a PEM RSA private key?)"
rm -f "$KEY"
jwt="$header.$claims.$sig"

# Every value below is validated or generated here, so none can close a quoted
# config string; the body's own quotes are escaped for curl's config syntax.
cfg="url = \"$API_URL/app/installations/$INSTALLATION_ID/access_tokens\"
request = \"POST\"
noproxy = \"*\"
header = \"Accept: application/vnd.github+json\"
header = \"X-GitHub-Api-Version: 2022-11-28\"
header = \"Authorization: Bearer $jwt\""
if [ ${#REPOS[@]} -gt 0 ]; then
  body="{\"repositories\":[$(printf '"%s",' "${REPOS[@]}" | sed 's/,$//')]}"
  cfg="$cfg
header = \"Content-Type: application/json\"
data-raw = \"${body//\"/\\\"}\""
fi
unset jwt sig

# The request runs as a background job and the main shell `wait`s on it: bash
# defers a trap until a foreground child exits, but `wait` returns as soon as a
# trapped signal arrives, so TERM/INT/HUP stop the script at once, and the
# EXIT trap (cleanup) stops the job and curl on any exit. The response stays in
# the job's memory and reaches jq through pipes (a bash 3.2 here-string would
# be a temp file).
(
  trap - EXIT INT TERM HUP # the job must not run the parent's cleanup
  resp="$(printf '%s\n' "$cfg" | curl -q -sS --max-time 30 -w '\n%{http_code}' --config -)" \
    || die "request to $API_URL failed"
  status="${resp##*$'\n'}"
  resp="${resp%$'\n'*}"

  if [ "$status" != "201" ]; then
    msg="$(printf '%s' "$resp" | jq -r '.message // "no message"' 2>/dev/null || echo "unreadable response")"
    # GitHub's message is plain text; strip control characters and anything
    # token-shaped all the same, and cap the length.
    msg="$(printf '%s' "$msg" | LC_ALL=C tr -d '\000-\037\177' \
      | sed -E 's/(gh[a-z]_|github_pat_)[A-Za-z0-9_.-]+/[redacted]/g' | cut -c1-300)"
    die "GitHub returned HTTP $status: $msg"
  fi

  token="$(printf '%s' "$resp" | jq -r '.token // empty')"
  [ -n "$token" ] || die "GitHub returned no token"
  if [ "$VERBOSE" -eq 1 ]; then
    echo "agent-token: expires_at $(printf '%s' "$resp" | jq -r '.expires_at // "unknown"')" >&2
  fi
  printf '%s\n' "$token"
) &
REQ_PID=$!
unset cfg
rc=0
wait "$REQ_PID" || rc=$?
exit "$rc"
