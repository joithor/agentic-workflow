#!/usr/bin/env bash
# NativeAOT canary for Aw.Kernel.Data: publish this project with PublishAot for the host RID, fail on any trim/AOT
# (IL2xxx/IL3xxx) warning, run the native binary on a fresh directory and compare its stdout with expected.txt.
# Output goes to artifacts/aot-data-smoke/ (gitignored). The bundled libe_sqlite3 must sit beside the binary.
# The project has no lock file and takes the host RID as a project property (see its .csproj), so publishing never
# rewrites Aw.Kernel's or Aw.Kernel.Data's packages.lock.json.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../artifacts/aot-data-smoke"
mkdir -p "$out"

dotnet publish "$here/Aw.Kernel.Data.AotSmoke.csproj" -c Release -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 \
  || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi

work="$(mktemp -d "$out/run.XXXXXX")"
trap 'find "$work" -delete' EXIT
"$out/bin/Aw.Kernel.Data.AotSmoke" "$work" </dev/null >"$out/actual.txt"
if ! cmp -s "$out/actual.txt" "$here/expected.txt"; then
  diff "$here/expected.txt" "$out/actual.txt" | head -20 >&2
  exit 1
fi
echo "aot-data-smoke: native binary passed (files: $(ls "$out/bin" | grep -v -E '\.(pdb|xml|dSYM)$' | tr '\n' ' '))"
