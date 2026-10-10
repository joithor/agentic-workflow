#!/usr/bin/env bash
# NativeAOT byte-compat smoke for Aw.Kernel (spec §4c): publish this project with PublishAot for the host RID,
# run the native binary, and compare its stdout with node-expected.mjs byte for byte. Exit 0 only when identical
# and the publish had no trim/AOT (IL2xxx/IL3xxx) warnings. R1a's AOT CI job runs this script.
# Output goes to artifacts/aot-smoke/ (gitignored, the C# build's own directory).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../artifacts/aot-smoke"
mkdir -p "$out"

case "$(uname -s)-$(uname -m)" in
  Darwin-arm64) rid=osx-arm64 ;;
  Darwin-x86_64) rid=osx-x64 ;;
  Linux-aarch64) rid=linux-arm64 ;;
  *) rid=linux-x64 ;;
esac

dotnet publish "$here/Aw.Kernel.AotSmoke.csproj" -c Release -r "$rid" -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 \
  || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi

"$out/bin/Aw.Kernel.AotSmoke" </dev/null >"$out/cs.out"
node "$here/node-expected.mjs" </dev/null >"$out/node.out"
if ! cmp "$out/cs.out" "$out/node.out"; then
  diff <(od -c "$out/cs.out") <(od -c "$out/node.out") | head -20 >&2
  exit 1
fi
echo "aot-smoke: $rid native binary output identical to Node ($(wc -c <"$out/cs.out" | tr -d ' ') bytes)"
