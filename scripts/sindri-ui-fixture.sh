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
# FIXTURE_REPO names the items' repo (default: none, so the API's default applies).
# Environment: AW_SINDRI_HOME (required: sindri-prove.sh's guard, a temp or proof folder), SINDRI (the built C# CLI,
# as sindri-prove.sh reads it), STALL_TYPE (the stalled row's type; default service.ran, a type the projector reads).
# Needs sqlite3 for stall and unstall, jq for up.
# Exit: 0 done; 2 the guard, a usage error or a missing tool; otherwise the failing step's exit code.
# Tests: bash scripts/tests/sindri-ui-fixture.test.sh
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROVE="$ROOT/scripts/sindri-prove.sh"
STALL_KEY="sindri-ui-fixture:stall"

die() { echo "sindri-ui-fixture: $*" >&2; exit 2; }
usage() { sed -n '5,21p' "$0" | sed 's/^# \{0,1\}//'; }

# sindri-prove.sh's guard alone: it runs before any verb, and SINDRI=true makes the verb a no-op.
guard() { SINDRI=true "$PROVE" guard >/dev/null || exit 2; }

need() { command -v "$1" >/dev/null 2>&1 || die "$1 is needed for '$verb'"; }

ledger() {
  [ -f "$AW_SINDRI_HOME/ledger.db" ] || die "no ledger at $AW_SINDRI_HOME/ledger.db: run 'up' first"
  sqlite3 -bail "$AW_SINDRI_HOME/ledger.db" "$1"
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
  hash="$("$PROVE" profile validate --json | jq -r '.hash // empty')"
  [ -n "$hash" ] || die "profile validate gave no hash"
  printf '%s\n' "${hash:0:6}" | "$PROVE" human profile approve "$hash" || return
  "$PROVE" human --headless
}

# One row, the same one every time (INSERT OR IGNORE on its idempotency key), at the ledger's current epoch.
stall() {
  need sqlite3
  local type="${STALL_TYPE:-service.ran}"
  case "$type" in *[!a-z0-9._-]*) die "STALL_TYPE must be an event type (a-z, 0-9, . _ -): $type" ;; esac
  ledger "INSERT OR IGNORE INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
          VALUES ('$STALL_KEY', '$type', 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'), 'prover:fixture', 'local', 0, '$STALL_KEY',
                  COALESCE((SELECT MAX(epoch) FROM events), 0), '$STALL_KEY', '{not json');
          SELECT seq || ' ' || type FROM events WHERE idempotency_key = '$STALL_KEY';"
}

unstall() {
  need sqlite3
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
  up | stall | unstall | drift | down) [ "$#" -eq 1 ] || die "'$verb' takes no arguments" ;;
  items) [ "$#" -eq 1 ] || { [ "$#" -eq 2 ] && [ "$2" = "--large" ]; } || die "usage: items [--large]" ;;
  close) [ "$#" -eq 2 ] || { [ "$#" -eq 3 ] && [ "$3" = "--cancelled" ]; } || die "usage: close <item> [--cancelled]" ;;
  *) usage >&2; die "unknown verb: $verb" ;;
esac

guard
shift
"$verb" "$@"
