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
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" --large 2>&1)"; check "--large is reserved (exit 2)" "$?" "2"
contains "--large says it is reserved" "$out" "reserved for U2/U3"
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" stall extra 2>&1)"; check "extra arguments exit 2" "$?" "2"

# --- the guard (sindri-prove.sh's): nothing is written outside a temp or proof home ---
out="$(env -u AW_SINDRI_HOME bash "$FIXTURE" up 2>&1)"; check "an unset home is refused" "$?" "2"
contains "the refusal is the prover's" "$out" "sindri-prove:"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" up >/dev/null 2>&1; check "a home outside TMPDIR is refused" "$?" "2"
check "the refused up wrote no profile" "$(ls -A "$WORK/elsewhere")" ""
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" drift >/dev/null 2>&1; check "drift outside TMPDIR is refused" "$?" "2"

# --- stall / unstall against a ledger made from the first migration ---
command -v sqlite3 >/dev/null || { echo "not ok - sqlite3 is needed"; exit 1; }
sqlite3 "$H/ledger.db" < "$ROOT/src/Sindri/Aw.Sindri.Infrastructure/Ledger/Migrations/0001_events.sql"
sqlite3 "$H/ledger.db" "INSERT INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
  VALUES ('e1', 'process.started', 1, '2026-10-12T12:00:00Z', 'host:sindri', 'local', 0, 'c1', 3, 'process.started:3', '{}');"

out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" stall 2>&1)"; check "stall exits 0" "$?" "0"
check "stall prints the row it inserted" "$out" "2 service.ran"
row="$(sqlite3 "$H/ledger.db" "SELECT type, epoch, json_valid(payload_json), actor FROM events WHERE seq = 2")"
check "the row is a registered type at the ledger's epoch with an unparseable payload" "$row" "service.ran|3|0|prover:fixture"
AW_SINDRI_HOME="$H" bash "$FIXTURE" stall >/dev/null 2>&1
check "a second stall adds nothing" "$(sqlite3 "$H/ledger.db" "SELECT COUNT(*) FROM events")" "2"
out="$(AW_SINDRI_HOME="$H" STALL_TYPE="x'; DROP TABLE events; --" bash "$FIXTURE" stall 2>&1)"; check "a STALL_TYPE that isn't a type is refused" "$?" "2"
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" unstall 2>&1)"; check "unstall exits 0" "$?" "0"
check "unstall says what it removed" "$out" "1 row(s) removed"
check "unstall leaves the other rows" "$(sqlite3 "$H/ledger.db" "SELECT GROUP_CONCAT(type) FROM events")" "process.started"
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
check "up validates, approves with the hash's first six characters, then starts headless" "$(tr '\n' ';' < "$STUB_LOG")" \
  "profile validate --json;profile approve abcdef0123456789;typed=abcdef;--headless;"
AW_SINDRI_HOME="$U" bash "$FIXTURE" drift >/dev/null 2>&1; check "drift exits 0" "$?" "0"
contains "drift changes the cadence" "$(cat "$U/profile/profile.yaml")" "every: 7s"
AW_SINDRI_HOME="$U" bash "$FIXTURE" drift >/dev/null 2>&1
contains "drift again changes it back" "$(cat "$U/profile/profile.yaml")" "every: 5s"
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" down >/dev/null 2>&1; check "down exits 0" "$?" "0"
check "down stops it" "$(cat "$STUB_LOG")" "stop"

exit "$fail"
