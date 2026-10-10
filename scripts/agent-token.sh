#!/usr/bin/env bash
# Mints a short-lived installation token for the agent GitHub App, so agent
# sessions push and open PRs as the App instead of the owner (agent readiness
# spec §4d). The token lasts at most an hour.
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
# AGENT_APP_KEYCHAIN_SERVICE / AGENT_APP_KEYCHAIN_ACCOUNT override the item,
# AGENT_APP_API_URL the API base.
#
# Secrets: only the token is ever printed, and only to stdout. The key lives in
# a mode-600 file inside a private temp dir that is removed on exit; the JWT and
# the Authorization header reach curl through its stdin config, never argv. On
# an API error only the HTTP status and GitHub's message are shown.
set -euo pipefail

APP_ID="${AGENT_APP_ID:-5259848}"
INSTALLATION_ID="${AGENT_APP_INSTALLATION_ID:-169843852}"
API_URL="${AGENT_APP_API_URL:-https://api.github.com}"
KC_SERVICE="${AGENT_APP_KEYCHAIN_SERVICE:-agentic-workflow-agent-app}"
KC_ACCOUNT="${AGENT_APP_KEYCHAIN_ACCOUNT:-private-key}"
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

if [ -t 1 ] && [ "$FORCE_TTY" -eq 0 ]; then
  die "refusing to print a token to a terminal; capture it (GH_TOKEN=\"\$(scripts/agent-token.sh)\") or pass --force-tty"
fi

umask 077
WORK="$(mktemp -d "${TMPDIR:-/tmp}/agent-token.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT INT TERM
KEY="$WORK/key.pem"

if [ -n "${AGENT_APP_KEY_FILE:-}" ]; then
  [ -r "$AGENT_APP_KEY_FILE" ] || die "key file not readable: $AGENT_APP_KEY_FILE"
  cat "$AGENT_APP_KEY_FILE" >"$KEY"
else
  command -v security >/dev/null || die "no AGENT_APP_KEY_FILE and no macOS keychain (security) on this machine"
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

{
  printf 'url = "%s/app/installations/%s/access_tokens"\n' "$API_URL" "$INSTALLATION_ID"
  printf 'request = "POST"\n'
  printf 'header = "Accept: application/vnd.github+json"\n'
  printf 'header = "X-GitHub-Api-Version: 2022-11-28"\n'
  printf 'header = "Authorization: Bearer %s"\n' "$jwt"
  if [ ${#REPOS[@]} -gt 0 ]; then
    jq -cn '{repositories: $ARGS.positional}' --args "${REPOS[@]}" >"$WORK/body.json"
    printf 'header = "Content-Type: application/json"\n'
    printf 'data-binary = "@%s"\n' "$WORK/body.json"
  fi
} >"$WORK/curl.cfg"
unset jwt sig

status="$(curl -sS --max-time 30 -o "$WORK/resp.json" -w '%{http_code}' --config "$WORK/curl.cfg")" \
  || die "request to $API_URL failed"
rm -f "$WORK/curl.cfg"

if [ "$status" != "201" ]; then
  msg="$(jq -r '.message // "no message"' "$WORK/resp.json" 2>/dev/null || echo "unreadable response")"
  # GitHub's message is plain text; strip anything token-shaped all the same.
  msg="$(printf '%s' "$msg" | sed -E 's/gh[a-z]_[A-Za-z0-9_]+/[redacted]/g')"
  die "GitHub returned HTTP $status: $msg"
fi

token="$(jq -r '.token // empty' "$WORK/resp.json")"
[ -n "$token" ] || die "GitHub returned no token"
if [ "$VERBOSE" -eq 1 ]; then
  echo "agent-token: expires_at $(jq -r '.expires_at // "unknown"' "$WORK/resp.json")" >&2
fi
printf '%s\n' "$token"
