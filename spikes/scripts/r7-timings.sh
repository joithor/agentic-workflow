#!/usr/bin/env bash
# R7 timings: wall time of `dotnet test` for the xUnit v3 and TUnit spike projects.
#   cold    = project obj/ and bin/ deleted + `dotnet build-server shutdown`, then `dotnet test`
#   warm    = `dotnet test` again with no changes (incremental build), 3 runs
#   nobuild = `dotnet test --no-build`, 3 runs
#   exe     = the test app run directly (MTP test projects are executables), 3 runs
# Run from spikes/ under the heavy-job lock. Prints one line per measurement.
set -u
now_ms() { perl -MTime::HiRes=time -e 'printf "%d", time()*1000'; }
measure() { # label cmd...
  local t0 t1 rc
  t0=$(now_ms)
  "${@:2}" </dev/null >/dev/null 2>&1
  rc=$?
  t1=$(now_ms)
  printf '%-28s %6d ms  exit %d\n' "$1" "$((t1 - t0))" "$rc"
}
for t in XunitV3 TUnit; do
  proj="r7-tests/$t.Tests/$t.Tests.csproj"
  dir="r7-tests/$t.Tests"
  for d in "$dir/obj" "$dir/bin"; do [ -d "$d" ] && find "$d" -delete; done
  dotnet build-server shutdown >/dev/null 2>&1
  measure "$t cold" dotnet test --project "$proj"
  for i in 1 2 3; do measure "$t warm #$i" dotnet test --project "$proj"; done
  for i in 1 2 3; do measure "$t no-build #$i" dotnet test --project "$proj" --no-build; done
  exe="$dir/bin/Debug/net10.0/osx-arm64/$t.Tests"
  for i in 1 2 3; do measure "$t exe #$i" "$exe"; done
done
