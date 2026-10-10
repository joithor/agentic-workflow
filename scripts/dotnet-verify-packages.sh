#!/usr/bin/env bash
# dotnet-verify-packages.sh — the NuGet supply-chain check for CI (agent-readiness spec §4b.6, §4d).
#
# Usage: scripts/dotnet-verify-packages.sh [repo-root]     (DOTNET overrides the dotnet binary)
#
# Restore alone does not hold the line:
#   - locked mode fails only when package references drift; a missing lock file is silently created and a
#     hand-edited one is silently rewritten (and `--force-evaluate` bypasses locked mode entirely, so no CI
#     step may pass it);
#   - signatureValidationMode=require and trustedSigners in nuget.config are applied during restore only
#     when the client verifies signatures (off by default on macOS; DOTNET_NUGET_SIGNATURE_VERIFICATION=true
#     turns it on) and only to packages it extracts, so a warm package cache is never re-checked.
# So this script:
#   1. reads the projects from AgenticWorkflow.slnx (a missing or unparsable file fails) and fails if any of
#      them has no packages.lock.json in the git index next to it;
#   2. restores the solution with CI=true, --locked-mode and DOTNET_NUGET_SIGNATURE_VERIFICATION=true;
#   3. fails if restore changed any lock file (compared by content hash before and after);
#   4. runs `dotnet nuget verify --all` with the repo nuget.config over every package the lock files name.
#      The one carve-out: packages signed years ago can carry an expired signature whose timestamp chains
#      to a since-distrusted timestamp authority (NU3037, NU3028). Restore accepts those, so if EVERY
#      failing line carries one of those two codes it is a warning; anything else fails.
# Only projects in the solution count: projects outside it (spikes/) are never restored by CI.
# A parsed solution with no projects is a no-op success.
set -euo pipefail

ROOT="${1:-$(git rev-parse --show-toplevel)}"
ROOT="$(cd "$ROOT" && pwd)"
DOTNET="${DOTNET:-dotnet}"
cd "$ROOT"

fail() { echo "dotnet-verify-packages: $*" >&2; exit 1; }

[ -f AgenticWorkflow.slnx ] || fail "AgenticWorkflow.slnx not found in $ROOT"

# 1. Projects from the solution (<Project Path="..."/>, possibly inside <Folder>).
if ! projects="$(python3 -c '
import sys, xml.etree.ElementTree as ET
for el in ET.parse(sys.argv[1]).getroot().iter("Project"):
    path = el.get("Path", "").replace("\\", "/")
    if path:
        print(path)
' AgenticWorkflow.slnx 2>&1)"; then
  echo "$projects" >&2
  fail "cannot parse AgenticWorkflow.slnx"
fi
if [ -z "$projects" ]; then
  echo "dotnet-verify-packages: no C# projects in AgenticWorkflow.slnx; nothing to verify"
  exit 0
fi

locks=()
missing=0
while IFS= read -r proj; do
  lock="$(dirname "$proj")/packages.lock.json"
  if git ls-files --error-unmatch "$lock" >/dev/null 2>&1; then
    locks+=("$lock")
  else
    echo "dotnet-verify-packages: missing $lock — run \`dotnet restore\` locally and commit it" >&2
    missing=1
  fi
done <<< "$projects"
[ "$missing" -eq 0 ] || exit 1

hash_locks() { for f in "${locks[@]}"; do git hash-object "$f"; done; }

# 2. Locked, signature-verified restore.
before="$(hash_locks)"
CI=true DOTNET_NUGET_SIGNATURE_VERIFICATION=true \
  "$DOTNET" restore AgenticWorkflow.slnx --locked-mode

# 3. Restore must not have changed a lock file.
if [ "$(hash_locks)" != "$before" ]; then
  echo "dotnet-verify-packages: restore changed lock files; run \`dotnet restore\` locally and commit:" >&2
  git status --short -- "${locks[@]}" >&2
  exit 1
fi

# 4. Verify every locked package's signature against the repo's trusted signers.
if ! pkg_list="$(python3 -c '
import json, sys
seen = set()
for path in sys.argv[1:]:
    for deps in json.load(open(path))["dependencies"].values():
        for pid, d in deps.items():
            if d.get("type") != "Project" and d.get("resolved"):
                seen.add((pid, d["resolved"]))
for pid, ver in sorted(seen):
    print(pid, ver)
' "${locks[@]}" 2>&1)"; then
  echo "$pkg_list" >&2
  fail "cannot read lock files"
fi

pkgs_dir="$("$DOTNET" nuget locals global-packages --list | sed -n 's/^global-packages: //p')"
pkgs_dir="${pkgs_dir%/}"
nupkgs=()
absent=0
while IFS=' ' read -r id version; do
  [ -n "$id" ] || continue
  lid="$(printf '%s' "$id" | tr '[:upper:]' '[:lower:]')"
  lver="$(printf '%s' "$version" | tr '[:upper:]' '[:lower:]')"
  nupkg="$pkgs_dir/$lid/$lver/$lid.$lver.nupkg"
  if [ -f "$nupkg" ]; then
    nupkgs+=("$nupkg")
  else
    echo "dotnet-verify-packages: $lid $lver is locked but not in $pkgs_dir" >&2
    absent=1
  fi
done <<< "$pkg_list"
[ "$absent" -eq 0 ] || exit 1
[ "${#nupkgs[@]}" -gt 0 ] || { echo "dotnet-verify-packages: lock files name no packages"; exit 0; }

set +e
verify_out="$("$DOTNET" nuget verify --all "${nupkgs[@]}" --configfile "$ROOT/nuget.config" 2>&1)"
verify_code=$?
set -e
printf '%s\n' "$verify_out"
if [ "$verify_code" -eq 0 ]; then
  echo "dotnet-verify-packages: ${#nupkgs[@]} packages locked, restored and signature-verified"
  exit 0
fi
# Every failing line must carry an allowed code; a failure with no allowed code (or no code) fails closed.
failing="$(printf '%s\n' "$verify_out" | grep -iE '^[[:space:]]*error' || true)"
disallowed="$(printf '%s\n' "$failing" | grep . | grep -vE 'NU3037|NU3028' || true)"
if [ -z "$failing" ] || [ -n "$disallowed" ]; then
  printf '%s\n' "${disallowed:-(nuget verify failed with no error line)}" >&2
  fail "signature verification failed (see above)"
fi
codes="$(printf '%s\n' "$failing" | grep -oE 'NU3[0-9]{3}' | sort -u | tr '\n' ' ')"
echo "dotnet-verify-packages: warning: expired legacy signature timestamps only (${codes% }); restore accepts these" >&2
echo "dotnet-verify-packages: ${#nupkgs[@]} packages locked, restored and signature-verified with expired legacy timestamps"
