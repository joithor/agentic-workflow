#!/usr/bin/env bash
# Proves the secret-scan pre-commit hook refuses a secret-shaped string. Runs in a
# throwaway repo with the global git config ignored, so nothing touches this repo.
set -euo pipefail
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
SCRATCH="$(mktemp -d)"
trap 'rm -rf "$SCRATCH"' EXIT
git -C "$SCRATCH" init -q
sindri scrub --install-pre-commit --repo "$SCRATCH"
printf 'k = "%s%s"\n' "AKIA" "ABCDEFGHIJKLMNOP" > "$SCRATCH/fixture.txt"
git -C "$SCRATCH" add fixture.txt
if out="$(git -C "$SCRATCH" -c user.name=proof -c user.email=proof@example.invalid commit -qm "must be refused" 2>&1)"; then
  echo "FAIL: the hook let a secret-shaped string through"
  exit 1
fi
if ! grep -q "SND-SCRUB-002" <<<"$out"; then
  echo "FAIL: the commit was refused, but not by the secret scan:"
  echo "$out"
  exit 1
fi
echo "PASS: the pre-commit hook refused the fixture (SND-SCRUB-002)"
