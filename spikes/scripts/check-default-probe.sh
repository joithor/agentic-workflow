#!/usr/bin/env bash
# Regression check: building r9-ids/VogenDefaultProbe must fail with VOG009 (Vogen bans default(ItemId)).
set -u
log=$(mktemp)
dotnet build r9-ids/VogenDefaultProbe/VogenDefaultProbe.csproj -c Release </dev/null >"$log" 2>&1
rc=$?
grep -oE "error VOG009: [^[]*" "$log" | sort -u
if [ "$rc" -ne 0 ] && grep -q "error VOG009" "$log"; then echo "OK (build exit $rc, VOG009)"; exit 0; fi
echo "FAIL (build exit $rc)"; exit 1
