#!/usr/bin/env bash
# scripts/sindri-ui-fixture.sh — the Sindri UI's proof fixture (UI spec §18.2, first part: U1). It builds the state a
# UI phase is proven against in a guarded temp home, through scripts/sindri-prove.sh only (its guard runs before
# anything is written, so the owner's real home is never touched).
#
#   sindri-ui-fixture.sh up        write the live profile (a heartbeat service, port 0), approve it as the owner and
#                                  start the control plane (sindri --headless, through sindri-prove.sh human)
#   sindri-ui-fixture.sh stall     insert one row of a registered event type with an unparseable payload into
#                                  <home>/ledger.db: the projection throws and the views stall (the stalled banner)
#   sindri-ui-fixture.sh unstall   delete that row again (then Rebuild views recovers)
#   sindri-ui-fixture.sh drift     edit the live profile so it no longer matches the approval (a profile row)
#   sindri-ui-fixture.sh down      stop the control plane (sindri stop, as the owner)
#   sindri-ui-fixture.sh items [--large]
#                                  add the default fixture's 4 items (sindri item add, as the owner; U2, API P3);
#                                  --large adds 30 more for the fold test (U3 adds its 12 needs-you rows)
#   sindri-ui-fixture.sh close <item> [--cancelled]
#                                  close an item at the CLI (sindri item close, as the owner): with the app open on
#                                  the same item, the app's own Close then sends a stale version (SND-ITEM-409)
#
#   sindri-ui-fixture.sh request [--item ID] [--repo R] <text>
#                                  file a request as a session (a POST to /sindri/requests with no caller header, so the
#                                  API reads a session; --repo sends X-Sindri-Repo): a decision lands in Needs you (U3')
#   sindri-ui-fixture.sh decide <DEC-id> <choice>
#                                  decide at the CLI (sindri decide, as the owner): with the app open on the same
#                                  decision, the app's own Decide is then refused (SND-ITEM-409)
#   sindri-ui-fixture.sh fail      add a service the build cannot run to the live profile and approve it (a failure row)
#   sindri-ui-fixture.sh unfail    remove it again and approve (the row clears)
#                                  (items --large also files 12 requests, 6 in FIXTURE_REPO and 6 in <FIXTURE_REPO>-two)
#
# FIXTURE_REPO names the items' repo (default: none, so the API's default applies).
# Environment: AW_SINDRI_HOME (required: sindri-prove.sh's guard, a temp or proof folder), SINDRI (the built C# CLI,
# as sindri-prove.sh reads it), STALL_TYPE (the stalled row's type; default service.ran, a type the projector reads),
# SINDRI_UI_FIXTURE_SQL=python (use python3's sqlite3 module even when the sqlite3 CLI is installed; without the CLI it is
# the fallback anyway). Needs sqlite3 or python3 for stall and unstall, jq for up.
# Exit: 0 done; 2 the guard, a usage error or a missing tool; otherwise the failing step's exit code.
# Tests: bash scripts/tests/sindri-ui-fixture.test.sh
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROVE="$ROOT/scripts/sindri-prove.sh"
STALL_KEY="sindri-ui-fixture:stall"
STALL_ID="7f3c1a52-0b6e-4d1e-9a55-3c2f6d8e1b04"
STALL_CORRELATION="7f3c1a52-0b6e-4d1e-9a55-3c2f6d8e1b05"

die() { echo "sindri-ui-fixture: $*" >&2; exit 2; }
usage() { sed -n '5,31p' "$0" | sed 's/^# \{0,1\}//'; }

# sindri-prove.sh's guard alone: it runs before any verb, and SINDRI=true makes the verb a no-op.
guard() { SINDRI=true "$PROVE" guard >/dev/null || exit 2; }

need() { command -v "$1" >/dev/null 2>&1 || die "$1 is needed for '$verb'"; }

# The sqlite3 CLI when there is one, else python3's sqlite3 module (a box without the CLI). Both run the statements in
# order, stop at the first error and print each selected row with its columns joined by '|'.
use_cli() { [ "${SINDRI_UI_FIXTURE_SQL:-}" != python ] && command -v sqlite3 >/dev/null 2>&1; }
need_sql() { use_cli || { command -v python3 >/dev/null 2>&1 && python3 -I -c 'import sqlite3' >/dev/null 2>&1; } || die "sqlite3 (or python3) is needed for '$verb'"; }

ledger() {
  [ -f "$AW_SINDRI_HOME/ledger.db" ] || die "no ledger at $AW_SINDRI_HOME/ledger.db: run 'up' first"
  if use_cli; then sqlite3 -bail "$AW_SINDRI_HOME/ledger.db" "$1"; else python_sql "$AW_SINDRI_HOME/ledger.db" "$1"; fi
}

python_sql() {
  python3 -I -c '
import sqlite3, sys
con = sqlite3.connect(sys.argv[1], isolation_level=None)
pending = ""
for part in sys.argv[2].split(";"):
    pending += part
    if not sqlite3.complete_statement(pending + ";"):
        pending += ";"
        continue
    for row in con.execute(pending):
        print("|".join("" if v is None else str(v) for v in row))
    pending = ""
' "$1" "$2"
}

profile_path() { printf '%s\n' "${AW_SINDRI_PROFILE:-$AW_SINDRI_HOME/profile/profile.yaml}"; }

up() {
  need jq
  local profile hash
  profile="$(profile_path)"
  mkdir -p "$(dirname "$profile")"
  cat > "$profile" <<'YAML'
schemaVersion: 1
user: fixture
services:
  heartbeat: { every: 5s }
api: { port: 0 }
YAML
  approve || return
  "$PROVE" human --headless
}

# Validates the live profile and approves its hash as the owner, typing the first six characters.
approve() {
  local hash
  hash="$("$PROVE" profile validate --json | jq -r '.hash // empty')"
  [ -n "$hash" ] || die "profile validate gave no hash"
  printf 'approve profile %s\n' "${hash:0:6}" | "$PROVE" human profile approve "$hash"
}

# A service the build has no code for: the scheduler reports its failure, and Needs you shows a failure row.
fail() {
  need jq
  local profile
  profile="$(profile_path)"
  [ -f "$profile" ] || die "no live profile at $profile: run 'up' first"
  if ! grep -q 'broken:' "$profile"; then
    awk '{print} /^  heartbeat:/ {print "  broken: { every: 5s }"}' "$profile" > "$profile.new" && mv "$profile.new" "$profile"
  fi
  approve
}

unfail() {
  need jq
  local profile
  profile="$(profile_path)"
  [ -f "$profile" ] || die "no live profile at $profile: run 'up' first"
  grep -v '^  broken:' "$profile" > "$profile.new"; mv "$profile.new" "$profile"
  approve
}

# A session's request: no X-Sindri-Caller, so the API reads a session.
request() {
  need jq
  local item="" repo="" url body
  while [ $# -gt 1 ]; do
    case "$1" in
      --item) item="$2"; shift 2 ;;
      --repo) repo="$2"; shift 2 ;;
      *) break ;;
    esac
  done
  [ $# -eq 1 ] && [ -n "$1" ] || die "usage: request [--item ID] [--repo R] <text>"
  if [ -n "$item" ] && ! [[ "$item" =~ ^[A-Za-z]+-[A-Za-z0-9]+$ ]]; then die "request: '$item' is not an item id"; fi
  if [ -n "$repo" ] && ! [[ "$repo" =~ ^[A-Za-z0-9._-]+$ ]]; then die "request: '$repo' is not a repo name"; fi
  url="$("$PROVE" url)" || return
  body="$(jq -nc --arg text "$1" --arg item "$item" '{kind: "rescope", text: $text} + (if $item == "" then {} else {itemId: $item} end)')"
  if [ -n "$repo" ]; then
    curl -sS -X POST -H 'Content-Type: application/json' -H "X-Sindri-Repo: $repo" -d "$body" "$url/sindri/requests"
  else
    curl -sS -X POST -H 'Content-Type: application/json' -d "$body" "$url/sindri/requests"
  fi
}

decide() {
  [[ "$1" =~ ^[A-Za-z]+-[A-Za-z0-9]+$ ]] || die "decide: '$1' is not a decision id"
  [[ "$2" =~ ^[A-Za-z0-9._-]+$ ]] || die "decide: '$2' is not a choice"
  "$PROVE" human decide "$1" "$2"
}

# One row, the same one every time (INSERT OR IGNORE on its idempotency key), at the ledger's current epoch. The ledger's
# reader takes only GUIDs for id and correlation_id and the 7-digit round-trip timestamp for occurred_at, or the row
# is rejected, the projection never stalls and /events answers 500.
stall() {
  need_sql
  local type="${STALL_TYPE:-service.ran}"
  case "$type" in *[!a-z0-9._-]*) die "STALL_TYPE must be an event type (a-z, 0-9, . _ -): $type" ;; esac
  ledger "INSERT OR IGNORE INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
          VALUES ('$STALL_ID', '$type', 1, strftime('%Y-%m-%dT%H:%M:%S', 'now') || '.0000000Z', 'prover:fixture', 'local', 0, '$STALL_CORRELATION',
                  COALESCE((SELECT MAX(epoch) FROM events), 0), '$STALL_KEY', '{not json');
          SELECT seq || ' ' || type FROM events WHERE idempotency_key = '$STALL_KEY';"
}

unstall() {
  need_sql
  ledger "DELETE FROM events WHERE idempotency_key = '$STALL_KEY'; SELECT changes() || ' row(s) removed';"
}

# A changed cadence changes the profile's hash (comments and key order don't), so the approval no longer matches.
drift() {
  local profile
  profile="$(profile_path)"
  [ -f "$profile" ] || die "no live profile at $profile: run 'up' first"
  if grep -q 'every: 5s' "$profile"; then
    sed -i.bak 's/every: 5s/every: 7s/' "$profile"
  else
    sed -i.bak 's/every: 7s/every: 5s/' "$profile"
  fi
  rm -f "$profile.bak"
  grep 'every:' "$profile"
}

down() { "$PROVE" human stop; }

# The default fixture's items (UI spec §16's queue, U2's spec-shaped queue/fixture.json), oldest first.
ITEMS=("Stream limits 429 fallback" "Retry storm in outbox dispatcher" "Context map review for tabs.layout" "Try the queue")

add() {
  if [ -n "${FIXTURE_REPO:-}" ]; then "$PROVE" human item add "$1" --repo "$FIXTURE_REPO"; else "$PROVE" human item add "$1"; fi
}

items() {
  local title n
  for title in "${ITEMS[@]}"; do add "$title" || return; done
  [ "${1:-}" = "--large" ] || return 0
  for n in $(seq 1 30); do add "Large fixture item $n" || return; done
  # 12 requests across two repos (U3's large Needs-you list): six each, filed as a session.
  local repo="${FIXTURE_REPO:-demo}"
  for n in $(seq 1 6); do
    request --repo "$repo" "Large fixture request $n for $repo" || return
    request --repo "$repo-two" "Large fixture request $n for $repo-two" || return
  done
}

close() {
  [[ "$1" =~ ^[A-Za-z]+-[A-Za-z0-9]+$ ]] || die "close: '$1' is not an item id"
  if [ "${2:-}" = "--cancelled" ]; then "$PROVE" human item close "$1" --cancelled; else "$PROVE" human item close "$1"; fi
}

verb="${1:-}"
case "$verb" in
  -h | --help) usage; exit 0 ;;
  "") usage >&2; exit 2 ;;
  --large) die "--large belongs to 'items': sindri-ui-fixture.sh items --large" ;;
  up | stall | unstall | drift | down | fail | unfail) [ "$#" -eq 1 ] || die "'$verb' takes no arguments" ;;
  items) [ "$#" -eq 1 ] || { [ "$#" -eq 2 ] && [ "$2" = "--large" ]; } || die "usage: items [--large]" ;;
  close) [ "$#" -eq 2 ] || { [ "$#" -eq 3 ] && [ "$3" = "--cancelled" ]; } || die "usage: close <item> [--cancelled]" ;;
  request) [ "$#" -ge 2 ] || die "usage: request [--item ID] [--repo R] <text>" ;;
  decide) [ "$#" -eq 3 ] || die "usage: decide <DEC-id> <choice>" ;;
  *) usage >&2; die "unknown verb: $verb" ;;
esac

guard
shift
"$verb" "$@"
