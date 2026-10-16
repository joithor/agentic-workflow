#!/usr/bin/env bash
# Tests for scripts/sindri-ui-fixture.sh. Run: bash scripts/tests/sindri-ui-fixture.test.sh
# No dotnet and no real sindri: the guard is sindri-prove.sh's own, stall and unstall run against a temp ledger.db made
# from the ledger's first migration, and up and down run a stub SINDRI that records its arguments.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { # $1 name, $2 haystack, $3 needle
  case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export TMPDIR="$WORK/tmp" AW_STATE_DIR="$WORK/state"
mkdir -p "$TMPDIR" "$AW_STATE_DIR" "$WORK/elsewhere" "$WORK/bin"
H="$TMPDIR/home"
mkdir -p "$H"

# --- usage ---
out="$(bash "$FIXTURE" 2>&1)"; check "no verb exits 2" "$?" "2"
contains "no verb prints usage" "$out" "sindri-ui-fixture.sh stall"
bash "$FIXTURE" --help >/dev/null 2>&1; check "--help exits 0" "$?" "0"
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" sideways 2>&1)"; check "an unknown verb exits 2" "$?" "2"
contains "an unknown verb is named" "$out" "unknown verb: sideways"
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" --large 2>&1)"; check "--large alone is a usage error (exit 2)" "$?" "2"
contains "--large points at items" "$out" "items --large"
AW_SINDRI_HOME="$H" bash "$FIXTURE" items --big >/dev/null 2>&1; check "items takes only --large (exit 2)" "$?" "2"
AW_SINDRI_HOME="$H" bash "$FIXTURE" close >/dev/null 2>&1; check "close needs an item (exit 2)" "$?" "2"
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" stall extra 2>&1)"; check "extra arguments exit 2" "$?" "2"

# --- the guard (sindri-prove.sh's): nothing is written outside a temp or proof home ---
out="$(env -u AW_SINDRI_HOME bash "$FIXTURE" up 2>&1)"; check "an unset home is refused" "$?" "2"
contains "the refusal is the prover's" "$out" "sindri-prove:"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" up >/dev/null 2>&1; check "a home outside TMPDIR is refused" "$?" "2"
check "the refused up wrote no profile" "$(ls -A "$WORK/elsewhere")" ""
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" drift >/dev/null 2>&1; check "drift outside TMPDIR is refused" "$?" "2"

# --- stall / unstall against a ledger made from the first migration ---
# sqlite3 CLI when there is one, else python3's module (this box has no CLI); the fixture has the same fallback.
command -v python3 >/dev/null || command -v sqlite3 >/dev/null || { echo "not ok - sqlite3 or python3 is needed"; exit 1; }
sqlq() { # $1 db, $2 sql: statements in order, rows printed '|'-joined
  if command -v sqlite3 >/dev/null 2>&1; then sqlite3 "$1" "$2"; else
    python3 -I -c '
import sqlite3, sys
con = sqlite3.connect(sys.argv[1], isolation_level=None)
for row in con.execute(sys.argv[2]):
    print("|".join("" if v is None else str(v) for v in row))' "$1" "$2"
  fi
}
migrate() { # $1 db, $2 migration file
  if command -v sqlite3 >/dev/null 2>&1; then sqlite3 "$1" < "$2"; else
    python3 -I -c '
import sqlite3, sys
con = sqlite3.connect(sys.argv[1], isolation_level=None)
con.executescript(open(sys.argv[2]).read())' "$1" "$2"
  fi
}
migrate "$H/ledger.db" "$ROOT/src/Sindri/Aw.Sindri.Infrastructure/Ledger/Migrations/0001_events.sql"
sqlq "$H/ledger.db" "INSERT INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
  VALUES ('e1', 'process.started', 1, '2026-10-12T12:00:00Z', 'host:sindri', 'local', 0, 'c1', 3, 'process.started:3', '{}');"

out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" stall 2>&1)"; check "stall exits 0" "$?" "0"
check "stall prints the row it inserted" "$out" "2 service.ran"
row="$(sqlq "$H/ledger.db" "SELECT type, epoch, json_valid(payload_json), actor FROM events WHERE seq = 2")"
check "the row is a registered type at the ledger's epoch with an unparseable payload" "$row" "service.ran|3|0|prover:fixture"
# K7(b): the ledger's reader takes GUIDs for id and correlation_id and the 7-digit round-trip timestamp, or it rejects the row.
shape="$(sqlq "$H/ledger.db" "SELECT id, correlation_id, occurred_at FROM events WHERE seq = 2")"
IFS='|' read -r row_id row_correlation row_at <<< "$shape"
guid='^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
[[ "$row_id" =~ $guid ]]; check "the stall row's id is a GUID" "$?" "0"
[[ "$row_correlation" =~ $guid ]]; check "the stall row's correlation_id is a GUID" "$?" "0"
[[ "$row_at" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{7}Z$ ]]; check "the stall row's occurred_at has seven fractional digits" "$?" "0"
AW_SINDRI_HOME="$H" bash "$FIXTURE" stall >/dev/null 2>&1
check "a second stall adds nothing" "$(sqlq "$H/ledger.db" "SELECT COUNT(*) FROM events")" "2"
out="$(AW_SINDRI_HOME="$H" STALL_TYPE="x'; DROP TABLE events; --" bash "$FIXTURE" stall 2>&1)"; check "a STALL_TYPE that isn't a type is refused" "$?" "2"
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" unstall 2>&1)"; check "unstall exits 0" "$?" "0"
check "unstall says what it removed" "$out" "1 row(s) removed"
check "unstall leaves the other rows" "$(sqlq "$H/ledger.db" "SELECT GROUP_CONCAT(type) FROM events")" "process.started"

# The python fallback (a box without the sqlite3 CLI) does the same.
out="$(AW_SINDRI_HOME="$H" SINDRI_UI_FIXTURE_SQL=python bash "$FIXTURE" stall 2>&1)"; check "stall through python3 exits 0" "$?" "0"
contains "stall through python3 prints the row it inserted" "$out" " service.ran"
check "the python3 row has the same shape" "$(sqlq "$H/ledger.db" "SELECT id, correlation_id, length(occurred_at) FROM events WHERE idempotency_key = 'sindri-ui-fixture:stall'")" "7f3c1a52-0b6e-4d1e-9a55-3c2f6d8e1b04|7f3c1a52-0b6e-4d1e-9a55-3c2f6d8e1b05|28"
out="$(AW_SINDRI_HOME="$H" SINDRI_UI_FIXTURE_SQL=python bash "$FIXTURE" unstall 2>&1)"; check "unstall through python3 says what it removed" "$out" "1 row(s) removed"

# A python3 without the sqlite3 module is a missing tool, not a traceback in the middle of a verb.
mkdir -p "$WORK/nosql"
printf '#!/bin/sh\ncase "$*" in *"import sqlite3"*) exit 1 ;; esac\nexit 0\n' > "$WORK/nosql/python3"
chmod +x "$WORK/nosql/python3"
out="$(PATH="$WORK/nosql:$PATH" AW_SINDRI_HOME="$H" SINDRI_UI_FIXTURE_SQL=python bash "$FIXTURE" stall 2>&1)"; check "python3 without sqlite3 exits 2" "$?" "2"
contains "it says which tool is missing" "$out" "sqlite3 (or python3) is needed for 'stall'"
out="$(AW_SINDRI_HOME="$TMPDIR/empty" bash "$FIXTURE" stall 2>&1)"; check "stall with no ledger exits 2" "$?" "2"
contains "stall with no ledger says to run up" "$out" "run 'up' first"

# --- up, drift and down with a stub SINDRI ---
STUB="$WORK/bin/sindri-stub"
cat > "$STUB" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$STUB_LOG"
case "$*" in
  "profile validate --json") echo '{"valid":true,"hash":"abcdef0123456789","approved":false}' ;;
  "profile approve abcdef0123456789") IFS= read -r line; echo "typed=$line" >> "$STUB_LOG" ;;
esac
exit 0
EOF
chmod +x "$STUB"
export STUB_LOG="$WORK/calls"
U="$TMPDIR/up"
mkdir -p "$U"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" up >/dev/null 2>&1; check "up exits 0" "$?" "0"
contains "up writes a profile with a heartbeat" "$(cat "$U/profile/profile.yaml")" "heartbeat: { every: 5s }"
check "up validates, approves by typing 'approve profile' and the hash's first six characters (K7a), then starts headless" "$(tr '\n' ';' < "$STUB_LOG")" \
  "profile validate --json;profile approve abcdef0123456789;typed=approve profile abcdef;--headless;"
AW_SINDRI_HOME="$U" bash "$FIXTURE" drift >/dev/null 2>&1; check "drift exits 0" "$?" "0"
contains "drift changes the cadence" "$(cat "$U/profile/profile.yaml")" "every: 7s"
AW_SINDRI_HOME="$U" bash "$FIXTURE" drift >/dev/null 2>&1
contains "drift again changes it back" "$(cat "$U/profile/profile.yaml")" "every: 5s"
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" down >/dev/null 2>&1; check "down exits 0" "$?" "0"
check "down stops it" "$(cat "$STUB_LOG")" "stop"

# --- items and close (U2): human item add / item close through sindri-prove.sh ---
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" items >/dev/null 2>&1; check "items exits 0" "$?" "0"
check "items adds the fixture's four items, oldest first" "$(tr '\n' ';' < "$STUB_LOG")" \
  "item add Stream limits 429 fallback;item add Retry storm in outbox dispatcher;item add Context map review for tabs.layout;item add Try the queue;"
: > "$STUB_LOG"
mkdir -p "$WORK/nocurl"; printf '#!/bin/sh\nexit 0\n' > "$WORK/nocurl/curl"; chmod +x "$WORK/nocurl/curl"
PATH="$WORK/nocurl:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" FIXTURE_REPO=demo bash "$FIXTURE" items --large >/dev/null 2>&1; check "items --large exits 0" "$?" "0"
check "items --large adds 34 items" "$(grep -c '^item add' "$STUB_LOG")" "34"
contains "FIXTURE_REPO names the repo" "$(head -1 "$STUB_LOG")" "--repo demo"
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" close ITM-0192a1b2 --cancelled >/dev/null 2>&1; check "close exits 0" "$?" "0"
check "close sends item close --cancelled" "$(cat "$STUB_LOG")" "item close ITM-0192a1b2 --cancelled"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" close 'ITM-1;rm' >/dev/null 2>&1; check "close refuses a bad id (exit 2)" "$?" "2"

# --- U3': request, decide, fail / unfail, items --large's requests ---
# A fake curl records its arguments; control.json gives the prover's `url` a base.
cat > "$WORK/bin/curl" <<'EOF'
#!/usr/bin/env bash
echo "curl $*" >> "$STUB_LOG"
exit 0
EOF
chmod +x "$WORK/bin/curl"
printf '{"url":"http://127.0.0.1:7999"}\n' > "$U/control.json"
absent() { case "$2" in *"$3"*) check "$1" "present" "absent" ;; *) check "$1" "absent" "absent" ;; esac; }
: > "$STUB_LOG"
PATH="$WORK/bin:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" request --item ITM-01 --repo demo "Split it" >/dev/null 2>&1; check "request exits 0" "$?" "0"
call="$(cat "$STUB_LOG")"
contains "request_sends_a_session_curl: a POST to /sindri/requests" "$call" "http://127.0.0.1:7999/sindri/requests"
contains "request names the repo scope" "$call" "X-Sindri-Repo: demo"
contains "request sends the item" "$call" '"itemId":"ITM-01"'
contains "request sends the text" "$call" '"text":"Split it"'
absent "request_sends_no_caller_header" "$call" "X-Sindri-Caller"
absent "request sends no token" "$call" "X-Sindri-Token"
: > "$STUB_LOG"
PATH="$WORK/bin:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" request "Plain" >/dev/null 2>&1
absent "request without flags sends no repo" "$(cat "$STUB_LOG")" "X-Sindri-Repo"
absent "request without flags sends no item" "$(cat "$STUB_LOG")" "itemId"
AW_SINDRI_HOME="$U" bash "$FIXTURE" request >/dev/null 2>&1; check "request needs text (exit 2)" "$?" "2"
AW_SINDRI_HOME="$U" bash "$FIXTURE" request --item 'x;y' hi >/dev/null 2>&1; check "request refuses a bad item id (exit 2)" "$?" "2"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" request hi >/dev/null 2>&1; check "every_verb_refuses_an_unguarded_home: request" "$?" "2"

: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" decide DEC-0192a1 accept >/dev/null 2>&1; check "decide exits 0" "$?" "0"
check "decide_runs_sindri_decide_as_the_owner" "$(cat "$STUB_LOG")" "decide DEC-0192a1 accept"
AW_SINDRI_HOME="$U" bash "$FIXTURE" decide 'DEC-1;rm' accept >/dev/null 2>&1; check "decide refuses a bad id (exit 2)" "$?" "2"
AW_SINDRI_HOME="$U" bash "$FIXTURE" decide DEC-1 >/dev/null 2>&1; check "decide needs a choice (exit 2)" "$?" "2"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" decide DEC-1 accept >/dev/null 2>&1; check "every_verb_refuses_an_unguarded_home: decide" "$?" "2"

: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" fail >/dev/null 2>&1; check "fail exits 0" "$?" "0"
contains "fail_adds_a_service_the_build_cannot_run" "$(cat "$U/profile/profile.yaml")" "broken: { every: 5s }"
contains "fail approves the changed profile" "$(tr '\n' ';' < "$STUB_LOG")" "profile approve abcdef0123456789;typed=approve profile abcdef;"
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" unfail >/dev/null 2>&1; check "unfail exits 0" "$?" "0"
absent "unfail_removes_it" "$(cat "$U/profile/profile.yaml")" "broken"
contains "unfail approves the profile again" "$(tr '\n' ';' < "$STUB_LOG")" "typed=approve profile abcdef;"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" fail >/dev/null 2>&1; check "every_verb_refuses_an_unguarded_home: fail" "$?" "2"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" unfail >/dev/null 2>&1; check "every_verb_refuses_an_unguarded_home: unfail" "$?" "2"

: > "$STUB_LOG"
PATH="$WORK/bin:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" items --large >/dev/null 2>&1; check "items --large exits 0" "$?" "0"
check "items_large_adds_30_items_and_12_requests_across_two_repos: 34 items" "$(grep -c '^item add' "$STUB_LOG")" "34"
check "items --large files 12 requests" "$(grep -c '^curl .*sindri/requests' "$STUB_LOG")" "12"
check "the requests span two repos" "$(grep -o 'X-Sindri-Repo: [a-z-]*' "$STUB_LOG" | sort -u | wc -l | tr -d ' ')" "2"

exit "$fail"
