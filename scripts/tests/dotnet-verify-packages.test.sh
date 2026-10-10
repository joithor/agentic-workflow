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
  "restore "*) if [ -n "${FAKE_RESTORE_EDIT:-}" ]; then echo '{"changed":true}' > "$FAKE_RESTORE_EDIT"; fi; exit 0 ;;
  "nuget locals") echo "global-packages: $FAKE_PKGS/"; exit 0 ;;
  "nuget verify") exit "${FAKE_VERIFY_EXIT:-0}" ;;
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
  unset FAKE_RESTORE_EDIT FAKE_VERIFY_EXIT
}
add_project() { # name, with-lock(yes|no)
  mkdir -p "$REPO/src/$1"; echo '<Project Sdk="Microsoft.NET.Sdk" />' > "$REPO/src/$1/$1.csproj"
  if [ "$2" = yes ]; then echo "$LOCK" > "$REPO/src/$1/packages.lock.json"; fi
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

# --- a package that fails signature verification fails ---
new_repo; add_project Demo.Core yes
export FAKE_VERIFY_EXIT=1
run
check "untrusted package: exit 1" "$CODE" "1"
check "untrusted package: says verification failed" "$(echo "$OUT" | grep -c 'signature verification failed')" "1"

# --- a locked package missing from the packages folder fails ---
new_repo
LOCK_SAVED="$LOCK"; LOCK="${LOCK//1.2.3/9.9.9}"; add_project Demo.Core yes; LOCK="$LOCK_SAVED"
run
check "missing nupkg: exit 1" "$CODE" "1"
check "missing nupkg: names it" "$(echo "$OUT" | grep -c 'demo.pkg 9.9.9')" "1"

exit "$fail"
