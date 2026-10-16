#!/usr/bin/env bash
# NativeAOT smoke for aw-packets (src/Packets/Aw.Packets.App): publish it with PublishAot for the host RID, fail on any
# trim/AOT (IL2xxx/IL3xxx) warning, then run the native binary against a temp git repository holding the packet in
# packet/ (its baseSha is set to the temp commit): validate, validate a broken copy, freeze twice. The normalized
# output must equal expected.txt, and the run must write nothing outside $AW_STATE_DIR/packets/.
# Output goes to artifacts/aot-packets-smoke/ (gitignored). The publish runs on a copy of the files it needs (the root
# build files, src/Aw.Kernel, src/Aw.Kernel.Telemetry and src/Redaction (the host starts telemetry, which scrubs through
# Redaction), src/Aw.Analyzers (every production project loads it), src/Packets): PublishAot with a RID
# makes restore add the host RID's ILCompiler packs to the lock files, so it never runs on the checkout, whose lock files
# stay RID-free.
set -euo pipefail
# The binary writes Kernel telemetry to stderr, which expected.txt captures, and log lines carry wall-clock timestamps:
# pin the level that writes none, whatever the caller set (docs/csharp/telemetry.md, the CLI stderr contract).
export AW_LOG_LEVEL=none

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../../artifacts/aot-packets-smoke"
mkdir -p "$out"

root="$(cd "$here/../../.." && pwd)"
tree="$out/tree"
[ ! -d "$tree" ] || find "$tree" -delete
mkdir -p "$tree"
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt \
  src/Aw.Kernel src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/Packets | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
dotnet publish "$tree/src/Packets/Aw.Packets.App/Aw.Packets.App.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false \
  -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi

work="$(mktemp -d "$out/run.XXXXXX")"
trap 'find "$work" -delete' EXIT
repo="$work/repo"
mkdir -p "$repo/src" "$repo/docs"
printf 'x\n' >"$repo/README.md"
printf 'x\n' >"$repo/src/a.cs"
printf 'x\n' >"$repo/docs/guide.md"
git -C "$repo" init -q
git -C "$repo" add -A
git -C "$repo" -c user.name=smoke -c user.email=smoke@example.invalid commit -q -m base
sha="$(git -C "$repo" rev-parse HEAD)"
cp -R "$here/packet" "$repo/packet"
sed "s/__BASE_SHA__/$sha/" "$here/packet/packet.json" >"$repo/packet/packet.json"
cp -R "$repo/packet" "$work/broken"
sed 's/"deps": \[\]/"deps": ["t2"]/' "$repo/packet/tasks.json" >"$work/broken/tasks.json"
git -C "$repo" add -A
git -C "$repo" -c user.name=smoke -c user.email=smoke@example.invalid commit -q -m packet

export AW_STATE_DIR="$work/state"
bin="$out/bin/aw-packets"
{
  echo "## validate"
  "$bin" validate "$repo/packet" --repo "$repo" || echo "exit $?"
  echo "## validate broken"
  "$bin" validate "$work/broken" --repo "$repo" || echo "exit $?"
  echo "## freeze"
  "$bin" freeze "$repo/packet" --repo "$repo" || echo "exit $?"
  echo "## freeze again"
  "$bin" freeze "$repo/packet" --repo "$repo" --json | grep -o '"alreadyFrozen":[a-z]*' || echo "exit $?"
  echo "## no arguments"
  "$bin" || echo "exit $?"
  echo "## state files"
  (cd "$AW_STATE_DIR" && find . -type f | sort)
} >"$out/raw.txt" 2>&1
sed -E -e "s#$work#<tmp>#g" -e 's/[0-9a-f]{64}/<hex>/g' -e 's/[0-9a-f]{12}/<sha>/g' "$out/raw.txt" >"$out/actual.txt"
if ! cmp -s "$out/actual.txt" "$here/expected.txt"; then
  diff "$here/expected.txt" "$out/actual.txt" | head -40 >&2
  exit 1
fi
if [ -n "$(git -C "$repo" status --porcelain)" ]; then
  echo "aot-packets-smoke: the run changed the repository" >&2
  exit 1
fi
echo "aot-packets-smoke: native binary passed ($(du -h "$bin" | cut -f1) aw-packets)"
