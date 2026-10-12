#!/usr/bin/env bash
# Publish smoke for sindri (src/Sindri/Aw.Sindri.Host), CI's AOT-job entry for the host (engine spec rev 8 §11, heavy
# lane: "the framework-dependent publish of the Sindri host"). The host is framework-dependent, not NativeAOT: it hosts
# the Temporal worker through Aw.Sindri.Workflows and the Temporal .NET SDK has no trimming support
# (temporalio/sdk-dotnet#286; .agents/rules/csharp.md "AOT scope"). So this publishes the host framework-dependent and
# portable (no runtime identifier, no self-contained runtime) with warnings as errors, checks that the output is
# framework-dependent and carries the Temporal SDK with its native bridge, then drives the published apphost 'sindri'
# (DOTNET_ROOT set from the 'dotnet' on PATH when unset) through the S1 and API P1 journeys against a temp home (serve
# brings up the pinned Temporal dev server, so the temporal CLI must be installed: scripts/binaries-lock.sh fetch
# temporal): doctor with nothing running, start headless with an unapproved profile, a second serve refused by the lease,
# the approve preview and its non-terminal refusal, a session's typed approve refused (SND-AUTH-002), the human's typed
# approval, status (text and --json's apiVersion), a session's stop refused, the human's stop, status and doctor again;
# then recipe validate on the shipped recipes (their hashes; workflow.item reads its child beside it) and on an invalid
# fixture (RCP-REACH-001, exit 1). The normalized output (pids, ports, hashes, times, the host name and the temp path
# masked) must equal expected.txt.
# The apphost, not 'dotnet sindri.dll': start re-launches its own executable as 'serve' (Environment.ProcessPath), which
# under 'dotnet sindri.dll' is the dotnet muxer.
# Callers (API spec §6): the run unsets the agent variables (CLAUDECODE, AW_SINDRI_CHILD, CODEX_SANDBOX, CURSOR_AGENT)
# and sets AW_SINDRI_PROVER=1, so the binary is the human; a "(human, terminal)" step runs under a pty
# (scripts/lib/with-tty.sh: CRs and terminal control sequences stripped, stderr merged), a "(session)" step sets CLAUDECODE=1 with stdin from /dev/null.
# Every invocation of the binary is bounded (30 s, perl's alarm); one that runs over prints 'exit 124'.
# The publish runs on a copy of the files it needs, in a temp dir under $TMPDIR that is deleted on exit, so the
# checkout's lock files and build output are never touched.
# Kept in artifacts/aot-sindri-smoke/ (gitignored): bin/ (the publish output with the apphost), publish.log, actual.txt.
set -euo pipefail
export AW_LOG_LEVEL=none

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
# shellcheck source=../../../scripts/lib/with-tty.sh
source "$root/scripts/lib/with-tty.sh"
unset CLAUDECODE AW_SINDRI_CHILD CODEX_SANDBOX CURSOR_AGENT
export AW_SINDRI_PROVER=1
out="$root/artifacts/aot-sindri-smoke"
mkdir -p "$out"
[ ! -d "$out/bin" ] || find "$out/bin" -delete
alarm=(perl -e 'alarm shift; exec @ARGV or die "exec $ARGV[0]: $!"' 30)   # SIGALRM after 30 s; a command, so with_tty can run it
bounded() { "${alarm[@]}" "$@"; }
bin="$out/bin/sindri"
scratch=""
work=""
# On any exit, a server this run started is stopped before its home is deleted: only once this run's home is exported
# (never the owner's default home), as the human at a terminal (a session's stop is refused, SND-AUTH-002), and, should
# that fail, by signalling the pid control.json still names, then waiting (bounded) for it to exit. The scratch tree is
# deleted on any exit, so the trap is set before it is made.
cleanup() {
  [ -n "$scratch" ] || return 0
  local control="$work/home/control.json" pid
  if [ -x "$bin" ] && [ "${AW_SINDRI_HOME:-}" = "$work/home" ] && [ -f "$control" ]; then
    with_tty "${alarm[@]}" "$bin" stop </dev/null >/dev/null 2>&1 || true
    pid="$(jq -r '.pid // empty' "$control" 2>/dev/null || true)"
    if [ -n "$pid" ]; then
      kill "$pid" 2>/dev/null || true
      for _ in 1 2 3 4 5 6 7 8 9 10; do kill -0 "$pid" 2>/dev/null || break; sleep 0.2; done
    fi
  fi
  find "$scratch" -delete
}
trap cleanup EXIT
# The physical path: macOS's $TMPDIR is under /var, a link to /private/var, and restore given both spellings of one
# project writes its props twice ("…nuget.g.props already exists").
scratch="$(cd "$(mktemp -d "${TMPDIR:-/tmp}/aot-sindri-smoke.XXXXXX")" && pwd -P)"
tree="$scratch/tree"
work="$scratch/run"
mkdir -p "$tree" "$work"
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' BannedSymbols.txt CodeMetricsConfig.txt \
  src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/Sindri | (cd "$root" && tar --null -T - -cf -) | tar -xf - -C "$tree"
dotnet publish "$tree/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj" -c Release --no-self-contained -warnaserror   -o "$out/bin" </dev/null >"$out/publish.log" 2>&1 || { tail -20 "$out/publish.log" >&2; exit 1; }

# Framework-dependent: the runtimeconfig names the shared framework and no runtime ships beside the host.
pub="$out/bin"
grep -q '"name": "Microsoft.AspNetCore.App"' "$pub/sindri.runtimeconfig.json" || { echo "aot-sindri-smoke: sindri.runtimeconfig.json names no shared framework" >&2; exit 1; }
[ ! -e "$pub/System.Private.CoreLib.dll" ] || { echo "aot-sindri-smoke: the publish is self-contained (System.Private.CoreLib.dll beside the host)" >&2; exit 1; }
# The Temporal SDK and its native core bridge for this platform are in the publish output.
[ -f "$pub/Temporalio.dll" ] || { echo "aot-sindri-smoke: Temporalio.dll is missing from the publish output" >&2; exit 1; }
os="$(uname -s)"; arch="$(uname -m)"
case "$os/$arch" in
  Darwin/arm64) bridge=runtimes/osx-arm64/native/libtemporalio_sdk_core_c_bridge.dylib ;;
  Darwin/x86_64) bridge=runtimes/osx-x64/native/libtemporalio_sdk_core_c_bridge.dylib ;;
  Linux/x86_64) bridge=runtimes/linux-x64/native/libtemporalio_sdk_core_c_bridge.so ;;
  Linux/aarch64) bridge=runtimes/linux-arm64/native/libtemporalio_sdk_core_c_bridge.so ;;
  *) bridge="" ;;
esac
[ -z "$bridge" ] || [ -f "$pub/$bridge" ] || { echo "aot-sindri-smoke: the Temporal core bridge $bridge is missing from the publish output" >&2; exit 1; }
[ -x "$bin" ] || { echo "aot-sindri-smoke: the apphost 'sindri' is missing from the publish output" >&2; exit 1; }
if [ -z "${DOTNET_ROOT:-}" ]; then
  DOTNET_ROOT="$(cd "$(dirname "$(perl -MCwd=abs_path -e 'print abs_path(shift)' "$(command -v dotnet)")")" && pwd)"
  export DOTNET_ROOT
fi

export AW_SINDRI_HOME="$work/home" AW_SINDRI_PORT=0
mkdir -p "$AW_SINDRI_HOME/profile"
printf 'schemaVersion: 1\nuser: smoke\nservices:\n  heartbeat: { every: 5s }\n' >"$AW_SINDRI_HOME/profile/profile.yaml"

exit_line() { # $1 = exit code
  local rc="$1"
  [ "$rc" -ne 142 ] || rc=124   # killed by the alarm
  [ "$rc" -eq 0 ] || echo "exit $rc"
}
run() { # the human, no terminal
  echo "## sindri $*"
  local rc=0
  bounded "$bin" "$@" </dev/null 2>&1 || rc=$?
  exit_line "$rc"
}
human() { # the human at a terminal; stdin passed through (the typed confirmation)
  echo "## sindri $* (human, terminal)"
  local rc=0
  with_tty "${alarm[@]}" "$bin" "$@" || rc=$?
  exit_line "$rc"
}
session() { # an agent session (CLAUDECODE set), no terminal
  echo "## sindri $* (session)"
  local rc=0
  CLAUDECODE=1 bounded "$bin" "$@" </dev/null 2>&1 || rc=$?
  exit_line "$rc"
}
session_tty() { # an agent session at a terminal (the CLI's own terminal check passes, so the server decides)
  echo "## sindri $* (session, terminal)"
  local rc=0
  with_tty env CLAUDECODE=1 "${alarm[@]}" "$bin" "$@" || rc=$?
  exit_line "$rc"
}
{
  run doctor
  human --headless </dev/null
  run serve
  human profile approve </dev/null
  hash="$({ bounded "$bin" profile validate --json </dev/null || true; } | sed -E 's/.*"hash":"([0-9a-f]{64})".*/\1/')"
  run profile approve "$hash"
  printf '%s\n' "approve profile ${hash:0:6}" | session_tty profile approve "$hash"
  printf '%s\n' "approve profile ${hash:0:6}" | human profile approve "$hash"
  run status
  echo "## sindri status --json | jq '{running, apiVersion}'"
  json="$({ bounded "$bin" status --json </dev/null 2>&1 || true; })"
  jq -c '{running, apiVersion}' <<<"$json" 2>/dev/null || printf '%s\n' "$json"
  session stop
  human stop </dev/null
  run status
  run doctor
  run recipe validate "$root/recipes/sindri/workflow.step.yaml"
  run recipe validate "$root/recipes/sindri/workflow.item.yaml"
  run recipe validate "$root/tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/RCP-REACH-001.yaml"
} >"$out/actual.raw"

sed -E -e "s#$work#WORK#g" -e "s#$root#ROOT#g" -e "s#held by $(hostname -s)[^/ ]*#held by HOST#g" -e 's/[0-9a-f]{64}/HASH/g' -e 's/hash [0-9a-f]{6}/hash HASH6/g' -e 's/approve profile [0-9a-f]{6}/approve profile HASH6/g' \
  -e 's/pid [0-9]+/pid PID/g' -e 's#HOST/[0-9]+#HOST/PID#g' -e 's/127\.0\.0\.1:[0-9]+/127.0.0.1:PORT/g' \
  -e 's/[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:]{8}Z/TIME/g' -e 's/\(last [0-9:]{8}Z\)/(last TIME)/g' "$out/actual.raw" >"$out/actual.txt"
diff -u "$here/expected.txt" "$out/actual.txt"
echo "aot-sindri-smoke: ok (framework-dependent publish, $(du -sh "$pub" | cut -f1))"
