#!/usr/bin/env bash
# NativeAOT publish check for Aw.Kernel: publish this project with PublishAot for the host RID, run the native
# binary, and compare its stdout with expected.txt. Exit 0 only when identical and the publish had no trim/AOT
# (IL2xxx/IL3xxx) warnings; any other outcome exits non-zero (1 = check failed, 2 = missing tool). The .NET job of
# the CI workflow (#93) runs this script. The project has no lock file and takes the host RID (osx-arm64, linux-x64,
# ...) as a project property (see its .csproj), so publishing never writes a lock file, and Aw.Kernel's lock file
# restores unchanged, in locked mode under CI=true too. On Linux, NativeAOT needs clang and zlib (zlib1g-dev),
# which GitHub's ubuntu runners have.
# Output goes to artifacts/aot-smoke/ (gitignored, the C# build's own directory).
set -euo pipefail

command -v dotnet >/dev/null 2>&1 || { echo "aot-smoke: dotnet not found on PATH" >&2; exit 2; }

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
