#!/usr/bin/env bash
# Publish every spike exactly as docs/readiness/aot-spike.md measures it, writing one log per publish to
# logs/<area>-<project>[.<variant>].publish.log (the files the doc's warning counts come from), then print
# the warning table: occurrences of each IL/CS code per log, as `grep -oE 'warning (IL|CS)[0-9]{4}' | uniq -c`.
# Run from spikes/ (under the heavy-job lock). Release, osx-arm64, PublishAot=true throughout.
set -u
mkdir -p logs
pub() { # log-name project [extra msbuild args...]
  local log="logs/$1.publish.log" proj="$2"; shift 2
  dotnet publish "$proj" -c Release -r osx-arm64 -p:PublishAot=true "$@" </dev/null >"$log" 2>&1
  printf '%-44s exit %-3s %s\n' "$log" "$?" "$(grep -oE 'warning (IL|CS)[0-9]{4}' "$log" | awk '{print $2}' | sort | uniq -c | awk '{printf "%s×%s ", $2, $1}')"
}
for p in AdoNet EfCore DapperAot AdoNetSystemSqlite DapperFallback; do pub "r8-$p" "r8-data/$p/$p.csproj"; done
pub r8-AdoNet.icu r8-data/AdoNet/AdoNet.csproj -p:InvariantGlobalization=false -o out-icu
pub r8-DapperAot.warnaserror r8-data/DapperAot/DapperAot.csproj -p:TreatWarningsAsErrors=true -o out-wae-DapperAot
pub r8-EfCore.warnaserror r8-data/EfCore/EfCore.csproj -p:TreatWarningsAsErrors=true -o out-wae-EfCore
for p in HandIds VogenIds VogenHybrid RouteBind; do pub "r9-$p" "r9-ids/$p/$p.csproj"; done
for p in StdioLow StdioAttr HttpSlim; do pub "mcp-$p" "mcp/$p/$p.csproj"; done
for p in XunitV3.Tests TUnit.Tests TUnit.Bulk; do pub "r7-$p.aot" "r7-tests/$p/$p.csproj"; done
for p in XunitV3.Tests TUnit.Tests XunitV3.Bulk TUnit.Bulk; do
  dotnet build "r7-tests/$p/$p.csproj" -c Release </dev/null >"logs/r7-$p.build.log" 2>&1
  printf '%-44s exit %s\n' "logs/r7-$p.build.log" "$?"
done
