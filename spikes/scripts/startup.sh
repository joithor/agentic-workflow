#!/usr/bin/env bash
# Startup / end-to-end wall time of each published spike binary: 1 warm-up run, then N timed runs
# (fresh temp DB each run), reporting min / median / max in ms, plus size and trim/AOT warning count.
# Run from spikes/ after publishing, ideally on an otherwise idle machine (results vary with load).
set -u
N=${N:-15}
now_ms() { perl -MTime::HiRes=time -e 'printf "%d", time()*1000'; }
tmp=$(mktemp -d)
printf '%-12s %10s %7s %7s %7s %9s\n' binary size_MB min_ms med_ms max_ms IL_warns
for p in r8-data/AdoNet r8-data/EfCore r8-data/DapperAot r9-ids/HandIds r9-ids/VogenIds r9-ids/VogenHybrid; do
  n=$(basename "$p"); b="$p/bin/Release/net10.0/osx-arm64/publish/$n"
  "$b" "$tmp/warm-$n.db" </dev/null >/dev/null 2>&1
  times=()
  for i in $(seq 1 "$N"); do
    t0=$(now_ms); "$b" "$tmp/$n-$i.db" </dev/null >/dev/null 2>&1; t1=$(now_ms)
    times+=($((t1 - t0)))
  done
  sorted=($(printf '%s\n' "${times[@]}" | sort -n))
  size=$(stat -f %z "$b")
  log=$(ls -t logs/*"$n"*.publish*.log 2>/dev/null | head -1)
  warns=$( [ -n "$log" ] && grep -oE 'warning IL[0-9]{4}: [^[]*' "$log" | sed -E 's/^.*(warning IL[0-9]{4}: )/\1/' | sort -u | wc -l | tr -d ' ' || echo '?')
  printf '%-12s %10.2f %7d %7d %7d %9s\n' "$n" "$(echo "$size/1048576" | bc -l)" "${sorted[0]}" "${sorted[$((N / 2))]}" "${sorted[$((N - 1))]}" "$warns"
done
