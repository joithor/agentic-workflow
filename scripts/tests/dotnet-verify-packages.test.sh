#!/usr/bin/env bash
# Tests for scripts/dotnet-verify-packages.sh. Run: bash scripts/tests/dotnet-verify-packages.test.sh
# Uses a fake `dotnet` on DOTNET, so no SDK, network or package cache is touched.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
SCRIPT="$ROOT/scripts/dotnet-verify-packages.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Fake dotnet: logs its args; `restore` may rewrite a lock file (FAKE_RESTORE_EDIT);
# `nuget locals` prints the packages folder; `nuget verify` exits FAKE_VERIFY_EXIT.
FAKE="$WORK/dotnet"
cat > "$FAKE" <<'FAKE_EOF'
#!/usr/bin/env bash
echo "$* | CI=${CI:-} SIGVERIFY=${DOTNET_NUGET_SIGNATURE_VERIFICATION:-}" >> "$FAKE_LOG"
case "$1 ${2:-}" in
  "restore "*)
    if [ -n "${FAKE_RESTORE_FAIL_ONCE:-}" ] && [ ! -e "$FAKE_RESTORE_FAIL_ONCE" ]; then
      touch "$FAKE_RESTORE_FAIL_ONCE"
      echo "error NU1900: Error occurred while getting package vulnerability data: The SSL connection could not be established."
      exit 1
    fi
    if [ -n "${FAKE_RESTORE_EDIT:-}" ]; then echo '{"changed":true}' > "$FAKE_RESTORE_EDIT"; fi; exit "${FAKE_RESTORE_EXIT:-0}" ;;
  "nuget locals") echo "global-packages: $FAKE_PKGS/"; exit 0 ;;
  "nuget verify") printf '%b' "${FAKE_VERIFY_OUT:-}"; exit "${FAKE_VERIFY_EXIT:-0}" ;;
esac
exit 0
FAKE_EOF
chmod +x "$FAKE"
export DOTNET="$FAKE" FAKE_PKGS="$WORK/pkgs"
mkdir -p "$FAKE_PKGS/demo.pkg/1.2.3"
touch "$FAKE_PKGS/demo.pkg/1.2.3/demo.pkg.1.2.3.nupkg"

LOCK='{"version":2,"dependencies":{"net10.0":{"Demo.Pkg":{"type":"Direct","requested":"[1.2.3, 1.2.3]","resolved":"1.2.3","contentHash":"x"}}}}'

new_repo() {
  REPO="$WORK/repo-$RANDOM"; mkdir -p "$REPO"
  git -C "$REPO" init -q
  git -C "$REPO" config user.email t@example.com; git -C "$REPO" config user.name t
  echo '<Solution />' > "$REPO/AgenticWorkflow.slnx"
  echo '<configuration />' > "$REPO/nuget.config"
  export FAKE_LOG="$WORK/fake-$RANDOM.log"; : > "$FAKE_LOG"
  unset FAKE_RESTORE_EDIT FAKE_VERIFY_EXIT FAKE_VERIFY_OUT FAKE_RESTORE_EXIT FAKE_RESTORE_FAIL_ONCE
}
add_project() { # name, with-lock(yes|no), dir (default src), in-solution(yes|no, default yes)
  local dir="${3:-src}"
  mkdir -p "$REPO/$dir/$1"; echo '<Project Sdk="Microsoft.NET.Sdk" />' > "$REPO/$dir/$1/$1.csproj"
  if [ "$2" = yes ]; then echo "$LOCK" > "$REPO/$dir/$1/packages.lock.json"; fi
  if [ "${4:-yes}" = yes ]; then
    python3 - "$REPO/AgenticWorkflow.slnx" "$dir/$1/$1.csproj" <<'PY'
import sys
p, proj = sys.argv[1], sys.argv[2]
body = open(p).read().replace("<Solution />", "<Solution>\n</Solution>")
body = body.replace("</Solution>", '  <Folder Name="/x/">\n    <Project Path="%s" />\n  </Folder>\n</Solution>' % proj)
open(p, "w").write(body)
PY
  fi
  git -C "$REPO" add -A; git -C "$REPO" commit -qm "add $1"
}
run() { set +e; OUT="$(bash "$SCRIPT" "$REPO" 2>&1)"; CODE=$?; set -e; }

# --- no C# projects: a no-op success ---
new_repo; git -C "$REPO" add -A; git -C "$REPO" commit -qm init
run
check "no projects: exit 0" "$CODE" "0"
check "no projects: says so" "$(echo "$OUT" | grep -c 'no C# projects')" "1"

# --- a project without a lock file fails before any restore ---
new_repo; add_project Demo.Core no
run
check "missing lock: exit 1" "$CODE" "1"
check "missing lock: names the file" "$(echo "$OUT" | grep -c 'src/Demo.Core/packages.lock.json')" "1"
check "missing lock: no restore ran" "$(grep -c '^restore' "$FAKE_LOG" || true)" "0"

# --- only projects in AgenticWorkflow.slnx count: a spike project outside the solution needs no lock file ---
new_repo; add_project Demo.Core yes; add_project Spike.Probe no spikes no
run
check "project outside the solution: exit 0" "$CODE" "0"
check "project outside the solution: not named" "$(echo "$OUT" | grep -c 'Spike.Probe' || true)" "0"

# --- a solution project in a nested folder without a lock file still fails ---
new_repo; add_project Demo.Core yes; add_project Demo.Tests no tests
run
check "solution project without lock (tests/): exit 1" "$CODE" "1"
check "solution project without lock (tests/): names it" "$(echo "$OUT" | grep -c 'tests/Demo.Tests/packages.lock.json')" "1"

# --- only spike projects, none in the solution: no-op success ---
new_repo; add_project Spike.Only no spikes no
run
check "spikes only: exit 0" "$CODE" "0"
check "spikes only: says no C# projects" "$(echo "$OUT" | grep -c 'no C# projects')" "1"

# --- restore runs locked, in CI mode, with signature verification on; verify gets every locked package ---
new_repo; add_project Demo.Core yes
run
check "clean: exit 0" "$CODE" "0"
check "restore is locked with CI=true and signature verification" \
  "$(grep -c '^restore .*--locked-mode.* | CI=true SIGVERIFY=true' "$FAKE_LOG" || true)" "1"
check "verify gets the repo nuget.config and the locked package" \
  "$(grep -c "^nuget verify --all .*demo.pkg.1.2.3.nupkg.*--configfile $REPO/nuget.config" "$FAKE_LOG" || true)" "1"

# --- a lock file changed by restore fails ---
new_repo; add_project Demo.Core yes
export FAKE_RESTORE_EDIT="$REPO/src/Demo.Core/packages.lock.json"
run
check "lock drift: exit 1" "$CODE" "1"
check "lock drift: names the file" "$(echo "$OUT" | grep -c 'src/Demo.Core/packages.lock.json')" "1"

# --- a package not signed by a trusted signer fails ---
new_repo; add_project Demo.Core yes
export FAKE_VERIFY_EXIT=1 FAKE_VERIFY_OUT='Verifying Demo.Pkg.1.2.3\nerror: NU3034: This package is signed but not by a trusted signer.\n'
run
check "untrusted package: exit 1" "$CODE" "1"
check "untrusted package: says verification failed" "$(echo "$OUT" | grep -c 'signature verification failed')" "1"

# --- an unsigned package fails ---
new_repo; add_project Demo.Core yes
export FAKE_VERIFY_EXIT=1 FAKE_VERIFY_OUT='Verifying Demo.Pkg.1.2.3\nerror: NU3004: The package is not signed.\n'
run
check "unsigned package: exit 1" "$CODE" "1"

# --- only expired legacy signature timestamps (NU3037, NU3028): restore accepts them, so a warning, not a failure ---
new_repo; add_project Demo.Core yes
export FAKE_VERIFY_EXIT=1 FAKE_VERIFY_OUT='Verifying Demo.Pkg.1.2.3\nerror: NU3037: The author primary signature validity period has expired.\nerror: NU3028: The author primary signature timestamp found a chain building issue: ExplicitDistrust\n'
run
check "expired legacy timestamps only: exit 0" "$CODE" "0"
check "expired legacy timestamps only: warns" "$(echo "$OUT" | grep -c 'warning: .*NU3037')" "1"

# --- verify failing with no NU3 code at all fails closed ---
new_repo; add_project Demo.Core yes
export FAKE_VERIFY_EXIT=1 FAKE_VERIFY_OUT='something unexpected\n'
run
check "verify failure without a code: exit 1" "$CODE" "1"

# --- a locked package missing from the packages folder fails ---
new_repo
LOCK_SAVED="$LOCK"; LOCK="${LOCK//1.2.3/9.9.9}"; add_project Demo.Core yes; LOCK="$LOCK_SAVED"
run
check "missing nupkg: exit 1" "$CODE" "1"
check "missing nupkg: names it" "$(echo "$OUT" | grep -c 'demo.pkg 9.9.9')" "1"

# --- a missing solution file fails closed ---
new_repo; rm "$REPO/AgenticWorkflow.slnx"; git -C "$REPO" add -A; git -C "$REPO" commit -qm "no slnx"
run
check "missing slnx: exit 1" "$CODE" "1"
check "missing slnx: says so" "$(echo "$OUT" | grep -c 'AgenticWorkflow.slnx not found')" "1"

# --- a malformed solution file fails closed ---
new_repo; add_project Demo.Core yes
printf '<Solution>\n  <Folder Name="/x/">\n    <Project Path="src/Demo.Core/Demo.Core.csproj" />\n</Solution>\n' > "$REPO/AgenticWorkflow.slnx"
git -C "$REPO" commit -qam "mangled slnx"
run
check "malformed slnx: exit 1" "$CODE" "1"
check "malformed slnx: says it can't parse" "$(echo "$OUT" | grep -c 'cannot parse AgenticWorkflow.slnx')" "1"
check "malformed slnx: no restore ran" "$(grep -c '^restore' "$FAKE_LOG" || true)" "0"

# --- a corrupt lock file fails closed ---
new_repo; LOCK_SAVED="$LOCK"; LOCK='{"version":2,"dependencies":'; add_project Demo.Core yes; LOCK="$LOCK_SAVED"
run
check "corrupt lock: exit 1" "$CODE" "1"
check "corrupt lock: says it can't read the lock files" "$(echo "$OUT" | grep -c 'cannot read lock files')" "1"

# --- a failed restore fails, after exactly one retry ---
new_repo; add_project Demo.Core yes
export FAKE_RESTORE_EXIT=1
run
check "restore failure: exit 1" "$CODE" "1"
check "restore failure: tried twice" "$(grep -c '^restore' "$FAKE_LOG")" "2"

# --- an advisory-feed outage (NU1900) on the first restore is retried once and then passes ---
new_repo; add_project Demo.Core yes
export FAKE_RESTORE_FAIL_ONCE="$WORK/failed-once-$RANDOM"
run
check "NU1900 then success: exit 0" "$CODE" "0"
check "NU1900 then success: restore ran twice" "$(grep -c '^restore' "$FAKE_LOG")" "2"
check "NU1900 then success: says it retried" "$(echo "$OUT" | grep -c 'retrying once')" "1"

# --- allowed codes plus a failing line with no code: fails closed ---
new_repo; add_project Demo.Core yes
export FAKE_VERIFY_EXIT=1 FAKE_VERIFY_OUT='Verifying Demo.Pkg.1.2.3\nerror: NU3037: The author primary signature validity period has expired.\nerror: Could not load file or assembly; unexpected failure\n'
run
check "NU3037 plus an uncoded error: exit 1" "$CODE" "1"

# --- after the expired-timestamp warning, no "signature-verified" success claim ---
new_repo; add_project Demo.Core yes
export FAKE_VERIFY_EXIT=1 FAKE_VERIFY_OUT='Verifying Demo.Pkg.1.2.3\nerror: NU3037: The author primary signature validity period has expired.\n'
run
check "warning run: exit 0" "$CODE" "0"
check "warning run: no clean success line" "$(echo "$OUT" | grep -c 'restored and signature-verified$' || true)" "0"
check "warning run: success line names the warning" "$(echo "$OUT" | grep -c 'with expired legacy timestamps')" "1"

# --- a staged (not yet committed) lock file that restore leaves alone passes ---
new_repo; add_project Demo.Core yes; add_project Demo.Two no
echo "$LOCK" > "$REPO/src/Demo.Two/packages.lock.json"; git -C "$REPO" add src/Demo.Two/packages.lock.json
run
check "staged lock untouched by restore: exit 0" "$CODE" "0"

exit "$fail"
