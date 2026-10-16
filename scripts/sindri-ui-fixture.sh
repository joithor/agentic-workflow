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
#                                  (items --large also files 12 requests, in FIXTURE_REPO when set, else with no repo)
#   sindri-ui-fixture.sh workflows [--large]
#                                  start the strip's instances through the CLI (sindri workflow start, as the owner, each on a
#                                  new item): one triage and two work instances; --large starts twelve roots across three recipes
#   sindri-ui-fixture.sh incident start|stop
#                                  start an incident-urgency instance (recipes/dev/incident-hold.yaml; it tops the strip) or cancel it
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
usage() { sed -n '5,36p' "$0" | sed 's/^# \{0,1\}//'; }

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
  body="$(jq -nc --arg text "$1" --arg item "$item" '{kind: "rescope", text: $text} + (if $item == "" then {} else {item: $item} end)')"
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
  # 12 requests (U3's large Needs-you list), filed as a session. The API refuses a repo the approved profile lacks and the
  # fixture's profile has none, so they name FIXTURE_REPO when it is set (a repo the profile has) and no repo otherwise.
  for n in $(seq 1 12); do
    if [ -n "${FIXTURE_REPO:-}" ]; then
      request --repo "$FIXTURE_REPO" "Large fixture request $n" || return
    else
      request "Large fixture request $n" || return
    fi
  done
}

close() {
  [[ "$1" =~ ^[A-Za-z]+-[A-Za-z0-9]+$ ]] || die "close: '$1' is not an item id"
  if [ "${2:-}" = "--cancelled" ]; then "$PROVE" human item close "$1" --cancelled; else "$PROVE" human item close "$1"; fi
}

# A new item, its id printed (the id is the first ITM- token sindri item add prints).
new_item() {
  local out id
  out="$(add "$1")" || return
  id="$(printf '%s\n' "$out" | grep -o 'ITM-[A-Za-z0-9]*' | head -1)"
  [ -n "$id" ] || die "item add printed no item id: $out"
  printf '%s\n' "$id"
}

# The inputs the dev recipes declare: triage-three-state takes fact:item (JSON naming the item), desk-round-trip takes
# fact:request (text). The files live in the guarded home, one folder for all of them.
fact_args() { # $1 recipe, $2 item, $3 title; prints the --fact arguments, one for each line
  local dir="$AW_SINDRI_HOME/ui-fixture-facts"
  mkdir -p "$dir" || return
  case "$1" in
    *triage-three-state)
      jq -nc --arg id "$2" '{id: $id}' > "$dir/$2.item.json" || return
      printf '%s\n%s\n' --fact "fact:item=$dir/$2.item.json" ;;
    *desk-round-trip)
      printf 'Fixture request for %s\n' "$3" > "$dir/$2.request.txt" || return
      printf '%s\n%s\n' --fact "fact:request=$dir/$2.request.txt" ;;
  esac
}

start_on_new_item() { # $1 recipe, $2 item title; prints the item
  local item facts=() line out
  need jq
  item="$(new_item "$2")" || return
  while IFS= read -r line; do facts+=("$line"); done < <(fact_args "$1" "$item" "$2")
  # The pty the owner's verbs run under joins stderr into stdout, so the output is kept to show on a refusal.
  if ! out="$("$PROVE" human workflow start "$1" --item "$item" "${facts[@]}" 2>&1)"; then
    die "workflow start $1 on $item was refused: $out"
  fi
  printf 'started %s on %s\n' "$1" "$item" >&2
  printf '%s\n' "$item"
}

# One triage and two work instances (screens §8.2); --large: twelve roots, four on each of three recipes.
workflows() {
  local n r
  if [ "${1:-}" = "--large" ]; then
    for n in 1 2 3 4; do
      for r in workflow.dev-triage-three-state workflow.dev-desk-round-trip workflow.dev-notify; do
        start_on_new_item "$r" "Large fixture workflow $r $n" >/dev/null || return
      done
    done
    return 0
  fi
  start_on_new_item workflow.dev-triage-three-state "Fixture triage" >/dev/null || return
  start_on_new_item workflow.dev-desk-round-trip "Fixture work one" >/dev/null || return
  start_on_new_item workflow.dev-desk-round-trip "Fixture work two" >/dev/null
}

incident_file() { printf '%s\n' "$AW_SINDRI_HOME/ui-fixture-incident-item"; }

incident() {
  local item file
  file="$(incident_file)"
  case "$1" in
    start)
      item="$(start_on_new_item workflow.dev-incident-hold "Fixture incident")" || return
      printf '%s\n' "$item" > "$file"
      printf '%s\n' "$item" ;;
    stop)
      [ -f "$file" ] || die "no incident started by this fixture: run 'incident start' first"
      item="$(cat "$file")"
      "$PROVE" human workflow cancel "$item" && rm -f -- "$file" && printf 'cancelled %s\n' "$item" ;;
  esac
}

verb="${1:-}"
case "$verb" in
  -h | --help) usage; exit 0 ;;
  "") usage >&2; exit 2 ;;
  --large) die "--large belongs to 'items': sindri-ui-fixture.sh items --large" ;;
  up | stall | unstall | drift | down | fail | unfail) [ "$#" -eq 1 ] || die "'$verb' takes no arguments" ;;
  items) [ "$#" -eq 1 ] || { [ "$#" -eq 2 ] && [ "$2" = "--large" ]; } || die "usage: items [--large]" ;;
  close) [ "$#" -eq 2 ] || { [ "$#" -eq 3 ] && [ "$3" = "--cancelled" ]; } || die "usage: close <item> [--cancelled]" ;;
  workflows) [ "$#" -eq 1 ] || { [ "$#" -eq 2 ] && [ "$2" = "--large" ]; } || die "usage: workflows [--large]" ;;
  incident) { [ "$#" -eq 2 ] && { [ "$2" = start ] || [ "$2" = stop ]; }; } || die "usage: incident start|stop" ;;
  request) [ "$#" -ge 2 ] || die "usage: request [--item ID] [--repo R] <text>" ;;
  decide) [ "$#" -eq 3 ] || die "usage: decide <DEC-id> <choice>" ;;
  *) usage >&2; die "unknown verb: $verb" ;;
esac

guard
shift
"$verb" "$@"
