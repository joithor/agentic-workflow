#!/usr/bin/env bash
# dotnet-verify-packages.sh — the NuGet supply-chain check for CI (agent-readiness spec §4b.6, §4d).
#
# Usage: scripts/dotnet-verify-packages.sh [repo-root]     (DOTNET overrides the dotnet binary)
#
# Restore alone does not hold the line:
#   - locked mode fails only when package references drift; a missing lock file is silently created and a
#     hand-edited one is silently rewritten;
#   - signatureValidationMode=require and trustedSigners in nuget.config are applied during restore only
#     when the client verifies signatures. That is off by default on macOS and switched on by
#     DOTNET_NUGET_SIGNATURE_VERIFICATION=true.
# So this script:
#   1. fails if any committed .csproj has no packages.lock.json next to it;
#   2. restores the solution with CI=true, --locked-mode and DOTNET_NUGET_SIGNATURE_VERIFICATION=true;
#   3. fails if restore changed any lock file;
#   4. runs `dotnet nuget verify --all` with the repo nuget.config over every package the lock files name,
#      so an untrusted or unsigned package fails even where restore didn't verify.
# With no C# projects it reports a no-op success.
set -euo pipefail

ROOT="${1:-$(git rev-parse --show-toplevel)}"
ROOT="$(cd "$ROOT" && pwd)"
DOTNET="${DOTNET:-dotnet}"
cd "$ROOT"

projects="$(git ls-files '*.csproj')"
if [ -z "$projects" ]; then
  echo "dotnet-verify-packages: no C# projects; nothing to verify"
  exit 0
fi

# 1. Every project has a committed lock file.
missing=0
while IFS= read -r proj; do
  lock="$(dirname "$proj")/packages.lock.json"
  if ! git ls-files --error-unmatch "$lock" >/dev/null 2>&1; then
    echo "dotnet-verify-packages: missing $lock — run \`dotnet restore\` locally and commit it" >&2
    missing=1
  fi
done <<< "$projects"
[ "$missing" -eq 0 ] || exit 1

# 2. Locked, signature-verified restore.
CI=true DOTNET_NUGET_SIGNATURE_VERIFICATION=true \
  "$DOTNET" restore AgenticWorkflow.slnx --locked-mode

# 3. Restore must not have changed a committed lock file.
changed="$(git status --porcelain -- '*packages.lock.json')"
if [ -n "$changed" ]; then
  echo "dotnet-verify-packages: restore changed lock files; run \`dotnet restore\` locally and commit:" >&2
  echo "$changed" >&2
  exit 1
fi

# 4. Verify every locked package's signature against the repo's trusted signers.
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
done < <(git ls-files '*packages.lock.json' | python3 -c '
import json, sys
seen = set()
for path in sys.stdin.read().split():
    for deps in json.load(open(path))["dependencies"].values():
        for pid, d in deps.items():
            if d.get("type") != "Project" and d.get("resolved"):
                seen.add((pid, d["resolved"]))
for pid, ver in sorted(seen):
    print(pid, ver)
')
[ "$absent" -eq 0 ] || exit 1
[ "${#nupkgs[@]}" -gt 0 ] || { echo "dotnet-verify-packages: lock files name no packages"; exit 0; }

# `nuget verify` is stricter than restore about age: packages signed years ago can carry an expired
# signature whose timestamp chains to a since-distrusted timestamp authority (NU3037, NU3028). Restore
# accepts those, so they are warnings here. Any other failure (NU3034 untrusted signer, NU3004 unsigned,
# NU3008 tampered, or a failure with no NU3 code at all) fails the check.
set +e
verify_out="$("$DOTNET" nuget verify --all "${nupkgs[@]}" --configfile "$ROOT/nuget.config" 2>&1)"
verify_code=$?
set -e
printf '%s\n' "$verify_out"
if [ "$verify_code" -ne 0 ]; then
  codes="$(printf '%s\n' "$verify_out" | grep -oE 'NU3[0-9]{3}' | sort -u || true)"
  hard="$(printf '%s\n' "$codes" | grep -vE '^(NU3037|NU3028)$' | grep . || true)"
  if [ -z "$codes" ] || [ -n "$hard" ]; then
    echo "dotnet-verify-packages: signature verification failed (${hard:-no NU3 code}; see above)" >&2
    exit 1
  fi
  echo "dotnet-verify-packages: warning: expired legacy signature timestamps only ($(echo $codes)); restore accepts these" >&2
fi
echo "dotnet-verify-packages: ${#nupkgs[@]} packages locked, restored and signature-verified"
