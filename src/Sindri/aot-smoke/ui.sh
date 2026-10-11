#!/usr/bin/env bash
# NativeAOT publish check for the Sindri app's desktop head (src/Sindri/Aw.Sindri.Ui.Desktop, docs/csharp/sindri-ui.md):
# publish sindri-ui with PublishAot for the host RID and fail on any trim/AOT (IL2xxx/IL3xxx) warning or a missing
# binary. It doesn't open a window (CI has no display); the hand test and the headless view tests cover what it shows.
# Output goes to artifacts/aot-sindri-ui/ (gitignored); the publish runs on a copy, like run.sh, so the RID's ILCompiler
# packs never reach the checkout's lock files. Pass a directory to also copy sindri-ui there (next to a published sindri).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
out="$root/artifacts/aot-sindri-ui"
mkdir -p "$out"
tree="$out/tree"
[ ! -d "$tree" ] || find "$tree" -delete
mkdir -p "$tree"
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' BannedSymbols.txt CodeMetricsConfig.txt \
  src/Aw.Kernel src/Aw.Analyzers src/Sindri/Aw.Sindri.Ui src/Sindri/Aw.Sindri.Ui.Desktop | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
dotnet publish "$tree/src/Sindri/Aw.Sindri.Ui.Desktop/Aw.Sindri.Ui.Desktop.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false \
  -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi
[ -x "$out/bin/sindri-ui" ] || { echo "aot-sindri-ui: no sindri-ui binary in $out/bin" >&2; exit 1; }
if [ "${1:-}" != "" ]; then find "$out/bin" -maxdepth 1 -type f -exec cp {} "$1/" \; ; fi
echo "aot-sindri-ui: ok ($(du -h "$out/bin/sindri-ui" | cut -f1) binary; $(file -b "$out/bin/sindri-ui" | cut -d, -f1))"
