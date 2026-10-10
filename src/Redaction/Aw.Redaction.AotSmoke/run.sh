#!/usr/bin/env bash
# NativeAOT canary for Aw.Redaction: publish this project with PublishAot for the host RID, fail on any trim/AOT
# (IL2xxx/IL3xxx) warning, run the native binary and compare its stdout with expected.txt. Exit 0 only when identical
# and the publish had no trim/AOT warnings (1 = check failed, 2 = missing tool). The .NET job of the CI workflow runs
# this script. Output goes to artifacts/aot-redaction-smoke/ (gitignored).
set -euo pipefail

command -v dotnet >/dev/null 2>&1 || { echo "aot-redaction-smoke: dotnet not found on PATH" >&2; exit 2; }

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../../artifacts/aot-redaction-smoke"
mkdir -p "$out"

dotnet publish "$here/Aw.Redaction.AotSmoke.csproj" -c Release -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 \
  || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi

"$out/bin/Aw.Redaction.AotSmoke" </dev/null >"$out/actual.txt"
if ! cmp -s "$out/actual.txt" "$here/expected.txt"; then
  diff "$here/expected.txt" "$out/actual.txt" | head -20 >&2
  exit 1
fi
echo "aot-redaction-smoke: native binary output matches expected.txt ($(wc -l <"$out/actual.txt" | tr -d ' ') lines)"
