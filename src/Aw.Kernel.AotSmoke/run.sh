#!/usr/bin/env bash
# NativeAOT publish check for Aw.Kernel: publish this project with PublishAot for the host RID, run the native
# binary, and compare its stdout with expected.txt. Exit 0 only when identical and the publish had no trim/AOT
# (IL2xxx/IL3xxx) warnings. R1a's AOT CI job runs this script. The project has no lock file and takes the host RID
# as a project property (see its .csproj), so publishing never rewrites Aw.Kernel's packages.lock.json.
# Output goes to artifacts/aot-smoke/ (gitignored, the C# build's own directory).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../artifacts/aot-smoke"
mkdir -p "$out"

dotnet publish "$here/Aw.Kernel.AotSmoke.csproj" -c Release -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 \
  || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi

"$out/bin/Aw.Kernel.AotSmoke" </dev/null >"$out/cs.out"
if ! cmp "$out/cs.out" "$here/expected.txt"; then
  diff "$out/cs.out" "$here/expected.txt" | head -20 >&2
  exit 1
fi
echo "aot-smoke: native binary output matches expected.txt ($(wc -c <"$out/cs.out" | tr -d ' ') bytes)"
