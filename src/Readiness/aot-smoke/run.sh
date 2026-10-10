#!/usr/bin/env bash
# NativeAOT smoke for aw-readiness: publish the CLI for the host RID, fail on any trim/AOT (IL2xxx/IL3xxx) warning,
# then run the native binary twice, offline, on a small fixture checkout with a scratch AW_STATE_DIR, and compare the
# normalized output and exit codes with expected.txt (first run records a baseline; the second reports no change).
# Output goes to artifacts/aot-readiness/ (gitignored). A publish for a runtime (-r) restores RID-specific packages
# (the ILCompiler packs) into the lock files, so it runs unlocked and the committed packages.lock.json files are
# copied aside first and put back afterwards: the repo's lock files stay RID-free and the same on every OS.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../../artifacts/aot-readiness"
mkdir -p "$out"
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
root="$(cd "$here/../../.." && pwd)"

locks="$(mktemp -d "$out/locks.XXXXXX")"
(cd "$root" && git ls-files -z '*packages.lock.json' | xargs -0 -I{} sh -c 'mkdir -p "$1/$(dirname "$2")" && cp "$2" "$1/$2"' _ "$locks" {})
# Put the lock files back; keep the backup (and say where) unless every copy succeeded.
put_back() {
  [ -d "$locks" ] || return 0
  if cp -R "$locks/." "$root/"; then
    find "$locks" -delete
  else
    echo "aot-readiness-smoke: restoring lock files failed; the originals are in $locks" >&2
    exit 1   # an EXIT trap's return status doesn't set the script's exit code; exit does
  fi
}
trap put_back EXIT

dotnet publish "$here/../Aw.Readiness.App/Aw.Readiness.App.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false \
  -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }
if grep -qE 'IL[23][0-9]{3}' "$out/publish.log"; then
  grep -E 'IL[23][0-9]{3}' "$out/publish.log" >&2
  exit 1
fi

work="$(mktemp -d "$out/run.XXXXXX")"
trap 'put_back; find "$work" -delete || true' EXIT
repo="$work/repo"
state="$work/state"
mkdir -p "$repo/tests/Aw.Demo.Tests" "$state"
printf 'version: 1\nrepo: smoke\nring: 0\nscope: csharp-template\ntargets:\n  - ring: 0\n    level: 2\n' >"$repo/readiness.yaml"
printf '# Smoke\n' >"$repo/README.md"
printf 'dotnet build -warnaserror\ndotnet test --solution AgenticWorkflow.slnx\ndotnet format --verify-no-changes\n' >"$repo/AGENTS.md"
printf '[*.cs]\ndotnet_diagnostic.CA1031.severity = error\n' >"$repo/.editorconfig"
printf '<Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n' >"$repo/Directory.Build.props"
printf '<GlobalPackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" />\n' >"$repo/Directory.Packages.props"
printf '[Fact, Trait("Lane", "unit")]\n' >"$repo/tests/Aw.Demo.Tests/DemoTests.cs"

{
  for run in first second; do
    echo "== $run run"
    set +e
    NO_COLOR=1 AW_STATE_DIR="$state" "$out/bin/aw-readiness" check --root "$repo" --no-remote </dev/null
    echo "exit=$?"
    set -e
  done
  echo "== bad flag"
  set +e
  "$out/bin/aw-readiness" check --bogus </dev/null 2>&1
  echo "exit=$?"
  set -e
} | sed -e "s#$state#<state>#g" -e 's#/[0-9]\{8\}T[0-9]\{13\}Z-[0-9]\{3\}\.json#/<run>.json#' \
  -e 's#since [0-9-]*T[0-9:]*Z#since <time>#' >"$out/actual.txt"

if ! cmp -s "$out/actual.txt" "$here/expected.txt"; then
  diff "$here/expected.txt" "$out/actual.txt" | head -30 >&2
  exit 1
fi
if find "$state" -type f | grep -v "^$state/readiness/" | grep -q .; then
  echo "aot-readiness-smoke: wrote outside readiness/" >&2
  exit 1
fi
echo "aot-readiness-smoke: native binary passed ($(du -h "$out/bin/aw-readiness" | cut -f1), files: $(ls "$out/bin" | grep -v -E '\.(pdb|xml|dSYM)$' | tr '\n' ' '))"
