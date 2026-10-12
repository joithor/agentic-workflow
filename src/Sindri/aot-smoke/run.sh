#!/usr/bin/env bash
# Publish smoke for sindri (src/Sindri/Aw.Sindri.Host), CI's AOT-job entry for the host (engine spec rev 8 §11, heavy
# lane: "the framework-dependent publish of the Sindri host"). The host is framework-dependent, not NativeAOT: it hosts
# the Temporal worker through Aw.Sindri.Workflows and the Temporal .NET SDK has no trimming support
# (temporalio/sdk-dotnet#286; .agents/rules/csharp.md "AOT scope"). So this publishes the host framework-dependent and
# portable (no runtime identifier, no self-contained runtime) with warnings as errors, checks that the output is
# framework-dependent and carries the Temporal SDK with its native bridge, then drives the published apphost 'sindri'
# (DOTNET_ROOT set from the 'dotnet' on PATH when unset) over the S1 journeys against a temp home: doctor with nothing running, start headless with an unapproved
# profile, a second serve refused by the lease, the approve preview and its non-terminal refusal, status, stop, and
# doctor again; then recipe validate on the shipped recipes (their hashes; workflow.item reads its child beside it) and
# on an invalid fixture (RCP-REACH-001, exit 1). The normalized output (pids, ports, hashes, times, the host name and the
# temp path masked) must equal expected.txt.
# The apphost, not 'dotnet sindri.dll': start re-launches its own executable as 'serve' (Environment.ProcessPath), which
# under 'dotnet sindri.dll' is the dotnet muxer.
# Every invocation is bounded (30 s, perl's alarm); one that runs over prints 'exit 124'.
# Output goes to artifacts/aot-sindri-smoke/ (gitignored). The publish runs on a copy of the files it needs, so the
# checkout's lock files and build output are never touched (a publish without a runtime identifier keeps the lock files
# as they are; the copy keeps it from sharing obj/ with a build running beside it).
set -euo pipefail
export AW_LOG_LEVEL=none

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
out="$root/artifacts/aot-sindri-smoke"
mkdir -p "$out"
tree="$out/tree"
[ ! -d "$tree" ] || find "$tree" -delete
[ ! -d "$out/bin" ] || find "$out/bin" -delete
mkdir -p "$tree"
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' BannedSymbols.txt CodeMetricsConfig.txt \
  src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/Sindri | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
dotnet publish "$tree/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj" -c Release --no-self-contained -warnaserror \
  -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }

# Framework-dependent: the runtimeconfig names the shared framework and no runtime ships beside the host.
bin="$out/bin"
grep -q '"name": "Microsoft.AspNetCore.App"' "$bin/sindri.runtimeconfig.json" || { echo "aot-sindri-smoke: sindri.runtimeconfig.json names no shared framework" >&2; exit 1; }
[ ! -e "$bin/System.Private.CoreLib.dll" ] || { echo "aot-sindri-smoke: the publish is self-contained (System.Private.CoreLib.dll beside the host)" >&2; exit 1; }
# The Temporal SDK and its native core bridge for this platform are in the publish output.
[ -f "$bin/Temporalio.dll" ] || { echo "aot-sindri-smoke: Temporalio.dll is missing from the publish output" >&2; exit 1; }
os="$(uname -s)"; arch="$(uname -m)"
case "$os/$arch" in
  Darwin/arm64) bridge=runtimes/osx-arm64/native/libtemporalio_sdk_core_c_bridge.dylib ;;
  Darwin/x86_64) bridge=runtimes/osx-x64/native/libtemporalio_sdk_core_c_bridge.dylib ;;
  Linux/x86_64) bridge=runtimes/linux-x64/native/libtemporalio_sdk_core_c_bridge.so ;;
  Linux/aarch64) bridge=runtimes/linux-arm64/native/libtemporalio_sdk_core_c_bridge.so ;;
  *) bridge="" ;;
esac
[ -z "$bridge" ] || [ -f "$bin/$bridge" ] || { echo "aot-sindri-smoke: the Temporal core bridge $bridge is missing from the publish output" >&2; exit 1; }
[ -x "$bin/sindri" ] || { echo "aot-sindri-smoke: the apphost 'sindri' is missing from the publish output" >&2; exit 1; }
if [ -z "${DOTNET_ROOT:-}" ]; then
  DOTNET_ROOT="$(cd "$(dirname "$(perl -MCwd=abs_path -e 'print abs_path(shift)' "$(command -v dotnet)")")" && pwd)"
  export DOTNET_ROOT
fi

work="$(mktemp -d "$out/run.XXXXXX")"
export AW_SINDRI_HOME="$work/home" AW_SINDRI_PORT=0
bounded() { perl -e 'alarm shift; exec @ARGV or die "exec $ARGV[0]: $!"' 30 "$@"; }   # SIGALRM after 30 s
cleanup() { bounded "$bin/sindri" stop >/dev/null 2>&1 || true; find "$work" -delete; }
trap cleanup EXIT
mkdir -p "$AW_SINDRI_HOME/profile"
printf 'schemaVersion: 1\nuser: smoke\nservices:\n  heartbeat: { every: 5s }\n' >"$AW_SINDRI_HOME/profile/profile.yaml"

run() {
  echo "## sindri $*"
  local rc=0
  bounded "$bin/sindri" "$@" </dev/null 2>&1 || rc=$?
  [ "$rc" -ne 142 ] || rc=124   # killed by the alarm
  [ "$rc" -eq 0 ] || echo "exit $rc"
}
{
  run doctor
  run --headless
  run serve
  run profile approve
  hash="$({ bounded "$bin/sindri" profile validate --json </dev/null || true; } | sed -E 's/.*"hash":"([0-9a-f]{64})".*/\1/')"
  run profile approve "$hash"
  run status
  run stop
  run status
  run doctor
  run recipe validate "$root/recipes/sindri/workflow.step.yaml"
  run recipe validate "$root/recipes/sindri/workflow.item.yaml"
  run recipe validate "$root/tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/RCP-REACH-001.yaml"
} >"$out/actual.raw"

sed -E -e "s#$work#WORK#g" -e "s#$root#ROOT#g" -e "s#held by $(hostname -s)[^/ ]*#held by HOST#g" -e 's/[0-9a-f]{64}/HASH/g' -e 's/hash [0-9a-f]{6}/hash HASH6/g' \
  -e 's/pid [0-9]+/pid PID/g' -e 's#HOST/[0-9]+#HOST/PID#g' -e 's/127\.0\.0\.1:[0-9]+/127.0.0.1:PORT/g' \
  -e 's/[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:]{8}Z/TIME/g' -e 's/\(last [0-9:]{8}Z\)/(last TIME)/g' "$out/actual.raw" >"$out/actual.txt"
diff -u "$here/expected.txt" "$out/actual.txt"
echo "aot-sindri-smoke: ok (framework-dependent publish, $(du -sh "$bin" | cut -f1))"
