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
# Secrets: only the token is ever printed, and only to stdout. The key lives in
# a mode-600 file inside a private temp dir that is removed on exit; the JWT and
# the Authorization header reach curl on its stdin (--config -), so they are
# never in argv or on disk. curl runs with -q, so ~/.curlrc can't add
# directives. On an API error only the HTTP status and GitHub's message are
# shown, with token shapes and control characters removed.
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
for c in curl jq openssl; do
  command -v "$c" >/dev/null || die "$c is required"
done

if [ -t 1 ] && [ "$FORCE_TTY" -eq 0 ]; then
  die "refusing to print a token to a terminal; capture it (GH_TOKEN=\"\$(scripts/agent-token.sh)\") or pass --force-tty"
fi

umask 077
WORK="$(mktemp -d "${TMPDIR:-/tmp}/agent-token.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
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

status="$(printf '%s\n' "$cfg" | curl -q -sS --max-time 30 -o "$WORK/resp.json" -w '%{http_code}' --config -)" \
  || die "request to $API_URL failed"
unset cfg

if [ "$status" != "201" ]; then
  msg="$(jq -r '.message // "no message"' "$WORK/resp.json" 2>/dev/null || echo "unreadable response")"
  # GitHub's message is plain text; strip control characters and anything
  # token-shaped all the same, and cap the length.
  msg="$(printf '%s' "$msg" | LC_ALL=C tr -d '\000-\037\177' \
    | sed -E 's/(gh[a-z]_|github_pat_)[A-Za-z0-9_.-]+/[redacted]/g' | cut -c1-300)"
  die "GitHub returned HTTP $status: $msg"
fi

token="$(jq -r '.token // empty' "$WORK/resp.json")"
[ -n "$token" ] || die "GitHub returned no token"
if [ "$VERBOSE" -eq 1 ]; then
  echo "agent-token: expires_at $(jq -r '.expires_at // "unknown"' "$WORK/resp.json")" >&2
fi
printf '%s\n' "$token"
