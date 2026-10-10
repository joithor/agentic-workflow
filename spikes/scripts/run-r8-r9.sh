#!/usr/bin/env bash
# Run every published R8/R9 binary against a fresh temp DB, then cross-check the DB with the sqlite3 CLI
# (a different code path from the binary). Also exercises the error path (DB in a missing directory).
# Run from spikes/ after publishing. Exit 0 only when every binary passes and every cross-check matches.
set -u
tmp=$(mktemp -d)
fails=0
pub() { echo "$1/bin/Release/net10.0/osx-arm64/publish/$(basename "$1")"; }
for p in r8-data/AdoNet r8-data/AdoNetSystemSqlite r8-data/EfCore r8-data/DapperAot r9-ids/HandIds r9-ids/VogenIds r9-ids/VogenHybrid; do
  b=$(pub "$p"); n=$(basename "$p"); db="$tmp/$n.db"
  echo "## $n"
  "$b" "$db" </dev/null 2>&1 | head -20
  rc=${PIPESTATUS[0]}
  echo "exit $rc"
  [ "$rc" -eq 0 ] || { echo "FAIL $n exit $rc"; fails=$((fails + 1)); }
  case "$p" in
    r8-*)
      got=$(sqlite3 "$db" "select id||'|'||name||'|'||size from items order by id" | tr '\n' ' ')
      jm=$(sqlite3 "$db" "pragma journal_mode")
      echo "sqlite3: rows [$got] journal_mode=$jm"
      [ "$got" = "a|alpha|1 b|beta-2|20 d|delta|4 e|epsilon|5 " ] && [ "$jm" = wal ] || { echo "FAIL $n cross-check"; fails=$((fails + 1)); }
      "$b" "$tmp/missing-dir/x.db" </dev/null >/dev/null 2>&1
      erc=$?
      echo "error path (missing dir): exit $erc"
      [ "$erc" -eq 2 ] || { echo "FAIL $n error path"; fails=$((fails + 1)); } ;;
    r9-*)
      got=$(sqlite3 "$db" "select id||'|'||budget from ids")
      echo "sqlite3: [$got]"
      [ "$got" = "item-1|1200" ] || { echo "FAIL $n cross-check"; fails=$((fails + 1)); } ;;
  esac
done
[ "$fails" -eq 0 ] && echo OK || echo "FAILED $fails"
[ "$fails" -eq 0 ]
